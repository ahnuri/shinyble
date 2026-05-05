namespace HannaDemoApp.Services.Ota;

public sealed record HNAOtaDeviceCapability(
    string MeterModel,
    string ProductIdHexUpper,
    bool SupportsOta);

public static class HNAOtaCapabilities
{
    private static readonly Dictionary<string, HNAOtaDeviceCapability> Known =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["HI97115"] = new HNAOtaDeviceCapability("HI97115", "8F070200", true),
            ["HI97105"] = new HNAOtaDeviceCapability("HI97105", "8F070200", true),
            ["HI98594"] = new HNAOtaDeviceCapability("HI98594", "8F070400", true),
            ["HI9810"] = new HNAOtaDeviceCapability("HI9810", string.Empty, false)
        };

    public static bool TryGet(string meterModel, out HNAOtaDeviceCapability capability) =>
        Known.TryGetValue(meterModel.Trim(), out capability!);
}

