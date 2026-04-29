namespace HannaDemoApp.Features.LogHistory;

[QueryProperty(nameof(DeviceId), "deviceId")]
public partial class HNALogHistoryPage : ContentPage
{
    private readonly HNALogHistoryViewModel _viewModel;

    public string? DeviceId
    {
        get => _viewModel.DeviceId;
        set => _viewModel.DeviceId = value;
    }

    public HNALogHistoryPage(HNALogHistoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        await _viewModel.LoadLogsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        // Restore tab bar if user navigates away while selection mode is active.
        Shell.SetTabBarIsVisible(this, true);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HNALogHistoryViewModel.IsSelectionMode))
            Shell.SetTabBarIsVisible(this, !_viewModel.IsSelectionMode);
    }
}
