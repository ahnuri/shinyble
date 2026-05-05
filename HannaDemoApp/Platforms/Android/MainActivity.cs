using Android.App;
using Android.Content.PM;
using Android.OS;
using HannaDemoApp.Services.Ble;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

namespace HannaDemoApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density , ScreenOrientation = ScreenOrientation.Portrait)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnPause()
    {
        try
        {
            var bleService = IPlatformApplication.Current?.Services?.GetService<IHNABleService>();
            bleService?.DrainHaloPersistenceBatches();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BLE] OnPause drain error: {ex}");
        }

        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();

        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services == null)
            {
                return;
            }

            var bleService = services.GetService<IHNABleService>();
            if (bleService != null)
            {
                _ = bleService.ResumeLiveUpdatesAsync().ContinueWith(
                    t => System.Diagnostics.Debug.WriteLine($"[BLE] ResumeLiveUpdatesAsync failed: {t.Exception}"),
                    System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BLE] OnResume error: {ex}");
        }
    }
}


