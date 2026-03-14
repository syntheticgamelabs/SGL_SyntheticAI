#pragma warning disable CA1416 // Platform compatibility - this is a Windows-only server
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.ServiceProcess;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server;

/// <summary>
/// Manages the Cloudflare tunnel (cloudflared) Windows service,
/// monitors tunnel health, ensures firewall ports are open,
/// and provides DDoS indicator detection for the SyntheticAI API server.
/// </summary>
public sealed class CloudflareService : IDisposable
{
    private readonly int _port;
    private readonly string? _publicDomain;
    private readonly CancellationTokenSource _cts = new();
    private Timer? _healthTimer;

    // ── Public status properties ──────────────────────────────────────
    public bool IsCloudflaredRunning { get; private set; }
    public bool IsTunnelHealthy { get; private set; }
    public int TunnelConnections { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public string LastInstallLog { get; private set; } = string.Empty;

    /// <summary>
    /// Indicates that while the service and port are running locally,
    /// the actual end-to-end tunnel connectivity has NOT been verified.
    /// True means "locally OK but no proof traffic flows through Cloudflare."
    /// </summary>
    public bool IsEndToEndVerified { get; private set; }

    // ── DDoS tracking ────────────────────────────────────────────────
    private readonly ConcurrentQueue<DateTime> _requestTimestamps = new();
    private int _totalRequests;
    private int _errorResponses;
    public bool IsDdosDetected { get; private set; }
    public double RequestsPerSecond { get; private set; }
    public double ErrorRatio { get; private set; }

    public CloudflareService(int port = 5000, string? publicDomain = null)
    {
        _port = port;
        _publicDomain = publicDomain;

        // Poll tunnel health every 15 seconds
        _healthTimer = new Timer(_ => RefreshStatus(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
    }

    // ──────────────────────────────────────────────────────────────────
    //  Cloudflared Windows Service management
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if the "Cloudflared" (or "cloudflared") Windows service is installed and running.
    /// </summary>
    public bool CheckCloudflaredServiceRunning()
    {
        try
        {
            using var sc = new ServiceController("Cloudflared");
            IsCloudflaredRunning = sc.Status == ServiceControllerStatus.Running;
            return IsCloudflaredRunning;
        }
        catch (InvalidOperationException)
        {
            // Service not installed
            IsCloudflaredRunning = false;
            return false;
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to query Cloudflared service status.", ex);
            IsCloudflaredRunning = false;
            return false;
        }
    }

    /// <summary>
    /// Starts the Cloudflared Windows service if it is installed but not running.
    /// </summary>
    public void EnsureCloudflaredRunning()
    {
        try
        {
            using var sc = new ServiceController("Cloudflared");

            if (sc.Status == ServiceControllerStatus.Running)
            {
                SglLogger.Information("Cloudflared service is already running.");
                IsCloudflaredRunning = true;
                return;
            }

            if (sc.Status == ServiceControllerStatus.Stopped ||
                sc.Status == ServiceControllerStatus.Paused)
            {
                SglLogger.Information("Starting Cloudflared service...");
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                IsCloudflaredRunning = true;
                SglLogger.Information("Cloudflared service started successfully.");
            }
        }
        catch (InvalidOperationException)
        {
            SglLogger.Warning("Cloudflared service is not installed on this machine. Tunnel will not be available.");
            IsCloudflaredRunning = false;
            LastError = "Cloudflared service not installed. Run 'cloudflared.exe service install <token>' to install.";
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            SglLogger.Error("Failed to start Cloudflared service (access denied or service error).", ex);
            IsCloudflaredRunning = false;
            LastError = $"Access denied starting Cloudflared: {ex.Message}";
        }
        catch (Exception ex)
        {
            SglLogger.Error("Unexpected error starting Cloudflared service.", ex);
            IsCloudflaredRunning = false;
            LastError = ex.Message;
        }
    }

    /// <summary>
    /// Installs the cloudflared Windows service with the given tunnel token.
    /// This replaces any existing cloudflared service configuration.
    /// If cloudflared.exe is not found, attempts to install from the bundled MSI.
    /// </summary>
    public async Task<(bool success, string message)> InstallCloudflaredService(string tunnelToken)
    {
        var log = new System.Text.StringBuilder();
        void Log(string msg)
        {
            log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            SglLogger.Information(msg);
        }

        if (string.IsNullOrWhiteSpace(tunnelToken))
        {
            LastInstallLog = "Tunnel token is empty.";
            return (false, "Tunnel token is empty.");
        }

        try
        {
            // Find cloudflared.exe
            Log("Searching for cloudflared executable...");
            var cloudflaredPath = FindCloudflaredExe();

            if (cloudflaredPath == null)
            {
                Log("cloudflared executable not found on disk or PATH. Checking for bundled MSI...");

                // Try MSI fallback installation
                var msiPath = FindCloudflaredMsi();
                if (msiPath != null)
                {
                    Log($"Found MSI at: {msiPath}. Attempting silent install...");
                    var msiResult = RunProcess("msiexec", $"/i \"{msiPath}\" /quiet /norestart", timeoutMs: 120_000);
                    Log($"MSI install exit code: {msiResult.exitCode}, output: {msiResult.output}");

                    if (msiResult.exitCode == 0)
                    {
                        Log("MSI install succeeded. Waiting for install to finalize...");
                        await Task.Delay(3000);

                        // Retry finding the exe after MSI install
                        cloudflaredPath = FindCloudflaredExe();
                        if (cloudflaredPath != null)
                        {
                            Log($"cloudflared found after MSI install at: {cloudflaredPath}");
                        }
                        else
                        {
                            var msg = "MSI installed but cloudflared.exe still not found on disk.";
                            Log(msg);
                            LastInstallLog = log.ToString();
                            return (false, msg);
                        }
                    }
                    else
                    {
                        var msg = $"MSI install failed (exit code {msiResult.exitCode}): {msiResult.output}";
                        Log(msg);
                        LastInstallLog = log.ToString();
                        return (false, msg);
                    }
                }
                else
                {
                    var msg = "cloudflared.exe not found and no MSI installer available. Please install Cloudflare WARP/cloudflared.";
                    Log(msg);
                    LastInstallLog = log.ToString();
                    return (false, msg);
                }
            }
            else
            {
                Log($"Found cloudflared at: {cloudflaredPath}");
            }

            Log("Uninstalling existing cloudflared service (if any)...");
            var uninstall = RunProcess(cloudflaredPath, "service uninstall", timeoutMs: 15000);
            Log($"Uninstall result: exit={uninstall.exitCode}, output={uninstall.output}");

            // Install with new token
            Log("Installing cloudflared service with tunnel token...");
            var result = RunProcess(cloudflaredPath, $"service install {tunnelToken}", timeoutMs: 30000);
            Log($"Install result: exit={result.exitCode}, output={result.output}");

            if (result.exitCode == 0 || result.output.Contains("installed", StringComparison.OrdinalIgnoreCase))
            {
                Log("Cloudflared service installed successfully. Waiting before starting...");

                // Start the service
                await Task.Delay(2000);
                EnsureCloudflaredRunning();

                var finalMsg = "Cloudflared service installed and started.";
                Log(finalMsg);
                LastInstallLog = log.ToString();
                return (true, finalMsg);
            }

            var failMsg = $"Install returned exit code {result.exitCode}: {result.output}";
            Log(failMsg);
            LastInstallLog = log.ToString();
            return (false, failMsg);
        }
        catch (Exception ex)
        {
            var errMsg = $"Install failed: {ex.Message}";
            log.AppendLine($"[{DateTime.Now:HH:mm:ss}] EXCEPTION: {ex}");
            SglLogger.Error("Failed to install cloudflared service.", ex);
            LastInstallLog = log.ToString();
            return (false, errMsg);
        }
    }

    /// <summary>
    /// Locates cloudflared.exe (or cloudflared-windows-amd64.exe) on the system.
    /// Searches common install paths, bundled locations, and the system PATH.
    /// </summary>
    private static string? FindCloudflaredExe()
    {
        var baseDir = AppContext.BaseDirectory;

        // Resolve project root by walking up from base directory
        // (base is typically bin/Debug/net8.0 or similar)
        string? projectRoot = null;
        try
        {
            var dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "src")) ||
                    Directory.Exists(Path.Combine(dir.FullName, "Web Documentation")) ||
                    Directory.Exists(Path.Combine(dir.FullName, "Toolset")))
                {
                    projectRoot = dir.FullName;
                    break;
                }
                dir = dir.Parent;
            }
        }
        catch { }

