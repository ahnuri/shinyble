# Hanna BLE App Flow

This document explains how this .NET MAUI app works from launch to live BLE measurement updates.

This version is written for a beginner developer, so it focuses on both:

- what the app is doing
- why the code is written that way

## Overview

The runtime flow is:

1. MAUI starts and builds the dependency injection container.
2. `AppShell` becomes the root UI container.
3. `MainPage` is shown as the first screen.
4. The user taps `Connect Device`.
5. `LandingPageViewModel` runs a navigation command.
6. Shell opens `BleDevicesPage`.
7. `BleDevicesPageViewModel` starts BLE scanning through `HNABLEServiceManager`.
8. Matching devices are shown in `AvailableDevices`.
9. The user taps `Connect`.
10. The app connects to the BLE device, loads info, and starts measurement streaming.
11. Incoming BLE responses are added to the UI as live logs.

## Beginner Concepts Used In This App

Before reading the files, these are the key MAUI and C# ideas used throughout the app.

### What `Binding` means

In XAML, `Binding` connects a UI property to a property or command from the page's `BindingContext`.

Example:

```xml
<Button Text="Connect Device"
        Command="{Binding ConnectDeviceCommand}" />
```

This means:

- the button text is `"Connect Device"`
- when the button is tapped, MAUI looks for a property called `ConnectDeviceCommand`
- that property is expected to exist on the current `BindingContext`

So `Binding` is the bridge between XAML and C#.

### What `BindingContext` means

`BindingContext` is the object the page uses as its source for bindings.

Example:

```csharp
public MainPage(LandingPageViewModel viewModel)
{
    InitializeComponent();
    BindingContext = viewModel;
}
```

This means:

- all bindings in `MainPage.xaml` will read from `LandingPageViewModel`
- `{Binding ConnectDeviceCommand}` will look for `ConnectDeviceCommand` inside that view model

### What `Command` means

In MVVM, buttons usually do not call methods directly from code-behind. Instead, they bind to commands.

Example:

```csharp
ConnectDeviceCommand = new Command(async () => await _navigationService.NavigateToBleDevicesAsync());
```

This means:

- the button calls the command
- the command runs the logic in the lambda expression
- the page stays simple, and behavior moves to the view model

### What `Task` means

`Task` is the standard C# return type for asynchronous work.

Example:

```csharp
Task NavigateToBleDevicesAsync();
```

This means:

- the method may take time
- the caller can `await` it
- the UI does not freeze while waiting

Example usage:

```csharp
await _navigationService.NavigateToBleDevicesAsync();
```

`await` means: wait for the async work to finish without blocking the UI thread.

### What `INotifyPropertyChanged` means

When a class implements `INotifyPropertyChanged`, the UI can refresh automatically when property values change.

This is how labels, button text, and other bound values update without manually redrawing the page.

### What `ObservableCollection<T>` means

`ObservableCollection<T>` is used for lists shown in the UI.

This is important because:

- adding an item updates the UI list automatically
- removing an item updates the UI list automatically
- this is why `AvailableDevices`, `ConnectedDevices`, and `MeasurementLogs` can stay in sync with the page

## 1. App Startup

The app starts in `MauiProgram.cs`. This is where services, pages, and view models are registered for dependency injection.

```csharp
builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
#if ANDROID
builder.Services.AddSingleton<IBackgroundService, AndroidBackgroundService>();
#else
builder.Services.AddSingleton<IBackgroundService, NoOpBackgroundService>();
#endif
builder.Services.AddSingleton<HNABLEServiceManager>();
builder.Services.AddTransient<LandingPageViewModel>();
builder.Services.AddTransient<BleDevicesPageViewModel>();
builder.Services.AddTransient<MainPage>();
builder.Services.AddTransient<BleDevicesPage>();
builder.Services.AddSingleton<AppShell>();
```

Why this matters:

- `AddSingleton` means one shared instance for the life of the app.
- `AddTransient` means create a new instance when needed.
- `HNABLEServiceManager` is a singleton because BLE state should be shared.
- pages and view models are transient because they are UI objects.

File:

