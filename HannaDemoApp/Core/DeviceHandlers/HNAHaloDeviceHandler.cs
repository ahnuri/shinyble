using HannaDemoApp.Models;

namespace HannaDemoApp.Core.DeviceHandlers;

// Handles BLE responses for HI9810 (Halo) devices.
// Uses incremental timestamps for measurement responses.
public sealed class HNAHaloDeviceHandler : HNADeviceHandlerBase
{
    private static readonly IReadOnlyList<string> Commands =
    [
        "set setup start",
        "set setup exit",
        "get tag",
        "set setup Tag stability",
        "set setup Tag current",
        "set standby",
        "display clr",
        "display TEMP BROKEN",
        "display TEMP HIGH",
        "display TEMP LOW",
        "display PH HIGH",
        "display PH LOW",
        "display MV HIGH",
        "display MV LOW",
        "get glp",
        "get setup",
        "get battery",
        "display HERE I AM",
        "set cal confirm",
        "set cal exit",
        "set cal clear",
        "set setup ATC",
        "set meas on",
        "set cal start",
        "set setup pH",
        "set setup mV",
        "set setup Celsius",
        "set setup Fahrenheit",
        "set setup Stability slow",
        "set setup Stability medium",
        "set setup Stability fast",
        "set setup Aoff disabled",
        "set setup Aoff 8min",
        "set setup Aoff 60min",
        "set setup Buffer NIST",
        "set setup Buffer Hanna",
        "set setup 0.1pH",
        "set setup 0.01pH",
        "set setup 1mV",
        "set setup 0.1mV"
    ];

    public override bool TryHandleResponse(HNABleDeviceModel deviceItem, string response)
    {
        if (string.IsNullOrWhiteSpace(response) || response.StartsWith("I,", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryApplyBatteryStatus(deviceItem, response))
        {
            return true;
        }

        if (!IsMeasurementResponse(response))
        {
            return false;
        }

        deviceItem.QueueMeasurementLog(deviceItem.GetNextIncrementalMeasurementTime(), response);
        return true;
    }

    public override IReadOnlyList<string> GetCommands() => Commands;
}
