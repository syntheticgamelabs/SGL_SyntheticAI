using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGL.JudgeDredd.KnowledgeBase.Data;
using SGL.JudgeDredd.KnowledgeBase.Entities;

namespace SGL.JudgeDredd.KnowledgeBase.Seeders;

public static class FirewallTemplateSeeder
{
    /// <summary>
    /// Internal DTO for clean JSON serialization of firewall rule data.
    /// </summary>
    private sealed record RuleDto(
        string Name,
        string Description,
        string? ApplicationPath,
        int Action,
        int Direction,
        int Protocol,
        string? LocalPorts,
        string? RemotePorts,
        string? RemoteAddresses,
        bool Enabled,
        string GroupName);

    public static async Task SeedAsync(KnowledgeDbContext context)
    {
        if (await context.FirewallTemplates.AnyAsync())
            return;

        var serializerOptions = new JsonSerializerOptions { WriteIndented = false };

        var templates = new List<FirewallTemplateEntity>
        {
            // Gaming Mode - Allows game-related traffic, blocks telemetry and ads
            new()
            {
                Name = "Gaming Mode",
                Category = "Gaming",
                Description = "Optimized for online gaming. Allows game platform traffic (Steam, Epic, Xbox Live, Battle.net), blocks ad networks and unnecessary telemetry to reduce latency and improve security during gaming sessions.",
                RulesJson = JsonSerializer.Serialize(new RuleDto[]
                {
                    new(
                        Name: "Allow Steam Client",
                        Description: "Allow Steam client connections for game downloads and multiplayer",
                        ApplicationPath: @"C:\Program Files (x86)\Steam\steam.exe",
                        Action: 0, // Allow
                        Direction: 2, // Both
                        Protocol: 0, // Any
                        LocalPorts: null,
                        RemotePorts: "80,443,27015-27050",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Gaming Mode"),
                    new(
                        Name: "Allow Epic Games Launcher",
                        Description: "Allow Epic Games Launcher for game management and multiplayer",
                        ApplicationPath: @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
                        Action: 0,
                        Direction: 2,
                        Protocol: 0,
                        LocalPorts: null,
                        RemotePorts: "80,443,5222,5795-5847",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Gaming Mode"),
                    new(
                        Name: "Allow Xbox Live",
                        Description: "Allow Xbox Live services for Game Pass and multiplayer",
                        ApplicationPath: null,
                        Action: 0,
                        Direction: 2,
                        Protocol: 1, // TCP
                        LocalPorts: null,
                        RemotePorts: "80,443,3074",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Gaming Mode"),
                    new(
                        Name: "Allow Xbox Live UDP",
                        Description: "Allow Xbox Live UDP for voice chat and multiplayer NAT traversal",
                        ApplicationPath: null,
                        Action: 0,
                        Direction: 2,
                        Protocol: 2, // UDP
                        LocalPorts: null,
                        RemotePorts: "88,500,3074,3544,4500",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Gaming Mode"),
                    new(
                        Name: "Allow Battle.net",
                        Description: "Allow Blizzard Battle.net for game services",
                        ApplicationPath: null,
                        Action: 0,
                        Direction: 2,
                        Protocol: 1,
                        LocalPorts: null,
                        RemotePorts: "80,443,1119,3724",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Gaming Mode"),
                    new(
                        Name: "Block Telemetry Outbound",
                        Description: "Block Windows telemetry to reduce background network usage during gaming",
                        ApplicationPath: null,
                        Action: 1, // Block
                        Direction: 1, // Outbound
                        Protocol: 1,
                        LocalPorts: null,
                        RemotePorts: "443",
                        RemoteAddresses: "13.107.4.50,13.107.4.52,51.104.136.2,40.68.222.18",
                        Enabled: true,
                        GroupName: "Gaming Mode"),
                    new(
                        Name: "Allow DNS",
                        Description: "Allow DNS resolution for game services",
                        ApplicationPath: null,
                        Action: 0,
                        Direction: 1,
                        Protocol: 2,
                        LocalPorts: null,
                        RemotePorts: "53",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Gaming Mode")
                }, serializerOptions),
                IsBuiltIn = true
            },

            // Streaming Mode - Allows OBS/streaming traffic while maintaining security
            new()
            {
                Name = "Streaming Mode",
                Category = "Streaming",
                Description = "Optimized for live streaming. Allows OBS, Streamlabs, and streaming platform traffic (Twitch, YouTube). Permits RTMP/RTMPS connections while blocking suspicious inbound connections.",
                RulesJson = JsonSerializer.Serialize(new RuleDto[]
                {
                    new(
                        Name: "Allow OBS Studio",
                        Description: "Allow OBS Studio for streaming and recording",
                        ApplicationPath: @"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
                        Action: 0,
                        Direction: 1, // Outbound
                        Protocol: 1, // TCP
                        LocalPorts: null,
                        RemotePorts: "80,443,1935,1936",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Streaming Mode"),
                    new(
                        Name: "Allow Streamlabs",
                        Description: "Allow Streamlabs desktop application",
                        ApplicationPath: @"C:\Program Files\Streamlabs Desktop\Streamlabs Desktop.exe",
                        Action: 0,
                        Direction: 1,
                        Protocol: 1,
                        LocalPorts: null,
                        RemotePorts: "80,443,1935,1936",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Streaming Mode"),
                    new(
                        Name: "Allow RTMP Outbound",
                        Description: "Allow RTMP/RTMPS streaming protocol to streaming platforms",
                        ApplicationPath: null,
                        Action: 0,
                        Direction: 1,
                        Protocol: 1,
                        LocalPorts: null,
                        RemotePorts: "1935,1936,443",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Streaming Mode"),
                    new(
                        Name: "Allow Twitch API",
                        Description: "Allow Twitch API connections for chat, alerts, and channel management",
                        ApplicationPath: null,
                        Action: 0,
                        Direction: 2,
                        Protocol: 1,
                        LocalPorts: null,
                        RemotePorts: "80,443,6667,6697",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Streaming Mode"),
                    new(
                        Name: "Allow YouTube Streaming",
                        Description: "Allow YouTube Live streaming connections",
                        ApplicationPath: null,
                        Action: 0,
                        Direction: 1,
                        Protocol: 1,
                        LocalPorts: null,
                        RemotePorts: "80,443,1935",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Streaming Mode"),
                    new(
                        Name: "Block Inbound Unsolicited",
                        Description: "Block unsolicited inbound connections from unknown sources during streaming",
                        ApplicationPath: null,
                        Action: 1,
                        Direction: 0, // Inbound
                        Protocol: 1,
                        LocalPorts: "1-1024",
                        RemotePorts: null,
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Streaming Mode"),
                    new(
                        Name: "Allow DNS",
                        Description: "Allow DNS resolution for streaming services",
                        ApplicationPath: null,
                        Action: 0,
                        Direction: 1,
                        Protocol: 2,
                        LocalPorts: null,
                        RemotePorts: "53",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Streaming Mode")
                }, serializerOptions),
                IsBuiltIn = true
            },

            // Lockdown Mode - Maximum security, blocks nearly everything
            new()
            {
                Name = "Lockdown Mode",
                Category = "Security",
                Description = "Maximum security lockdown. Blocks all inbound connections, restricts outbound to essential services only (DNS, HTTPS, Windows Update). Use during active threat response or when maximum protection is required.",
                RulesJson = JsonSerializer.Serialize(new RuleDto[]
                {
                    new(
                        Name: "Block All Inbound",
                        Description: "Block all inbound connections - no exceptions during lockdown",
                        ApplicationPath: null,
                        Action: 1, // Block
                        Direction: 0, // Inbound
                        Protocol: 0, // Any
                        LocalPorts: null,
                        RemotePorts: null,
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Lockdown Mode"),
                    new(
                        Name: "Allow HTTPS Outbound Only",
                        Description: "Allow only HTTPS outbound for essential secure communications",
                        ApplicationPath: null,
                        Action: 0, // Allow
                        Direction: 1, // Outbound
                        Protocol: 1, // TCP
                        LocalPorts: null,
                        RemotePorts: "443",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Lockdown Mode"),
                    new(
                        Name: "Allow DNS Outbound",
                        Description: "Allow DNS resolution - required for basic connectivity",
                        ApplicationPath: null,
                        Action: 0,
                        Direction: 1,
                        Protocol: 2, // UDP
                        LocalPorts: null,
                        RemotePorts: "53",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Lockdown Mode"),
                    new(
                        Name: "Allow Windows Update",
                        Description: "Allow Windows Update service to maintain security patches",
                        ApplicationPath: @"C:\Windows\System32\svchost.exe",
                        Action: 0,
                        Direction: 1,
                        Protocol: 1,
                        LocalPorts: null,
                        RemotePorts: "80,443",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Lockdown Mode"),
                    new(
                        Name: "Block All Other Outbound TCP",
                        Description: "Block all non-essential outbound TCP connections",
                        ApplicationPath: null,
                        Action: 1,
                        Direction: 1,
                        Protocol: 1,
                        LocalPorts: null,
                        RemotePorts: "1-79,81-442,444-65535",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Lockdown Mode"),
                    new(
                        Name: "Block All Other Outbound UDP",
                        Description: "Block all non-essential outbound UDP connections",
                        ApplicationPath: null,
                        Action: 1,
                        Direction: 1,
                        Protocol: 2,
                        LocalPorts: null,
                        RemotePorts: "1-52,54-65535",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Lockdown Mode"),
                    new(
                        Name: "Block ICMP",
                        Description: "Block all ICMP traffic to prevent network reconnaissance",
                        ApplicationPath: null,
                        Action: 1,
                        Direction: 2, // Both
                        Protocol: 3, // ICMP
                        LocalPorts: null,
                        RemotePorts: null,
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Lockdown Mode"),
                    new(
                        Name: "Block SMB",
                        Description: "Block SMB/CIFS to prevent lateral movement and worm propagation",
                        ApplicationPath: null,
                        Action: 1,
                        Direction: 2,
                        Protocol: 1,
                        LocalPorts: "445,139",
                        RemotePorts: "445,139",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Lockdown Mode"),
                    new(
                        Name: "Block RDP",
                        Description: "Block Remote Desktop Protocol to prevent unauthorized remote access",
                        ApplicationPath: null,
                        Action: 1,
                        Direction: 2,
                        Protocol: 1,
                        LocalPorts: "3389",
                        RemotePorts: "3389",
                        RemoteAddresses: null,
                        Enabled: true,
                        GroupName: "Lockdown Mode")
                }, serializerOptions),
                IsBuiltIn = true
            }
        };

        context.FirewallTemplates.AddRange(templates);
        await context.SaveChangesAsync();
    }
}
