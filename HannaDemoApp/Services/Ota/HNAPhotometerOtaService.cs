using HannaDemoApp.Models;
using Microsoft.Maui.Networking;

namespace HannaDemoApp.Services.Ota;

/// <summary>High-level photometer OTA: cloud check/download or local package + BLE transfer.</summary>
public sealed class HNAPhotometerOtaService(
    IHNAPhotometerOtaApiService otaApi,
    HNAPhotometerOtaCoordinator coordinator)
{
    private readonly IHNAPhotometerOtaApiService _otaApi = otaApi;
    private readonly HNAPhotometerOtaCoordinator _coordinator = coordinator;

    public Task<HNAOtaResult> RunFromCloudAsync(
        HNABleDeviceModel device,
        IProgress<HNAOtaUiProgress>? uiProgress = null,
        CancellationToken cancellationToken = default) =>
        RunFromCloudCoreAsync(device, uiProgress, cancellationToken);

    public Task<HNAOtaResult> RunFromLocalPathAsync(
        HNABleDeviceModel device,
        string filePath,
        IProgress<HNAOtaUiProgress>? uiProgress = null,
        CancellationToken cancellationToken = default) =>
        RunFromLocalCoreAsync(device, filePath, uiProgress, cancellationToken);

    private async Task<HNAOtaResult> RunFromCloudCoreAsync(
        HNABleDeviceModel device,
        IProgress<HNAOtaUiProgress>? uiProgress,
        CancellationToken cancellationToken)
    {
        if (!device.IsConnected)
        {
            return HNAOtaResult.Fail(HNAOtaErrorCode.DeviceNotConnected, "Connect the photometer first.");
        }

        var model = device.MeterModel?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(model) || !HNAOtaCapabilities.TryGet(model, out var cap) || !cap.SupportsOta)
        {
            return HNAOtaResult.Fail(HNAOtaErrorCode.UnsupportedDevice, $"OTA is not supported for model '{model}'.");
        }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return HNAOtaResult.Fail(
                HNAOtaErrorCode.InternetUnavailable,
                "Internet access is required to check and download firmware.");
        }

        var checkResult = await _otaApi.CheckFirmwareAsync(device, cancellationToken).ConfigureAwait(false);
        if (!checkResult.IsSuccess || checkResult.Value == null)
        {
            return HNAOtaResult.Fail(
                checkResult.ErrorCode == HNAOtaErrorCode.None ? HNAOtaErrorCode.FirmwareCheckFailed : checkResult.ErrorCode,
                string.IsNullOrWhiteSpace(checkResult.Message)
                    ? "No firmware information was returned from the server."
                    : checkResult.Message);
        }
        var cloud = checkResult.Value;

        if (!HNAPhotometerFirmwareVersionComparer.IsCloudNewer(device.MeterFirmwareVersion, cloud.FileVersion))
        {
            return HNAOtaResult.Fail(
                HNAOtaErrorCode.FirmwareNotFound,
                $"Firmware {device.MeterFirmwareVersion} is already up to date or newer than cloud {cloud.FileVersion}.");
        }

        var tempPath = Path.Combine(FileSystem.CacheDirectory, cloud.FileName);
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            var downloadResult = await _otaApi.DownloadFirmwareAsync(device, cloud, tempPath, cancellationToken).ConfigureAwait(false);
            if (!downloadResult.IsSuccess)
            {
                return HNAOtaResult.Fail(
                    downloadResult.ErrorCode == HNAOtaErrorCode.None ? HNAOtaErrorCode.DownloadFailed : downloadResult.ErrorCode,
                    string.IsNullOrWhiteSpace(downloadResult.Message) ? "Firmware download failed." : downloadResult.Message);
            }

            var hash = HNAPhotometerOtaApiService.Sha256HexOfFile(tempPath);
            if (!string.Equals(hash, cloud.FileKey, StringComparison.OrdinalIgnoreCase))
            {
                return HNAOtaResult.Fail(
                    HNAOtaErrorCode.ChecksumMismatch,
                    "Downloaded file checksum does not match the server.");
            }

            var packageResult = HNAPhotometerFirmwarePackageReader.BuildParts([tempPath]);
            if (!packageResult.IsSuccess || packageResult.Value == null)
            {
                return HNAOtaResult.Fail(
                    packageResult.ErrorCode == HNAOtaErrorCode.None ? HNAOtaErrorCode.PackageInvalid : packageResult.ErrorCode,
                    string.IsNullOrWhiteSpace(packageResult.Message) ? "Could not read firmware package." : packageResult.Message);
            }

            return await RunBleTransferAsync(device, packageResult.Value, uiProgress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                /* ignore */
            }
        }
    }

    private async Task<HNAOtaResult> RunFromLocalCoreAsync(
        HNABleDeviceModel device,
        string filePath,
        IProgress<HNAOtaUiProgress>? uiProgress,
        CancellationToken cancellationToken)
    {
        if (!device.IsConnected)
        {
            return HNAOtaResult.Fail(HNAOtaErrorCode.DeviceNotConnected, "Connect the photometer first.");
        }

        var model = device.MeterModel?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(model) || !HNAOtaCapabilities.TryGet(model, out var cap) || !cap.SupportsOta)
        {
            return HNAOtaResult.Fail(HNAOtaErrorCode.UnsupportedDevice, $"OTA is not supported for model '{model}'.");
        }

        var packageResult = HNAPhotometerFirmwarePackageReader.BuildParts([filePath]);
        if (!packageResult.IsSuccess || packageResult.Value == null)
        {
            return HNAOtaResult.Fail(
                packageResult.ErrorCode == HNAOtaErrorCode.None ? HNAOtaErrorCode.PackageInvalid : packageResult.ErrorCode,
                string.IsNullOrWhiteSpace(packageResult.Message) ? "Could not read firmware package." : packageResult.Message);
        }

        return await RunBleTransferAsync(device, packageResult.Value, uiProgress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HNAOtaResult> RunBleTransferAsync(
        HNABleDeviceModel device,
        List<HNAPhotometerFirmwarePart> parts,
        IProgress<HNAOtaUiProgress>? uiProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            await _coordinator.RunTransferAsync(device, parts, uiProgress, cancellationToken).ConfigureAwait(false);
            return HNAOtaResult.Success("Update completed. The photometer may reconnect.");
        }
        catch (OperationCanceledException)
        {
            return HNAOtaResult.Fail(HNAOtaErrorCode.Canceled, "Firmware update was canceled.");
        }
        catch (TimeoutException ex)
        {
            return HNAOtaResult.Fail(HNAOtaErrorCode.OtaTimeout, ex.Message);
        }
        catch (Exception ex)
        {
            return HNAOtaResult.Fail(HNAOtaErrorCode.OtaProtocolError, ex.Message);
        }
    }
}
