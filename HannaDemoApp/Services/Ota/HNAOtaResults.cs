namespace HannaDemoApp.Services.Ota;

public enum HNAOtaErrorCode
{
    None = 0,
    UnsupportedDevice,
    DeviceNotConnected,
    InternetUnavailable,
    FirmwareCheckFailed,
    FirmwareNotFound,
    DownloadFailed,
    ChecksumMismatch,
    PackageInvalid,
    OtaProtocolError,
    OtaTimeout,
    Canceled,
    Unknown
}

public readonly record struct HNAOtaResult(
    bool IsSuccess,
    HNAOtaErrorCode ErrorCode,
    string Message)
{
    public static HNAOtaResult Success(string message = "Firmware update completed.") =>
        new(true, HNAOtaErrorCode.None, message);

    public static HNAOtaResult Fail(HNAOtaErrorCode code, string message) =>
        new(false, code, message);
}

public readonly record struct HNAOtaApiResult<T>(
    bool IsSuccess,
    T? Value,
    HNAOtaErrorCode ErrorCode,
    string Message)
{
    public static HNAOtaApiResult<T> Success(T value) =>
        new(true, value, HNAOtaErrorCode.None, string.Empty);

    public static HNAOtaApiResult<T> Fail(HNAOtaErrorCode code, string message) =>
        new(false, default, code, message);
}

