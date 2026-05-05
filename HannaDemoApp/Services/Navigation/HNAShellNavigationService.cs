using HannaDemoApp.Core.Constants;

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

    public Task NavigateToConnectedPhotometerDetailsAsync(string deviceId, string deviceName)
    {
        var escapedDeviceId = Uri.EscapeDataString(deviceId);
        var escapedDeviceName = Uri.EscapeDataString(deviceName);

        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.GoToAsync(
                $"{HNAAppConstants.Routes.ConnectedPhotometerDetails}?deviceId={escapedDeviceId}&deviceName={escapedDeviceName}"));
    }

    public Task NavigateToConnectedMultiMeterDetailsAsync(string deviceId, string deviceName)
    {
        var escapedDeviceId = Uri.EscapeDataString(deviceId);
        var escapedDeviceName = Uri.EscapeDataString(deviceName);

        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.GoToAsync(
                $"{HNAAppConstants.Routes.ConnectedMultiMeterDetails}?deviceId={escapedDeviceId}&deviceName={escapedDeviceName}"));
    }

    public Task NavigateToLogDetailAsync(int logFileId)
    {
        return MainThread.InvokeOnMainThreadAsync(
            () => Shell.Current.GoToAsync($"{HNAAppConstants.Routes.LogDetail}?logFileId={logFileId}"));
    }
    
}
