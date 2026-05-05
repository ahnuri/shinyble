namespace HannaDemoApp.Core.Enums;

// Identifies supported Hanna product families matched during BLE scans.
// Add new entries here when supporting additional devices.
public enum HNAProductId
{
    HI9810,   // Halo
    HI98494,  // MultiMeter
    HI97115,  // Photometer
    HI98594,  // MultiMeter
    HI97105   // Photometer (same BLE family as HI97115; resolved from meter model after info)
}
