namespace HannaDemoApp.Core.Constants;

// Application-wide static configuration values.
public static class HNAAppConstants
{
    public const int DefaultScanTimeoutMs = 10_000;
    public const int DeviceInfoTimeoutSeconds = 6;
    public const int BondValidationTimeoutSeconds = 30;
    public const int BondValidationAttemptTimeoutSeconds = 1;
    /// <summary>Auto-save batch size (e.g. 1 Hz × 3600 s).</summary>
    public const int MaxMeasurementLogEntries = 3600;

    public const string NordicUartServiceUuid = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
    public const string NordicUartWriteCharacteristicUuid = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E";
    public const string NordicUartNotifyCharacteristicUuid = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E";

    public const string DatabaseFileName = "hannalogs.db";

    public static class AndroidNotification
    {
        public const string ChannelId = "HannaBleChannel";
        public const int ForegroundServiceNotificationId = 1001;
    }

    public static class Routes
    {
        public const string Landing = "Landing";
        public const string Devices = "Devices";
        public const string LogHistory = "LogHistory";
        public const string LiveDetails = "LiveDetails";
        public const string ConnectedPhotometerDetails = "ConnectedPhotometerDetails";
        public const string ConnectedMultiMeterDetails = "ConnectedMultiMeterDetails";
        public const string LogDetail = "LogDetail";
        public const string UserSettings = "UserSettings";
    }
}
