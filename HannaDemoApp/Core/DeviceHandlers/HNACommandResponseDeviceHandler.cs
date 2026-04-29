using HannaDemoApp.Models;

namespace HannaDemoApp.Core.DeviceHandlers;

// Handles devices where each BLE reply should be shown as a response row instead of a live stream.
public sealed class HNACommandResponseDeviceHandler : HNADeviceHandlerBase
{
    private static readonly IReadOnlyList<string> Commands =
    [
        "set meas on",
        "get setup",
        "get battery",
        "get glp",
        "info"
    ];

    public override bool TryHandleResponse(HNABleDeviceModel deviceItem, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        deviceItem.QueueMeasurementLog(DateTime.Now, response);
        return true;
    }

    public override IReadOnlyList<string> GetCommands() => Commands;
}
