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

/// <summary>Actual production-speed crew, navigation, focus/follow and bounded finder UI.</summary>
[InitializeOnLoad]
public static class CrewTrackingPlaytest
{
    const string Key="HM.CrewTrackingPlaytest",Report="Artifacts/Recovery/crew-tracking-playtest.txt",Save="Artifacts/Recovery/crew-tracking-save.json";
    static readonly List<string> rows=new List<string>();
    static bool running,finished;
    static double deadline;
    const string TelemetryBase="Artifacts/Recovery/crew-tracking-editor-telemetry";
    [Serializable] sealed class EditorTelemetry
    {
        public string utc,stage,status,scope="Unity editor observation only; not device profiling, phone FPS, a full-city stress test or a performance acceptance gate.";
        public string unityVersion,graphicsDevice,graphicsApi,pipelineAsset,mapRendererData,mainRendererData;
        public bool batchMode,complete,mapShadows,mapPostProcessing,mapAllowHDR,mapAllowMSAA;
        public string mapAntialiasing;
        public int width,height,targetSamples=120,observedRenderedFrames,renderedCounterStart,renderedCounterEnd,updateFrameStart,updateFrameEnd;
        public int livingCrew,activeEnemies,selectedCrew,movingCrew,maxVisibleRoutes,maxCachedRoutes,maxVisibleLabels,maxActiveEffects;
        public int routeCap=CrewTrackingSystem.MaximumRoutes,cornersPerRouteCap=CrewTrackingSystem.MaximumCorners,labelCap=CrewWorldLabels.MaximumVisibleLabels;
        public string effectsBudget="GameFeedbackBurst source pool: 6 bursts x 6 stars; active burst count observed below. Other presentation systems not profiled.";
        public float timeScale,elapsedSeconds,medianMilliseconds,p95Milliseconds,minMilliseconds,maxMilliseconds;
        public float[] renderedFrameMilliseconds;
    }
    static bool telemetryWritten;
    static CrewTrackingPlaytest()
    {
        if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;
    }
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    [MenuItem("Hooligan/Recovery/Run crew movement and tracking check")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run from edit mode.");
        Directory.CreateDirectory("Artifacts/Recovery");
        telemetryWritten=false;
        File.Delete(TelemetryBase+".json");File.Delete(TelemetryBase+".txt");
        SessionState.SetString(Key+".save",SaveDataInLocal.EditorSaveOverride??"");SessionState.SetFloat(Key+".timescale",Time.timeScale);
        BackupInt("run",CrewMovementPreference.RunKey);BackupFloat("pace",CrewMovementPreference.PaceKey);
        BackupInt("routes",CrewTrackingSystem.AllRoutesKey);BackupInt("install",GameCacheBootstrap.InstallMarkerKey);
        PlayerPrefs.DeleteKey(CrewMovementPreference.RunKey);PlayerPrefs.DeleteKey(CrewMovementPreference.PaceKey);
        PlayerPrefs.SetInt(CrewTrackingSystem.AllRoutesKey,0);PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);PlayerPrefs.Save();CrewMovementPreference.ReloadPreferences();
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var data=new PlayerData{PlayerName="Crew Tracking QA",Money=5000,Fans=6};RecoveryProgression.Begin(data);
        foreach(var name in new[]{"Brick","Dex","Ash","Lee","Kai","Sam"})data.RecruitedAgents.Add(new AgentData(name,hp:1500,speed:2.5f));
        SaveDataInLocal.DataSave(data);SessionState.SetBool(Key,true);
        RecoveryReport.WriteLines(Report,new[]{"RUNNING "+DateTime.UtcNow.ToString("O")});EditorApplication.isPlaying=true;
    }
    static void BackupInt(string name,string preference)
    {SessionState.SetBool(Key+name+".exists",PlayerPrefs.HasKey(preference));SessionState.SetInt(Key+name+".value",PlayerPrefs.GetInt(preference));}
    static void BackupFloat(string name,string preference)
    {SessionState.SetBool(Key+name+".exists",PlayerPrefs.HasKey(preference));SessionState.SetFloat(Key+name+".value",PlayerPrefs.GetFloat(preference));}
    static void RestoreInt(string name,string preference)
    {if(SessionState.GetBool(Key+name+".exists",false))PlayerPrefs.SetInt(preference,SessionState.GetInt(Key+name+".value",0));else PlayerPrefs.DeleteKey(preference);}
    static void RestoreFloat(string name,string preference)
    {if(SessionState.GetBool(Key+name+".exists",false))PlayerPrefs.SetFloat(preference,SessionState.GetFloat(Key+name+".value",0));else PlayerPrefs.DeleteKey(preference);}
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {rows.Clear();running=finished=false;deadline=EditorApplication.timeSinceStartup+150;Time.timeScale=1;CityGameplay.HomeMode=true;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("Gameplay");}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".save","");
            RestoreInt("run",CrewMovementPreference.RunKey);RestoreFloat("pace",CrewMovementPreference.PaceKey);
            RestoreInt("routes",CrewTrackingSystem.AllRoutesKey);RestoreInt("install",GameCacheBootstrap.InstallMarkerKey);
            PlayerPrefs.Save();CrewMovementPreference.ReloadPreferences();Time.timeScale=SessionState.GetFloat(Key+".timescale",1);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(rows.Any(r=>r.StartsWith("FAIL "))?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Crew tracking watchdog expired.");return;}
        if(running||!BattleManager.instance||!BattleManager.instance.BattleActive||!CameraPanTouchOnly.Instance||!LiveMiniMap.Instance||!HomeTerritorySystem.Instance)return;
        running=true;deadline=EditorApplication.timeSinceStartup+540;
        var host=new GameObject("Crew Tracking Test Runner");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<CrewTrackingTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator test)
    {
        while(true){bool more=false;object current=null;try{more=test.MoveNext();if(more)current=test.Current;}catch(Exception error){Finish(error.ToString());yield break;}if(!more){Finish(null);yield break;}yield return current;}
    }
    static void Check(bool ok,string text){rows.Add((ok?"PASS ":"FAIL ")+text);RecoveryReport.WriteLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);
        if(!telemetryWritten)WriteTelemetry(new EditorTelemetry{utc=DateTime.UtcNow.ToString("O"),stage="not reached",status="No telemetry sample completed before fixture exit."});
        LiveMiniMap.Instance?.CloseTacticalMap();CrewTrackingSystem.StopTracking();Time.timeScale=SessionState.GetFloat(Key+".timescale",1);
        rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add("Native isolated fixture: six durable crew retain production Data.Speed 2.5. Pace/boost checks run at time scale 1; only travel/follow observation uses time scale 3. NavMesh Warp arranges split/offscreen scenarios, never supplies asserted movement. Unrelated pressure suppressed. Preferences/save override restored. No device/performance/natural campaign validation.");
        rows.Add("UTC "+DateTime.UtcNow.ToString("O"));RecoveryReport.WriteLines(Report,rows);EditorApplication.isPlaying=false;
    }
    static IEnumerator Frames(int count=3){for(int i=0;i<count;i++)yield return null;}
    static void WriteTelemetry(EditorTelemetry value)
    {
        File.WriteAllText(TelemetryBase+".json",JsonUtility.ToJson(value,true));
        File.WriteAllText(TelemetryBase+".txt",value.scope+"\n"+value.status+"\n"+
            $"UTC {value.utc}\nStage: {value.stage}\nViewport {value.width}x{value.height}; Unity {value.unityVersion}; batch {value.batchMode}; {value.graphicsDevice} / {value.graphicsApi}\n"+
            $"Pipeline {value.pipelineAsset}; actual map renderer {value.mapRendererData}; actual main renderer {value.mainRendererData}\n"+
            $"Map camera options: shadows {value.mapShadows}, post {value.mapPostProcessing}, HDR {value.mapAllowHDR}, MSAA {value.mapAllowMSAA}, AA {value.mapAntialiasing}\n"+
            $"Observed rendered-frame samples {value.observedRenderedFrames}/{value.targetSamples}; complete {value.complete}; elapsed {value.elapsedSeconds:F3}s; time scale {value.timeScale:F2}\n"+
            $"unscaledDeltaTime milliseconds: median {value.medianMilliseconds:F3}, p95 {value.p95Milliseconds:F3}, minimum {value.minMilliseconds:F3}, maximum {value.maxMilliseconds:F3}\n"+
            $"Actors: living crew {value.livingCrew}, active enemies {value.activeEnemies}, selected {value.selectedCrew}, moving at start {value.movingCrew}\n"+
            $"Observed peaks: routes {value.maxVisibleRoutes}/{value.routeCap}, cached routes {value.maxCachedRoutes}/{value.routeCap}, labels {value.maxVisibleLabels}/{value.labelCap}, effects {value.maxActiveEffects}\n"+
            value.effectsBudget+"\nRaw samples and rendered/update frame counter bounds are in the JSON. No profiler/GC attribution was collected. Sampling overhead and the editor are included.\n");
        telemetryWritten=true;
    }
    static IEnumerator CaptureEditorTelemetry()
    {
        var value=new EditorTelemetry{utc=DateTime.UtcNow.ToString("O"),stage="Following moving Brick with an independently moving Dex; production roster speed 2.5 and brisk gait, simulation time scale 1",unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),batchMode=Application.isBatchMode,width=Screen.width,height=Screen.height,timeScale=Time.timeScale};
        PopulateRendererContext(value);
        foreach(var actor in BattleManager.instance.PlayerAgents)if(actor&&actor.IsAlive){value.livingCrew++;if(actor.IsSelected)value.selectedCrew++;var agent=actor.GetComponent<NavMeshAgent>();if(agent&&agent.velocity.sqrMagnitude>.01f)value.movingCrew++;}
        foreach(var enemy in BattleManager.instance.EnemyAgents)if(enemy&&enemy.IsAlive&&enemy.gameObject.activeInHierarchy)value.activeEnemies++;
        var labels=UnityEngine.Object.FindFirstObjectByType<CrewWorldLabels>();var effects=UnityEngine.Object.FindFirstObjectByType<GameFeedbackBurst>();
        var samples=new List<float>(value.targetSamples);float began=Time.realtimeSinceStartup;int last=Time.renderedFrameCount;
        value.renderedCounterStart=last;value.updateFrameStart=Time.frameCount;
        while(samples.Count<value.targetSamples&&Time.realtimeSinceStartup-began<30)
        {
            yield return null;
            int rendered=Time.renderedFrameCount;
            if(rendered!=last){last=rendered;if(Time.unscaledDeltaTime>0&&!float.IsNaN(Time.unscaledDeltaTime)&&!float.IsInfinity(Time.unscaledDeltaTime))samples.Add(Time.unscaledDeltaTime*1000);}
            var tracking=CrewTrackingSystem.Instance;
            if(tracking){value.maxVisibleRoutes=Mathf.Max(value.maxVisibleRoutes,tracking.VisibleRouteCount);value.maxCachedRoutes=Mathf.Max(value.maxCachedRoutes,tracking.CachedMemberCount);}
            if(labels)value.maxVisibleLabels=Mathf.Max(value.maxVisibleLabels,labels.VisibleLabelCount);
            if(effects)value.maxActiveEffects=Mathf.Max(value.maxActiveEffects,effects.ActiveCount);
        }
        value.elapsedSeconds=Time.realtimeSinceStartup-began;value.renderedCounterEnd=Time.renderedFrameCount;value.updateFrameEnd=Time.frameCount;value.observedRenderedFrames=samples.Count;
        value.complete=samples.Count==value.targetSamples;value.status=value.complete?"Completed bounded editor timing observation; no timing pass threshold applied.":"Incomplete: rendered-frame target was not observed within the 30-second bound; no extrapolation or device claim.";
        value.renderedFrameMilliseconds=samples.ToArray();samples.Sort();
        if(samples.Count>0){int middle=samples.Count/2;value.medianMilliseconds=samples.Count%2==0?(samples[middle-1]+samples[middle])*.5f:samples[middle];value.p95Milliseconds=samples[Mathf.Clamp(Mathf.CeilToInt(samples.Count*.95f)-1,0,samples.Count-1)];value.minMilliseconds=samples[0];value.maxMilliseconds=samples[samples.Count-1];}
        WriteTelemetry(value);
    }
    static int FeatureFreeRendererIndex(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset pipeline)
    {
        if(!pipeline)return -1;var renderers=pipeline.rendererDataList;
        for(int i=0;i<renderers.Length;i++)if(renderers[i] is UnityEngine.Rendering.Universal.UniversalRendererData renderer&&renderer.renderingMode==UnityEngine.Rendering.Universal.RenderingMode.Forward&&renderer.rendererFeatures.Count==0)return i;
        return -1;
    }
    static void PopulateRendererContext(EditorTelemetry value)
    {
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if(!pipeline)return;value.pipelineAsset=pipeline.name;var renderers=pipeline.rendererDataList;
        var map=LiveMiniMap.Instance.MapCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var main=Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        value.mapShadows=map&&map.renderShadows;value.mapPostProcessing=map&&map.renderPostProcessing;
        value.mapAllowHDR=LiveMiniMap.Instance.MapCamera.allowHDR;value.mapAllowMSAA=LiveMiniMap.Instance.MapCamera.allowMSAA;value.mapAntialiasing=map?map.antialiasing.ToString():"missing camera data";
        for(int i=0;i<renderers.Length;i++)if(renderers[i])
        {var renderer=pipeline.GetRenderer(i);if(map&&map.scriptableRenderer==renderer)value.mapRendererData=renderers[i].name;if(main&&main.scriptableRenderer==renderer)value.mainRendererData=renderers[i].name;}
    }
    static IEnumerator Until(Func<bool> ready,float seconds=12)
    {float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;}
    static float Flat(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    static float Length(NavMeshPath path){float length=0;var corners=path.corners;for(int i=1;i<corners.Length;i++)length+=Vector3.Distance(corners[i-1],corners[i]);return length;}
    static Vector3 Destination(AgentController actor,float minimumDistance=90,Vector3? avoid=null)
    {
        var nav=actor.GetComponent<NavMeshAgent>();var filter=new NavMeshQueryFilter{agentTypeID=nav.agentTypeID,areaMask=nav.areaMask};var path=new NavMeshPath();
        foreach(var target in CityGameplay.Instance.Locations)
            if(Flat(target,actor.transform.position)>=minimumDistance&&(!avoid.HasValue||Flat(target,avoid.Value)>50)&&
                NavMesh.SamplePosition(target,out var hit,8,filter)&&nav.CalculatePath(hit.position,path)&&path.status==NavMeshPathStatus.PathComplete&&Length(path)>=minimumDistance)return hit.position;
        for(int ring=1;ring<=4;ring++)for(int point=0;point<16;point++)
        {
            float angle=point*Mathf.PI/8;var target=actor.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(minimumDistance+ring*50);
            if(avoid.HasValue&&Flat(target,avoid.Value)<50)continue;
            if(NavMesh.SamplePosition(target,out var hit,10,filter)&&Flat(actor.transform.position,hit.position)>=minimumDistance&&nav.CalculatePath(hit.position,path)&&path.status==NavMeshPathStatus.PathComplete&&Length(path)>=minimumDistance)return hit.position;
        }
        throw new InvalidOperationException("No complete crew path for fixture at "+actor.transform.position+" with minimum distance "+minimumDistance);
    }
    static bool Arrange(AgentController actor,Vector3 requested)
    {
        var nav=actor.GetComponent<NavMeshAgent>();var filter=new NavMeshQueryFilter{agentTypeID=nav.agentTypeID,areaMask=nav.areaMask};
        if(!NavMesh.SamplePosition(requested,out var hit,8,filter)||!nav.Warp(hit.position))return false;
        nav.ResetPath();actor.TryCommandMoveTo(hit.position,1);return true;
    }
    static bool RealCorners(CrewTrackingSystem.RouteRecord record)
    {
        if(record==null||record.Count<2||!record.Nav||record.Count>record.Corners.Length)return false;
        var path=record.Nav.path;var actual=path.corners;
        if(path.status!=NavMeshPathStatus.PathComplete||actual.Length<2||Flat(record.Corners[record.Count-1],actual[actual.Length-1])>.7f)return false;
        var filter=new NavMeshQueryFilter{agentTypeID=record.Nav.agentTypeID,areaMask=record.Nav.areaMask};
        for(int i=1;i<record.Count;i++)if(!NavMesh.SamplePosition(record.Corners[i],out var hit,.6f,filter)||Flat(hit.position,record.Corners[i])>.6f)return false;
        return true;
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);float x0=float.MaxValue,y0=float.MaxValue,x1=float.MinValue,y1=float.MinValue;
        foreach(var corner in corners){var p=RectTransformUtility.WorldToScreenPoint(null,corner);x0=Mathf.Min(x0,p.x);y0=Mathf.Min(y0,p.y);x1=Mathf.Max(x1,p.x);y1=Mathf.Max(y1,p.y);}
        return Rect.MinMaxRect(x0,y0,x1,y1);
    }
    static bool TerrainAgrees(LiveMiniMap map,Vector3 reference)
    {
        foreach(var delta in new[]{Vector3.zero,new Vector3(25,0,-15),new Vector3(-30,0,20)})
            if(!map.TryProjectDisplayedTerrain(reference+delta,out var displayed)||(displayed-map.WorldToMap(reference+delta)).sqrMagnitude>.000001f)return false;
        return true;
    }
    static int OverlayVertexCount(TacticalMapOverlay overlay)
    {
        Canvas.ForceUpdateCanvases();var mesh=overlay.canvasRenderer.GetMesh();
        return mesh?mesh.vertexCount:0;
    }
    static void Isolate()
    {
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();CityUiSheets.CloseCurrent();
        foreach(var director in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))director.enabled=false;
        foreach(var enemy in BattleManager.instance.EnemyAgents)if(enemy){enemy.SetCinematicIdle(true);enemy.isHostile=false;}
        if(LivePoliceSystem.Instance){LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;}
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(1);Isolate();CrewTrackingSystem.Ensure();
        var crew=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();var brick=crew[0];var dex=crew[1];var ash=crew[2];
        var selection=AgentSelectionManager.instance;var camera=CameraPanTouchOnly.Instance;var city=CityGameplay.Instance;var map=LiveMiniMap.Instance;var nav=brick.GetComponent<NavMeshAgent>();
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        int leanRenderer=FeatureFreeRendererIndex(pipeline);
        var mapCameraData=map.MapCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var mainCameraData=Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        bool rendererMatches=leanRenderer>=0&&mapCameraData&&mapCameraData.scriptableRenderer==pipeline.GetRenderer(leanRenderer);
        Check(rendererMatches&&!mapCameraData.renderShadows&&!mapCameraData.renderPostProcessing&&!map.MapCamera.allowHDR&&!map.MapCamera.allowMSAA&&mapCameraData.antialiasing==UnityEngine.Rendering.Universal.AntialiasingMode.None,"Actual minimap camera uses the feature-free forward renderer with explicit low-cost camera options; rendererMatch="+rendererMatches+"; shadows="+(mapCameraData&&mapCameraData.renderShadows)+"; post="+(mapCameraData&&mapCameraData.renderPostProcessing)+"; HDR="+map.MapCamera.allowHDR+"; MSAA="+map.MapCamera.allowMSAA+"; AA="+(mapCameraData?mapCameraData.antialiasing.ToString():"missing"));
        CityAtmosphere.Apply();
        Check(leanRenderer>=0&&mapCameraData&&!mapCameraData.renderShadows&&!mapCameraData.renderPostProcessing&&mapCameraData.antialiasing==UnityEngine.Rendering.Universal.AntialiasingMode.None&&!map.MapCamera.allowHDR&&!map.MapCamera.allowMSAA&&mapCameraData.scriptableRenderer==pipeline.GetRenderer(leanRenderer),"Repeating the actual atmospheric world-camera setup preserves minimap-owned shadows, AA and renderer policy");
        Check(pipeline&&mainCameraData&&mainCameraData.scriptableRenderer==pipeline.scriptableRenderer&&pipeline.scriptableRenderer==pipeline.GetRenderer(0),"Main world camera retains the original pipeline default renderer at index zero");
        Check(crew.Length==6&&crew.All(a=>Mathf.Abs(a.Data.Speed-2.5f)<.001f),"Fixture begins with six actual living crew at production roster speed 2.5");
        Check(CrewMovementPreference.RunOnTap&&Mathf.Abs(CrewMovementPreference.SpeedMultiplier-1.65f)<.001f,"Fresh movement preferences use run-on-tap and default brisk pace 1.65");
        float baselineTimeScale=Time.timeScale;var target=Destination(brick,120);
        selection.DeselectAll();selection.Select(brick);Check(brick.TryCommandMoveTo(target),"Production-speed member accepts a real complete city route");
        yield return Until(()=>nav.hasPath&&!nav.pathPending);yield return Frames();
        float runningSpeed=2.5f*1.65f;
        Check(brick.CurrentState==AgentController.State.MovingToPoint&&Mathf.Abs(nav.speed-runningSpeed)<.02f,"Run gait uses the actual NavMesh speed after repeated runtime Updates");
        CrewMovementPreference.RunOnTap=false;yield return Frames(5);
        Check(Mathf.Abs(nav.speed-runningSpeed*.52f)<.02f,"Walk preference changes the live NavMesh gait to 52 percent without replacing roster speed");
        CrewMovementPreference.RunOnTap=true;yield return Frames(5);
        Check(Mathf.Abs(nav.speed-runningSpeed)<.02f,"Run preference takes effect again after runtime Updates");
        brick.ApplyTemporaryBoost(BoostType.Speed,.5f,3);yield return Frames(5);yield return new WaitForSeconds(.7f);
        Check(Mathf.Abs(nav.speed-runningSpeed*1.5f)<.03f,"Temporary speed boost survives Update instead of being overwritten by movement preference");
        yield return Until(()=>Mathf.Abs(brick.CurrentTravelSpeed-runningSpeed)<.02f,7);yield return Frames();
        Check(Mathf.Abs(nav.speed-runningSpeed)<.03f,"Temporary speed boost expires back to the configured live gait");
        GameManager.Save();var saved=SaveDataInLocal.DataLoad();
        Check(saved.RecruitedAgents.All(a=>Mathf.Abs(a.Speed-2.5f)<.001f)&&Mathf.Approximately(Time.timeScale,baselineTimeScale),"Pace/gait/boost changes preserve saved production speed and global simulation time scale");

        // Only this travel observation is accelerated. Assert displacement from
        // actual navigation frames, not a Warp or hand-written destination line.
        Time.timeScale=3;var origin=brick.transform.position;CrewTrackingSystem.TrackMember(brick);
        yield return Until(()=>Flat(origin,brick.transform.position)>12,12);yield return new WaitForSecondsRealtime(.4f);
        var record=CrewTrackingSystem.Instance.GetRoute(brick);
        Check(Flat(origin,brick.transform.position)>12&&RealCorners(record),"Actual city travel advances the crew and exposes real complete remaining path corners/endpoints");
        Check(camera.IsFollowingMember&&camera.FollowedMember==brick&&Flat(CityActivityStreaming.GroundFocus(Camera.main),brick.transform.position)<25,"Camera follows the moving individual rather than an empty group centroid");
        var worldLabels=UnityEngine.Object.FindFirstObjectByType<CrewWorldLabels>();
        bool named=worldLabels&&worldLabels.TryGetLabel(brick,out var identity)&&identity&&identity.gameObject.activeInHierarchy&&identity.text.Contains(brick.Data.AgentName)&&identity.text.Contains("TO ")&&!identity.raycastTarget;
        Check(named&&worldLabels.VisibleLabelCount<=CrewWorldLabels.MaximumVisibleLabels,"Tracked moving crew has a bounded real name/destination label without an extra pointer hitbox");
        // Separate honest editor timing observation: no production speed changes,
        // no frame-rate threshold and no inference about a physical device.
        Time.timeScale=baselineTimeScale;dex.TryCommandMoveTo(Destination(dex,120));selection.Select(dex);yield return Frames(3);
        yield return CaptureEditorTelemetry();Time.timeScale=3;
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/crew-world-routes.png");yield return null;
        var reroute=Destination(brick,80,target);int revision=brick.OrderRevision;
        Check(brick.TryCommandMoveTo(reroute),"A new real route replaces the tracked member's previous destination");yield return Frames(6);yield return new WaitForSecondsRealtime(.3f);
        record=CrewTrackingSystem.Instance.GetRoute(brick);
        Check(record!=null&&record.Revision>revision&&RealCorners(record)&&Flat(record.Destination,nav.destination)<1,"World route record refreshes the accepted reroute and actual endpoint");
        map.TacticalOverlay.RefreshSnapshot();
        Check(!LiveMiniMap.IsExpanded&&map.TacticalOverlay.TryGetRoute(brick,out var compactStart,out var compactGoal,out int compactSegments)&&compactSegments>0&&Flat(compactStart,brick.transform.position)<.1f&&Flat(compactGoal,nav.path.corners.Last())<.7f,"Compact map follows the accepted reroute's actual remaining navigation start and endpoint");
        revision=brick.OrderRevision;var preserved=brick.CommandDestination;var preservedNav=nav.destination;
        Check(!brick.TryCommandMoveTo(new Vector3(90000,0,90000))&&brick.OrderRevision==revision&&Flat(brick.CommandDestination,preserved)<.1f&&Flat(nav.destination,preservedNav)<.1f,"Unreachable reroute retains the exact previous live order and navigation destination");
        map.TacticalOverlay.RefreshSnapshot();
        Check(map.TacticalOverlay.TryGetRoute(brick,out compactStart,out compactGoal,out compactSegments)&&compactSegments>0&&Flat(compactGoal,preservedNav)<.7f,"Rejected reroute preserves the compact map's real previous destination");
        Time.timeScale=baselineTimeScale;camera.FocusOn(city.Home);yield return Frames();
        Check(!camera.IsFollowingMember&&camera.FollowedMember==null,"Explicit manual camera focus cancels individual follow without changing crew orders");

        var far=Destination(brick,180);
        Check(Arrange(brick,city.Home+Vector3.left*4)&&Arrange(dex,far)&&Arrange(ash,city.Home+Vector3.right*5),"NavMesh Warp arranges a split group and separate Home guard for camera checks");yield return Frames(5);
        selection.DeselectAll();selection.Select(ash);selection.ArmGuardCommand();selection.CommandSelectedGround(city.Home+Vector3.right*5);
        int guardRevision=ash.OrderRevision;var guardOrder=ash.CurrentOrder;
        selection.DeselectAll();selection.Select(brick);selection.Select(dex);camera.CenterOnSelection();yield return new WaitForSecondsRealtime(1.5f);
        Check(camera.IsFollowingMember&&camera.FollowedMember==dex&&AgentSelectionManager.LastPicked==dex,"Centering a split selection follows LastPicked instead of the empty midpoint");
        Check(selection.SelectedAgents.Count==2&&brick.IsSelected&&dex.IsSelected&&ash.CurrentOrder==guardOrder&&ash.OrderRevision==guardRevision,"Individual camera centering preserves the command selection and another group's guard");
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/crew-split-follow.png");yield return null;
        CrewTrackingSystem.FocusMember(brick);yield return new WaitForSecondsRealtime(1);
        Check(!camera.IsFollowingMember&&selection.SelectedAgents.Count==2&&ash.OrderRevision==guardRevision&&Flat(CityActivityStreaming.GroundFocus(Camera.main),brick.transform.position)<25,"Focus is a one-time individual view and leaves selection/other duties intact");

        // Query from Dex after the split arrangement, rather than assuming a
        // destination reachable from Brick is reachable from this deployment.
        var farOther=Destination(dex,100,far);var dexOrigin=dex.transform.position;
        bool deployedRouteAccepted=dex.TryCommandMoveTo(farOther);
        Check(deployedRouteAccepted,"The independently deployed member accepts a real route before map tracking; failure="+dex.LastOrderFailure+"; from="+dexOrigin+"; target="+farOther);
        yield return Until(()=>Flat(dexOrigin,dex.transform.position)>1.2f,4);
        Check(deployedRouteAccepted&&Flat(dexOrigin,dex.transform.position)>1.2f&&RealCorners(CrewTrackingSystem.Instance.GetRoute(dex)),"Independently deployed Dex actually travels along his own complete route before map tracking");
        map.OpenTacticalMap();yield return Frames();map.TrackMember(dex);
        Check(!camera.enabled&&CrewTrackingSystem.TrackedMember==dex&&camera.FollowedMember==dex,"Tracking from the expanded map pins an individual while world camera input stays frozen");
        Check(TerrainAgrees(map,city.Home),"Rendered camera matrix plus displayed UV crop matches map coordinates independently of inverse mapping");
        var rect=map.MapFrame.rect;var center=RectTransformUtility.WorldToScreenPoint(null,map.MapFrame.TransformPoint(rect.center));
        map.OnMapPointerDown(center);map.OnMapPointerDrag(center+new Vector2(45,28));map.OnMapPointerUp();
        Check(TerrainAgrees(map,city.Home),"Dragging immediately synchronizes cached terrain crop and live marker projection");map.OnMapScroll(1);
        Check(TerrainAgrees(map,city.Home),"Zooming immediately synchronizes rendered terrain and map marker projection");
        map.CloseTacticalMap();yield return new WaitForSecondsRealtime(1.3f);
        Check(camera.enabled&&camera.IsFollowingMember&&camera.FollowedMember==dex&&Flat(CityActivityStreaming.GroundFocus(Camera.main),dex.transform.position)<25,"Map collapse re-enables the camera and preserves individual follow target/focus");
        camera.FocusOn(city.Home);map.OpenTacticalMap();yield return Frames();map.FocusMember(dex);yield return new WaitForSecondsRealtime(1.3f);
        Check(!LiveMiniMap.IsExpanded&&!camera.IsFollowingMember&&Flat(CityActivityStreaming.GroundFocus(Camera.main),dex.transform.position)<25,"Map FOCUS closes first so camera OnEnable cannot discard the requested destination");

        // Six genuine live walking routes exercise the compact cap beyond its
        // boundary. These orders are not substituted lines or arranged Warps.
        bool routesAccepted=true;foreach(var actor in crew)routesAccepted&=actor.TryCommandMoveTo(Destination(actor,120));
        yield return Until(()=>crew.All(a=>{var n=a.GetComponent<NavMeshAgent>();return n.hasPath&&!n.pathPending&&n.pathStatus==NavMeshPathStatus.PathComplete;}));yield return Frames();
        Check(routesAccepted,"All six production-speed crew accept complete real routes for the compact-map budget scenario");
        selection.DeselectAll();for(int i=0;i<crew.Length-1;i++)selection.Select(crew[i]);
        CrewTrackingSystem.TrackMember(crew[5]);camera.FocusOn(city.Home);
        var routeRevisions=crew.Select(a=>a.OrderRevision).ToArray();
        map.OpenTacticalMap();map.TacticalOverlay.SetRouteMode(TacticalMapOverlay.Routes.All);
        Check(map.TacticalOverlay.VisibleRouteCount==6,"Expanded All view retains six actual routes before compact-map transition");
        map.CloseTacticalMap();
        Check(map.TacticalOverlay.VisibleRouteCount==TacticalMapOverlay.MaximumCompactRoutes&&map.TacticalOverlay.TryGetRoute(crew[5],out _,out _,out _)&&!crew[5].IsSelected,"Collapsing immediately caps real routes at three and prioritizes an unselected tracked member");
        map.TacticalOverlay.SetRouteMode(TacticalMapOverlay.Routes.Off);int withoutRouteVertices=OverlayVertexCount(map.TacticalOverlay);
        Check(map.TacticalOverlay.VisibleRouteCount==0,"Compact route Off removes snapshots without replacing crew orders");
        map.TacticalOverlay.SetRouteMode(TacticalMapOverlay.Routes.Selected);int withRouteVertices=OverlayVertexCount(map.TacticalOverlay);
        Check(withoutRouteVertices>0&&withRouteVertices>withoutRouteVertices&&!map.TacticalOverlay.raycastTarget,"Compact routes add actual rendered UGUI mesh geometry without a map-order hitbox");
        selection.DeselectAll();selection.Select(crew[0]);map.TacticalOverlay.SetRouteMode(TacticalMapOverlay.Routes.All);
        bool unrelated=false;for(int i=1;i<crew.Length-1;i++)unrelated|=map.TacticalOverlay.TryGetRoute(crew[i],out _,out _,out _);
        Check(map.TacticalOverlay.VisibleRouteCount==2&&!unrelated&&map.TacticalOverlay.TryGetRoute(crew[0],out _,out _,out _)&&map.TacticalOverlay.TryGetRoute(crew[5],out _,out _,out _),"Compact All still shows only selected and tracked crew, excluding other moving groups");
        map.TacticalOverlay.SetRouteMode(TacticalMapOverlay.Routes.Tracked);
        Check(map.TacticalOverlay.VisibleRouteCount==1&&map.TacticalOverlay.TryGetRoute(crew[5],out _,out _,out _),"Compact Tracked mode keeps only the actual pinned member's path");
        Check(crew.Select((a,i)=>a.OrderRevision==routeRevisions[i]).All(same=>same)&&selection.SelectedAgents.Count==1&&crew[0].IsSelected,"Route display modes preserve all six live orders and command selection");
        map.TacticalOverlay.SetRouteMode(TacticalMapOverlay.Routes.Selected);
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/crew-compact-map-routes.png");yield return null;

        // Arrangement only: all watched people occupy a distant real walking
        // region to stress edge-slot packing, not to prove travel completion.
        camera.FocusOn(city.Home);CrewTrackingSystem.TrackMember(crew[5]);camera.FocusOn(city.Home);
        bool arranged=true;for(int i=0;i<crew.Length;i++)arranged&=Arrange(crew[i],far+new Vector3((i%3)*2,0,(i/3)*2));
        selection.DeselectAll();foreach(var actor in crew)selection.Select(actor);CrewTrackingSystem.ShowAllRoutes=true;
        yield return new WaitForSecondsRealtime(1.5f);Check(arranged,"Crew finder scenario places six watched actors on actual distant NavMesh points");
        var frame=WorldButtonLayer.Frame();var buttonRects=new List<Rect>();named=true;
        for(int i=0;i<5;i++)
        {
            var button=frame.Find("CrewOffscreen"+i) as RectTransform;if(!button||!button.gameObject.activeInHierarchy)continue;
            buttonRects.Add(ScreenRect(button));var text=button.GetComponentInChildren<TMPro.TextMeshProUGUI>().text;
            named&=crew.Any(a=>text.Contains(a.Data.AgentName.ToUpperInvariant()));
        }
        bool packed=true;for(int i=0;i<buttonRects.Count;i++)for(int j=i+1;j<buttonRects.Count;j++)packed&=!buttonRects[i].Overlaps(buttonRects[j]);
        var tracking=frame.Find("IndividualTracking") as RectTransform;bool trackingShown=tracking&&tracking.gameObject.activeInHierarchy;
        if(trackingShown){var bar=ScreenRect(tracking);foreach(var box in buttonRects)packed&=!box.Overlaps(bar);}
        Check(buttonRects.Count>=3&&buttonRects.Count<=5&&named,"Crew finder shows three-to-five bounded buttons with actual member names");
        Check(trackingShown&&packed,"Finder buttons never overlap one another or the visible individual tracking bar");
        Check(CrewTrackingSystem.Instance.VisibleRouteCount<=CrewTrackingSystem.MaximumRoutes&&CrewTrackingSystem.Instance.CachedMemberCount<=CrewTrackingSystem.MaximumRoutes,"Actual world tracking records/rendered route count stay within the 24-route production budget");
        var labels=UnityEngine.Object.FindFirstObjectByType<CrewWorldLabels>();
        Check(labels&&labels.VisibleLabelCount<=CrewWorldLabels.MaximumVisibleLabels,"World crew labels retain the six-label bound while finder UI is active");
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/crew-finder.png");yield return null;
        Check(crew.All(a=>Mathf.Abs(a.Data.Speed-2.5f)<.001f)&&Mathf.Approximately(Time.timeScale,baselineTimeScale),"Tracking/finder checks finish with unchanged roster speed and restored baseline time scale");
    }
}
public sealed class CrewTrackingTestRunner:MonoBehaviour{}
#endif
