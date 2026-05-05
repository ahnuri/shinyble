using HannaDemoApp.Services.Ota;

namespace HannaDemoApp.Tests;

public class FirmwareVersionComparerTests
{
    [Theory]
    [InlineData("V1.0.0", "1.0.1")]
    [InlineData("1.2", "1.2.1")]
    [InlineData("1.2b1", "1.2b2")]
    public void IsCloudNewer_ReturnsTrue_WhenCloudHigher(string meter, string cloud)
    {
        Assert.True(HNAPhotometerFirmwareVersionComparer.IsCloudNewer(meter, cloud));
    }

    [Theory]
    [InlineData("1.2.1", "1.2.0")]
    [InlineData("1.2b3", "1.2b2")]
    [InlineData("1.2", "1.2")]
    public void IsCloudNewer_ReturnsFalse_WhenCloudNotHigher(string meter, string cloud)
    {
        Assert.False(HNAPhotometerFirmwareVersionComparer.IsCloudNewer(meter, cloud));
    }
}

