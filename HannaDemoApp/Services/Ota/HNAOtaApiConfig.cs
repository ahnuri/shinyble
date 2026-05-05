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
        HNAOtaEnvironment.Prod => new Uri("https://www.hinstdev.com"),
        _ => new Uri("https://test.hinstdev.com")
    };

    public static Uri GraphqlUrl => new(BaseUrl, "api/graphql");
    public static Uri FirmwareDownloadUrl => new(BaseUrl, "api/getFirmwareFile");
}
