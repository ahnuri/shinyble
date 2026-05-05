namespace HannaDemoApp.Services.Ota;

public sealed class HNAOtaOptions
{
    public static HNAOtaOptions Default { get; } = new();

    public int InterChunkDelayMs { get; init; } = 8;
    public int MinDataChunkSize { get; init; } = 14;
    public int MaxDataChunkSize { get; init; } = 241;
    public int DefaultDataChunkSize { get; init; } = 178;

    public TimeSpan EnterBootloaderTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan FastConnectionTimeout { get; init; } = TimeSpan.FromSeconds(45);
    public TimeSpan BlockPicTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan SetFileAckTimeout { get; init; } = TimeSpan.FromSeconds(120);
    public TimeSpan ChunkAckTimeout { get; init; } = TimeSpan.FromSeconds(300);
    public TimeSpan EnablePicTimeout { get; init; } = TimeSpan.FromSeconds(60);

    public int MtuRequestValue { get; init; } = 247;
}

