#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class EnhancementChecks
{
    [MenuItem("Hooligan Manager/Enhancements/Configure assets")]
    public static void Configure()
    {
        const string path = "Assets/Resources/GameplayTuning.asset";
        if (!AssetDatabase.LoadAssetAtPath<GameplayTuning>(path))
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameplayTuning>(), path);
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] {"Assets/Resources/Audio"}))
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter;
            if (!importer) continue;
            var settings = importer.defaultSampleSettings;
            settings.loadType = importer.assetPath.Contains("music") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .55f;
            importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
        }
        // These legacy maps are bypassed by the active Gameplay environment.
        var gameplay = EditorBuildSettings.scenes.FirstOrDefault(s => s.path.EndsWith("/Gameplay.unity"));
        if (gameplay != null && File.ReadAllText(gameplay.path).Contains("useActiveGameplayEnvironment: 1"))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Select(s => new EditorBuildSettingsScene(s.path, s.enabled && !System.Text.RegularExpressions.Regex.IsMatch(s.path, @"/Map\d*\.unity$"))).ToArray();
        PlayerSettings.stripEngineCode = true;
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Artifacts/Enhancements");
        File.WriteAllText("Artifacts/Enhancements/configuration.txt", "Tuning asset and compressed audio configured. Engine stripping enabled.\n" + string.Join("\n", EditorBuildSettings.scenes.Select(s => s.enabled + " " + s.path)));
    }
    [MenuItem("Hooligan Manager/Enhancements/Run regression checks")]
    public static void Run()
    {
        var rows = new List<string>();
        void Check(bool pass, string label) { rows.Add((pass ? "PASS " : "FAIL ") + label); }
        var prior = SaveDataInLocal.EditorSaveOverride;
        try
        {
            Directory.CreateDirectory("Artifacts/Enhancements");
            SaveDataInLocal.EditorSaveOverride = Path.GetFullPath("Artifacts/Enhancements/test-save.json");
            if (File.Exists(SaveDataInLocal.EditorSaveOverride)) File.Delete(SaveDataInLocal.EditorSaveOverride);
            Check(!SaveDataInLocal.HasSavedCampaign(), "Cleared PlayerPrefs leaves no saved campaign");
            var d = new PlayerData(); var agent = new AgentData("Regression member");
            agent.CurrentHp = 0; d.RecruitedAgents.Add(agent); d.BattleRecruitSlotsUsed[1] = 2; d.PoliceHeat = 22;
            d.CityCapturedZones = new List<string> { "Test territory" }; d.PowerPackagesPurchased = 2;
            d.DestinationVisitCounts.Set("Test district", 3);
            SaveDataInLocal.DataSave(d); var loaded = SaveDataInLocal.DataLoad();
            Check(SaveDataInLocal.HasSavedCampaign(), "Explicit save creates a campaign");
            Check(loaded != null && loaded.RecruitedAgents[0].AgentId == agent.AgentId, "Stable roster identity survives save/load");
            Check(loaded.RecruitedAgents[0].CurrentHp == 0, "Downed members remain in saved roster");
            Check(loaded.BattleRecruitSlotsUsed[1] == 2, "Recruitment usage persists");
            Check(loaded.CityCapturedZones.Contains("Test territory"), "Captured territory persists");
            Check(loaded.PowerPackagesPurchased == 2, "Upgrade purchase limit persists");
            Check(loaded.DestinationVisitCounts.GetOrDefault("Test district") == 3, "Destination dictionary survives JSON serialization");
            var holder = new GameObject("GameDataRegression").AddComponent<GameData>();
            holder.PlayerData = loaded;
            holder.NormalizeCampaignData(save: false);
            Check(holder.PlayerData.PoliceHeat == 10, "Police heat clamps consistently to 0-10");
            var activeA = new AgentData("Selected A"); var activeB = new AgentData("Selected B"); var downed = new AgentData("Downed"); downed.CurrentHp = 0;
            holder.PlayerData.RecruitedAgents = new List<AgentData> { activeA, activeB, downed };
            holder.PlayerData.DeploymentSelectionCustomized = false;
            holder.NormalizeCampaignData(save: false);
            Check(holder.PlayerData.Fans == 2, "Fan count follows living roster");
            Check(holder.PlayerData.SelectedAwayAgentIds.Count == 2 && holder.PlayerData.SelectedAwayAgentIds.Contains(activeA.AgentId) && holder.PlayerData.SelectedAwayAgentIds.Contains(activeB.AgentId), "Default away deployment includes all living members");
            holder.PlayerData.DeploymentSelectionCustomized = true;
            holder.PlayerData.SelectedAwayAgentIds = new List<string> { activeA.AgentId };
            holder.NormalizeCampaignData(save: false);
            Check(holder.PlayerData.SelectedAwayAgentIds.Count == 1 && holder.PlayerData.SelectedAwayAgentIds[0] == activeA.AgentId, "Custom away deployment is preserved");
            UnityEngine.Object.DestroyImmediate(holder.gameObject);
            File.WriteAllText(SaveDataInLocal.EditorSaveOverride, "broken JSON");
            Check(SaveDataInLocal.DataLoad() == null, "Corrupt save fails safely");
            var clean = new PlayerData();
            Check(clean.BattleRecruitSlotsUsed.All(n => n == 0), "Fresh campaign has unused recruitment slots");
            var t = GameplayTuning.Current;
            Check(Mathf.Approximately(t.characterScale, 1.5f), "Shared character scale is 1.5");
            Check(t.impactDelay < t.attackInterval && t.combatHeight < t.explorationHeight, "Impact timing and fight framing constraints");
            foreach (string name in new[] { "click", "popup", "impact", "capture", "recovery", "injury", "loading", "move", "menu_music", "gameplay_music" })
                Check(Resources.Load<AudioClip>("Audio/" + name) != null, "Audio present: " + name);
            Check(Resources.Load<GameplayTuning>("GameplayTuning") != null, "Editable tuning asset ships in Resources");
        }
        finally
        {
            SaveDataInLocal.EditorSaveOverride = prior;
            File.WriteAllLines("Artifacts/Enhancements/regression-checks.txt", rows);
        }
        if (rows.Any(r => r.StartsWith("FAIL"))) throw new Exception("Enhancement regression failed; see report.");
    }
    [MenuItem("Hooligan Manager/Enhancements/Build Android review APK")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before building.");
        Configure();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "Artifacts/Enhancements/Hooligan-Enhanced-Review.apk", target = BuildTarget.Android,
            options = BuildOptions.CompressWithLz4HC
        });
        File.WriteAllText("Artifacts/Enhancements/android-build.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nBytes: " + report.summary.totalSize + "\nDuration: " + report.summary.totalTime);
        File.WriteAllLines("Artifacts/Enhancements/largest-build-assets.txt", report.packedAssets.SelectMany(p => p.contents).OrderByDescending(a => a.packedSize).Take(80).Select(a => a.packedSize + " " + a.sourceAssetPath));
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed; see report and Unity log.");
    }
}
#endif
