using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class FirewallViewModel : ViewModelBase
{
    private readonly IFirewallManager _firewallManager;

    [ObservableProperty]
    private bool _isGamingMode;

    [ObservableProperty]
    private bool _isStreamingMode;

    [ObservableProperty]
    private bool _isLockdownMode;

    public ObservableCollection<FirewallRule> Rules { get; } = [];
    public ObservableCollection<NetworkConnection> Connections { get; } = [];

    private readonly FirewallPreset _gamingPreset = new()
    {
        Name = "Gaming",
        Description = "Optimized for online gaming: opens common game ports and reduces latency restrictions",
        Category = "Presets",
        Rules = new List<FirewallRule>
        {
            // Block telemetry to reduce latency and bandwidth usage
            new()
            {
                Name = "Gaming - Block Telemetry HTTP",
                Description = "Block outbound HTTP to telemetry.microsoft.com",
                Action = Core.Enums.FirewallAction.Block,
                Direction = Core.Enums.FirewallDirection.Outbound,
                Protocol = Core.Enums.FirewallProtocol.TCP,
                RemotePorts = "80",
                RemoteAddresses = "telemetry.microsoft.com",
                Enabled = true,
                GroupName = "JD-Gaming",
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Name = "Gaming - Block Telemetry HTTPS",
                Description = "Block outbound HTTPS to telemetry.microsoft.com",
                Action = Core.Enums.FirewallAction.Block,
                Direction = Core.Enums.FirewallDirection.Outbound,
                Protocol = Core.Enums.FirewallProtocol.TCP,
                RemotePorts = "443",
                RemoteAddresses = "telemetry.microsoft.com",
                Enabled = true,
                GroupName = "JD-Gaming",
                CreatedAt = DateTime.UtcNow,
            },
            // Allow Steam & Source engine game ports (UDP)
            new()
            {
                Name = "Gaming - Allow Steam UDP 27000-27100",
                Description = "Allow Steam game traffic (UDP 27000-27100)",
                Action = Core.Enums.FirewallAction.Allow,
                Direction = Core.Enums.FirewallDirection.Both,
                Protocol = Core.Enums.FirewallProtocol.UDP,
                LocalPorts = "27000-27100",
                Enabled = true,
                GroupName = "JD-Gaming",
                CreatedAt = DateTime.UtcNow,
            },
            // Allow Xbox Live / Halo / CoD ports
            new()
            {
                Name = "Gaming - Allow Xbox Live TCP 3074",
                Description = "Allow Xbox Live and multiplayer traffic (TCP 3074)",
                Action = Core.Enums.FirewallAction.Allow,
                Direction = Core.Enums.FirewallDirection.Both,
                Protocol = Core.Enums.FirewallProtocol.TCP,
                LocalPorts = "3074",
                Enabled = true,
                GroupName = "JD-Gaming",
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Name = "Gaming - Allow Xbox Live UDP 3074",
                Description = "Allow Xbox Live and multiplayer traffic (UDP 3074)",
                Action = Core.Enums.FirewallAction.Allow,
                Direction = Core.Enums.FirewallDirection.Both,
                Protocol = Core.Enums.FirewallProtocol.UDP,
                LocalPorts = "3074",
                Enabled = true,
                GroupName = "JD-Gaming",
                CreatedAt = DateTime.UtcNow,
            },
            // Allow Steam server browser and SRCDS
            new()
            {
                Name = "Gaming - Allow Steam Server TCP 27015-27030",
                Description = "Allow Steam dedicated server and matchmaking (TCP 27015-27030)",
                Action = Core.Enums.FirewallAction.Allow,
                Direction = Core.Enums.FirewallDirection.Both,
                Protocol = Core.Enums.FirewallProtocol.TCP,
                LocalPorts = "27015-27030",
                Enabled = true,
                GroupName = "JD-Gaming",
                CreatedAt = DateTime.UtcNow,
            },
        }
    };

    private readonly FirewallPreset _streamingPreset = new()
    {
        Name = "Streaming",
        Description = "Optimized for streaming: allows OBS, Twitch, YouTube traffic",
        Category = "Presets",
        Rules = new List<FirewallRule>
        {
            // Allow RTMP (used by Twitch, YouTube, Facebook Live)
            new()
            {
                Name = "Streaming - Allow RTMP TCP 1935",
                Description = "Allow RTMP streaming to Twitch, YouTube, etc.",
                Action = Core.Enums.FirewallAction.Allow,
                Direction = Core.Enums.FirewallDirection.Outbound,
                Protocol = Core.Enums.FirewallProtocol.TCP,
                RemotePorts = "1935",
                Enabled = true,
                GroupName = "JD-Streaming",
                CreatedAt = DateTime.UtcNow,
            },
            // Allow HLS / HTTPS streaming
            new()
            {
                Name = "Streaming - Allow HLS/HTTPS TCP 443",
                Description = "Allow HLS and HTTPS streaming traffic",
                Action = Core.Enums.FirewallAction.Allow,
                Direction = Core.Enums.FirewallDirection.Outbound,
                Protocol = Core.Enums.FirewallProtocol.TCP,
                RemotePorts = "443",
                Enabled = true,
                GroupName = "JD-Streaming",
                CreatedAt = DateTime.UtcNow,
            },
            // Block P2P torrent traffic to protect upload bandwidth
            new()
            {
                Name = "Streaming - Block P2P TCP 6881-6889",
                Description = "Block BitTorrent TCP to preserve upload bandwidth",
                Action = Core.Enums.FirewallAction.Block,
                Direction = Core.Enums.FirewallDirection.Both,
                Protocol = Core.Enums.FirewallProtocol.TCP,
                LocalPorts = "6881-6889",
                Enabled = true,
                GroupName = "JD-Streaming",
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Name = "Streaming - Block P2P UDP 6881-6889",
                Description = "Block BitTorrent UDP to preserve upload bandwidth",
                Action = Core.Enums.FirewallAction.Block,
                Direction = Core.Enums.FirewallDirection.Both,
                Protocol = Core.Enums.FirewallProtocol.UDP,
                LocalPorts = "6881-6889",
                Enabled = true,
                GroupName = "JD-Streaming",
                CreatedAt = DateTime.UtcNow,
            },
        }
    };

    private readonly FirewallPreset _lockdownPreset = new()
    {
        Name = "Lockdown",
        Description = "Maximum security: blocks all non-essential traffic",
        Category = "Presets",
        Rules = new List<FirewallRule>
        {
            // Block all inbound connections
            new()
            {
                Name = "Lockdown - Block All Inbound",
                Description = "Block all inbound connections for maximum security",
                Action = Core.Enums.FirewallAction.Block,
                Direction = Core.Enums.FirewallDirection.Inbound,
                Protocol = Core.Enums.FirewallProtocol.Any,
                Enabled = true,
                GroupName = "JD-Lockdown",
                CreatedAt = DateTime.UtcNow,
            },
            // Allow only HTTP outbound
            new()
            {
                Name = "Lockdown - Allow HTTP Outbound",
                Description = "Allow outbound HTTP traffic (TCP 80)",
                Action = Core.Enums.FirewallAction.Allow,
                Direction = Core.Enums.FirewallDirection.Outbound,
                Protocol = Core.Enums.FirewallProtocol.TCP,
                RemotePorts = "80",
                Enabled = true,
                GroupName = "JD-Lockdown",
                CreatedAt = DateTime.UtcNow,
            },
            // Allow only HTTPS outbound
            new()
            {
                Name = "Lockdown - Allow HTTPS Outbound",
                Description = "Allow outbound HTTPS traffic (TCP 443)",
                Action = Core.Enums.FirewallAction.Allow,
                Direction = Core.Enums.FirewallDirection.Outbound,
                Protocol = Core.Enums.FirewallProtocol.TCP,
                RemotePorts = "443",
                Enabled = true,
                GroupName = "JD-Lockdown",
                CreatedAt = DateTime.UtcNow,
            },
            // Allow DNS outbound
            new()
            {
                Name = "Lockdown - Allow DNS Outbound",
                Description = "Allow outbound DNS queries (UDP 53)",
                Action = Core.Enums.FirewallAction.Allow,
                Direction = Core.Enums.FirewallDirection.Outbound,
                Protocol = Core.Enums.FirewallProtocol.UDP,
                RemotePorts = "53",
                Enabled = true,
                GroupName = "JD-Lockdown",
                CreatedAt = DateTime.UtcNow,
            },
            // Block everything else outbound
            new()
            {
                Name = "Lockdown - Block All Other Outbound",
                Description = "Block all other outbound traffic not explicitly allowed",
                Action = Core.Enums.FirewallAction.Block,
                Direction = Core.Enums.FirewallDirection.Outbound,
                Protocol = Core.Enums.FirewallProtocol.Any,
                Enabled = true,
                GroupName = "JD-Lockdown",
                CreatedAt = DateTime.UtcNow,
            },
        }
    };

    public FirewallViewModel(IFirewallManager firewallManager)
    {
        _firewallManager = firewallManager;
        Title = "Firewall";

        LoadRules();
        RefreshConnectionsInternal();
    }

    private void LoadRules()
    {
        Rules.Clear();
        foreach (var rule in _firewallManager.GetAllRules())
        {
            Rules.Add(rule);
        }
    }

    private void RefreshConnectionsInternal()
    {
        Connections.Clear();
        foreach (var conn in _firewallManager.GetActiveConnections())
        {
            Connections.Add(conn);
        }
    }

    [RelayCommand]
    private void AddRule()
    {
        var newRule = new FirewallRule
        {
            Name = $"New Rule {Rules.Count + 1}",
            Description = "Custom rule",
            Action = Core.Enums.FirewallAction.Block,
            Direction = Core.Enums.FirewallDirection.Inbound,
            Protocol = Core.Enums.FirewallProtocol.TCP,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _firewallManager.AddRule(newRule);
        Rules.Add(newRule);
    }

    [RelayCommand]
    private void RemoveRule(FirewallRule? rule)
    {
        if (rule is null) return;

        _firewallManager.RemoveRule(rule.Name);
        Rules.Remove(rule);
    }

    [RelayCommand]
    private async Task ToggleGamingAsync()
    {
        IsGamingMode = !IsGamingMode;

        if (IsGamingMode)
        {
            _firewallManager.ApplyPreset(_gamingPreset);
            IsStreamingMode = false;
            IsLockdownMode = false;
            await AvatarViewModel.Instance.ShowSpeechBubble("Gaming mode activated! GG.");
        }
        else
        {
            _firewallManager.RevertPreset(_gamingPreset);
            await AvatarViewModel.Instance.ShowSpeechBubble("Gaming mode deactivated.");
        }

        LoadRules();
    }

    [RelayCommand]
    private async Task ToggleStreamingAsync()
    {
        IsStreamingMode = !IsStreamingMode;

        if (IsStreamingMode)
        {
            _firewallManager.ApplyPreset(_streamingPreset);
            IsGamingMode = false;
            IsLockdownMode = false;
            await AvatarViewModel.Instance.ShowSpeechBubble("Streaming mode activated!");
        }
        else
        {
            _firewallManager.RevertPreset(_streamingPreset);
            await AvatarViewModel.Instance.ShowSpeechBubble("Streaming mode deactivated.");
        }

        LoadRules();
    }

    [RelayCommand]
    private async Task ToggleLockdownAsync()
    {
        IsLockdownMode = !IsLockdownMode;

        if (IsLockdownMode)
        {
            _firewallManager.ApplyPreset(_lockdownPreset);
            IsGamingMode = false;
            IsStreamingMode = false;
            AvatarViewModel.Instance.SetExpression(Core.Enums.AvatarExpression.UnderAttack);
            await AvatarViewModel.Instance.ShowSpeechBubble("LOCKDOWN MODE ENGAGED. All non-essential traffic blocked.");
        }
        else
        {
            _firewallManager.RevertPreset(_lockdownPreset);
            AvatarViewModel.Instance.SetExpression(Core.Enums.AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble("Lockdown mode lifted.");
        }

        LoadRules();
    }

    [RelayCommand]
    private void RefreshConnections()
    {
        RefreshConnectionsInternal();
    }
}
