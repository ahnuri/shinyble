namespace HannaDemoApp.Services.Ota;

public static class HNAOtaProtocol
{
    public static class Commands
    {
        public const string BootloaderMode = "set PICh2N6a";
        public const string FastConnection = "set conn fast";
        public const string BlockPic = "set mode boot";
        public const string EnablePic = "set mode app";
        public const string RestartPic = "set mode app,start";
        public const string SetFilePrefix = "set file,";
        public const string SendFilePrefix = "SF,";
    }

    public static class Acks
    {
        public const string Sc = "SC";
        public const string So = "SO";
        public const string Sf = "SF";
        public const string Sp = "SP";
        public const string Sv = "SV";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            Sc, So, Sf, Sp, Sv
        };
    }

    public static string ParseAckHead(string responseLine)
    {
        if (string.IsNullOrWhiteSpace(responseLine))
        {
            return string.Empty;
        }

        var line = responseLine.Trim();
        var comma = line.IndexOf(',');
        return (comma >= 0 ? line[..comma] : line).Trim();
    }

    public static bool IsOtaAck(string responseLine) =>
        Acks.All.Contains(ParseAckHead(responseLine));
}

