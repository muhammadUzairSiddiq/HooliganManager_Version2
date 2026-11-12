#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITHappy;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Actual road model, GPU traffic, supporter queue and NPC clearance in an isolated firm campaign.</summary>
[InitializeOnLoad]
public static class RoadIncidentPlaytest
{
    const string Key="HM.RoadIncidentPlaytest",Save="Artifacts/Recovery/road-incident-save.json",Report="Artifacts/Recovery/road-incident-playtest.txt";
    static readonly List<string> rows=new List<string>();static bool running,finished;static double deadline;
    static RoadIncidentPlaytest(){if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;}
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    [MenuItem("Hooligan/Recovery/Run real road incident check")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Start in edit mode.");RedesignAssets.Ensure();Directory.CreateDirectory("Artifacts/Recovery");
        SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var data=new PlayerData{PlayerName="Road incident QA",Money=5000,Fans=3};RecoveryProgression.Begin(data);foreach(var name in new[]{"Road Observer","Home Reserve","Runner"})data.RecruitedAgents.Add(new AgentData(name,hp:1000,speed:2.5f));
        SaveDataInLocal.DataSave(data);SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING");EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){rows.Clear();running=finished=false;deadline=EditorApplication.timeSinceStartup+150;CityGameplay.HomeMode=true;SceneManager.LoadScene("Gameplay");}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");int marker=SessionState.GetInt(Key+".marker",-1);if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(rows.Any(r=>r.StartsWith("FAIL "))?1:0);}}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Road incident watchdog expired.");return;}
        if(running||!BattleManager.instance||!BattleManager.instance.BattleActive||!CityGameplay.Instance||!CityRoadIncidentSystem.Instance)return;
        running=true;deadline=EditorApplication.timeSinceStartup+600;var host=new GameObject("Road Incident Test Runner");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<RoadIncidentTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator routine){while(true){bool more=false;object value=null;try{more=routine.MoveNext();if(more)value=routine.Current;}catch(Exception error){Finish(error.ToString());yield break;}if(!more){Finish(null);yield break;}yield return value;}}
    static void Check(bool ok,string message){rows.Add((ok?"PASS ":"FAIL ")+message);RecoveryReport.WriteLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add("Native isolated firm fixture: actual road model/GPU car samples, slow supporter/officer travel and registered NPC recovery truck. Explicit incident creation isolates outcomes; no device FPS or natural event-frequency claim.");rows.Add("UTC "+DateTime.UtcNow.ToString("O"));RecoveryReport.WriteLines(Report,rows);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static IEnumerator Until(Func<bool> condition,float seconds=90){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;}
    static void Detail(CityRoadIncidentSystem.RoadIncident incident,string stage)
    {rows.Add($"DETAIL {stage}: id={incident.Id}, state={incident.State}, position={incident.Location}, progress={incident.ClearanceProgress}, queue={incident.QueuedPeople}, pressure={incident.CrowdPressure}, truck={(incident.RecoveryVehicle?incident.RecoveryVehicle.position:Vector3.zero)}");RecoveryReport.WriteLines(Report,rows);}
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(2);var system=CityRoadIncidentSystem.Instance;system.AutomaticIncidentsEnabled=false;system.AutomaticClearanceEnabled=false;
        if(LivingRivalDirector.Instance)LivingRivalDirector.Instance.AutomaticDecisionsEnabled=false;
        if(LivePoliceSystem.Instance){LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;}
        if(CityTrainSchedule.Instance)CityTrainSchedule.Instance.AutomaticArrivalsEnabled=false;
        var cone=Resources.Load<GameObject>("CityIncidents/TrafficCone");var barrier=Resources.Load<GameObject>("CityIncidents/RoadBarricade");
        Check(cone&&cone.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh&&m.sharedMesh.vertexCount>=100&&m.sharedMesh.subMeshCount==2),"Safety cones are real striped 3D meshes with separate reflective material bands");
        Check(barrier&&barrier.GetComponentsInChildren<MeshRenderer>().Any()&&barrier.GetComponentsInChildren<Collider>().Length==0,"Barricades reuse real existing city art without introducing a blocking collider");
        var roads=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("road_")||r.name.StartsWith("railway_")).ToArray();var roadStates=roads.Select(r=>(r.enabled,r.forceRenderingOff)).ToArray();
        var data=GameManager.Data;int money=data.Money;CityRoadIncidentSystem.RoadIncident incident=null;
        foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))
        {
            car.GetTrafficStructs(out var carData,out var transport,out var position);if(transport.speed<1||position.forward.sqrMagnitude<.5f)continue;
            if(system.TryCreateIncident(position.position+position.forward*10,1,out incident))break;
        }
        if(incident==null)system.TryCreateIncident(CityGameplay.Instance.Home+Vector3.right*35,1,out incident);
        Check(incident!=null,"An actual connected street supports a bounded local road incident");if(incident==null)yield break;
        CameraPanTouchOnly.Instance.FocusOn(incident.Location);yield return new WaitForSecondsRealtime(.5f);
        Check(!incident.IsCleared&&incident.ClearanceProgress==0&&data.Money==money,"Creating an obstruction never instantly clears it or changes the firm wallet");
        var path=new NavMeshPath();var source=BattleManager.instance.enemyAgentPrefab.GetComponent<NavMeshAgent>();var filter=new NavMeshQueryFilter{agentTypeID=source.agentTypeID,areaMask=source.areaMask};
        Check(NavMesh.CalculatePath(incident.WalkingBypassStart,incident.WalkingBypassEnd,filter,path)&&path.status==NavMeshPathStatus.PathComplete,"The incident leaves a genuine connected walking bypass");
        Check(system.GetComponentsInChildren<NavMeshObstacle>().Length==0&&roads.Select((r,i)=>r&&r.enabled==roadStates[i].Item1&&r.forceRenderingOff==roadStates[i].Item2).All(v=>v),"Road and railway surfaces remain visible and no navigation-carving trap is added");
        var traffic=UnityEngine.Object.FindFirstObjectByType<TrafficManager>();yield return Until(()=>traffic&&traffic.IncidentStoppedVehicleCount>0,35);
        Check(traffic&&traffic.IncidentStoppedVehicleCount>0,"Actual GPU traffic stops at the blocked lane rather than driving through its barriers");
        var people=new List<EnemyController>();var origins=new List<Vector3>();Vector3 side=Vector3.Cross(Vector3.up,(incident.WalkingBypassEnd-incident.WalkingBypassStart).normalized);
        for(int i=0;i<5;i++)
        {
            Vector3 candidate=incident.WalkingBypassStart-(incident.WalkingBypassEnd-incident.WalkingBypassStart).normalized*8+side*(i%2)*1.6f;
            if(!NavMesh.SamplePosition(candidate,out var start,4,filter))continue;
            var person=BattleManager.instance.SpawnMatchdayFighter(start.position,"ROAD QUEUE QA",Color.green,100,0,MatchdayUnitRole.HomeSupporter,"ROAD QUEUE QA "+i);if(!person)continue;
            person.GetComponent<MatchdayMarch>().enabled=false;origins.Add(person.transform.position);people.Add(person);
            system.TryQueueArrival(person,incident.WalkingBypassEnd);
            yield return new WaitForSeconds(.3f);
        }
        Check(people.Count>=3&&people.Count(CityRoadIncidentSystem.IsQueued)>=3,"Several real arriving supporters accept separate connected queue positions");
        Time.timeScale=2;yield return Until(()=>incident.QueuedPeople>=3,45);Detail(incident,"Queue formed");
        Check(incident.QueuedPeople>=3&&incident.CrowdPressure>0&&people.Select((p,i)=>Vector3.Distance(p.transform.position,origins[i])>1).Any(v=>v),"Crowd pressure grows only after actual people physically reach the queue");
        Time.timeScale=1;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/road-incident-queue.png");yield return null;Time.timeScale=2;
        GameManager.Save();var loaded=SaveDataInLocal.DataLoad();
        Check(loaded.CityRoadIncidents.Any(r=>r.id==incident.Id&&!r.cleared&&r.severity==1&&Vector3.Distance(r.location,incident.Location)<.1f),"Native save preserves the actual uncleared incident and its road location");
        var officer=BattleManager.instance.SpawnMatchdayFighter(incident.WalkingBypassStart,"POLICE",Color.blue,500,0,MatchdayUnitRole.Police,"ROAD RESPONSE QA");if(officer)officer.GetComponent<MatchdayMarch>().enabled=false;
        Check(officer&&CityBusinessSystem.HorizontalDistance(officer.transform.position,incident.Location)>9,"A real NPC officer starts outside the clearance work radius");
        Vector3 officerStart=officer?officer.transform.position:Vector3.zero;system.AutomaticClearanceEnabled=true;
        yield return Until(()=>incident.ClearanceProgress>.05f||incident.IsCleared,60);Detail(incident,"Officers working");
        Check(officer&&Vector3.Distance(officer.transform.position,officerStart)>1&&incident.ClearanceProgress>0&&!incident.IsCleared,"Hooligan-mode police physically arrive and begin timed clearance work");
        yield return Until(()=>incident.IsCleared,40);int clears=system.ClearedCount;var departed=people.Select(p=>p.transform.position).ToArray();yield return new WaitForSeconds(3);
        Check(incident.IsCleared&&system.ActiveIncidents.All(i=>i.Id!=incident.Id),"Completed on-site police work removes the real road obstruction");
        Check(people.All(p=>!CityRoadIncidentSystem.IsQueued(p))&&people.Select((p,i)=>Vector3.Distance(p.transform.position,departed[i])>1).Any(v=>v),"Clearing releases actual waiting people along their original onward routes");
        Check(!system.TryCompleteTow(incident.Id,incident.Location)&&system.ClearedCount==clears&&data.Money==money,"A completed incident cannot replay clearance or reward NPC work to the player");
        yield return Until(()=>!traffic||traffic.IncidentStoppedVehicleCount==0,15);
        Check(traffic&&traffic.IncidentStoppedVehicleCount==0,"Clearing restores authored traffic speeds and releases the live vehicle queue");
        CityRoadIncidentSystem.RoadIncident heavy=null;
        for(int attempt=0;attempt<8&&heavy==null;attempt++)system.TryCreateIncident(incident.Location+new Vector3(Mathf.Cos(attempt*Mathf.PI/4),0,Mathf.Sin(attempt*Mathf.PI/4))*65,2,out heavy);
        Check(heavy!=null&&heavy.RequiresTow&&!system.TryCompleteTow(heavy.Id,heavy.Location),"A heavy obstruction requires a registered recovery truck and cannot clear instantly");if(heavy==null)yield break;
        CameraPanTouchOnly.Instance.FocusOn(heavy.Location);yield return Until(()=>heavy.RecoveryVehicle,40);Vector3 towStart=heavy.RecoveryVehicle?heavy.RecoveryVehicle.position:Vector3.zero;
        Check(heavy.RecoveryVehicle&&heavy.RecoveryVehicle.GetComponentsInChildren<MeshRenderer>().Length>0&&heavy.ClearanceProgress==0,"Heavy clearance dispatches a real recovery model before paying any work progress");
        yield return Until(()=>heavy.ClearanceProgress>.05f||heavy.IsCleared,100);Detail(heavy,"Tow work");
        Check(heavy.RecoveryVehicle&&Vector3.Distance(towStart,heavy.RecoveryVehicle.position)>8&&heavy.ClearanceProgress>0&&!heavy.IsCleared,"The recovery vehicle physically drives to the obstruction and works beside the officers");
        Time.timeScale=1;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/road-incident-towing.png");yield return null;Time.timeScale=2;
        yield return Until(()=>heavy.IsCleared,35);GameManager.Save();loaded=SaveDataInLocal.DataLoad();
        Check(heavy.IsCleared&&loaded.CityRoadIncidents.Count(r=>r.cleared)>=2&&data.Money==money,"Actual timed towing clears once and persists both resolved incidents without a player cash reward");
        Time.timeScale=1;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/road-incident-cleared.png");yield return null;
        foreach(var person in people)if(person)BattleManager.instance.DespawnMatchdayFighter(person);
    }
}
public sealed class RoadIncidentTestRunner:MonoBehaviour{}
#endif