        var candidates = new List<string>
        {
            // Original "cloudflared.exe" paths
            Path.Combine(baseDir, "cloudflared.exe"),
            Path.Combine(baseDir, "Toolset", "cloudflared.exe"),

            // Actual binary name: cloudflared-windows-amd64.exe
            Path.Combine(baseDir, "cloudflared-windows-amd64.exe"),
            Path.Combine(baseDir, "Toolset", "cloudflared-windows-amd64.exe"),

            // Bundled in Web Documentation folder (relative to base dir)
            Path.Combine(baseDir, "Web Documentation", "Cloudflared", "cloudflared-windows-amd64.exe"),

            // Standard Windows install locations
            @"C:\Program Files\Cloudflare\cloudflared.exe",
            @"C:\Program Files (x86)\Cloudflare\cloudflared.exe",
            @"C:\Program Files\cloudflared\cloudflared.exe",
            @"C:\ProgramData\cloudflared\cloudflared.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "cloudflared", "cloudflared.exe"),
        };

        // Add project root paths if resolved
        if (projectRoot != null)
        {
            candidates.Add(Path.Combine(projectRoot, "Web Documentation", "Cloudflared", "cloudflared-windows-amd64.exe"));
            candidates.Add(Path.Combine(projectRoot, "Toolset", "cloudflared-windows-amd64.exe"));
            candidates.Add(Path.Combine(projectRoot, "Toolset", "cloudflared.exe"));
        }

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        // Try PATH lookup for both binary names
        foreach (var exeName in new[] { "cloudflared.exe", "cloudflared-windows-amd64.exe" })
        {
            try
            {
                var psi = new ProcessStartInfo("where", exeName)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(5000);
                    if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        var first = output.Split('\n')[0].Trim();
                        if (File.Exists(first))
                            return first;
                    }
                }
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// Locates the bundled cloudflared MSI installer for fallback installation.
    /// </summary>
    private static string? FindCloudflaredMsi()
    {
        var baseDir = AppContext.BaseDirectory;

        var candidates = new List<string>
        {
            Path.Combine(baseDir, "Toolset", "cloudflared-windows-amd64.msi"),
            Path.Combine(baseDir, "cloudflared-windows-amd64.msi"),
        };

        // Walk up to find project root
        try
        {
            var dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                var toolsetMsi = Path.Combine(dir.FullName, "Toolset", "cloudflared-windows-amd64.msi");
                if (File.Exists(toolsetMsi))
                    return toolsetMsi;
                dir = dir.Parent;
            }
        }
        catch { }

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static (int exitCode, string output) RunProcess(string fileName, string arguments, int timeoutMs = 10000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                return (-1, "Failed to start process.");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(timeoutMs);

            var combined = string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}";
            return (process.ExitCode, combined.Trim());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    /// <summary>
    /// Returns a multi-line diagnostic report about the cloudflared tunnel configuration and status.
    /// </summary>
    public string DiagnosticInfo
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Cloudflare Tunnel Diagnostic Report ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 1. Executable location
            try
            {
                var exePath = FindCloudflaredExe();
                if (exePath != null)
                    sb.AppendLine($"[OK] cloudflared executable found: {exePath}");
                else
                    sb.AppendLine("[FAIL] cloudflared executable NOT found on disk or PATH.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[ERROR] Error searching for cloudflared: {ex.Message}");
            }

