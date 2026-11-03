#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MilestoneOneChecks
{
    [MenuItem("Hooligan/Landscape/Milestone 01 regression checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in edit mode.");
        Directory.CreateDirectory("Artifacts/Milestone01");
        var rows = new List<string>();
        void Check(bool ok, string label)
        {
            rows.Add((ok ? "PASS " : "FAIL ") + label);
            if (!ok) throw new Exception(label);
        }
        var oldOverride = SaveDataInLocal.EditorSaveOverride;
        var oldRandom = UnityEngine.Random.state;
        var oldRaid = GameManager.PendingRivalAfterRaid;
        var oldSaving = GameData.OnSavingData;
        var prefs = new[] { "OpenRecruitmentOnLoad", "OpenRankingsOnLoad" };
        var values = prefs.Select(p => PlayerPrefs.GetInt(p, -1)).ToArray();
        var holder = new GameObject("Isolated campaign check");
        holder.SetActive(false);
        GameObject display = null;
        try
        {
            SaveDataInLocal.EditorSaveOverride = Path.GetFullPath("Artifacts/Milestone01/regression-save.json");
            GameData.OnSavingData = null;
            var data = holder.AddComponent<GameData>();
            data.PlayerData = new PlayerData {
                Money = 0, CurrentLevel = 5, MusicVolume = .3f,
                CompletedCampaignMissions = new List<int> { 1, 2, 3, 4 },
                CampaignActionKeys = new List<string> { "old-action" },
                LastSessionMode = "Away", BattleSessionActive = true,
                RecruitedAgents = new List<AgentData> { new AgentData("Fallen") }
            };
            data.PlayerData.RecruitedAgents[0].CurrentHp = 0;
            string deadId = data.PlayerData.RecruitedAgents[0].AgentId;
            data.StartNewGame("Test", "T", "#FF0000", "#FFFFFF", "Test Crew", "Rival", 0, 40, 10, 7, 5, 0);
            var d = data.PlayerData;
            Check(GameManager.HasLivingCrew(d) && d.Fans == RecoveryProgression.StarterCrew && d.Money == 5000, "Empty preset / defeated save starts with a viable split crew and starter cash");
            Check(d.RecruitedAgents.All(a => a.AgentId != deadId) && d.SelectedAwayAgentIds.Count == RecoveryProgression.StarterCrew, "Fresh identities are selected for deployment");
            Check(d.CurrentLevel == 1 && !d.BattleSessionActive && (d.CompletedCampaignMissions == null || d.CompletedCampaignMissions.Count == 0) && (d.CampaignActionKeys == null || d.CampaignActionKeys.Count == 0), "Previous campaign progress and session cleared");
            Check(Mathf.Approximately(d.MusicVolume, .3f), "Personal preferences preserved");
            var loaded = SaveDataInLocal.DataLoad();
            Check(GameManager.HasLivingCrew(loaded) && loaded.Money == 5000, "New crew and cash survive save/reload");
            data.StartNewGame("Test", "T", "#FF0000", "#FFFFFF", "Test Crew", "Rival", 8, 40, 10, 7, 5, 9000);
            Check(data.PlayerData.RecruitedAgents.Count == 8 && data.PlayerData.Money == 9000, "Valid higher club starting resources preserved; repeated new game does not duplicate roster");

            display = new GameObject("Display test", typeof(RectTransform));
            var root = (RectTransform)display.transform;
            var safe = LandscapeUI.Rect("Safe", root, 0, 0, 1600, 900);
            var frame = LandscapeUI.Rect("Frame", safe, 0, 0, 1600, 900);
            var viewport = safe.gameObject.AddComponent<LandscapeViewport>();
            viewport.frame = frame;
            var sizes = new[] { new Vector2(1280,720), new Vector2(1920,1080), new Vector2(2400,1080), new Vector2(2532,1170), new Vector2(2960,1440), new Vector2(1024,768) };
            foreach (var size in sizes)
            foreach (bool notch in new[] { false, true })
            {
                root.sizeDelta = size;
                var area = notch ? new Rect(70, 24, size.x - 120, size.y - 24) : new Rect(0, 0, size.x, size.y);
                viewport.Fit(area, size);
                var actual = frame.rect.size * frame.localScale.x;
                Check(Mathf.Abs(actual.x - area.width) < .1f && Mathf.Abs(actual.y - area.height) < .1f && frame.rect.width >= 1599.9f && frame.rect.height >= 899.9f,
                    $"{size.x}x{size.y}, notch={notch}: complete authored layout fits safe area without stretching");
                var scale = frame.localScale;
                viewport.Fit(area, size);
                Check(frame.localScale == scale, "Unchanged display retains layout");
            }
            Check(PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeLeft && !PlayerSettings.allowedAutorotateToPortrait, "Landscape player configuration");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(holder);
            if (display) UnityEngine.Object.DestroyImmediate(display);
            SaveDataInLocal.EditorSaveOverride = oldOverride;
            GameData.OnSavingData = oldSaving;
            UnityEngine.Random.state = oldRandom;
            GameManager.PendingRivalAfterRaid = oldRaid;
            for (int i = 0; i < prefs.Length; i++)
                if (values[i] == -1) PlayerPrefs.DeleteKey(prefs[i]); else PlayerPrefs.SetInt(prefs[i], values[i]);
            File.WriteAllLines("Artifacts/Milestone01/regression-checks.txt", rows);
        }
    }
}
#endif
