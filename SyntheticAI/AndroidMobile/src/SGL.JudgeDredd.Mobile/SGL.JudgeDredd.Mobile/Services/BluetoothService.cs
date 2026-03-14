using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using SGL.JudgeDredd.Mobile.Models;
using System.Text;

#if ANDROID
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Java.Util;
using AdvertiseCallback = Android.Bluetooth.LE.AdvertiseCallback;
#endif

namespace SGL.JudgeDredd.Mobile.Services;

/// <summary>
/// Provides BLE scanning, connection, device analysis, and BLE advertising/broadcasting.
/// Uses Plugin.BLE for scanning and connecting, and native Android BluetoothLeAdvertiser
/// for BLE advertisement broadcasting on Android.
/// </summary>
public class BluetoothService
{
    // Constants

    /// <summary>
    /// Custom SyntheticAI service UUID used for BLE advertisements.
    /// 00005A01-0000-1000-8000-00805f9b34fb
    /// </summary>
    private static readonly Guid SyntheticAiServiceUuid =
        Guid.Parse("00005A01-0000-1000-8000-00805f9b34fb");

    /// <summary>Maximum payload size for BLE service data (BLE 4.x advertisement limit).</summary>
    private const int MaxServiceDataBytes = 20;

    // Plugin.BLE fields

    private readonly IBluetoothLE _ble;
    private readonly IAdapter _adapter;
    private CancellationTokenSource? _scanCts;
    private readonly List<BluetoothDeviceInfo> _discoveredDevices = new();

    // Broadcast state

    private bool _isBroadcasting;

#if ANDROID
    private BluetoothLeAdvertiser? _advertiser;
    private SyntheticAiAdvertiseCallback? _advertiseCallback;
#endif

    // Events

    /// <summary>Raised when a new BLE device is discovered or an existing entry is updated.</summary>
    public event EventHandler<BluetoothDeviceInfo>? DeviceDiscovered;

    /// <summary>Raised when a status message should be shown to the user.</summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>Raised when an error occurs during any BLE operation.</summary>
    public event EventHandler<string>? ErrorOccurred;

    // Public properties

    public bool IsScanning => _adapter.IsScanning;
    public bool IsAvailable => _ble.IsAvailable;
    public bool IsOn => _ble.IsOn;
    public bool IsBroadcasting => _isBroadcasting;
    public IReadOnlyList<BluetoothDeviceInfo> DiscoveredDevices => _discoveredDevices.AsReadOnly();

    // Constructor

