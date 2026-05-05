using HannaDemoApp.Models;

namespace HannaDemoApp.Core.DeviceHandlers;

// Shared base logic for parsing device info and measurement responses.
// Subclasses override only what differs for their product family.
public abstract class HNADeviceHandlerBase : IHNADeviceHandler
{
    public virtual bool ShouldAutoStartMeasurementStream => false;

    public virtual bool ShouldQueueDeviceInfoInMeasurementHistory => false;

    public virtual bool UsesTimedMeasurementBatchPersistence => false;

    public virtual void ApplyDeviceInfo(HNABleDeviceModel deviceItem, string response)
    {
        var values = response.Split(',', StringSplitOptions.TrimEntries);
        if (values.Length < 2 || !string.Equals(values[0], "I", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var deviceInfo = deviceItem.DeviceInfo;
        deviceInfo.RawDeviceInfo = response;
        deviceInfo.RecallCount = string.Empty;
        deviceInfo.Language = string.Empty;
        deviceInfo.LanguageVersion = string.Empty;
        deviceInfo.CalibrationDate = string.Empty;

        deviceItem.MeterModel = values.Length > 1 ? values[1] : string.Empty;
        deviceItem.MeterId = values.Length > 2 ? values[2] : string.Empty;
        deviceItem.MeterFirmwareVersion = FindValueAfterLabel(values, "FW");
        deviceItem.BleFirmwareVersion = FindValueAfterLabel(values, "nRF FW");
        deviceItem.SerialNumber = FindValueAfterLabel(values, "SN");

        ApplyPhotometerInfo(deviceInfo, values);
        ApplyHaloInfo(deviceInfo, values);
    }

    public virtual bool TryHandleResponse(HNABleDeviceModel deviceItem, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        if (TryApplyBatteryStatus(deviceItem, response))
        {
            return true;
        }

        if (IsKnownNonMeasurementPrefix(response))
        {
            return false;
        }

        if (IsMeasurementResponse(response))
        {
            deviceItem.QueueMeasurementLog(DateTime.Now, response);
            return true;
        }

        return false;
    }

    public abstract IReadOnlyList<string> GetCommands();

    protected static bool IsMeasurementResponse(string response)
    {
        if (response.Length == 0)
        {
            return false;
        }

        var type = char.ToUpperInvariant(response[0]);
        return type is 'M' or 'S' or 'C' && response.Length > 1 && response[1] == ',';
    }

    protected static string FindValueAfterLabel(IReadOnlyList<string> values, string label)
    {
        for (var i = 0; i < values.Count - 1; i++)
        {
            if (string.Equals(values[i], label, StringComparison.OrdinalIgnoreCase))
            {
                return values[i + 1];
            }
        }

        return string.Empty;
    }

    protected static int FindLabelIndex(IReadOnlyList<string> values, string label)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], label, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void ApplyPhotometerInfo(HNADeviceInfoModel deviceInfo, IReadOnlyList<string> values)
    {
        var recallIndex = FindLabelIndex(values, "RCL");
        if (recallIndex < 0)
        {
            return;
        }

        deviceInfo.RecallCount = recallIndex + 1 < values.Count ? values[recallIndex + 1] : string.Empty;
        deviceInfo.Language = recallIndex + 2 < values.Count ? values[recallIndex + 2] : string.Empty;
        deviceInfo.LanguageVersion = recallIndex + 3 < values.Count ? values[recallIndex + 3] : string.Empty;
    }

    private static void ApplyHaloInfo(HNADeviceInfoModel deviceInfo, IReadOnlyList<string> values)
    {
        if (!string.IsNullOrWhiteSpace(deviceInfo.RecallCount))
        {
            return;
        }

        var serialIndex = FindLabelIndex(values, "SN");
        if (serialIndex >= 0 && serialIndex + 2 < values.Count)
        {
            deviceInfo.CalibrationDate = values[serialIndex + 2];
        }
    }

    protected static bool TryApplyBatteryStatus(HNABleDeviceModel deviceItem, string response)
    {
        if (!response.StartsWith("GB,", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var batteryValue = response[3..].Trim();
        deviceItem.BatteryStatus = string.IsNullOrWhiteSpace(batteryValue)
            ? "Unavailable"
            : batteryValue;

        return true;
    }

    private static bool IsKnownNonMeasurementPrefix(string response)
    {
        return response.StartsWith("I,", StringComparison.OrdinalIgnoreCase)
            || response.StartsWith("GB,", StringComparison.OrdinalIgnoreCase)
            || response.StartsWith("GG,", StringComparison.OrdinalIgnoreCase)
            || response.StartsWith("GS,", StringComparison.OrdinalIgnoreCase)
            || response.StartsWith("GT,", StringComparison.OrdinalIgnoreCase);
    }
}