- [MauiProgram.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HannaDemoApp/MauiProgram.cs)

`App.xaml.cs` creates the main window and uses `AppShell` as the root UI container:

```csharp
protected override Window CreateWindow(IActivationState? activationState)
{
    return new Window(_appShell);
}
```

File:

- [App.xaml.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HannaDemoApp/App.xaml.cs)

## 2. Shell And Navigation

`AppShell` defines the main routes in the app.

```xml
<ShellContent
    Title="Measure"
    ContentTemplate="{DataTemplate local:MainPage}"
    Route="Measure" />

<ShellContent
    Title="BLE Devices"
    ContentTemplate="{DataTemplate local:BleDevicesPage}"
    Route="ble" />
```

What this means:

- `MainPage` is one Shell page
- `BleDevicesPage` is another Shell page
- `"ble"` is the route name used for navigation

File:

- [AppShell.xaml](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HannaDemoApp/AppShell.xaml)

Navigation is wrapped in `INavigationService`:

```csharp
public interface INavigationService
{
    Task NavigateToBleDevicesAsync();
}
```

Why use an interface:

- the view model should not know Shell details
- navigation logic can be changed later without changing the view model
- it makes testing easier

The real implementation is:

```csharp
public Task NavigateToBleDevicesAsync()
{
    return MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//ble"));
}
```

Beginner explanation of this method:

- `public` means other classes can call it
- `Task` means it is asynchronous
- `NavigateToBleDevicesAsync` is just a descriptive method name
- `Shell.Current.GoToAsync("//ble")` tells MAUI to navigate to the Shell route named `ble`
- `MainThread.InvokeOnMainThreadAsync(...)` makes sure the navigation happens on the UI thread

Files:

- [INavigationService.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAServices/INavigationService.cs)
- [ShellNavigationService.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAServices/ShellNavigationService.cs)

## 3. Landing Page

The landing page is simple. It only contains one main button:

```xml
<Button Text="Connect Device"
        Command="{Binding ConnectDeviceCommand}"
        BackgroundColor="Blue"
        TextColor="White" />
```

How to read this:

- `Text` controls what the user sees
- `Command="{Binding ConnectDeviceCommand}"` means the button executes a command from the view model
- no click handler is needed in code-behind

Files:

- [MainPage.xaml](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HannaDemoApp/MainPage.xaml)
- [MainPage.xaml.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HannaDemoApp/MainPage.xaml.cs)

The code-behind only sets the `BindingContext`:

```csharp
public MainPage(LandingPageViewModel viewModel)
{
    InitializeComponent();
    BindingContext = viewModel;
}
```

This is common in MVVM:

- XAML defines layout
- the view model defines state and actions
- the page connects the two with `BindingContext`

`LandingPageViewModel` creates the command:

```csharp
public LandingPageViewModel(INavigationService navigationService)
{
    _navigationService = navigationService;
    ConnectDeviceCommand = new Command(async () => await _navigationService.NavigateToBleDevicesAsync());
}
```

Why this is useful:

- the view model is responsible for user intent
- the navigation service is responsible for navigation implementation
- the page remains very small and easy to understand

File:

- [LandingPageViewModel.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAViewModels/LandingPageViewModel.cs)

## 4. BLE Devices Page

When the app navigates to the BLE page, the page binds to `BleDevicesPageViewModel`.

Key UI bindings:

```xml
<ToolbarItem Text="{Binding ScanButtonText}"
             Command="{Binding ToggleScanCommand}" />

<CollectionView ItemsSource="{Binding ConnectedDevices}" />
<CollectionView ItemsSource="{Binding AvailableDevices}" />
```

How to read these:

- `ScanButtonText` supplies the label for the toolbar action
- `ToggleScanCommand` runs when the toolbar item is tapped
- `ItemsSource` tells the `CollectionView` which list to display

Because those lists are `ObservableCollection<T>`, the screen updates automatically when items are added or removed.

The page code-behind stays small:

```csharp
public BleDevicesPage(BleDevicesPageViewModel viewModel)
{
    InitializeComponent();
    BindingContext = viewModel;
}
```

Files:

