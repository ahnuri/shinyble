using HannaDemoApp.Models;

namespace HannaDemoApp.Services.Ota;

public interface IPhotometerOtaBleTransport
{
    IDisposable SubscribeAcks(HNABleDeviceModel device, Action<string> onLine);
    Task SendCommandAsync(HNABleDeviceModel device, string command, CancellationToken cancellationToken);
    Task SendPayloadAsync(HNABleDeviceModel device, byte[] payload, CancellationToken cancellationToken);
    Task TryRequestMtuAsync(HNABleDeviceModel device, int mtu);
    int GetMtuOrDefault(HNABleDeviceModel device, int fallbackMtu);
}

