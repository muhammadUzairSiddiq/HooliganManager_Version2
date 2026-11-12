#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Actual Unity save serializer checks with a disposable campaign.</summary>
public static class RecoveryChecks
{
    [MenuItem("Hooligan/Recovery/Run startup and save checks")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run from edit mode.");
        Directory.CreateDirectory("Artifacts/Recovery");
        var rows=new List<string>();
        void Check(bool ok,string message){rows.Add((ok?"PASS ":"FAIL ")+message);}
        var previous=SaveDataInLocal.EditorSaveOverride;
        var random=UnityEngine.Random.state;
        var saving=GameData.OnSavingData;
        bool pendingRaid=GameManager.PendingRivalAfterRaid;
        var keys=new[]{"OpenRecruitmentOnLoad","OpenRankingsOnLoad"};
        var values=keys.Select(k=>PlayerPrefs.GetInt(k,-1)).ToArray();
        var root=new GameObject("Isolated recovery checks");root.SetActive(false);
        try
        {
            SaveDataInLocal.EditorSaveOverride=Path.GetFullPath("Artifacts/Recovery/serializer-test-save.json");
            GameData.OnSavingData=null;
            var data=root.AddComponent<GameData>();
            data.PlayerData=new PlayerData{Money=0,CurrentLevel=5,MusicVolume=.35f,RecruitedAgents=new List<AgentData>{new AgentData("Fallen"){CurrentHp=0}}};
            data.StartNewGame("Test","TEST","#00AA00","#FFFFFF","Test Firm","Test Rival",0,40,10,7,5,0);
            var d=data.PlayerData;
            Check(d.RecruitedAgents.Count(a=>a!=null&&a.IsAlive)==3&&d.Money==5000&&d.PoliceHeat==1,"Fresh reset grants three living crew, starter funds and low heat");
            Check(d.RecoveryGuidedStart&&d.RecoveryVersion==RecoveryProgression.Version&&d.CurrentLevel==1,"Fresh campaign starts the new guided progression");
            Check(Mathf.Approximately(d.MusicVolume,.35f),"Personal audio preference survives New Game");
            RecoveryProgression.RecordSelection(d,1);RecoveryProgression.RecordOrder(d,"MOVE");RecoveryProgression.RecordOrder(d,"MOVE");
            RecoveryProgression.RecordSelection(d,2);RecoveryProgression.RecordSelection(d,1,true);
            RecoveryProgression.RecordOrder(d,"GUARD");RecoveryProgression.RecordOrder(d,"PATROL");
            CityBusinessLedger.Control(d,"pizza");CityBusinessLedger.Loot(d,"pizza");
            d.BusinessEconomySeconds=38f;d.MatchdayStartedDay=1;d.MatchdayPhase="BuildUp";d.MatchdayPhaseSeconds=42f;
            d.MatchdayCompletedResponses=new List<string>{"stadium-approach"};d.MatchdayStadiumResponseSeconds=20f;
            data.SaveData();var loaded=SaveDataInLocal.DataLoad();
            Check(loaded!=null&&RecoveryProgression.OrientationComplete(loaded)&&RecoveryProgression.DutiesComplete(loaded),"Learned controls survive the actual Unity save roundtrip");
            var pizza=CityBusinessLedger.Find(loaded,"pizza");
            Check(pizza.controlled&&pizza.lootCooldownSeconds==300&&pizza.disruptionSeconds==180&&Mathf.Approximately(loaded.BusinessEconomySeconds,38f),"Business ownership, loot cooldown, disruption and tick survive save/reload");
            Check(loaded.MatchdayStartedDay==1&&loaded.MatchdayPhase=="BuildUp"&&Mathf.Approximately(loaded.MatchdayPhaseSeconds,42f)&&loaded.MatchdayCompletedResponses.Contains("stadium-approach"),"Matchday phase, clock and response outcome survive save/reload");
            int money=loaded.Money;CityBusinessLedger.Ensure(loaded);CampaignMissions.Ensure(loaded);
            Check(loaded.Money==money&&!CityBusinessLedger.Loot(loaded,"pizza"),"Reload/normalization does not pay again or bypass the loot cooldown");
            d.SelectedAwayAgentIds.Clear();d.DeploymentSelectionCustomized=true;data.SaveData();
            Check(d.SelectedAwayAgentIds.Count==0&&SaveDataInLocal.DataLoad().SelectedAwayAgentIds.Count==0,"A deliberate empty Away preparation selection survives normalization and native save");
            d.ActiveAwayAgentIds=d.RecruitedAgents.Take(2).Select(a=>a.AgentId).ToList();d.MatchdayElapsedSeconds=233;
            d.MatchdayFailedResponses=new List<string>{"pub-security"};d.MatchdayPenalizedResponses=new List<string>{"pub-security"};
            d.MatchdayGroupsDay=d.MatchDay;d.MatchdayGroups=new List<MatchdayGroupSave>{new MatchdayGroupSave{name="Saved Firm",color="#FF0000",role=1,task="REGROUP",destination=new Vector3(12,0,8),members=new List<MatchdayMemberSave>{new MatchdayMemberSave{hp=0},new MatchdayMemberSave{hp=17,position=new Vector3(3,0,4)}}}};
            data.SaveData();loaded=SaveDataInLocal.DataLoad();
            Check(loaded.ActiveAwayAgentIds.Count==2&&loaded.MatchdayFailedResponses.Contains("pub-security")&&loaded.MatchdayPenalizedResponses.Contains("pub-security")&&Mathf.Approximately(loaded.MatchdayElapsedSeconds,233),"Native save preserves actual travellers, matchday deadlines and penalty-once records");
            Check(loaded.MatchdayGroups.Count==1&&loaded.MatchdayGroups[0].members[0].hp==0&&loaded.MatchdayGroups[0].members[1].hp==17&&loaded.MatchdayGroups[0].destination==new Vector3(12,0,8),"Native group serialization preserves casualties, injuries and destinations");
            d.BattleStartPoliceHeat=0;d.BattleStartAgentIds=d.RecruitedAgents.Select(a=>a.AgentId).ToList();data.RestoreBattleSessionSnapshot();
            Check(d.PoliceHeat==0,"Retry preserves a valid zero-heat snapshot");
            var old=new PlayerData{RecoveryVersion=0,PoliceHeat=9,Money=123,CurrentLevel=3,CompletedCampaignMissions=new List<int>{1,2}};
            CampaignMissions.Ensure(old);CampaignMissions.Ensure(old);
            Check(old.PoliceHeat==9&&old.Money==123&&old.CurrentLevel==3&&CampaignMissions.HomeCleared(old)&&!old.RecoveryGuidedStart,"Legacy migration preserves earned heat, cash and unlocked Home/Away progress");

            loaded.MobileTutorialVersion=GuidedCityTutorial.Version;
            loaded.MobileTutorialRequired=false;
            loaded.MobileTutorialStep=24;
            loaded.MobileTutorialRecruitDone=true;
            loaded.RemoveAdsEntitlement=true;
            Check(GameCacheBootstrap.RefreshFirmTutorial(loaded,"0.3.1")&&loaded.MobileTutorialRequired&&
                loaded.MobileTutorialStep==0&&loaded.MobileTutorialVersion==0,"An existing firm gets a fresh tutorial after an app update");
            loaded.MobileTutorialStep=4;
            SaveDataInLocal.DataSave(loaded);
            loaded=SaveDataInLocal.DataLoad();
            Check(!GameCacheBootstrap.RefreshFirmTutorial(loaded,"0.3.1")&&loaded.MobileTutorialStep==4&&
                loaded.RemoveAdsEntitlement&&loaded.MobileTutorialRecruitDone&&loaded.MatchdayGroups.Count==1,
                "Same version preserves tutorial progress, purchase entitlement and the saved firm");
            Check(GameCacheBootstrap.RefreshFirmTutorial(loaded,"0.3.2")&&loaded.MobileTutorialStep==0,
                "The next release refreshes the tutorial again");

            var career=PoliceCampaignStore.Create();
            career.PoliceTutorialRequired=false;
            career.PoliceTutorialStep=12;
            career.PoliceTutorialRoadId="stale";
            career.booked=5;
            Check(GameCacheBootstrap.RefreshPoliceTutorial(career,"0.3.1")&&career.PoliceTutorialRequired&&
                career.PoliceTutorialStep==0&&career.PoliceTutorialRoadId==null,"A Police update removes the stale tutorial target");
            PoliceCampaignStore.SaveCareer(career);
            career=PoliceCampaignStore.LoadOrCreate();
            Check(!GameCacheBootstrap.RefreshPoliceTutorial(career,"0.3.1")&&career.booked==5,
                "Police upgrade receipt and bookings survive the real serializer");

            string scratch=Path.GetFullPath(".utmp/version-check-"+Guid.NewGuid().ToString("N"));
            try
            {
                string nested=Path.Combine(scratch,"cache","nested");
                Directory.CreateDirectory(nested);
                File.WriteAllText(Path.Combine(nested,"stale"),"temporary");
                string savedFile=Path.Combine(scratch,"persistent");
                File.WriteAllText(savedFile,"keep");
                Check(GameCacheBootstrap.ClearTemporaryCache(Path.Combine(scratch,"cache"))&&
                    !Directory.Exists(nested)&&File.ReadAllText(savedFile)=="keep",
                    "Temporary cache cleanup removes nested files and leaves adjacent data alone");
                string ledger=Path.Combine(scratch,"release-versions.txt");
                var first=ReleaseBuildVersion.Reserve(ledger,"0.3.0",3,"0");
                var second=ReleaseBuildVersion.Reserve(ledger,"0.3.0",3,"0");
                Check(first==("0.3.1",4)&&second==("0.3.2",5),
                    "Consecutive release reservations advance even when an isolated build has older project settings");
            }
            finally
            {
                if(GameCacheBootstrap.ClearTemporaryCache(scratch)&&Directory.Exists(scratch))Directory.Delete(scratch);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            SaveDataInLocal.EditorSaveOverride=previous;UnityEngine.Random.state=random;GameData.OnSavingData=saving;GameManager.PendingRivalAfterRaid=pendingRaid;
            for(int i=0;i<keys.Length;i++)if(values[i]<0)PlayerPrefs.DeleteKey(keys[i]);else PlayerPrefs.SetInt(keys[i],values[i]);
            File.WriteAllLines("Artifacts/Recovery/unity-save-checks.txt",rows);
        }
        if(rows.Any(r=>r.StartsWith("FAIL ")))throw new Exception("Recovery checks failed; see Artifacts/Recovery/unity-save-checks.txt.");
        Debug.Log($"Recovery startup/save checks: {rows.Count} passed.");
    }
}
#endif