- [BleDevicesPage.xaml](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAPages/BleDevicesPage.xaml)
- [BleDevicesPage.xaml.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAPages/BleDevicesPage.xaml.cs)

## 5. BLE Page View Model

`BleDevicesPageViewModel` coordinates what the page can do.

It exposes:

- lists for available and connected devices
- commands for scan, connect, and disconnect
- UI-friendly properties like `StatusMessage` and `ScanButtonText`

Constructor:

```csharp
public BleDevicesPageViewModel(HNABLEServiceManager bleServiceManager)
{
    _bleServiceManager = bleServiceManager;
    AvailableDevices = bleServiceManager.AvailableDevices;
    ConnectedDevices = bleServiceManager.ConnectedDevices;

    ToggleScanCommand = new Command(async () => await ToggleScanAsync());
    ConnectDeviceCommand = new Command<BleDeviceItem>(async device => await ConnectDeviceAsync(device));
    DisconnectDeviceCommand = new Command<BleDeviceItem>(async device => await DisconnectDeviceAsync(device));
}
```

Why this is good design:

- the page does not know Bluetooth details
- the view model does not talk to Android APIs directly
- BLE work is delegated to `HNABLEServiceManager`

The scan button eventually runs:

```csharp
private async Task ToggleScanAsync()
{
    if (_bleServiceManager.IsScanning)
    {
        _bleServiceManager.StopScan();
        return;
    }

    await _bleServiceManager.ScanAsync();
}
```

Beginner explanation:

- `private` means only this class uses the method
- `async` means the method can use `await`
- `Task` means it completes asynchronously
- if scanning is already running, it stops
- otherwise it starts scanning

File:

- [BleDevicesPageViewModel.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAViewModels/BleDevicesPageViewModel.cs)

## 6. The Main BLE Service

`HNABLEServiceManager` is the core BLE class in this app.

It is responsible for:

- scanning
- filtering supported products
- handling connect and disconnect
- discovering characteristics
- requesting device info
- starting live measurement streaming
- processing incoming BLE messages

If you want to understand the real behavior of the app, this is the most important file.

File:

- [HNABLEServiceManager.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAServices/HNABLEServiceManager.cs)

## 7. Product Matching

Supported products are defined using an `enum`:

```csharp
public enum ProductId
{
    HI9810,
    HI98494,
    HI97105,
    HI98594
}
```

Why use an `enum`:

- it gives a fixed set of valid values
- it is easier to read than raw strings everywhere
- the compiler can help catch mistakes

Advertisement matching values are stored in a dictionary:

```csharp
public static class ProductMatching
{
    public static readonly Dictionary<ProductId, string[]> ProductHex = new()
    {
        { ProductId.HI9810, ["8f070100"] },
        { ProductId.HI98494, ["6e400001b5a3f393e0a9e50e24dcca9e"] },
        { ProductId.HI97105, ["8f070200"] },
        { ProductId.HI98594, ["8f070400"] }
    };
}
```

Why use a dictionary here:

- each product can have one or more advertisement signatures
- the scan logic can look up those values by product id
- product data stays separate from the scan algorithm

## 8. Scan Flow

The main scan method is:

```csharp
public async Task ScanAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
{
    if (_bluetoothLE.State != BluetoothState.On)
    {
        StatusText = $"Bluetooth is {_bluetoothLE.State}. Turn it on to scan.";
        return;
    }

    if (!await EnsurePermissionsAsync())
    {
        StatusText = "Required Bluetooth permission is not granted.";
        return;
    }

    await MainThread.InvokeOnMainThreadAsync(() => AvailableDevices.Clear());

    IsScanning = true;
    StatusText = "Scanning for Hanna BLE devices...";

    await _adapter.StartScanningForDevicesAsync(
        deviceFilter: device => IsDeviceMatchingProduct(device, _currentProductId, out _),
        cancellationToken: cancellationToken);
}
```

Step-by-step explanation:

1. Check whether Bluetooth is turned on.
2. Request required permissions.
3. Clear old scan results.
4. Set `IsScanning = true` so the UI can change button text.
5. Update `StatusText` so the page can show the current state.
6. Start Plugin.BLE scanning with a filter.

