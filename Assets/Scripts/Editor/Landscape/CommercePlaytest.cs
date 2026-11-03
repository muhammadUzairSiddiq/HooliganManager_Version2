#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CommercePlaytest
{
    const string Key="HM.CommercePlaytest",Save="Artifacts/Recovery/commerce-save.json",Report="Artifacts/Recovery/commerce-playtest.txt";
    static readonly List<string> checks=new List<string>();
    static bool running,finished;static double deadline;
    static CommercePlaytest()
    {
        if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;
    }
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run from edit mode.");
        Directory.CreateDirectory("Artifacts/Recovery");SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var d=new PlayerData{PlayerName="Commerce QA",Money=5000,CitySupplies=8};RecoveryProgression.Begin(d);
        foreach(var name in new[]{"Brick","Dex","Ash"})d.RecruitedAgents.Add(new AgentData(name,hp:500,speed:20));
        d.CitySupplies=8;SaveDataInLocal.DataSave(d);SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING");EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){running=finished=false;checks.Clear();deadline=EditorApplication.timeSinceStartup+120;CityGameplay.HomeMode=true;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("Gameplay");}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");
            int marker=SessionState.GetInt(Key+".marker",-1);if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(checks.Any(c=>c.StartsWith("FAIL "))?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Commerce watchdog expired");return;}
        if(running||!CityCommerceSystem.Instance||!CityBusinessSystem.Instance||!BattleManager.instance||!BattleManager.instance.BattleActive)return;
        running=true;deadline=EditorApplication.timeSinceStartup+660;var host=new GameObject("Commerce runner");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<CommerceTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator routine)
    {
        while(true){object value=null;bool more=false;try{more=routine.MoveNext();if(more)value=routine.Current;}catch(Exception e){Finish(e.ToString());yield break;}if(!more){Finish(null);yield break;}yield return value;}
    }
    static void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines(Report,checks);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)checks.Add("FAIL "+error);
        checks.Add($"RESULT {checks.Count(c=>c.StartsWith("PASS "))} passed, {checks.Count(c=>c.StartsWith("FAIL "))} failed");
        checks.Add("Isolated native commerce fixture: durable fast crew, accelerated simulation and ambient pressure suppressed. No device or live store purchases.");File.WriteAllLines(Report,checks);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(1);
        var d=GameManager.Data;var business=CityBusinessSystem.Instance;var commerce=CityCommerceSystem.Instance;var selection=AgentSelectionManager.instance;
        // Resource-context migration runs during city startup. Seed the isolated
        // construction budget only after it has completed, never in a real save.
        d.CitySupplies=8;
        foreach(var director in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))director.enabled=false;
        foreach(var enemy in BattleManager.instance.EnemyAgents)if(enemy){enemy.SetCinematicIdle(true);enemy.isHostile=false;}
        if(LivePoliceSystem.Instance){LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;}
        var development=UnityEngine.Object.FindFirstObjectByType<CityDevelopmentSystem>();if(development)development.enabled=false;
        var site=business.Sites.First(s=>s.Definition.id=="burger");var brick=BattleManager.instance.PlayerAgents.First();
        selection.DeselectAll();int cash=d.Money,supplies=d.CitySupplies;
        Check(!commerce.TryTrade(site,true)&&d.Money==cash&&d.CitySupplies==supplies,"A venue cannot trade remotely without selected on-site crew");
        Check(!CityCommerceRules.Trade(d,"burger",true),"An uncontrolled venue cannot sell supplies");
        selection.Select(brick);Time.timeScale=4;
        Check(business.TryAssign(site,false),"Selected crew accepts actual travel and control work at the business");
        float until=Time.realtimeSinceStartup+100;while(business.HasAssignment(site)&&Time.realtimeSinceStartup<until)yield return null;
        var state=CityBusinessLedger.Find(d,"burger");Check(state.controlled,"Physical arrival and on-site control establish the trading venue");
        cash=d.Money;supplies=d.CitySupplies;
        Check(commerce.TryTrade(site,true)&&d.Money==cash-120&&d.CitySupplies==supplies+1,"On-site purchase exchanges exactly £120 for one supply");
        Check(commerce.TryTrade(site,false)&&d.Money==cash-50&&d.CitySupplies==supplies,"Selling returns £70; buy-sell roundtrips cannot create money");
        state.disruptionSeconds=20;cash=d.Money;
        Check(!commerce.TryTrade(site,true)&&d.Money==cash,"A disrupted venue refuses trade without charging");state.disruptionSeconds=0;
        int originalSupplies=d.CitySupplies;cash=d.Money;int earned=d.BusinessIncomeEarned;
        Check(business.TryInvest(site),"A controlled venue accepts a funded crew investment assignment");
        selection.CommandSelectedMoveTo(CityGameplay.Instance.Home);yield return null;yield return null;
        Check(!business.HasAssignment(site)&&state.investmentLevel==0&&d.Money==cash+(d.BusinessIncomeEarned-earned)&&d.CitySupplies==originalSupplies,"Manual orders cancel investment without taking cash or supplies");
        Check(business.TryInvest(site),"Cancelled venue investment can be deliberately restarted");
        cash=d.Money;earned=d.BusinessIncomeEarned;until=Time.realtimeSinceStartup+100;while(business.HasAssignment(site)&&Time.realtimeSinceStartup<until)yield return null;
        Check(state.investmentLevel==1&&d.Money==cash-350+(d.BusinessIncomeEarned-earned)&&d.CitySupplies==originalSupplies-2,"First expansion takes £350 and two supplies once after real crew work");
        Check(GameObject.Find("BUSINESS EXPANSION LEVEL 2"),"Investment is represented by a visible expanded venue and supply crates");
        Check(CityBusinessLedger.Yield(d,site.Definition,false)>site.Definition.income,"The completed expansion increases future income, not an instant payout");
        GameManager.Save();var loaded=SaveDataInLocal.DataLoad();Check(CityBusinessLedger.Find(loaded,"burger").investmentLevel==1,"Native save retains invested venue level");
        state.rivalPressureSeconds=2;cash=d.Money;Check(!CityCommerceRules.Invest(d,"burger")&&d.Money==cash,"Contested venue cannot upgrade or spend cash");state.rivalPressureSeconds=0;
        state.controlled=false;Check(!CityCommerceRules.Trade(d,"burger",false)&&state.investmentLevel==1,"Losing control disables trade while retaining the physical investment");state.controlled=true;
        Time.timeScale=1;commerce.Open(site);CameraPanTouchOnly.Instance.FocusOn(site.transform.position);yield return new WaitForSecondsRealtime(.5f);
        Check(commerce.IsOpen&&AgentSelectionManager.BlocksWorldTap(),"The local trade card owns input without ordering through the panel");
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/commerce-venue.png");yield return null;commerce.Close();
        // Home completion below is fixture setup so the real Away planner can
        // be exercised without replaying unrelated combat and matchday here.
        d.CurrentLevel=2;if(!d.CompletedCampaignMissions.Contains(1))d.CompletedCampaignMissions.Add(1);
        d.SelectedAwayAgentIds=d.RecruitedAgents.Where(a=>a.IsAlive).Take(3).Select(a=>a.AgentId).ToList();d.DeploymentSelectionCustomized=true;GameManager.Save();
        var plannerFlags=BindingFlags.NonPublic|BindingFlags.Instance;
        GameManager.instance.OpenAwayPlanner();yield return new WaitForSecondsRealtime(2);
        until=Time.realtimeSinceStartup+60;PlanAwayTripController planner=null;
        while(!(planner=UnityEngine.Object.FindFirstObjectByType<PlanAwayTripController>(FindObjectsInactive.Include))&&Time.realtimeSinceStartup<until)yield return null;
        if(!planner)throw new Exception("Away planner did not load for commerce fixture.");
        typeof(PlanAwayTripController).GetMethod("SelectDestination",plannerFlags).Invoke(planner,new object[]{planner.destinations.FindIndex(x=>x.name=="Riverside")});
        typeof(PlanAwayTripController).GetMethod("OnStartTrip",plannerFlags).Invoke(planner,null);yield return new WaitForSecondsRealtime(2);
        until=Time.realtimeSinceStartup+100;while((CityGameplay.HomeMode||!CityBusinessSystem.Instance||CityBusinessSystem.Instance.Sites.Count!=3||!BattleManager.instance.BattleActive)&&Time.realtimeSinceStartup<until)yield return null;
        d=GameManager.Data;business=CityBusinessSystem.Instance;commerce=CityCommerceSystem.Instance;selection=AgentSelectionManager.instance;
        foreach(var director in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))director.enabled=false;
        foreach(var enemy in BattleManager.instance.EnemyAgents)if(enemy){enemy.SetCinematicIdle(true);enemy.isHostile=false;}
        if(LivePoliceSystem.Instance){LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;}
        Check(!CityGameplay.HomeMode&&business.Sites.Count==3&&business.Sites.All(s=>s.Definition.id.StartsWith("M2:")),"Actual Away travel creates three independently scoped Riverside venues");
        var awaySite=business.Sites.First(s=>s.Definition.id=="M2:burger");brick=BattleManager.instance.PlayerAgents.First();selection.DeselectAll();selection.Select(brick);Time.timeScale=4;
        Check(!CityBusinessLedger.Find(d,awaySite.Definition.id).controlled&&CityBusinessLedger.Find(d,"burger").controlled,"Entering Away does not copy Home shop ownership into the new district");
        Check(business.TryAssign(awaySite,false),"Selected Away travellers can take a local shop-control assignment");
        until=Time.realtimeSinceStartup+100;while(business.HasAssignment(awaySite)&&Time.realtimeSinceStartup<until)yield return null;
        Check(CityBusinessLedger.Find(d,awaySite.Definition.id).controlled,"Away venue control requires actual arrival and timed local work");
        d.CitySupplies=2;Check(business.TryInvest(awaySite),"Away resource budget can fund a local venue improvement");
        until=Time.realtimeSinceStartup+100;while(business.HasAssignment(awaySite)&&Time.realtimeSinceStartup<until)yield return null;
        Check(CityBusinessLedger.Find(d,awaySite.Definition.id).investmentLevel==1,"Away crew work completes a saved local improvement");
        cash=d.Money;earned=d.BusinessIncomeEarned;
        Check(business.TryAssign(awaySite,true),"Away crew can deliberately raid the district shop");
        until=Time.realtimeSinceStartup+100;while(business.HasAssignment(awaySite)&&Time.realtimeSinceStartup<until)yield return null;
        var awayState=CityBusinessLedger.Find(d,awaySite.Definition.id);
        Check(awayState.lootCooldownSeconds>0&&d.Money==cash+awaySite.Definition.loot+(d.BusinessIncomeEarned-earned),"Physical Away loot pays once with its own cooldown and trade disruption");
        Check(CityBusinessLedger.Find(d,"burger").lootCooldownSeconds==0&&CityBusinessLedger.Find(d,"burger").investmentLevel==1,"Away looting leaves Home cooldown and Home investment untouched");
        GameManager.Save();Time.timeScale=1;commerce.Open(awaySite);CameraPanTouchOnly.Instance.FocusOn(awaySite.transform.position);yield return new WaitForSecondsRealtime(.5f);ScreenCapture.CaptureScreenshot("Artifacts/Recovery/commerce-away.png");yield return null;commerce.Close();
        GameManager.instance.EnterHomeTerritory();yield return new WaitForSecondsRealtime(2);
        until=Time.realtimeSinceStartup+100;while((!CityGameplay.HomeMode||!CityBusinessSystem.Instance||CityBusinessSystem.Instance.Sites.Count!=6||!BattleManager.instance.BattleActive)&&Time.realtimeSinceStartup<until)yield return null;
        d=GameManager.Data;Check(CityGameplay.HomeMode&&CityBusinessSystem.Instance.Sites.Count==6&&CityBusinessLedger.Find(d,"burger").investmentLevel==1&&CityBusinessLedger.Find(d,"M2:burger").investmentLevel==1&&CityBusinessLedger.Find(d,"M2:burger").lootCooldownSeconds>0,"Actual Home return retains both districts' independent venue progression and raid state");
        GameManager.Save();loaded=SaveDataInLocal.DataLoad();Check(CityBusinessLedger.Find(loaded,"M2:burger").controlled&&CityBusinessLedger.Find(loaded,"M2:burger").investmentLevel==1&&CityBusinessLedger.Find(loaded,"M2:burger").lootCooldownSeconds>0,"Native save after returning Home preserves Away venue ownership, improvement and cooldown");
        GameManager.instance.OpenAwayPlanner();yield return new WaitForSecondsRealtime(2);until=Time.realtimeSinceStartup+60;planner=null;
        while(!(planner=UnityEngine.Object.FindFirstObjectByType<PlanAwayTripController>(FindObjectsInactive.Include))&&Time.realtimeSinceStartup<until)yield return null;
        if(!planner)throw new Exception("Away planner did not reload for revisit fixture.");
        typeof(PlanAwayTripController).GetMethod("SelectDestination",plannerFlags).Invoke(planner,new object[]{planner.destinations.FindIndex(x=>x.name=="Riverside")});
        typeof(PlanAwayTripController).GetMethod("OnStartTrip",plannerFlags).Invoke(planner,null);yield return new WaitForSecondsRealtime(2);
        until=Time.realtimeSinceStartup+100;while((CityGameplay.HomeMode||!CityBusinessSystem.Instance||CityBusinessSystem.Instance.Sites.Count!=3||!BattleManager.instance.BattleActive)&&Time.realtimeSinceStartup<until)yield return null;
        d=GameManager.Data;awayState=CityBusinessLedger.Find(d,"M2:burger");
        Check(!CityGameplay.HomeMode&&awayState.controlled&&awayState.investmentLevel==1&&awayState.lootCooldownSeconds>0,"A real Riverside revisit restores its existing control, investment and raid cooldown");
        Check(GameObject.Find("BUSINESS EXPANSION LEVEL 2"),"Revisited Away venue rebuilds its visible saved expansion");
    }
}
public sealed class CommerceTestRunner:MonoBehaviour{}
#endif
