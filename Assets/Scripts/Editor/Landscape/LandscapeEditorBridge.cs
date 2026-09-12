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
            if (command=="build") LandscapeSceneBuilder.ApplyAll();
            else if (command.StartsWith("size:")) LandscapeSceneBuilder.SetGameViewSize(command.Substring(5));
            else if (command=="play") EditorApplication.isPlaying=true;
            else if (command=="stop") EditorApplication.isPlaying=false;
            else if (command.StartsWith("page:")) UnityEngine.Object.FindFirstObjectByType<LandscapeFrontEnd>()?.Navigate(command.Substring(5));
            else if (command.StartsWith("scene:"))
            {
                string name=command.Substring(6);
                if (name!="MainMenu" && name!="DashboardScene" && name!="GameScene") throw new InvalidOperationException("Unknown UI scene");
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
