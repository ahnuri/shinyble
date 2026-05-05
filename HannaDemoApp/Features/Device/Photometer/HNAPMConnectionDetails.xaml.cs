using Microsoft.Maui.Controls;

namespace HannaDemoApp.Features.Photometer;

[QueryProperty(nameof(DeviceId), "deviceId")]
[QueryProperty(nameof(DeviceName), "deviceName")]
public partial class HNAPMConnectionDetails : ContentPage
{
    private readonly HNAPMConnectionDetailsViewModel _viewModel;

    public HNAPMConnectionDetails(HNAPMConnectionDetailsViewModel viewModel)
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
            typeof(HNAPMConnectionDetails),
            null,
            propertyChanged: (bindable, _, newValue) =>
            {
                if (bindable is HNAPMConnectionDetails page)
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
            typeof(HNAPMConnectionDetails),
            null,
            propertyChanged: (bindable, _, newValue) =>
            {
                if (bindable is HNAPMConnectionDetails page)
                {
                    page._viewModel.SetDeviceQuery(page.DeviceId, newValue as string);
                }
            });

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Subscribe();
    }

    protected override void OnDisappearing()
    {
        _viewModel.Unsubscribe();
        base.OnDisappearing();
    }
}
