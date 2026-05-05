namespace HannaDemoApp.Core.Enums;

/// <summary>
/// Maps meter-reported model strings (from the <c>I,</c> info response) to <see cref="HNAProductId"/>.
/// Keeps scan-based product hints aligned with the actual device after connection.
/// </summary>
public static class HNAProductCatalog
{
    public static HNAProductId? ResolveFromMeterModel(string? meterModel)
    {
        if (string.IsNullOrWhiteSpace(meterModel))
        {
            return null;
        }

        if (meterModel.StartsWith("HI9810", StringComparison.OrdinalIgnoreCase))
        {
            return HNAProductId.HI9810;
        }

        if (meterModel.StartsWith("HI98494", StringComparison.OrdinalIgnoreCase))
        {
            return HNAProductId.HI98494;
        }

        if (meterModel.StartsWith("HI98594", StringComparison.OrdinalIgnoreCase))
        {
            return HNAProductId.HI98594;
        }

        if (meterModel.StartsWith("HI97105", StringComparison.OrdinalIgnoreCase))
        {
            return HNAProductId.HI97105;
        }

        if (meterModel.StartsWith("HI97115", StringComparison.OrdinalIgnoreCase))
        {
            return HNAProductId.HI97115;
        }

        return null;
    }
}
