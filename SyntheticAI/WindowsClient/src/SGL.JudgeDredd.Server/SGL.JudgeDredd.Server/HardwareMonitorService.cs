using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server;

/// <summary>
/// Provides real hardware monitoring data: CPU, RAM, GPU, temperatures, fan speeds.
/// Uses LibreHardwareMonitor for reliable sensor data (temperatures, fan speeds).
/// Falls back to WMI on Windows and /proc on Linux for basic metrics.
/// </summary>
public class HardwareMonitorService : IDisposable
{
    private readonly System.Timers.Timer _pollTimer;
    private HardwareSnapshot _lastSnapshot = new();
    private readonly object _lock = new();
    private bool _disposed;

    // CPU tracking
    private TimeSpan _prevTotalCpu;
    private DateTime _prevCpuSample;

    // LibreHardwareMonitor computer instance (Windows only)
    private Computer? _computer;

    /// <summary>Event raised when hardware data is refreshed.</summary>
    public event EventHandler<HardwareSnapshot>? SnapshotUpdated;

    public HardwareMonitorService(int pollIntervalMs = 3000)
    {
        _prevCpuSample = DateTime.UtcNow;
        _prevTotalCpu = TimeSpan.Zero;

        _pollTimer = new System.Timers.Timer(pollIntervalMs);
        _pollTimer.Elapsed += (_, _) => PollHardware();
        _pollTimer.AutoReset = true;
    }

