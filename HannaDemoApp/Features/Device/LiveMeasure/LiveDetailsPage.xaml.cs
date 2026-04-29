using System.Collections;
using System.Collections.Specialized;

namespace HannaDemoApp.Features.Device.LiveMeasure;

[QueryProperty(nameof(DeviceId), "deviceId")]
public partial class LiveDetailsPage : ContentPage
{
    private readonly LiveDetailsViewModel _viewModel;

    // 🔥 Controls auto-scroll (same pattern as DevicePage)
    private bool _autoScrollEnabled = true;

    // Prevent recursive scroll events
    private bool _ignoreNextScrollEvent;

    public LiveDetailsPage(LiveDetailsViewModel viewModel)
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
            typeof(LiveDetailsPage),
            null,
            propertyChanged: (bindable, _, newValue) =>
            {
                if (bindable is LiveDetailsPage page && newValue is string id)
                {
                    page._viewModel.SetDeviceIdAndInitialize(id);
                }
            });

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.OnPageAppearing();

        if (_viewModel.MeasurementLogs != null)
            _viewModel.MeasurementLogs.CollectionChanged += OnLogsChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_viewModel.MeasurementLogs != null)
            _viewModel.MeasurementLogs.CollectionChanged -= OnLogsChanged;

        _viewModel.OnPageDisappearing();
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