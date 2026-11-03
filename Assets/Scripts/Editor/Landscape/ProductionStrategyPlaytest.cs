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

/// <summary>Actual Home investment, matchday allocation and tutorial contracts in a disposable city.</summary>
[InitializeOnLoad]
public static class ProductionStrategyPlaytest
{
    const string Key="HM.ProductionStrategyPlaytest";
    const string Save="Artifacts/Recovery/production-strategy-save.json";
    const string Report="Artifacts/Recovery/production-strategy-playtest.txt";
    static readonly List<string> rows=new List<string>();
    static bool running,finished;
    static double deadline;
    static ProductionStrategyPlaytest()
    {
        if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;
    }
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    [MenuItem("Hooligan/Recovery/Run production strategy check")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run from edit mode.");
        Directory.CreateDirectory("Artifacts/Recovery");RedesignAssets.Ensure();
        SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));
        PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var data=new PlayerData{PlayerName="Production Strategy QA",Money=20000,Fans=5};RecoveryProgression.Begin(data);
        foreach(var name in new[]{"Builder","Stadium Crew","Pub Crew","Stall Crew","Escort Crew"})data.RecruitedAgents.Add(new AgentData(name,hp:1000,speed:18));
        SaveDataInLocal.DataSave(data);SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING "+DateTime.UtcNow.ToString("O"));
        // Runtime scene changes preserve the scene currently open in the editor.
        EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {rows.Clear();running=finished=false;deadline=EditorApplication.timeSinceStartup+150;CityGameplay.HomeMode=true;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("Gameplay");}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");
            int marker=SessionState.GetInt(Key+".marker",-1);if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(rows.Any(r=>r.StartsWith("FAIL "))?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Production strategy watchdog expired.");return;}
        if(running||!Ready())return;
        running=true;deadline=EditorApplication.timeSinceStartup+900;
        var host=new GameObject("Production Strategy Test Runner");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<ProductionStrategyTestRunner>().StartCoroutine(Observe(Run()));
    }
    static bool Ready()=>GameManager.instance&&GameData.instance&&BattleManager.instance&&BattleManager.instance.BattleActive&&ModernCityHud.Instance&&HomeTerritorySystem.Instance&&MatchdayStrategySystem.Instance;
    static IEnumerator Observe(IEnumerator routine)
    {
        while(true){bool more=false;object current=null;try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception error){Finish(error.ToString());yield break;}if(!more){Finish(null);yield break;}yield return current;}
    }
    static void Check(bool ok,string label){rows.Add((ok?"PASS ":"FAIL ")+label);RecoveryReport.WriteLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);
        rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add("Native disposable fixture: five durable fast crew; accelerated time; unrelated rival/police pressure suppressed. Matchday clock frozen after explicit start to isolate allocation, escort and staffing, then resumed through its real aftermath timer. Later preparation is seeded; not a natural campaign, device touch, performance or client-acceptance test.");
        rows.Add("UTC "+DateTime.UtcNow.ToString("O"));RecoveryReport.WriteLines(Report,rows);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static IEnumerator Until(Func<bool> complete,float seconds=60)
    {float until=Time.realtimeSinceStartup+seconds;while(!complete()&&Time.realtimeSinceStartup<until)yield return null;}
    static bool Near(AgentController actor,Vector3 point,float radius=7)=>actor&&CityBusinessSystem.HorizontalDistance(actor.transform.position,point)<radius;
    static void Select(params AgentController[] actors)
    {var selection=AgentSelectionManager.instance;selection.DeselectAll();foreach(var actor in actors)selection.Select(actor);}
    static void Isolate()
    {
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();CityUiSheets.CloseCurrent();
        foreach(var rival in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))rival.enabled=false;
        if(LivePoliceSystem.Instance){LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;}
        foreach(var actor in BattleManager.instance.EnemyAgents)if(actor){actor.SetCinematicIdle(true);actor.isHostile=false;}
    }
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(2);Isolate();Time.timeScale=5;
        var data=GameManager.Data;var city=CityGameplay.Instance;var home=HomeTerritorySystem.Instance;var hud=ModernCityHud.Instance;var select=AgentSelectionManager.instance;
        var crew=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();
        Check(crew.Length==5&&!StadiumMatchdayActivity.Instance.HasStarted,"Fresh fixture has a viable crew and waits for a deliberate matchday start");
        if(crew.Length<5)yield break;
        Check(FirstSessionGuide.TryLesson(data,out var lesson)&&lesson.step==1&&lesson.button=="OPEN CREW","First lesson names the actual CREW button and waits for selection");
        hud.OpenCrew();yield return null;hud.CloseSheet();
        Check(!data.LearnedSelection&&FirstSessionGuide.TryLesson(data,out lesson)&&lesson.step==1,"Opening the crew sheet alone cannot complete the selection lesson");
        select.Select(crew[0]);select.CommandSelectedMoveTo(CityGameplay.ReachableApproach(city.Home,city.Home+Vector3.right*4));select.CommandSelectedMoveTo(CityGameplay.ReachableApproach(city.Home,city.Home+Vector3.forward*4));select.Select(crew[1]);select.Deselect(crew[1]);
        Check(RecoveryProgression.OrientationComplete(data)&&FirstSessionGuide.TryLesson(data,out lesson)&&lesson.step==5,"Real selection, repeated movement and group split advance to the Home construction lesson");
        Select(crew[0]);int cash=data.Money;
        Check(home.BeginBuild(0),"The tutorial's headquarters action assigns real selected crew");
        yield return Until(()=>home.Progress>2||!home.IsBuilding);
        var ghost=GameObject.Find("HOME CONSTRUCTION IN PROGRESS");float earlyHeight=ghost?ghost.transform.localScale.y:0;
        yield return new WaitForSeconds(3);
        Check(home.IsBuilding&&ghost&&ghost.transform.localScale.y>earlyHeight,"On-site work visibly grows the construction model before completion");
        yield return Until(()=>!home.IsBuilding);
        Check(HomeTerritorySystem.Level(data,0)==1&&data.Money==cash,"Headquarters completes once without a cash charge");
        Check(FirstSessionGuide.TryLesson(data,out lesson)&&lesson.step==6,"Finished headquarters leads to a specific supply-production lesson");
        Check(home.BeginBuild(1),"Selected crew starts the supply yard in the world");yield return Until(()=>!home.IsBuilding);
        Check(HomeTerritorySystem.Level(data,1)==1&&data.Money==cash-300,"Supply yard consumes its declared investment only after construction");
        Check(home.BeginBuild(2),"Training ground can be developed after the headquarters");yield return Until(()=>!home.IsBuilding);
        Check(HomeTerritorySystem.Level(data,2)==1&&data.HomeTrainingLevel==1&&!FirstSessionGuide.TryLesson(data,out lesson),"All three developed facilities complete the short coach through actual outcomes");
        data.CitySupplies=20;cash=data.Money;int upgradeCost=HomeTerritorySystem.Cost(data,1);
        Check(home.BeginBuild(1),"A completed base permits a second-level production investment");
        yield return Until(()=>home.Progress>4||!home.IsBuilding);
        float savedWork=home.Progress;GameManager.Save();var loaded=SaveDataInLocal.DataLoad();
        Check(savedWork>0&&loaded.HomeBuildings.Any(b=>b.site==1&&b.constructing&&b.targetLevel==2&&b.workSeconds>=savedWork-.1f&&b.workerIds.Contains(crew[0].Data.AgentId)),"Native save retains unfinished investment, work progress and actual builder identity");
        string builderId=crew[0].Data.AgentId;int oldHomeId=home.GetInstanceID();Time.timeScale=1;
        GameData.instance.PlayerData=loaded;SceneManager.LoadScene("Gameplay");
        yield return Until(()=>Ready()&&HomeTerritorySystem.Instance.GetInstanceID()!=oldHomeId,100);yield return new WaitForSecondsRealtime(3);Isolate();
        data=GameManager.Data;city=CityGameplay.Instance;home=HomeTerritorySystem.Instance;hud=ModernCityHud.Instance;select=AgentSelectionManager.instance;crew=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();
        if(!home||!home.IsBuilding||home.Progress<savedWork-.1f)
        {
            var resumeState=data.HomeBuildings.Find(b=>b.site==1);
            rows.Add($"DETAIL construction resume: status={home?.Status}; active={home?.IsBuilding}; progress={home?.Progress}; savedWork={savedWork}; level={resumeState.level}; requested={resumeState.constructing}; target={resumeState.targetLevel}; workers={string.Join(",",resumeState.workerIds)}; liveCrew={crew.Length}; expectedBuilderPresent={crew.Any(a=>a.Data.AgentId==builderId)}; cash={data.Money}; expectedBeforeCharge={cash}");
        }
        Check(home&&home.IsBuilding&&home.Progress>=savedWork-.1f,"A real scene reload resumes unfinished construction rather than charging or resetting it");
        Time.timeScale=5;yield return Until(()=>!home.IsBuilding);
        Check(HomeTerritorySystem.Level(data,1)==2&&data.Money==cash-upgradeCost,"Resumed investment completes to level two and debits its price once");
        Check(data.CityDevelopment.Find(p=>p.site==1).level>=2&&GameObject.Find("SUPPLY YARD upgrade 2"),"Production upgrade changes supply capacity and adds visible built-world development");
        Check(!data.HomeBuildings.Find(b=>b.site==1).constructing,"Completed investment clears its saved work assignment");

        // Verify the owner's explicit visibility rule using an actual screen-space touch.
        var visibility=RtsBuildingVisibility.Instance;var roads=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("road_")||r.name.StartsWith("railway_")&&!r.name.StartsWith("railway_station_")).ToArray();
        var roadStates=roads.Select(r=>(r.enabled,r.forceRenderingOff)).ToArray();
        Check(roads.Length>0&&!RtsBuildingVisibility.IsBuildingName("police_department_platform_001")&&!RtsBuildingVisibility.IsBuildingName("parking_001")&&!RtsBuildingVisibility.IsBuildingName("road_001"),"Roads, station platforms and parking are excluded from the building-only reveal rule");
        var fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.name="skyscraper_strategy_fixture";fixture.transform.position=Vector3.Lerp(Camera.main.transform.position,crew[0].transform.position+Vector3.up*2,.35f);fixture.transform.localScale=Vector3.one*6;
        var render=fixture.GetComponent<Renderer>();var registered=(List<Renderer>)typeof(RtsBuildingVisibility).GetField("buildings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(visibility);registered.Add(render);
        yield return new WaitForSeconds(.25f);
        Check(!render.forceRenderingOff,"A building is not automatically hidden just because a unit is behind it");
        var touch=Camera.main.WorldToScreenPoint(fixture.transform.position);
        Check(visibility.TryRevealAt(touch)&&render.forceRenderingOff&&fixture.GetComponent<Collider>().enabled,"An explicit building touch hides its rendering while retaining collision");
        Check(roads.Select((r,i)=>r&&r.enabled==roadStates[i].Item1&&r.forceRenderingOff==roadStates[i].Item2).All(v=>v),"The reveal leaves every sampled road and railway renderer unchanged");
        visibility.RestoreAll();UnityEngine.Object.Destroy(fixture);
        Check(!ModernCityHud.InspectWorld&&UnityEngine.Object.FindObjectsByType<SiteOutlineVisibility>(FindObjectsSortMode.None).All(s=>s.GetComponentsInChildren<LineRenderer>().All(l=>!l.enabled)),"Ordinary site footprints are hidden by default instead of dumping circles across the city");

        // Five people must be allocated between stadium, two pub staff, a stall and escort.
        var match=MatchdayStrategySystem.Instance;var stadium=StadiumMatchdayActivity.Instance;
        Check(match.EscortRouteMetres>=20&&match.EscortRouteMetres<=150&&match.EscortRouteMetres/(2.5f*.52f)+5<180,"The controlled supporter corridor fits a starter walker's matchday budget instead of requiring a whole-city march");
        // Supply production was verified through the upgraded ledger above;
        // suspend its periodic credits while asserting exact matchday debits.
        if(CityDevelopmentSystem.Instance)CityDevelopmentSystem.Instance.enabled=false;
        data.CitySupplies=30;data.TransportPrepared=data.StadiumAccessPrepared=true;data.LearnedGuard=data.LearnedPatrol=true;
        var tactics=MatchdayTactics.Ensure(data);Check(MatchdayTactics.Choose(data,2),"Player deliberately chooses the higher-staffing matchday trade approach");
        Select(crew[0]);match.Assign(0);var stadiumDuty=crew[0].CurrentOrder;
        Select(crew[1]);match.Assign(1);
        Check(!stadium.HasStarted&&crew[0].CurrentOrder==AgentController.StandingOrder.Guard&&crew[1].CurrentOrder==AgentController.StandingOrder.Guard,"Separate crews can pre-position before matchday starts without replacing each other's duty");
        Select(crew[3]);cash=data.Money;int supplies=data.CitySupplies;
        Check(match.BuildStall(),"A spare selected member accepts the optional visible matchday-stall investment");
        yield return Until(()=>tactics.stallBuilt||!tactics.stallBuildRequested,100);
        if(!tactics.stallBuilt||data.Money!=cash-250||data.CitySupplies!=supplies-3||!GameObject.Find("PLAYER BUILT MATCHDAY STALL"))
            rows.Add($"DETAIL stall: built={tactics.stallBuilt}; work={tactics.stallWorkSeconds}; cash={data.Money}; expectedCash={cash-250}; supplies={data.CitySupplies}; expectedSupplies={supplies-3}; model={!!GameObject.Find("PLAYER BUILT MATCHDAY STALL")}; territories={string.Join(",",data.CityCapturedZones??new List<string>())}");
        Check(tactics.stallBuilt&&data.Money==cash-250&&data.CitySupplies==supplies-3&&GameObject.Find("PLAYER BUILT MATCHDAY STALL"),"Actual travel and twenty seconds of work build the stall and consume its declared resources once");
        var pubTerritory=UnityEngine.Object.FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None).Where(t=>t&&t.zoneName.ToLowerInvariant().Contains("pub")).OrderBy(t=>Vector3.Distance(t.transform.position,match.ResponsePoints[1])).FirstOrDefault();
        Check(pubTerritory&&!pubTerritory.CaptureRequested&&!pubTerritory.IsCaptured&&data.Money==cash-250,"Pre-positioning a pub guard does not request territory capture or silently charge an occupation tax");
        Check(!match.BuildStall(),"An existing stall cannot replay its build charge");
        Select(crew[4]);Check(match.AssignEscort(),"A separate crew member can be assigned to meet arriving supporters");
        yield return Until(()=>Near(crew[0],match.ResponsePoints[0],9)&&Near(crew[1],match.ResponsePoints[1],9)&&Near(crew[4],match.EscortPickup,7),120);
        Check(Near(crew[0],match.ResponsePoints[0],9)&&Near(crew[1],match.ResponsePoints[1],9)&&Near(crew[4],match.EscortPickup,7),"Pre-positioned groups physically reach their separate stadium, pub and supporter pickup destinations");
        cash=data.Money;yield return new WaitForSeconds(24);
        Check(!stadium.HasStarted&&data.Money==cash&&!match.ResponseComplete(0)&&!match.ResponseComplete(1)&&tactics.stallIncomeSeconds==0,"Pre-positioning and an unattended calendar cannot auto-start matchday or pay coverage/trade rewards");
        // Remove stall staff before measuring coverage rewards and unattended trade.
        Select(crew[3]);select.CommandSelectedMoveTo(city.Home);yield return Until(()=>!Near(crew[3],match.StallPoint,12),40);
        cash=data.Money;supplies=data.CitySupplies;
        Check(stadium.BeginMatchday()&&data.Money==cash-150&&data.CitySupplies==supplies-2,"Explicit START commits the selected trade plan cost once");
        yield return new WaitForSeconds(1);Isolate();stadium.enabled=false;
        cash=data.Money;supplies=data.CitySupplies;
        Check(!stadium.BeginMatchday()&&data.Money==cash&&data.CitySupplies==supplies&&!MatchdayTactics.Choose(data,1),"Repeated START cannot charge again or change a committed matchday approach");
        stadium.OpenBriefing();yield return null;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/production-matchday-command.png");yield return null;match.CloseBoard();
        yield return new WaitForSeconds(23);
        Check(match.ResponseComplete(0)&&!match.ResponseComplete(1)&&data.MatchdayPubResponseSeconds==0,"One stadium guard covers its location while one pub member cannot satisfy the trade plan's two-person requirement");
        Check(tactics.stallIncomeSeconds==0,"An empty built stall generates no passive matchday income");
        supplies=data.CitySupplies;Check(match.SpendSupplies(1)&&data.CitySupplies==supplies-2&&!match.SpendSupplies(1)&&data.CitySupplies==supplies-2,"Equipping the pub spends two supplies once and cannot be replayed");
        Select(crew[2]);match.Assign(1);yield return Until(()=>match.ResponseComplete(1),120);
        Check(match.ResponseComplete(1)&&crew[0].CurrentOrder==stadiumDuty,"Sending a second pub member completes that response without replacing the stadium group's guard order");
        Check(!data.MatchdayIncidentResolved,"Covering locations early does not auto-finish the chapter before aftermath");
        Select(crew[3]);select.CommandSelectedMoveTo(match.StallPoint);yield return Until(()=>Near(crew[3],match.StallPoint,6),100);
        cash=data.Money;yield return Until(()=>data.Money>cash,30);
        Check(data.Money==cash+90,"Real staffing at the built stall earns the trade approach's ninety-pound active-play payout");
        select.CommandSelectedMoveTo(city.Home);yield return Until(()=>!Near(crew[3],match.StallPoint,12),40);float incomeClock=tactics.stallIncomeSeconds;cash=data.Money;yield return new WaitForSeconds(48);
        Check(data.Money==cash&&Mathf.Abs(tactics.stallIncomeSeconds-incomeClock)<.2f,"Moving the stall's crew away stops its income clock and payouts");
        // The escort uses a normal-speed guide so it cannot outrun its on-foot followers.
        crew[4].Data.Speed=2.5f;Select(crew[4]);yield return Until(()=>tactics.escortPositioned,15);
        var follower=UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).FirstOrDefault(a=>a&&a.MatchdayGroupName=="PLAYER SUPPORTER ESCORT");
        Check(tactics.escortPositioned&&follower,"Supporter pickup creates an actual following group rather than an instant reward");
        Vector3 followerStart=follower?follower.transform.position:Vector3.zero;cash=data.Money;
        Check(crew[4].TryCommandMoveTo(match.ResponsePoints[0],6),"The escort's selected guide accepts a real stadium route");
        yield return Until(()=>tactics.escortComplete,180);
        if(!tactics.escortComplete&&follower){var nav=follower.GetComponent<NavMeshAgent>();rows.Add($"DETAIL escort: guide={crew[4].transform.position}; follower={follower.transform.position}; target={match.ResponsePoints[0]}; navOn={nav&&nav.enabled&&nav.isOnNavMesh}; path={nav.pathStatus}; remaining={nav.remainingDistance}; stopped={nav.isStopped}; failure={follower.LastEscortFailure}");}
        Check(tactics.escortComplete&&follower&&Vector3.Distance(followerStart,follower.transform.position)>15&&Near(crew[4],match.ResponsePoints[0],10),"The guide and actual supporter follower physically travel to the stadium and complete the escort");
        Check(data.Money==cash+175&&!match.AssignEscort(),"Completed escort pays its declared reward once and rejects a duplicate assignment");
        GameManager.Save();loaded=SaveDataInLocal.DataLoad();
        Check(loaded.MatchdayTactics.planCommitted&&loaded.MatchdayTactics.plan==2&&loaded.MatchdayTactics.escortComplete&&loaded.MatchdayTactics.stallBuilt&&loaded.MatchdayTactics.supplySupport.Contains(1)&&loaded.MatchdayCompletedResponses.Count==2,"Native save retains the chosen approach, invested stall, escort, equipped response and coverage outcomes");
        Time.timeScale=1;CameraPanTouchOnly.Instance.FocusOn(match.ResponsePoints[0]);yield return new WaitForSecondsRealtime(.5f);ScreenCapture.CaptureScreenshot("Artifacts/Recovery/production-matchday-world.png");yield return null;
        // Resume the actual phase timer; coverage alone must not have completed
        // the chapter, while the eventual real aftermath must record it once.
        stadium.enabled=true;Time.timeScale=8;
        yield return Until(()=>stadium.IsAftermath&&data.MatchdayIncidentResolved,100);
        Check(stadium.IsAftermath&&data.MatchdayIncidentResolved&&CampaignMissions.ActionCount(data,1,"matchday")==1,"The real resumed matchday clock reaches aftermath and records the completed operation once");
        int outcomeCash=data.Money;yield return new WaitForSeconds(5);
        Check(CampaignMissions.ActionCount(data,1,"matchday")==1&&data.Money==outcomeCash,"Remaining in aftermath cannot replay the operation outcome or pay another reward");
        // Territory has an explicit paid assignment. Matchday guard presence
        // must remain distinct; a revised manual order cancels only this job.
        Isolate();
        if(pubTerritory)
        {
            // This assertion isolates assignment/cost from combat, which has
            // its own RTS regression; clear only rival bodies on the test pad.
            foreach(var enemy in BattleManager.instance.EnemyAgents)
                if(enemy&&!enemy.IsHomeMatchdaySupporter&&enemy.firmName!="POLICE"&&Vector3.Distance(enemy.transform.position,pubTerritory.transform.position)<pubTerritory.detectionRadius+5)enemy.gameObject.SetActive(false);
        }
        Select(crew[1]);cash=data.Money;var unaffectedOrder=crew[0].CurrentOrder;
        bool captureStarted=pubTerritory&&pubTerritory.BeginCapture(new[]{crew[1]});
        select.CommandSelectedMoveTo(city.Home);yield return null;yield return null;
        Check(captureStarted&&!pubTerritory.CaptureRequested&&!pubTerritory.IsCaptured&&data.Money==cash&&crew[0].CurrentOrder==unaffectedOrder,"A revised order cancels the explicit capture without tax or changing another group's guard duty");
        bool captureRestarted=pubTerritory&&pubTerritory.BeginCapture(new[]{crew[1]});
        yield return Until(()=>pubTerritory&&pubTerritory.IsCaptured,100);
        Check(captureRestarted&&pubTerritory.IsCaptured&&data.Money==cash-pubTerritory.occupationTax&&!GamePopup.AnyOpen&&!pubTerritory.BeginCapture(new[]{crew[1]})&&data.Money==cash-pubTerritory.occupationTax,"Explicit capture physically completes, charges its declared tax once, rejects replay and opens no global popup");
        int originalLevel=data.CurrentLevel;data.CurrentLevel=3;cash=data.Money;
        bool firstIncome=pubTerritory.CollectIncome();bool duplicateIncome=pubTerritory.CollectIncome();
        Check(firstIncome&&!duplicateIncome&&!pubTerritory.CanCollectIncome&&data.Money==cash+pubTerritory.moneyReward&&!GamePopup.AnyOpen,"Returning to Home with a later Away unlock cannot replay territory income or force a receipt popup");
        data.CurrentLevel=originalLevel;GameManager.Save();
        Time.timeScale=1;
        hud.FocusHome();yield return new WaitForSecondsRealtime(1.2f);ScreenCapture.CaptureScreenshot("Artifacts/Recovery/production-home-growth.png");yield return null;
        hud.OpenHome();yield return new WaitForSecondsRealtime(.5f);ScreenCapture.CaptureScreenshot("Artifacts/Recovery/production-home-investment.png");yield return null;hud.CloseSheet();
    }
}
public sealed class ProductionStrategyTestRunner:MonoBehaviour{}
#endif
