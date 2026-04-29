using HannaDemoApp.Core.Enums;

namespace HannaDemoApp.Core.DeviceHandlers;

// Maps HNAProductId values to their device handlers.
// New devices are added by calling Register — no other code changes needed.
public sealed class HNADeviceHandlerRegistry
{
    private readonly Dictionary<HNAProductId, IHNADeviceHandler> _handlers = new();

    public HNADeviceHandlerRegistry()
    {
        Register(HNAProductId.HI9810, new HNAHaloDeviceHandler());
        Register(HNAProductId.HI98494, new HNAMultiMeterDeviceHandler());
        Register(HNAProductId.HI97105, new HNAPhotometerDeviceHandler());
        Register(HNAProductId.HI98594, new HNAMultiMeterDeviceHandler());
    }

    // Wire up a handler for a product family. Call this when adding support for a new device.
    public void Register(HNAProductId productId, IHNADeviceHandler handler)
    {
        _handlers[productId] = handler;
    }

    // Grab the handler for a product, or null if we don't support it yet.
    public IHNADeviceHandler? GetHandler(HNAProductId productId)
    {
        _handlers.TryGetValue(productId, out var handler);
        return handler;
    }

    // All product IDs we know about — used to set up scan filters.
    public IEnumerable<HNAProductId> RegisteredProducts => _handlers.Keys;
}
