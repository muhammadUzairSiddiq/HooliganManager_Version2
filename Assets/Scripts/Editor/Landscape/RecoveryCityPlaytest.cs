#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Native integration with disposable saves. Fixture setup is reported explicitly.</summary>
[InitializeOnLoad]
public static class RecoveryCityPlaytest
{
    const string Key="HM.RecoveryCityPlaytest", Report="Artifacts/Recovery/city-integration-checks.txt";
    const string Save="Artifacts/Recovery/city-integration-save.json";
    static readonly List<string> rows=new List<string>();
    static double deadline;
    static bool running,finished;
    static RecoveryCityPlaytest()
    {
        if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        EditorApplication.playModeStateChanged+=StateChanged;EditorApplication.update+=Tick;
    }
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    [MenuItem("Hooligan/Recovery/Run city integration playtest")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run from edit mode.");
        Directory.CreateDirectory("Artifacts/Recovery");
        SessionState.SetString(Key+".override",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetBool(Key+".marker",PlayerPrefs.HasKey(GameCacheBootstrap.InstallMarkerKey));
        PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var d=new PlayerData{PlayerName="Recovery QA",Money=12000,Fans=5,AcceptedPrivacyPolicy=true};
        RecoveryProgression.Begin(d);
        foreach(string name in new[]{"Brick","Dex","Ash","Finn","Lee"})d.RecruitedAgents.Add(new AgentData(name,hp:500,speed:24));
        SaveDataInLocal.DataSave(d);SessionState.SetBool(Key,true);
        File.WriteAllText(Report,"RUNNING "+DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=true;
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            rows.Clear();running=false;finished=false;deadline=EditorApplication.timeSinceStartup+120;
            LandscapeSceneBuilder.SetGameViewSize("1280x720");
            CityGameplay.HomeMode=true;SceneManager.LoadScene("Gameplay");
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".override","");
            if(!SessionState.GetBool(Key+".marker",false))PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(File.ReadAllText(Report).Contains("FAIL ")?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Integration watchdog expired.");return;}
        if(running||!Ready())return;
        running=true;deadline=EditorApplication.timeSinceStartup+780;
        var host=new GameObject("Persistent recovery QA");UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<RecoveryCityTestRunner>().StartCoroutine(Observe(Run()));
    }
    static bool Ready()=>BattleManager.instance&&BattleManager.instance.BattleActive&&CityGameplay.Instance&&
        CityGameplay.Instance.Locations?.Length>7&&CityBusinessSystem.Instance&&AgentSelectionManager.instance;
    static IEnumerator Observe(IEnumerator routine)
    {
        while(true)
        {
            bool more=false;object next=null;
            try{more=routine.MoveNext();if(more)next=routine.Current;}
            catch(Exception error){Finish(error.ToString());yield break;}
            if(!more){Finish(null);yield break;}yield return next;
        }
    }
    static void Check(bool ok,string title){rows.Add((ok?"PASS ":"FAIL ")+title);File.WriteAllLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;
        if(error!=null)rows.Add("FAIL "+error);
        rows.Add($"RESULT {rows.Count(x=>x.StartsWith("PASS "))} passed, {rows.Count(x=>x.StartsWith("FAIL "))} failed");
        rows.Add("Fixture: five durable fast crew; ambient pressure disabled for economy isolation. Later mission prerequisites are seeded for extraction/return checks, not claimed as a played campaign.");
        rows.Add("UTC "+DateTime.UtcNow.ToString("O"));File.WriteAllLines(Report,rows);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static void Isolate()
    {
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();CityUiSheets.CloseCurrent();
        foreach(var director in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))director.enabled=false;
        if(LivePoliceSystem.Instance){LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;}
        foreach(var enemy in BattleManager.instance.EnemyAgents)if(enemy){enemy.SetCinematicIdle(true);enemy.isHostile=false;}
    }
    static void Warp(AgentController agent,Vector3 point)
    {var nav=agent.GetComponent<NavMeshAgent>();if(!nav.Warp(point))throw new Exception("QA warp failed at "+point);}
    static void Phase(StadiumMatchdayActivity stadium,string name,float elapsed)
    {
        var field=typeof(StadiumMatchdayActivity).GetField("phase",BindingFlags.NonPublic|BindingFlags.Instance);
        field.SetValue(stadium,Enum.Parse(field.FieldType,name));
        typeof(StadiumMatchdayActivity).GetField("phaseClock",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(stadium,0f);
        GameManager.Data.MatchdayPhase=name;GameManager.Data.MatchdayElapsedSeconds=elapsed;
    }
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot("Artifacts/Recovery/"+name+".png");
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(1f);Isolate();
        var d=GameManager.Data;var city=CityGameplay.Instance;var business=CityBusinessSystem.Instance;
        var select=AgentSelectionManager.instance;var brick=BattleManager.instance.PlayerAgents[0];var dex=BattleManager.instance.PlayerAgents[1];
        StadiumMatchdayActivity.Instance.AutomaticMatchdaysEnabled=false;
        Check(!StadiumMatchdayActivity.Instance.HasStarted&&!MatchdayStrategySystem.Instance.IsOpen&&!GamePopup.AnyOpen,"Legacy manual fixture starts with no Matchday or popup");
        Check(!d.LearnedSelection&&!d.LearnedMultiSelection&&select.SelectedAgents.Count==0,"Fresh startup does not complete selection lessons automatically");
        Check(business.Sites.Count==6&&business.Sites.All(s=>s.Storefront)&&business.Sites.Select(s=>s.Storefront).Distinct().Count()==6,"Six business archetypes are associated with distinct existing storefronts");
        var path=new NavMeshPath();
        foreach(var point in city.Locations.Concat(business.Sites.Select(s=>s.transform.position)))
        {
            Check(NavMesh.CalculatePath(city.Home,point,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Complete mandatory path from HQ to "+point);
            Check(NavMesh.CalculatePath(point,city.Home,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Complete return path to HQ from "+point);
        }
        CameraPanTouchOnly.Instance.FocusOn(city.Home);yield return new WaitForSeconds(.5f);Capture("recovery-home");yield return null;
        business.OpenLedger();Check(business.IsOpen,"Business ledger opens deliberately");
        CityOperationsSystem.Instance.OpenBoard();Check(!business.IsOpen&&CityOperationsSystem.Instance.IsBoardOpen,"Opening operations closes the previous business sheet");
        MatchdayStrategySystem.Instance.OpenBoard();Check(!CityOperationsSystem.Instance.IsBoardOpen&&MatchdayStrategySystem.Instance.IsOpen,"Matchday sheet replaces operations without stacked panels");
        MatchdayStrategySystem.Instance.CloseBoard();
        var site=business.Sites.OrderBy(s=>Vector3.Distance(s.transform.position,brick.transform.position)).First();
        var bubble=site.GetComponentInChildren<WorldInteractBubble>(true);
        Check(bubble&&bubble.IsLive&&!site.GetComponent<WorldChoiceBar>(),"Showing an available business interaction does not execute it");
        bubble.Activate();Check(site.GetComponent<WorldChoiceBar>().IsOpen,"A deliberate venue tap opens its local choices");WorldChoiceBar.DismissCurrent();
        select.DeselectAll();select.ToggleSelect(brick);
        Check(business.TryAssign(site,false)&&business.HasAssignment(site),"Selected crew accepts a timed business control assignment");
        var position=brick.transform.position;yield return new WaitForSeconds(.6f);
        Check(Vector3.Distance(position,brick.transform.position)>1f,"Business assignment moves the actual character along the street");
        Time.timeScale=5;float until=Time.time+100;
        while(business.HasAssignment(site)&&Time.time<until)yield return null;
        Check(CityBusinessLedger.Find(d,site.Definition.id).controlled&&!business.HasAssignment(site),"Control completes from arrival and on-site presence");
        Check(brick.IsSelected,"Business control keeps crew selected");
        int earned=d.BusinessIncomeEarned;until=Time.time+92;
        while(d.BusinessIncomeEarned==earned&&Time.time<until)yield return null;
        Check(d.BusinessIncomeEarned>earned,"Native city Update pays controlled business income after active simulation time");
        var second=business.Sites.First(s=>s!=site);
        Check(business.TryAssign(second,false),"A second business assignment starts");
        select.CommandSelectedMoveTo(city.Home);yield return null;yield return null;
        Check(!business.HasAssignment(second)&&!CityBusinessLedger.Find(d,second.Definition.id).controlled,"A manual MOVE cancels business work without awarding ownership");
        Warp(dex,site.transform.position);select.DeselectAll();select.ToggleSelect(dex);select.ArmGuardCommand();select.CommandSelectedGround(site.transform.position);
        var oldOrder=dex.CurrentOrder;select.DeselectAll();select.ToggleSelect(brick);
        Check(business.TryAssign(second,true),"A separate selected group can loot while the defender keeps its duty");
        until=Time.time+100;while(business.HasAssignment(second)&&Time.time<until)yield return null;
        var raided=CityBusinessLedger.Find(d,second.Definition.id);
        rows.Add($"NAV TRACE loot completed at {brick.transform.position}, target {second.Definition.id} {second.transform.position}, order {brick.CommandDestination}, state {brick.CurrentState}");
        Check(raided.lootCooldownSeconds>0&&raided.disruptionSeconds>0,"Native on-site loot sets saved cooldown and trade disruption");
        Check(dex.CurrentOrder==oldOrder&&oldOrder==AgentController.StandingOrder.Guard,"Loot does not overwrite another group's GUARD");
        int cash=d.Money;Check(!business.TryAssign(second,true)&&d.Money==cash,"Duplicate loot is rejected without paying twice");
        GameManager.Save();var saved=SaveDataInLocal.DataLoad();
        Check(CityBusinessLedger.Find(saved,site.Definition.id).controlled&&CityBusinessLedger.Find(saved,second.Definition.id).lootCooldownSeconds>0,"Native save keeps ownership and loot cooldown after world actions");
        var rival=BattleManager.instance.EnemyAgents.First(e=>e&&e.IsAlive&&e.firmName!="POLICE"&&!e.IsAmbientMatchdayUnit);
        var rivalNav=rival.GetComponent<NavMeshAgent>();var rivalPosition=rival.transform.position;
        rival.SetCinematicIdle(true);rivalNav.Warp(second.transform.position);rival.isHostile=true;
        Check(business.TryAssign(second,false),"A control assignment can approach a contested venue");
        yield return new WaitForSeconds(4);
        Check(business.AssignmentSeconds(second)==0&&!CityBusinessLedger.Find(d,second.Definition.id).controlled,"Hostile presence blocks business control progress");
        rival.isHostile=false;rivalNav.Warp(rivalPosition);
        until=Time.time+30;while(business.HasAssignment(second)&&Time.time<until)yield return null;
        Check(CityBusinessLedger.Find(d,second.Definition.id).controlled,"Control resumes when the hostile contest leaves");
        dex.SetCinematicIdle(true);rivalNav.Warp(site.transform.position);rival.isHostile=true;
        yield return new WaitForSeconds(5);
        Check(CityBusinessLedger.Find(d,site.Definition.id).controlled&&CityBusinessLedger.Find(d,site.Definition.id).rivalPressureSeconds==0,"A living venue defender prevents hostile takeover pressure");
        Warp(dex,city.Home);yield return new WaitForSeconds(27);
        Check(!CityBusinessLedger.Find(d,site.Definition.id).controlled,"An undefended venue is lost after the visible takeover window");
        rival.isHostile=false;rivalNav.Warp(rivalPosition);dex.SetCinematicIdle(false);
        select.DeselectAll();select.ToggleSelect(dex);business.TryAssign(site,false);
        until=Time.time+100;while(business.HasAssignment(site)&&Time.time<until)yield return null;
        Check(CityBusinessLedger.Find(d,site.Definition.id).controlled,"Lost business control can be recovered through a real crew assignment");
        Time.timeScale=1;business.OpenLedger();yield return null;Capture("recovery-businesses");yield return null;business.CloseLedger();
        rows.Add($"NAV TRACE after ledger {brick.transform.position}, state {brick.CurrentState}");

        d.LearnedSelection=d.LearnedMultiSelection=d.LearnedSplitSelection=true;d.LearnedMoveOrders=2;
        d.TransportPrepared=d.StadiumAccessPrepared=true;
        var stadium=StadiumMatchdayActivity.Instance;var match=MatchdayStrategySystem.Instance;
        Check(stadium.BeginMatchday(),"Prepared player explicitly starts matchday");
        d.MatchdayStreet=null; // Exercise an older in-progress coverage save.
        yield return new WaitForSeconds(1);
        var navProbe=brick.GetComponent<NavMeshAgent>();var probePath=new NavMeshPath();
        var probeFilter=new NavMeshQueryFilter{agentTypeID=navProbe.agentTypeID,areaMask=navProbe.areaMask};
        bool direct=navProbe.CalculatePath(match.ResponsePoints[0],probePath);string nativeStatus=probePath.status.ToString();
        bool global=NavMesh.CalculatePath(navProbe.nextPosition,match.ResponsePoints[0],probeFilter,probePath);
        rows.Add($"NAV PROBE from={brick.transform.position} nav={navProbe.nextPosition} target={match.ResponsePoints[0]} offLink={navProbe.isOnOffMeshLink} direct={direct}/{nativeStatus} global={global}/{probePath.status}");
        Isolate();stadium.enabled=false;select.DeselectAll();select.ToggleSelect(brick);match.Assign(0);
        Check(brick.CurrentOrder==AgentController.StandingOrder.Guard,"Matchday response uses actual GUARD orders (accepted="+select.LastAcceptedOrderCount+", reason="+brick.LastOrderFailure+")");
        if(brick.CurrentOrder!=AgentController.StandingOrder.Guard)rows.Add("DIAGNOSTIC "+city.EventText.Replace('\n',' '));
        Warp(brick,match.ResponsePoints[0]);select.DeselectAll();select.ToggleSelect(dex);Warp(dex,match.ResponsePoints[1]);match.Assign(1);
        Time.timeScale=5;until=Time.time+30;while((!match.ResponseComplete(0)||!match.ResponseComplete(1))&&Time.time<until)yield return null;
        Check(match.ResponseComplete(0)&&match.ResponseComplete(1),"Separate living crews cover both matchday responses through timed presence");
        Check(!d.MatchdayIncidentResolved&&CampaignMissions.ActionCount(d,1,"matchday")==0,"Early response coverage does not finish the chapter before aftermath");
        Phase(stadium,"Aftermath",420);yield return null;yield return null;
        Check(d.MatchdayIncidentResolved&&CampaignMissions.ActionCount(d,1,"matchday")==1,"Aftermath commits one matchday chapter outcome");
        cash=d.Money;yield return null;GameManager.Save();Check(d.Money==cash,"Resolved matchday does not replay response rewards on Save");
        // Exercise a missed window as a separate native boundary fixture.
        d.MatchdayCompletedResponses.Clear();d.MatchdayStadiumResponseSeconds=d.MatchdayPubResponseSeconds=0;d.MatchdayIncidentResolved=false;
        d.MatchdayFailedResponses.Clear();d.MatchdayPenalizedResponses.Clear();
        Phase(stadium,"Aftermath",420);select.DeselectAll();Warp(brick,city.Home);Warp(dex,city.Home);
        int morale=d.FanMorale;yield return null;yield return null;
        Check(d.MatchdayFailedResponses.Count==2&&d.FanMorale==morale-10,"Ignoring both response deadlines applies local consequences");
        morale=d.FanMorale;yield return null;Check(d.FanMorale==morale,"Missed deadline penalties apply once");
        stadium.RetryMissedResponses();Check(d.MatchdayFailedResponses.Count==0&&stadium.CurrentPhase=="RIVALPRESSURE","Aftermath offers a deliberate response retry without ads");
        Phase(stadium,"BuildUp",103);stadium.enabled=true;GameManager.Save();
        var groups=d.MatchdayGroups;Check(groups?.Count>0&&groups.All(g=>g.members.Count>0),"Active matchday serializes named group and member records");
        int groupCount=groups.Count;
        // Change the actual simulation records so scene-exit saves preserve them.
        var runtimeGroups=(IList)typeof(StadiumMatchdayActivity).GetField("groups",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(stadium);
        var firstGroup=runtimeGroups[0];
        var slots=(IList)firstGroup.GetType().GetField("Slots").GetValue(firstGroup);
        for(int i=0;i<2;i++)
        {
            var slot=slots[i];var type=slot.GetType();float hp=i==0?0:17;
            type.GetField("Hp").SetValue(slot,hp);
            var body=(EnemyController)type.GetField("Body").GetValue(slot);
            if(body)body.RestoreStreamedHealth(hp);
        }
        GameManager.Save();
        GameData.instance.PlayerData=SaveDataInLocal.DataLoad();SceneManager.LoadScene("Gameplay");yield return null;
        until=Time.realtimeSinceStartup+60;while(!Ready()&&Time.realtimeSinceStartup<until)yield return null;
        yield return new WaitForSeconds(1);Isolate();d=GameManager.Data;city=CityGameplay.Instance;business=CityBusinessSystem.Instance;
        Check(StadiumMatchdayActivity.Instance.CurrentPhase=="BUILDUP"&&d.MatchdayElapsedSeconds>=103,"Scene resume keeps matchday phase and elapsed time");
        StadiumMatchdayActivity.Instance.CaptureState();
        Check(d.MatchdayGroups.Count==groupCount&&d.MatchdayGroups[0].members[0].hp==0&&d.MatchdayGroups[0].members[1].hp<=17,"Continue keeps group count, casualties and injured member health");
        Check(CityBusinessLedger.Find(d,site.Definition.id).controlled,"Home business ownership survives a real scene reload");
        Time.timeScale=1;MatchdayStrategySystem.Instance.OpenBoard();yield return null;Capture("recovery-matchday");yield return null;MatchdayStrategySystem.Instance.CloseBoard();

        // Campaign prerequisites below are fixture setup; combat itself is covered by RTS tests.
        d.CurrentLevel=2;d.CompletedCampaignMissions.Add(1);d.Money=20000;
        d.SelectedAwayAgentIds=d.RecruitedAgents.Take(3).Select(a=>a.AgentId).ToList();d.DeploymentSelectionCustomized=true;
        d.SelectedAwayAgentIds.Clear();GameManager.Save();Check(d.SelectedAwayAgentIds.Count==0,"NONE remains an intentional empty preparation selection after Save");
        var travellerIds=d.RecruitedAgents.Take(3).Select(a=>a.AgentId).ToArray();d.SelectedAwayAgentIds=travellerIds.ToList();d.BusinessEconomySeconds=89;int incomeBeforeAway=d.BusinessIncomeEarned;GameManager.Save();
        SceneManager.LoadScene(GameManager.SCENE_DASHBOARD);yield return null;yield return new WaitForSeconds(1);
        var planner=UnityEngine.Object.FindFirstObjectByType<PlanAwayTripController>(FindObjectsInactive.Include);
        if(!planner)throw new Exception("Away planner missing from dashboard.");
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        typeof(PlanAwayTripController).GetMethod("SelectDestination",flags).Invoke(planner,new object[]{planner.destinations.FindIndex(x=>x.name=="Riverside")});
        d=GameManager.Data;cash=d.Money;
        typeof(PlanAwayTripController).GetMethod("OnStartTrip",flags).Invoke(planner,null);
        typeof(PlanAwayTripController).GetMethod("OnStartTrip",flags).Invoke(planner,null);
        Check(d.Money==cash-900,"Actual Away launch debits travel once despite a repeated launch callback");
        yield return new WaitForSeconds(2);until=Time.realtimeSinceStartup+60;
        while((!Ready()||CityGameplay.HomeMode)&&Time.realtimeSinceStartup<until)yield return null;
        Isolate();d=GameManager.Data;city=CityGameplay.Instance;select=AgentSelectionManager.instance;
        Check(!CityGameplay.HomeMode&&BattleManager.instance.PlayerAgents.Count==3&&BattleManager.instance.PlayerAgents.All(a=>travellerIds.Contains(a.Data.AgentId)),"Away scene deploys exactly the chosen three travellers");
        Check(CampaignMissions.HomeReserves(d)==2,"Nondeployed living members remain Home reserves");
        Check(d.BusinessIncomeEarned>incomeBeforeAway,"Home businesses continue earning simulation income while Away");
        Check(CampaignMissions.Steps(d).Length==6&&!CampaignMissions.Steps(d).Any(s=>new[]{"extort","taxi","sabotage"}.Contains(s.id)),"Riverside uses linked objectives with optional civilian crimes");
        AwayExtractionSystem.Instance.RequestExtraction();Check(!AwayExtractionSystem.Instance.IsExtracting,"Extraction refuses incomplete district goals");
        foreach(var step in CampaignMissions.Steps(d))if(step.operation.HasValue)CampaignMissions.CompleteOperation(d,step.operation.Value);
        CampaignMissions.RecordRival(d,"QA Riverside A");CampaignMissions.RecordRival(d,"QA Riverside B");CampaignMissions.RecordTerritory(d,"QA Foothold");
        select.DeselectAll();select.ToggleSelect(BattleManager.instance.PlayerAgents[0]);
        AwayExtractionSystem.Instance.RequestExtraction();Check(AwayExtractionSystem.Instance.IsExtracting&&select.SelectedAgents.Count==1,"Extraction request retains the player's chosen subgroup");
        foreach(var member in BattleManager.instance.PlayerAgents)Warp(member,city.Home);
        Time.timeScale=5;until=Time.time+12;while(!CampaignMissions.Completed(d,2)&&Time.time<until)yield return null;
        Check(CampaignMissions.Completed(d,2)&&d.CurrentLevel==3,"Physical extraction completes Riverside and unlocks North End");
        cash=d.Money;Check(!CampaignMissions.TryComplete(d,out _)&&d.Money==cash,"Completed Away chapter cannot pay its reward again");
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();GameManager.instance.EnterHomeTerritory();yield return new WaitForSeconds(2);
        until=Time.realtimeSinceStartup+60;while((!Ready()||!CityGameplay.HomeMode)&&Time.realtimeSinceStartup<until)yield return null;
        d=GameManager.Data;Check(CityGameplay.HomeMode&&CityBusinessLedger.Find(d,site.Definition.id).controlled&&CampaignMissions.Completed(d,1),"Actual return preserves Home ownership and completed Home chapter");
        Isolate();Time.timeScale=1;d.Money=0;
        foreach(var member in BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray())member.TakeDamage(99999);
        yield return null;GameManager.instance.RebuildAtHeadquarters();yield return new WaitForSecondsRealtime(2);
        until=Time.realtimeSinceStartup+60;while(!Ready()&&Time.realtimeSinceStartup<until)yield return null;
        d=GameManager.Data;
        Check(CityGameplay.HomeMode&&d.RecruitedAgents.Count(a=>a.IsAlive)>=3&&BattleManager.instance.PlayerAgents.Count(a=>a&&a.IsAlive)>=3,"A completely lost crew can rebuild a playable Home team without ads");
        Check(d.Money==0&&CampaignMissions.Completed(d,1)&&CityBusinessLedger.Find(d,site.Definition.id).controlled,"Zero-cash recovery preserves campaign and businesses without granting farmable cash");
    }
}
public sealed class RecoveryCityTestRunner : MonoBehaviour { }
#endif
