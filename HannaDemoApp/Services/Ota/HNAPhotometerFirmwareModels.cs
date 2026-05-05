namespace HannaDemoApp.Services.Ota;

/// <summary>Metadata returned from <c>checkFirmwareFile</c> GraphQL.</summary>
public sealed class HNAPhotometerCloudFirmwareInfo
{
    public string FileName { get; init; } = string.Empty;
    public string FileSize { get; init; } = string.Empty;
    public string FileVersion { get; init; } = string.Empty;
    public string KeyFeatures { get; init; } = string.Empty;
    public string FileKey { get; init; } = string.Empty;
    public string HashKey { get; init; } = string.Empty;
    public string TimeStamp { get; init; } = string.Empty;
}

/// <summary>One logical firmware payload (HEX or LNG) in OTA transfer order.</summary>
public sealed class HNAPhotometerFirmwarePart
{
    public string FileType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public byte[] FileData { get; init; } = [];
    public int Sequence { get; init; }
}
