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

/// <summary>Real completed models, floor anchoring, earned Home footprint and native visual reload.</summary>
[InitializeOnLoad]
public static class HomeVisualPlaytest
{
    const string Key="HM.HomeVisualPlaytest",Save="Artifacts/Recovery/home-visual-save.json",Report="Artifacts/Recovery/home-visual-playtest.txt";
    static readonly List<string> checks=new List<string>();
    static readonly FieldInfo TrainNextStop=typeof(CityTrainSchedule).GetField("nextStop",BindingFlags.Instance|BindingFlags.NonPublic);
    static bool running,finished;static double deadline;
    static HomeVisualPlaytest()
    {
        if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;
    }
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    [MenuItem("Hooligan/Recovery/Run visible Home development check")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run from edit mode.");
        RedesignAssets.Ensure();Directory.CreateDirectory("Artifacts/Recovery");
        SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));
        PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var data=new PlayerData{PlayerName="Visible Home QA",Money=5000,Fans=3};RecoveryProgression.Begin(data);
        foreach(var name in new[]{"Home Builder","Home Guard","Home Runner"})data.RecruitedAgents.Add(new AgentData(name,hp:1000,speed:2.5f));
        SaveDataInLocal.DataSave(data);SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING "+DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){checks.Clear();running=finished=false;deadline=EditorApplication.timeSinceStartup+150;CityGameplay.HomeMode=true;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("Gameplay");}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");
            int marker=SessionState.GetInt(Key+".marker",-1);if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(checks.Any(c=>c.StartsWith("FAIL "))?1:0);}
        }
    }
    static bool Ready()=>GameData.instance&&BattleManager.instance&&BattleManager.instance.BattleActive&&HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.WorldBounds.size.x>0&&ModernCityHud.Instance;
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        SuppressPeriodicArrivals();
        if(EditorApplication.timeSinceStartup>deadline){Finish("Visible Home watchdog expired.");return;}
        if(running||!Ready())return;running=true;deadline=EditorApplication.timeSinceStartup+420;
        var host=new GameObject("Home Visual Test Runner");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<HomeVisualTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator routine)
    {
        while(true){bool more=false;object current=null;try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception error){Finish(error.ToString());yield break;}if(!more){Finish(null);yield break;}yield return current;}
    }
    static void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Report,checks);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)checks.Add("FAIL "+error);
        checks.Add($"RESULT {checks.Count(c=>c.StartsWith("PASS "))} passed, {checks.Count(c=>c.StartsWith("FAIL "))} failed");
        checks.Add("Native disposable Home visual fixture: durable crew at production base speed 2.5, normal-time travel, up to 3x stationary construction work, periodic arrivals deferred in fixture data. Actual meshes, renderer bounds, costs, save/reload and captures; not device touch/performance or natural campaign balance.");
        checks.Add("UTC "+DateTime.UtcNow.ToString("O"));File.WriteAllLines(Report,checks);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static IEnumerator Until(Func<bool> condition,float seconds=80){float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;}
    static void Isolate()
    {
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();CityUiSheets.CloseCurrent();
        foreach(var director in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))director.enabled=false;
        if(LivePoliceSystem.Instance){LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;}
        foreach(var enemy in BattleManager.instance.EnemyAgents)if(enemy){enemy.SetCinematicIdle(true);enemy.isHostile=false;}
        SuppressPeriodicArrivals();
    }
    static void SuppressPeriodicArrivals()
    {
        // Keep production components enabled: OnDisable can release actors or
        // alter train state. Only this disposable fixture's arrival clocks move.
        var data=GameManager.Data;
        if(data?.RivalStreets!=null)foreach(var firm in data.RivalStreets)if(firm!=null)firm.nextArrivalSeconds=Mathf.Max(firm.nextArrivalSeconds,10000);
        if(CityTrainSchedule.Instance&&TrainNextStop!=null)TrainNextStop.SetValue(CityTrainSchedule.Instance,Time.time+10000);
    }
    static void BuildDetail(HomeTerritorySystem home,AgentController builder,string stage)
    {
        var state=GameManager.Data?.HomeBuildings?.Find(b=>b.site==1);var nav=builder?builder.GetComponent<NavMeshAgent>():null;
        string route=nav&&nav.enabled&&nav.isOnNavMesh?$"hasPath={nav.hasPath},pending={nav.pathPending},status={nav.pathStatus},remaining={nav.remainingDistance:0.00},destination={nav.destination},speed={nav.speed:0.00},velocity={nav.velocity},corners={nav.path.corners.Length},delta={Time.deltaTime:0.000},scale={Time.timeScale:0.0}":"navigation unavailable";
        checks.Add($"DETAIL {stage}: status={home.Status}; active={home.IsBuilding}; site={home.ActiveSite}; work={home.Progress:0.00}; yardLevel={state?.level}; yardTarget={state?.targetLevel}; requested={state?.constructing}; cash={GameManager.Data?.Money}; supplies={GameManager.Data?.CitySupplies}; builder={(builder?builder.transform.position:Vector3.zero)}; builderState={(builder?builder.CurrentState.ToString():"missing")}; orderRevision={(builder?builder.OrderRevision:-1)}; distanceToYard={(builder?CityBusinessSystem.HorizontalDistance(builder.transform.position,home.Sites[1]):-1):0.00}; {route}");
        File.WriteAllLines(Report,checks);
    }
    static IEnumerator WaitForBuild(HomeTerritorySystem home,AgentController builder,string stage)
    {
        float until=Time.realtimeSinceStartup+110;
        while(home.IsBuilding&&Time.realtimeSinceStartup<until)
        {
            bool working=builder&&builder.CurrentState==AgentController.State.Idle&&home.ActiveSite>=0&&home.Progress>0&&CityBusinessSystem.HorizontalDistance(builder.transform.position,home.Sites[home.ActiveSite])<HomeTerritorySystem.WorkRadius;
            Time.timeScale=working?3:1;
            yield return null;
        }
        Time.timeScale=1;
        BuildDetail(home,builder,stage+(home.IsBuilding?" TIMED OUT":" ended"));
    }
    static float Area(Bounds bounds)=>bounds.size.x*bounds.size.z;
    static bool ContainsXZ(Bounds outer,Bounds inner)=>outer.min.x<=inner.min.x+.05f&&outer.min.z<=inner.min.z+.05f&&outer.max.x>=inner.max.x-.05f&&outer.max.z>=inner.max.z-.05f;
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(1);Isolate();var home=HomeTerritorySystem.Instance;var data=GameManager.Data;
        var builder=BattleManager.instance.PlayerAgents.First(a=>a&&a.IsAlive);AgentSelectionManager.instance.DeselectAll();AgentSelectionManager.instance.Select(builder);
        Bounds empty=home.WorldBounds;
        Check(HomeTerritorySystem.BuiltCount(data)==0&&empty.size.x>30&&empty.size.z>20,"Fresh Home has one authoritative highlighted footprint and no prebuilt facilities");
        foreach(var key in new[]{"Headquarters","SupplyYard","TrainingGround"})
        {
            var prefab=Resources.Load<GameObject>("HomeFacilities/"+key);var body=prefab?prefab.transform.Find("Facility building body"):null;
            Check(prefab&&prefab.transform.Find("HomeFacilityModelV2")&&body&&body.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh&&m.sharedMesh.vertexCount>24),key+" uses a versioned real existing city-building mesh instead of a tiny stand-in prop");
        }
        var roads=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("road_")||r.name.StartsWith("railway_")||r.name.StartsWith("parking_")).ToArray();
        var roadStates=roads.Select(r=>(r.enabled,r.forceRenderingOff)).ToArray();
        CameraPanTouchOnly.Instance.FocusOn(CityGameplay.Instance.Home);yield return new WaitForSecondsRealtime(.5f);ScreenCapture.CaptureScreenshot("Artifacts/Recovery/home-visual-before.png");yield return null;
        Time.timeScale=1;int cash=data.Money;
        Check(home.BeginBuild(0),"Selected crew accepts the free visible headquarters construction");
        yield return Until(()=>home.Progress>2||!home.IsBuilding);var work=GameObject.Find("HOME CONSTRUCTION IN PROGRESS");float earlyHeight=work?work.transform.localScale.y:0;
        Check(work&&GameObject.Find("Home active construction scaffold")&&work.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled&&!r.forceRenderingOff),"Actual construction has a visible model and scaffold on its plot");
        Time.timeScale=home.Progress>0&&builder.CurrentState==AgentController.State.Idle?3:1;yield return new WaitForSeconds(3);Time.timeScale=1;
        Check(work&&work.transform.localScale.y>earlyHeight,"Actual on-site work grows the real model before its completion");
        var dust=home.GetComponentsInChildren<ParticleSystem>().Where(p=>p.name=="Pooled Home construction dust").ToArray();
        Check(dust.Length==1&&dust[0].main.maxParticles<=24,"Construction uses one bounded reusable working-dust emitter");
        yield return new WaitForSecondsRealtime(.25f);ScreenCapture.CaptureScreenshot("Artifacts/Recovery/home-visual-construction.png");yield return null;
        yield return WaitForBuild(home,builder,"HQ construction");yield return null;
        Check(HomeTerritorySystem.Level(data,0)==1&&data.Money==cash,"Completion changes the real saved headquarters level without changing its free first-level price");
        for(int site=1;site<3;site++){Check(home.BeginBuild(site),"Crew accepts visible "+HomeTerritorySystem.Names[site]+" construction");yield return WaitForBuild(home,builder,HomeTerritorySystem.Names[site]+" construction");yield return null;}
        yield return new WaitForSeconds(3);
        Check(HomeTerritorySystem.BuiltCount(data)==3&&Area(home.WorldBounds)>Area(empty)*1.03f,"Three earned facilities visibly expand the authoritative green Home area");
        for(int site=0;site<3;site++)
        {
            bool found=home.TryGetFacilityBounds(site,out var bounds);var root=GameObject.Find("BUILT "+HomeTerritorySystem.Names[site]);
            if(found)checks.Add($"DETAIL {HomeTerritorySystem.Names[site]} mesh bounds: {bounds}; site={home.Sites[site]}");
            Check(found&&Mathf.Max(bounds.size.x,bounds.size.z)>=9&&bounds.size.y>2&&bounds.min.y>=home.Sites[site].y+.04f&&bounds.min.y<=home.Sites[site].y+.2f,HomeTerritorySystem.Names[site]+" has a substantial above-ground 3D footprint with its real mesh floor aligned to the site");
            Check(root&&root.GetComponentsInChildren<MeshRenderer>().All(r=>r.enabled&&!r.forceRenderingOff&&r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported))&&root.GetComponentsInChildren<Collider>().All(c=>!c.enabled),HomeTerritorySystem.Names[site]+" has visible supported materials and adds no walking/road collider");
            Check(found&&ContainsXZ(home.WorldBounds,bounds),HomeTerritorySystem.Names[site]+" is enclosed by the same Home footprint used by map and world UI");
        }
        Check(roads.Select((r,i)=>r&&r.enabled==roadStates[i].Item1&&r.forceRenderingOff==roadStates[i].Item2).All(value=>value),"Home completion does not hide or alter any sampled road, railway or parking renderer");
        Bounds firstTier=home.WorldBounds;home.TryGetFacilityBounds(1,out var yardBefore);data.CitySupplies=10;
        Check(home.BeginBuild(1),"The built production facility accepts its existing second-level upgrade");BuildDetail(home,builder,"Upgrade assigned");yield return WaitForBuild(home,builder,"Supply level two");yield return new WaitForSeconds(3);
        home.TryGetFacilityBounds(1,out var actualYard);checks.Add($"DETAIL upgrade geometry: before={yardBefore}; after={actualYard}; districtBefore={firstTier}; districtAfter={home.WorldBounds}");
        Check(HomeTerritorySystem.Level(data,1)==2&&home.TryGetFacilityBounds(1,out var yardAfter)&&Area(yardAfter)>Area(yardBefore)*1.1f&&Area(home.WorldBounds)>Area(firstTier),"An earned production upgrade adds a visible annex and expands the green district footprint");
        Check(!GameObject.Find("HOME CONSTRUCTION IN PROGRESS")&&!GameObject.Find("Home active construction scaffold"),"Finished work removes its scaffold and construction preview, keeping the completed building");
        Time.timeScale=1;CameraPanTouchOnly.Instance.FocusOn(home.WorldBounds.center);yield return new WaitForSecondsRealtime(.6f);ScreenCapture.CaptureScreenshot("Artifacts/Recovery/home-visual-complete.png");yield return null;
        Bounds savedFootprint=home.TargetBounds;int[] savedLevels=Enumerable.Range(0,3).Select(site=>HomeTerritorySystem.Level(data,site)).ToArray();GameManager.Save();var loaded=SaveDataInLocal.DataLoad();int oldId=home.GetInstanceID();GameData.instance.PlayerData=loaded;SceneManager.LoadScene("Gameplay");
        yield return Until(()=>Ready()&&HomeTerritorySystem.Instance.GetInstanceID()!=oldId,100);yield return new WaitForSecondsRealtime(1);Isolate();home=HomeTerritorySystem.Instance;
        checks.Add($"DETAIL reload levels: saved={string.Join(",",savedLevels)}; actual={string.Join(",",Enumerable.Range(0,3).Select(site=>HomeTerritorySystem.Level(GameManager.Data,site)))}; status={home.Status}");
        Check(HomeTerritorySystem.BuiltCount(GameManager.Data)==3&&Enumerable.Range(0,3).All(site=>HomeTerritorySystem.Level(GameManager.Data,site)==savedLevels[site]&&home.TryGetFacilityBounds(site,out var b)&&b.size.y>2&&GameObject.Find("BUILT "+HomeTerritorySystem.Names[site]).transform.childCount==savedLevels[site]),"Actual native reload reconstructs each earned facility level and its corresponding visible 3D models");
        Check(Vector3.Distance(home.TargetBounds.center,savedFootprint.center)<.1f&&Vector3.Distance(home.TargetBounds.size,savedFootprint.size)<.1f,"Reload derives the same earned Home footprint from saved development rather than resetting the green area");
        CameraPanTouchOnly.Instance.FocusOn(home.WorldBounds.center);yield return new WaitForSecondsRealtime(.6f);ScreenCapture.CaptureScreenshot("Artifacts/Recovery/home-visual-reloaded.png");yield return null;
    }
}
public sealed class HomeVisualTestRunner:MonoBehaviour{}
#endif
