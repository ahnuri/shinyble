using HannaDemoApp.Models;

namespace HannaDemoApp.Core.DeviceHandlers;

// What every product handler must be able to do.
public interface IHNADeviceHandler
{
    // Parse the "I,..." device info string and populate the device model with metadata.
    void ApplyDeviceInfo(HNABleDeviceModel deviceItem, string response);

    // Try to handle an incoming BLE response. Returns true if this handler consumed it.
    bool TryHandleResponse(HNABleDeviceModel deviceItem, string response);

    // The list of commands this product family supports (shown in the UI command picker).
    IReadOnlyList<string> GetCommands();

    /// <summary>When true, the service sends <c>set meas on</c> after bonding and on resume.</summary>
    bool ShouldAutoStartMeasurementStream { get; }

    /// <summary>When true, <c>I,</c> info lines are also appended to the session measurement list.</summary>
    bool ShouldQueueDeviceInfoInMeasurementHistory { get; }

    /// <summary>When true, the 3600-record auto-save / flush cycle applies (Halo live stream).</summary>
    bool UsesTimedMeasurementBatchPersistence { get; }
}
