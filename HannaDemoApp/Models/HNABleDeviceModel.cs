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
    private readonly Queue<HNAMeasurementLogModel> _pendingMeasurementLogs = new();
    private IPeripheral? _device;
    private bool _isConnected;
    private bool _isConnecting;
    private bool _isLoadingDeviceInfo;
    private string _name;
    private int _signalStrength;
    private string _advertisementHex = string.Empty;
    private string _meterModel = string.Empty;
    private string _meterFirmwareVersion = string.Empty;
    private string _bleFirmwareVersion = string.Empty;
    private string _serialNumber = string.Empty;
    private string _userSetName = string.Empty;
    private string _rawDeviceInfo = string.Empty;
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
    }

    public string Id { get; }

    public HNAProductId ProductId
    {
        get => _productId;
        set
        {
            if (SetField(ref _productId, value))
            {
                OnPropertyChanged(nameof(UsesLiveMeasurementUi));
                OnPropertyChanged(nameof(ResponseHistoryTitle));
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
        get => _meterModel;
        set
        {
            if (SetField(ref _meterModel, value))
            {
                OnPropertyChanged(nameof(HasDeviceInfo));
            }
        }
    }

    public string MeterFirmwareVersion
    {
        get => _meterFirmwareVersion;
        set
        {
            if (SetField(ref _meterFirmwareVersion, value))
            {
                OnPropertyChanged(nameof(HasDeviceInfo));
            }
        }
    }

    public string BleFirmwareVersion
    {
        get => _bleFirmwareVersion;
        set
        {
            if (SetField(ref _bleFirmwareVersion, value))
            {
                OnPropertyChanged(nameof(HasDeviceInfo));
            }
        }
    }

    public string SerialNumber
    {
        get => _serialNumber;
        set
        {
            if (SetField(ref _serialNumber, value))
            {
                OnPropertyChanged(nameof(HasDeviceInfo));
            }
        }
    }

    public string UserSetName
    {
        get => _userSetName;
        set
        {
            if (SetField(ref _userSetName, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(HasDeviceInfo));
            }
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(UserSetName) ? Name : UserSetName;

    public string RawDeviceInfo
    {
        get => _rawDeviceInfo;
        set
        {
            if (SetField(ref _rawDeviceInfo, value))
            {
                OnPropertyChanged(nameof(HasDeviceInfo));
            }
        }
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
    public bool UsesLiveMeasurementUi => ProductId == HNAProductId.HI9810;
    public string ResponseHistoryTitle => UsesLiveMeasurementUi ? "Live Measurements" : "Command Responses";

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

    public bool HasDeviceInfo =>
        !string.IsNullOrWhiteSpace(MeterModel) ||
        !string.IsNullOrWhiteSpace(MeterFirmwareVersion) ||
        !string.IsNullOrWhiteSpace(BleFirmwareVersion) ||
        !string.IsNullOrWhiteSpace(SerialNumber) ||
        !string.IsNullOrWhiteSpace(UserSetName) ||
        !string.IsNullOrWhiteSpace(RawDeviceInfo);

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
    public bool SupportsAutoSavedMeasurementLogs => ProductId == HNAProductId.HI9810;

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
    /// Checks if the measurement batch has reached the limit for auto-save.
    /// Returns the batch data if limit is reached, along with timing information.
    /// </summary>
    public (bool batchLimitReached, List<HNAMeasurementLogModel> batchData, DateTime batchStartTime, DateTime batchEndTime) 
        CheckIfBatchLimitReached(int maxEntries = HNAAppConstants.MaxMeasurementLogEntries)
    {
        lock (_measurementLogLock)
        {
            if (MeasurementLogs.Count >= maxEntries && _batchStartTime.HasValue && MeasurementLogs.Count > 0)
            {
                var batchData = new List<HNAMeasurementLogModel>(MeasurementLogs);
                var startTime = _batchStartTime.Value;
                var endTime = MeasurementLogs[MeasurementLogs.Count - 1].RecordedAt;
                return (true, batchData, startTime, endTime);
            }

            return (false, new List<HNAMeasurementLogModel>(), DateTime.MinValue, DateTime.MinValue);
        }
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

    /// <summary>
    /// Extracts current measurement logs for persistence by the service layer.
    /// Clears the internal logs and resets batch tracking.
    /// </summary>
    /// <returns>List of measurements to be persisted, or empty if no measurements.</returns>
    public List<HNAMeasurementLogModel> ExtractMeasurementsForPersistence()
    {
        lock (_measurementLogLock)
        {
            if (MeasurementLogs.Count == 0)
            {
                return [];
            }

            var records = MeasurementLogs.Select(entry => 
                new HNAMeasurementLogModel(entry.Timestamp, entry.Response)).ToList();

            MeasurementLogs.Clear();
            _lastMeasurementTimestamp = null;
            _batchStartTime = null;

            return records;
        }
    }

    /// <summary>
    /// Gets the batch start time for persistence file naming.
    /// </summary>
    public DateTime? GetBatchStartTime()
    {
        lock (_measurementLogLock)
        {
            return _batchStartTime;
        }
    }
}
