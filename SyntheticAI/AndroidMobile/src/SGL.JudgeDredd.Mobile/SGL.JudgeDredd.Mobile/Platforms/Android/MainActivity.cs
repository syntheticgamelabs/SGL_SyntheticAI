using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using SGL.JudgeDredd.Mobile.Platforms.Android;

namespace SGL.JudgeDredd.Mobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize
                         | ConfigChanges.Orientation
                         | ConfigChanges.UiMode
                         | ConfigChanges.ScreenLayout
                         | ConfigChanges.SmallestScreenSize
                         | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int NotificationPermissionRequestCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android 13+ (API 33) requires runtime POST_NOTIFICATIONS permission
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            if (CheckSelfPermission(Android.Manifest.Permission.PostNotifications)
                != Permission.Granted)
            {
                RequestPermissions(
                    new[] { Android.Manifest.Permission.PostNotifications },
                    NotificationPermissionRequestCode);
            }
            else
            {
                StartProtectionService();
            }
        }
        else
        {
            StartProtectionService();
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == NotificationPermissionRequestCode)
        {
            StartProtectionService();
        }
    }

    private void StartProtectionService()
    {
        var serviceIntent = new Intent(this, typeof(JudgeDreddForegroundService));

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            StartForegroundService(serviceIntent);
        }
        else
        {
            StartService(serviceIntent);
        }
    }

    /// <summary>
    /// Override back press to minimize to notification instead of closing.
    /// The app stays alive via foreground service. Only shutdown from Settings
    /// or the notification "Shut Down" action.
    /// </summary>
    public override void OnBackPressed()
    {
        MoveTaskToBack(true);
    }

    protected override void OnDestroy()
    {
        // Do NOT stop the foreground service on activity destroy.
        // Service keeps running and shows notification.
        // Only explicit "Shut Down" action stops it.
        base.OnDestroy();
    }
}
