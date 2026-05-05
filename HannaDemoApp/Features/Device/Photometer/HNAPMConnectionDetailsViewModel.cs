using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HannaDemoApp.Models;
using HannaDemoApp.Services.Ble;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HannaDemoApp.Features.Photometer;

public partial class HNAPMConnectionDetailsViewModel : ObservableObject
{
    private readonly IHNABleService _bleService;
    private bool _isSubscribed;

    [ObservableProperty]
    private string? deviceId;

    [ObservableProperty]
    private string? deviceName;

    [ObservableProperty]
    private string deviceDisplayName = "Photometer Readings";

    [ObservableProperty]
    private HNABleDeviceModel? device;

    public HNAPMConnectionDetailsViewModel(IHNABleService bleService)
    {
        _bleService = bleService;
    }

    public bool HasDevice => Device != null;
    public bool HasDeviceInfo => Device?.HasDeviceInfo == true;
    public bool IsLoadingDeviceInfo => Device?.IsLoadingDeviceInfo == true;
    public bool IsNotLoadingDeviceInfo => !IsLoadingDeviceInfo;
    public string DeviceSubtitle => Device?.Subtitle ?? "No connected photometer was found.";

    public void SetDeviceQuery(string? id, string? name)
    {
        DeviceId = id;
        DeviceName = name;
        LoadSelectedDevice();
    }

    public void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _bleService.ConnectedDevices.CollectionChanged += OnConnectedDevicesChanged;
        _isSubscribed = true;
        LoadSelectedDevice();
    }

    public void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _bleService.ConnectedDevices.CollectionChanged -= OnConnectedDevicesChanged;
        _isSubscribed = false;
        Device = null;
    }

    partial void OnDeviceIdChanged(string? value)
    {
        LoadSelectedDevice();
    }

    partial void OnDeviceNameChanged(string? value)
    {
        UpdateDisplayProperties();
    }

    partial void OnDeviceChanging(HNABleDeviceModel? oldValue, HNABleDeviceModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnDevicePropertyChanged;
            oldValue.DeviceInfo.PropertyChanged -= OnDeviceInfoPropertyChanged;
        }
    }

    partial void OnDeviceChanged(HNABleDeviceModel? value)
    {
        if (value != null)
        {
            value.PropertyChanged += OnDevicePropertyChanged;
            value.DeviceInfo.PropertyChanged += OnDeviceInfoPropertyChanged;
        }

        OnPropertyChanged(nameof(HasDevice));
        UpdateDisplayProperties();
    }

    [RelayCommand]
    private async Task RefreshDeviceInfoAsync()
    {
        if (Device is not { IsConnected: true })
        {
            return;
        }

        await _bleService.SendCommandAsync(Device, "info");
    }

    private void LoadSelectedDevice()
    {
        Device = string.IsNullOrWhiteSpace(DeviceId)
            ? null
            : _bleService.ConnectedDevices.FirstOrDefault(d => d.Id == DeviceId);
    }

    private void OnConnectedDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        LoadSelectedDevice();
    }

    private void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HNABleDeviceModel.DisplayName)
            or nameof(HNABleDeviceModel.Subtitle)
            or nameof(HNABleDeviceModel.HasDeviceInfo)
            or nameof(HNABleDeviceModel.IsLoadingDeviceInfo))
        {
            UpdateDisplayProperties();
        }
    }

    private void OnDeviceInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasDeviceInfo));
    }

    private void UpdateDisplayProperties()
    {
        DeviceDisplayName = Device?.DisplayName
            ?? (string.IsNullOrWhiteSpace(DeviceName) ? "Photometer Readings" : DeviceName);

        OnPropertyChanged(nameof(DeviceSubtitle));
        OnPropertyChanged(nameof(HasDeviceInfo));
        OnPropertyChanged(nameof(IsLoadingDeviceInfo));
        OnPropertyChanged(nameof(IsNotLoadingDeviceInfo));
    }
}