    public void Start()
    {
        // Initialize LibreHardwareMonitor on Windows for reliable sensor data
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true,
                };
                _computer.Open();
                SglLogger.Information("LibreHardwareMonitor initialized for sensor data.");
            }
            catch (Exception ex)
            {
                SglLogger.Warning($"LibreHardwareMonitor init failed (will use WMI fallback): {ex.Message}");
                _computer = null;
            }
        }

        PollHardware(); // Initial poll
        _pollTimer.Start();
    }

    public void Stop() => _pollTimer.Stop();

    public HardwareSnapshot GetSnapshot()
    {
        lock (_lock)
            return _lastSnapshot;
    }

    private void PollHardware()
    {
        try
        {
            var snapshot = new HardwareSnapshot { Timestamp = DateTime.UtcNow };

            // CPU Usage (cross-platform via Process)
            snapshot.CpuUsagePercent = GetCpuUsagePercent();

            // CPU info
            snapshot.CpuName = GetCpuName();
            snapshot.CpuCores = Environment.ProcessorCount;

            // RAM
            GetMemoryInfo(out var totalMB, out var usedMB, out var availMB);
            snapshot.TotalRamMB = totalMB;
            snapshot.UsedRamMB = usedMB;
            snapshot.AvailableRamMB = availMB;
            snapshot.RamUsagePercent = totalMB > 0 ? Math.Round((double)usedMB / totalMB * 100, 1) : 0;

            // GPU (WMI on Windows)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GetGpuInfo(snapshot);
                GetSensorData(snapshot);
            }

            // Disk I/O
            GetDiskInfo(snapshot);

            // Process-level resource usage of this app
            var proc = Process.GetCurrentProcess();
            snapshot.AppCpuTimeSec = proc.TotalProcessorTime.TotalSeconds;
            snapshot.AppMemoryMB = proc.WorkingSet64 / (1024 * 1024);

            lock (_lock)
                _lastSnapshot = snapshot;

            SnapshotUpdated?.Invoke(this, snapshot);
        }
        catch (Exception ex)
        {
            SglLogger.Error($"Hardware monitor poll error: {ex.Message}");
        }
    }

    private double GetCpuUsagePercent()
    {
        try
        {
            var now = DateTime.UtcNow;
            var allProcesses = Process.GetProcesses();
            var totalCpu = TimeSpan.Zero;

            foreach (var p in allProcesses)
            {
                try { totalCpu += p.TotalProcessorTime; }
                catch { /* access denied for some processes */ }
            }

            var elapsed = (now - _prevCpuSample).TotalMilliseconds;
            if (elapsed < 100 || _prevTotalCpu == TimeSpan.Zero)
            {
                _prevTotalCpu = totalCpu;
                _prevCpuSample = now;
                return 0;
            }

            var cpuDelta = (totalCpu - _prevTotalCpu).TotalMilliseconds;
            var usage = cpuDelta / (elapsed * Environment.ProcessorCount) * 100;

            _prevTotalCpu = totalCpu;
            _prevCpuSample = now;

            return Math.Clamp(Math.Round(usage, 1), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static string GetCpuName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                    return obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
            }
            else if (File.Exists("/proc/cpuinfo"))
            {
                var lines = File.ReadAllLines("/proc/cpuinfo");
                var modelLine = lines.FirstOrDefault(l => l.StartsWith("model name", StringComparison.OrdinalIgnoreCase));
                if (modelLine != null)
                    return modelLine.Split(':').Last().Trim();
            }
        }
        catch { }
        return $"{Environment.ProcessorCount}-core CPU";
    }

    private static void GetMemoryInfo(out long totalMB, out long usedMB, out long availMB)
    {
        totalMB = 0; usedMB = 0; availMB = 0;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    totalMB = (long)(memStatus.ullTotalPhys / (1024 * 1024));
                    availMB = (long)(memStatus.ullAvailPhys / (1024 * 1024));
                    usedMB = totalMB - availMB;
                }
            }
            else if (File.Exists("/proc/meminfo"))
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:"))
                        totalMB = long.Parse(line.Split(':')[1].Trim().Split(' ')[0]) / 1024;
                    else if (line.StartsWith("MemAvailable:"))
                        availMB = long.Parse(line.Split(':')[1].Trim().Split(' ')[0]) / 1024;
                }
                usedMB = totalMB - availMB;
            }
        }
        catch { }
    }

    private static void GetGpuInfo(HardwareSnapshot snapshot)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "Unknown GPU";
                var ram = obj["AdapterRAM"] is uint vram ? vram / (1024 * 1024) : 0;
                var driver = obj["DriverVersion"]?.ToString() ?? "";

                snapshot.GpuName = name;
                snapshot.GpuMemoryMB = (int)ram;
                snapshot.GpuDriverVersion = driver;
                break; // Use primary GPU
            }

            // GPU usage via Win32_PerfFormattedData_GPUPerformanceCounters (Windows 10+)
            try
            {
                using var gpuSearcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE '%engtype_3D%'");
                double maxUtil = 0;
                foreach (var obj in gpuSearcher.Get())
                {
                    var util = Convert.ToDouble(obj["UtilizationPercentage"] ?? 0);
                    if (util > maxUtil) maxUtil = util;
                }
                snapshot.GpuUsagePercent = Math.Round(maxUtil, 1);
            }
            catch
            {
                // GPU perf counters not available on all systems
            }
        }
        catch { }
    }

    /// <summary>
    /// Reads temperature and fan speed from LibreHardwareMonitor sensors.
    /// Falls back to WMI if LibreHardwareMonitor is unavailable.
    /// </summary>
    private void GetSensorData(HardwareSnapshot snapshot)
    {
        bool gotTemp = false, gotFan = false;

        // Try LibreHardwareMonitor first (reliable on most hardware)
        if (_computer != null)
        {
            try
            {
                var fans = new List<FanInfo>();
                foreach (var hw in _computer.Hardware)
                {
                    hw.Update();
                    foreach (var subHw in hw.SubHardware)
                        subHw.Update();

                    // CPU temperature
                    if (hw.HardwareType == HardwareType.Cpu)
                    {
                        foreach (var sensor in hw.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                            {
                                var val = sensor.Value.Value;
                                // Prefer "CPU Package" or "Core (Tctl/Tdie)" temp
                                if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                                    sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase))
                                {
                                    snapshot.CpuTemperatureC = Math.Round(val, 1);
                                    gotTemp = true;
                                }
                                else if (!gotTemp && val > 0 && val < 150)
                                {
                                    snapshot.CpuTemperatureC = Math.Round(val, 1);
                                    gotTemp = true;
                                }
                            }
                        }
                    }

                    // GPU temperature
                    if (hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd ||
                        hw.HardwareType == HardwareType.GpuIntel)
                    {
                        foreach (var sensor in hw.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                            {
                                snapshot.GpuTemperatureC = Math.Round(sensor.Value.Value, 1);
                                break;
                            }
                        }
                    }

                    // Fan speeds from any hardware component
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value.Value > 0)
                        {
                            fans.Add(new FanInfo
                            {
                                Name = sensor.Name,
                                SpeedRpm = (int)sensor.Value.Value,
                                StatusInfo = "Active"
                            });
                        }
                    }

                    // Also check sub-hardware (motherboard often has fan sensors in sub-hardware)
                    foreach (var subHw in hw.SubHardware)
                    {
                        foreach (var sensor in subHw.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature && !gotTemp && sensor.Value.HasValue)
                            {
                                if (subHw.HardwareType == HardwareType.Cpu)
                                {
                                    snapshot.CpuTemperatureC = Math.Round(sensor.Value.Value, 1);
                                    gotTemp = true;
                                }
                            }
                            if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value.Value > 0)
                            {
                                fans.Add(new FanInfo
                                {
                                    Name = sensor.Name,
                                    SpeedRpm = (int)sensor.Value.Value,
                                    StatusInfo = "Active"
                                });
                            }
                        }
                    }
                }

                if (fans.Count > 0)
                {
                    snapshot.Fans = fans;
                    gotFan = true;
                }
            }
            catch (Exception ex)
            {
                SglLogger.Warning($"LibreHardwareMonitor sensor read failed: {ex.Message}");
            }
        }

        // WMI fallback for temperature
        if (!gotTemp)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                foreach (var obj in searcher.Get())
                {
                    var tempKelvin = Convert.ToDouble(obj["CurrentTemperature"]) / 10.0;
                    var tempCelsius = tempKelvin - 273.15;
                    if (tempCelsius > 0 && tempCelsius < 150)
                    {
                        snapshot.CpuTemperatureC = Math.Round(tempCelsius, 1);
                        break;
                    }
                }
            }
            catch { }
        }

        // WMI fallback for fans
        if (!gotFan)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Fan");
                var fans = new List<FanInfo>();
                foreach (var obj in searcher.Get())
                {
                    fans.Add(new FanInfo
                    {
                        Name = obj["Name"]?.ToString() ?? "Fan",
                        SpeedRpm = Convert.ToInt32(obj["DesiredSpeed"] ?? 0),
                        StatusInfo = obj["StatusInfo"]?.ToString() ?? "Unknown"
                    });
                }
                if (fans.Count > 0)
                    snapshot.Fans = fans;
            }
            catch { }
        }
    }

    private static void GetDiskInfo(HardwareSnapshot snapshot)
    {
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToList();
            snapshot.Disks = drives.Select(d => new DiskInfo
            {
                Name = d.Name,
                Label = d.VolumeLabel,
                TotalGB = (int)(d.TotalSize / (1024L * 1024 * 1024)),
                FreeGB = (int)(d.AvailableFreeSpace / (1024L * 1024 * 1024)),
                UsagePercent = Math.Round((1.0 - (double)d.AvailableFreeSpace / d.TotalSize) * 100, 1)
            }).ToList();
        }
        catch { }
    }

    // Windows native memory status
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer.Stop();
        _pollTimer.Dispose();
        try { _computer?.Close(); } catch { }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A point-in-time snapshot of hardware metrics.
/// </summary>
public class HardwareSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // CPU
    public string CpuName { get; set; } = "Unknown";
    public int CpuCores { get; set; }
    public double CpuUsagePercent { get; set; }
    public double CpuTemperatureC { get; set; }

    // RAM
    public long TotalRamMB { get; set; }
    public long UsedRamMB { get; set; }
    public long AvailableRamMB { get; set; }
    public double RamUsagePercent { get; set; }

    // GPU
    public string GpuName { get; set; } = "Unknown";
    public int GpuMemoryMB { get; set; }
    public double GpuUsagePercent { get; set; }
    public double GpuTemperatureC { get; set; }
    public string GpuDriverVersion { get; set; } = "";

    // Fans
    public List<FanInfo> Fans { get; set; } = [];

    // Disks
    public List<DiskInfo> Disks { get; set; } = [];

    // App-specific
    public double AppCpuTimeSec { get; set; }
    public long AppMemoryMB { get; set; }
}

public class FanInfo
{
    public string Name { get; set; } = "";
    public int SpeedRpm { get; set; }
    public string StatusInfo { get; set; } = "";
}

public class DiskInfo
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public int TotalGB { get; set; }
    public int FreeGB { get; set; }
    public double UsagePercent { get; set; }
}
