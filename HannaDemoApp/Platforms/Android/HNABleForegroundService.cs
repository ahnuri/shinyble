using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using AndroidX.Core.App;
using HannaDemoApp.Core.Constants;

namespace HannaDemoApp;

// Runs a foreground notification while a BLE device is connected on Android.
[Service(ForegroundServiceType = Android.Content.PM.ForegroundService.TypeConnectedDevice)]
public class HNABleForegroundService : Service
{
    private PowerManager.WakeLock? _cpuWakeLock;

    // Foreground services are started only; binding is not supported.
    public override IBinder? OnBind(Intent? intent) => null;

    // Starts the BLE foreground notification and keeps the service alive.
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();

        var context = ApplicationContext ?? throw new InvalidOperationException("Android application context unavailable.");

#pragma warning disable CS8602
        var notificationBuilder = new NotificationCompat.Builder(context, HNAAppConstants.AndroidNotification.ChannelId)!
            .SetContentTitle("Hanna Meter Connected")
            .SetContentText("Maintaining connection for live measurements.")
            .SetSmallIcon(Android.Resource.Drawable.StatSysDataBluetooth)
            .SetOngoing(true);

        var notification = notificationBuilder.Build()
            ?? throw new InvalidOperationException("Unable to build foreground notification.");
#pragma warning restore CS8602

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            StartForeground(HNAAppConstants.AndroidNotification.ForegroundServiceNotificationId, notification, Android.Content.PM.ForegroundService.TypeConnectedDevice);
        }
        else
        {
            StartForeground(HNAAppConstants.AndroidNotification.ForegroundServiceNotificationId, notification);
        }

        AcquireCpuWakeLock();

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        ReleaseCpuWakeLock();
        base.OnDestroy();
    }

    /// <summary>
    /// Without a partial wake lock, some devices stop delivering CPU work (including app BLE callbacks) for long periods when locked, even though GATT stays "connected".
    /// </summary>
    private void AcquireCpuWakeLock()
    {
        try
        {
            var pm = GetSystemService(PowerService) as PowerManager;
            if (pm == null)
            {
                return;
            }

            ReleaseCpuWakeLock();
            _cpuWakeLock = pm.NewWakeLock(WakeLockFlags.Partial, $"{PackageName}:BleMeasurementStream");
            _cpuWakeLock.SetReferenceCounted(false);
            _cpuWakeLock.Acquire();
        }
        catch (Exception ex)
        {
            Log.Warn(nameof(HNABleForegroundService), $"WakeLock acquire failed: {ex.Message}");
        }
    }

    private void ReleaseCpuWakeLock()
    {
        try
        {
            _cpuWakeLock?.Release();
        }
        catch (Exception ex)
        {
            Log.Warn(nameof(HNABleForegroundService), $"WakeLock release failed: {ex.Message}");
        }
        finally
        {
            _cpuWakeLock = null;
        }
    }

    private void CreateNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var channel = new NotificationChannel(HNAAppConstants.AndroidNotification.ChannelId, "Hanna BLE Service", NotificationImportance.Low)
        {
            Description = "Keep BLE connection active in the background"
        };

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(channel);
    }
}
