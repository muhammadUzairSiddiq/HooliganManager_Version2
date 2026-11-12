#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Disposable native career: custody travel, corruption receipts, physical crowd and crane work.</summary>
[InitializeOnLoad]
public static class PoliceOperationsPlaytest
{
    const string Key="HM.PoliceOperationsPlaytest",Report="Artifacts/Recovery/police-operations-playtest.txt",Save="Artifacts/Recovery/police-operations-firm.json";
    static readonly List<string> checks=new List<string>();static bool running,finished;static double deadline;
    static PoliceOperationsPlaytest(){if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;}
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run from edit mode.");
        Directory.CreateDirectory("Artifacts/Recovery");SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);var d=new PlayerData{PlayerName="Police Operations Firm",Money=4321,CurrentLevel=2,RecoveryVersion=RecoveryProgression.Version};d.RecruitedAgents.Add(new AgentData("Untouched reserve"));SaveDataInLocal.DataSave(d);
        SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING");EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange change)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(change==PlayModeStateChange.EnteredPlayMode){checks.Clear();running=finished=false;deadline=EditorApplication.timeSinceStartup+120;GuidedCityTutorial.EditorExerciseTutorial=false;SceneManager.LoadScene("MainMenu");}
        if(change==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");int marker=SessionState.GetInt(Key+".marker",-1);if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(checks.Any(c=>c.StartsWith("FAIL "))?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Police operations watchdog expired");return;}
        if(running||!GameManager.instance||!GameData.instance)return;running=true;deadline=EditorApplication.timeSinceStartup+600;
        var host=new GameObject("Police operations runner");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<PoliceOperationsRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator routine)
    {
        while(true){object value=null;bool more=false;try{more=routine.MoveNext();if(more)value=routine.Current;}catch(Exception e){Finish(e.ToString());yield break;}if(!more){Finish(null);yield break;}yield return value;}
    }
    static void Check(bool ok,string text){checks.Add((ok?"PASS ":"FAIL ")+text);File.WriteAllLines(Report,checks);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)checks.Add("FAIL "+error);
        checks.Add("RESULT "+checks.Count(c=>c.StartsWith("PASS "))+" passed, "+checks.Count(c=>c.StartsWith("FAIL "))+" failed");checks.Add("Native isolated accelerated fixture; no phone input, hardware mix, natural pacing or store claim.");File.WriteAllLines(Report,checks);Time.timeScale=1;
        if(PoliceCityMode.Active)PoliceCityMode.ExitToMenu();EditorApplication.isPlaying=false;
    }
    static void Select(params AgentController[] actors){var selection=AgentSelectionManager.instance;selection.DeselectAll();PolicePatrolVehicle.Instance?.Deselect();PoliceRecoveryVehicle.Instance?.Deselect();foreach(var actor in actors)selection.Select(actor);}
    static Vector3 RoadAway(Vector3 from)
    {
        var path=new NavMeshPath();for(int ring=0;ring<4;ring++)for(int n=0;n<16;n++)
        {float angle=n*Mathf.PI/8;var guess=from+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(55+ring*12);if(NavMesh.SamplePosition(guess,out var hit,10,PolicePatrolVehicle.RoadMask)&&Vector3.Distance(from,hit.position)>35&&NavMesh.CalculatePath(from,hit.position,PolicePatrolVehicle.RoadMask,path)&&path.status==NavMeshPathStatus.PathComplete)return hit.position;}
        throw new InvalidOperationException("No connected away road for custody travel check.");
    }
    static void IsolatePressure()
    {
        foreach(var rival in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))rival.enabled=false;
        if(CityRoadIncidentSystem.Instance){CityRoadIncidentSystem.Instance.AutomaticIncidentsEnabled=false;CityRoadIncidentSystem.Instance.AutomaticClearanceEnabled=false;}
        if(PoliceCityMode.Instance&&PoliceCityMode.Instance.CrowdOperations)PoliceCityMode.Instance.CrowdOperations.enabled=false;
        foreach(var actor in BattleManager.instance.PlayerAgents)actor.Data.Speed=20;
    }
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(1);var firm=GameData.instance.PlayerData;string firmBytes=File.ReadAllText(Save);
        PoliceCityMode.Launch(true);float until=Time.realtimeSinceStartup+90;while((!PoliceCityMode.Instance||!PoliceCityMode.Instance.Ready)&&Time.realtimeSinceStartup<until)yield return null;
        var mode=PoliceCityMode.Instance;if(!mode||!mode.Ready)throw new Exception("Police mode did not prepare.");IsolatePressure();Time.timeScale=3;
        var career=PoliceCityMode.Career;var a=BattleManager.instance.PlayerAgents[0];var b=BattleManager.instance.PlayerAgents[1];var reserve=BattleManager.instance.PlayerAgents[2];var van=PolicePatrolVehicle.Instance;
        Check(a.Data.MaxHp>=160&&a.Data.Strength>=18,"Police specialist health and strength are applied to actual controllable officers");
        int money=career.officers.Money,roster=BattleManager.instance.PlayerAgents.Count;Check(mode.RequestReinforcements(),"Good-integrity career can request a real station reinforcement");
        Check(career.officers.Money==money-500&&BattleManager.instance.PlayerAgents.Count==roster+1,"Backup pays once and creates an actual additional officer");
        money=career.officers.Money;Check(!mode.RequestReinforcements()&&career.officers.Money==money,"Repeated backup request cannot duplicate its officer or payment");IsolatePressure();
        reserve.CommandGuard(new Bounds(reserve.transform.position,new Vector3(12,4,12)),reserve.transform.position);int guardRevision=reserve.OrderRevision;
        Select(a);Check(mode.Respond(0),"Officer accepts the actual shoplifting assessment route");
        until=Time.realtimeSinceStartup+60;while(career.calls[0].state<1&&Time.realtimeSinceStartup<until)yield return null;
        Check(career.calls[0].state==1,"Physical arrival and assessment identify a real suspect");
        var call=career.calls[0];var suspect=mode.GetSuspect(call);Select(a);Check(mode.Detain(0),"Nearby selected officer begins a real timed detention");
        until=Time.realtimeSinceStartup+15;while(call.state!=3&&Time.realtimeSinceStartup<until)yield return null;
        Check(call.state==3&&career.arrests==1,"Detention holds for five simulation seconds and records one arrest");
        Check(mode.LoadSuspect(0)&&!call.inVehicle&&van.SuspectCount==0,"Loading starts a walking escort instead of collecting a distant suspect instantly");
        until=Time.realtimeSinceStartup+90;while(!call.inVehicle&&Time.realtimeSinceStartup<until)yield return null;
        Check(call.inVehicle&&van.SuspectCount==1&&van.PassengerCount>=1,"Escort and suspect physically reach the van, occupy custody/driver seats");
        Check(!suspect.GetComponent<NavMeshAgent>().enabled&&suspect.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"Seated suspect leaves street navigation and visible foot rendering");
        Check(reserve.OrderRevision==guardRevision&&reserve.CurrentOrder==AgentController.StandingOrder.Guard,"Custody transport leaves another officer's Guard duty intact");
        var start=van.transform.position;Check(van.DriveTo(RoadAway(mode.Station)),"Custody van accepts a real complete road route away from station");
        until=Time.realtimeSinceStartup+30;while(van.IsDriving&&Time.realtimeSinceStartup<until)yield return null;
        Check(Vector3.Distance(start,van.transform.position)>20&&Vector3.Distance(suspect.transform.position,van.transform.position)<1,"Actual road travel carries the detained suspect with the van");
        Check(!mode.BookVanSuspects()&&career.booked==0,"A remote parked van cannot book a suspect outside the station");
        string callId=call.id;GameManager.Save();var saved=PoliceCampaignStore.LoadOrCreate();
        Check(saved.vehicle.suspectCallIds.Contains(callId)&&saved.calls[0].inVehicle,"Native police save persists exact custody IDs and transport state");
        PoliceCityMode.ExitToMenu();yield return new WaitForSecondsRealtime(2);PoliceCityMode.Launch();until=Time.realtimeSinceStartup+90;while((!PoliceCityMode.Instance||!PoliceCityMode.Instance.Ready)&&Time.realtimeSinceStartup<until)yield return null;
        mode=PoliceCityMode.Instance;career=PoliceCityMode.Career;van=PolicePatrolVehicle.Instance;call=career.calls.First(c=>c.id==callId);IsolatePressure();Time.timeScale=3;
        Check(call.inVehicle&&van.HasSuspect(callId)&&career.booked==0,"Actual Police Continue restores physical custody without granting booking credit");
        Check(mode.DriveVanToStation(),"Restored driver can return the actual custody van to station");until=Time.realtimeSinceStartup+40;while(van.IsDriving&&Time.realtimeSinceStartup<until)yield return null;
        Check(mode.BookVanSuspects()&&!call.inVehicle,"Station booking unloads the suspect at a connected curb for the final foot escort");
        until=Time.realtimeSinceStartup+40;while(call.state!=4&&Time.realtimeSinceStartup<until)yield return null;
        Check(call.state==4&&call.resolution=="BOOKED"&&career.booked==1,"Physical officer/suspect station arrival and verification complete booking once");
        Check(!mode.BookVanSuspects()&&career.booked==1,"Repeating station booking cannot duplicate a recorded booking");
        var crowd=mode.CrowdOperations;crowd.enabled=true;CameraPanTouchOnly.Instance.FocusOn(crowd.Location);until=Time.realtimeSinceStartup+35;while(crowd.CurrentState<1&&Time.realtimeSinceStartup<until)yield return null;
        yield return new WaitForSecondsRealtime(.5f);Check(crowd.Fighters.Count>=2&&crowd.Fighters.Count<=8&&crowd.Fighters.Select(x=>x.firmName).Distinct().Count()==2,"Stadium incident materializes bounded real opposing gang actors");
        a=BattleManager.instance.PlayerAgents.First(x=>x&&x.IsAlive&&!PolicePatrolVehicle.IsMounted(x));b=BattleManager.instance.PlayerAgents.First(x=>x&&x.IsAlive&&x!=a&&!PolicePatrolVehicle.IsMounted(x));Select(a,b);
        Check(crowd.AssignSelected(),"Two selected officers receive physical crowd-control travel assignments");until=Time.realtimeSinceStartup+75;while(crowd.CurrentState<2&&Time.realtimeSinceStartup<until)yield return null;
        var cases=career.calls.Where(c=>c.id.Contains(":crowd:")&&c.state==1).ToArray();Check(crowd.CurrentState>=2&&cases.Length>=1,"Actual officer presence and hold separate the clash and identify living suspects");
        foreach(var crowdCase in cases)
        {
            var target=mode.GetSuspect(crowdCase);var officer=BattleManager.instance.PlayerAgents.Where(x=>x&&x.IsAlive&&!PolicePatrolVehicle.IsMounted(x)).OrderBy(x=>(x.transform.position-target.transform.position).sqrMagnitude).First();
            officer.TryCommandMoveTo(target.transform.position);until=Time.realtimeSinceStartup+30;while(Vector3.Distance(officer.transform.position,target.transform.position)>5&&Time.realtimeSinceStartup<until)yield return null;Select(officer);
            int before=career.officers.Money,integrity=career.integrity,debt=career.disciplineDebt;int index=career.calls.IndexOf(crowdCase);
            Check(mode.TryAcceptBribe(index)&&career.officers.Money==before+crowdCase.bribeOffer&&career.integrity==Mathf.Max(0,integrity-18)&&career.disciplineDebt==debt+150,"Real nearby bribe decision pays once and records integrity plus disciplinary consequences");
            before=career.officers.Money;Check(!mode.TryAcceptBribe(index)&&career.officers.Money==before,"Repeated bribe callback cannot pay or resolve the same case again");
        }
        var snapshot=JsonUtility.FromJson<PoliceCampaignData>(JsonUtility.ToJson(career));snapshot.reinforcementReceipts.Clear();snapshot.integrity=30;snapshot.trust=70;snapshot.morale=70;
        bool blocked=!PoliceCampaignStore.ReinforcementEligible(snapshot);snapshot.integrity=80;Check(blocked&&PoliceCampaignStore.ReinforcementEligible(snapshot),"Low integrity blocks backup in an otherwise eligible funded career");
        snapshot.disciplineDebt=300;snapshot.patrolChecks=new List<int>{0,1,2};foreach(var c in snapshot.calls)c.state=4;int beforeShift=snapshot.officers.Money;
        Check(PoliceCampaignStore.FinishShift(snapshot)&&snapshot.officers.Money==beforeShift+450&&snapshot.lastDisciplineCost==300,"Shift settlement actually deducts disciplinary debt from available funds");int settled=snapshot.officers.Money;Check(!PoliceCampaignStore.FinishShift(snapshot)&&snapshot.officers.Money==settled,"Repeated shift settlement cannot charge debt or pay the grant twice");
        string roadId=mode.EnsureTutorialRoadIncident();var roads=CityRoadIncidentSystem.Instance;var incident=roads.GetIncident(roadId);if(incident==null)throw new Exception("No real road incident could be created.");
        a=BattleManager.instance.PlayerAgents.First(x=>x&&x.IsAlive&&!PolicePatrolVehicle.IsMounted(x));Select(a);Check(roads.TryAssignOfficers(roadId,new[]{a}),"A real on-foot officer is dispatched to the roadside clearance area");
        money=career.officers.Money;Check(roads.TryDispatchTow(roadId),"A funded career hires and dispatches the actual recovery truck");
        Check(career.officers.Money==money-525&&PoliceRecoveryVehicle.Instance&&PoliceRecoveryVehicle.Instance.IsDriving,"Recovery charges hire/dispatch once and starts real road travel");money=career.officers.Money;Check(roads.TryDispatchTow(roadId)&&career.officers.Money==money,"Repeated recovery dispatch does not duplicate hire or per-incident charges");
        Check(!roads.TryCompleteTow(roadId,PoliceRecoveryVehicle.Instance.transform.position)&&!incident.IsCleared,"Dispatch cannot instantly clear an unworked road obstruction");
        until=Time.realtimeSinceStartup+75;while(!incident.IsCleared&&Time.realtimeSinceStartup<until)yield return null;
        Check(incident.IsCleared&&roads.ClearedCount>0&&CityBusinessSystem.HorizontalDistance(PoliceRecoveryVehicle.Instance.transform.position,incident.Location)<=12,"Actual truck arrival plus assigned foot coverage and timed crane work clear the road");
        Check(!roads.TryCompleteTow(roadId,PoliceRecoveryVehicle.Instance.transform.position),"Cleared incident cannot replay its clearance outcome");
        GameManager.Save();saved=PoliceCampaignStore.LoadOrCreate();Check(saved.bribeReceipts.Count==career.bribeReceipts.Count&&saved.recoveryVehicle.hired&&saved.officers.CityRoadIncidents.Any(i=>i.id==roadId&&i.cleared),"Native police save persists corruption receipts, owned recovery truck and actual cleared incident");
        Check(File.ReadAllText(Save)==firmBytes,"Expanded Police operations leave the original firm save byte-identical");PoliceCityMode.ExitToMenu();yield return new WaitForSecondsRealtime(2);Check(GameData.instance.PlayerData==firm,"Leaving the expanded career restores the original firm object");
    }
}
public sealed class PoliceOperationsRunner:MonoBehaviour{}
#endif
