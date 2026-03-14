namespace SGL.JudgeDredd.Mobile.Models;

public class WifiNetworkInfo
{
    public string Ssid { get; set; } = string.Empty;
    public string Bssid { get; set; } = string.Empty;
    public int SignalLevel { get; set; }
    public string SignalStrength => SignalLevel switch
    {
        > -50 => "Excellent",
        > -60 => "Good",
        > -70 => "Fair",
        > -80 => "Weak",
        _ => "Very Weak"
    };
    public string SecurityType { get; set; } = "Unknown";
    public int Frequency { get; set; }
    public string Band => Frequency > 4900 ? "5 GHz" : "2.4 GHz";
    public bool IsCurrentNetwork { get; set; }
    public bool IsSecure => SecurityType != "Open" && SecurityType != "None";
    public string SecurityRating { get; set; } = "Unknown";
    public double LatencyMs { get; set; }
    public List<string> TraceRoute { get; set; } = new();
    public string DnsServer { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string ExternalIp { get; set; } = string.Empty;
}
