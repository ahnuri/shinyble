using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using HannaDemoApp.Core.Constants;
using HannaDemoApp.Core.DeviceHandlers;
using HannaDemoApp.Core.Enums;
using HannaDemoApp.Models;
using HannaDemoApp.Services.Background;
using HannaDemoApp.Services.Database;
using HannaDemoApp.Services.Dialog;
using Shiny;
using Shiny.BluetoothLE;

namespace HannaDemoApp.Services.Ble;

// ======= HNABleService — main BLE orchestrator for all Hanna meter communication. ==================
//
// This class uses Shiny.BluetoothLE (IBleManager / IPeripheral) instead of Plugin.BLE.
// The overall flow is the same as before — only the BLE plumbing changes.
//
//   1. SCAN         User taps scan. We request access, subscribe to bleManager.Scan(),
//                   and filter each result through TryMatchScanResult() to keep only
//                   Hanna devices.  Disposing the subscription stops the scan.
//
//   2. DISCOVER     Each matched peripheral goes through OnScanResult() and gets
//                   added to AvailableDevices for the UI.
//
//   3. CONNECT      User picks a device, we call peripheral.ConnectAsync().
//                   On success we move it to ConnectedDevices, subscribe to
//                   WhenDisconnected() for cleanup, and start the init pipeline.
//
//   4. SETUP        InitializeConnectedDeviceAsync() still runs the same three steps:
//                   a) discover GATT write + notify characteristics
//                   b) send "info" and wait for the "I,..." device-info response
//                   c) start live measurements only for products that support it
//
//   5. STREAMING    Notifications come in via peripheral.NotifyCharacteristic() observable.
//                   The subscription handler calls HandleIncomingResponse() → product handler
//                   → QueueMeasurementLog → FlushPendingMeasurementLogs → UI.
//
//   6. DISCONNECT   The WhenDisconnected() subscription or the connection monitor
//                   triggers cleanup, session teardown, and foreground service stop.
//
//   7. RESUME       ResumeLiveUpdatesAsync() flushes pending logs and re-sends
//                   "set meas on" only to products that use the live stream.
// =====================================================================================



public partial class HNABleService : ObservableObject, IHNABleService
{
    private readonly IBleManager _bleManager; // Shiny core entry point like iOS CBCentralManager, BLE controller
    private readonly IHNABackgroundService? _backgroundService;
    private readonly IHNADialogService _dialogService;
    private readonly IHNALogRepository _logRepository;
    private readonly HNADeviceHandlerRegistry _handlerRegistry;
    private readonly IHNAProductMatchingService _productMatching;

    // Device session state management. We keep track of active sessions, known devices, and disconnection subscriptions in concurrent dictionaries keyed by device ID (peripheral UUID) for thread-safe access across async operations and event handlers.
    private readonly ConcurrentDictionary<string, DeviceSession> _deviceSessions = new();
    private readonly ConcurrentDictionary<string, HNABleDeviceModel> _knownDevices = new();
    private readonly ConcurrentDictionary<string, IDisposable> _disconnectSubscriptions = new();
    private readonly SemaphoreSlim _resumeLock = new(1, 1);
    private HNAProductId _scanTargetProductId = HNAProductId.HI9810;

    // Controls scan lifecycle (start/stop/timeout)
    private CancellationTokenSource? _monitorCts;
    private CancellationTokenSource? _scanCts;

    public HNABleService(
        IBleManager bleManager,
        IHNALogRepository logRepository,
        HNADeviceHandlerRegistry handlerRegistry,
        IHNAProductMatchingService productMatching,
        IHNADialogService dialogService,
        IHNABackgroundService? backgroundService = null)
    {
        _bleManager = bleManager;
        _dialogService = dialogService;
        _logRepository = logRepository;
        _handlerRegistry = handlerRegistry;
        _productMatching = productMatching;
        _backgroundService = backgroundService;
    }

    public ObservableCollection<HNABleDeviceModel> AvailableDevices { get; } = [];
    public ObservableCollection<HNABleDeviceModel> ConnectedDevices { get; } = [];

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Ready to scan for nearby BLE devices.";

    // Starts a scan targeting a specific product. We stash the product ID so that if
    // advertisement matching fails during connect, we still know which handler to use.
    public async Task ScanForProductAsync(HNAProductId productId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        _scanTargetProductId = productId;
        await ScanAsync(timeout, cancellationToken);
    }

