using SGL.JudgeDredd.Antivirus.Scanners;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Firewall.WindowsFirewall;
using SGL.JudgeDredd.KnowledgeBase;
using SGL.JudgeDredd.KnowledgeBase.Data;
using SGL.JudgeDredd.Security.Monitors;
using SGL.JudgeDredd.Service;
using SGL.JudgeDredd.Shared.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Load settings
var settingsPath = Path.Combine(AppContext.BaseDirectory, "data", "settings.json");
var appSettings = AppSettings.LoadFromFile(settingsPath);

builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton<KnowledgeDbContext>();
builder.Services.AddSingleton<KnowledgeBaseService>();
builder.Services.AddSingleton<IKnowledgeBase>(sp => sp.GetRequiredService<KnowledgeBaseService>());
builder.Services.AddSingleton<IScanEngine>(sp => new ScanEngine(sp.GetRequiredService<IKnowledgeBase>()));
builder.Services.AddSingleton<IFirewallManager, WindowsFirewallManager>();
builder.Services.AddSingleton<SecurityMonitorService>();
builder.Services.AddSingleton<ISecurityMonitor>(sp => sp.GetRequiredService<SecurityMonitorService>());
builder.Services.AddSingleton<IProcessMonitor>(sp => sp.GetRequiredService<SecurityMonitorService>());
builder.Services.AddHostedService<Worker>();

// Enable running as Windows Service
builder.Services.AddWindowsService(options => options.ServiceName = "SGL SyntheticAI Security Service");

var host = builder.Build();

// Initialize knowledge base
var kb = host.Services.GetRequiredService<KnowledgeBaseService>();
await kb.InitializeAsync();

host.Run();
