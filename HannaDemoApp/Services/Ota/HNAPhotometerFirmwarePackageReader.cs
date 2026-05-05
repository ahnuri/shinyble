using System.IO.Compression;

namespace HannaDemoApp.Services.Ota;

/// <summary>
/// Builds ordered firmware parts from ZIP / HEX / LNG, matching native <c>getFirmwareDowloadFiles</c>.
/// </summary>
public static class HNAPhotometerFirmwarePackageReader
{
    public static HNAOtaApiResult<List<HNAPhotometerFirmwarePart>> BuildParts(IReadOnlyList<string> paths)
    {
        var parts = new List<HNAPhotometerFirmwarePart>();

        if (paths.Count == 0)
        {
            return HNAOtaApiResult<List<HNAPhotometerFirmwarePart>>.Fail(
                HNAOtaErrorCode.PackageInvalid,
                "No firmware file was selected.");
        }

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return HNAOtaApiResult<List<HNAPhotometerFirmwarePart>>.Fail(
                    HNAOtaErrorCode.PackageInvalid,
                    "Firmware file not found.");
            }

            try
            {
                CollectFromPath(path, parts);
            }
            catch (Exception ex)
            {
                return HNAOtaApiResult<List<HNAPhotometerFirmwarePart>>.Fail(
                    HNAOtaErrorCode.PackageInvalid,
                    $"Firmware package parse failed: {ex.Message}");
            }
        }

        if (parts.Count == 0)
        {
            return HNAOtaApiResult<List<HNAPhotometerFirmwarePart>>.Fail(
                HNAOtaErrorCode.PackageInvalid,
                "No HEX/LNG firmware data found in the selected package.");
        }

        var total = parts.Sum(p => p.FileData.Length);
        if (total <= 0)
        {
            return HNAOtaApiResult<List<HNAPhotometerFirmwarePart>>.Fail(
                HNAOtaErrorCode.PackageInvalid,
                "Firmware package is empty.");
        }

        if (!parts.Any(p => p.FileType == "HEX"))
        {
            return HNAOtaApiResult<List<HNAPhotometerFirmwarePart>>.Fail(
                HNAOtaErrorCode.PackageInvalid,
                "Firmware package must include exactly one HEX file.");
        }

        if (total > 15 * 1024 * 1024)
        {
            return HNAOtaApiResult<List<HNAPhotometerFirmwarePart>>.Fail(
                HNAOtaErrorCode.PackageInvalid,
                "Firmware package is too large.");
        }

        parts.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
        return HNAOtaApiResult<List<HNAPhotometerFirmwarePart>>.Success(parts);
    }

    private static void CollectFromPath(string path, List<HNAPhotometerFirmwarePart> parts)
    {
        var ext = Path.GetExtension(path);
        if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            ExtractZipAndCollect(path, parts);
            return;
        }

        AddHexOrLngFile(new FileInfo(path), parts);
    }

    private static void ExtractZipAndCollect(string zipPath, List<HNAPhotometerFirmwarePart> parts)
    {
        var extractRoot = Path.Combine(Path.GetTempPath(), "hanna_fw_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractRoot);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, extractRoot, overwriteFiles: true);
            var tempFiles = new List<string>();
            foreach (var file in Directory.EnumerateFiles(extractRoot, "*", SearchOption.AllDirectories))
            {
                tempFiles.Add(file);
                var fi = new FileInfo(file);
                AddHexOrLngFile(fi, parts);
            }

            foreach (var f in tempFiles)
            {
                try
                {
                    File.Delete(f);
                }
                catch
                {
                    /* best effort */
                }
            }
        }
        finally
        {
            try
            {
                Directory.Delete(extractRoot, recursive: true);
            }
            catch
            {
                /* best effort */
            }
        }
    }

    private static void AddHexOrLngFile(FileInfo file, List<HNAPhotometerFirmwarePart> parts)
    {
        var fileName = file.Name;
        var upper = fileName.ToUpperInvariant();
        if (!upper.EndsWith(".HEX", StringComparison.Ordinal) && !upper.EndsWith(".LNG", StringComparison.Ordinal))
        {
            return;
        }

        var data = File.ReadAllBytes(file.FullName);

        if (upper.EndsWith(".LNG", StringComparison.Ordinal))
        {
            var sequence = LanguageSequenceFromPrefix(fileName);
            if (parts.Any(p => string.Equals(p.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            parts.Add(new HNAPhotometerFirmwarePart
            {
                FileType = "LNG",
                FileName = fileName,
                FileData = data,
                Sequence = sequence
            });
        }
        else if (upper.EndsWith(".HEX", StringComparison.Ordinal))
        {
            if (parts.Any(p => p.FileType == "HEX"))
            {
                return;
            }

            parts.Add(new HNAPhotometerFirmwarePart
            {
                FileType = "HEX",
                FileName = fileName,
                FileData = data,
                Sequence = 1
            });
        }
    }

    private static int LanguageSequenceFromPrefix(string fileName)
    {
        if (fileName.Length < 2)
        {
            return 2;
        }

        var two = fileName[..2].ToUpperInvariant();
        return two switch
        {
            "DE" => 2,
            "EN" => 3,
            "ES" => 4,
            "FR" => 5,
            "IT" => 6,
            "NL" => 7,
            "PT" => 8,
            "RO" => 9,
            "LT" => 10,
            "PL" => 11,
            "HU" => 12,
            "CZ" => 13,
            "SK" => 14,
            _ => 2
        };
    }
}
