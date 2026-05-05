using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HannaDemoApp.Models;
using HannaDemoApp.Services.Ble;

namespace HannaDemoApp.Features.Device.Halo;

public partial class HNAHaloMeasurePageViewModel : ObservableObject
{
    private readonly IHNABleService _bleService;
    private HNABleDeviceModel? _device;

    [ObservableProperty]
    private string? deviceId;

    [ObservableProperty]
    private ObservableCollection<HNAMeasurementLogModel> measurementLogs = [];

    [ObservableProperty]
    private string deviceDisplayName = "Halo Live Readings";

    [ObservableProperty]
    private string deviceSubtitle = "No connected Halo device was found.";

    [ObservableProperty]
    private string batteryStatusText = "Battery: Unavailable";

    [ObservableProperty]
    private string totalLiveCountText = "Total Live Count: 0";

    [ObservableProperty]
    private bool hasMeasurements;

    [ObservableProperty]
    private bool showEmptyState = true;

    [ObservableProperty]
    private string emptyStateText = "Connect a Halo device and tap 'View Live' again.";

    public HNAHaloMeasurePageViewModel(IHNABleService bleService)
    {
        _bleService = bleService;
    }

    public HNABleDeviceModel? Device
    {
        get => _device;
        set
        {
            if (ReferenceEquals(_device, value))
                return;

            UnsubscribeDevice();

            _device = value;

            if (_device != null)
            {
                _device.PropertyChanged += OnDevicePropertyChanged;
                SubscribeDevice(_device);
            }

            OnPropertyChanged(nameof(HasDevice));
            UpdateDeviceDisplayProperties();
        }
    }

    public bool HasDevice => Device != null;

    public void SetDeviceIdAndInitialize(string? id)
    {
        DeviceId = id;
        LoadSelectedDevice();
    }

    public async void OnPageAppearing()
    {
        try
        {
            LoadSelectedDevice();

            if (Device != null)
            {
                await _bleService.SendCommandAsync(Device, "get battery");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiveDetailsError] {ex.Message}");
        }
    }

    public void OnPageDisappearing()
    {
        UnsubscribeDevice();
    }

    private void LoadSelectedDevice()
    {
        Device = string.IsNullOrWhiteSpace(DeviceId)
            ? null
            : _bleService.ConnectedDevices
                .FirstOrDefault(d => d.Id == DeviceId && d.UsesLiveMeasurementUi);
    }

    private void SubscribeDevice(HNABleDeviceModel device)
    {
        device.MeasurementLogs.CollectionChanged += OnLogsChanged;
    }

    private void UnsubscribeDevice()
    {
        if (_device != null)
        {
            _device.MeasurementLogs.CollectionChanged -= OnLogsChanged;
            _device.PropertyChanged -= OnDevicePropertyChanged;
        }
    }

    private void OnLogsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateMeasurementLogs();
    }

    private void UpdateMeasurementLogs()
    {
        if (Device?.MeasurementLogs != null && !ReferenceEquals(MeasurementLogs, Device.MeasurementLogs))
        {
            MeasurementLogs = Device.MeasurementLogs;
        }

        HasMeasurements = MeasurementLogs.Count > 0;
        TotalLiveCountText = $"Live Logs Count: {MeasurementLogs.Count}";
        UpdateEmptyStateDisplay();
    }

    private void UpdateEmptyStateDisplay()
    {
        ShowEmptyState = !HasMeasurements;

        EmptyStateText = Device == null
            ? "Connect a Halo device and tap 'View Live' again."
            : "Waiting for live readings from the Halo device.";
    }

    private void OnDevicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HNABleDeviceModel.DisplayName) or nameof(HNABleDeviceModel.Subtitle))
        {
            DeviceDisplayName = Device?.DisplayName ?? "Halo Live Readings";
            DeviceSubtitle = Device?.Subtitle ?? "No connected Halo device was found.";
        }

        if (e.PropertyName == nameof(HNABleDeviceModel.BatteryStatus))
        {
            BatteryStatusText = $"Battery: {Device?.BatteryStatus ?? "Unavailable"}";
        }
    }

    private void UpdateDeviceDisplayProperties()
    {
        DeviceDisplayName = Device?.DisplayName ?? "Halo Live Readings";
        DeviceSubtitle = Device?.Subtitle ?? "No connected Halo device was found.";
        BatteryStatusText = $"Battery: {Device?.BatteryStatus ?? "Unavailable"}";

        MeasurementLogs = Device?.MeasurementLogs ?? [];
        UpdateMeasurementLogs();
    }
}
