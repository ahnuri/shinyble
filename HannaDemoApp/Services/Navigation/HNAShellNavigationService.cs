using HannaDemoApp.Core.Constants;
using HannaDemoApp.Features.Device;

namespace HannaDemoApp.Services.Navigation;

// Uses MAUI Shell to navigate between application routes.
public class HNAShellNavigationService : IHNANavigationService
{
    public Task NavigateToLandingAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.GoToAsync($"//{HNAAppConstants.Routes.Landing}"));
    }

    public Task NavigateToDevicesAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.GoToAsync($"//{HNAAppConstants.Routes.Devices}"));
    }

    public Task NavigateToLogHistoryAsync(string deviceId)
    {
        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.GoToAsync($"///{HNAAppConstants.Routes.LogHistory}?deviceId={deviceId}"));
    }

    public Task NavigateToLiveDetailsAsync(string deviceId)
    {
        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.GoToAsync($"{HNAAppConstants.Routes.LiveDetails}?deviceId={deviceId}"));
    }

    public Task NavigateToLogDetailAsync(int logFileId)
    {
        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.GoToAsync($"{HNAAppConstants.Routes.LogDetail}?logFileId={logFileId}"));
    }
    
}
