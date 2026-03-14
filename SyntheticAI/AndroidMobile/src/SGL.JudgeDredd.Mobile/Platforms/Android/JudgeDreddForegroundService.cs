#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;

namespace SGL.JudgeDredd.Mobile.Platforms.Android;

/// <summary>
/// Android foreground service that keeps the app alive when the user navigates away.
/// Shows a persistent notification with a "Shut Down" action button so the user
/// can explicitly stop the service and exit the app.
/// </summary>
[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public class JudgeDreddForegroundService : Service
{
    public const string ChannelId = "judgedredd_protection";
    public const int NotificationId = 9001;
    public const string ActionShutdown = "com.syntheticgamelabs.judgedredd.ACTION_SHUTDOWN";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Handle shutdown action from notification button
        if (intent?.Action == ActionShutdown)
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();

            // Kill the app process
            var activity = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            activity?.FinishAffinity();
            Java.Lang.JavaSystem.Exit(0);
            return StartCommandResult.NotSticky;
        }

        CreateNotificationChannel();

        // Tapping the notification opens the app
        var notificationIntent = new Intent(this, typeof(MainActivity));
        notificationIntent.SetFlags(ActivityFlags.SingleTop);

        var pendingIntent = PendingIntent.GetActivity(
            this, 0, notificationIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        // Shutdown action button in the notification
        var shutdownIntent = new Intent(this, typeof(JudgeDreddForegroundService));
        shutdownIntent.SetAction(ActionShutdown);
        var shutdownPendingIntent = PendingIntent.GetService(
            this, 1, shutdownIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var notification = new Notification.Builder(this, ChannelId)
            .SetContentTitle("SyntheticAI Protection Active")
            .SetContentText("Real-time security monitoring is running")
            .SetSmallIcon(global::Android.Resource.Drawable.IcLockIdleLock)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .AddAction(new Notification.Action.Builder(
                global::Android.Resource.Drawable.IcMenuCloseClearCancel,
                "Shut Down",
                shutdownPendingIntent).Build())
            .Build()!;

        StartForeground(NotificationId, notification);

        return StartCommandResult.Sticky;
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                "SyntheticAI Protection",
                NotificationImportance.Low)
            {
                Description = "Keeps SyntheticAI security monitoring active in the background"
            };

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.CreateNotificationChannel(channel);
        }
    }

    public override void OnDestroy()
    {
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }
}
#endif
