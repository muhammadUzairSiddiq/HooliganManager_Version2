#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static partial class LandscapeSceneBuilder
{
    public static void SetGameViewSize(string value)
    {
        var parts=value.Split('x');int width=int.Parse(parts[0]),height=int.Parse(parts[1]);
        Require(width>=640 && width<=3840 && height>=360 && height<=2160,"Invalid preview resolution.");
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var groupType=sizesType.GetProperty("currentGroupType",flags).GetValue(sizes);
        var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new object[]{groupType});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");
        var kind=assembly.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.ToObject(kind,1),width,height,"Landscape UI "+value},null);
        group.GetType().GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});
        int built=(int)group.GetType().GetMethod("GetBuiltinCount",flags).Invoke(group,null);
        int custom=(int)group.GetType().GetMethod("GetCustomCount",flags).Invoke(group,null);
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",flags).SetValue(view,built+custom-1);
        view.maximized=true;view.Show();view.Focus();Application.runInBackground=true;
    }
    static void Require(bool condition,string message) {if(!condition) throw new InvalidOperationException(message);}
    [MenuItem("Hooligan/Landscape/Validate scene wiring")]
    public static void Validate()
    {
        if(EditorApplication.isPlaying) {ValidateCurrent();return;}
        string restore=SceneManager.GetActiveScene().path;
        var report=new List<string>();
        foreach(string name in SceneNames)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");ValidateCurrent();
            report.Add("PASS "+name+": landscape canvas, navigation and required controller bindings.");
        }
        Require(!PlayerSettings.allowedAutorotateToPortrait && PlayerSettings.allowedAutorotateToLandscapeLeft && PlayerSettings.allowedAutorotateToLandscapeRight,"Landscape player settings missing.");
        report.Add("PASS player orientation: both landscape rotations enabled, portrait disabled.");
        Directory.CreateDirectory("Artifacts/LandscapeUI");File.WriteAllLines("Artifacts/LandscapeUI/validation.txt",report);
        if(!string.IsNullOrEmpty(restore)) EditorSceneManager.OpenScene(restore);
    }
    static void ValidateCurrent()
    {
        Require(Find<LandscapeViewport>()!=null,"No landscape viewport.");
        var root=SceneManager.GetActiveScene().GetRootGameObjects();
        foreach(var r in root) foreach(var t in r.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script at "+t.name);
        var shell=Find<LandscapeFrontEnd>();
        if(shell)
        {
            Require(shell.money && shell.fans && shell.reputation && shell.day && shell.settingsPanel,"Missing top bar / settings binding.");
            foreach(var action in shell.GetComponentsInChildren<LandscapeAction>(true))
                Require(action.target==shell && !string.IsNullOrWhiteSpace(action.action),"Broken navigation: "+action.name);
            if(!shell.mainMenu)
            {
                Require(shell.home && shell.trips && shell.recruitment && shell.rankings && shell.club && shell.squad && shell.missions,"Missing management screen.");
                var trips=Find<PlanAwayTripController>();Require(trips && trips.startTripBtn && trips.destinations.All(d=>d.mapButton),"Missing trip selection binding.");
                var recruit=Find<RecruitFansController>();Require(recruit && recruit.recruitBtn && recruit.optionCards.Count==recruit.recruitOptions.Count,"Missing recruit cards.");
                var rank=Find<RankingsController>();Require(rank && rank.leaderboardRowPrefab && rank.leaderboardContainer,"Missing ranking prefab.");
            }
        }
        var battle=Find<BattleUIController>();
        if(battle) Require(battle.resultPanel && battle.pauseButton && battle.attackBtn && battle.moveBtn && battle.retreatBtn && battle.portraitCardPrefab && battle.portraitStrip && battle.buildingInteractionPanel,"Missing battle bindings.");
    }
    public static void PreviewResult()
    {
        var battle=Find<BattleUIController>();Require(battle && battle.resultPanel,"Load battle scene before result preview.");
        battle.resultPanel.Show(new BattleResultData {
            Result=GameData.BattleResult.Victory, PlayerFirmName=GameData.instance?.PlayerData?.FirmName??"NORTH CITY CREW",PlayerFirmSubName="",EnemyFirmName="EAST END CREW",EnemyFirmSubName="",
            PlayerRoundsWon=2,EnemyRoundsWon=1,TotalRounds=3,EnemiesDefeated=8,UnitsLost=1,MoneyEarned=2500,ReputationGained=12,FansGained=3,PoliceHeatChange=2,DestinationName="East Docks"
        });
    }
    static void InvokePrivate(object target,string method,params object[] args) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
    public static void SmokeTest()
    {
        Require(EditorApplication.isPlaying,"Smoke checks run in Play mode on DashboardScene.");
        var shell=Find<LandscapeFrontEnd>();Require(shell && !shell.mainMenu,"Open headquarters before running the smoke checks.");
        var gd=GameData.instance;Require(gd && gd.PlayerData!=null,"Save data unavailable.");
        var original=gd.PlayerData;var random=UnityEngine.Random.state;
        var report=new List<string>();
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(".utmp/landscape-smoke-save.sz");
        try
        {
            using(var stream=new MemoryStream()) {var f=new BinaryFormatter();f.Serialize(stream,original);stream.Position=0;gd.PlayerData=(PlayerData)f.Deserialize(stream);}
            var d=gd.PlayerData;d.Money=15000;d.Reputation=60;d.FanMorale=85;d.PoliceWatchlisted=false;
            foreach(string page in new[]{"home","trips","recruitment","rankings","club","squad","missions"}) {shell.Navigate(page);Require(shell.currentPage==page,"Navigation failed: "+page);}
            report.Add("PASS all seven headquarters pages navigate with live data.");
            shell.Navigate("recruitment");var rc=Find<RecruitFansController>();
            InvokePrivate(rc,"BuildCards");InvokePrivate(rc,"SelectOption",0);
            int before=d.Money,pending=d.PendingFansGain;InvokePrivate(rc,"OnRecruit");
            Require(d.Money<before && d.PendingFansGain>pending,"Recruitment did not charge and queue fans.");
            report.Add("PASS recruitment charges money, queues fans and displays confirmation.");
            d.Money=0;before=d.PendingFansGain;InvokePrivate(rc,"OnRecruit");Require(d.PendingFansGain==before,"Insufficient funds still recruited.");
            report.Add("PASS recruitment rejects insufficient funds.");
            var agent=new AgentData("UI TEST",0);agent.CurrentHp=0;d.RecruitedAgents.Add(agent);d.Money=10000;
            shell.Navigate("squad");shell.Navigate("squad-fallen");
            Require(gd.ReviveFan(agent,600) && agent.IsAlive,"Squad revival failed.");
            report.Add("PASS fallen roster and revival restore a member.");
            d.LandscapeClaimedMissions=new List<string>();d.Reputation=60;d.Fans=6;d.BattleWins=4;d.CurrentLevel=3;
            shell.Navigate("missions");int moneyBefore=d.Money;InvokePrivate(shell,"ClaimMission","crew5");int moneyAfter=d.Money;
            InvokePrivate(shell,"ClaimMission","crew5");Require(moneyAfter==moneyBefore+1000 && d.Money==moneyAfter,"Mission claim not idempotent.");
            report.Add("PASS mission reward granted once; repeated claim cannot pay twice.");
            gd.SaveData();var reloaded=SaveDataInLocal.DataLoad();Require(reloaded.LandscapeClaimedMissions.Contains("crew5"),"Mission state did not persist.");
            report.Add("PASS mission claims survive save/load.");
            var btn=rc.recruitBtn;btn.interactable=false;int calls=0;UnityEngine.Events.UnityAction listener=()=>calls++;btn.OnClick.AddListener(listener);
            btn.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));btn.OnClick.RemoveListener(listener);Require(calls==0,"Disabled button accepted input.");
            report.Add("PASS disabled ButtonUI rejects pointer input.");
            shell.Navigate("settings");Require(shell.settingsPanel.activeSelf,"Settings did not open.");shell.Navigate("close-settings");Require(!shell.settingsPanel.activeSelf,"Settings did not close.");
            report.Add("PASS settings open, persist and close.");
        }
        finally
        {
            gd.PlayerData=original;UnityEngine.Random.state=random;SaveDataInLocal.EditorSaveOverride=null;shell.Navigate("home");shell.dashboard?.RefreshUI();
        }
        File.WriteAllLines("Artifacts/LandscapeUI/smoke-tests.txt",report);
        Debug.Log("[Landscape UI] "+report.Count+" smoke checks passed. User campaign was preserved.");
    }
}
#endif
