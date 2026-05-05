using HannaDemoApp.Services.Ota;

namespace HannaDemoApp.Tests;

public class FirmwarePackageReaderTests
{
    [Fact]
    public void BuildParts_ReturnsInvalid_WhenNoHexExists()
    {
        var root = CreateTempDir();
        try
        {
            File.WriteAllBytes(Path.Combine(root, "EN.LNG"), [1, 2, 3]);

            var result = HNAPhotometerFirmwarePackageReader.BuildParts([Path.Combine(root, "EN.LNG")]);

            Assert.False(result.IsSuccess);
            Assert.Equal(HNAOtaErrorCode.PackageInvalid, result.ErrorCode);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildParts_SortsHexBeforeLanguageBySequence()
    {
        var root = CreateTempDir();
        try
        {
            var hex = Path.Combine(root, "FW.HEX");
            var en = Path.Combine(root, "EN.LNG");
            var fr = Path.Combine(root, "FR.LNG");
            File.WriteAllBytes(hex, [0x01, 0x02]);
            File.WriteAllBytes(en, [0x11, 0x12]);
            File.WriteAllBytes(fr, [0x21, 0x22]);

            var result = HNAPhotometerFirmwarePackageReader.BuildParts([hex, fr, en]);

            Assert.True(result.IsSuccess, result.Message);
            Assert.NotNull(result.Value);
            var parts = result.Value!;
            Assert.Equal("HEX", parts[0].FileType);
            Assert.Equal(1, parts[0].Sequence);
            Assert.Equal("EN.LNG", parts[1].FileName);
            Assert.Equal("FR.LNG", parts[2].FileName);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDir()
    {
        var root = Path.Combine(Path.GetTempPath(), "hanna_ota_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}

