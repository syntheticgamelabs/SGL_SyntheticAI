using SGL.JudgeDredd.Mobile.Models;

namespace SGL.JudgeDredd.Mobile.Services;

public class TelemetryService
{
    private static readonly HashSet<string> KnownTelemetryPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "com.google.android.gms", "com.google.android.gsf",
        "com.facebook.katana", "com.facebook.orca", "com.facebook.services",
        "com.amazon.mShop.android.shopping",
        "com.microsoft.office.outlook", "com.microsoft.teams",
        "com.tiktok.android", "com.zhiliaoapp.musically",
        "com.snapchat.android", "com.twitter.android",
        "com.instagram.android", "com.whatsapp",
        "com.spotify.music", "com.netflix.mediaclient",
        "com.linkedin.android", "com.pinterest",
        "com.alexa.lucy", "com.amazon.dee.app"
    };

    private static readonly HashSet<string> DangerousPermissions = new()
    {
        "android.permission.RECORD_AUDIO",
        "android.permission.CAMERA",
        "android.permission.ACCESS_FINE_LOCATION",
        "android.permission.ACCESS_COARSE_LOCATION",
        "android.permission.READ_CONTACTS",
        "android.permission.READ_CALL_LOG",
        "android.permission.READ_SMS",
        "android.permission.READ_PHONE_STATE",
        "android.permission.READ_CALENDAR",
        "android.permission.BODY_SENSORS",
        "android.permission.ACTIVITY_RECOGNITION",
        "android.permission.ACCESS_BACKGROUND_LOCATION"
    };

    public event EventHandler<string>? StatusChanged;

    public async Task<List<TelemetryItem>> ScanForTelemetryAsync()
    {
        var items = new List<TelemetryItem>();

#if ANDROID
        await Task.Run(() =>
        {
            try
            {
                var context = Android.App.Application.Context;
                var pm = context.PackageManager;
                if (pm == null) return;

                var packages = pm.GetInstalledPackages(Android.Content.PM.PackageInfoFlags.Permissions);
                if (packages == null) return;

                var am = (Android.App.ActivityManager?)context.GetSystemService(Android.Content.Context.ActivityService);
                var runningServices = am?.GetRunningServices(200) ?? new List<Android.App.ActivityManager.RunningServiceInfo>();
                var runningPkgs = runningServices.Select(s => s.Service?.PackageName ?? "").ToHashSet();

                foreach (var pkg in packages)
                {
                    if (pkg.PackageName == null) continue;

                    var requestedPerms = pkg.RequestedPermissions ?? Array.Empty<string>();
                    var dangerous = requestedPerms.Where(p => DangerousPermissions.Contains(p)).ToList();

                    if (dangerous.Count == 0 && !KnownTelemetryPackages.Contains(pkg.PackageName))
                        continue;

                    var appInfo = pkg.ApplicationInfo;
                    bool isSystem = appInfo != null &&
                        (appInfo.Flags & Android.Content.PM.ApplicationInfoFlags.System) != 0;

                    var category = CategorizeApp(pkg.PackageName, dangerous);
                    var threat = AssessThreatLevel(dangerous, isSystem, KnownTelemetryPackages.Contains(pkg.PackageName));

                    items.Add(new TelemetryItem
                    {
                        PackageName = pkg.PackageName,
                        AppName = appInfo?.LoadLabel(pm)?.ToString() ?? pkg.PackageName,
                        Category = category,
                        Description = $"Has {dangerous.Count} sensitive permissions",
                        IsRunning = runningPkgs.Contains(pkg.PackageName),
                        IsSystemApp = isSystem,
                        DangerousPermissions = dangerous,
                        ThreatLevel = threat,
                        CanDisable = !isSystem,
                        DataCollectionType = GetDataCollectionType(dangerous)
                    });
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Scan error: {ex.Message}");
            }
        });
#endif

        return items.OrderByDescending(i => i.ThreatLevel == "High")
                   .ThenByDescending(i => i.IsRunning)
                   .ThenByDescending(i => i.DangerousPermissions.Count).ToList();
    }

    private string CategorizeApp(string packageName, List<string> perms)
    {
        if (perms.Any(p => p.Contains("RECORD_AUDIO"))) return "Audio Listener";
        if (perms.Any(p => p.Contains("CAMERA"))) return "Camera Access";
        if (perms.Any(p => p.Contains("LOCATION"))) return "Location Tracker";
        if (perms.Any(p => p.Contains("READ_CONTACTS") || p.Contains("READ_CALL_LOG"))) return "Contact/Call Data";
        if (perms.Any(p => p.Contains("READ_SMS"))) return "SMS Reader";
        return "Data Collector";
    }

    private string AssessThreatLevel(List<string> perms, bool isSystem, bool isKnownTelemetry)
    {
        if (perms.Count >= 5) return "High";
        if (isKnownTelemetry && perms.Count >= 2) return "High";
        if (perms.Count >= 3) return "Medium";
        if (perms.Any(p => p.Contains("RECORD_AUDIO") || p.Contains("CAMERA"))) return "Medium";
        return "Low";
    }

    private string GetDataCollectionType(List<string> perms)
    {
        var types = new List<string>();
        if (perms.Any(p => p.Contains("RECORD_AUDIO"))) types.Add("Audio");
        if (perms.Any(p => p.Contains("CAMERA"))) types.Add("Video");
        if (perms.Any(p => p.Contains("LOCATION"))) types.Add("Location");
        if (perms.Any(p => p.Contains("READ_CONTACTS"))) types.Add("Contacts");
        if (perms.Any(p => p.Contains("READ_SMS"))) types.Add("Messages");
        if (perms.Any(p => p.Contains("READ_PHONE_STATE"))) types.Add("Phone State");
        return types.Count > 0 ? string.Join(", ", types) : "General telemetry";
    }

    public async Task<bool> ForceStopAppAsync(string packageName)
    {
#if ANDROID
        try
        {
            var am = (Android.App.ActivityManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.ActivityService);
            if (am != null)
            {
                am.KillBackgroundProcesses(packageName);
                StatusChanged?.Invoke(this, $"Killed background processes for {packageName}");
                return true;
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Cannot force stop: {ex.Message}. Open Settings to force stop.");
        }
#endif
        await Task.CompletedTask;
        return false;
    }

    public async Task OpenAppSettingsAsync(string packageName)
    {
#if ANDROID
        try
        {
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionApplicationDetailsSettings);
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            intent.SetData(Android.Net.Uri.Parse($"package:{packageName}"));
            Android.App.Application.Context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Cannot open settings: {ex.Message}");
        }
#endif
        await Task.CompletedTask;
    }
}