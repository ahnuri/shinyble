using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HannaDemoApp.Models;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace HannaDemoApp.Services.Ota;

public sealed class HNAPhotometerOtaApiService : IHNAPhotometerOtaApiService
{
    private const int MaxRetryAttempts = 3;

    private const string CheckFirmwareQuery =
        """
        query CheckFirmwareFile($model: String!, $FWVersion: String!, $BLVersion: String!, $source: String!, $hash: String!) {
          checkFirmwareFile(
            model: $model
            FWVersion: $FWVersion
            BLVersion: $BLVersion
            source: $source
            hash: $hash
          ) {
            fileName
            fileSize
            fileVersion
            keyFeatures
            fileKey
            hashKey
            timeStamp
          }
        }
        """;

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return c;
    }

    /// <inheritdoc />
    public async Task<HNAOtaApiResult<HNAPhotometerCloudFirmwareInfo>> CheckFirmwareAsync(
        HNABleDeviceModel device,
        CancellationToken cancellationToken = default)
    {
        var model = device.MeterModel?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(model) || !HNAOtaCapabilities.TryGet(model, out var capability) || !capability.SupportsOta)
        {
            return HNAOtaApiResult<HNAPhotometerCloudFirmwareInfo>.Fail(
                HNAOtaErrorCode.UnsupportedDevice,
                $"OTA is not supported for model '{model}'.");
        }

        var hash = Sha256HexUtf8(capability.ProductIdHexUpper);
        var fw = device.MeterFirmwareVersion?.Trim() ?? string.Empty;
        var bl = device.BleFirmwareVersion?.Trim() ?? string.Empty;
        var source = DeviceInfo.Platform == DevicePlatform.Android ? "android" : "ios";

        var url = HNAOtaApiConfig.OtaCheckUrl;
        var body = new JsonObject
        {
            ["query"] = CheckFirmwareQuery,
            ["variables"] = new JsonObject
            {
                ["model"] = model,
                ["FWVersion"] = fw,
                ["BLVersion"] = bl,
                ["source"] = source,
                ["hash"] = hash
            }
        };

        string json;
        try
        {
            using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            json = await PostWithRetryAsync(url, content, source, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OtaDebug($"[OTA][CheckFirmware] Request failed: {ex.Message}");
            return HNAOtaApiResult<HNAPhotometerCloudFirmwareInfo>.Fail(
                HNAOtaErrorCode.FirmwareCheckFailed,
                $"Firmware check failed: {ex.Message}");
        }

        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            OtaDebug("[OTA][CheckFirmware] GraphQL errors were returned.");
            return HNAOtaApiResult<HNAPhotometerCloudFirmwareInfo>.Fail(
                HNAOtaErrorCode.FirmwareCheckFailed,
                "Cloud returned GraphQL errors while checking firmware.");
        }

        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("checkFirmwareFile", out var cf) ||
            cf.ValueKind != JsonValueKind.Object)
        {
            OtaDebug("[OTA][CheckFirmware] data.checkFirmwareFile missing/null.");
            return HNAOtaApiResult<HNAPhotometerCloudFirmwareInfo>.Fail(
                HNAOtaErrorCode.FirmwareNotFound,
                "No firmware data returned from server.");
        }

        static string? ReadString(JsonElement el, string name) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        var info = new HNAPhotometerCloudFirmwareInfo
        {
            FileName = ReadString(cf, "fileName") ?? string.Empty,
            FileSize = ReadString(cf, "fileSize") ?? string.Empty,
            FileVersion = ReadString(cf, "fileVersion") ?? string.Empty,
            KeyFeatures = ReadString(cf, "keyFeatures") ?? string.Empty,
            FileKey = ReadString(cf, "fileKey") ?? string.Empty,
            HashKey = ReadString(cf, "hashKey") ?? string.Empty,
            TimeStamp = ReadString(cf, "timeStamp") ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(info.FileName))
        {
            OtaDebug("[OTA][CheckFirmware] checkFirmwareFile returned object but fileName is empty.");
            return HNAOtaApiResult<HNAPhotometerCloudFirmwareInfo>.Fail(
                HNAOtaErrorCode.FirmwareNotFound,
                "No downloadable firmware was returned.");
        }

        return HNAOtaApiResult<HNAPhotometerCloudFirmwareInfo>.Success(info);
    }

    /// <inheritdoc />
    public async Task<HNAOtaApiResult<string>> DownloadFirmwareAsync(
        HNABleDeviceModel device,
        HNAPhotometerCloudFirmwareInfo info,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var model = device.MeterModel?.Trim() ?? string.Empty;
        var fw = info.FileVersion?.Trim() ?? string.Empty;
        var bl = string.IsNullOrWhiteSpace(device.BleFirmwareVersion)
            ? fw
            : device.BleFirmwareVersion.Trim();
        var hashForBody = Sha256HexUtf8(info.HashKey);
        var source = DeviceInfo.Platform == DevicePlatform.Android ? "android" : "ios";

        var payload = new
        {
            fileName = info.FileName,
            model,
            timeStamp = info.TimeStamp,
            hashKey = hashForBody,
            FWVersion = fw,
            BLVersion = bl,
            source
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await PostResponseWithRetryAsync(HNAOtaApiConfig.FirmwareDownloadUrl, content, source, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode || response.Content == null)
        {
            return HNAOtaApiResult<string>.Fail(
                HNAOtaErrorCode.DownloadFailed,
                $"Firmware download failed ({(int)response.StatusCode}).");
        }

        await using var fs = File.Create(destinationPath);
        await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        return HNAOtaApiResult<string>.Success(destinationPath);
    }

    private static async Task<string> PostWithRetryAsync(Uri url, HttpContent content, string source, CancellationToken cancellationToken)
    {
        using var response = await PostResponseWithRetryAsync(url, content, source, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PostResponseWithRetryAsync(Uri url, HttpContent content, string source, CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Recreate content for retry since HttpContent is single-use.
                var payload = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                using var c = new ByteArrayContent(payload);
                foreach (var header in content.Headers)
                {
                    c.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = c
                };

                var appVersion = HNAOtaApiConfig.BuildAppVersionHeader(source);
                request.Headers.TryAddWithoutValidation(HNAOtaApiConfig.AppVersionHeaderName, appVersion);
                // Keep compatibility with backends expecting snake_case header.
                request.Headers.TryAddWithoutValidation("app_version", appVersion);

                var accessToken = GetAccessToken();
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
                }

                var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode >= 500 && attempt < MaxRetryAttempts)
                {
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return response;
            }
            catch (Exception ex) when (attempt < MaxRetryAttempts && ex is HttpRequestException or TaskCanceledException)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                last = ex;
                break;
            }
        }

        throw last ?? new HttpRequestException("Request failed.");
    }

    public static string Sha256HexUtf8(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256HexOfFile(string path)
    {
        using var fs = File.OpenRead(path);
        var hash = SHA256.HashData(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetAccessToken()
    {
        var keys = new[] { "access_token", "accessToken", "token", "jwt" };
        foreach (var key in keys)
        {
            var value = Preferences.Default.Get(key, string.Empty);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void OtaDebug(string message) =>
        System.Diagnostics.Debug.WriteLine(message);
}
