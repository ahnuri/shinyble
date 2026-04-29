using HannaDemoApp.Core.Enums;

namespace HannaDemoApp.Services.Ble;

/// <summary>
/// Abstraction for device product matching during BLE advertisement scanning.
/// Allows identification of supported Hanna devices by their advertisement patterns.
/// </summary>
public interface IHNAProductMatchingService
{
	/// <summary>
	/// Gets all registered product IDs that can be matched during scanning.
	/// </summary>
	IEnumerable<HNAProductId> RegisteredProducts { get; }

	/// <summary>
	/// Registers a new product with its associated BLE advertisement hex patterns.
	/// Allows dynamic product registration at runtime.
	/// </summary>
	/// <param name="productId">The product ID to register.</param>
	/// <param name="hexPatterns">Array of hex patterns that identify this product in BLE advertisements.</param>
	void Register(HNAProductId productId, string[] hexPatterns);

	/// <summary>
	/// Retrieves the hex patterns for a given product ID.
	/// </summary>
	/// <param name="productId">The product ID to look up.</param>
	/// <returns>Array of hex patterns if product is registered; null otherwise.</returns>
	string[]? GetHexPatterns(HNAProductId productId);
}
