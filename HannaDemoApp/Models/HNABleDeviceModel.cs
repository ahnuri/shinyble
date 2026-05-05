using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HannaDemoApp.Core.Constants;
using HannaDemoApp.Core.Enums;
using Shiny.BluetoothLE;

namespace HannaDemoApp.Models;

/// <summary>
/// Represents a BLE device entry shown in the scan and connection UI.
/// This is a pure data model with no service dependencies.
/// Measurement persistence is handled by the application service layer.
/// </summary>
public class HNABleDeviceModel : INotifyPropertyChanged
{
    private readonly Lock _measurementLogLock = new();
    private readonly object _haloPersistLock = new();
    private readonly List<HNAMeasurementLogModel> _haloPersistBuffer = [];
    private readonly Queue<HNAMeasurementLogModel> _pendingMeasurementLogs = new();
    private IPeripheral? _device;
    private bool _isConnected;
    private bool _isConnecting;
    private bool _isLoadingDeviceInfo;
    private string _name;
    private int _signalStrength;
    private string _advertisementHex = string.Empty;
    private string _batteryStatus = "Checking...";
    private string _lastValue = string.Empty;
    private DateTime? _batchStartTime;
    private DateTime? _lastMeasurementTimestamp;
    private HNAProductId _productId;

    /// <summary>
    /// Creates a new BLE device model with basic identification information.
    /// </summary>
    /// <param name="id">Unique device identifier (UUID).</param>
    /// <param name="name">Device name/advertising name.</param>
    /// <param name="signalStrength">RSSI signal strength in dBm.</param>
    /// <param name="productId">The type of Hanna product.</param>
    /// <param name="device">Optional reference to the underlying Shiny peripheral.</param>
    /// <param name="advertisementHex">Optional hex data from BLE advertisement.</param>
    public HNABleDeviceModel(
        string id,
        string name,
        int signalStrength,
        HNAProductId productId,
        IPeripheral? device = null,
        string advertisementHex = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        Id = id;
        _name = name;
        _signalStrength = signalStrength;
        _productId = productId;
        _device = device;
        _advertisementHex = advertisementHex;
        DeviceInfo.PropertyChanged += OnDeviceInfoPropertyChanged;
    }

    public string Id { get; }
    public HNADeviceInfoModel DeviceInfo { get; } = new();

    public HNAProductId ProductId
    {
        get => _productId;
        set
        {
            if (SetField(ref _productId, value))
            {
                OnPropertyChanged(nameof(DetailWorkflow));
                OnPropertyChanged(nameof(IsHaloDevice));
                OnPropertyChanged(nameof(UsesLiveMeasurementUi));
                OnPropertyChanged(nameof(UsesPhotometerDetailsUi));
                OnPropertyChanged(nameof(UsesMultiMeterDetailsUi));
                OnPropertyChanged(nameof(ResponseHistoryTitle));
                OnPropertyChanged(nameof(DeviceDetailsButtonText));
                OnPropertyChanged(nameof(LiveDetailsButtonText));
            }
        }
    }

