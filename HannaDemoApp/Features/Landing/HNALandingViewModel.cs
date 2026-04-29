using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HannaDemoApp.Services.Ble;
using HannaDemoApp.Services.Dialog;
using HannaDemoApp.Services.Navigation;

namespace HannaDemoApp.Features.Landing;

/// <summary>
/// ViewModel for the landing page. Manages device connection and navigation.
/// </summary>
public partial class HNALandingViewModel : ObservableObject
{
    private readonly IHNANavigationService _navigationService;
    private readonly IHNADialogService _dialogService;

    public HNALandingViewModel(IHNANavigationService navigationService, IHNADialogService dialogService)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    [RelayCommand]
    private Task ConnectDevice() => _navigationService.NavigateToDevicesAsync();

    [RelayCommand]
    private Task NavigateToLogHistory() => _navigationService.NavigateToLogHistoryAsync(string.Empty);

    [RelayCommand]
    private async Task OnToolbarClicked()
    {
        await _dialogService.ShowAlertAsync(
            "Work In Progress",
            "Cloud status indicators will be shown here in a future update.",
            "OK");
    }
}
