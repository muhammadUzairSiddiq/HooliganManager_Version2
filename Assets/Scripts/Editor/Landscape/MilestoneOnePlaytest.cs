#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using TMPro;

[InitializeOnLoad]
public static class MilestoneOnePlaytest
{
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    const string Key = "HM.MilestoneOnePlaytest";
    static int stage, sizeIndex;
    static double next, deadline;
    static readonly List<string> rows = new List<string>();
    static readonly string[] sizes = { "1280x720", "2400x1080", "1024x768" };
    static MilestoneOnePlaytest()
    {
        if (SessionState.GetBool(Key, false)) SaveDataInLocal.EditorSaveOverride = Path.GetFullPath("Artifacts/Milestone01/playtest-save.json");
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
    }
    public static void Begin()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Requires edit mode and a saved scene.");
        Directory.CreateDirectory("Artifacts/Milestone01");
        SessionState.SetBool(Key, true);
        SaveDataInLocal.EditorSaveOverride = Path.GetFullPath("Artifacts/Milestone01/playtest-save.json");
        var d = new PlayerData { Money = 0, Fans = 0, CurrentLevel = 5 };
        d.RecruitedAgents.Add(new AgentData("Fallen") { CurrentHp = 0 });
        SaveDataInLocal.DataSave(d);
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        { stage = 0; sizeIndex = 0; rows.Clear(); next = EditorApplication.timeSinceStartup + 3; deadline = next + 120; UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu"); }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            Time.timeScale = 1;
            SessionState.SetBool(Key, false); SaveDataInLocal.EditorSaveOverride = null;
            if(SessionState.GetBool(Key+".batch",false))
            {
                SessionState.SetBool(Key+".batch",false);
                EditorApplication.Exit(rows.Any(r=>r.StartsWith("FAIL "))?1:0);
            }
        }
    }
    static void Check(bool ok, string label)
    { rows.Add((ok ? "PASS " : "FAIL ") + label); if (!ok) throw new Exception(label); }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (stage == 0)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                if (!menu) throw new Exception("Main menu missing");
                Check(!GameManager.HasLivingCrew(GameManager.Data), "Reproduced defeated saved campaign");
                Check(menu.newGameBtn.GetComponentInChildren<TextMeshProUGUI>().text.Contains("CHOOSE ROLE"), "Main action introduces role selection");
                menu.newGameBtn.AfterClickAnimation.Invoke();
                Check(SimpleShellLayout.ShowModes(),"Role menu is available before entering a campaign");
                menu.ConfirmNewFirm();
                Check(GamePopup.AnyOpen, "New game requests overwrite instead of resuming defeated campaign");
                var confirm = GamePopup.Instance.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<TextMeshProUGUI>().text == "START NEW GAME");
                confirm.onClick.Invoke(); stage = 1; next = EditorApplication.timeSinceStartup + 2;
            }
            else if (stage == 1)
            {
                var battle = BattleManager.instance;
                if (!battle || !battle.BattleActive || !CityGameplay.Instance || battle.PlayerAgents.Count == 0)
                { if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Gameplay did not become playable"); return; }
                Check(CityGameplay.HomeMode && GameManager.HasLivingCrew(GameManager.Data), "New game entered home gameplay with living crew");
                Check(GameManager.Data.Money >= 5000, "Starter cash available without ads");
                Check(GameManager.Data.PoliceHeat==RecoveryProgression.StarterHeat,"Actual New Game entry preserves quiet starting police heat");
                Check(!GameManager.Data.LearnedSelection&&!GameManager.Data.LearnedMultiSelection&&AgentSelectionManager.instance.SelectedAgents.Count==0,"New Game waits for the player's selection lesson instead of completing it automatically");
                Check(battle.PlayerAgents.All(a => a && a.IsAlive && a.GetComponent<NavMeshAgent>().isOnNavMesh), "Crew spawned alive on navigation mesh");
                Check(UnityEngine.Object.FindFirstObjectByType<LandscapeBattleHUD>() && CameraPanTouchOnly.Instance && AgentSelectionManager.instance, "Landscape HUD, touch camera and unit selection loaded");
                AgentSelectionManager.instance.SelectAll();
                CameraPanTouchOnly.Instance.CenterOnSelection();
                battle.SetAllAgentsCinematicIdle(true);
                stage = 2; next = EditorApplication.timeSinceStartup + 8;
            }
            else if (stage == 2)
            {
                Time.timeScale = 1;
                if (sizeIndex == sizes.Length)
                {
                    File.WriteAllLines("Artifacts/Milestone01/playtest-checks.txt", rows);
                    Time.timeScale = 1;
                    EditorApplication.isPlaying = false; stage = 4; return;
                }
                LandscapeSceneBuilder.SetGameViewSize(sizes[sizeIndex]);
                next = EditorApplication.timeSinceStartup + 3; stage = 3;
            }
            else if (stage == 3)
            {
                var hud = UnityEngine.Object.FindFirstObjectByType<LandscapeBattleHUD>();
                Check(hud.frame.rect.width >= 1599 && hud.frame.rect.height >= 899, sizes[sizeIndex] + " gameplay retains complete logical layout");
                Check(ModernCityHud.Instance&&!hud.frame.Find("StatusStrip").gameObject.activeInHierarchy&&hud.frame.Find("CityTop/Resources").gameObject.activeInHierarchy,
                    sizes[sizeIndex] + " cash and compact heat visible, extended statistics hidden");
                ScreenCapture.CaptureScreenshot("Artifacts/Milestone01/gameplay-" + sizes[sizeIndex] + ".png");
                sizeIndex++; stage = 2; next = EditorApplication.timeSinceStartup + 1;
            }
        }
        catch (Exception e)
        {
            rows.Add("FAIL " + e); File.WriteAllLines("Artifacts/Milestone01/playtest-checks.txt", rows);
            Time.timeScale = 1;
            EditorApplication.isPlaying = false; stage = 4;
        }
    }
}
#endif
