#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PoliceCityPlaytest
{
    const string Key="HM.PoliceCityPlaytest",SavePath="Artifacts/Recovery/police-fixture-firm.json",Report="Artifacts/Recovery/police-playtest.txt";
    static readonly List<string> rows=new List<string>();
    static bool running,finished;
    static double deadline;
    static PoliceCityPlaytest()
    {
        if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(SavePath);
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;
    }
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    [MenuItem("Hooligan/Recovery/Run playable police career check")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Start from edit mode.");
        Directory.CreateDirectory("Artifacts/Recovery");
        SessionState.SetString(Key+".previous",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));
        PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(SavePath);
        var firm=new PlayerData{PlayerName="Police Test Firm",Money=6789,CurrentLevel=3,RecoveryVersion=RecoveryProgression.Version};
        firm.RecruitedAgents.Add(new AgentData("Firm Reserve"));SaveDataInLocal.DataSave(firm);
        SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING "+DateTime.UtcNow.ToString("O"));
        // Preserve the user's open (possibly unsaved) edit-mode scene. The
        // disposable menu/city scenes are loaded only after entering Play.
        EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {rows.Clear();running=finished=false;deadline=EditorApplication.timeSinceStartup+120;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("MainMenu");}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".previous","");
            int marker=SessionState.GetInt(Key+".marker",-1);if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(File.ReadAllText(Report).Contains("FAIL ")?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Police playtest watchdog expired.");return;}
        if(running||!GameManager.instance||!GameData.instance)return;
        running=true;deadline=EditorApplication.timeSinceStartup+420;
        var host=new GameObject("Police Test Runner");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<PoliceCareerTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator routine)
    {
        while(true)
        {
            bool more=false;object current=null;
            try{more=routine.MoveNext();if(more)current=routine.Current;}
            catch(Exception error){Finish(error.ToString());yield break;}
            if(!more){Finish(null);yield break;}yield return current;
        }
    }
    static void Check(bool value,string label)
    {rows.Add((value?"PASS ":"FAIL ")+label);File.WriteAllLines(Report,rows);}
    static string HierarchyPath(Transform node)
    {
        var path=node.name;
        for(var parent=node.parent;parent;parent=parent.parent)path=parent.name+"/"+path;
        return path;
    }
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);
        rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add("Native city fixture: disposable firm and police files; officer movement speed raised for test travel; simulation time accelerated; no real account save touched.");
        rows.Add("UTC "+DateTime.UtcNow.ToString("O"));File.WriteAllLines(Report,rows);Time.timeScale=1;
        if(PoliceCityMode.Active)PoliceCityMode.ExitToMenu();EditorApplication.isPlaying=false;
    }
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(2);
        var firm=GameData.instance.PlayerData;int firmMoney=firm.Money;var original=File.ReadAllText(SavePath);
        PoliceCityMode.Launch(true);yield return null;
        float until=Time.realtimeSinceStartup+90;
        while((!PoliceCityMode.Instance||!PoliceCityMode.Instance.Ready)&&Time.realtimeSinceStartup<until)yield return null;
        var mode=PoliceCityMode.Instance;
        Check(mode&&mode.Ready&&PoliceCityMode.Active,"PLAY AS POLICE loads a real city patrol session");
        if(!mode||!mode.Ready)yield break;
        Check(GameManager.IsPolicePlayer&&BattleManager.instance.PlayerAgents.Count==3,"Three controllable officers spawn as the player side");
        Check(mode.PatrolPoints.All(p=>CityBusinessSystem.HorizontalDistance(p,mode.Station)>=16)&&mode.PatrolPoints.Distinct().Count()==3,"Police patrol checks are distinct reachable locations away from the station");
        Check(GameData.instance.PlayerData!=firm&&firm.Money==firmMoney,"Police roster is separate from the preserved firm roster");
        Check(File.ReadAllText(SavePath)==original,"Launching police leaves the firm save bytes unchanged");
        var career=PoliceCityMode.Career;var select=AgentSelectionManager.instance;
        foreach(var member in BattleManager.instance.PlayerAgents)member.Data.Speed=18;
        Time.timeScale=6;
        var officer=BattleManager.instance.PlayerAgents[0];var backup=BattleManager.instance.PlayerAgents[1];
        select.DeselectAll();select.Select(officer);
        var start=officer.transform.position;Check(mode.AssignPatrol(0),"Selected officer accepts a real A/B patrol duty");
        float maxTravel=0;until=Time.time+12;
        while(Time.time<until&&maxTravel<=1){maxTravel=Mathf.Max(maxTravel,Vector3.Distance(start,officer.transform.position));yield return null;}
        Check(maxTravel>1&&officer.CurrentOrder==AgentController.StandingOrder.Patrol&&officer.IsSelected,"Patrol walks the officer and preserves selection (travel="+maxTravel.ToString("F1")+", order="+officer.CurrentOrder+", selected="+officer.IsSelected+")");
        for(int i=0;i<3;i++)
        {
            select.DeselectAll();select.Select(officer);officer.CommandGuard(new Bounds(mode.PatrolPoints[i],new Vector3(14,8,14)));
            until=Time.realtimeSinceStartup+30;while(!career.patrolChecks.Contains(i)&&Time.realtimeSinceStartup<until)yield return null;
            Check(career.patrolChecks.Contains(i),"Physical officer presence completes patrol check "+(i+1));
        }
        select.DeselectAll();select.Select(backup);mode.AssignPatrol(1);var backupOrder=backup.CurrentOrder;
        select.DeselectAll();select.Select(officer);
        CameraPanTouchOnly.Instance.FocusOn(career.calls[0].suspectPosition);
        Check(mode.Respond(0),"Dispatch sends only the selected officer to a reported incident");
        until=Time.realtimeSinceStartup+90;while(career.calls[0].state==0&&Time.realtimeSinceStartup<until)yield return null;
        Check(career.calls[0].state==1,"Actual travel and on-site assessment identify the reported suspect");
        Check(backup.CurrentOrder==backupOrder&&backupOrder==AgentController.StandingOrder.Patrol,"Dispatch does not overwrite the other officer's patrol");
        Check(mode.Detain(0),"A nearby selected officer can detain the identified suspect");
        until=Time.realtimeSinceStartup+15;while(career.calls[0].state==2&&Time.realtimeSinceStartup<until)yield return null;
        Check(career.calls[0].state==3&&career.arrests==1,"Five-second local detention records one arrest");
        Check(!mode.Detain(0)&&career.arrests==1,"A repeated detention request cannot duplicate the arrest");
        Check(mode.Escort(0),"The assigned arresting officer accepts a station escort route");
        // Lose only the arresting officer. The two living officers must still
        // be able to complete the existing detention, without another arrest.
        officer.TakeDamage(99999);yield return null;yield return null;
        Check(career.calls[0].state==3&&career.arrests==1&&BattleManager.instance.PlayerAgents.Count(a=>a&&a.IsAlive)==2,"A partial officer loss keeps the detained suspect and existing arrest outcome");
        select.DeselectAll();select.Select(backup);
        Check(!mode.Escort(0),"Escort handover rejects a distant selected officer");
        var detainedSuspect=UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).First(e=>e&&e.firmName=="IDENTIFIED SUSPECT");
        mode.FocusNext();mode.CloseDispatch();yield return new WaitForSecondsRealtime(.6f);
        var callMarker=GameObject.Find("Police "+career.calls[0].title);
        var focusedSuspect=Camera.main.WorldToViewportPoint(detainedSuspect.transform.position);
        Check(Vector3.Distance(mode.CallActionPoint(career.calls[0]),detainedSuspect.transform.position)<.2f&&callMarker&&Vector3.Distance(callMarker.transform.position,detainedSuspect.transform.position)<.2f,
            "Handover action and world marker point to the waiting suspect, not the station");
        Check(focusedSuspect.z>0&&focusedSuspect.x>.2f&&focusedSuspect.x<.8f&&focusedSuspect.y>.2f&&focusedSuspect.y<.8f,"SHOW ME focuses the detained suspect when a replacement escort is required");
        File.WriteAllLines("Artifacts/Recovery/police-visibility-audit.txt",new[]{"UTC "+DateTime.UtcNow.ToString("O"),"Camera "+Camera.main.transform.position}
            .Concat(UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(r=>r&&r.forceRenderingOff).OrderBy(r=>HierarchyPath(r.transform))
                .Select(r=>$"{r.name} | {HierarchyPath(r.transform)} | enabled={r.enabled} active={r.gameObject.activeInHierarchy} | bounds={r.bounds}")));
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/police-handover.png");yield return null;
        backup.TryCommandMoveTo(detainedSuspect.transform.position);
        until=Time.realtimeSinceStartup+90;
        while(Vector3.Distance(backup.transform.position,detainedSuspect.transform.position)>4&&Time.realtimeSinceStartup<until)yield return null;
        Check(mode.Escort(0)&&career.calls[0].assignedOfficerId==backup.Data.AgentId&&career.arrests==1,"A nearby selected living officer takes over the existing escort without a second arrest");
        yield return new WaitForSecondsRealtime(.3f);
        Check(Vector3.Distance(mode.CallActionPoint(career.calls[0]),mode.Station)<.2f&&callMarker&&Vector3.Distance(callMarker.transform.position,mode.Station)<.2f,
            "Transferred escort action and world marker direct booking to the station");
        Check(callMarker&&!callMarker.GetComponentInChildren<TMPro.TextMeshPro>(true).gameObject.activeSelf,"Booking marker keeps its ring while the permanent station caption remains the only station label");
        CameraPanTouchOnly.Instance.FocusOn(mode.Station);
        until=Time.realtimeSinceStartup+130;while(career.calls[0].state<4&&Time.realtimeSinceStartup<until)yield return null;
        Check(career.calls[0].state==4&&career.booked==1,"Officer and suspect physically return to the station and complete booking");
        yield return new WaitForSecondsRealtime(.3f);
        Check(callMarker&&!callMarker.activeSelf,"Resolved police call removes its world objective marker");
        select.DeselectAll();select.Select(backup);CameraPanTouchOnly.Instance.FocusOn(career.calls[1].suspectPosition);
        bool secondAccepted=mode.Respond(1);
        Check(secondAccepted,"A second officer can respond independently to the station-route call ("+backup.LastOrderFailure+")");
        until=Time.realtimeSinceStartup+90;while(career.calls[1].state<4&&Time.realtimeSinceStartup<until)yield return null;
        Check(career.calls[1].state==4&&career.resolvedCalls==2,"Sustained presence resolves the public route task without a fight");
        int budget=career.officers.Money;Check(mode.CompleteShift()&&career.officers.Money==budget+750,"A completed patrol/booking shift grants the documented budget once");
        budget=career.officers.Money;Check(!mode.CompleteShift()&&career.officers.Money==budget,"Repeated shift completion cannot duplicate budget");
        GameManager.Save();var loaded=PoliceCampaignStore.LoadOrCreate();
        Check(loaded.booked==1&&loaded.completedShifts==1&&loaded.patrolChecks.Count==3&&loaded.calls.All(c=>c.state==4),"Actual police save preserves patrol, booking, incidents and shift outcomes");
        Check(loaded.positions.Count==3,"Police Continue has saved officer positions and duties");
        Check(File.ReadAllText(SavePath)==original,"Repeated police saves never overwrite the firm save");
        Time.timeScale=1;mode.OpenDispatch();yield return null;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/police-dispatch.png");yield return null;

        // Exercise real Continue with a moving patrol and a non-default guard
        // rectangle/post. Saving only position/current destination loses these.
        mode.CloseDispatch();
        string guardId=backup.Data.AgentId,fallenId=officer.Data.AgentId;
        var patroller=BattleManager.instance.PlayerAgents.First(a=>a&&a.IsAlive&&a!=backup);
        string patrolId=patroller.Data.AgentId;
        var guardPost=backup.transform.position;
        var guardBounds=new Bounds(guardPost+Vector3.right*3,new Vector3(22,9,28));
        Check(backup.CommandGuard(guardBounds,guardPost),"Continue fixture assigns a custom rectangular guard with an off-centre post ("+backup.LastOrderFailure+")");
        var acceptedGuardPost=backup.DutyEnd;
        var patrolA=mode.PatrolPoints[0];var patrolB=mode.PatrolPoints[2];
        Check(patroller.CommandPatrol(patrolA,patrolB),"Continue fixture assigns a full two-endpoint patrol");
        until=Time.realtimeSinceStartup+20;
        while((patroller.PatrolReturning||Vector3.Distance(patroller.transform.position,patrolA)<3||Vector3.Distance(patroller.transform.position,patrolB)<3)&&Time.realtimeSinceStartup<until)yield return null;
        // Slow only this fixture during reload so it cannot reach an endpoint
        // and legitimately reverse before the saved-leg assertion observes it.
        patroller.Data.Speed=.75f;
        GameManager.Save();loaded=PoliceCampaignStore.LoadOrCreate();
        var savedPatrol=loaded.positions.First(p=>p.id==patrolId);var savedGuard=loaded.positions.First(p=>p.id==guardId);
        var originalPatrolA=savedPatrol.patrolA;var originalPatrolB=savedPatrol.patrolB;bool savedReturning=savedPatrol.patrolReturning;
        Check(savedPatrol.dutyVersion==1&&savedGuard.dutyVersion==1&&savedGuard.rectangularGuard&&savedGuard.guardCenter==guardBounds.center&&savedGuard.guardSize==guardBounds.size&&Vector3.Distance(savedGuard.guardPost,acceptedGuardPost)<.05f,
            "Native police save stores versioned A/B patrol and exact guard rectangle/post geometry");
        PoliceCityMode.ExitToMenu();yield return new WaitForSecondsRealtime(2);
        PoliceCityMode.Launch();yield return new WaitForSecondsRealtime(2);until=Time.realtimeSinceStartup+90;
        while((!PoliceCityMode.Instance||!PoliceCityMode.Instance.Ready)&&Time.realtimeSinceStartup<until)yield return null;
        mode=PoliceCityMode.Instance;career=PoliceCityMode.Career;select=AgentSelectionManager.instance;
        patroller=BattleManager.instance.PlayerAgents.First(a=>a&&a.Data.AgentId==patrolId);
        backup=BattleManager.instance.PlayerAgents.First(a=>a&&a.Data.AgentId==guardId);
        Check(mode.Ready&&career.completedShifts==1&&career.officers.RecruitedAgents.Any(a=>a.AgentId==fallenId&&!a.IsAlive),"Actual Police Continue reloads career outcomes and the down officer without touching the firm");
        Check(patroller.HasPatrolRoute(originalPatrolA,originalPatrolB)&&patroller.PatrolReturning==savedReturning,
            "Police Continue preserves original patrol A/B endpoints and current travel direction");
        Check(backup.CurrentOrder==AgentController.StandingOrder.Guard&&backup.IsRectangularGuard&&backup.GuardRegion==guardBounds&&Vector3.Distance(backup.DutyEnd,acceptedGuardPost)<.2f,
            "Police Continue preserves the chosen guard geometry and off-centre post");
        patroller.Data.Speed=18;
        bool towardA=false,towardB=false;until=Time.realtimeSinceStartup+25;
        while((!towardA||!towardB)&&Time.realtimeSinceStartup<until)
        {
            towardA|=Vector3.Distance(patroller.CommandDestination,originalPatrolA)<1;
            towardB|=Vector3.Distance(patroller.CommandDestination,originalPatrolB)<1;
            yield return null;
        }
        Check(towardA&&towardB&&patroller.HasPatrolRoute(originalPatrolA,originalPatrolB),"Resumed patrol physically reaches and reverses between its original endpoints");
        backup.TakeDamage(20);
        Check(career.officers.RecruitedAgents.Any(a=>a.AgentId==fallenId&&!a.IsAlive)&&backup.IsAlive&&backup.CurrentHp<backup.Data.MaxHp,"Next-shift fixture contains a down officer and an injured survivor");
        mode.BeginNextShift();yield return new WaitForSecondsRealtime(2);
        until=Time.realtimeSinceStartup+90;while((!PoliceCityMode.Instance||!PoliceCityMode.Instance.Ready)&&Time.realtimeSinceStartup<until)yield return null;
        Check(PoliceCityMode.Career.shift==2&&PoliceCityMode.Career.calls.Count==2&&PoliceCityMode.Career.patrolChecks.Count==0,"NEXT SHIFT creates fresh dispatch tasks while retaining career progress");
        Check(PoliceCityMode.Career.officers.RecruitedAgents.All(a=>a!=null&&a.CurrentHp==a.MaxHp)&&BattleManager.instance.PlayerAgents.Count(a=>a&&a.IsAlive&&a.CurrentHp==a.Data.MaxHp)==3,"NEXT SHIFT preserves healed saved/runtime officers instead of recapturing previous down or injured actors");
        mode=PoliceCityMode.Instance;
        Check(!AgentSelectionManager.BlocksWorldTap(),"Police world commands remain available after dispatch closes");
        mode.OpenDispatch();Check(AgentSelectionManager.BlocksWorldTap(),"Open dispatch blocks accidental orders through its sheet");mode.CloseDispatch();
        foreach(var member in BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray())member.TakeDamage(99999);
        yield return null;
        Check(!GamePopup.AnyOpen,"Lost officers do not trigger the firm campaign's Wasted flow");
        mode.RecoverOfficers();yield return new WaitForSecondsRealtime(2);until=Time.realtimeSinceStartup+90;
        while((!PoliceCityMode.Instance||!PoliceCityMode.Instance.Ready)&&Time.realtimeSinceStartup<until)yield return null;
        Check(PoliceCityMode.Career.officers.RecruitedAgents.Count(a=>a.IsAlive)==3&&BattleManager.instance.PlayerAgents.Count(a=>a&&a.IsAlive)==3,"Recover officers restores a playable team without dead actor saves overwriting recovery");
        PoliceCityMode.ExitToMenu();yield return new WaitForSecondsRealtime(2);
        Check(!PoliceCityMode.Active&&GameData.instance.PlayerData==firm&&GameManager.Data.Money==firmMoney,"Leaving police restores the exact original firm campaign");
        Check(File.ReadAllText(SavePath)==original,"Firm campaign remains byte-identical after full police shift and exit");
    }
}
public sealed class PoliceCareerTestRunner : MonoBehaviour{}
#endif
