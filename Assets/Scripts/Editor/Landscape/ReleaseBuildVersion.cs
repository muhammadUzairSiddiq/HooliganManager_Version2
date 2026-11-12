#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class ReleaseBuildVersion : IPreprocessBuildWithReport
{
    // Isolated review builds reserve from the same sequence as the main editor.
    public const string LedgerVariable = "HM_RELEASE_VERSION_LEDGER";
    public const string LocalLedger = "UserSettings/HooliganReleaseVersions.txt";
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if ((report.summary.options & BuildOptions.Development) != 0)
            return;

        string ledger = Environment.GetEnvironmentVariable(LedgerVariable);
        if (string.IsNullOrEmpty(ledger)) ledger = LocalLedger;
        var (version, number) = Reserve(ledger, PlayerSettings.bundleVersion,
            PlayerSettings.Android.bundleVersionCode, PlayerSettings.iOS.buildNumber);
        PlayerSettings.bundleVersion = version;
        PlayerSettings.Android.bundleVersionCode = number;
        PlayerSettings.iOS.buildNumber = number.ToString(CultureInfo.InvariantCulture);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ReleaseBuildVersion] {version}, build {number}");
    }

    internal static (string version, int number) Reserve(string path, string bundleVersion, int androidCode, string iosBuild)
    {
        if (!Version.TryParse(bundleVersion, out var latestVersion) || latestVersion.Build < 0 || latestVersion.Revision >= 0)
            throw new BuildFailedException("Set the player version to major.minor.patch before making a release build.");
        if (!int.TryParse(iosBuild, NumberStyles.None, CultureInfo.InvariantCulture, out int iosCode))
            throw new BuildFailedException("Set the iOS build number to an integer before making a release build.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        // Keep the reservation and read under one file lock. A failed build
        // can leave a gap, but another build cannot reuse its reserved version.
        using var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var reader = new StreamReader(file, Encoding.UTF8, true, 1024, true);
        int latestCode = Math.Max(androidCode, iosCode);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            var fields = line.Split('|');
            if (fields.Length != 2 || !Version.TryParse(fields[0], out var version) ||
                version.Build < 0 || version.Revision >= 0 ||
                !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int code))
                continue;
            if (version > latestVersion) latestVersion = version;
            latestCode = Math.Max(latestCode, code);
        }

        var nextVersion = new Version(latestVersion.Major, latestVersion.Minor, checked(latestVersion.Build + 1)).ToString(3);
        int nextCode = checked(latestCode + 1);
        file.Seek(0, SeekOrigin.End);
        using var writer = new StreamWriter(file, new UTF8Encoding(false), 1024, true);
        writer.WriteLine();
        writer.WriteLine(nextVersion + "|" + nextCode.ToString(CultureInfo.InvariantCulture));
        writer.Flush();
        file.Flush(true);
        return (nextVersion, nextCode);
    }
}
#endif
