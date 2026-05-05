namespace HannaDemoApp.Services.Ota;

/// <summary>Version compare logic ported from native <c>OTAViewModel.compareFWVersions</c>.</summary>
public static class HNAPhotometerFirmwareVersionComparer
{
    public static int Compare(string meterVersion, string cloudVersion)
    {
        var mVer = meterVersion.ToLowerInvariant().Replace("v", "", StringComparison.Ordinal);
        var cVer = cloudVersion.ToLowerInvariant().Replace("v", "", StringComparison.Ordinal);

        var mParts = mVer.Split('b', 2, StringSplitOptions.None);
        var cParts = cVer.Split('b', 2, StringSplitOptions.None);

        if (mParts.Length == 0 || cParts.Length == 0)
        {
            return 0;
        }

        var mMain = mParts[0];
        var cMain = cParts[0];

        var mMainArr = mMain.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var cMainArr = cMain.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();

        var maxCount = Math.Max(mMainArr.Length, cMainArr.Length);
        for (var i = 0; i < maxCount; i++)
        {
            var mVal = i < mMainArr.Length ? mMainArr[i] : 0;
            var cVal = i < cMainArr.Length ? cMainArr[i] : 0;
            if (mVal < cVal)
            {
                return -1;
            }

            if (mVal > cVal)
            {
                return 1;
            }
        }

        var mHasBeta = mParts.Length > 1;
        var cHasBeta = cParts.Length > 1;

        if (mHasBeta && cHasBeta &&
            int.TryParse(mParts[1], out var mBeta) &&
            int.TryParse(cParts[1], out var cBeta))
        {
            if (mBeta < cBeta)
            {
                return -1;
            }

            if (mBeta > cBeta)
            {
                return 1;
            }

            return 0;
        }

        if (!mHasBeta && cHasBeta)
        {
            return -1;
        }

        return 0;
    }

    public static bool IsCloudNewer(string meterVersion, string cloudVersion) =>
        Compare(meterVersion, cloudVersion) < 0;
}
