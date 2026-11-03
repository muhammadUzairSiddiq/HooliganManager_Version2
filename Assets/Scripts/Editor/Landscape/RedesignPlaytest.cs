#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RedesignPlaytest
{
    const string Key="HM.RedesignPlaytest",Report="Artifacts/Recovery/redesign-playtest.txt",Save="Artifacts/Recovery/redesign-save.json";
    static readonly List<string> rows=new List<string>();
    static bool running,finished;
    static double deadline;
    static RedesignPlaytest()
    {
        if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;
    }
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new Exception("Start from edit mode");
        RedesignAssets.Ensure();
        Directory.CreateDirectory("Artifacts/Recovery");SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var d=new PlayerData{PlayerName="Redesign QA",Money=5000,Fans=3};RecoveryProgression.Begin(d);
        foreach(var name in new[]{"Brick","Dex","Ash"})d.RecruitedAgents.Add(new AgentData(name,hp:300,speed:12));
        SaveDataInLocal.DataSave(d);SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING");EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange change)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(change==PlayModeStateChange.EnteredPlayMode)
        {running=finished=false;rows.Clear();deadline=EditorApplication.timeSinceStartup+120;CityGameplay.HomeMode=true;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("Gameplay");}
        if(change==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");
            int marker=SessionState.GetInt(Key+".marker",-1);if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(rows.Any(r=>r.StartsWith("FAIL "))?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Watchdog expired");return;}
        if(running||!ModernCityHud.Instance||!HomeTerritorySystem.Instance||!BattleManager.instance?.BattleActive==true)return;
        running=true;deadline=EditorApplication.timeSinceStartup+360;
        var host=new GameObject("Redesign test runner");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<RedesignTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator test)
    {
        while(true)
        {
            object next=null;bool more=false;try{more=test.MoveNext();if(more)next=test.Current;}catch(Exception e){Finish(e.ToString());yield break;}
            if(!more){Finish(null);yield break;}yield return next;
        }
    }
    static void Check(bool value,string title){rows.Add((value?"PASS ":"FAIL ")+title);File.WriteAllLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);
        rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add("Isolated native fixture: accelerated time and crew speed; ambient pressure suppressed for construction; not a complete natural campaign or device test.");
        File.WriteAllLines(Report,rows);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(1);
        var d=GameManager.Data;var city=CityGameplay.Instance;var home=HomeTerritorySystem.Instance;var hud=ModernCityHud.Instance;var selection=AgentSelectionManager.instance;
        foreach(var director in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))director.enabled=false;
        foreach(var enemy in BattleManager.instance.EnemyAgents)if(enemy){enemy.SetCinematicIdle(true);enemy.isHostile=false;}
        LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;
        Check(GameObject.Find("HOME TERRITORY - permanent base")&&home.Sites.Length==3,"Home has a named highlighted world area and three construction plots");
        Check(!HomeTerritorySystem.Built(d,0)&&!d.LearnedSelection,"Fresh Home starts unbuilt and waits for deliberate control");
        Check(hud.CurrentInstruction.Contains("Press CREW"),"The opening explains which button selects the first crew");
        var frame=UnityEngine.Object.FindFirstObjectByType<LandscapeBattleHUD>().frame;
        foreach(var name in new[]{"Move","Attack","Guard","Patrol","Retreat"})Check(!frame.Find("BattleHUD/"+name).gameObject.activeInHierarchy,name+" is absent from a permanent command row");
        Check(!ModernCityHud.InspectWorld,"City opens with optional map annotations hidden");
        Check(!home.BeginBuild(0),"Construction requires the player's selected crew");
        var brick=BattleManager.instance.PlayerAgents[0];var dex=BattleManager.instance.PlayerAgents[1];
        selection.Select(brick);selection.CommandSelectedMoveTo(city.Home+Vector3.right*4);selection.CommandSelectedMoveTo(city.Home+Vector3.forward*4);selection.Select(dex);selection.Deselect(dex);
        Check(RecoveryProgression.OrientationComplete(d),"Real selection and repeated orders complete orientation");
        hud.ToggleOrders();Check(hud.OrdersOpen&&selection.SelectedAgents.Count==1&&AgentSelectionManager.BlocksWorldTap(),"One explained Orders menu retains selection and blocks orders through the sheet");hud.ToggleOrders();
        var gestures=RtsGestureController.Instance;
        Action<int> chooseOrder=index=>{hud.ToggleOrders();frame.Find("ContextOrders/Order"+index).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();};
        gestures.BeginGuard();chooseOrder(0);
        Check(gestures.Mode==RtsGestureController.InputMode.Camera&&selection.PendingCommand==AgentSelectionManager.TargetCommand.Move&&selection.SelectedAgents.Count==1,"Choosing Move replaces a pending guard gesture without changing selection");
        gestures.BeginPatrol();chooseOrder(1);
        Check(gestures.Mode==RtsGestureController.InputMode.Camera&&selection.PendingCommand==AgentSelectionManager.TargetCommand.Attack,"Choosing Attack replaces a pending patrol gesture");
        chooseOrder(2);
        Check(gestures.Mode==RtsGestureController.InputMode.GuardArea&&selection.PendingCommand==AgentSelectionManager.TargetCommand.Move,"Choosing Guard clears the previous attack target mode");
        gestures.Cancel();
        Check(gestures.Mode==RtsGestureController.InputMode.Camera&&selection.PendingCommand==AgentSelectionManager.TargetCommand.Move,"Cancelling targeting restores ordinary ground movement");
        selection.CommandSelectedAttack();chooseOrder(3);
        Check(gestures.Mode==RtsGestureController.InputMode.PatrolA&&selection.PendingCommand==AgentSelectionManager.TargetCommand.Move,"Choosing Patrol clears the previous attack target mode");
        selection.CommandSelectedAttack();chooseOrder(4);
        Check(gestures.Mode==RtsGestureController.InputMode.Camera&&selection.PendingCommand==AgentSelectionManager.TargetCommand.Move&&selection.SelectedAgents.Count==1,"Regroup clears gesture and attack targeting while retaining selection");
        Check(home.BeginBuild(0),"Free headquarters starts as an on-map crew assignment");
        Check(CityBusinessSystem.HorizontalDistance(brick.CommandDestination,home.Sites[0])<HomeTerritorySystem.WorkRadius,"Construction accepts an actual route endpoint inside the plot work radius");
        int cash=d.Money;Time.timeScale=5;bool workedWhileTravelling=false;float until=Time.time+60;while(home.IsBuilding&&Time.time<until){workedWhileTravelling|=home.Progress>0&&brick.CurrentState==AgentController.State.MovingToPoint;yield return null;}
        Check(!workedWhileTravelling,"Construction waits for arrival instead of starting as crew pass through the plot");
        Check(HomeTerritorySystem.Built(d,0)&&d.Money==cash,"Crew arrival and timed work establish headquarters once without cost");
        Check(!home.BeginBuild(0),"A completed headquarters cannot rebuild or replay its outcome");
        Check(home.BeginBuild(1),"Supply yard can be built after establishing headquarters");
        selection.CommandSelectedMoveTo(city.Home);yield return null;yield return null;
        Check(!home.IsBuilding&&!HomeTerritorySystem.Built(d,1)&&d.Money==cash,"Manual movement cancels construction without payment");
        Check(home.BeginBuild(1),"A cancelled supply-yard assignment can be restarted by the selected crew");until=Time.time+80;while(home.IsBuilding&&Time.time<until)yield return null;
        if(!HomeTerritorySystem.Built(d,1))rows.Add($"DETAIL supply: {home.Status}; position={brick.transform.position}; plot={home.Sites[1]}; destination={brick.CommandDestination}; state={brick.CurrentState}; failure={brick.LastOrderFailure}; progress={home.Progress:0.0}");
        Check(HomeTerritorySystem.Built(d,1)&&d.Money==cash-300,"Supply yard charges once after physical construction");
        CityDevelopmentLedger.Ensure(d);Check(d.CityDevelopment.First(p=>p.site==1).level>=1,"Built supply yard activates the real supply economy");
        selection.DeselectAll();selection.Select(dex);selection.ArmGuardCommand();selection.CommandSelectedGround(city.Home);var duty=dex.CurrentOrder;
        selection.DeselectAll();selection.Select(brick);home.BeginBuild(2);until=Time.time+80;while(home.IsBuilding&&Time.time<until)yield return null;
        Check(HomeTerritorySystem.Built(d,2)&&d.HomeTrainingLevel>=1,"Training ground adds permanent crew development");
        Check(dex.CurrentOrder==duty&&duty==AgentController.StandingOrder.Guard,"Construction leaves a different group's guard duty intact");
        GameManager.Save();var loaded=SaveDataInLocal.DataLoad();Check(HomeTerritorySystem.BuiltCount(loaded)==3,"All three Home facilities survive the native save roundtrip");
        Check(GameObject.Find("BUILT FIRM HEADQUARTERS")&&GameObject.Find("BUILT SUPPLY YARD")&&GameObject.Find("BUILT TRAINING GROUND"),"Completed facilities have visible reused world models");
        int beforeRecruit=d.Money;int oldCrew=BattleManager.instance.PlayerAgents.Count;
        Check(home.RecruitOne()&&d.Money==beforeRecruit-300&&BattleManager.instance.PlayerAgents.Count==oldCrew+1,"One deliberate HQ recruitment charges once and adds a usable crew member");
        Time.timeScale=1;yield return new WaitForSeconds(.3f);hud.CloseSheet();hud.FocusHome();yield return new WaitForSeconds(.3f);ScreenCapture.CaptureScreenshot("Artifacts/Recovery/redesign-home-world.png");yield return null;hud.OpenHome();yield return null;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/redesign-home-building.png");yield return null;hud.CloseSheet();
        hud.OpenManagement();yield return null;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/redesign-management.png");yield return null;hud.CloseSheet();
        hud.ToggleOrders();yield return null;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/redesign-orders.png");yield return null;
    }
}
public sealed class RedesignTestRunner:MonoBehaviour{}
#endif
