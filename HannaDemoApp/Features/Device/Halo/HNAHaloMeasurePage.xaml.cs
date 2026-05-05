using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using HannaDemoApp.Core.Constants;

namespace HannaDemoApp.Features.Device.Halo;

[QueryProperty(nameof(DeviceId), "deviceId")]
public partial class HNAHaloMeasurePage : ContentPage
{
    private readonly HNAHaloMeasurePageViewModel _viewModel;
    private bool _isNavigatingAway;

    // 🔥 Controls auto-scroll (same pattern as DevicePage)
    private bool _autoScrollEnabled = true;

    // Prevent recursive scroll events
    private bool _ignoreNextScrollEvent;

    public HNAHaloMeasurePage(HNAHaloMeasurePageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string? DeviceId
    {
        get => (string?)GetValue(DeviceIdProperty);
        set => SetValue(DeviceIdProperty, value);
    }

    public static readonly BindableProperty DeviceIdProperty =
        BindableProperty.Create(
            nameof(DeviceId),
            typeof(string),
            typeof(HNAHaloMeasurePage),
            null,
            propertyChanged: (bindable, _, newValue) =>
            {
                if (bindable is HNAHaloMeasurePage page && newValue is string id)
                {
                    page._viewModel.SetDeviceIdAndInitialize(id);
                }
            });

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.OnPageAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (_viewModel.MeasurementLogs != null)
            _viewModel.MeasurementLogs.CollectionChanged += OnLogsChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        if (_viewModel.MeasurementLogs != null)
            _viewModel.MeasurementLogs.CollectionChanged -= OnLogsChanged;

        _viewModel.OnPageDisappearing();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HNAHaloMeasurePageViewModel.HasDevice))
        {
            return;
        }

        if (_isNavigatingAway || _viewModel.HasDevice)
        {
            return;
        }

        _isNavigatingAway = true;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Shell.Current.GoToAsync($"///{HNAAppConstants.Routes.Devices}"));
        }
        finally
        {
            _isNavigatingAway = false;
        }
    }

    // 🔹 Initial load → scroll to bottom
   private async void OnMeasurementsLoaded(object? sender, EventArgs e)
{
    await Task.Delay(50);
    ScrollToBottom(false);
}

    // 🔹 Detect if user is near bottom (ONLY user-driven)
    private void OnMeasurementsScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (_ignoreNextScrollEvent)
        {
            _ignoreNextScrollEvent = false;
            return;
        }

        var total = _viewModel.MeasurementLogs?.Count ?? 0;
        if (total == 0)
            return;

        // 🔥 Same logic as working DevicePage
        _autoScrollEnabled = e.LastVisibleItemIndex >= total - 2;
    }

    // 🔹 Handle new incoming data
   private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
{
    if (e.Action != NotifyCollectionChangedAction.Add)
        return;

    if (!_autoScrollEnabled)
        return;

    if (MeasurementsView.ItemsSource is not IList list || list.Count == 0)
        return;

    var last = list[list.Count - 1];

    MainThread.BeginInvokeOnMainThread(async () =>
    {
        // 🔥 CRITICAL FIX → wait for layout
        await Task.Delay(50);

        _ignoreNextScrollEvent = true;

        MeasurementsView.ScrollTo(
            last,
            position: ScrollToPosition.End,
            animate: false);
    });
}

    private void ScrollToBottom(bool animate)
    {
        if (MeasurementsView.ItemsSource is not IList list || list.Count == 0)
            return;

        _ignoreNextScrollEvent = true;

        MeasurementsView.ScrollTo(
            list[list.Count - 1],
            position: ScrollToPosition.MakeVisible,
            animate: animate);
    }
}