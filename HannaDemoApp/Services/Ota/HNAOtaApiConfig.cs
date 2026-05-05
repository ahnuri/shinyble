using System.Text;

namespace HannaDemoApp.Services.Ota;

/// <summary>Backend environment for Hanna OTA (GraphQL check + firmware download). Match native HI app settings.</summary>
public enum HNAOtaEnvironment
{
    Dev,
    Test,
    Prod
}

/// <summary>
/// Base URLs and paths aligned with native iOS <c>HNAAPIConfig</c>.
/// IMPORTANT: Use <see cref="HNAOtaEnvironment.Test"/> for App Store–aligned testing per native app guidance.
/// </summary>
public static class HNAOtaApiConfig
{
    /// <summary>Active environment; defaults to Test.</summary>
    public static HNAOtaEnvironment Environment { get; set; } = HNAOtaEnvironment.Test;

    public static Uri BaseUrl => Environment switch
    {
        HNAOtaEnvironment.Dev => new Uri("https://www.hinstdev.com"),
        HNAOtaEnvironment.Test => new Uri("https://test.hinstdev.com"),
        HNAOtaEnvironment.Prod => new Uri("https://hannacloud.com"),
        _ => new Uri("https://test.hinstdev.com")
    };

    /// <summary>
    /// Native iOS OTA check endpoint is /api/checkFirmware (not /api/graphql).
    /// </summary>
    public static Uri OtaCheckUrl => new(BaseUrl, "api/checkFirmware");
    public static Uri FirmwareDownloadUrl => new(BaseUrl, "api/getFirmwareFile");

    /// <summary>
    /// Native app uses this in appVersion header payload.
    /// Replace with production key when required by backend.
    /// </summary>
    public static string RandomConfigStr { get; set; } = "7b0ca88469383b0449f6b73b7ab8d8a8";

    /// <summary>
    /// Mirrors iOS appVersion header format: "{source}4.1_{randomConfigStr}_{unixTs}" base64.
    /// </summary>
    public static string BuildAppVersionHeader(string source)
    {
        var unixTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var raw = $"{source}4.1_{RandomConfigStr}_{unixTs}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public const string AppVersionHeaderName = "appVersion";
}
