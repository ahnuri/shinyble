using HannaDemoApp.Models;
using HannaDemoApp.Services.Ble;
using Shiny.BluetoothLE;

namespace HannaDemoApp.Services.Ota;

public sealed class HNAPhotometerOtaBleTransport(IHNABleService bleService) : IPhotometerOtaBleTransport
{
    private readonly IHNABleService _bleService = bleService;

    public IDisposable SubscribeAcks(HNABleDeviceModel device, Action<string> onLine) =>
        _bleService.SubscribePhotometerOtaResponses(device, onLine);

    public Task SendCommandAsync(HNABleDeviceModel device, string command, CancellationToken cancellationToken) =>
        _bleService.SendPhotometerOtaCommandAsync(device, command, cancellationToken);

    public Task SendPayloadAsync(HNABleDeviceModel device, byte[] payload, CancellationToken cancellationToken) =>
        _bleService.WritePhotometerOtaWithoutResponseAsync(device, payload, cancellationToken);

    public async Task TryRequestMtuAsync(HNABleDeviceModel device, int mtu)
    {
        if (device.Device is ICanRequestMtu requestor)
        {
            try
            {
                await requestor.TryRequestMtuAsync(mtu).ConfigureAwait(false);
                await Task.Delay(150).ConfigureAwait(false);
            }
            catch
            {
                /* optional best-effort */
            }
        }
    }

    public int GetMtuOrDefault(HNABleDeviceModel device, int fallbackMtu)
    {
        if (device.Device is IPeripheral peripheral)
        {
            return Math.Max(23, peripheral.Mtu);
        }

        return fallbackMtu;
    }
}

