namespace HannaDemoApp.Services.Navigation;

// Defines app-level navigation actions used by view models.
public interface IHNANavigationService
{
    Task NavigateToLandingAsync();
    Task NavigateToDevicesAsync();
    Task NavigateToLogHistoryAsync(string deviceId);

    Task NavigateToLiveDetailsAsync(string deviceId);
    Task NavigateToConnectedPhotometerDetailsAsync(string deviceId, string deviceName);
    Task NavigateToConnectedMultiMeterDetailsAsync(string deviceId, string deviceName);
    Task NavigateToLogDetailAsync(int logFileId);

}
