namespace HannaDemoApp.Core.DeviceHandlers;

// Handles BLE responses for HI98494 and HI98594 (MultiMeter) devices.
// Uses default measurement parsing from base class.
public sealed class HNAMultiMeterDeviceHandler : HNADeviceHandlerBase
{
    public override bool ShouldQueueDeviceInfoInMeasurementHistory => true;

    private static readonly IReadOnlyList<string> Commands =
    [
        "info",
        "ll G",
        "head G:/",
        "cat G:/"
    ];

    public override IReadOnlyList<string> GetCommands() => Commands;
}
