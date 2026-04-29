using HannaDemoApp.Core.Enums;

namespace HannaDemoApp.Services.Ble;

/// <summary>
/// Matches BLE advertisement hex patterns to product IDs.
/// </summary>
public sealed class HNAProductMatchingService : IHNAProductMatchingService
{
    // Product → advertisement hex patterns used for device identification
    private readonly Dictionary<HNAProductId, string[]> _productHex = new()
    {
        { HNAProductId.HI9810, ["8f070100"] },
        { HNAProductId.HI98494, ["6e400001b5a3f393e0a9e50e24dcca9e"] },
        { HNAProductId.HI97105, ["8f070200"] },
        { HNAProductId.HI98594, ["8f070400"] }
    };

    /// <summary>
    /// List of all supported product IDs.
    /// </summary>
    public IEnumerable<HNAProductId> RegisteredProducts => _productHex.Keys;

    /// <summary>
    /// Adds or updates a product with its advertisement patterns.
    /// </summary>
    public void Register(HNAProductId productId, string[] hexPatterns)
    {
        ArgumentNullException.ThrowIfNull(hexPatterns, nameof(hexPatterns));
        if (hexPatterns.Length == 0)
            throw new ArgumentException("At least one hex pattern is required.", nameof(hexPatterns));

        _productHex[productId] = hexPatterns;
    }

    /// <summary>
    /// Returns advertisement patterns for a product, if registered.
    /// </summary>
    public string[]? GetHexPatterns(HNAProductId productId)
    {
        _productHex.TryGetValue(productId, out var patterns);
        return patterns;
    }
}