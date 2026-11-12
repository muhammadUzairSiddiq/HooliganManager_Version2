#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>Native projection, individual selection, real routes and explicit map orders.</summary>
[InitializeOnLoad]
public static class TacticalMapPlaytest
{
    const string Key="HM.TacticalMapPlaytest",Report="Artifacts/Recovery/tactical-map-playtest.txt",Save="Artifacts/Recovery/tactical-map-save.json";
    static readonly List<string> rows=new List<string>();
    static bool running,finished;
    static double deadline;
    static TacticalMapPlaytest()
    {
        if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;
    }
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    [MenuItem("Hooligan/Recovery/Run tactical map check")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run from edit mode.");
        Directory.CreateDirectory("Artifacts/Recovery");
        SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetBool(Key+".newsExists",PlayerPrefs.HasKey(CityNewsFeed.ExpandedPreferenceKey));SessionState.SetInt(Key+".newsValue",PlayerPrefs.GetInt(CityNewsFeed.ExpandedPreferenceKey,1));
        SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var data=new PlayerData{PlayerName="Tactical Map QA",Money=5000,Fans=3};RecoveryProgression.Begin(data);
        foreach(var name in new[]{"Brick","Dex","Ash"})data.RecruitedAgents.Add(new AgentData(name,hp:1000,speed:10));
        SaveDataInLocal.DataSave(data);SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING "+DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {running=finished=false;rows.Clear();deadline=EditorApplication.timeSinceStartup+150;CityGameplay.HomeMode=true;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("Gameplay");}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");
            int marker=SessionState.GetInt(Key+".marker",-1);if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);
            if(SessionState.GetBool(Key+".newsExists",false))PlayerPrefs.SetInt(CityNewsFeed.ExpandedPreferenceKey,SessionState.GetInt(Key+".newsValue",1));else PlayerPrefs.DeleteKey(CityNewsFeed.ExpandedPreferenceKey);PlayerPrefs.Save();
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(rows.Any(r=>r.StartsWith("FAIL "))?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Tactical map watchdog expired.");return;}
        if(running||!BattleManager.instance||!BattleManager.instance.BattleActive||!LiveMiniMap.Instance||!HomeTerritorySystem.Instance)return;
        running=true;deadline=EditorApplication.timeSinceStartup+240;
        var host=new GameObject("Tactical Map Test Runner");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<TacticalMapTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator test)
    {
        while(true){bool more=false;object current=null;try{more=test.MoveNext();if(more)current=test.Current;}catch(Exception error){Finish(error.ToString());yield break;}if(!more){Finish(null);yield break;}yield return current;}
    }
    static void Check(bool ok,string text){rows.Add((ok?"PASS ":"FAIL ")+text);File.WriteAllLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);
        LiveMiniMap.Instance?.CloseTacticalMap();
        rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add("Native isolated tactical-map fixture: real city/navigation/selection, durable fast crew and unrelated pressure suppressed. No device touch, frame-rate, natural campaign or real save used.");
        rows.Add("UTC "+DateTime.UtcNow.ToString("O"));File.WriteAllLines(Report,rows);EditorApplication.isPlaying=false;
    }
    static Vector2 ScreenPoint(LiveMiniMap map,Vector2 normalized)
    {
        var rect=map.MapFrame.rect;
        var local=new Vector3(rect.xMin+normalized.x*rect.width,rect.yMin+normalized.y*rect.height,0);
        return RectTransformUtility.WorldToScreenPoint(null,map.MapFrame.TransformPoint(local));
    }
    static void CheckExpandedPresentation(LiveMiniMap map,string size)
    {
        var background=map.ExpandedBackdrop;var canvas=background?background.GetComponentInParent<Canvas>():null;
        var corners=new Vector3[4];if(background)background.rectTransform.GetWorldCorners(corners);
        bool covers=background&&corners[0].x<=1&&corners[0].y<=1&&corners[2].x>=Screen.width-1&&corners[2].y>=Screen.height-1;
        Check(background&&background.gameObject.activeInHierarchy&&background.type==UnityEngine.UI.Image.Type.Simple&&!background.sprite&&background.color.a>=.999f&&background.raycastTarget&&covers,size+" expanded map has an opaque solid backdrop over the full display, including safe-area gutters");
        int hudOrder=int.MinValue;bool foundHud=false;
        foreach(var label in UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
        if(label.transform.parent&&(label.transform.parent.name=="CityTop"||label.transform.parent.name=="SelectionCoach"))
        {var hud=label.GetComponentInParent<Canvas>();if(hud){foundHud=true;hudOrder=Mathf.Max(hudOrder,hud.rootCanvas.sortingOrder);}}
        Check(foundHud&&canvas&&canvas.sortingOrder>hudOrder&&canvas.sortingOrder<29500&&background.transform.parent==canvas.transform&&background.transform.GetSiblingIndex()==0,size+" map covers the actual Home/selection HUD while leaving settings and popups above it");
        var target=map.RenderTargetSize;float frameAspect=map.MapFrame.rect.width/map.MapFrame.rect.height;
        var origin=CityGameplay.Instance.Home;var a=ScreenPoint(map,map.WorldToMap(origin));var x=ScreenPoint(map,map.WorldToMap(origin+Vector3.right*10));var z=ScreenPoint(map,map.WorldToMap(origin+Vector3.forward*10));
        Check(target.x>512&&target.y>128&&Mathf.Abs((float)target.x/target.y-frameAspect)<.05f&&Mathf.Abs(Vector2.Distance(a,x)-Vector2.Distance(a,z))<.5f,size+" expanded feed uses bounded higher resolution with correct aspect and equal world-axis scale");
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(1);
        foreach(var director in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))director.enabled=false;
        foreach(var enemy in BattleManager.instance.EnemyAgents)if(enemy){enemy.SetCinematicIdle(true);enemy.isHostile=false;}
        if(LivePoliceSystem.Instance){LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;}
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();CityUiSheets.CloseCurrent();
        var map=LiveMiniMap.Instance;var city=CityGameplay.Instance;var home=HomeTerritorySystem.Instance;var selection=AgentSelectionManager.instance;
        var crew=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();var brick=crew[0];var dex=crew[1];var ash=crew[2];
        selection.DeselectAll();selection.Select(ash);selection.ArmGuardCommand();selection.CommandSelectedGround(city.Home);int ashRevision=ash.OrderRevision;var ashDuty=ash.CurrentOrder;
        selection.DeselectAll();map.SetCompactVisible(true);yield return null;Canvas.ForceUpdateCanvases();yield return null;
        var compactSurface=map.MapFrame.Find("DragCatcher").GetComponent<MiniMapDragCatcher>();var compactPoint=ScreenPoint(map,Vector2.one*.5f);
        var pointer=new PointerEventData(EventSystem.current){position=compactPoint};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==compactSurface.gameObject,"The real compact feed is the top raycast click target rather than a hidden or overlapped parent button");
        compactSurface.OnPointerDown(pointer);pointer.position+=new Vector2(40,20);compactSurface.OnDrag(pointer);compactSurface.OnPointerUp(pointer);compactSurface.OnPointerClick(pointer);
        Check(!LiveMiniMap.IsExpanded,"Dragging over the compact map cannot accidentally open it or issue a world order");
        pointer.position=compactPoint;compactSurface.OnPointerDown(pointer);compactSurface.OnPointerUp(pointer);compactSurface.OnPointerClick(pointer);
        yield return null;Canvas.ForceUpdateCanvases();yield return null;
        Check(LiveMiniMap.IsExpanded&&AgentSelectionManager.BlocksWorldTap(),"Expanded tactical map blocks accidental world taps while the live city continues");
        CheckExpandedPresentation(map,"1280x720");
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/tactical-map-1280x720.png");yield return null;
        Check(map.TacticalControls.VisibleCrewCount==3,"Expanded crew sheet names all three real living crew members");
        Check(CityNewsFeed.Instance&&!CityNewsFeed.Instance.HeaderTarget.gameObject.activeInHierarchy,"The tactical map covers/suspends its news feed without pausing city simulation");
        var captions=map.MapFrame.parent.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
        Check(crew.All(a=>captions.Any(t=>t.text.Contains(a.Data.AgentName))),"Actual roster names appear instead of invented or generic player labels");
        foreach(var offset in new[]{new Vector3(-40,0,-25),new Vector3(35,0,50),Vector3.zero})
        {
            var world=city.Home+offset;var restored=map.MapToWorld(map.WorldToMap(world),world.y);
            Check(Vector3.Distance(world,restored)<.01f,"Map/world projection roundtrips the same city coordinate "+offset);
        }
        var uv=new Vector2(.23f,.68f);var expected=map.MapToWorld(uv);
        Check(map.TryMapScreenPoint(ScreenPoint(map,uv),out var actual)&&Vector3.Distance(expected,actual)<.1f,"Map pointer conversion includes the real canvas scale and rectangular viewport");
        Check(map.TacticalOverlay.HasHomeFootprint&&home.WorldBounds.size.x>0,"Tactical Home footprint uses the actual world Home bounds");
        var brickMarker=map.MapFrame.GetComponentsInChildren<TacticalMapMemberTarget>().FirstOrDefault(m=>m.Member==brick&&m.GetComponent<UnityEngine.UI.Image>().raycastTarget);
        Check(brickMarker&&EventSystem.current&&ExecuteEvents.Execute(brickMarker.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler)&&brick.IsSelected,"Actual clickable map marker invokes the real additive selection handler");
        int brickRevision=brick.OrderRevision;map.ToggleMapMember(dex);
        Check(selection.SelectedAgents.Count==2&&brick.IsSelected&&dex.IsSelected&&LiveMiniMap.IsExpanded,"Map member taps add individuals without closing the map");
        map.ToggleMapMember(brick);
        Check(!brick.IsSelected&&dex.IsSelected&&selection.SelectedAgents.Count==1&&brick.OrderRevision==brickRevision,"Tapping Brick again removes only Brick without changing his duty");
        var destination=CityGameplay.ReachableApproach(dex.transform.position,city.Locations[1]);int dexRevision=dex.OrderRevision;
        Check(!map.TryIssueMapMove(destination)&&dex.OrderRevision==dexRevision,"Ordinary map exploration cannot silently issue a movement order");
        selection.CommandSelectedAttack();RtsGestureController.Instance?.BeginGuard();map.SetRerouteMode(true);
        Check(selection.PendingCommand==AgentSelectionManager.TargetCommand.Move&&RtsGestureController.Instance.Mode==RtsGestureController.InputMode.Camera,"Reroute explicitly clears prior Attack/Guard targeting before street movement");
        var surface=map.MapFrame.Find("DragCatcher").GetComponent<MiniMapDragCatcher>();
        var click=new PointerEventData(EventSystem.current){position=ScreenPoint(map,map.WorldToMap(destination))};
        surface.OnPointerDown(click);surface.OnPointerUp(click);surface.OnPointerClick(click);
        Check(selection.LastAcceptedOrderCount==1&&dex.OrderRevision>dexRevision,"An actual armed map surface click uses the real validated selection command and reports acceptance");
        Check(!map.RerouteMode&&dex.IsSelected&&selection.SelectedAgents.Count==1&&ash.OrderRevision==ashRevision&&ash.CurrentOrder==ashDuty,"Accepted map move is one-shot and preserves selection plus another group's guard duty");
        for(int i=0;i<8;i++)yield return null;
        map.TacticalOverlay.RefreshSnapshot();var nav=dex.GetComponent<NavMeshAgent>();
        bool snapshot=map.TacticalOverlay.TryGetRoute(dex,out var routeStart,out var routeEnd,out int segments);
        Check(snapshot&&segments>0&&Vector3.Distance(routeStart,dex.transform.position)<.1f&&Vector3.Distance(routeEnd,nav.path.corners.Last())<.1f,"Map route uses the member's actual remaining NavMesh corners and actual endpoint");
        map.TacticalOverlay.SetRouteMode(TacticalMapOverlay.Routes.Off);
        Check(map.TacticalOverlay.VisibleRouteCount==0,"Route overlay can be turned off without changing live orders");
        map.TacticalOverlay.SetRouteMode(TacticalMapOverlay.Routes.All);
        Check(map.TacticalOverlay.VisibleRouteCount>0&&map.TacticalOverlay.VisibleRouteCount<=32,"Optional all-crew routes remain bounded and draw existing live paths");
        int selectedBefore=selection.SelectedAgents.Count;map.TrackMember(brick);
        Check(CrewTrackingSystem.TrackedMember==brick&&selection.SelectedAgents.Count==selectedBefore&&dex.IsSelected,"Individual tracking is separate from the selected command group");
        map.ShowHomeOnMap();var homeUv=map.WorldToMap(home.WorldBounds.center);
        Check((homeUv-Vector2.one*.5f).sqrMagnitude<.0001f,"Show Home centers the same authoritative Home footprint on the map");
        map.ShowWholeCity();yield return null;
        Check(map.RunDragResponseSelfCheck(),"Terrain projection and pooled labels follow the same map drag without duplicate label objects");
        foreach(var size in new[]{"2400x1080","1024x768"})
        {
            LandscapeSceneBuilder.SetGameViewSize(size);yield return null;Canvas.ForceUpdateCanvases();yield return null;
            uv=new Vector2(.63f,.32f);expected=map.MapToWorld(uv);
            Check(map.TryMapScreenPoint(ScreenPoint(map,uv),out actual)&&Vector3.Distance(expected,actual)<.15f,size+" preserves accurate map pointer/world coordinates");
            CheckExpandedPresentation(map,size);
            ScreenCapture.CaptureScreenshot("Artifacts/Recovery/tactical-map-"+size+".png");yield return null;
        }
        LandscapeSceneBuilder.SetGameViewSize("1280x720");yield return null;Canvas.ForceUpdateCanvases();map.ShowHomeOnMap();
        selection.DeselectAll();map.SetRerouteMode(true);dexRevision=dex.OrderRevision;
        Check(!map.TryIssueMapMove(destination)&&dex.OrderRevision==dexRevision,"Empty-selection reroute rejects cleanly without borrowing another squad");
        map.SetRerouteMode(false);map.ToggleMapMember(brick);map.ToggleMapMember(dex);map.TacticalOverlay.SetRouteMode(TacticalMapOverlay.Routes.Selected);
        yield return null;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/tactical-map.png");yield return null;
        map.FocusMember(brick);yield return null;
        Check(!LiveMiniMap.IsExpanded&&AgentSelectionManager.instance.enabled&&CameraPanTouchOnly.Instance.enabled,"Focusing one member returns to the world and restores the pre-map camera/input components");
        yield return new WaitForSecondsRealtime(.2f);
        var feed=CityNewsFeed.Ensure();feed.SetVisible(true);yield return new WaitForSecondsRealtime(.2f);
        var headerCorners=new Vector3[4];feed.HeaderTarget.GetWorldCorners(headerCorners);var fixedTogglePosition=headerCorners[0];float timeScale=Time.timeScale;
        ExecuteEvents.Execute(feed.HeaderTarget.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);yield return new WaitForSecondsRealtime(.2f);
        feed.HeaderTarget.GetWorldCorners(headerCorners);
        Check(!CityNewsFeed.IsExpanded&&Vector3.Distance(fixedTogglePosition,headerCorners[0])<.1f&&Mathf.Approximately(timeScale,Time.timeScale),"Actual news toggle folds upward content without moving its hitbox or changing simulation speed");
        city.PostEvent("NEWS PIPELINE · actual city event forwarding check");
        Check(feed.Entries.Any(e=>e.message.Contains("NEWS PIPELINE")),"Existing CityGameplay.PostEvent reaches the campaign news history");
        string repeated="NEWS REPEAT · bounded duplicate activity";CityNewsFeed.Post(repeated);CityNewsFeed.Post(repeated);
        Check(feed.Entries.Count(e=>e.message==repeated)==1&&feed.Entries.Last().repeats==2,"Repeated event spam coalesces into one readable row with a repeat count");
        Check(!CityNewsFeed.Post(" ")&&CityNewsFeed.ClassifyChannel("TRAIN ARRIVAL")==CityNewsChannel.Arrival&&CityNewsFeed.ClassifyChannel("SHOP RAID")==CityNewsChannel.Raid&&CityNewsFeed.ClassifyChannel("MISSION COMPLETE")==CityNewsChannel.Mission,"Blank events are rejected and important arrival, raid and mission events have distinct channels");
        for(int i=0;i<70;i++)CityNewsFeed.Post("Disposable backlog "+i,CityNewsChannel.General,false,"fixture:"+i);
        feed.SetVisible(true);yield return new WaitForSecondsRealtime(.2f);
        Check(feed.EntryCount==CityNewsFeed.MaximumEntries&&feed.VisibleRowCount<=CityNewsFeed.MaximumEntries&&feed.PanelBounds.rect.height<=312,"News history and pooled scroll rows remain bounded after a 70-event synthetic backlog");
        var newsScroll=feed.GetComponentInChildren<UnityEngine.UI.ScrollRect>();Canvas.ForceUpdateCanvases();
        Check(newsScroll&&newsScroll.vertical&&newsScroll.content.rect.height>newsScroll.viewport.rect.height&&selection.IsPointerOverUI(RectTransformUtility.WorldToScreenPoint(null,feed.HeaderTarget.TransformPoint(feed.HeaderTarget.rect.center))),"News uses a real scroll viewport and its actual controls block ground taps");
        ash.TakeDamage(99999);float deathUntil=Time.realtimeSinceStartup+12;
        while(ash.gameObject.activeInHierarchy&&Time.realtimeSinceStartup<deathUntil)yield return null;
        int actualDeathNotices=feed.Entries.Count(e=>e.uniqueKey!=null&&e.uniqueKey.EndsWith(ash.Data.AgentId));
        CityNewsFeed.ReportCasualty(ash);CityNewsFeed.ReportCasualty(ash);yield return new WaitForSecondsRealtime(.2f);
        Check(!ash.IsAlive&&!ash.gameObject.activeInHierarchy&&actualDeathNotices==1&&feed.Entries.Count(e=>e.uniqueKey!=null&&e.uniqueKey.EndsWith(ash.Data.AgentId))==1&&feed.Entries.Last().critical&&feed.Entries.Last().message.Contains(ash.Data.AgentName),"Actual completed death callback publishes one critical real-name notice before repeated callbacks are suppressed");
        GameManager.Save();var loaded=SaveDataInLocal.DataLoad();
        Check(loaded.CityNews!=null&&loaded.CityNews.Count==CityNewsFeed.MaximumEntries&&loaded.CityNews.Last().critical&&loaded.CityNews.Last().message.Contains(ash.Data.AgentName),"Native campaign serialization retains bounded news and its named critical casualty");
        Check(!GamePopup.AnyOpen&&Mathf.Approximately(timeScale,Time.timeScale),"News and a single crew casualty do not open a global popup or pause the city");
        foreach(var size in new[]{"1280x720","2400x1080","1024x768"})
        {
            LandscapeSceneBuilder.SetGameViewSize(size);yield return null;Canvas.ForceUpdateCanvases();yield return null;
            bool newsInside=feed.TryGetScreenBounds(out var newsBounds)&&newsBounds.xMin>=Screen.safeArea.xMin-1&&newsBounds.yMin>=Screen.safeArea.yMin-1&&newsBounds.xMax<=Screen.safeArea.xMax+1&&newsBounds.yMax<=Screen.safeArea.yMax+1;
            Check(newsInside,size+" news feed stays inside the actual display safe area");
            ScreenCapture.CaptureScreenshot("Artifacts/Recovery/city-news-feed-"+size+".png");yield return null;
        }
        LandscapeSceneBuilder.SetGameViewSize("1280x720");yield return null;
    }
}
public sealed class TacticalMapTestRunner:MonoBehaviour{}
#endif
