using System.Collections.ObjectModel;
using HannaDemoApp.Core.Enums;
using HannaDemoApp.Models;

namespace HannaDemoApp.Services.Ble;

// ============= IHNABleService — Public BLE API (used by ViewModels / UI) ===========================
// 
// This interface abstracts all BLE operations, So UI layer does NOT depend on Shiny BLE Library.
//  This is similar to iOS and acts like a wrapper around: CBCentralManager, CBPeripheral and Delegates
//
// Responsibilities include:
//   - Scan BLE devices
//   - Connect / Disconnect
//   - Send commands
//   - Maintain device lists
//   - Resume after background
// =====================================================================================


public interface IHNABleService : IDisposable
{
    ObservableCollection<HNABleDeviceModel> AvailableDevices { get; } // Devices discovered during scanning
    ObservableCollection<HNABleDeviceModel> ConnectedDevices { get; } // Devices currently connected
    bool IsScanning { get; } // Indicates if a BLE scan is in progress
    string StatusText { get; } // Status message for UI display/Debug (e.g., "Scanning...", "Connected to XYZ", etc.)

    // ===================== SCANNING ==============================================================
    Task ScanAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default); // Start generic scan like iOS CBCentralManager.ScanForPeripherals

    // Start a scan for devices matching a specific product ID (e.g., "Hanna Product ID"). This is a higher-level API that abstracts filtering logic.
    Task ScanForProductAsync(HNAProductId productId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    void StopScan(); // Stop scan manually



    // ===================== CONNECTION ==================================================
    Task ConnectAsync(HNABleDeviceModel device, CancellationToken cancellationToken = default); // Connect to device
    Task DisconnectAsync(HNABleDeviceModel device, CancellationToken cancellationToken = default); // Disconnect from device



    // ===================== COMMANDS =====================================================
    Task SendCommandAsync(HNABleDeviceModel device, string command); // Send raw command to device

    /// <summary>
    /// Receives photometer firmware OTA acknowledgements (SC/SO/SF/SP/SV) on the BLE notification thread.
    /// Only one subscriber per device; disposing clears the handler.
    /// </summary>
    IDisposable SubscribePhotometerOtaResponses(HNABleDeviceModel device, Action<string> onPhotometerOtaLine);

    /// <summary>Writes a raw payload to the UART TX characteristic without response (used for SF, chunk data).</summary>
    Task WritePhotometerOtaWithoutResponseAsync(HNABleDeviceModel device, byte[] payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a text command during photometer OTA using write-without-response (matches native <c>sendCMD</c>).
    /// </summary>
    Task SendPhotometerOtaCommandAsync(HNABleDeviceModel device, string command, CancellationToken cancellationToken = default);

    // ===================== BACKGROUND RESUME =====================================================
    Task ResumeLiveUpdatesAsync(CancellationToken cancellationToken = default); // Resume streaming after app foreground

    /// <summary>
    /// Flushes Halo DB batches from the off-UI persistence buffer. Safe to call from any thread (e.g. Android <c>OnPause</c>).
    /// </summary>
    void DrainHaloPersistenceBatches();

    // ===================== EVENTS & PROPERTY CHANGES =====================================================
    event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;   // Required for ObservableObject binding
}
