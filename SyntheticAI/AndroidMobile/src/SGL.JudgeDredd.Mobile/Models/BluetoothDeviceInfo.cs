namespace SGL.JudgeDredd.Mobile.Models;

public class BluetoothDeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Unknown Device";
    public int Rssi { get; set; }
    public string SignalStrength => Rssi switch
    {
        > -50 => "Excellent",
        > -60 => "Good",
        > -70 => "Fair",
        > -80 => "Weak",
        _ => "Very Weak"
    };
    public string DeviceType { get; set; } = "Unknown";
    public string ManufacturerData { get; set; } = string.Empty;
    public bool IsConnectable { get; set; }
    public bool IsConnected { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.Now;
    public List<string> ServiceUuids { get; set; } = new();
    public string MacAddress { get; set; } = string.Empty;
    public string AnalysisResult { get; set; } = string.Empty;
    public string ThreatLevel { get; set; } = "Safe";
}
