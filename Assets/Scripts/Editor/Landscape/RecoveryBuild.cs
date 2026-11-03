#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class RecoveryBuild
{
    public static void Android()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before building.");
        Directory.CreateDirectory("Artifacts/Builds");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName="Artifacts/Builds/Hooligan-RTS-Recovery-Review.apk",
            target=BuildTarget.Android,options=BuildOptions.CompressWithLz4HC});
        File.WriteAllText("Artifacts/Recovery/android-build.txt",$"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\nUTC: {DateTime.UtcNow:O}\nPhysical device validation: not performed");
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Recovery Android build failed; inspect the build report and log.");
    }
}
#endif