            // 2. Windows service installed
            bool serviceInstalled = false;
            try
            {
                using var sc = new ServiceController("Cloudflared");
                _ = sc.Status; // triggers if not installed
                serviceInstalled = true;
                sb.AppendLine($"[OK] Cloudflared Windows service is installed (status: {sc.Status}).");
            }
            catch (InvalidOperationException)
            {
                sb.AppendLine("[FAIL] Cloudflared Windows service is NOT installed.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[ERROR] Could not query service: {ex.Message}");
            }

            // 3. Service running
            if (serviceInstalled)
            {
                sb.AppendLine(IsCloudflaredRunning
                    ? "[OK] Cloudflared service is RUNNING."
                    : "[FAIL] Cloudflared service is NOT running.");
            }

            // 4. Local port reachable
            var portReachable = IsLocalPortReachable();
            sb.AppendLine(portReachable
                ? $"[OK] Local port {_port} is reachable."
                : $"[FAIL] Local port {_port} is NOT reachable.");

            // 5. Tunnel token (hint)
            try
            {
                // Check if the service has a token configured by reading the registry or service args
                // For now we just note the status
                sb.AppendLine("[INFO] Tunnel token: (use InstallCloudflaredService to configure)");
            }
            catch { }

            // 6. Last error
            if (!string.IsNullOrEmpty(LastError))
                sb.AppendLine($"[ERROR] Last error: {LastError}");

