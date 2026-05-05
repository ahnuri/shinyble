using Android.Content;
using Android.OS;
using HannaDemoApp.Services.Background;

namespace HannaDemoApp;

// Starts and stops the Android foreground service used to keep BLE sessions active.
public class HNAAndroidBackgroundService : IHNABackgroundService
{
    // Uses application context so BLE can stay promoted to a foreground service when the activity is not in the foreground.
    public void StartService()
    {
        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(HNABleForegroundService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
    }

    public void StopService()
    {
        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(HNABleForegroundService));
        context.StopService(intent);
    }
}
