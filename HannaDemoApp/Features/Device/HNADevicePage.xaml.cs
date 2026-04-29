using HannaDemoApp.Models;
using System.Collections.Specialized;

namespace HannaDemoApp.Features.Device;

public partial class HNADevicePage : ContentPage
{
    // Whether each device's list should auto-scroll to the newest row.
    // True by default; set to false when the user scrolls away from the bottom.
    private readonly Dictionary<string, bool> _autoScroll = new();

    // CollectionView reference per device — populated by the Loaded event so
    // we can call ScrollTo without needing the user to interact first.
    private readonly Dictionary<string, CollectionView> _measurementViews = new();

    // Stored delegates so CollectionChanged handlers can be removed cleanly.
    private readonly Dictionary<string, NotifyCollectionChangedEventHandler> _logHandlers = new();

    private readonly HNADeviceViewModel _viewModel;

    public HNADevicePage(HNADeviceViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.ConnectedDevices.CollectionChanged += OnConnectedDevicesChanged;
        foreach (var device in _viewModel.ConnectedDevices)
            SubscribeDevice(device);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ConnectedDevices.CollectionChanged -= OnConnectedDevicesChanged;
        foreach (var device in _viewModel.ConnectedDevices)
            UnsubscribeDevice(device);
    }

    private void OnConnectedDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (HNABleDeviceModel d in e.NewItems)
                SubscribeDevice(d);
        if (e.OldItems != null)
            foreach (HNABleDeviceModel d in e.OldItems)
                UnsubscribeDevice(d);
    }

    private void SubscribeDevice(HNABleDeviceModel device)
    {
        if (_logHandlers.ContainsKey(device.Id))
            return;

        _autoScroll[device.Id] = true;

        NotifyCollectionChangedEventHandler handler = (_, e) =>
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;
            if (!_autoScroll.TryGetValue(device.Id, out var enabled) || !enabled) return;
            if (!_measurementViews.TryGetValue(device.Id, out var cv)) return;
            if (device.MeasurementLogs.Count == 0) return;

            var last = device.MeasurementLogs[^1];
            MainThread.BeginInvokeOnMainThread(() => cv.ScrollTo(last, animate: false));
        };

        _logHandlers[device.Id] = handler;
        device.MeasurementLogs.CollectionChanged += handler;
    }

    private void UnsubscribeDevice(HNABleDeviceModel device)
    {
        if (_logHandlers.TryGetValue(device.Id, out var handler))
        {
            device.MeasurementLogs.CollectionChanged -= handler;
            _logHandlers.Remove(device.Id);
        }
        _autoScroll.Remove(device.Id);
        _measurementViews.Remove(device.Id);
    }

    // Fires when the CollectionView for a device card enters the visual tree.
    // Captures the reference early so ScrollTo works before the user has scrolled at all.
    private void OnMeasurementsLoaded(object? sender, EventArgs e)
    {
        if (sender is CollectionView cv && cv.BindingContext is HNABleDeviceModel device)
            _measurementViews[device.Id] = cv;
    }

    // Tracks whether the user is at the bottom of each device's list.
    // Scrolling away disables auto-scroll; reaching the last row re-enables it.
    private void OnMeasurementsScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (sender is not CollectionView cv || cv.BindingContext is not HNABleDeviceModel device)
            return;

        _measurementViews[device.Id] = cv;

        var totalItems = device.MeasurementLogs.Count;
        if (totalItems == 0) return;

        _autoScroll[device.Id] = e.LastVisibleItemIndex >= totalItems - 2;
    }
}
