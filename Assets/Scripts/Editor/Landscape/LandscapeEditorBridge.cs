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
            if (command=="enhancement-configure") EnhancementChecks.Configure();
            else if (command=="enhancement-play") EnhancementPlaytest.Begin();
            else if (command=="enhancement-checks") EnhancementChecks.Run();
            else if (command=="enhancement-build") EnhancementChecks.Build();
            else if (command=="refresh") AssetDatabase.Refresh();
            else if (command=="refresh-theme") { AssetDatabase.Refresh(); LandscapeThemeImporter.Import(); }
            else if (command=="city-build") CityGameplayBuilder.Build();
            else if(command=="milestone-checks") MilestoneChecks.Run();
            else if(command=="rts-checks") CityRtsChecks.Run();
            else if(command=="shops-preview")
            {
                var point=CityGameplay.Instance.Locations[2];CameraPanTouchOnly.Instance?.FocusOn(point);
                var replacement=CityLandmarks.ClearNearby(point);
                File.WriteAllText("Artifacts/CityQA/shops-placement.txt","Current "+point+" clear="+CityLandmarks.HasTacticalClearance(point)+" nearest clear="+replacement+" clear="+CityLandmarks.HasTacticalClearance(replacement));
                File.WriteAllLines("Artifacts/CityQA/shops-obstacles.txt",UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r&&r.bounds.SqrDistance(point)<10000&&!r.GetComponentInParent<Canvas>()).Select(r=>r.name+" "+r.bounds+" collider="+(r.GetComponent<Collider>()!=null)));
            }
            else if(command=="character-audit") CharacterProductionAudit.Run();
            else if(command=="character-build") CharacterProductionBuilder.Build();
            else if(command=="character-animations") { CharacterProductionBuilder.BuildAnimations(); AssetDatabase.SaveAssets(); }
            else if(command=="character-preview") CharacterProductionAudit.Preview();
            else if(command=="character-gesture-preview") CharacterProductionAudit.Preview(true);
            else if(command=="character-checks") CharacterProductionChecks.Run();
            else if(command=="clothing-topology") ClothingTopologyAudit.Run();
            else if(command=="rts-streaming-probe") CityRtsChecks.BeginStreamingProbe();
            else if(command=="development-preview") CityDevelopmentSystem.Instance?.OpenBoard();
            else if(command=="milestone4-playtest")
            {
                if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter play mode first.");
                var actions=UnityEngine.Object.FindFirstObjectByType<CityActionSystem>();
                if(!actions)throw new InvalidOperationException("City action system is unavailable.");
                actions.BeginAutomatedPlaytest("Artifacts/CityQA/milestone4-interactive-playtest.txt");
            }
            else if(command=="npc-preview")
            {
                if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter play mode first.");
                var agent=BattleManager.instance?.PlayerAgents.FirstOrDefault(a=>a&&a.IsAlive);
                var npc=UnityEngine.Object.FindObjectsByType<SocialNpc>(FindObjectsSortMode.None).FirstOrDefault(n=>n&&!n.JoinedCrew);
                if(!agent||!npc)throw new InvalidOperationException("A living crew member and pedestrian are required.");
                Vector3 target=agent.transform.position+Vector3.right*3f;
                if(UnityEngine.AI.NavMesh.SamplePosition(target,out var hit,5f,UnityEngine.AI.NavMesh.AllAreas))
                {
                    var nav=npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if(nav&&nav.isOnNavMesh)nav.Warp(hit.position);else npc.transform.position=hit.position;
                }
                npc.Talk();
                if(!NpcConversationUI.Instance||!NpcConversationUI.Instance.IsConversationOpen)throw new InvalidOperationException("Conversation panel did not open.");
            }
            else if(command=="service-checks") CityServiceChecks.Run();
            else if(command=="stadium-preview")
            {
                var city=UnityEngine.Object.FindFirstObjectByType<CityGameplay>();
                if(!city || city.Locations==null || city.Locations.Length<=5)throw new InvalidOperationException("Live home stadium is unavailable.");
                if(GamePopup.AnyOpen)GamePopup.Instance.Hide();
                CameraPanTouchOnly.Instance?.FocusOn(city.Locations[5]);
            }
            else if(command=="operations-preview")
            {
                if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter play mode first.");
                if(GamePopup.AnyOpen)GamePopup.Instance.Hide();
                var operations=UnityEngine.Object.FindFirstObjectByType<CityOperationsSystem>();
                if(!operations)throw new InvalidOperationException("City operations are unavailable.");
                operations.OpenBoard();
                if(!operations.IsBoardOpen)throw new InvalidOperationException("City operations board did not open.");
            }
            else if(command=="matchday-preview")
            {
                if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter play mode first.");
                CityOperationsSystem.Instance?.CloseBoard();
                var matchday=UnityEngine.Object.FindFirstObjectByType<StadiumMatchdayActivity>();
                if(!matchday)throw new InvalidOperationException("Home matchday activity is unavailable.");
                CameraPanTouchOnly.Instance?.FocusOn(matchday.transform.position);
                matchday.OpenBriefing();
            }
            else if(command=="operations-debug")
            {
                var operations=UnityEngine.Object.FindFirstObjectByType<CityOperationsSystem>();
                if(!operations)throw new InvalidOperationException("City operations are unavailable.");
                var lines=operations.GetComponentsInChildren<Transform>(true).Select(t=>
                {
                    var rt=t as RectTransform;var canvas=t.GetComponent<Canvas>();var graphic=t.GetComponent<UnityEngine.UI.Graphic>();
                    return PathOf(t)+" | active="+t.gameObject.activeInHierarchy+
                        (rt?" rect="+rt.rect+" pos="+rt.anchoredPosition+" scale="+rt.lossyScale:"")+
                        (canvas?" canvas="+canvas.renderMode+" order="+canvas.sortingOrder+" enabled="+canvas.enabled:"")+
                        (graphic?" color="+graphic.color+" enabled="+graphic.enabled:"");
                });
                File.WriteAllLines(OutputPath,lines);return;
            }
            else if(command=="operations-playtest")
            {
                if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter play mode first.");
                var operations=UnityEngine.Object.FindFirstObjectByType<CityOperationsSystem>();
                if(!operations)throw new InvalidOperationException("City operations are unavailable.");
                operations.CloseBoard();
                operations.BeginAutomatedPlaytest("Artifacts/CityQA/city-operations-playtest.txt");
            }
            else if(command.StartsWith("activity-preview:"))
            {
                if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter play mode first.");
                string activityName=command.Substring("activity-preview:".Length);
                var activity=UnityEngine.Object.FindObjectsByType<CityLifeActivity>(FindObjectsSortMode.None)
                    .FirstOrDefault(a=>a.ActivityType.IndexOf(activityName,StringComparison.OrdinalIgnoreCase)>=0);
                if(!activity)throw new InvalidOperationException("Social activity not found: "+activityName);
                CameraPanTouchOnly.Instance?.FocusOn(activity.transform.position);
            }
            else if(command=="away-preview")
            {
                if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter play mode first.");
                CityGameplay.HomeMode=false;
                if(GameManager.Data!=null)GameManager.Data.LastSelectedDestination="East Docks";
                UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
            }
            else if(command=="home-preview")
            {
                if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter play mode first.");
                CityGameplay.HomeMode=true;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
            }
            else if(command=="android-review-build")
            {
                if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode before building.");
                Directory.CreateDirectory("Artifacts/Builds");
                File.WriteAllText("Artifacts/Builds/android-build.txt","BUILDING "+DateTime.Now.ToString("s"));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                    locationPathName="Artifacts/Builds/Hooligan-Milestone-3-Campaign-Review.apk",target=BuildTarget.Android,options=BuildOptions.None
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
            else if (command=="capture-city") GameplaySnapshotCapture.StartFromCommandLine();
            else if (command=="apply-city-shots") GameplaySnapshotCapture.ApplySavedShots();
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
