#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Project-local, opt-in commands for repeatable UI migration and visual validation.</summary>
[InitializeOnLoad]
public static class LandscapeEditorBridge
{
    const string CommandPath=".utmp/landscape-command.txt";
    const string OutputPath="Artifacts/LandscapeUI/editor-status.txt";
    static LandscapeEditorBridge() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(CommandPath)) return;
        string command=File.ReadAllText(CommandPath).Trim(); File.Delete(CommandPath);
        Directory.CreateDirectory("Artifacts/LandscapeUI");
        try
        {
            if (command=="inspect")
            {
                var lines=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(c=>c && c.gameObject.scene.IsValid()).Select(c=>c.GetType().Name+" @ "+PathOf(c.transform));
                File.WriteAllLines(OutputPath,lines); return;
            }
            if (command=="city-build") CityGameplayBuilder.Build();
            else if(command=="milestone-checks") MilestoneChecks.Run();
            else if(command=="service-checks") CityServiceChecks.Run();
            else if(command=="android-review-build")
            {
                if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before building.");
                Directory.CreateDirectory("Artifacts/Builds");
                File.WriteAllText("Artifacts/Builds/android-build.txt","BUILDING "+DateTime.Now.ToString("s"));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                    locationPathName="Artifacts/Builds/Hooligan-Milestones-1-2-Review.apk",target=BuildTarget.Android,options=BuildOptions.None
                });
                File.WriteAllText("Artifacts/Builds/android-build.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nSize: "+report.summary.totalSize+"\nDuration: "+report.summary.totalTime);
                if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Android build did not succeed.");
            }
            else if(command=="city-report") CityGameplayBuilder.Report();
            else if (command=="city-inspect")
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var rows = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true))
                    .Select(r=>PathOf(r.transform)+" | center="+r.bounds.center.ToString("F2")+" size="+r.bounds.size.ToString("F2")+" collider="+(r.GetComponent<Collider>()!=null));
                File.WriteAllLines("Artifacts/LandscapeUI/city-geometry.txt",rows);
            }
            else if (command=="build") LandscapeSceneBuilder.ApplyAll();
            else if (command.StartsWith("size:")) LandscapeSceneBuilder.SetGameViewSize(command.Substring(5));
            else if (command=="play") EditorApplication.isPlaying=true;
            else if (command=="stop") EditorApplication.isPlaying=false;
            else if (command.StartsWith("page:")) UnityEngine.Object.FindFirstObjectByType<LandscapeFrontEnd>()?.Navigate(command.Substring(5));
            else if (command.StartsWith("scene:"))
            {
                string name=command.Substring(6);
                if (name!="MainMenu" && name!="DashboardScene" && name!="GameScene" && name!="Gameplay") throw new InvalidOperationException("Unknown UI scene");
                if (EditorApplication.isPlaying) UnityEngine.SceneManagement.SceneManager.LoadScene(name);
                else EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
            }
            else if (command.StartsWith("capture:"))
            {
                string name=command.Substring(8);
                if (name.IndexOfAny(Path.GetInvalidFileNameChars())>=0) throw new InvalidOperationException("Invalid capture name");
                ScreenCapture.CaptureScreenshot("Artifacts/LandscapeUI/"+name+".png");
            }
            else if (command=="validate") LandscapeSceneBuilder.Validate();
            else if (command=="smoke") LandscapeSceneBuilder.SmokeTest();
            else if (command=="result") LandscapeSceneBuilder.PreviewResult();
            File.WriteAllText(OutputPath, DateTime.Now.ToString("s")+" OK "+command);
        }
        catch(Exception e) { File.WriteAllText(OutputPath,e.ToString()); Debug.LogException(e); }
    }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
}
#endif
