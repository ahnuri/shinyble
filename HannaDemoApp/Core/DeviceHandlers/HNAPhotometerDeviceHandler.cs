using HannaDemoApp.Models;

namespace HannaDemoApp.Core.DeviceHandlers;

// Handles BLE responses for HI97105 (Photometer) devices.
public sealed class HNAPhotometerDeviceHandler : HNADeviceHandlerBase
{
    private static readonly IReadOnlyList<string> Commands =
    [
        "get recall",
        "beep",
        "get setup languages,all",
        "get setup tank,all",
        "get battery",
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