Important note for this app:

- `ScanAsync()` currently filters by `_currentProductId`
- `_currentProductId` defaults to `HI9810`
- `ScanForProductAsync(productId)` is the method that changes the selected product

## 9. How Device Matching Works

The app checks whether a scanned BLE device matches the selected product:

```csharp
private static bool IsDeviceMatchingProduct(IDevice device, ProductId productId, out string advertisementHex)
{
    advertisementHex = TryGetAdvertisementHex(device, productId);
    return !string.IsNullOrEmpty(advertisementHex);
}
```

Why the `out` parameter is used:

- the method returns `true` or `false` to say whether the device matches
- it also returns the actual matched advertisement string through `advertisementHex`

The low-level comparison happens here:

```csharp
private static string TryGetAdvertisementHex(IDevice device, ProductId productId)
{
    if (!ProductMatching.ProductHex.TryGetValue(productId, out var productHexValues) ||
        device.AdvertisementRecords == null)
    {
        return string.Empty;
    }

    foreach (var record in device.AdvertisementRecords)
    {
        var payloadHex = BitConverter.ToString(record.Data).Replace("-", "").ToLowerInvariant();

        if (record.Type == AdvertisementRecordType.ManufacturerSpecificData &&
            productHexValues.Any(target => payloadHex.Contains(target, StringComparison.OrdinalIgnoreCase)))
        {
            return payloadHex;
        }
    }

    return string.Empty;
}
```

Meaning:

- each BLE advertisement record is read
- the bytes are converted to a hex string
- the app checks whether the expected product fragment exists in the advertisement payload

## 10. Adding Devices To The UI

When Plugin.BLE discovers a device, `OnDeviceDiscovered` runs:

```csharp
private void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
{
    if (e.Device == null || !IsDeviceMatchingProduct(e.Device, _currentProductId, out var advertisementHex))
    {
        return;
    }

    MainThread.BeginInvokeOnMainThread(() =>
    {
        if (ConnectedDevices.Any(existing => existing.Id == e.Device.Id.ToString()))
        {
            return;
        }

        var item = GetOrCreateDeviceItem(e.Device, advertisementHex);
        if (AvailableDevices.Any(existing => existing.Id == item.Id))
        {
            UpdateItem(item, e.Device, advertisementHex);
            return;
        }

        AvailableDevices.Add(item);
    });
}
```

Why this uses `MainThread.BeginInvokeOnMainThread(...)`:

- BLE callbacks may happen on a background thread
- MAUI UI-bound collections should be updated on the main thread
- this keeps collection binding safe

## 11. What `BleDeviceItem` Is For

`BleDeviceItem` is the UI-friendly model for one device.

```csharp
public class BleDeviceItem : INotifyPropertyChanged
{
    public string Id { get; }
    public IDevice? Device { get; set; }
    public string Name { get; set; }
    public int SignalStrength { get; set; }
    public string AdvertisementHex { get; set; }
    public bool IsConnected { get; set; }
    public ObservableCollection<MeasurementLogEntry> MeasurementLogs { get; } = [];
}
```

Why this class exists:

- Plugin.BLE device objects are low-level
- the page needs display-friendly properties
- this model stores UI state such as name, RSSI, connection state, latest values, and measurement logs

File:

- [BleDeviceItem.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAModels/BleDeviceItem.cs)

## 12. Connection Flow

When the user taps a device's `Connect` button, the view model calls:

```csharp
await _bleServiceManager.ConnectAsync(device);
```

Inside the service:

```csharp
public async Task ConnectAsync(BleDeviceItem device, CancellationToken cancellationToken = default)
{
    if (device.Device == null)
    {
        StatusText = "Device reference is unavailable. Scan again.";
        return;
    }

    await _adapter.ConnectToDeviceAsync(device.Device, cancellationToken: cancellationToken);
    StatusText = $"Connected to {device.Name}.";
}
```

How to read this:

- `device` is the selected UI item
- `device.Device` is the real Plugin.BLE device reference
- `ConnectToDeviceAsync(...)` starts the BLE connection
- `StatusText` is updated so the UI can show feedback

