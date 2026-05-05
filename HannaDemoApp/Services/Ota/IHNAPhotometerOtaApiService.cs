using HannaDemoApp.Models;

namespace HannaDemoApp.Services.Ota;

public interface IHNAPhotometerOtaApiService
{
    /// <summary>GraphQL <c>checkFirmwareFile</c> with structured error result.</summary>
    Task<HNAOtaApiResult<HNAPhotometerCloudFirmwareInfo>> CheckFirmwareAsync(
        HNABleDeviceModel device,
        CancellationToken cancellationToken = default);

    /// <summary>POST <c>getFirmwareFile</c>; writes payload to <paramref name="destinationPath"/> with structured result.</summary>
    Task<HNAOtaApiResult<string>> DownloadFirmwareAsync(
        HNABleDeviceModel device,
        HNAPhotometerCloudFirmwareInfo info,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
