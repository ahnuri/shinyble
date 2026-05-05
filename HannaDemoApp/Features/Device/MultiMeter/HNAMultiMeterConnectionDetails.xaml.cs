using HannaDemoApp.Core.Constants;
using Microsoft.Maui.Controls;
using System.ComponentModel;

namespace HannaDemoApp.Features.Device.MultiMeter;

[QueryProperty(nameof(DeviceId), "deviceId")]
[QueryProperty(nameof(DeviceName), "deviceName")]
public partial class HNAMultiMeterConnectionDetails : ContentPage
{
    private readonly HNAMultiMeterConnectionDetailsViewModel _viewModel;
    private bool _isNavigatingAway;

    public HNAMultiMeterConnectionDetails(HNAMultiMeterConnectionDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
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
            typeof(HNAMultiMeterConnectionDetails),
            null,
            propertyChanged: (bindable, _, newValue) =>
            {
                if (bindable is HNAMultiMeterConnectionDetails page)
                {
                    page._viewModel.SetDeviceQuery(newValue as string, page.DeviceName);
                }
            });

    public string? DeviceName
    {
        get => (string?)GetValue(DeviceNameProperty);
        set => SetValue(DeviceNameProperty, value);
    }

    public static readonly BindableProperty DeviceNameProperty =
        BindableProperty.Create(
            nameof(DeviceName),
            typeof(string),
            typeof(HNAMultiMeterConnectionDetails),
            null,
            propertyChanged: (bindable, _, newValue) =>
            {
                if (bindable is HNAMultiMeterConnectionDetails page)
                {
                    page._viewModel.SetDeviceQuery(page.DeviceId, newValue as string);
                }
            });

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Subscribe();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Unsubscribe();
        base.OnDisappearing();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HNAMultiMeterConnectionDetailsViewModel.HasDevice))
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
}
