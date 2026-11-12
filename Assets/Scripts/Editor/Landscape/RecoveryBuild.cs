#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class RecoveryBuild
{
    public static void AndroidClient()
    {
        BuildAndroid("Hooligan-Client.apk", "client-android-build.txt");
    }
    public static void Android()
    {
        BuildAndroid("Hooligan-RTS-Recovery-Review.apk", "android-build.txt");
    }
    public static void AndroidTrackingPolish()
    {
        BuildAndroid("Hooligan-RTS-Tracking-Polish-Review.apk", "tracking-android-build.txt");
    }
    static void BuildAndroid(string packageName, string reportName)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before building.");
        Directory.CreateDirectory("Artifacts/Builds");
        bool isolated=!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ReleaseBuildVersion.LedgerVariable));
        var original=(version:PlayerSettings.bundleVersion, code:PlayerSettings.Android.bundleVersionCode, ios:PlayerSettings.iOS.buildNumber);
        try
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName="Artifacts/Builds/"+packageName,
                target=BuildTarget.Android,options=BuildOptions.CompressWithLz4HC});
            Directory.CreateDirectory("Artifacts/Recovery");
            File.WriteAllText("Artifacts/Recovery/"+reportName,$"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\nUTC: {DateTime.UtcNow:O}\nVersion: {PlayerSettings.bundleVersion}\nAndroid version code: {PlayerSettings.Android.bundleVersionCode}\nPhysical device validation: not performed");
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Recovery Android build failed; inspect the build report and log.");
        }
        finally
        {
            if(isolated)
            {
                PlayerSettings.bundleVersion=original.version;
                PlayerSettings.Android.bundleVersionCode=original.code;
                PlayerSettings.iOS.buildNumber=original.ios;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