            // 7. Last install log
            if (!string.IsNullOrEmpty(LastInstallLog))
            {
                sb.AppendLine();
                sb.AppendLine("--- Last Install Log ---");
                sb.AppendLine(LastInstallLog);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Tests end-to-end tunnel connectivity by hitting the public tunnel URL health endpoint.
    /// Returns whether the request succeeds through the Cloudflare tunnel.
    /// </summary>
    public async Task<(bool success, string message)> TestTunnelConnectivityAsync()
    {
        if (string.IsNullOrEmpty(_publicDomain))
        {
            return (false, "No public domain configured. Set publicDomain in server settings.");
        }

        var healthUrl = $"https://{_publicDomain}/health";

        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await http.GetAsync(healthUrl);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var msg = $"Tunnel connectivity OK. Status: {(int)response.StatusCode} {response.StatusCode}. Body: {body}";
                SglLogger.Information("Tunnel connectivity test PASSED: {StatusCode}", (int)response.StatusCode);
                return (true, msg);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                var msg = $"Tunnel returned non-success status: {(int)response.StatusCode} {response.StatusCode}. Body: {body}";
                SglLogger.Warning("Tunnel connectivity test FAILED: {StatusCode} {Body}", (int)response.StatusCode, body);
                return (false, msg);
            }
        }
        catch (HttpRequestException ex)
        {
            var msg = $"Tunnel connectivity test FAILED (HTTP error): {ex.Message}";
            SglLogger.Error("Tunnel connectivity test HTTP error.", ex);
            return (false, msg);
        }
        catch (TaskCanceledException)
        {
            var msg = $"Tunnel connectivity test FAILED: Request to {healthUrl} timed out after 10 seconds.";
            SglLogger.Warning(msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            var msg = $"Tunnel connectivity test FAILED (unexpected error): {ex.Message}";
            SglLogger.Error("Tunnel connectivity test unexpected error.", ex);
            return (false, msg);
        }
    }

    /// <summary>
    /// Returns a summary of the current tunnel connection details.
    /// </summary>
    public TunnelStatus GetTunnelStatus()
    {
        CheckCloudflaredServiceRunning();
        var localReachable = IsLocalPortReachable();

        return new TunnelStatus
        {
            IsServiceRunning = IsCloudflaredRunning,
            IsLocalPortReachable = localReachable,
            Port = _port,
            IsTunnelHealthy = IsTunnelHealthy, // Now based on actual end-to-end test, not just local checks
            IsEndToEndVerified = IsEndToEndVerified,
            TunnelConnections = TunnelConnections,
            IsDdosDetected = IsDdosDetected,
            RequestsPerSecond = RequestsPerSecond,
            ErrorRatio = ErrorRatio,
            LastError = LastError,
        };
    }

