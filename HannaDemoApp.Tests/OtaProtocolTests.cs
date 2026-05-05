using HannaDemoApp.Services.Ota;

namespace HannaDemoApp.Tests;

public class OtaProtocolTests
{
    [Theory]
    [InlineData("SF,123", "SF")]
    [InlineData("SP", "SP")]
    [InlineData(" SO  ", "SO")]
    public void ParseAckHead_ParsesExpectedHead(string line, string expected)
    {
        var head = HNAOtaProtocol.ParseAckHead(line);
        Assert.Equal(expected, head);
    }

    [Theory]
    [InlineData("SF,42", true)]
    [InlineData("SP", true)]
    [InlineData("XX,1", false)]
    [InlineData("", false)]
    public void IsOtaAck_ValidatesKnownAckSet(string line, bool expected)
    {
        Assert.Equal(expected, HNAOtaProtocol.IsOtaAck(line));
    }
}

