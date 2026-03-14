using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.JudgeDredd.Api.Contracts;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Server.ApiServer.Endpoints;
using SGL.JudgeDredd.Server.ApiServer.Middleware;
using SGL.JudgeDredd.Server.ApiServer.Services;
using SGL.JudgeDredd.Server.ClientManagement;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.ApiServer;

/// <summary>
/// Embedded Kestrel HTTP API server that runs as an IHostedService inside the WPF application.
/// Listens on configurable port (default 5000 for Cloudflare tunnel + 7743 legacy) and serves all client-server API endpoints.
/// Supports stop/restart — each StartAsync cleanly rebuilds the web application pipeline.
/// </summary>
public sealed class JudgeDreddApiServer : IHostedService, IDisposable
{
    private readonly IServiceProvider _appServices;
    private readonly int _port;
    private readonly int _legacyPort;
    private readonly string _listenAddress;
    private WebApplication? _webApp;
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private bool _isRunning;
    private string? _startupError;

    public DateTime StartedAt => _startedAt;
    public bool IsRunning => _isRunning;
    public string? StartupError => _startupError;

    public JudgeDreddApiServer(IServiceProvider appServices, int port = ApiConstants.DefaultPort,
        string listenAddress = "0.0.0.0", int legacyPort = ApiConstants.LegacyPort)
    {
        _appServices = appServices;
        _port = port;
        _listenAddress = listenAddress;
        _legacyPort = legacyPort;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // If already running, stop first to allow clean restart
        if (_isRunning && _webApp != null)
        {
            SglLogger.Information("Server already running — stopping for restart...");
            await StopAsync(cancellationToken);
        }

        _startupError = null;
        SglLogger.Information("Starting SyntheticAI API server on {Address}:{Port} (legacy {LegacyPort})...",
            _listenAddress, _port, _legacyPort);

        try
        {
            var builder = WebApplication.CreateSlimBuilder();

            // Suppress default ASP.NET Core logging noise — we use SglLogger
            builder.Logging.ClearProviders();

            builder.WebHost.ConfigureKestrel(options =>
            {
                // Parse the listen address so it respects the configuration.
                // "0.0.0.0" -> IPAddress.Any (all interfaces), "127.0.0.1" -> loopback only.
                var ip = IPAddress.Parse(_listenAddress);
                options.Listen(ip, _port);       // Primary port (5000) - Cloudflare tunnel
                if (_legacyPort != _port)
                    options.Listen(ip, _legacyPort); // Legacy port (7743) - direct/backward compat

                // Increase limits for file transfers (APK download etc.)
                options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // 200MB
            });

            // Register services from the parent DI container
            var clientTracker = _appServices.GetRequiredService<ConnectedClientTracker>();
            var clientDataStore = _appServices.GetRequiredService<ClientDataStore>();
            builder.Services.AddSingleton(clientTracker);
            builder.Services.AddSingleton(clientDataStore);

            // Load previously registered clients from disk so they survive server restarts
            await clientTracker.LoadFromDiskAsync(clientDataStore);

            // Register JwtService as singleton (creates or loads signing key)
            var jwtService = new JwtService();
            builder.Services.AddSingleton(jwtService);

            // Register UserAccountStore as singleton for persistent account storage
            var userAccountStore = new UserAccountStore();
            builder.Services.AddSingleton(userAccountStore);

            // Register WebSocketGateway as singleton
            var wsGateway = new WebSocketGateway(jwtService);
            builder.Services.AddSingleton(wsGateway);

            // Use CloudflareService from parent DI if available, otherwise create new
            var cloudflareService = _appServices.GetService<CloudflareService>() ?? new CloudflareService(_port, null);
            builder.Services.AddSingleton(cloudflareService);

            // Try to register ILlmService and IKnowledgeBase if available
            var llmService = _appServices.GetService(typeof(Core.Interfaces.ILlmService));
            if (llmService != null)
                builder.Services.AddSingleton(typeof(Core.Interfaces.ILlmService), llmService);

            var knowledgeBase = _appServices.GetService(typeof(Core.Interfaces.IKnowledgeBase));
            if (knowledgeBase != null)
                builder.Services.AddSingleton(typeof(Core.Interfaces.IKnowledgeBase), knowledgeBase);

            // Register LLM Model Manager service for model download endpoints
            // Resolve the LLM folder by walking up from the base directory
            var llmRoot = ResolveLlmRoot();
            var llmModelManagerService = new LlmModelManagerService(llmRoot);
            builder.Services.AddSingleton(llmModelManagerService);

            // Register Multi-LLM Manager for up to 4 concurrent model slots
            var multiLlmManager = new LLM.MultiLlmManager(llmRoot);
            builder.Services.AddSingleton(multiLlmManager);

            // Register Hardware Monitor service for real-time server telemetry
            var hardwareMonitor = new HardwareMonitorService();
            hardwareMonitor.Start();
            builder.Services.AddSingleton(hardwareMonitor);

            // Register Website Metrics service for visitor tracking
            var websiteMetrics = new WebsiteMetricsService();
            builder.Services.AddSingleton(websiteMetrics);

            // Register Swarm Distributed Learning services
            var seedService = new Services.Swarm.SeedService();
            builder.Services.AddSingleton(seedService);
            var swarmScheduler = new Services.Swarm.SwarmScheduler();
            // Wire real LLM service into swarm scheduler for actual inference
            if (llmService is Core.Interfaces.ILlmService typedLlm)
                swarmScheduler.SetLlmService(typedLlm);
            builder.Services.AddSingleton(swarmScheduler);
            var knowledgeGraph = new Services.Swarm.KnowledgeGraphService(seedService);
            builder.Services.AddSingleton(knowledgeGraph);

            // Wire knowledge graph and image gen into swarm scheduler for real operations
            swarmScheduler.SetKnowledgeGraph(knowledgeGraph);

            // Pass the server instance itself for status endpoint
            builder.Services.AddSingleton(this);

            // Try to register ITtsService if available from parent DI
            var ttsService = _appServices.GetService(typeof(ITtsService));
            if (ttsService != null)
                builder.Services.AddSingleton(typeof(ITtsService), ttsService);

            // Register Image Generation service (connects to local SD WebUI server)
            var imageGenService = new LLM.ImageGenerationService();
            builder.Services.AddSingleton(imageGenService);

            // Wire image generation into swarm scheduler for image pipeline operations
            swarmScheduler.SetImageGenService(imageGenService);

            // Register Hierarchical Cluster Service (3-layer swarm architecture)
            var clusterService = new Services.Swarm.HierarchicalClusterService();
            builder.Services.AddSingleton(clusterService);

            // Register Breach Detection Service (dark web monitoring)
            var breachService = new Services.BreachDetectionService();
            builder.Services.AddSingleton<Core.Interfaces.IBreachDetectionService>(breachService);

            // Register Digital Immune System (5-layer defense)
            var immuneSystem = new Services.Swarm.DigitalImmuneSystem();
            builder.Services.AddSingleton(immuneSystem);

            // Register Self-Healing Network with cross-service wiring
            var selfHealing = new Services.Swarm.SelfHealingNetwork();
            selfHealing.SetClusterService(clusterService);
            selfHealing.SetSwarmScheduler(swarmScheduler);
            selfHealing.SetImmuneSystem(immuneSystem);
            builder.Services.AddSingleton(selfHealing);

            // Register Research LLM Agent with auto-mount capabilities
            var researchAgent = new Services.Swarm.ResearchLlmAgent();
            researchAgent.SetMultiLlm(multiLlmManager);
            if (llmService is Core.Interfaces.ILlmService researchLlm)
                researchAgent.SetLlmService(researchLlm);
            researchAgent.SetSeedService(seedService);
            researchAgent.SetKnowledgeGraph(knowledgeGraph);
            researchAgent.SetImmuneSystem(immuneSystem);
            builder.Services.AddSingleton(researchAgent);

            _webApp = builder.Build();

            // Ensure data download directories exist so endpoints don't 404 on missing folders
            var dataRoot = Path.Combine(AppContext.BaseDirectory, "data");
            foreach (var sub in new[] { "client", "mobile", "linux-client", "copilot", "website", "developers", "dm_store", "chat_history" })
            {
                var dir = Path.Combine(dataRoot, sub);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            // Enable WebSocket support
            _webApp.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });

            // Track all requests for DDoS detection (must be before auth middleware)
            _webApp.UseMiddleware<Middleware.RequestTrackingMiddleware>();

            // Add JWT auth middleware (replaces the old API key auth middleware)
            _webApp.UseMiddleware<JwtAuthMiddleware>();

            // ── Static website serving ──
            // Serves the built React website from data/website/ folder
            // When browsers visit the server URL, they see the marketing site
            // API clients continue to use /api/v1/* endpoints as before
            var websiteDir = Path.Combine(AppContext.BaseDirectory, "data", "website");
            var hasWebsite = Directory.Exists(websiteDir) &&
                             File.Exists(Path.Combine(websiteDir, "index.html"));

            if (hasWebsite)
            {
                var fileProvider = new PhysicalFileProvider(websiteDir);
                _webApp.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
                _webApp.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = fileProvider,
                    ServeUnknownFileTypes = false
                });
                SglLogger.Information("Website serving enabled from {Dir}", websiteDir);
            }
            else
            {
                // No website deployed — serve JSON health at root for backward compat
                _webApp.MapGet("/", () => Results.Ok(new
                {
                    service = "SGL-AI SyntheticAI Server",
                    status = "healthy",
                    version = SGL.JudgeDredd.Shared.VersionInfo.ServerVersion,
                    timestamp = DateTime.UtcNow
                }));
            }

            // Health endpoint always available (Cloudflare tunnel health probes)
            _webApp.MapGet("/health", () => Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow
            }));

            // Map WebSocket gateway endpoint
            _webApp.Map(ApiConstants.WebSocketGateway, async (HttpContext context) =>
            {
                var gateway = context.RequestServices.GetRequiredService<WebSocketGateway>();
                await gateway.HandleConnectionAsync(context);
            });

            // Map all REST endpoints
            ClientEndpoints.Map(_webApp);
            SignatureEndpoints.Map(_webApp);
            ThreatAnalysisEndpoints.Map(_webApp);
            LogEndpoints.Map(_webApp);
            ServerStatusEndpoints.Map(_webApp);
            CommandEndpoints.Map(_webApp);
            LlmModelEndpoints.Map(_webApp);
            ThreatKnowledgeEndpoints.Map(_webApp);
            FaqEndpoints.Map(_webApp);
            BroadcastEndpoints.Map(_webApp);
            ChatEndpoints.Map(_webApp);
            TtsEndpoints.Map(_webApp);
            AdminMetricsEndpoints.Map(_webApp);
            BroadcastNotificationEndpoints.Map(_webApp);
            CopilotEndpoints.Map(_webApp);
            VpnServerEndpoints.Map(_webApp);
            MultiLlmEndpoints.Map(_webApp);
            HardwareMonitorEndpoints.Map(_webApp);
            WebsiteContentEndpoints.Map(_webApp);
            LlmBackupEndpoints.Map(_webApp);
            ImageGenEndpoints.Map(_webApp);
            SwarmEndpoints.Map(_webApp);
            ClusterEndpoints.MapClusterEndpoints(_webApp);
            DeveloperEndpoints.Map(_webApp);
            BreachDetectionEndpoints.Map(_webApp);
            ChatRoomEndpoints.Map(_webApp);

            // SPA fallback — for React Router client-side routing
            // Any non-API, non-file path falls back to index.html
            if (hasWebsite)
            {
                _webApp.MapFallback(async context =>
                {
                    if (!context.Request.Path.StartsWithSegments("/api") &&
                        !context.Request.Path.StartsWithSegments("/ws") &&
                        !context.Request.Path.StartsWithSegments("/health"))
                    {
                        var indexPath = Path.Combine(websiteDir, "index.html");
                        if (File.Exists(indexPath))
                        {
                            context.Response.ContentType = "text/html";
                            await context.Response.SendFileAsync(indexPath);
                            return;
                        }
                    }
                    context.Response.StatusCode = 404;
                });
            }

            await _webApp.StartAsync(cancellationToken);
            _isRunning = true;

            SglLogger.Information("SyntheticAI API server STARTED on http://{Address}:{Port} (+ legacy port {LegacyPort})",
                _listenAddress, _port, _legacyPort);
            SglLogger.Information("WebSocket gateway available at ws://{Address}:{Port}{Path}",
                _listenAddress, _port, ApiConstants.WebSocketGateway);

            // After server is confirmed listening, ensure cloudflared and firewall
            _ = Task.Run(async () =>
            {
                try
                {
                    cloudflareService.EnsureFirewallPort(_port);
                    if (_legacyPort != _port)
                        cloudflareService.EnsureFirewallPort(_legacyPort);
                    cloudflareService.EnsureCloudflaredRunning();

                    // Verify localhost is reachable
                    await Task.Delay(1000, CancellationToken.None);
                    var reachable = cloudflareService.IsLocalPortReachable();
                    SglLogger.Information("Post-startup port check: localhost:{Port} reachable = {Reachable}", _port, reachable);
                }
                catch (Exception ex)
                {
                    SglLogger.Error("Post-startup cloudflare/firewall setup: " + ex.Message);
                }
            });
        }
        catch (Exception ex)
        {
            _startupError = ex.Message;
            _isRunning = false;
            SglLogger.Error("CRITICAL: SyntheticAI API server FAILED to start: " + ex.Message);
            SglLogger.Error("Full exception: " + ex.ToString());

            // Write crash details to disk for diagnostics
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "server-crash.log"),
                    $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SERVER START FAILED:\n{ex}\n");
            }
            catch { }

            // Don't rethrow — the WPF app should survive even if the server fails
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        SglLogger.Information("Stopping SyntheticAI API server...");

        if (_webApp != null)
        {
            try
            {
                await _webApp.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                SglLogger.Error("Error stopping web app: " + ex.Message);
            }

            try
            {
                await _webApp.DisposeAsync();
            }
            catch (Exception ex)
            {
                SglLogger.Error("Error disposing web app: " + ex.Message);
            }

            _webApp = null;
        }

        _isRunning = false;
        SglLogger.Information("SyntheticAI API server stopped.");
    }

    /// <summary>
    /// Walks up from the binary output directory to find the LLM/ models folder.
    /// </summary>
    private static string ResolveLlmRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var llmDir = Path.Combine(dir, "LLM");
            if (Directory.Exists(llmDir)) return llmDir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        // Check Program Files installed location
        var programFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "SGL SyntheticAI Server", "LLM");
        if (Directory.Exists(programFilesPath)) return programFilesPath;

        // Fallback to a default location
        return Path.Combine(AppContext.BaseDirectory, "LLM");
    }

    public void Dispose()
    {
        if (_webApp != null)
        {
            _webApp.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            _webApp = null;
        }
        _isRunning = false;
    }
}
