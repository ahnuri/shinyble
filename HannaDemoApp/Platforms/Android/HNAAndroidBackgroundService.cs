using Android.Content;
using HannaDemoApp.Services.Background;
using Microsoft.Maui.ApplicationModel;

namespace HannaDemoApp;

// Starts and stops the Android foreground service used to keep BLE sessions active.
public class HNAAndroidBackgroundService : IHNABackgroundService
{
    // Starts the Android BLE foreground service when an activity is available.
    public void StartService()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            return;
        }

        var intent = new Intent(activity, typeof(HNABleForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            activity.StartForegroundService(intent);
        }
        else
        {
            activity.StartService(intent);
        }
    }

    // Stops the Android BLE foreground service when an activity is available.
    public void StopService()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            return;
        }

        var intent = new Intent(activity, typeof(HNABleForegroundService));
        activity.StopService(intent);
    }
}