## 13. Why Connected State Is Handled In Events

After the adapter finishes connecting, Plugin.BLE raises `DeviceConnected`.

The manager responds here:

```csharp
private void OnDeviceConnected(object? sender, DeviceEventArgs e)
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        var advertisementHex = TryGetAdvertisementHex(e.Device, _currentProductId);
        var connectedItem = GetOrCreateDeviceItem(e.Device, advertisementHex);

        if (!ConnectedDevices.Any(existing => existing.Id == connectedItem.Id))
        {
            ConnectedDevices.Add(connectedItem);
        }

        connectedItem.IsConnected = true;
        RemoveFromAvailableDevices(connectedItem.Id);

        _ = InitializeConnectedDeviceAsync(connectedItem);
        _backgroundService?.StartService();
    });
}
```

Why this is done in an event:

- `ConnectAsync(...)` starts the request
- the real success signal comes later from the BLE adapter event
- this event is the correct place to update connected UI state

## 14. Background Service Abstraction

The app uses an interface so shared BLE code does not depend directly on Android classes.

```csharp
public interface IBackgroundService
{
    void StartService();
    void StopService();
}
```

On Android, the app starts a foreground service:

```csharp
var intent = new Intent(activity, typeof(BleForegroundService));
activity.StartForegroundService(intent);
```

The foreground service displays a persistent notification:

```csharp
var notification = new NotificationCompat.Builder(this, CHANNEL_ID)
    .SetContentTitle("Hanna Meter Connected")
    .SetContentText("Maintaining connection for live measurements.")
    .SetOngoing(true)
    .Build()!;
```

On non-Android platforms, `NoOpBackgroundService` does nothing.

Why this pattern is good:

- shared code stays cross-platform
- Android-specific logic stays inside Android files
- other platforms can safely ignore this behavior

Files:

- [IBackgroundService.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAServices/IBackgroundService.cs)
- [BackgroundService.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HannaDemoApp/Platforms/Android/BackgroundService.cs)
- [BleForegroundService.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HannaDemoApp/Platforms/Android/BleForegroundService.cs)
- [NoOpBackgroundService.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAServices/NoOpBackgroundService.cs)

## 15. Connected Device Initialization

After connection, the app still needs to set up communication with the device.

That is done here:

```csharp
private async Task InitializeConnectedDeviceAsync(BleDeviceItem deviceItem, CancellationToken cancellationToken = default)
{
    var session = await EnsureDeviceSessionAsync(deviceItem, cancellationToken);
    var response = await RequestDeviceInfoAsync(session, cancellationToken);

    if (!string.IsNullOrWhiteSpace(response))
    {
        await MainThread.InvokeOnMainThreadAsync(() => ApplyDeviceInfo(deviceItem, response));
    }

    await StartMeasurementStreamAsync(session, cancellationToken);
}
```

Why this extra step exists:

- connecting to BLE only opens the transport
- the app still needs to find services and characteristics
- then it must send commands the device understands
- only then can live data start streaming

## 16. Characteristic Discovery

The service tries to find the UART service first:

```csharp
var services = await device.GetServicesAsync(cancellationToken);
var uartService = services.FirstOrDefault(service => service.Id == NordicUartServiceUuid);
```

If needed, it falls back to the first write and notify/read characteristics it can find:

```csharp
var writeCharacteristic = characteristics.FirstOrDefault(characteristic => characteristic.CanWrite);
var notifyCharacteristic = characteristics.FirstOrDefault(characteristic => characteristic.CanUpdate || characteristic.CanRead);
```

Why this matters:

- BLE communication happens through characteristics
- the app needs one characteristic to send commands
- and another one to receive responses

## 17. Device Info Request

The app sends the `"info"` command:

```csharp
await SendCommandAsync(writeCharacteristic, "info", cancellationToken);
```

Responses beginning with `I,` are treated as device info:

```csharp
if (response.StartsWith("I,", StringComparison.OrdinalIgnoreCase))
{
    session.PendingInfoResponse?.TrySetResult(response);
    return;
}
```

The response is parsed into display properties:

