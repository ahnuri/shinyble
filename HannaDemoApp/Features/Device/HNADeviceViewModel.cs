using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HannaDemoApp.Core.DeviceHandlers;
using HannaDemoApp.Models;
using HannaDemoApp.Services.Ble;
using HannaDemoApp.Services.Ble.Permission;
using HannaDemoApp.Services.Dialog;
using HannaDemoApp.Services.Navigation;

namespace HannaDemoApp.Features.Device;

// Coordinates BLE scan, connect, disconnect, and command actions for the devices page.
public partial class HNADeviceViewModel : ObservableObject, IDisposable
{
    private readonly IHNABleService _bleService;
    private readonly IHNADialogService _dialogService;
    private readonly HNADeviceHandlerRegistry _handlerRegistry;
    private readonly IHNANavigationService _navigationService;
    private readonly IBlePermissionService _permissionService;
    private readonly Dictionary<string, HNABleDeviceModel> _trackedDevices = new();

    [ObservableProperty]
    private bool isBusy;


    public HNADeviceViewModel(
        IHNABleService bleService,
        IBlePermissionService permissionService,
        IHNANavigationService navigationService,
        IHNADialogService dialogService,
        HNADeviceHandlerRegistry handlerRegistry)
    {
        _bleService = bleService;
        _navigationService = navigationService;
        _permissionService = permissionService;
        _dialogService = dialogService;
        _handlerRegistry = handlerRegistry;
        AvailableDevices = bleService.AvailableDevices;
        ConnectedDevices = bleService.ConnectedDevices;

        _bleService.PropertyChanged += OnServicePropertyChanged;
        AvailableDevices.CollectionChanged += OnCollectionChanged;
        ConnectedDevices.CollectionChanged += OnCollectionChanged;

        foreach (var device in AvailableDevices)
        {
            TrackDevice(device);
        }

        foreach (var device in ConnectedDevices)
        {
            TrackDevice(device);
        }

        RaiseUiStateChanged();
    }

    public ObservableCollection<HNABleDeviceModel> AvailableDevices { get; }
    public ObservableCollection<HNABleDeviceModel> ConnectedDevices { get; }

    public string StatusMessage => _bleService.StatusText;
    public string ScanButtonText => _bleService.IsScanning ? "Stop Scan" : "Scan Devices";
    public bool HasAvailableDevices => AvailableDevices.Count > 0;
    public bool HasConnectedDevices => ConnectedDevices.Count > 0;
    public bool ShowHomeState => !HasConnectedDevices;
    public bool ShowAvailableEmptyState => !HasAvailableDevices;

    public bool ShowDeviceScanningState => _bleService.IsScanning;

    public string AvailableDeviceStateText => _bleService.IsScanning && !HasAvailableDevices ? "Scanning for devices. Make sure your device is powered on and within range." : "No devices found. Tap 'Scan Devices' to refresh the list.";
    public bool ShowConnectedEmptyState => !HasConnectedDevices;

    public bool IsConnectionValidationInProgress =>
        AvailableDevices.Any(device => device.IsConnecting) ||
        ConnectedDevices.Any(device => device.IsLoadingDeviceInfo);

    public void Dispose()
    {
        _bleService.PropertyChanged -= OnServicePropertyChanged;
        AvailableDevices.CollectionChanged -= OnCollectionChanged;
        ConnectedDevices.CollectionChanged -= OnCollectionChanged;

        foreach (var device in _trackedDevices.Values.ToList())
        {
            UntrackDevice(device);
        }
    }



    [RelayCommand]
    private async Task ToggleScan()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        if (_bleService.IsScanning)
        {
            _bleService.StopScan();
            RaiseUiStateChanged();
            IsBusy = false;
            return;
        }

        try
        {
#if ANDROID
            // Check and request location permission on Android before scanning, as it's required for BLE operations
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
                return;
#endif

            await _bleService.ScanAsync();
            RaiseUiStateChanged();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConnectDevice(HNABleDeviceModel? device)
    {
        if (device == null || device.IsConnected || device.IsConnecting)
        {
            return;
        }

        device.IsConnecting = true;
        try
        {
            await _bleService.ConnectAsync(device);
            RaiseUiStateChanged();
        }

        finally
        {
            device.IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectDevice(HNABleDeviceModel? device)
    {
        if (device == null || IsBusy || !device.IsConnected)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _bleService.DisconnectAsync(device);
            RaiseUiStateChanged();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShowCommands(HNABleDeviceModel? device)
    {
        if (device == null || !device.IsConnected)
        {
            return;
        }

        var handler = _handlerRegistry.GetHandler(device.ProductId);
        var commands = handler?.GetCommands() ?? [];
        if (commands.Count == 0)
        {
            return;
        }

        var action = await _dialogService.ShowActionSheetAsync(
            "Send Command to Meter",
            "Cancel",
            null,
            commands.ToArray());

        if (string.IsNullOrEmpty(action) || action == "Cancel")
        {
            return;
        }

        await _bleService.SendCommandAsync(device, action);
    }

    [RelayCommand]
    private async Task ViewLogs(HNABleDeviceModel? device)
    {
        if (device == null)
        {
            return;
        }

        try
        {
            await _navigationService.NavigateToLogHistoryAsync(device.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationError] Failed to navigate to log history: {ex.Message}");
        }
    }

    [RelayCommand]
    private void MeasureLiveDetails(HNABleDeviceModel? device)
    {
        if (device == null || !device.UsesLiveMeasurementUi)
        {
            return;
        }

        _ = _navigationService.NavigateToLiveDetailsAsync(device.Id);
    }



    private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IHNABleService.IsScanning) or nameof(IHNABleService.StatusText))
        {
            RaiseUiStateChanged();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (HNABleDeviceModel device in e.NewItems)
            {
                TrackDevice(device);
            }
        }

        if (e.OldItems != null)
        {
            foreach (HNABleDeviceModel device in e.OldItems)
            {
                UntrackDevice(device);
            }
        }

        RaiseUiStateChanged();
    }

    private void TrackDevice(HNABleDeviceModel device)
    {
        if (!_trackedDevices.TryAdd(device.Id, device))
        {
            return;
        }

        device.PropertyChanged += OnDevicePropertyChanged;
    }

    private void UntrackDevice(HNABleDeviceModel device)
    {
        if (!_trackedDevices.Remove(device.Id))
        {
            return;
        }

        device.PropertyChanged -= OnDevicePropertyChanged;
    }

    private void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HNABleDeviceModel.IsConnecting)
            or nameof(HNABleDeviceModel.IsLoadingDeviceInfo)
            or nameof(HNABleDeviceModel.IsConnected))
        {
            RaiseUiStateChanged();
        }
    }

    private void RaiseUiStateChanged()
    {
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(ScanButtonText));
        OnPropertyChanged(nameof(HasAvailableDevices));
        OnPropertyChanged(nameof(HasConnectedDevices));
        OnPropertyChanged(nameof(ShowHomeState));
        OnPropertyChanged(nameof(ShowAvailableEmptyState));
        OnPropertyChanged(nameof(AvailableDeviceStateText));
        OnPropertyChanged(nameof(ShowDeviceScanningState));
        OnPropertyChanged(nameof(ShowConnectedEmptyState));
        OnPropertyChanged(nameof(IsConnectionValidationInProgress));
    }
}
