using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HannaDemoApp.Models;
using HannaDemoApp.Services.Ble;
using HannaDemoApp.Services.Dialog;
using HannaDemoApp.Services.Ota;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HannaDemoApp.Features.Photometer;

public partial class HNAPMConnectionDetailsViewModel : ObservableObject
{
    private readonly IHNABleService _bleService;
    private readonly HNAPhotometerOtaService _photometerOtaService;
    private readonly IHNADialogService _dialogService;
    private bool _isSubscribed;

    [ObservableProperty]
    private string? deviceId;

    [ObservableProperty]
    private string? deviceName;

    [ObservableProperty]
    private string deviceDisplayName = "Photometer Readings";

    [ObservableProperty]
    private HNABleDeviceModel? device;

    [ObservableProperty]
    private bool _isOtaInProgress;

    [ObservableProperty]
    private double _otaProgress;

    [ObservableProperty]
    private string _otaProgressTitle = string.Empty;

    public HNAPMConnectionDetailsViewModel(
        IHNABleService bleService,
        HNAPhotometerOtaService photometerOtaService,
        IHNADialogService dialogService)
    {
        _bleService = bleService;
        _photometerOtaService = photometerOtaService;
        _dialogService = dialogService;
    }

    public bool HasDevice => Device != null;
    public bool HasDeviceInfo => Device?.HasDeviceInfo == true;
    public bool IsLoadingDeviceInfo => Device?.IsLoadingDeviceInfo == true;
    public bool IsNotLoadingDeviceInfo => !IsLoadingDeviceInfo;
    public bool CanTapFirmwareButton => IsNotLoadingDeviceInfo && !IsOtaInProgress;
    public string DeviceSubtitle => Device?.Subtitle ?? "No connected photometer was found.";

    partial void OnIsOtaInProgressChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTapFirmwareButton));
    }

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
    private async Task UpdateDeviceFirmwareAsync()
    {
        if (Device is not { IsConnected: true })
        {
            await _dialogService.ShowAlertAsync("Firmware", "Connect the photometer first.", "OK");
            return;
        }

        var choice = await _dialogService.ShowActionSheetAsync(
            "Firmware update",
            "Cancel",
            null,
            "Download from server",
            "Choose file (ZIP / HEX / LNG)");

        if (string.IsNullOrEmpty(choice) || choice == "Cancel")
        {
            return;
        }

        var uiProgress = new Progress<HNAOtaUiProgress>(p =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OtaProgressTitle = p.Title;
                OtaProgress = p.PercentComplete / 100.0;
            });
        });

        if (choice == "Download from server")
        {
            IsOtaInProgress = true;
            OtaProgress = 0;
            OtaProgressTitle = "Preparing…";
            try
            {
                var otaResult = await _photometerOtaService.RunFromCloudAsync(Device, uiProgress);
                await ShowOtaResultAsync(otaResult);
            }
            finally
            {
                IsOtaInProgress = false;
                OtaProgress = 0;
                OtaProgressTitle = string.Empty;
            }

            return;
        }

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select firmware package",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.iOS] = ["public.data", "public.item"],
                [DevicePlatform.Android] =
                [
                    "application/zip",
                    "application/octet-stream",
                    "*/*"
                ],
                [DevicePlatform.WinUI] = [".zip", ".hex", ".lng"],
                [DevicePlatform.MacCatalyst] = ["public.data", "public.item"]
            })
        });

        if (result?.FullPath is not { } path)
        {
            return;
        }

        IsOtaInProgress = true;
        OtaProgress = 0;
        OtaProgressTitle = "Preparing…";
        try
        {
            var otaResult = await _photometerOtaService.RunFromLocalPathAsync(Device, path, uiProgress);
            await ShowOtaResultAsync(otaResult);
        }
        finally
        {
            IsOtaInProgress = false;
            OtaProgress = 0;
            OtaProgressTitle = string.Empty;
        }
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
        OnPropertyChanged(nameof(CanTapFirmwareButton));
    }

    private Task ShowOtaResultAsync(HNAOtaResult result)
    {
        if (result.IsSuccess)
        {
            return _dialogService.ShowAlertAsync("Firmware", result.Message, "OK");
        }

        var title = result.ErrorCode switch
        {
            HNAOtaErrorCode.OtaTimeout => "Firmware Timeout",
            HNAOtaErrorCode.UnsupportedDevice => "Unsupported Device",
            HNAOtaErrorCode.PackageInvalid => "Invalid Package",
            _ => "Firmware"
        };

        return _dialogService.ShowAlertAsync(title, $"Update failed: {result.Message}", "OK");
    }
}
