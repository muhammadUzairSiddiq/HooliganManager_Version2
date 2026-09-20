#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class EnhancementPlaytest
{
    const string Key = "HM.EnhancementPlaytest";
    static int stage;
    static double readyAt;
    static readonly List<string> rows = new List<string>();
    static EnhancementPlaytest()
    {
        if (SessionState.GetBool(Key, false)) SaveDataInLocal.EditorSaveOverride = Path.GetFullPath("Artifacts/Enhancements/playtest-save.json");
        EditorApplication.playModeStateChanged += StateChanged;
        EditorApplication.update += Tick;
    }
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play before starting isolated checks.");
        Directory.CreateDirectory("Artifacts/Enhancements");
        var old = SaveDataInLocal.EditorSaveOverride;
        SaveDataInLocal.EditorSaveOverride = Path.GetFullPath("Artifacts/Enhancements/playtest-save.json");
        var d = new PlayerData { Money = 10000, Fans = 4, PlayerName = "QA", AcceptedPrivacyPolicy = true };
        for (int i = 0; i < 4; i++) d.RecruitedAgents.Add(new AgentData("Review Member " + (i + 1), i));
        SaveDataInLocal.DataSave(d); SaveDataInLocal.EditorSaveOverride = old;
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void StateChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            stage = 0; readyAt = EditorApplication.timeSinceStartup + 90;
            CityGameplay.HomeMode = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
        }
        if (change == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key, false); SaveDataInLocal.EditorSaveOverride = null; }
    }
    static void Check(bool pass, string label) => rows.Add((pass ? "PASS " : "FAIL ") + label);
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            var bm = BattleManager.instance;
            if (stage == 0)
            {
                if (!bm || !bm.BattleActive || !CityGameplay.Instance || bm.PlayerAgents.Count < 4)
                {
                    if (EditorApplication.timeSinceStartup > readyAt) throw new Exception("Gameplay did not become ready in 90 seconds");
                    return;
                }
                rows.Clear();
                foreach (var a in bm.PlayerAgents)
                {
                    Check(a.Data.MaxHp == 60 && a.Data.Strength == 10, "Spawn does not inflate stats: " + a.Data.AgentName);
                    Check(a.GetComponent<NavMeshAgent>().isOnNavMesh, "Player spawned on NavMesh");
                    Check(a.animator && Mathf.Approximately(a.animator.transform.localScale.x, 1.5f), "Player visual scale 1.5");
                    a.SetCinematicIdle(true);
                }
                foreach (var e in bm.EnemyAgents)
                    Check(e.animator && Mathf.Approximately(e.animator.transform.localScale.x, 1.5f), "Rival visual scale 1.5");
                var member = bm.PlayerAgents[0]; var data = member.Data;
                int money = GameManager.Data.Money;
                member.TakeDamage(1000);
                Check(!data.IsAlive && GameManager.Data.RecruitedAgents.Contains(data), "Downed member stays in roster");
                Check(SquadCare.Recover(data, 200), "Paid live recovery succeeds");
                Check(member.IsAlive && member.gameObject.activeSelf && member.GetComponent<NavMeshAgent>().isOnNavMesh, "Recovered unit immediately usable on the map");
                Check(GameManager.Data.Money == money - 200, "Recovery charged exactly once");
                Check(!SquadCare.Recover(data, 200) && GameManager.Data.Money == money - 200, "Healthy recovery cannot charge twice");
                Check(!SquadCare.Recover(data, -200), "Negative recovery price rejected");
                member.TakeDamage(10);
                Check(data.CurrentHp == member.CurrentHp, "Damage immediately syncs roster health");
                SquadCare.Recover(data, 200);
                Check(SaveDataInLocal.DataLoad().RecruitedAgents[0].CurrentHp == 60, "Recovered health survives reload");
                bm.SetAllAgentsCinematicIdle(true);
                GamePopup.Instance.Hide();
                CameraPanTouchOnly.Instance?.CenterOnSelection();
                readyAt = EditorApplication.timeSinceStartup + 2; stage = 1;
            }
            else if (stage == 1 && EditorApplication.timeSinceStartup > readyAt)
            {
                ScreenCapture.CaptureScreenshot("Artifacts/Enhancements/gameplay-review.png");
                GamePopup.Instance.Show("RECOVERY COMPLETE", "Your squad member has returned to full health and can be selected again.");
                readyAt = EditorApplication.timeSinceStartup + 2; stage = 2;
            }
            else if (stage == 2 && EditorApplication.timeSinceStartup > readyAt)
            {
                var volume = UnityEngine.Object.FindFirstObjectByType<ModalPresentation>();
                Check(volume && volume.GetComponent<UnityEngine.Rendering.Volume>().weight == 1, "Modal enables silver world post processing");
                ScreenCapture.CaptureScreenshot("Artifacts/Enhancements/silver-popup-review.png");
                File.WriteAllLines("Artifacts/Enhancements/live-checks.txt", rows); stage = 3;
            }
        }
        catch (Exception e)
        { rows.Add("FAIL " + e); File.WriteAllLines("Artifacts/Enhancements/live-checks.txt", rows); stage = 3; }
    }
}
#endif
