using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Core.Interfaces;
using SGL.JudgeDredd.Core.Models;

namespace SGL.JudgeDredd.Firewall.WindowsFirewall;

public sealed class WindowsFirewallManager : IFirewallManager, IDisposable
{
    private const string RulePrefix = "SGL-SAI: ";

    private const int NET_FW_ACTION_BLOCK = 0;
    private const int NET_FW_ACTION_ALLOW = 1;

    private const int NET_FW_RULE_DIR_IN = 1;
    private const int NET_FW_RULE_DIR_OUT = 2;

    private const int NET_FW_IP_PROTOCOL_ICMP = 1;
    private const int NET_FW_IP_PROTOCOL_TCP = 6;
    private const int NET_FW_IP_PROTOCOL_UDP = 17;
    private const int NET_FW_IP_PROTOCOL_ANY = 256;

    private readonly dynamic _firewallPolicy;
    private bool _disposed;

    /// <summary>
    /// Indicates whether the current process has administrator privileges.
    /// Modifying firewall rules requires elevated permissions.
    /// </summary>
    public bool IsAdministrator { get; }

    public WindowsFirewallManager()
    {
        // Check for admin privileges
        IsAdministrator = CheckAdministrator();

        Type? policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
        if (policyType == null)
        {
            throw new PlatformNotSupportedException(
                "Windows Firewall COM component 'HNetCfg.FwPolicy2' is not available on this system.");
        }

        _firewallPolicy = Activator.CreateInstance(policyType)
            ?? throw new InvalidOperationException("Failed to create an instance of HNetCfg.FwPolicy2.");
    }

    private static bool CheckAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void ThrowIfNotAdmin()
    {
        if (!IsAdministrator)
            throw new UnauthorizedAccessException(
                "Administrator privileges are required to modify Windows Firewall rules. " +
                "Please run the application as Administrator.");
    }

    public IReadOnlyList<FirewallRule> GetAllRules()
    {
        ThrowIfDisposed();

        var rules = new List<FirewallRule>();

        foreach (dynamic comRule in _firewallPolicy.Rules)
        {
            try
            {
                var rule = ConvertFromComRule(comRule);
                rules.Add(rule);
            }
            catch (COMException)
            {
                // Some built-in rules may have restricted access; skip them.
            }
            finally
            {
                if (comRule != null && Marshal.IsComObject(comRule))
                {
                    Marshal.ReleaseComObject(comRule);
                }
            }
        }

        return rules.AsReadOnly();
    }

    public void AddRule(FirewallRule rule)
    {
        ThrowIfDisposed();
        ThrowIfNotAdmin();
        ArgumentNullException.ThrowIfNull(rule);

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new ArgumentException("Firewall rule name must not be empty.", nameof(rule));
        }