    public BluetoothService()
    {
        _ble = CrossBluetoothLE.Current;
        _adapter = CrossBluetoothLE.Current.Adapter;
        _adapter.ScanTimeout = 15000; // 15 seconds
        _adapter.ScanMode = Plugin.BLE.Abstractions.Contracts.ScanMode.LowLatency;

        _adapter.DeviceDiscovered += OnDeviceDiscovered;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PERMISSIONS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Requests required Bluetooth and location runtime permissions.
    /// On Android 12+ (API 31) BLUETOOTH_SCAN, BLUETOOTH_CONNECT, and BLUETOOTH_ADVERTISE
    /// are runtime permissions. On older versions ACCESS_FINE_LOCATION is needed for scanning.
    /// Returns true when all required permissions have been granted.
    /// </summary>
    private async Task<bool> RequestPermissionsAsync()
    {
#if ANDROID
        // Android 12+ (API 31): BLUETOOTH_SCAN, BLUETOOTH_CONNECT, BLUETOOTH_ADVERTISE
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var bluetoothStatus = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
            if (bluetoothStatus != PermissionStatus.Granted)
            {
                bluetoothStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
                if (bluetoothStatus != PermissionStatus.Granted)
                {
                    RaiseError("Bluetooth permissions denied. Please grant BLUETOOTH_SCAN, " +
                               "BLUETOOTH_CONNECT, and BLUETOOTH_ADVERTISE in Settings.");
                    return false;
                }
            }
        }

        // ACCESS_FINE_LOCATION is required for BLE scanning on Android < 12,
        // and still useful on 12+ when neverForLocation is not set.
        var locationStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (locationStatus != PermissionStatus.Granted)
        {
            locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (locationStatus != PermissionStatus.Granted)
            {
                RaiseError("Location permission denied. ACCESS_FINE_LOCATION is required " +
                           "for Bluetooth scanning on Android.");
                return false;
            }
        }
#endif
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SCANNING  (Plugin.BLE)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts an asynchronous BLE scan. The scan runs until the adapter timeout
    /// elapses or <see cref="StopScan"/> is called.
    /// </summary>
    public async Task StartScanAsync()
    {
        if (!IsAvailable)
        {
            RaiseError("Bluetooth is not available on this device.");
            return;
        }

        if (!IsOn)
        {
            RaiseError("Bluetooth is turned off. Please enable it.");
            return;
        }

        if (!await RequestPermissionsAsync())
            return;

        _discoveredDevices.Clear();
        _scanCts = new CancellationTokenSource();

        RaiseStatus("Scanning for Bluetooth devices...");

        try
        {
            await _adapter.StartScanningForDevicesAsync(cancellationToken: _scanCts.Token);
            RaiseStatus($"Scan complete. Found {_discoveredDevices.Count} device(s).");
        }
        catch (System.OperationCanceledException)
        {
            RaiseStatus("Scan stopped.");
        }
        catch (Exception ex)
        {
            RaiseError($"Scan error: {ex.Message}");
        }
    }

    /// <summary>Stops any running BLE scan.</summary>
    public void StopScan()
    {
        _scanCts?.Cancel();

        // Plugin.BLE StopScanningForDevicesAsync is fire-and-forget safe here
        if (_adapter.IsScanning)
        {
            _ = Task.Run(async () =>
            {
                try { await _adapter.StopScanningForDevicesAsync(); }
                catch { /* best effort */ }
            });
        }

        RaiseStatus("Scan stopped.");
    }

    /// <summary>Async overload kept for backward compatibility with callers that await stop.</summary>
    public async Task StopScanAsync()
    {
        _scanCts?.Cancel();
        if (_adapter.IsScanning)
            await _adapter.StopScanningForDevicesAsync();
        RaiseStatus("Scan stopped.");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CONNECTION  (Plugin.BLE)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Connects to a previously discovered device by its ID string.
    /// Returns true on success, false on failure.
    /// </summary>
    public async Task<bool> ConnectToDeviceAsync(string deviceId)
    {
        try
        {
            var device = _adapter.DiscoveredDevices.FirstOrDefault(d => d.Id.ToString() == deviceId);
            if (device == null)
            {
                RaiseError("Device not found. It may have moved out of range.");
                return false;
            }

            await _adapter.ConnectToDeviceAsync(device);
            RaiseStatus($"Connected to {device.Name}");
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"Connection failed: {ex.Message}");
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DEVICE ANALYSIS  (Plugin.BLE connect + enumerate)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Connects to the device, enumerates its GATT services and characteristics,
    /// and performs a basic threat-level assessment.
    /// </summary>
    public async Task<BluetoothDeviceInfo> AnalyzeDeviceAsync(BluetoothDeviceInfo deviceInfo)
    {
        RaiseStatus($"Analyzing {deviceInfo.Name}...");

        try
        {
            var device = _adapter.DiscoveredDevices.FirstOrDefault(d => d.Id.ToString() == deviceInfo.Id);
            if (device == null)
            {
                deviceInfo.AnalysisResult = "Device no longer in range.";
                deviceInfo.ThreatLevel = "Unknown";
                return deviceInfo;
            }

            await _adapter.ConnectToDeviceAsync(device);
            var services = await device.GetServicesAsync();

            var analysis = new StringBuilder();
            analysis.AppendLine($"Device: {deviceInfo.Name}");
            analysis.AppendLine($"Signal: {deviceInfo.Rssi} dBm ({deviceInfo.SignalStrength})");
            analysis.AppendLine($"Services found: {services.Count}");

            foreach (var service in services)
            {
                analysis.AppendLine($"  Service: {service.Id}");
                var chars = await service.GetCharacteristicsAsync();
                foreach (var c in chars)
                {
                    analysis.AppendLine($"    Char: {c.Id} [{c.Properties}]");
                }
            }

            deviceInfo.AnalysisResult = analysis.ToString();
            deviceInfo.ServiceUuids = services.Select(s => s.Id.ToString()).ToList();

            // Flag known-suspicious service UUIDs
            bool suspicious = services.Any(s =>
            {
                var id = s.Id.ToString().ToLowerInvariant();
                return id.Contains("1812") ||   // HID (potential BadUSB / keystroke injection)
                       id.Contains("fee0") ||   // proprietary / potentially malicious
                       id.Contains("feedface"); // canary UUID
            });

            deviceInfo.ThreatLevel = suspicious ? "Suspicious" : "Safe";

            await _adapter.DisconnectDeviceAsync(device);
            RaiseStatus($"Analysis of {deviceInfo.Name} complete.");
        }
        catch (Exception ex)
        {
            deviceInfo.AnalysisResult = $"Analysis failed: {ex.Message}";
            deviceInfo.ThreatLevel = "Unknown";
        }

        return deviceInfo;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BLE BROADCAST / ADVERTISING  (Native Android BluetoothLeAdvertiser)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts a BLE advertisement broadcast containing <paramref name="message"/> as
    /// service data under the SyntheticAI service UUID.
    /// The message is truncated to 20 bytes if necessary.
    /// </summary>
    /// <param name="message">Short text payload (up to 20 bytes UTF-8).</param>
    public async Task StartBroadcastAsync(string message)
    {
        if (!await RequestPermissionsAsync())
            return;

#if ANDROID
        try
        {
            var bluetoothManager =
                (BluetoothManager?)Android.App.Application.Context.GetSystemService(Context.BluetoothService);

            var btAdapter = bluetoothManager?.Adapter;
            if (btAdapter == null || !btAdapter.IsEnabled)
            {
                RaiseError("Bluetooth adapter is not available or not enabled.");
                return;
            }

            if (!btAdapter.IsMultipleAdvertisementSupported)
            {
                RaiseError("This device does not support BLE advertising.");
                return;
            }

            _advertiser = btAdapter.BluetoothLeAdvertiser;
            if (_advertiser == null)
            {
                RaiseError("BluetoothLeAdvertiser is not available on this device.");
                return;
            }

            // Stop any existing broadcast first
            if (_isBroadcasting && _advertiseCallback != null)
            {
                _advertiser.StopAdvertising(_advertiseCallback);
                _advertiseCallback = null;
                _isBroadcasting = false;
            }

            // Build settings: low-latency for fastest broadcast rate
            var settingsBuilder = new AdvertiseSettings.Builder()!;
            settingsBuilder.SetAdvertiseMode(AdvertiseMode.LowLatency)!
                           .SetConnectable(false)!
                           .SetTimeout(0)!             // 0 = no timeout, broadcast indefinitely
                           .SetTxPowerLevel(AdvertiseTx.PowerHigh);

            var settings = settingsBuilder.Build()!;

            // Prepare service data: UTF-8 bytes, clamped to MaxServiceDataBytes
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            if (messageBytes.Length > MaxServiceDataBytes)
            {
                messageBytes = messageBytes[..MaxServiceDataBytes];
            }

            var parcelUuid = new ParcelUuid(UUID.FromString(SyntheticAiServiceUuid.ToString()));

            // Build advertise data with service UUID + service data payload
            var dataBuilder = new AdvertiseData.Builder()!;
            dataBuilder.SetIncludeDeviceName(false)!        // save space in the 31-byte adv packet
                       .SetIncludeTxPowerLevel(false)!
                       .AddServiceUuid(parcelUuid)!
                       .AddServiceData(parcelUuid, messageBytes);

            var advertiseData = dataBuilder.Build()!;

            // Build scan response (optional, provides device name if space allows)
            var scanResponseBuilder = new AdvertiseData.Builder()!;
            scanResponseBuilder.SetIncludeDeviceName(true)!
                               .SetIncludeTxPowerLevel(true);
            var scanResponse = scanResponseBuilder.Build()!;

            // Create callback
            _advertiseCallback = new SyntheticAiAdvertiseCallback(this);

            _advertiser.StartAdvertising(settings, advertiseData, scanResponse, _advertiseCallback);

            _isBroadcasting = true;
            RaiseStatus($"BLE broadcast started: \"{TruncateForDisplay(message)}\"");
        }
        catch (Exception ex)
        {
            _isBroadcasting = false;
            RaiseError($"Failed to start BLE broadcast: {ex.Message}");
        }
#else
        await Task.CompletedTask;
        RaiseError("BLE advertising is only supported on Android.");
#endif
    }

    /// <summary>Stops any active BLE advertisement broadcast.</summary>
    public void StopBroadcast()
    {
#if ANDROID
        try
        {
            if (_advertiser != null && _advertiseCallback != null)
            {
                _advertiser.StopAdvertising(_advertiseCallback);
                _advertiseCallback = null;
            }

            _isBroadcasting = false;
            RaiseStatus("BLE broadcast stopped.");
        }
        catch (Exception ex)
        {
            RaiseError($"Failed to stop BLE broadcast: {ex.Message}");
        }
#else
        RaiseError("BLE advertising is only supported on Android.");
#endif
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EMERGENCY BROADCAST  (legacy -- writes to connected GATT devices)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Broadcasts an emergency text message (and optional image) by connecting to
    /// every connectable discovered device and writing to the first writable
    /// GATT characteristic found. Also starts a BLE advertisement with the message.
    /// </summary>
    public async Task BroadcastEmergencyAsync(string message, byte[]? imageData = null)
    {
        RaiseStatus("Broadcasting emergency alert...");

        // Start a BLE advertisement so nearby passive scanners can see the alert
        await StartBroadcastAsync($"SOS:{message}");

        try
        {
            var connectedDevices = _adapter.ConnectedDevices.ToList();
            int sent = 0;

            foreach (var device in _adapter.DiscoveredDevices.Where(d => d.IsConnectable))
            {
                try
                {
                    bool wasConnected = connectedDevices.Contains(device);
                    if (!wasConnected)
                        await _adapter.ConnectToDeviceAsync(device);

                    var services = await device.GetServicesAsync();

                    // Prefer custom emergency service UUID, fall back to first available
                    var writeService =
                        services.FirstOrDefault(s =>
                            s.Id == Guid.Parse("0000fff0-0000-1000-8000-00805f9b34fb"))
                        ?? services.FirstOrDefault();

                    if (writeService != null)
                    {
                        var chars = await writeService.GetCharacteristicsAsync();
                        var writeChar = chars.FirstOrDefault(c => c.CanWrite);
                        if (writeChar != null)
                        {
                            var msgBytes = Encoding.UTF8.GetBytes($"EMERGENCY: {message}");
                            await writeChar.WriteAsync(msgBytes);
                            sent++;

                            // Stream image data in 512-byte chunks if provided
                            if (imageData is { Length: > 0 })
                            {
                                const int chunkSize = 512;
                                for (int i = 0; i < imageData.Length; i += chunkSize)
                                {
                                    int length = Math.Min(chunkSize, imageData.Length - i);
                                    var chunk = new byte[length];
                                    Array.Copy(imageData, i, chunk, 0, length);
                                    await writeChar.WriteAsync(chunk);
                                }
                            }
                        }
                    }

                    if (!wasConnected)
                        await _adapter.DisconnectDeviceAsync(device);
                }
                catch
                {
                    // Best effort per device; continue to next
                }
            }

            RaiseStatus($"Emergency broadcast sent to {sent} device(s).");
        }
        catch (Exception ex)
        {
            RaiseError($"Broadcast failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  INTERNAL HELPERS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Handles Plugin.BLE device-discovered events and maps to BluetoothDeviceInfo.</summary>
    private void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
    {
        var device = e.Device;

        var info = new BluetoothDeviceInfo
        {
            Id = device.Id.ToString(),
            Name = string.IsNullOrEmpty(device.Name) ? "Unknown Device" : device.Name,
            Rssi = device.Rssi,
            IsConnectable = device.IsConnectable,
            MacAddress = device.Id.ToString(),
            DeviceType = ClassifyDevice(device),
            DiscoveredAt = DateTime.Now
        };

        // Parse advertisement records for service UUIDs and manufacturer data
        if (device.AdvertisementRecords != null)
        {
            foreach (var record in device.AdvertisementRecords)
            {
                switch (record.Type)
                {
                    case AdvertisementRecordType.UuidsComplete128Bit:
                    case AdvertisementRecordType.UuidsComplete16Bit:
                    case AdvertisementRecordType.UuidsIncomple16Bit:
                    case AdvertisementRecordType.UuidsIncomplete128Bit:
                        if (record.Data?.Length > 0)
                            info.ServiceUuids.Add(BitConverter.ToString(record.Data));
                        break;

                    case AdvertisementRecordType.ManufacturerSpecificData:
                        if (record.Data?.Length > 0)
                            info.ManufacturerData = BitConverter.ToString(record.Data);
                        break;
                }
            }
        }

        // Merge into discovered list and raise event on UI thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = _discoveredDevices.FirstOrDefault(d => d.Id == info.Id);
            if (existing != null)
            {
                var index = _discoveredDevices.IndexOf(existing);
                _discoveredDevices[index] = info;
            }
            else
            {
                _discoveredDevices.Add(info);
            }

            DeviceDiscovered?.Invoke(this, info);
        });
    }

    /// <summary>Classifies a device type by name heuristics.</summary>
    private static string ClassifyDevice(IDevice device)
    {
        var name = device.Name?.ToLowerInvariant() ?? "";

        return name switch
        {
            _ when name.Contains("phone") || name.Contains("pixel") ||
                   name.Contains("samsung") || name.Contains("iphone") ||
                   name.Contains("galaxy") => "Phone",

            _ when name.Contains("watch") || name.Contains("band") ||
                   name.Contains("fit") || name.Contains("garmin") => "Wearable",

            _ when name.Contains("speaker") || name.Contains("buds") ||
                   name.Contains("airpod") || name.Contains("headphone") ||
                   name.Contains("jbl") || name.Contains("sony") => "Audio",

            _ when name.Contains("tv") || name.Contains("chromecast") ||
                   name.Contains("fire") || name.Contains("roku") => "Media",

            _ when name.Contains("printer") || name.Contains("scanner") ||
                   name.Contains("keyboard") || name.Contains("mouse") => "Peripheral",

            _ when name.Contains("lock") || name.Contains("sensor") ||
                   name.Contains("light") || name.Contains("plug") ||
                   name.Contains("hue") || name.Contains("nest") => "IoT/Smart Home",

            _ when name.Contains("laptop") || name.Contains("macbook") ||
                   name.Contains("surface") || name.Contains("thinkpad") => "Computer",

            _ => "Unknown"
        };
    }

    /// <summary>Raises StatusChanged on the main thread.</summary>
    private void RaiseStatus(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke(this, message));
    }

    /// <summary>Raises ErrorOccurred on the main thread.</summary>
    private void RaiseError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => ErrorOccurred?.Invoke(this, message));
    }

    /// <summary>Truncates a string to a reasonable display length.</summary>
    private static string TruncateForDisplay(string text, int maxLength = 30)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ANDROID ADVERTISE CALLBACK  (nested class, compiled only on Android)
    // ═══════════════════════════════════════════════════════════════════

#if ANDROID
    /// <summary>
    /// Callback for BluetoothLeAdvertiser.StartAdvertising.
    /// Routes success/failure back to the owning BluetoothService.
    /// </summary>
    private sealed class SyntheticAiAdvertiseCallback : AdvertiseCallback
    {
        private readonly BluetoothService _owner;

        public SyntheticAiAdvertiseCallback(BluetoothService owner) => _owner = owner;

        public override void OnStartSuccess(AdvertiseSettings? settingsInEffect)
        {
            base.OnStartSuccess(settingsInEffect);
            _owner.RaiseStatus("BLE advertisement active.");
        }

        public override void OnStartFailure(AdvertiseFailure errorCode)
        {
            base.OnStartFailure(errorCode);
            _owner._isBroadcasting = false;

            string reason = errorCode switch
            {
                AdvertiseFailure.DataTooLarge =>
                    "Advertisement data exceeds 31 bytes. Shorten the message.",
                AdvertiseFailure.TooManyAdvertisers =>
                    "Too many advertisers active. Stop another advertisement first.",
                AdvertiseFailure.AlreadyStarted =>
                    "An advertisement is already running.",
                AdvertiseFailure.InternalError =>
                    "Internal Bluetooth stack error. Try toggling Bluetooth.",
                AdvertiseFailure.FeatureUnsupported =>
                    "BLE advertising is not supported on this device.",
                _ =>
                    $"Unknown advertising error (code {(int)errorCode})."
            };

            _owner.RaiseError($"BLE broadcast failed: {reason}");
        }
    }
#endif
}