    #region Private - Observable BLE scan stream
    // Main scan entry point. Requests BT access/permissions, clears old results, then
    // subscribes to bleManager.Scan(). Each result passes through TryMatchScanResult()
    // to keep only Hanna devices. The subscription is alive for the timeout duration;
    // disposing it (via the using block or _scanCts cancellation) stops the scan.
    public async Task ScanAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        // Shiny's RequestAccessAsync handles both permission requests and checking if Bluetooth is enabled, abstracting platform differences. It returns an AccessState indicating the result.
        var access = await _bleManager.RequestAccessAsync();
        if (access != AccessState.Available)
        {
            StatusText = $"Bluetooth is not available ({access}). Enable Bluetooth to scan.";
            return;
        }

        if (_bleManager.IsScanning)
            _bleManager.StopScan();

        await MainThread.InvokeOnMainThreadAsync(() => AvailableDevices.Clear());

        _scanCts?.Dispose();
        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _scanCts.CancelAfter(timeout ?? TimeSpan.FromMilliseconds(HNAAppConstants.DefaultScanTimeoutMs));

        IsScanning = true;
        StatusText = "Scanning for Hanna BLE devices...";

        try
        {
            using var sub = _bleManager
                .Scan()

                // Shiny's Scan() returns an IObservable<ScanResult> that emits a new ScanResult for every BLE advertisement received. We subscribe to this stream and process each result in OnScanResult. If an error occurs during scanning, we update the status text and cancel the scan.
                .Subscribe(
                    onNext: OnScanResult,
                    onError: ex => { StatusText = $"Scan error: {ex.Message}"; _scanCts?.Cancel(); });

            await Task.Delay(Timeout.InfiniteTimeSpan, _scanCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout elapsed or StopScan() called — normal completion
            StatusText = ConnectedDevices.Count > 0
                ? $"{ConnectedDevices.Count} device(s) connected."
                : AvailableDevices.Count > 0
                    ? $"{AvailableDevices.Count} matching device(s) found."
                    : "No matching Hanna devices found.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan canceled.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    #endregion



    // Stops the current scan immediately by canceling the scan CTS, which causes the
    // Task.Delay in ScanAsync to throw, which disposes the scan subscription.
    public void StopScan()
    {
        _scanCts?.Cancel();
        IsScanning = false;
        StatusText = "Scan stopped.";
    }


    #region  Public Connection Methods
    // Connects to the peripheral. Stops any active scan first, then awaits the connection.
    // On success: moves the device to ConnectedDevices, subscribes to WhenDisconnected()
    // for automatic cleanup, and fires off the init pipeline (GATT → info → optional live stream).
    public async Task ConnectAsync(HNABleDeviceModel device, CancellationToken cancellationToken = default)
    {
        if (device.Device == null)
        {
            StatusText = "Device reference is unavailable. Scan again.";
            return;
        }

        var peripheral = device.Device;

        try
        {
            _scanCts?.Cancel();
            IsScanning = false;

            await peripheral.ConnectAsync(cancelToken: cancellationToken);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ConnectedDevices.Any(d => d.Id == device.Id))
                    ConnectedDevices.Add(device);

                device.IsConnected = true;
                RemoveFromAvailableDevices(device.Id);
            });

            StatusText = $"Connected to {device.Name}.";

            // Subscribe to disconnection so cleanup always runs regardless of how we
            // lose the connection (user-initiated, link loss, background kill, etc.).
            var disconnectSub = peripheral.WhenDisconnected().Subscribe(_ =>
            {
                var deviceId = peripheral.Uuid;
                MainThread.BeginInvokeOnMainThread(() => CleanUpDisconnectedDevice(deviceId));
                StopDeviceSession(deviceId);
            });
            _disconnectSubscriptions[device.Id] = disconnectSub;

            // Initialize device with proper error handling (initialization errors are logged)
            InitializeConnectedDeviceAsync(device, cancellationToken)
                .SafeFireAndForget("Device initialization after connection");
            _backgroundService?.StartService();
            StartConnectionMonitor();
        }
        catch (Exception ex)
        {
            await HandleConnectionFailureAsync(device, ex);
        }
    }

    #endregion

    #region Public - Disconnect

    // User-initiated disconnect. Calls CancelConnection() — the WhenDisconnected()
    // subscription handles all cleanup when the peripheral actually disconnects.
    public Task DisconnectAsync(HNABleDeviceModel device, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (device.Device == null)
            return Task.CompletedTask;

        try
        {
            device.Device.CancelConnection();
            StatusText = $"Disconnected from {device.Name}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to disconnect from {device.Name}: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    #endregion

    #region Public - Send Command

    /// <summary>
    /// Sends a raw text command to a connected device over the Nordic UART write characteristic.
    /// Used for device-specific actions like meter queries or configuration commands.
    /// </summary>
    /// <param name="device">The target device (must be connected).</param>
    /// <param name="command">The command string to send (must not be null or empty).</param>
    /// <exception cref="ArgumentNullException">Thrown if device or command is null.</exception>
    /// <exception cref="ArgumentException">Thrown if command is empty or only whitespace.</exception>
    public async Task SendCommandAsync(HNABleDeviceModel device, string command)
    {
        ArgumentNullException.ThrowIfNull(device, nameof(device));
        ArgumentException.ThrowIfNullOrWhiteSpace(command, nameof(command));

        if (!device.IsConnected || device.Device == null || !_deviceSessions.TryGetValue(device.Id, out var session))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CommandError] Cannot send command to {device.Name}: device not properly connected");
            return;
        }

        await SendCommandAsync(session, command, CancellationToken.None);
    }

    #endregion

    #region Resume Handling

    // Called when the app comes back to the foreground (e.g., from Android's OnResume).
    // Flushes any measurements that piled up while we were in the background, then
    // re-sends "set meas on" to live-stream devices. If a session got dropped while
    // we were away, it re-runs the full setup pipeline from scratch.
    public async Task ResumeLiveUpdatesAsync(CancellationToken cancellationToken = default)
    {
        await _resumeLock.WaitAsync(cancellationToken);
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var connectedDevice in ConnectedDevices)
                {
                    connectedDevice.FlushPendingMeasurementLogs();
                    CheckAndSaveMeasurementBatchIfLimitReached(connectedDevice);
                }
            });

            foreach (var connectedDevice in ConnectedDevices.ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (connectedDevice.Device is not IPeripheral peripheral ||
                    peripheral.Status != ConnectionState.Connected)
                {
                    continue;
                }

                if (!_deviceSessions.TryGetValue(connectedDevice.Id, out var existingSession))
                {
                    InitializeConnectedDeviceAsync(connectedDevice, cancellationToken)
                        .SafeFireAndForget("Device re-initialization on resume");
                    continue;
                }

                if (ShouldAutoStartMeasurementStream(connectedDevice.ProductId))
                {
                    await RestartMeasurementStreamAsync(existingSession, cancellationToken);
                }
            }
        }
        finally
        {
            _resumeLock.Release();
        }
    }

    #endregion

    public void Dispose()
    {
        StopConnectionMonitor();

        _scanCts?.Cancel();
        _scanCts?.Dispose();

        foreach (var session in _deviceSessions.Values.ToArray())
            session.NotifySubscription.Dispose();

        foreach (var sub in _disconnectSubscriptions.Values.ToArray())
            sub.Dispose();

        _deviceSessions.Clear();
        _disconnectSubscriptions.Clear();
        _resumeLock.Dispose();
    }

    #region Private - Connection Monitor

    // Starts a background loop that polls every 3 seconds to check whether each
    // connected device is still actually connected at the OS level. This is the
    // fallback for cases where DeviceConnectionLost doesn't fire promptly —
    // which happens on both iOS (CoreBluetooth timeout) and Android (variable).
    private void StartConnectionMonitor()
    {
        if (_monitorCts != null) return; // already running

        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                MainThread.BeginInvokeOnMainThread(CheckConnectionStates);
            }
        }, CancellationToken.None);
    }

    private void StopConnectionMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;
    }

    // Checks every device in ConnectedDevices against its actual Shiny BLE state.
    // If the OS reports it is no longer connected, we run the same cleanup used by
    // the disconnect subscription so the UI updates immediately. Must run on the main thread.
    private void CheckConnectionStates()
    {
        foreach (var device in ConnectedDevices.ToList())
        {
            if (device.Device is IPeripheral p && p.Status == ConnectionState.Connected) continue;

            StatusText = $"{device.Name} disconnected.";
            CleanUpDisconnectedDevice(device.Id);
            StopDeviceSession(device.Id);
        }
    }

    #endregion

    #region Private - Scan & Disconnect Events

    // Shiny scan observable callback — fires for every advertisement received.
    // We check hex patterns to confirm it is a Hanna device, then build/update
    // the device model and add it to AvailableDevices for the UI.
    private void OnScanResult(ScanResult scanResult)
    {
        if (!TryMatchScanResult(scanResult, out var productId, out var advertisementHex))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var peripheral = scanResult.Peripheral;
            var deviceId = peripheral.Uuid;

            if (ConnectedDevices.Any(d => d.Id == deviceId))
                return;

            var item = GetOrCreateDeviceItem(peripheral, scanResult.Rssi, productId, advertisementHex);
            if (AvailableDevices.Any(d => d.Id == item.Id))
            {
                UpdateItem(item, peripheral, scanResult.Rssi, productId, advertisementHex);
                return;
            }

            AvailableDevices.Add(item);
        });
    }

    // Shared cleanup path used by both the WhenDisconnected() subscription and the
    // connection monitor. Must be called on the main thread.
    private void CleanUpDisconnectedDevice(string deviceId)
    {
        var connectedItem = GetKnownDevice(deviceId) ??
                            ConnectedDevices.FirstOrDefault(d => d.Id == deviceId);
        if (connectedItem == null) return;

        connectedItem.IsLoadingDeviceInfo = false;
        connectedItem.IsConnecting = false;
        connectedItem.IsConnected = false;
        ConnectedDevices.Remove(connectedItem);

        if (ConnectedDevices.Count == 0)
        {
            _backgroundService?.StopService();
            StopConnectionMonitor();
        }
    }

    #endregion

    #region Private - Device Matching

    // Goes through every registered product and checks if this scan result's advertisement
    // payload contains one of the known hex fragments. First match wins.
    private bool TryMatchScanResult(ScanResult scanResult, out HNAProductId productId, out string advertisementHex)
    {
        foreach (var registeredProduct in _productMatching.RegisteredProducts)
        {
            var hex = TryGetAdvertisementHex(scanResult, registeredProduct);
            if (!string.IsNullOrEmpty(hex))
            {
                productId = registeredProduct;
                advertisementHex = hex;
                return true;
            }
        }

        productId = default;
        advertisementHex = string.Empty;
        return false;
    }

    // Checks one product's hex patterns against this scan result's advertisement data.
    // Checks manufacturer-specific data first (company ID LE + payload), then service UUIDs
    // (for devices like HI98494 that advertise the Nordic UART service UUID).
    private string TryGetAdvertisementHex(ScanResult scanResult, HNAProductId productId)
    {
        var patterns = _productMatching.GetHexPatterns(productId);
        if (patterns == null) return string.Empty;

        var adv = scanResult.AdvertisementData;

        // Manufacturer-specific data: reconstruct the full record bytes (company ID LE + payload)
        if (adv.ManufacturerData is { } manuf)
        {
            var companyBytes = BitConverter.GetBytes(manuf.CompanyId);
            var manufHex = BitConverter.ToString(companyBytes).Replace("-", "").ToLowerInvariant()
                         + BitConverter.ToString(manuf.Data).Replace("-", "").ToLowerInvariant();
            if (patterns.Any(p => manufHex.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return manufHex;
        }

        // Service UUIDs — strip dashes and compare (e.g. HI98494 advertises Nordic UART UUID)
        if (adv.ServiceUuids is { } serviceUuids)
        {
            foreach (var uuid in serviceUuids)
            {
                var uuidHex = uuid.Replace("-", "").ToLowerInvariant();
                if (patterns.Any(p => uuidHex.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    return uuidHex;
            }
        }

        return string.Empty;
    }

    #endregion

    #region Private - Device Item Management

    /// <summary>
    /// Creates a new BLE device model from peripheral information.
    /// </summary>
    private HNABleDeviceModel CreateBleDeviceItem(
        IPeripheral peripheral,
        int rssi,
        HNAProductId productId,
        string advertisementHex)
    {
        return new HNABleDeviceModel(
            peripheral.Uuid,
            GetDeviceName(peripheral),
            rssi,
            productId,
            peripheral,
            advertisementHex)
        {
            IsConnected = peripheral.Status == ConnectionState.Connected
        };
    }

    private static void UpdateItem(HNABleDeviceModel item, IPeripheral peripheral, int rssi, HNAProductId productId, string advertisementHex)
    {
        item.Device = peripheral;
        item.ProductId = productId;
        item.Name = GetDeviceName(peripheral);
        item.SignalStrength = rssi;
        item.AdvertisementHex = advertisementHex;
        item.IsConnected = peripheral.Status == ConnectionState.Connected || item.IsConnected;
    }

    private HNABleDeviceModel GetOrCreateDeviceItem(IPeripheral peripheral, int rssi, HNAProductId productId, string advertisementHex)
    {
        var deviceId = peripheral.Uuid;
        if (_knownDevices.TryGetValue(deviceId, out var existingItem))
        {
            UpdateItem(existingItem, peripheral, rssi, productId, advertisementHex);
            return existingItem;
        }

        var newItem = CreateBleDeviceItem(peripheral, rssi, productId, advertisementHex);
        _knownDevices.TryAdd(deviceId, newItem);
        return _knownDevices[deviceId];
    }

    private HNABleDeviceModel? GetKnownDevice(string deviceId)
    {
        _knownDevices.TryGetValue(deviceId, out var item);
        return item;
    }

    private void RemoveFromAvailableDevices(string deviceId)
    {
        var item = AvailableDevices.FirstOrDefault(d => d.Id == deviceId);
        if (item != null)
            AvailableDevices.Remove(item);
    }

    private static string GetDeviceName(IPeripheral peripheral)
    {
        return string.IsNullOrWhiteSpace(peripheral.Name) ? $"Hanna Device {peripheral.Uuid}" : peripheral.Name;
    }

    #endregion

    #region Private - Session & Streaming

    // The main post-connection setup. Three things happen in order:
    //  1) Discover GATT services and subscribe to the notify characteristic
    //  2) Send "info" and wait for the device to tell us its model, serial, firmware, etc.
    //  3) Start the live stream only for products that support automatic measurements
    // If command exchange never becomes usable inside the validation window, the device is
    // disconnected because pairing/bonding did not complete at the OS level.
    private async Task InitializeConnectedDeviceAsync(HNABleDeviceModel deviceItem, CancellationToken cancellationToken = default)
    {
        if (deviceItem.Device == null)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() => deviceItem.IsLoadingDeviceInfo = true);

        try
        {
            var session = await EnsureDeviceSessionAsync(deviceItem, cancellationToken);
            var result = await ValidateBondAndRequestDeviceInfoAsync(deviceItem, session, cancellationToken);
            if (!result.IsBondValidated)
            {
                throw new InvalidOperationException("Meter did not respond after pairing validation.");
            }

            if (!string.IsNullOrWhiteSpace(result.InfoResponse))
            {
                await MainThread.InvokeOnMainThreadAsync(() => ApplyDeviceInfo(deviceItem, result.InfoResponse));
            }

            await MainThread.InvokeOnMainThreadAsync(() => deviceItem.IsLoadingDeviceInfo = false);

            if (ShouldAutoStartMeasurementStream(deviceItem.ProductId))
            {
                await StartMeasurementStreamAsync(session, cancellationToken);
                StatusText = string.IsNullOrWhiteSpace(result.InfoResponse)
                    ? $"Connected to {deviceItem.Name}. Bonding confirmed. Waiting for measurements."
                    : $"Connected to {deviceItem.Name}.";
            }
            else
            {
                StatusText = string.IsNullOrWhiteSpace(result.InfoResponse)
                    ? $"Connected to {deviceItem.Name}. Bonding confirmed. Ready for commands."
                    : $"Connected to {deviceItem.Name}. Ready for commands.";
            }
        }
        catch (Exception ex)
        {
            await HandleInitializationFailureAsync(deviceItem, ex, cancellationToken);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => deviceItem.IsLoadingDeviceInfo = false);
        }
    }

    private async Task<BondValidationResult> ValidateBondAndRequestDeviceInfoAsync(
        HNABleDeviceModel deviceItem,
        DeviceSession session,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= HNAAppConstants.BondValidationTimeoutSeconds; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StatusText = $"Checking {deviceItem.Name} bonding... ({attempt}/{HNAAppConstants.BondValidationTimeoutSeconds})";
            var requestResult = await RequestDeviceInfoAsync(session, cancellationToken);
            Console.WriteLine($"[BondValidation] attempt {attempt} Received any response: {requestResult.ReceivedAnyResponse}, Info response: {requestResult.InfoResponse}");
            if (!string.IsNullOrWhiteSpace(requestResult.InfoResponse))
            {
                return new BondValidationResult(true, requestResult.InfoResponse);
            }

            if (requestResult.ReceivedAnyResponse)
            {
                return new BondValidationResult(true, null);
            }

            session.PendingAnyResponse = null;
        }

        return new BondValidationResult(false, null);
    }

    // Sends "info" and waits for the device to reply with something starting with "I,".
    // We use a TaskCompletionSource so that when HandleIncomingResponse sees the "I,..." line
    // come in over BLE, it can resolve our wait. Gives up after 6 seconds if nothing comes back.

    //      Command → Send → Wait (TCS)
    //    → Notification → Resolve TCS
    //    → OR timeout → cancel
    private async Task<DeviceInfoRequestResult> RequestDeviceInfoAsync(DeviceSession session, CancellationToken cancellationToken)
    {
        var infoResponseTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var anyResponseTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.PendingInfoResponse = infoResponseTcs; //sc2 --> wait for the "I,..." response specifically
        session.PendingAnyResponse = anyResponseTcs; //sc2 --> wait for any response to confirm the device is talking at all (some devices reply with "E,..." error if you ask for info before bonding, so this lets us distinguish "not bonded" from "not responding")

        try
        {
            await SendCommandAsync(session, "info", cancellationToken); //sc1

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(HNAAppConstants.BondValidationAttemptTimeoutSeconds)); //sc6
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            using var registration = linked.Token.Register(() =>
            {
                infoResponseTcs.TrySetCanceled(linked.Token); //sc6 --> No response, cancel
                anyResponseTcs.TrySetCanceled(linked.Token); //sc6 --> No response, cancel
            });

            var completedTask = await Task.WhenAny(infoResponseTcs.Task, anyResponseTcs.Task); //sc5
            var response = await completedTask;

            return completedTask == infoResponseTcs.Task
                ? new DeviceInfoRequestResult(response, true)
                : new DeviceInfoRequestResult(null, true);
        }
        catch (OperationCanceledException) //sc6 --> timeout or external cancellation, treat as no response
        {
            return new DeviceInfoRequestResult(null, false);
        }
        finally
        {
            session.PendingInfoResponse = null;
            session.PendingAnyResponse = null;
        }
    }

    // GATT discovery. Looks for the well-known Nordic UART Service first.
    // Falls back to scanning all services for any writable + notify-capable pair.
    // Returns the service/characteristic UUID strings needed by NotifyCharacteristic and
    // WriteCharacteristic (which take string UUIDs directly on IPeripheral in Shiny v4).
    private static async Task<(string writeServiceUuid, string writeCharUuid, string notifyServiceUuid, string notifyCharUuid)>
        GetCommandCharacteristicUuidsAsync(IPeripheral peripheral, CancellationToken cancellationToken)
    {
        var services = await peripheral.GetServicesAsync(cancellationToken);

        // Prefer Nordic UART Service
        var uartService = services.FirstOrDefault(s =>
            string.Equals(s.Uuid, HNAAppConstants.NordicUartServiceUuid, StringComparison.OrdinalIgnoreCase));

        if (uartService != null)
        {
            var chars = await peripheral.GetCharacteristicsAsync(uartService.Uuid, cancellationToken);
            var writeChar = chars.FirstOrDefault(c =>
                string.Equals(c.Uuid, HNAAppConstants.NordicUartWriteCharacteristicUuid, StringComparison.OrdinalIgnoreCase));
            var notifyChar = chars.FirstOrDefault(c =>
                string.Equals(c.Uuid, HNAAppConstants.NordicUartNotifyCharacteristicUuid, StringComparison.OrdinalIgnoreCase));

            if (writeChar != null && notifyChar != null)
                return (uartService.Uuid, writeChar.Uuid, uartService.Uuid, notifyChar.Uuid);
        }

        // Fallback: first service with a writable + notify-capable characteristic pair
        foreach (var service in services)
        {
            var chars = await peripheral.GetCharacteristicsAsync(service.Uuid, cancellationToken);
            var writeChar = chars.FirstOrDefault(c => c.CanWrite());
            var notifyChar = chars.FirstOrDefault(c => c.CanNotifyOrIndicate());

            if (writeChar != null && notifyChar != null)
                return (service.Uuid, writeChar.Uuid, service.Uuid, notifyChar.Uuid);
        }

        throw new InvalidOperationException("Required notify/write characteristics were not found on this device.");
    }

    // Creates a new DeviceSession (or returns the existing one) for a connected device.
    // Subscribes to peripheral.NotifyCharacteristic() — which handles the BLE notification
    // subscription automatically. Disposing the subscription stops notifications.
    private async Task<DeviceSession> EnsureDeviceSessionAsync(HNABleDeviceModel deviceItem, CancellationToken cancellationToken)
    {
        if (_deviceSessions.TryGetValue(deviceItem.Id, out var existingSession))
            return existingSession;

        if (deviceItem.Device == null)
            throw new InvalidOperationException("Device reference is unavailable.");

        var peripheral = deviceItem.Device;
        var (writeServiceUuid, writeCharUuid, _, notifyCharUuid) =
            await GetCommandCharacteristicUuidsAsync(peripheral, cancellationToken);

        // Capture session in closure so the handler can reference it once created.
        DeviceSession? session = null;

        //sc3 --> The NotifyCharacteristic() method returns an IObservable<CharacteristicResult> that emits a new value every time a notification is received from the device. 
        //We subscribe to this stream and process incoming data in the onNext handler.
        // If an error occurs (e.g., connection drops), we rely on the WhenDisconnected() subscription to handle cleanup, so we don't do anything in onError here.
        var notifySub = peripheral
            .NotifyCharacteristic(writeServiceUuid, notifyCharUuid)
            .Subscribe(
                onNext: result =>
                {
                    var response = DecodeResponse(result.Data);
                    if (!string.IsNullOrWhiteSpace(response) && session != null)
                        HandleIncomingResponse(deviceItem, session, response); //sc3
                },
                onError: _ => { /* Connection dropped — WhenDisconnected() subscription handles cleanup */ });

        session = new DeviceSession(peripheral, writeServiceUuid, writeCharUuid, notifySub);
        _deviceSessions[deviceItem.Id] = session;
        return session;
    }

    //sc4
    // Central dispatch for all incoming BLE data. If the response starts with "I," it's the
    // device info reply — we hand it off to the pending TaskCompletionSource from RequestDeviceInfoAsync.
    // Everything else goes to the product handler which figures out if it's a measurement (M/S/C prefix)
    // and queues it onto the device model for display.
    private void HandleIncomingResponse(HNABleDeviceModel deviceItem, DeviceSession session, string response)
    {
        session.PendingAnyResponse?.TrySetResult(response);

        if (response.StartsWith("I,", StringComparison.OrdinalIgnoreCase))
        {
            if (ShouldShowAllResponsesInHistory(deviceItem.ProductId))
            {
                deviceItem.QueueMeasurementLog(DateTime.Now, response);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    deviceItem.FlushPendingMeasurementLogs();
                    CheckAndSaveMeasurementBatchIfLimitReached(deviceItem);
                });
            }

            session.PendingInfoResponse?.TrySetResult(response);
            return;
        }

        if (response.Length < 1)
        {
            return;
        }

        var handler = _handlerRegistry.GetHandler(deviceItem.ProductId);
        if (handler == null || !handler.TryHandleResponse(deviceItem, response))
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            deviceItem.FlushPendingMeasurementLogs();
            CheckAndSaveMeasurementBatchIfLimitReached(deviceItem);
        });
    }

    /// <summary>
    /// Checks if the device's measurement batch has reached the auto-save limit (3600 records).
    /// If the limit is reached, saves the batch to the database and clears the batch for continued monitoring.
    /// </summary>
    private void CheckAndSaveMeasurementBatchIfLimitReached(HNABleDeviceModel deviceItem)
    {
        var (batchLimitReached, batchData, batchStartTime, batchEndTime) = deviceItem.CheckIfBatchLimitReached();

        if (!batchLimitReached || batchData.Count == 0)
        {
            return;
        }

        try
        {
            // Save the batch to the database
            var fileName = $"{deviceItem.SerialNumber}_{batchStartTime:yyyyMMdd_HHmmss}";
            _logRepository.SaveLogFile(
                deviceItem.Id,
                deviceItem.MeterModel,
                deviceItem.DisplayName,
                fileName,
                batchStartTime,
                batchEndTime,
                batchData);

            // Clear the batch for the next recording session
            deviceItem.ClearMeasurementBatch();

            System.Diagnostics.Debug.WriteLine(
                $"[HNABleService] Batch saved and cleared for device {deviceItem.DisplayName}. " +
                $"Records saved: {batchData.Count}, Start: {batchStartTime:HH:mm:ss}, End: {batchEndTime:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[HNABleService] Failed to save measurement batch for {deviceItem.DisplayName}: {ex.Message}");
        }
    }

    private static bool ShouldAutoStartMeasurementStream(HNAProductId productId)
    {
        return productId is HNAProductId.HI9810;
    }

    private static bool ShouldShowAllResponsesInHistory(HNAProductId productId)
    {
        return productId is HNAProductId.HI97105 or HNAProductId.HI98594 or HNAProductId.HI98494;
    }

    private void ApplyDeviceInfo(HNABleDeviceModel deviceItem, string response)
    {
        deviceItem.RawDeviceInfo = response;
        _handlerRegistry.GetHandler(deviceItem.ProductId)?.ApplyDeviceInfo(deviceItem, response);
    }

    // Tears down the notification subscription and disconnect subscription for a device.
    // Safe to call multiple times; the ConcurrentDictionary TryRemove is idempotent.
    private void StopDeviceSession(string deviceId)
    {
        if (!_deviceSessions.TryRemove(deviceId, out var session))
            return;

        session.PendingInfoResponse?.TrySetCanceled();
        session.PendingAnyResponse?.TrySetCanceled();

        session.NotifySubscription.Dispose();

        if (_disconnectSubscriptions.TryRemove(deviceId, out var disconnectSub))
            disconnectSub.Dispose();
    }

    // Tells the device to start sending measurements. Only fires once per session.
    private static async Task StartMeasurementStreamAsync(DeviceSession session, CancellationToken cancellationToken)
    {
        if (session.MeasurementStreamStarted)
            return;

        await SendCommandAsync(session, "set meas on", cancellationToken);
        session.MeasurementStreamStarted = true;
    }

    // Re-sends "set meas on" to resume measurements after the app was backgrounded.
    private static async Task RestartMeasurementStreamAsync(DeviceSession session, CancellationToken cancellationToken)
    {
        await SendCommandAsync(session, "set meas on", cancellationToken);
        session.MeasurementStreamStarted = true;
    }

    private static async Task SendCommandAsync(DeviceSession session, string command, CancellationToken cancellationToken)
    {
        var commandBytes = Encoding.UTF8.GetBytes(command);
        await session.Peripheral.WriteCharacteristicAsync(
            session.WriteServiceUuid,
            session.WriteCharUuid,
            commandBytes,
            cancelToken: cancellationToken);
    }

    private static string DecodeResponse(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(bytes).Trim('\0', '\r', '\n', ' ');
    }

    private async Task HandleInitializationFailureAsync(HNABleDeviceModel deviceItem, Exception exception, CancellationToken cancellationToken)
    {
        var failure = await PresentConnectionFailureAsync(deviceItem, exception);

        await DisconnectAsync(deviceItem, cancellationToken);
    }

    private Task HandleConnectionFailureAsync(HNABleDeviceModel deviceItem, Exception exception)
    {
        return PresentConnectionFailureAsync(deviceItem, exception);
    }

    private async Task<ConnectionFailureInfo> PresentConnectionFailureAsync(HNABleDeviceModel deviceItem, Exception exception)
    {
        var failure = ClassifyConnectionFailure(exception);
        StatusText = failure.StatusMessage.Replace("{deviceName}", deviceItem.Name, StringComparison.Ordinal);

        try
        {
            await _dialogService.ShowAlertAsync(
                failure.AlertTitle,
                failure.AlertMessage.Replace("{deviceName}", deviceItem.Name, StringComparison.Ordinal),
                "OK");
        }
        catch
        {
            // Alert delivery should not block BLE cleanup.
        }

        return failure;
    }

    private static ConnectionFailureInfo ClassifyConnectionFailure(Exception exception)
    {
        var message = exception.ToString();
        if (ContainsAny(message,
                "pair",
                "bond",
                "authentication",
                "not paired",
                "peer removed pairing information",
                "insufficient authentication",
                "encryption"))
        {
            return new ConnectionFailureInfo(
                "Pairing Issue",
                "BLE device pairing information has been deleted from the device or the bond is no longer valid. Forget the device from Bluetooth settings and try to connect again.",
                "{deviceName} disconnected because device pairing information is missing or invalid.");
        }

        return new ConnectionFailureInfo(
            "Bonding Failed",
            "Device disconnected due to connection or bonding issue. The meter did not respond to pairing validation commands within 35 seconds.",
            "{deviceName} disconnected because pairing validation did not complete.");
    }

    private static bool ContainsAny(string input, params string[] values)
    {
        return values.Any(value => input.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    private sealed class DeviceSession(
        IPeripheral peripheral,
        string writeServiceUuid,
        string writeCharUuid,
        IDisposable notifySubscription)
    {
        public IPeripheral Peripheral { get; } = peripheral;
        public string WriteServiceUuid { get; } = writeServiceUuid;
        public string WriteCharUuid { get; } = writeCharUuid;
        public IDisposable NotifySubscription { get; } = notifySubscription;
        public TaskCompletionSource<string>? PendingInfoResponse { get; set; }
        public TaskCompletionSource<string>? PendingAnyResponse { get; set; }
        public bool MeasurementStreamStarted { get; set; }
    }

    private sealed record ConnectionFailureInfo(string AlertTitle, string AlertMessage, string StatusMessage);
    private sealed record BondValidationResult(bool IsBondValidated, string? InfoResponse);
    private sealed record DeviceInfoRequestResult(string? InfoResponse, bool ReceivedAnyResponse);
}