    public IPeripheral? Device
    {
        get => _device;
        set => SetField(ref _device, value);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public int SignalStrength
    {
        get => _signalStrength;
        set
        {
            if (SetField(ref _signalStrength, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    /// <summary>
    /// Gets or sets the advertise hex data from BLE advertisements (diagnostic/matching).
    /// </summary>
    public string AdvertisementHex
    {
        get => _advertisementHex;
        set => SetField(ref _advertisementHex, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (!SetField(ref _isConnected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(Subtitle));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(ConnectButtonText));

            if (!value)
            {
                lock (_measurementLogLock)
                {
                    // Note: Measurement persistence is handled by the BLE service layer,
                    // not here in the model. The model is responsible only for storing data.
                    _batchStartTime = null;
                    _lastMeasurementTimestamp = null;
                }
            }
        }
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        set
        {
            if (SetField(ref _isConnecting, value))
            {
                OnPropertyChanged(nameof(Subtitle));
                OnPropertyChanged(nameof(CanConnect));
                OnPropertyChanged(nameof(ConnectButtonText));
            }
        }
    }

    public bool IsLoadingDeviceInfo
    {
        get => _isLoadingDeviceInfo;
        set
        {
            if (SetField(ref _isLoadingDeviceInfo, value))
            {
                OnPropertyChanged(nameof(HasDeviceInfo));
            }
        }
    }

    public string MeterModel
    {
        get => DeviceInfo.MeterModel;
        set => DeviceInfo.MeterModel = value;
    }

    public string MeterId
    {
        get => DeviceInfo.MeterId;
        set => DeviceInfo.MeterId = value;
    }

    public string MeterFirmwareVersion
    {
        get => DeviceInfo.MeterFirmwareVersion;
        set => DeviceInfo.MeterFirmwareVersion = value;
    }

    public string BleFirmwareVersion
    {
        get => DeviceInfo.BleFirmwareVersion;
        set => DeviceInfo.BleFirmwareVersion = value;
    }

    public string SerialNumber
    {
        get => DeviceInfo.SerialNumber;
        set => DeviceInfo.SerialNumber = value;
    }

    public string UserSetName
    {
        get => DeviceInfo.UserSetName;
        set => DeviceInfo.UserSetName = value;
    }

    public string DisplayName => Name;

    public string RawDeviceInfo
    {
        get => DeviceInfo.RawDeviceInfo;
        set => DeviceInfo.RawDeviceInfo = value;
    }

    public string BatteryStatus
    {
        get => _batteryStatus;
        set => SetField(ref _batteryStatus, value);
    }

    public string LastValue
    {
        get => _lastValue;
        private set
        {
            if (SetField(ref _lastValue, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public ObservableCollection<HNAMeasurementLogModel> MeasurementLogs { get; } = [];
    public HNADeviceDetailWorkflow DetailWorkflow
    {
        get
        {
            var model = DeviceInfo.MeterModel;
            if (!string.IsNullOrWhiteSpace(model))
            {
                if (model.StartsWith("HI9810", StringComparison.OrdinalIgnoreCase))
                {
                    return HNADeviceDetailWorkflow.LiveReadings;
                }

                if (model.StartsWith("HI98494", StringComparison.OrdinalIgnoreCase)
                    || model.StartsWith("HI98594", StringComparison.OrdinalIgnoreCase))
                {
                    return HNADeviceDetailWorkflow.MultiMeterDetails;
                }

                if (model.StartsWith("HI97115", StringComparison.OrdinalIgnoreCase)
                    || model.StartsWith("HI97105", StringComparison.OrdinalIgnoreCase))
                {
                    return HNADeviceDetailWorkflow.PhotometerDetails;
                }
            }

            return ProductId switch
            {
                HNAProductId.HI9810 => HNADeviceDetailWorkflow.LiveReadings,
                HNAProductId.HI98494 or HNAProductId.HI98594 => HNADeviceDetailWorkflow.MultiMeterDetails,
                HNAProductId.HI97115 or HNAProductId.HI97105 => HNADeviceDetailWorkflow.PhotometerDetails,
                _ => HNADeviceDetailWorkflow.PhotometerDetails
            };
        }
    }

    public bool IsHaloDevice => DetailWorkflow == HNADeviceDetailWorkflow.LiveReadings;
    public bool UsesLiveMeasurementUi => DetailWorkflow == HNADeviceDetailWorkflow.LiveReadings;
    public bool UsesPhotometerDetailsUi => DetailWorkflow == HNADeviceDetailWorkflow.PhotometerDetails;
    public bool UsesMultiMeterDetailsUi => DetailWorkflow == HNADeviceDetailWorkflow.MultiMeterDetails;
    public string ResponseHistoryTitle => DetailWorkflow switch
    {
        HNADeviceDetailWorkflow.LiveReadings => "Live Measurements",
        HNADeviceDetailWorkflow.MultiMeterDetails => "MultiMeter responses",
        _ => "Photometer responses"
    };

    public string DeviceDetailsButtonText => DetailWorkflow switch
    {
        HNADeviceDetailWorkflow.LiveReadings => "View Live",
        HNADeviceDetailWorkflow.MultiMeterDetails => "View meter",
        _ => "View details"
    };
    public string LiveDetailsButtonText => DeviceDetailsButtonText;

    public string Subtitle
    {
        get
        {
            var status = IsConnected ? "Connected" : IsConnecting ? "Connecting" : "Available";
            var rssiText = $"RSSI {SignalStrength} dBm";
            return string.IsNullOrWhiteSpace(LastValue)
                ? $"{status} • {rssiText}"
                : $"{status} • {LastValue} • {rssiText}";
        }
    }

    public bool HasDeviceInfo => DeviceInfo.HasValues;

    public bool HasMeasurementLogs => MeasurementLogs.Count > 0;
    public bool CanConnect => !IsConnected && !IsConnecting;
    public string ConnectButtonText => IsConnected ? "Connected" : IsConnecting ? "Connecting..." : "Connect";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddMeasurementLog(DateTime recordedAt, string response, int maxEntries = HNAAppConstants.MaxMeasurementLogEntries)
    {
        AddMeasurementLog(new HNAMeasurementLogModel(recordedAt, response), maxEntries);
    }

    public void QueueMeasurementLog(DateTime recordedAt, string response)
    {
        lock (_measurementLogLock)
        {
            _pendingMeasurementLogs.Enqueue(new HNAMeasurementLogModel(recordedAt, response));
        }
    }

    /// <summary>
    /// Halo live stream: enqueue for UI (pending queue) and for an internal persistence buffer.
    /// The BLE service drains that buffer off the UI thread so batches still save when Android throttles the main looper.
    /// </summary>
    public void QueueHaloLiveMeasurement(DateTime recordedAt, string response)
    {
        var forUi = new HNAMeasurementLogModel(recordedAt, response);
        lock (_measurementLogLock)
        {
            _pendingMeasurementLogs.Enqueue(forUi);
        }

        lock (_haloPersistLock)
        {
            _haloPersistBuffer.Add(new HNAMeasurementLogModel(recordedAt, response));
        }
    }

    /// <summary>Copies the first <paramref name="batchSize"/> Halo rows without removing them (remove after successful DB write).</summary>
    public bool TryCopyFrontHaloPersistBatch(int batchSize, out List<HNAMeasurementLogModel> batch)
    {
        lock (_haloPersistLock)
        {
            if (_haloPersistBuffer.Count < batchSize)
            {
                batch = [];
                return false;
            }

            batch = _haloPersistBuffer.GetRange(0, batchSize).ToList();
            return true;
        }
    }

    public void RemoveFrontFromHaloPersistBuffer(int count)
    {
        lock (_haloPersistLock)
        {
            if (count <= 0 || _haloPersistBuffer.Count == 0)
            {
                return;
            }

            var n = Math.Min(count, _haloPersistBuffer.Count);
            _haloPersistBuffer.RemoveRange(0, n);
        }
    }

    /// <summary>Removes all remaining Halo persistence records (e.g. disconnect or final flush).</summary>
    public List<HNAMeasurementLogModel> DrainHaloPersistBuffer()
    {
        lock (_haloPersistLock)
        {
            if (_haloPersistBuffer.Count == 0)
            {
                return [];
            }

            var rest = _haloPersistBuffer.ToList();
            _haloPersistBuffer.Clear();
            return rest;
        }
    }

    public DateTime GetNextIncrementalMeasurementTime()
    {
        lock (_measurementLogLock)
        {
            if (!_lastMeasurementTimestamp.HasValue)
            {
                _lastMeasurementTimestamp = DateTime.Now;
                return _lastMeasurementTimestamp.Value;
            }

            _lastMeasurementTimestamp = _lastMeasurementTimestamp.Value.AddSeconds(1);
            return _lastMeasurementTimestamp.Value;
        }
    }

    /// <summary>
    /// Flushes all pending measurement logs into the main measurement collection.
    /// Used when a batch of measurements needs to be added to the display.
    /// </summary>
    public void FlushPendingMeasurementLogs(int maxEntries = HNAAppConstants.MaxMeasurementLogEntries)
    {
        lock (_measurementLogLock)
        {
            while (_pendingMeasurementLogs.Count > 0)
            {
                AddMeasurementLog(_pendingMeasurementLogs.Dequeue(), maxEntries);
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnDeviceInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.PropertyName))
        {
            OnPropertyChanged(e.PropertyName);
        }

        if (e.PropertyName is nameof(HNADeviceInfoModel.UserSetName) or nameof(HNADeviceInfoModel.MeterId))
        {
            OnPropertyChanged(nameof(UserSetName));
            OnPropertyChanged(nameof(MeterId));
        }

        if (e.PropertyName is nameof(HNADeviceInfoModel.MeterModel))
        {
            OnPropertyChanged(nameof(DetailWorkflow));
            OnPropertyChanged(nameof(IsHaloDevice));
            OnPropertyChanged(nameof(UsesLiveMeasurementUi));
            OnPropertyChanged(nameof(UsesPhotometerDetailsUi));
            OnPropertyChanged(nameof(UsesMultiMeterDetailsUi));
            OnPropertyChanged(nameof(ResponseHistoryTitle));
            OnPropertyChanged(nameof(DeviceDetailsButtonText));
            OnPropertyChanged(nameof(LiveDetailsButtonText));
        }

        OnPropertyChanged(nameof(HasDeviceInfo));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Adds a measurement log entry to the device.
    /// Notifies listeners when the log count exceeds maxEntries (can be used to trigger persistence).
    /// </summary>
    private void AddMeasurementLog(HNAMeasurementLogModel entry, int maxEntries)
    {
        if (TryGetMeasurementValue(entry.Response, out var latestValue))
        {
            LastValue = latestValue;
        }

        _batchStartTime ??= entry.RecordedAt;
        MeasurementLogs.Add(entry);

        OnPropertyChanged(nameof(HasMeasurementLogs));
        
        // Note: Measurement persistence is signaled via property changes.
        // The application service layer monitors these changes and saves when appropriate.
    }

    /// <summary>
    /// Indicates if this device type supports auto-saved measurement logs.
    /// </summary>
    public bool SupportsAutoSavedMeasurementLogs => DetailWorkflow == HNADeviceDetailWorkflow.LiveReadings;

    /// <summary>
    /// Attempts to extract the measurement value from a device response string.
    /// </summary>
    private static bool TryGetMeasurementValue(string response, out string latestValue)
    {
        latestValue = string.Empty;
        if (string.IsNullOrWhiteSpace(response) || response.Length < 3)
        {
            return false;
        }

        var type = char.ToUpperInvariant(response[0]);
        if (type is not ('M' or 'S' or 'C'))
        {
            return false;
        }

        latestValue = response[2..].Trim();
        return !string.IsNullOrWhiteSpace(latestValue);
    }

    /// <summary>
    /// Clears the current measurement batch after it has been saved to the database.
    /// Resets the batch state for the next recording session.
    /// </summary>
    public void ClearMeasurementBatch()
    {
        lock (_measurementLogLock)
        {
            MeasurementLogs.Clear();
            _batchStartTime = null;
            _lastMeasurementTimestamp = null;
            OnPropertyChanged(nameof(HasMeasurementLogs));
        }
    }

}