        dynamic comRule = CreateComRule();
        try
        {
            ApplyToComRule(comRule, rule);

            if (rule.Direction == FirewallDirection.Both)
            {
                comRule.Direction = NET_FW_RULE_DIR_IN;
                _firewallPolicy.Rules.Add(comRule);

                dynamic outboundRule = CreateComRule();
                try
                {
                    ApplyToComRule(outboundRule, rule);
                    outboundRule.Name = rule.Name + " [Outbound]";
                    outboundRule.Direction = NET_FW_RULE_DIR_OUT;
                    _firewallPolicy.Rules.Add(outboundRule);
                }
                finally
                {
                    if (Marshal.IsComObject(outboundRule))
                    {
                        Marshal.ReleaseComObject(outboundRule);
                    }
                }
            }
            else
            {
                _firewallPolicy.Rules.Add(comRule);
            }
        }
        finally
        {
            if (Marshal.IsComObject(comRule))
            {
                Marshal.ReleaseComObject(comRule);
            }
        }
    }

    public void RemoveRule(string name)
    {
        ThrowIfDisposed();
        ThrowIfNotAdmin();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var namesToRemove = new List<string>();

        foreach (dynamic comRule in _firewallPolicy.Rules)
        {
            try
            {
                string ruleName = (string)comRule.Name;
                if (string.Equals(ruleName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ruleName, name + " [Outbound]", StringComparison.OrdinalIgnoreCase))
                {
                    namesToRemove.Add(ruleName);
                }
            }
            finally
            {
                if (comRule != null && Marshal.IsComObject(comRule))
                {
                    Marshal.ReleaseComObject(comRule);
                }
            }
        }

        foreach (string ruleName in namesToRemove)
        {
            _firewallPolicy.Rules.Remove(ruleName);
        }
    }

    public void EnableRule(string name)
    {
        ThrowIfDisposed();
        ThrowIfNotAdmin();
        SetRuleEnabled(name, true);
    }

    public void DisableRule(string name)
    {
        ThrowIfDisposed();
        ThrowIfNotAdmin();
        SetRuleEnabled(name, false);
    }

    public void ApplyPreset(FirewallPreset preset)
    {
        ThrowIfDisposed();
        ThrowIfNotAdmin();
        ArgumentNullException.ThrowIfNull(preset);

        foreach (FirewallRule rule in preset.Rules)
        {
            var prefixedRule = CloneRuleWithPrefix(rule, preset.Name);
            AddRule(prefixedRule);
        }

        preset.IsActive = true;
    }

    public void RevertPreset(FirewallPreset preset)
    {
        ThrowIfDisposed();
        ThrowIfNotAdmin();
        ArgumentNullException.ThrowIfNull(preset);

        string prefix = RulePrefix + preset.Name + ": ";
        var namesToRemove = new List<string>();

        foreach (dynamic comRule in _firewallPolicy.Rules)
        {
            try
            {
                string ruleName = (string)comRule.Name;
                if (ruleName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    namesToRemove.Add(ruleName);
                }
            }
            finally
            {
                if (comRule != null && Marshal.IsComObject(comRule))
                {
                    Marshal.ReleaseComObject(comRule);
                }
            }
        }

        foreach (string ruleName in namesToRemove)
        {
            _firewallPolicy.Rules.Remove(ruleName);
        }

        preset.IsActive = false;
    }

    public IReadOnlyList<NetworkConnection> GetActiveConnections()
    {
        ThrowIfDisposed();

        var connections = new List<NetworkConnection>();

        string netstatOutput = RunNetstat();
        string[] lines = netstatOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var processNameCache = new Dictionary<int, string>();

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            if (line.StartsWith("Proto", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Active", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            NetworkConnection? connection = ParseNetstatLine(line, processNameCache);
            if (connection != null)
            {
                connections.Add(connection);
            }
        }

        return connections.AsReadOnly();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_firewallPolicy != null && Marshal.IsComObject(_firewallPolicy))
        {
            Marshal.ReleaseComObject(_firewallPolicy);
        }

        _disposed = true;
    }

    private static dynamic CreateComRule()
    {
        Type? ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
        if (ruleType == null)
        {
            throw new PlatformNotSupportedException(
                "Windows Firewall COM component 'HNetCfg.FWRule' is not available on this system.");
        }

        return Activator.CreateInstance(ruleType)
            ?? throw new InvalidOperationException("Failed to create an instance of HNetCfg.FWRule.");
    }

    private static void ApplyToComRule(dynamic comRule, FirewallRule rule)
    {
        comRule.Name = rule.Name;
        comRule.Description = rule.Description ?? string.Empty;
        comRule.Enabled = rule.Enabled;
        comRule.Action = MapActionToComValue(rule.Action);
        comRule.Protocol = MapProtocolToComValue(rule.Protocol);

        if (rule.Direction != FirewallDirection.Both)
        {
            comRule.Direction = MapDirectionToComValue(rule.Direction);
        }

        if (!string.IsNullOrWhiteSpace(rule.ApplicationPath))
        {
            comRule.ApplicationName = rule.ApplicationPath;
        }

        if (!string.IsNullOrWhiteSpace(rule.LocalPorts) && ProtocolSupportsPorts(rule.Protocol))
        {
            comRule.LocalPorts = rule.LocalPorts;
        }

        if (!string.IsNullOrWhiteSpace(rule.RemotePorts) && ProtocolSupportsPorts(rule.Protocol))
        {
            comRule.RemotePorts = rule.RemotePorts;
        }

        if (!string.IsNullOrWhiteSpace(rule.RemoteAddresses))
        {
            comRule.RemoteAddresses = rule.RemoteAddresses;
        }

        if (!string.IsNullOrWhiteSpace(rule.GroupName))
        {
            comRule.Grouping = rule.GroupName;
        }
    }

    private static bool ProtocolSupportsPorts(FirewallProtocol protocol)
    {
        return protocol == FirewallProtocol.TCP || protocol == FirewallProtocol.UDP;
    }

    private static FirewallRule ConvertFromComRule(dynamic comRule)
    {
        int directionValue = (int)comRule.Direction;
        int actionValue = (int)comRule.Action;
        int protocolValue = (int)comRule.Protocol;

        string? localPorts = null;
        string? remotePorts = null;

        if (protocolValue == NET_FW_IP_PROTOCOL_TCP || protocolValue == NET_FW_IP_PROTOCOL_UDP)
        {
            try { localPorts = (string?)comRule.LocalPorts; } catch (COMException) { }
            try { remotePorts = (string?)comRule.RemotePorts; } catch (COMException) { }
        }

        string? remoteAddresses = null;
        try { remoteAddresses = (string?)comRule.RemoteAddresses; } catch (COMException) { }

        string? appPath = null;
        try { appPath = (string?)comRule.ApplicationName; } catch (COMException) { }

        string? grouping = null;
        try { grouping = (string?)comRule.Grouping; } catch (COMException) { }

        string? description = null;
        try { description = (string?)comRule.Description; } catch (COMException) { }

        return new FirewallRule
        {
            Name = (string)comRule.Name,
            Description = description,
            ApplicationPath = appPath,
            Action = MapComValueToAction(actionValue),
            Direction = MapComValueToDirection(directionValue),
            Protocol = MapComValueToProtocol(protocolValue),
            LocalPorts = localPorts,
            RemotePorts = remotePorts,
            RemoteAddresses = remoteAddresses,
            Enabled = (bool)comRule.Enabled,
            GroupName = grouping,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static int MapActionToComValue(FirewallAction action)
    {
        return action switch
        {
            FirewallAction.Allow => NET_FW_ACTION_ALLOW,
            FirewallAction.Block => NET_FW_ACTION_BLOCK,
            FirewallAction.Log => NET_FW_ACTION_ALLOW,
            _ => NET_FW_ACTION_BLOCK
        };
    }

    private static FirewallAction MapComValueToAction(int comValue)
    {
        return comValue switch
        {
            NET_FW_ACTION_ALLOW => FirewallAction.Allow,
            NET_FW_ACTION_BLOCK => FirewallAction.Block,
            _ => FirewallAction.Block
        };
    }

    private static int MapDirectionToComValue(FirewallDirection direction)
    {
        return direction switch
        {
            FirewallDirection.Inbound => NET_FW_RULE_DIR_IN,
            FirewallDirection.Outbound => NET_FW_RULE_DIR_OUT,
            FirewallDirection.Both => NET_FW_RULE_DIR_IN,
            _ => NET_FW_RULE_DIR_IN
        };
    }

    private static FirewallDirection MapComValueToDirection(int comValue)
    {
        return comValue switch
        {
            NET_FW_RULE_DIR_IN => FirewallDirection.Inbound,
            NET_FW_RULE_DIR_OUT => FirewallDirection.Outbound,
            _ => FirewallDirection.Inbound
        };
    }

    private static int MapProtocolToComValue(FirewallProtocol protocol)
    {
        return protocol switch
        {
            FirewallProtocol.TCP => NET_FW_IP_PROTOCOL_TCP,
            FirewallProtocol.UDP => NET_FW_IP_PROTOCOL_UDP,
            FirewallProtocol.ICMP => NET_FW_IP_PROTOCOL_ICMP,
            FirewallProtocol.Any => NET_FW_IP_PROTOCOL_ANY,
            _ => NET_FW_IP_PROTOCOL_ANY
        };
    }

    private static FirewallProtocol MapComValueToProtocol(int comValue)
    {
        return comValue switch
        {
            NET_FW_IP_PROTOCOL_TCP => FirewallProtocol.TCP,
            NET_FW_IP_PROTOCOL_UDP => FirewallProtocol.UDP,
            NET_FW_IP_PROTOCOL_ICMP => FirewallProtocol.ICMP,
            NET_FW_IP_PROTOCOL_ANY => FirewallProtocol.Any,
            _ => FirewallProtocol.Any
        };
    }

    private void SetRuleEnabled(string name, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        bool found = false;

        foreach (dynamic comRule in _firewallPolicy.Rules)
        {
            try
            {
                string ruleName = (string)comRule.Name;
                if (string.Equals(ruleName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ruleName, name + " [Outbound]", StringComparison.OrdinalIgnoreCase))
                {
                    comRule.Enabled = enabled;
                    found = true;
                }
            }
            finally
            {
                if (comRule != null && Marshal.IsComObject(comRule))
                {
                    Marshal.ReleaseComObject(comRule);
                }
            }
        }

        if (!found)
        {
            throw new InvalidOperationException($"Firewall rule '{name}' was not found.");
        }
    }

    private static FirewallRule CloneRuleWithPrefix(FirewallRule source, string presetName)
    {
        return new FirewallRule
        {
            Name = RulePrefix + presetName + ": " + source.Name,
            Description = source.Description,
            ApplicationPath = source.ApplicationPath,
            Action = source.Action,
            Direction = source.Direction,
            Protocol = source.Protocol,
            LocalPorts = source.LocalPorts,
            RemotePorts = source.RemotePorts,
            RemoteAddresses = source.RemoteAddresses,
            Enabled = source.Enabled,
            GroupName = source.GroupName,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string RunNetstat()
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = "-ano",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(TimeSpan.FromSeconds(30));

        return output;
    }

    private static NetworkConnection? ParseNetstatLine(string line, Dictionary<int, string> processNameCache)
    {
        // netstat -ano output format:
        //   TCP    0.0.0.0:135       0.0.0.0:0       LISTENING       1234
        //   TCP    [::]:135          [::]:0          LISTENING       1234
        //   UDP    0.0.0.0:5353      *:*                             5678

        var match = Regex.Match(line,
            @"^\s*(TCP|UDP)\s+" +
            @"(\S+)" +
            @"\s+(\S+)" +
            @"\s+(\S*)" +
            @"\s+(\d+)\s*$",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        string protocol = match.Groups[1].Value.ToUpperInvariant();
        string localEndpoint = match.Groups[2].Value;
        string remoteEndpoint = match.Groups[3].Value;
        string state = match.Groups[4].Value;
        string pidStr = match.Groups[5].Value;

        if (!int.TryParse(pidStr, out int pid))
        {
            return null;
        }

        ParseEndpoint(localEndpoint, out string localAddress, out int localPort);
        ParseEndpoint(remoteEndpoint, out string remoteAddress, out int remotePort);

        if (protocol == "UDP" && string.IsNullOrWhiteSpace(state))
        {
            state = "*";
        }

        string? processName = ResolveProcessName(pid, processNameCache);

        return new NetworkConnection
        {
            Protocol = protocol,
            LocalAddress = localAddress,
            LocalPort = localPort,
            RemoteAddress = remoteAddress,
            RemotePort = remotePort,
            State = state,
            OwningProcessId = pid,
            ProcessName = processName
        };
    }

    private static void ParseEndpoint(string endpoint, out string address, out int port)
    {
        if (endpoint == "*:*")
        {
            address = "*";
            port = 0;
            return;
        }

        // IPv6 format: [::1]:port or [::]:port
        if (endpoint.StartsWith('['))
        {
            int closeBracket = endpoint.LastIndexOf(']');
            if (closeBracket >= 0 && closeBracket + 1 < endpoint.Length && endpoint[closeBracket + 1] == ':')
            {
                address = endpoint[..(closeBracket + 1)];
                string portStr = endpoint[(closeBracket + 2)..];
                int.TryParse(portStr, out port);
                return;
            }
        }

        // IPv4 format: 0.0.0.0:port
        int lastColon = endpoint.LastIndexOf(':');
        if (lastColon >= 0)
        {
            address = endpoint[..lastColon];
            string portStr = endpoint[(lastColon + 1)..];
            int.TryParse(portStr, out port);
            return;
        }

        address = endpoint;
        port = 0;
    }

    private static string? ResolveProcessName(int pid, Dictionary<int, string> cache)
    {
        if (pid == 0)
        {
            return "System Idle Process";
        }

        if (cache.TryGetValue(pid, out string? cached))
        {
            return cached;
        }

        try
        {
            using var proc = Process.GetProcessById(pid);
            string name = proc.ProcessName;
            cache[pid] = name;
            return name;
        }
        catch (ArgumentException)
        {
            // Process no longer exists.
            cache[pid] = "[Exited]";
            return "[Exited]";
        }
        catch (InvalidOperationException)
        {
            cache[pid] = "[Unknown]";
            return "[Unknown]";
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