    // ──────────────────────────────────────────────────────────────────
    //  Firewall port management
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Windows Firewall inbound rule allowing traffic on the specified port.
    /// Uses netsh advfirewall to add the rule idempotently (deletes existing rule first).
    /// </summary>
    public void EnsureFirewallPort(int port)
    {
        var ruleName = $"SGL SyntheticAI API Server (Port {port})";

        try
        {
            // Delete any existing rule with the same name (idempotent)
            RunNetsh($"advfirewall firewall delete rule name=\"{ruleName}\"");

            // Create a new inbound allow rule for TCP on the specified port
            var addResult = RunNetsh(
                $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port} profile=any enable=yes");

            if (addResult.Contains("Ok", StringComparison.OrdinalIgnoreCase))
            {
                SglLogger.Information("Firewall rule created: {RuleName} (port {Port})", ruleName, port);
            }
            else
            {
                SglLogger.Warning("Firewall rule creation returned unexpected output: {Output}", addResult);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to create firewall rule for port {Port}.", ex);
        }
    }

    private static string RunNetsh(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return string.Empty;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);
            return output;
        }
        catch (Exception ex)
        {
            SglLogger.Error("netsh execution failed: " + ex.Message);
            return string.Empty;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Tunnel health monitoring
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks whether the local API server port is reachable (TCP connect test).
    /// This is the same port Cloudflare tunnel forwards to.
    /// </summary>
    public bool IsLocalPortReachable()
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect("127.0.0.1", _port, null, null);
            var connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            if (connected)
            {
                client.EndConnect(result);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private int _healthTickCount;

    private void RefreshStatus()
    {
        try
        {
            CheckCloudflaredServiceRunning();
            var portReachable = IsLocalPortReachable();

            // Count active tunnel connections by querying cloudflared metrics
            if (IsCloudflaredRunning)
            {
                TunnelConnections = GetActiveTunnelConnections();
            }
            else
            {
                TunnelConnections = 0;
            }

            // Recalculate DDoS indicators
            CheckDdosIndicators();

            // Every 4th check (~60 seconds), do an actual end-to-end tunnel verification
            // This prevents the false "connected" status when the tunnel isn't actually working
            _healthTickCount++;
            if (_healthTickCount % 4 == 1 && IsCloudflaredRunning && portReachable)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var (success, message) = await TestTunnelConnectivityAsync();
                        IsEndToEndVerified = success;
                        IsTunnelHealthy = success;
                        if (!success)
                        {
                            LastError = $"End-to-end tunnel test failed: {message}";
                            SglLogger.Warning("Tunnel end-to-end test failed: {Message}", message);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(LastError) && LastError.StartsWith("End-to-end"))
                                LastError = string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        IsEndToEndVerified = false;
                        IsTunnelHealthy = false;
                        LastError = $"Tunnel verification error: {ex.Message}";
                    }
                });
            }
            else if (!IsCloudflaredRunning || !portReachable)
            {
                // If service is down or port unreachable, tunnel is definitely not healthy
                IsTunnelHealthy = false;
                IsEndToEndVerified = false;
            }
            // If we haven't done an e2e check yet, set healthy based on local status only (optimistic)
            // but mark as unverified
            else if (_healthTickCount <= 1)
            {
                IsTunnelHealthy = IsCloudflaredRunning && portReachable;
                IsEndToEndVerified = false;
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("Tunnel health check failed.", ex);
        }
    }

    /// <summary>
    /// Queries the cloudflared metrics endpoint to count active tunnel connections.
    /// cloudflared exposes Prometheus metrics on ports 20241-20245 (default range).
    /// Falls back to checking netstat for established connections from cloudflared.exe.
    /// </summary>
    private int GetActiveTunnelConnections()
    {
        try
        {
            // Method 1: Try cloudflared metrics endpoint (Prometheus format)
            // cloudflared uses ports 20241-20245 by default (NOT 33400)
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            int[] metricsPorts = [20241, 20242, 20243, 20244, 20245];
            foreach (var port in metricsPorts)
            {
                try
                {
                    var task = http.GetStringAsync($"http://localhost:{port}/metrics");
                    task.Wait(2000);
                    if (task.IsCompletedSuccessfully)
                    {
                        var metrics = task.Result;
                        var lines = metrics.Split('\n');
                        foreach (var line in lines)
                        {
                            // Check for HA connections (registered tunnel connections to Cloudflare edge)
                            if (line.StartsWith("cloudflared_tunnel_ha_connections") && !line.StartsWith("#"))
                            {
                                var parts = line.Split(' ');
                                if (parts.Length >= 2 && int.TryParse(parts[^1].Trim(), out var count))
                                    return count;
                            }
                            // Check for active streams (active proxied requests)
                            if (line.StartsWith("cloudflared_tunnel_active_streams") && !line.StartsWith("#"))
                            {
                                var parts = line.Split(' ');
                                if (parts.Length >= 2 && int.TryParse(parts[^1].Trim(), out var count))
                                    return count;
                            }
                        }
                        // Check register_connection count from histogram
                        foreach (var line in lines)
                        {
                            if (line.Contains("register_connection") && line.Contains("le=\"+Inf\"") && !line.StartsWith("#"))
                            {
                                var parts = line.Split(' ');
                                if (parts.Length >= 2 && int.TryParse(parts[^1].Trim(), out var count) && count > 0)
                                    return count;
                            }
                        }
                        // Metrics endpoint responded — tunnel process is running even if no specific metric found
                        return 1;
                    }
                }
                catch { /* This port didn't respond, try next */ }
            }
        }
        catch { /* Metrics endpoint not available, try fallback */ }

        try
        {
            // Method 2: Count established connections from cloudflared.exe process via netstat
            var psi = new ProcessStartInfo("netstat", "-ano")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);

                // Find cloudflared PID(s)
                var cfPids = new HashSet<string>();
                try
                {
                    foreach (var p in Process.GetProcessesByName("cloudflared"))
                    {
                        cfPids.Add(p.Id.ToString());
                    }
                }
                catch { }

                if (cfPids.Count > 0)
                {
                    var established = output.Split('\n')
                        .Count(line => line.Contains("ESTABLISHED") &&
                                      cfPids.Any(pid => line.TrimEnd().EndsWith(pid)));
                    return established;
                }
            }
        }
        catch { }

        // Fallback: no data available
        return 0;
    }

    // ──────────────────────────────────────────────────────────────────
    //  DDoS indicator detection
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from middleware or request pipeline to record each incoming request.
    /// </summary>
    public void RecordRequest(bool isError = false)
    {
        var now = DateTime.UtcNow;
        _requestTimestamps.Enqueue(now);
        Interlocked.Increment(ref _totalRequests);
        if (isError)
            Interlocked.Increment(ref _errorResponses);
    }

    /// <summary>
    /// Analyzes recent request patterns to detect potential DDoS attacks.
    /// Flags as DDoS if:
    ///   - Requests per second exceeds 1000, or
    ///   - Error response ratio exceeds 50%
    /// </summary>
    public void CheckDdosIndicators()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-10);

        // Prune old timestamps (older than 10 seconds)
        while (_requestTimestamps.TryPeek(out var ts) && ts < cutoff)
            _requestTimestamps.TryDequeue(out _);

        var recentCount = _requestTimestamps.Count;
        RequestsPerSecond = recentCount / 10.0;

        var total = Interlocked.CompareExchange(ref _totalRequests, 0, 0);
        var errors = Interlocked.CompareExchange(ref _errorResponses, 0, 0);
        ErrorRatio = total > 0 ? (double)errors / total : 0.0;

        var wasDdos = IsDdosDetected;
        IsDdosDetected = RequestsPerSecond > 1000 || (total > 100 && ErrorRatio > 0.5);

        if (IsDdosDetected && !wasDdos)
        {
            SglLogger.Warning(
                "Potential DDoS detected! Requests/sec: {RPS:F1}, Error ratio: {ErrorRatio:P1}",
                RequestsPerSecond, ErrorRatio);
        }
        else if (!IsDdosDetected && wasDdos)
        {
            SglLogger.Information("DDoS indicators have subsided.");
            // Reset counters after DDoS clears
            Interlocked.Exchange(ref _totalRequests, 0);
            Interlocked.Exchange(ref _errorResponses, 0);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Managed cloudflared process (alternative to Windows service)
    // ──────────────────────────────────────────────────────────────────

    private Process? _managedProcess;
    private readonly List<string> _tunnelLogs = new();
    private readonly object _logLock = new();

    /// <summary>
    /// Recent tunnel log lines from the managed cloudflared process.
    /// </summary>
    public IReadOnlyList<string> TunnelLogs
    {
        get
        {
            lock (_logLock) return _tunnelLogs.ToList();
        }
    }

    /// <summary>
    /// Starts cloudflared as a managed child process using the given tunnel token.
    /// This approach gives full visibility into tunnel output and doesn't require
    /// Windows service installation (no admin privileges for service install needed).
    /// Falls back to the Windows service if cloudflared.exe is not found.
    /// </summary>
    public async Task<(bool success, string message)> StartManagedTunnelAsync(string tunnelToken)
    {
        if (string.IsNullOrWhiteSpace(tunnelToken))
            return (false, "Tunnel token is empty.");

        // If managed process is already running, report success
        if (_managedProcess != null && !_managedProcess.HasExited)
        {
            return (true, "Managed cloudflared process is already running.");
        }

        // If the Windows service is running, use that instead
        if (CheckCloudflaredServiceRunning())
        {
            SglLogger.Information("Cloudflared Windows service is already running — using service mode.");
            return (true, "Cloudflared Windows service is already running.");
        }

        var cloudflaredPath = FindCloudflaredExe();
        if (cloudflaredPath == null)
        {
            return (false, "cloudflared.exe not found. Please install Cloudflare Tunnel client.");
        }

        try
        {
            SglLogger.Information("Starting managed cloudflared process: {Path}", cloudflaredPath);

            var psi = new ProcessStartInfo(cloudflaredPath, $"tunnel --no-autoupdate run --token {tunnelToken}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(cloudflaredPath) ?? AppContext.BaseDirectory,
            };

            _managedProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

            _managedProcess.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (_logLock)
                {
                    _tunnelLogs.Add($"[{DateTime.Now:HH:mm:ss}] {e.Data}");
                    if (_tunnelLogs.Count > 500) _tunnelLogs.RemoveAt(0);
                }
                SglLogger.Information("[cloudflared] {Line}", e.Data);
            };

            _managedProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (_logLock)
                {
                    _tunnelLogs.Add($"[{DateTime.Now:HH:mm:ss}] [ERR] {e.Data}");
                    if (_tunnelLogs.Count > 500) _tunnelLogs.RemoveAt(0);
                }
                // cloudflared sends normal status info to stderr
                SglLogger.Information("[cloudflared] {Line}", e.Data);
            };

            _managedProcess.Exited += (_, _) =>
            {
                SglLogger.Warning("[cloudflared] Managed process exited with code {ExitCode}",
                    _managedProcess?.ExitCode ?? -1);
                IsCloudflaredRunning = false;
            };

            _managedProcess.Start();
            _managedProcess.BeginOutputReadLine();
            _managedProcess.BeginErrorReadLine();

            // Give it a moment to start
            await Task.Delay(3000);

            if (_managedProcess.HasExited)
            {
                var code = _managedProcess.ExitCode;
                _managedProcess.Dispose();
                _managedProcess = null;
                return (false, $"cloudflared exited immediately with code {code}. Check tunnel token.");
            }

            IsCloudflaredRunning = true;
            SglLogger.Information("Managed cloudflared process started (PID {PID}).", _managedProcess.Id);
            return (true, $"Managed cloudflared tunnel started (PID {_managedProcess.Id}).");
        }
        catch (Exception ex)
        {
            SglLogger.Error("Failed to start managed cloudflared process.", ex);
            return (false, $"Failed to start cloudflared: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the managed cloudflared process if it is running.
    /// </summary>
    public void StopManagedTunnel()
    {
        if (_managedProcess == null) return;

        try
        {
            if (!_managedProcess.HasExited)
            {
                SglLogger.Information("Stopping managed cloudflared process...");
                _managedProcess.Kill(entireProcessTree: true);
                _managedProcess.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            SglLogger.Error("Error stopping managed cloudflared process.", ex);
        }
        finally
        {
            _managedProcess.Dispose();
            _managedProcess = null;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _healthTimer?.Dispose();
        _healthTimer = null;
        StopManagedTunnel();
        _cts.Dispose();
    }
}

/// <summary>
/// Snapshot of tunnel connection state returned by <see cref="CloudflareService.GetTunnelStatus"/>.
/// </summary>
public class TunnelStatus
{
    public bool IsServiceRunning { get; set; }
    public bool IsLocalPortReachable { get; set; }
    public int Port { get; set; }
    public bool IsTunnelHealthy { get; set; }
    public bool IsEndToEndVerified { get; set; }
    public int TunnelConnections { get; set; }
    public bool IsDdosDetected { get; set; }
    public double RequestsPerSecond { get; set; }
    public double ErrorRatio { get; set; }
    public string LastError { get; set; } = string.Empty;
}
