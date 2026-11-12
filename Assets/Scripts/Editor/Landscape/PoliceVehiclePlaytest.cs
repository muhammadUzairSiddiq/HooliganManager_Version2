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
using UnityEngine.UI;
using TMPro;

/// <summary>Actual patrol transport and Continue with disposable firm/police saves.</summary>
[InitializeOnLoad]
public static class PoliceVehiclePlaytest
{
    const string Key="HM.PoliceVehiclePlaytest",Report="Artifacts/Recovery/police-vehicle-playtest.txt",Save="Artifacts/Recovery/police-vehicle-firm.json";
    static readonly List<string> rows=new List<string>();
    static bool running,finished;
    static double deadline;
    static PoliceVehiclePlaytest(){EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);}
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    [MenuItem("Hooligan/Recovery/Run player police vehicle check")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Start from edit mode.");
        Directory.CreateDirectory("Artifacts/Recovery");SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var firm=new PlayerData{PlayerName="Vehicle QA Firm",Money=4321,CurrentLevel=2,RecoveryVersion=RecoveryProgression.Version};firm.RecruitedAgents.Add(new AgentData("Reserve"));SaveDataInLocal.DataSave(firm);
        SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING "+DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange change)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(change==PlayModeStateChange.EnteredPlayMode){rows.Clear();running=finished=false;deadline=EditorApplication.timeSinceStartup+120;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("MainMenu");}
        if(change==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");int marker=SessionState.GetInt(Key+".marker",-1);
            if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(rows.Any(r=>r.StartsWith("FAIL "))?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Watchdog expired.");return;}
        if(running||!GameData.instance||!GameManager.instance)return;running=true;deadline=EditorApplication.timeSinceStartup+360;
        var host=new GameObject("Police vehicle native fixture");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<PoliceVehicleTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator test)
    {
        while(true){object next=null;bool more=false;try{more=test.MoveNext();if(more)next=test.Current;}catch(Exception e){Finish(e.ToString());yield break;}if(!more){Finish(null);yield break;}yield return next;}
    }
    static void Check(bool value,string label){rows.Add((value?"PASS ":"FAIL ")+label);File.WriteAllLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);
        rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add("Native editor fixture: real roads, actors, boarding, drive, exit and Continue. Accelerated travel. No device touch or real save used.");rows.Add("UTC "+DateTime.UtcNow.ToString("O"));File.WriteAllLines(Report,rows);Time.timeScale=1;
        if(PoliceCityMode.Active)PoliceCityMode.ExitToMenu();EditorApplication.isPlaying=false;
    }
    static Vector3 ConnectedDestination(Vector3 from,float minimum)
    {
        var path=new NavMeshPath();
        for(int ring=1;ring<=4;ring++)for(int n=0;n<16;n++)
        {
            float angle=n*Mathf.PI/8;var guess=from+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(minimum+ring*8);
            if(!NavMesh.SamplePosition(guess,out var hit,12,PolicePatrolVehicle.RoadMask)||Vector3.Distance(hit.position,from)<minimum)continue;
            if(NavMesh.CalculatePath(from,hit.position,PolicePatrolVehicle.RoadMask,path)&&path.status==NavMeshPathStatus.PathComplete)return hit.position;
        }
        return from;
    }
    static Transform CrewRow(AgentController member)
    {
        foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if(button.name!="ToggleSelection")continue;
            var caption=button.GetComponentInChildren<TextMeshProUGUI>();
            if(caption&&(caption.text==member.Data.AgentName||caption.text.StartsWith(member.Data.AgentName+" ·",StringComparison.Ordinal)))return button.transform.parent;
        }
        return null;
    }
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(1);var firm=GameData.instance.PlayerData;string original=File.ReadAllText(Save);
        PoliceCityMode.Launch(true);float until=Time.realtimeSinceStartup+90;while((!PoliceCityMode.Instance||!PoliceCityMode.Instance.Ready)&&Time.realtimeSinceStartup<until)yield return null;
        var mode=PoliceCityMode.Instance;var van=PolicePatrolVehicle.Instance;var select=AgentSelectionManager.instance;
        Check(mode&&mode.Ready&&van,"Police career creates the actual player patrol van at a road");if(!mode||!mode.Ready||!van)yield break;
        Check(NavMesh.SamplePosition(van.transform.position,out var road,1f,PolicePatrolVehicle.RoadMask),"Vehicle spawn is on the road-only navigation area");
        Check(!van.DriveTo(ConnectedDestination(van.transform.position,20)),"An empty patrol van cannot drive without an officer aboard");
        foreach(var actor in BattleManager.instance.PlayerAgents)actor.Data.Speed=15;
        var a=BattleManager.instance.PlayerAgents[0];var b=BattleManager.instance.PlayerAgents[1];var reserve=BattleManager.instance.PlayerAgents[2];
        bool guarded=reserve.CommandGuard(new Bounds(reserve.transform.position,new Vector3(12,4,12)),reserve.GetComponent<NavMeshAgent>().nextPosition);
        Check(guarded&&reserve.CurrentOrder==AgentController.StandingOrder.Guard,"Reserve officer accepts an independent real Guard duty before the transport/map checks");
        int reserveRevision=reserve.OrderRevision;select.DeselectAll();select.Select(a);select.Select(b);Time.timeScale=3;
        van.Select();Check(select.SelectedAgents.Contains(a)&&select.SelectedAgents.Contains(b),"Selecting an empty van retains the chosen officers for the boarding action");
        Check(van.BoardSelected()&&van.BoardingCount==2&&van.PassengerCount==0,"Two selected officers receive walking boarding orders without instant collection");
        until=Time.realtimeSinceStartup+60;while(van.BoardingCount>0&&Time.realtimeSinceStartup<until)yield return null;
        Check(van.PassengerCount==2&&PolicePatrolVehicle.IsMounted(a)&&PolicePatrolVehicle.IsMounted(b),"Both officers physically reach and board the parked van");
        Check(a.IsActivityLocked&&b.IsActivityLocked&&!a.GetComponent<NavMeshAgent>().enabled&&!b.GetComponent<NavMeshAgent>().enabled,"Seated officers leave foot navigation and task assessment while remaining saved crew");
        Check(reserve.OrderRevision==reserveRevision&&!PolicePatrolVehicle.IsMounted(reserve),"The unselected third officer keeps their independent assignment");
        int aRevision=a.OrderRevision,bRevision=b.OrderRevision;var beforeMapSelection=select.SelectedAgents.ToArray();
        var map=LiveMiniMap.Instance;var camera=CameraPanTouchOnly.Instance;
        camera.FocusOn(van.transform.position+Vector3.right*70);yield return new WaitForSecondsRealtime(.4f);
        map.OpenTacticalMap();yield return new WaitForSecondsRealtime(.25f);map.TacticalControls.RefreshCrew();
        var aRow=CrewRow(a);var bRow=CrewRow(b);var reserveRow=CrewRow(reserve);
        Check(aRow&&bRow&&!aRow.Find("ToggleSelection").GetComponent<Button>().interactable&&!bRow.Find("ToggleSelection").GetComponent<Button>().interactable&&
            aRow.Find("ToggleSelection").GetComponentInChildren<TextMeshProUGUI>().text.Contains(" · IN VAN")&&aRow.Find("Duty").GetComponent<TextMeshProUGUI>().text.Contains("IN PATROL VAN"),"Actual boarded officers have explicit IN VAN map rows with disabled foot-selection controls");
        Check(reserveRow&&reserveRow.Find("ToggleSelection").GetComponent<Button>().interactable,"Mounted-row clarity keeps the independent on-foot officer selectable");
        if(!aRow)throw new InvalidOperationException("Mounted officer map row was not created.");
        aRow.Find("FocusMember").GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.7f);
        Check(!LiveMiniMap.IsExpanded&&!camera.IsFollowingMember&&CityBusinessSystem.HorizontalDistance(CityActivityStreaming.GroundFocus(Camera.main),van.transform.position)<25&&
            select.SelectedAgents.SequenceEqual(beforeMapSelection)&&a.OrderRevision==aRevision&&b.OrderRevision==bRevision&&reserve.OrderRevision==reserveRevision&&reserve.CurrentOrder==AgentController.StandingOrder.Guard,
            "Actual mounted-member FOCUS closes the map and centers the van without altering crew selection or the reserve Guard duty");
        map.OpenTacticalMap();yield return new WaitForSecondsRealtime(.2f);map.TacticalControls.RefreshCrew();aRow=CrewRow(a);
        aRow.Find("TrackMember").GetComponent<Button>().onClick.Invoke();
        Check(LiveMiniMap.IsExpanded&&CrewTrackingSystem.TrackedMember==a&&camera.FollowedMember==a,"Actual mounted-member TRACK pins that officer while the expanded map remains open");
        map.CloseTacticalMap();var followOrigin=CityActivityStreaming.GroundFocus(Camera.main);
        var destination=ConnectedDestination(van.transform.position,30);var start=van.transform.position;
        Check(Vector3.Distance(start,destination)>=30&&van.DriveTo(destination),"Selected patrol team accepts a complete road-only driving route");
        var accepted=van.Destination;
        Check(!van.DriveTo(new Vector3(100000,0,100000))&&van.IsDriving&&van.Destination==accepted,"An invalid off-map drive target preserves the accepted road order");
        until=Time.realtimeSinceStartup+20;while(Vector3.Distance(start,van.transform.position)<5&&Time.realtimeSinceStartup<until)yield return null;
        Check(Vector3.Distance(start,van.transform.position)>=5&&Vector3.Distance(a.transform.position,van.transform.position)<1,"Van travels visibly and carries the boarded team with it");
        yield return new WaitForSecondsRealtime(.15f);
        Check(camera.IsFollowingMember&&camera.FollowedMember==a&&Vector3.Distance(CrewTrackingSystem.WorldPosition(a),van.transform.position)<.01f&&
            CityBusinessSystem.HorizontalDistance(CityActivityStreaming.GroundFocus(Camera.main),followOrigin)>1&&CityBusinessSystem.HorizontalDistance(CityActivityStreaming.GroundFocus(Camera.main),van.transform.position)<25&&
            reserve.OrderRevision==reserveRevision&&reserve.CurrentOrder==AgentController.StandingOrder.Guard,
            "Tracking the mounted officer follows the actual road-driven van while another officer's Guard duty remains unchanged");
        CrewTrackingSystem.StopTracking();
        Time.timeScale=0;var paused=van.transform.position;yield return new WaitForSecondsRealtime(.3f);
        Check(Vector3.Distance(paused,van.transform.position)<.01f,"Paused simulation does not move the patrol vehicle");Time.timeScale=1;
        GameManager.Save();var loaded=PoliceCampaignStore.LoadOrCreate();var saved=loaded.vehicle;
        Check(saved!=null&&saved.passengers.Count==2&&saved.passengers.Contains(a.Data.AgentId)&&saved.passengers.Contains(b.Data.AgentId),"Native police save stores the van and exact boarded officer IDs");
        string aId=a.Data.AgentId,bId=b.Data.AgentId;PoliceCityMode.ExitToMenu();yield return new WaitForSecondsRealtime(2);PoliceCityMode.Launch();
        until=Time.realtimeSinceStartup+90;while((!PoliceCityMode.Instance||!PoliceCityMode.Instance.Ready)&&Time.realtimeSinceStartup<until)yield return null;
        mode=PoliceCityMode.Instance;van=PolicePatrolVehicle.Instance;select=AgentSelectionManager.instance;
        a=BattleManager.instance.PlayerAgents.First(x=>x&&x.Data.AgentId==aId);b=BattleManager.instance.PlayerAgents.First(x=>x&&x.Data.AgentId==bId);
        Check(van&&van.PassengerCount==2&&PolicePatrolVehicle.IsMounted(a)&&PolicePatrolVehicle.IsMounted(b),"Actual Police Continue restores the same boarded team");
        Check(van&&CityBusinessSystem.HorizontalDistance(van.transform.position,saved.position)<20f&&van.Destination==saved.destination,"Police Continue resumes near the saved road position and destination");
        CameraPanTouchOnly.Instance.FocusOn(van.transform.position);van.Select();yield return new WaitForSecondsRealtime(.4f);
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/police-patrol-van.png");yield return null;
        Check(van.ExitAll()&&!van.IsDriving&&van.PassengerCount==0,"EXIT OFFICERS deliberately stops and unloads the patrol team");
        Check(a.IsAlive&&b.IsAlive&&!a.IsActivityLocked&&!b.IsActivityLocked&&a.GetComponent<NavMeshAgent>().isOnNavMesh&&b.GetComponent<NavMeshAgent>().isOnNavMesh,"Disembarked officers regain live foot navigation and task control");
        Check(CityBusinessSystem.HorizontalDistance(a.transform.position,van.transform.position)<7&&CityBusinessSystem.HorizontalDistance(b.transform.position,van.transform.position)<7,"Disembarkation uses nearby walkable curb positions rather than distant teleportation");
        Check(select.SelectedAgents.Contains(a)&&select.SelectedAgents.Contains(b)&&!van.Selected,"The disembarked team is selected for immediate independent RTS orders");
        Check(File.ReadAllText(Save)==original,"Patrol vehicle and Continue saves leave the firm campaign byte-identical");
        PoliceCityMode.ExitToMenu();yield return new WaitForSecondsRealtime(2);Check(GameData.instance.PlayerData==firm,"Leaving vehicle patrol restores the original firm campaign object");
    }
}
public sealed class PoliceVehicleTestRunner:MonoBehaviour{}
#endif