```csharp
deviceItem.MeterModel = values.Length > 1 ? values[1] : string.Empty;
deviceItem.UserSetName = values.Length > 2 ? values[2] : string.Empty;
deviceItem.MeterFirmwareVersion = FindValueAfterLabel(values, "FW");
deviceItem.BleFirmwareVersion = FindValueAfterLabel(values, "nRF FW");
deviceItem.SerialNumber = FindValueAfterLabel(values, "SN");
```

Meaning:

- the device sends text data
- this code splits the string and maps parts of it into UI properties
- the page can then bind to those properties and display them

## 18. Live Measurement Stream

Once setup is complete, the app sends:

```csharp
await SendCommandAsync(writeCharacteristic, "set meas on", cancellationToken);
```

The app subscribes to notifications:

```csharp
notifyCharacteristic.ValueUpdated += Handler;
await notifyCharacteristic.StartUpdatesAsync(cancellationToken);
```

Incoming bytes are decoded:

```csharp
private static string DecodeResponse(byte[]? bytes)
{
    return Encoding.UTF8.GetString(bytes).Trim('\0', '\r', '\n', ' ');
}
```

Measurement responses begin with `M,`:

```csharp
if (!response.StartsWith("M,", StringComparison.OrdinalIgnoreCase))
{
    return;
}

deviceItem.QueueMeasurementLog(timestamp, response);
MainThread.BeginInvokeOnMainThread(() => deviceItem.FlushPendingMeasurementLogs());
```

Why the UI updates automatically:

- `MeasurementLogs` is an `ObservableCollection`
- the `CollectionView` in XAML binds to that collection
- adding log entries causes the UI to refresh

## 19. Disconnect Flow

When the user taps `Disconnect`, the view model calls:

```csharp
await _bleServiceManager.DisconnectAsync(device);
```

Later, the BLE adapter raises `DeviceDisconnected`, and the manager:

- removes the device from `ConnectedDevices`
- marks it disconnected
- stops the background service if no devices remain
- removes the active BLE session

Key code:

```csharp
if (ConnectedDevices.Count == 0)
{
    _backgroundService?.StopService();
}

_ = StopDeviceSessionAsync(e.Device.Id.ToString());
```

## 20. Architecture Summary

Each layer has a separate role:

- `MauiProgram` registers app services and UI types
- `AppShell` defines routes
- `MainPage` is the landing UI
- `LandingPageViewModel` handles the first user action
- `INavigationService` hides navigation implementation
- `BleDevicesPage` displays BLE-related UI
- `BleDevicesPageViewModel` coordinates screen actions and state
- `HNABLEServiceManager` contains BLE logic
- `BleDeviceItem` stores UI-friendly device state
- `IBackgroundService` hides platform-specific background behavior

## 21. Best Reading Order For A Beginner

If you want to learn this app quickly, read files in this order:

1. [MainPage.xaml](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HannaDemoApp/MainPage.xaml)
2. [LandingPageViewModel.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAViewModels/LandingPageViewModel.cs)
3. [AppShell.xaml](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HannaDemoApp/AppShell.xaml)
4. [ShellNavigationService.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAServices/ShellNavigationService.cs)
5. [BleDevicesPage.xaml](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAPages/BleDevicesPage.xaml)
6. [BleDevicesPageViewModel.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAViewModels/BleDevicesPageViewModel.cs)
7. [HNABLEServiceManager.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAServices/HNABLEServiceManager.cs)
8. [BleDeviceItem.cs](/Users/praburajendran/Desktop/My%20Workspace/hybridapp/HannaDemoApp/HNASourceCode/HNAModels/BleDeviceItem.cs)

## 22. Practical Notes

- `Binding` connects XAML to view model properties and commands.
- `BindingContext` decides where those bindings read from.
- `Command` is how buttons trigger logic in MVVM.
- `Task` and `await` are used so the UI stays responsive during navigation, scan, connect, and BLE communication.
- `INotifyPropertyChanged` keeps labels and state in sync with code changes.
- `ObservableCollection<T>` keeps list UI in sync with added or removed items.
- most page code-behind is intentionally small because the app follows an MVVM-style structure.

