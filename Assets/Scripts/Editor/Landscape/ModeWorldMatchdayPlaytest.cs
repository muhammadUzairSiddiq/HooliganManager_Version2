#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real mode entry, earned Away gate and raycastable stadium gameplay controls.</summary>
[InitializeOnLoad]
public static class ModeWorldMatchdayPlaytest
{
    const string Key="HM.ModeWorldMatchday";
    static bool EscortOnly=>SessionState.GetBool(Key+".escortOnly",false);
    static bool AutomaticOnly=>SessionState.GetBool(Key+".automaticOnly",false);
    static string Save=>AutomaticOnly?"Artifacts/Recovery/automatic-matchday-save.json":
        EscortOnly?"Artifacts/Recovery/matchday-escort-save.json":"Artifacts/Recovery/matchday-street-save.json";
    static string Report=>AutomaticOnly?"Artifacts/Recovery/automatic-matchday-playtest.txt":
        EscortOnly?"Artifacts/Recovery/matchday-escort-playtest.txt":"Artifacts/Recovery/matchday-street-playtest.txt";
    static readonly List<string> rows=new List<string>(),errors=new List<string>();
    static bool running,finished;static double deadline;
    static ModeWorldMatchdayPlaytest()
    {
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;
        if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
    }
    [MenuItem("Hooligan/Recovery/Run mode and world Matchday checks")]
    public static void Begin()=>Begin(false);
    public static void BeginEscort()=>Begin(true);
    public static void BeginAutomatic()=>Begin(false,true);
    static void Begin(bool escortOnly,bool automaticOnly=false)
    {
        if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Start from a saved edit scene.");
        SessionState.SetBool(Key+".escortOnly",escortOnly);
        SessionState.SetBool(Key+".automaticOnly",automaticOnly);
        Directory.CreateDirectory("Artifacts/Recovery");
        SessionState.SetString(Key+".scene",SceneManager.GetActiveScene().path);SessionState.SetString(Key+".override",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);
        SessionState.SetInt(Key+".news",PlayerPrefs.GetInt(CityNewsFeed.ExpandedPreferenceKey,-1));
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var data=new PlayerData{PlayerName="Mode/world QA",Money=7777,Fans=3,AcceptedPrivacyPolicy=true};RecoveryProgression.Begin(data);data.FirstSessionCoachHidden=true;
        foreach(var name in new[]{"Brick","Dex","Ash"})data.RecruitedAgents.Add(new AgentData(name,hp:1000,speed:2.5f));
        SaveDataInLocal.DataSave(data);SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING "+DateTime.UtcNow.ToString("O"));
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){rows.Clear();errors.Clear();Application.logMessageReceived+=Log;running=finished=false;deadline=EditorApplication.timeSinceStartup+600;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);SessionState.SetBool(Key+".escortOnly",false);
            SessionState.SetBool(Key+".automaticOnly",false);
            SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".override","");
            RestorePref(GameCacheBootstrap.InstallMarkerKey,".marker");RestorePref(CityNewsFeed.ExpandedPreferenceKey,".news");
            var scene=SessionState.GetString(Key+".scene","");if(!string.IsNullOrEmpty(scene)&&SceneManager.GetActiveScene().path!=scene)EditorSceneManager.OpenScene(scene);
        }
    }
    static void RestorePref(string pref,string key){int value=SessionState.GetInt(Key+key,-1);if(value<0)PlayerPrefs.DeleteKey(pref);else PlayerPrefs.SetInt(pref,value);}
    static void Log(string text,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&errors.Count<8)errors.Add(text);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish("Watchdog expired.");return;}
        if(running||!UnityEngine.Object.FindAnyObjectByType<LandscapeFrontEnd>())return;running=true;
        var host=new GameObject("Isolated mode/world review");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<ModeWorldTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator test)
    {while(true){bool more;object next=null;try{more=test.MoveNext();if(more)next=test.Current;}catch(Exception e){Finish(e.ToString());yield break;}if(!more){Finish(null);yield break;}yield return next;}}
    static void Check(bool value,string label){rows.Add((value?"PASS ":"FAIL ")+label);RecoveryReport.WriteLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);
        Application.logMessageReceived-=Log;Check(errors.Count==0,"No runtime errors during mode/world checks: "+string.Join(" | ",errors));
        rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add(AutomaticOnly?
            "Isolated Editor campaign. The active clock is accelerated for two automatic fixtures. The guide is positioned near the guard post for a real walking order. No device certification.":
            "Isolated editor campaign. Readiness and prerequisite chapter steps are seeded only in disposable data. Native Unity UI, road queues, police contact, supporter gate flow, Away fare, extraction and mode isolation. Crew are positioned on the baked NavMesh for focused interactions. No natural chapter completion or device certification.");
        rows.Add("UTC "+DateTime.UtcNow.ToString("O"));RecoveryReport.WriteLines(Report,rows);
        Time.timeScale=1;if(PoliceCityMode.Active)PoliceCityMode.ExitToMenu();EditorApplication.isPlaying=false;
    }
    static IEnumerator Until(Func<bool> done,float seconds=100){float end=Time.realtimeSinceStartup+seconds;while(!done()&&Time.realtimeSinceStartup<end)yield return null;}
    static Button Mode(string name)
    {var shell=UnityEngine.Object.FindAnyObjectByType<LandscapeFrontEnd>();return shell?shell.transform.Find("ModeSelect/"+name)?.GetComponent<Button>():null;}
    static GameObject Hit(Selectable button,out PointerEventData pointer)
    {
        pointer=null;
        if(!button||!button.gameObject.activeInHierarchy||!button.IsInteractable())return null;
        Canvas.ForceUpdateCanvases();
        var point=GuidedCityTutorial.ScreenRect((RectTransform)button.transform).center;
        pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer,hits);
        var top=hits.FirstOrDefault().gameObject;
        return top&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(top)==button.gameObject?top:null;
    }
    static bool Click(Selectable button)
    {
        var top=Hit(button,out var pointer);
        if(!top)return false;
        ExecuteEvents.ExecuteHierarchy(top,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(top,pointer,ExecuteEvents.pointerUpHandler);
        return ExecuteEvents.ExecuteHierarchy(top,pointer,ExecuteEvents.pointerClickHandler)==button.gameObject;
    }
    static Button Choice(string caption) => WorldButtonLayer.ChoiceFrame().GetComponentsInChildren<Button>()
        .FirstOrDefault(button=>button.GetComponentInChildren<TextMeshProUGUI>()?.text==caption);
    static void Warp(AgentController actor,Vector3 position)
    {
        var nav=actor.GetComponent<NavMeshAgent>();
        if(nav&&NavMesh.SamplePosition(position,out var hit,8,nav.areaMask)&&nav.Warp(hit.position))
            actor.TryCommandMoveTo(hit.position,2);
    }
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(2);SimpleShellLayout.ShowModes();yield return null;
        var data=GameManager.Data;int money=data.Money;var ids=data.RecruitedAgents.Select(a=>a.AgentId).ToArray();
        if(AutomaticOnly){yield return RunAutomaticMatchday();yield break;}
        if(EscortOnly)
        {
            LandscapeSceneBuilder.SetGameViewSize("1280x720");
            yield return Until(()=>Hit(Mode("HomeTerritory"),out _),6);
            Check(Click(Mode("HomeTerritory")),"Home opens through its native mode button");
            yield return Until(()=>BattleManager.instance&&BattleManager.instance.BattleActive&&StadiumMatchdayControl.Instance);
            var stadiumControl=StadiumMatchdayControl.Instance;
            if(!stadiumControl){Check(false,"Stadium became available");yield break;}
            StadiumMatchdayActivity.Instance.AutomaticMatchdaysEnabled=false;
            stadiumControl.FocusAndOpen();
            yield return Until(()=>Hit(TutorialAnchor.Find("world.matchday.start"),out _),8);
            Check(Click(TutorialAnchor.Find("world.matchday.start")),"Matchday starts through the stadium action");
            var activity=StadiumMatchdayActivity.Instance;
            yield return Until(()=>activity.HasStarted&&activity.SimulatedGroupCount>0,15);
            yield return TestEscort(activity,BattleManager.instance.PlayerAgents.First());
            GameManager.Save();
            Check(SaveDataInLocal.DataLoad().MatchdayTactics.escortComplete,"Native save persists the completed escort");
            yield break;
        }
        Check(Mode("HomeTerritory")&&Mode("AwayTrip")&&Mode("PlayPolice"),"Three explicit modes are Home, Away and Police");
        Check(Mode("HomeTerritory").IsInteractable()&&Mode("PlayPolice").IsInteractable()&&!Mode("AwayTrip").IsInteractable(),"Fresh Home/Police are available while Away is genuinely locked");
        Check(Mode("AwayTrip").GetComponentInChildren<TextMeshProUGUI>().text.Contains("LOCKED"),"Away has a visible lock and Home requirement");
        Click(Mode("AwayTrip"));yield return null;
        Check(SceneManager.GetActiveScene().name=="MainMenu"&&data.Money==money&&ids.SequenceEqual(data.RecruitedAgents.Select(a=>a.AgentId)),"Tapping locked Away cannot create a new firm or leave the menu");
        Check(!GameManager.instance.EnterAwayMode()&&SceneManager.GetActiveScene().name=="MainMenu"&&!CampaignMissions.HomeCleared(data),"Away launch guard also rejects a direct call before Home unlock");
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();
        foreach(string size in new[]{"1280x720","2400x1080","1024x768"})
        {
            LandscapeSceneBuilder.SetGameViewSize(size);yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();
            Check(new[]{Mode("HomeTerritory"),Mode("AwayTrip"),Mode("PlayPolice")}.All(b=>{var r=GuidedCityTutorial.ScreenRect((RectTransform)b.transform);return r.xMin>=0&&r.yMin>=0&&r.xMax<=Screen.width+1&&r.yMax<=Screen.height+1;}),"Mode choices fit "+size);
            var panel=Mode("HomeTerritory").transform.parent as RectTransform;var dim=panel.Find("Dim") as RectTransform;
            Check(Mathf.Abs(panel.rect.width-dim.rect.width)<1&&Mathf.Abs(panel.rect.height-dim.rect.height)<1&&Mathf.Abs(GuidedCityTutorial.ScreenRect((RectTransform)Mode("HomeTerritory").transform).center.x-Screen.width*.5f)<2,"Mode backdrop fills and choices centre in the viewport at "+size);
            ScreenCapture.CaptureScreenshot("Artifacts/Recovery/modes-locked-"+size+".png");yield return null;
        }
        LandscapeSceneBuilder.SetGameViewSize("1280x720");yield return new WaitForSecondsRealtime(.2f);
        Check(Click(Mode("HomeTerritory")),"Home mode receives its actual menu click");
        yield return Until(()=>BattleManager.instance&&BattleManager.instance.BattleActive&&StadiumMatchdayControl.Instance);
        Check(CityGameplay.HomeMode&&data.Money==money&&ids.SequenceEqual(GameManager.Data.RecruitedAgents.Select(a=>a.AgentId)),"Home entry resumes the saved firm without replacing cash or roster");
        if(!StadiumMatchdayControl.Instance){Check(false,"Stadium event button became ready");yield break;}
        foreach(var rivalDirector in UnityEngine.Object.FindObjectsByType<LivingRivalDirector>(FindObjectsSortMode.None))
            rivalDirector.AutomaticDecisionsEnabled=false;
        if(CityTrainSchedule.Instance)CityTrainSchedule.Instance.AutomaticArrivalsEnabled=false;
        var frame=UnityEngine.Object.FindAnyObjectByType<LandscapeBattleHUD>().frame;
        Check(!frame.Find("AwayTrip")&&!frame.Find("MatchdayAccess"),"Stadium control stays in the world UI");
        var control=StadiumMatchdayControl.Instance;
        var owner=StadiumMatchdayActivity.Instance;
        owner.AutomaticMatchdaysEnabled=false;
        var selection=AgentSelectionManager.instance;
        var actor=BattleManager.instance.PlayerAgents.First();
        var camera=Camera.main;
        var rig=CameraPanTouchOnly.Instance;
        var building=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .First(r=>RtsBuildingVisibility.IsBuildingName(r.name)&&r.bounds.size.y>6);
        var roadMesh=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .First(r=>r.name.StartsWith("road",StringComparison.OrdinalIgnoreCase)&&r.enabled);
        bool roadWasHidden=roadMesh.forceRenderingOff;
        bool buildingWasHidden=building.forceRenderingOff;
        bool colliderWasEnabled=building.GetComponent<Collider>()?.enabled??false;
        rig.enabled=false;
        camera.transform.position=building.bounds.center;
        yield return new WaitForEndOfFrame();
        Check(building.forceRenderingOff&&roadMesh.forceRenderingOff==roadWasHidden,"Camera inside a real building hides only its building mesh");
        camera.transform.position=roadMesh.bounds.center;
        yield return new WaitForEndOfFrame();
        Check(building.forceRenderingOff==buildingWasHidden&&roadMesh.forceRenderingOff==roadWasHidden&&
            (building.GetComponent<Collider>()?.enabled??false)==colliderWasEnabled,"Camera exit restores building rendering and preserves road rendering/collision");
        rig.enabled=true;
        var city=CityGameplay.Instance;
        var director=LivingRivalDirector.Instance;
        var site=CityBusinessSystem.Instance.Sites.OrderBy(shop=>Vector3.Distance(shop.transform.position,city.Home))
            .FirstOrDefault(shop=>director.TryStartShopRaid(shop));
        Check(site,"A real rival raid creates a stock rescue opportunity");
        if(site)
        {
            var venue=CityBusinessLedger.Find(data,site.Definition.id);
            venue.controlled=true;
            var origin=site.transform.position;
            var nav=actor.GetComponent<NavMeshAgent>();
            var path=new NavMeshPath();
            for(int index=0;index<24;index++)
            {
                float angle=index*Mathf.PI/12;
                var point=origin+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*17;
                if(!NavMesh.SamplePosition(point,out var hit,3,nav.areaMask)||
                    CityBusinessSystem.HorizontalDistance(origin,hit.position)<=12.3f||
                    !NavMesh.CalculatePath(hit.position,city.Home,nav.areaMask,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                nav.Warp(hit.position);
                break;
            }
            selection.DeselectAll();selection.Select(actor);
            site.OpenInteraction();yield return new WaitForSecondsRealtime(.25f);
            var rescue=WorldButtonLayer.MissionFrame().GetComponentsInChildren<Button>()
                .FirstOrDefault(button=>button.GetComponentInChildren<TextMeshProUGUI>()?.text.StartsWith("RESCUE STOCK")==true);
            int before=data.CitySupplies;
            Check(Click(rescue)&&data.ActiveStockRescue!=null&&data.CitySupplies==before,"Stock rescue uses the real venue action and grants no instant supplies");
            Time.timeScale=3;
            yield return Until(()=>data.ActiveStockRescue==null,65);
            Time.timeScale=1;
            Check(data.CitySupplies==before+2&&CityBusinessSystem.HorizontalDistance(actor.transform.position,city.Home)<10,
                "Supplies are earned only after the stock carrier actually reaches Home");
            Check(!director.TryRescueStock(site),"The same venue cannot pay stock rescue twice on one day");
        }
        rig.TutorialFocus(control.TapPoint);
        yield return new WaitForSecondsRealtime(.3f);
        var pin=TutorialAnchor.Find("world.matchday");
        var stadium=CityLandmarks.FindBuilding(new[]{"stadium_001"});
        var stadiumRenderers=stadium.GetComponentsInChildren<MeshRenderer>();
        var stadiumBounds=stadiumRenderers[0].bounds;
        foreach(var mesh in stadiumRenderers)stadiumBounds.Encapsulate(mesh.bounds);
        Check(Vector3.Distance(stadiumBounds.ClosestPoint(control.TapPoint),control.TapPoint)<23,
            "Matchday button is anchored beside the actual stadium apron");
        Check(pin&&pin.transform.parent==WorldButtonLayer.Frame(),"MATCHDAY uses the same native world UI as city pins");
        Check(Click(pin),"Actual Matchday button opens local actions");
        yield return new WaitForSecondsRealtime(.25f);
        Check(control.GetComponent<WorldChoiceBar>()?.IsOpen==true&&!MatchdayStrategySystem.Instance.IsOpen&&!owner.HasStarted,"Pin opens local choices without beginning the event");
        int prePlanCash=data.Money;
        data.CitySupplies=4;
        control.ShowPlans();
        yield return new WaitForSecondsRealtime(.25f);
        Check(Click(TutorialAnchor.Find("world.matchday.plan1")),"Native approach choice is available before kickoff");
        yield return new WaitForSecondsRealtime(.25f);
        Check(data.Money==prePlanCash,"Approach choice does not charge before kickoff");
        Check(Click(TutorialAnchor.Find("world.matchday.start")),"Actual START MATCHDAY action works with a viable crew");
        yield return Until(()=>owner.HasStarted&&data.MatchdayStreet?.arrivalWaves>0&&
            CityRoadIncidentSystem.Instance.ActiveIncidents.Count(road=>road.IsMatchdayCordon)>=3,24);
        var roads=CityRoadIncidentSystem.Instance.ActiveIncidents.Where(road=>road.IsMatchdayCordon).ToArray();
        Check(owner.HasStarted&&data.MatchdayStreet?.arrivalWaves>0&&owner.SimulatedGroupCount>0,"Live Matchday advances with a wave of real home and visiting supporters");
        Check(data.Money==prePlanCash-200&&data.CitySupplies==2,"Event commits the selected approach once");
        Check(roads.Select(road=>road.District).Distinct().Count()>=3,"Rail, pub and stadium road closures run concurrently");
        foreach(var road in roads)
        {
            var forward=road.Direction;
            Check(CityRoadIncidentSystem.TrafficSpeedMultiplier(road.Location-forward*8,forward)==0,
                road.District+" closure stops traffic");
        }
        rig.TutorialFocus(city.Locations[7]);
        yield return Until(()=>BattleManager.instance.EnemyAgents.Count(fan=>fan&&fan.IsAlive&&fan.gameObject.activeInHierarchy&&
            (fan.MatchdayRole==MatchdayUnitRole.HomeSupporter||fan.MatchdayRole==MatchdayUnitRole.RivalSupporter)&&
            CityBusinessSystem.HorizontalDistance(fan.transform.position,city.Locations[7])<75)>=12,14);
        var arriving=BattleManager.instance.EnemyAgents.Where(fan=>fan&&fan.IsAlive&&fan.gameObject.activeInHierarchy&&
            (fan.MatchdayRole==MatchdayUnitRole.HomeSupporter||fan.MatchdayRole==MatchdayUnitRole.RivalSupporter)&&
            CityBusinessSystem.HorizontalDistance(fan.transform.position,city.Locations[7])<75).ToArray();
        Check(arriving.Length>=12,"Home and away supporters are rendered near the rail arrival area");
        var startingPositions=arriving.Select(fan=>fan.transform.position).ToArray();
        yield return new WaitForSeconds(2);
        Check(arriving.Where((fan,index)=>fan&&fan.gameObject.activeInHierarchy&&
            Vector3.Distance(fan.transform.position,startingPositions[index])>1).Any(),
            "Supporters physically walk out of the rail arrival area");
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/matchday-street-rail-arrivals.png");yield return null;
        foreach(var districtRoad in roads)
        {
            rig.TutorialFocus(districtRoad.Location);
            var response=districtRoad.PoliceResponse;
            yield return Until(()=>response&&response.SirenPlayed,12);
            var audio=response&&response.Vehicle?response.Vehicle.GetComponent<AudioSource>():null;
            Check(response&&response.SirenPlayed&&audio&&audio.spatialBlend==1&&audio.clip,
                districtRoad.District+" sends a marked van with a playing spatial siren");
            yield return Until(()=>response&&response.OnSite&&response.Officers.Count==2,18);
            var props=CityRoadIncidentSystem.Instance.transform.Find("ROAD INCIDENT "+districtRoad.Id);
            Check(response&&response.OnSite&&response.Officers.Count==2&&props&&
                props.GetComponentsInChildren<MeshFilter>().Length>=13,
                districtRoad.District+" has officers, a van, and a full cordon of 3D cones and barriers");
            rows.Add($"Cordon {districtRoad.District}: site {districtRoad.Location}, van {response?.Vehicle?.position}, officers {response?.Officers.Count}.");
        }
        var strategy=MatchdayStrategySystem.Instance;
        rows.Add($"STADIUM: pin {control.TapPoint}; model {control.TutorialBounds.center}; gate {strategy.ResponsePoints[0]}; rail {CityGameplay.Instance.Locations[7]}; escort metres {strategy.EscortRouteMetres:0}");
        if(roads.Length>0)
        {
            var road=roads[0];
            rig.TutorialFocus(road.Location);
            yield return Until(()=>road.QueuedPeople>=3,20);
            var waiting=BattleManager.instance.EnemyAgents.Where(fan=>fan&&CityRoadIncidentSystem.IsQueued(fan)&&
                CityBusinessSystem.HorizontalDistance(fan.transform.position,road.WalkingBypassStart)<12)
                .Select(fan=>(fan,life:fan.LifeRevision,position:fan.transform.position)).ToArray();
            Check(road.QueuedPeople>=3&&waiting.Length>=3,"A visible supporter queue waits behind the district roadblock");
            ScreenCapture.CaptureScreenshot("Artifacts/Recovery/matchday-street-cordon.png");yield return null;
            Warp(actor,road.OfficerWorkPoint);
            selection.DeselectAll();selection.Select(actor);
            owner.StreetOperations.FocusEvent(1);
            yield return new WaitForSecondsRealtime(.25f);
            Check(Click(Choice(road.District.ToUpperInvariant())),"Road list opens the selected district closure");
            yield return Until(()=>Hit(Choice("OPEN ROAD"),out _),6);
            bool clicked=Click(Choice("OPEN ROAD"));
            bool requested=data.CityRoadIncidents.Any(record=>record.id==road.Id&&record.clearanceRequested);
            Check(clicked&&requested,"Crew on site can ask police to clear a real closure");
            if(requested)yield return Until(()=>road.IsCleared&&data.MatchdayStreet.routeReopened,110);
            Check(road.IsCleared&&data.MatchdayStreet.routeReopened&&
                CityRoadIncidentSystem.TrafficSpeedMultiplier(road.Location-road.Direction*8,road.Direction)>0,
                "Police physically reopen the requested road and release traffic before objective credit");
            yield return Until(()=>waiting.Count(entry=>entry.fan&&entry.fan.LifeRevision==entry.life&&
                !CityRoadIncidentSystem.IsQueued(entry.fan)&&Vector3.Distance(entry.position,entry.fan.transform.position)>4)>=2,16);
            Check(waiting.Count(entry=>entry.fan&&entry.fan.LifeRevision==entry.life&&
                !CityRoadIncidentSystem.IsQueued(entry.fan)&&Vector3.Distance(entry.position,entry.fan.transform.position)>4)>=2,
                "The same waiting supporters resume walking once police remove the roadblock");
        }
        var gates=owner.StreetOperations.Gates;
        Check(gates&&gates.Ready&&Vector3.Distance(gates.MainGate,gates.SideGate)>17,
            "Stadium has two distinct gates on reachable pedestrian approaches");
        if(gates&&gates.Ready)
        {
            Warp(actor,gates.QueuePoint);
            selection.DeselectAll();selection.Select(actor);
            owner.StreetOperations.FocusEvent(4);
            yield return Until(()=>Hit(Choice("SIDE · £80"),out _),6);
            int gateCash=data.Money;
            Check(Click(Choice("SIDE · £80"))&&data.MatchdayStreet.gateChoice==2&&
                !data.MatchdayStreet.gateAdmitted&&data.Money==gateCash-MatchdayGates.SideGateCost,
                "Real side gate action opens the lane and charges once before any admission credit");
            yield return Until(()=>data.MatchdayStreet.gateAdmitted,45);
            rows.Add($"Gates: queue {gates.QueuePoint}, main {gates.MainGate}, side {gates.SideGate}.");
            rows.Add("Gate supporters: "+string.Join(" | ",BattleManager.instance.EnemyAgents
                .Where(fan=>fan&&fan.MatchdayGroupName=="STADIUM GATE QUEUE")
                .Select(fan=>$"{fan.transform.position} {fan.LastEscortFailure}")));
            Check(data.MatchdayStreet.gateAdmitted&&owner.GateQueueAt(gates.SideGate)>=MatchdayGates.AdmissionTarget,
                "Stadium objective counts real fans after they walk through the selected gate");
            ScreenCapture.CaptureScreenshot("Artifacts/Recovery/matchday-stadium-side-gate.png");yield return null;
        }
        Warp(actor,CityGameplay.Instance.Locations[1]);
        selection.DeselectAll();selection.Select(actor);
        var yard=data.HomeBuildings.First(building=>building.site==1);
        yard.level=1;yard.complete=true;
        int stock=data.CitySupplies;
        owner.StreetOperations.FocusEvent(2);
        yield return new WaitForSecondsRealtime(.25f);
        Check(Click(Choice("USE 1 SUPPLY"))&&data.MatchdayStreet.pubStocked&&data.CitySupplies==stock-1,
            "A completed Home Supply Yard reduces the live Matchday pub restock to one supply");
        yield return Until(()=>data.MatchdayStreet.pubTrading,75);
        Check(data.MatchdayStreet.pubTrading,"Actual unobstructed pub trade completes the resupply objective");
        yield return TestEscort(owner,actor);
        rig.TutorialFocus(actor.transform.position);
        yield return Until(()=>owner.StreetPatrols().Any(officer=>officer&&!officer.IsEscorting),20);
        var patrol=owner.StreetPatrols().FirstOrDefault(officer=>officer&&!officer.IsEscorting);
        if(patrol)
        {
            Warp(actor,patrol.transform.position+Vector3.right);
            var rival=BattleManager.instance.EnemyAgents.FirstOrDefault(e=>e&&e.IsAlive&&e.gameObject.activeInHierarchy&&e.firmName!="POLICE"&&!e.IsHomeMatchdaySupporter);
            if(rival)
            {
                var nav=rival.GetComponent<NavMeshAgent>();
                var nearby=actor.transform.position+Vector3.right*1.6f;
                if(nav&&NavMesh.SamplePosition(nearby,out var hit,3,nav.areaMask)&&nav.Warp(hit.position))
                {
                    rival.EndEscort();
                    rival.HoldFire(false);
                    rival.SetCinematicIdle(false);
                    rival.isHostile=true;
                    rival.AlertToTarget(actor);
                    actor.CommandAttackTarget(rival);
                }
            }
            yield return Until(()=>data.MatchdayStreet.crewBooked>0,15);
            Check(data.MatchdayStreet.crewBooked>0&&actor.IsAlive&&actor.IsActivityLocked,"Police make contact and detain a fighting crew member without killing them");
        }
        else Check(false,"A streamed police patrol is available for custody verification");
        foreach(var role in new[]{MatchdayUnitRole.HomeSupporter,MatchdayUnitRole.RivalSupporter})
        {
            var officer=owner.StreetPatrols().FirstOrDefault(value=>value&&!value.IsEscorting);
            var fan=BattleManager.instance.EnemyAgents.FirstOrDefault(value=>value&&value.IsAlive&&value.gameObject.activeInHierarchy&&value.MatchdayRole==role);
            if(!officer||!fan){Check(false,"Streamed patrol and supporter available for "+role);continue;}
            var nav=fan.GetComponent<NavMeshAgent>();
            if(NavMesh.SamplePosition(officer.transform.position+Vector3.right*1.5f,out var meet,3,nav.areaMask))nav.Warp(meet.position);
            int bookings=role==MatchdayUnitRole.HomeSupporter?data.MatchdayStreet.homeBooked:data.MatchdayStreet.rivalBooked;
            fan.isHostile=true;
            yield return Until(()=>(role==MatchdayUnitRole.HomeSupporter?data.MatchdayStreet.homeBooked:data.MatchdayStreet.rivalBooked)>bookings,15);
            Check((role==MatchdayUnitRole.HomeSupporter?data.MatchdayStreet.homeBooked:data.MatchdayStreet.rivalBooked)>bookings,
                "Police book fighting "+role+" after local contact");
        }
        Time.timeScale=6;
        yield return Until(()=>owner.IsAftermath,95);
        Time.timeScale=1;
        Check(owner.IsAftermath&&data.MatchdayIncidentResolved&&CampaignMissions.ActionCount(data,1,"matchday")==1,
            "Aftermath grants one campaign result for two or more completed street objectives");
        int credit=data.MatchdayTravelCredit;
        Check(credit==420,"Road, pub and gate outcomes fund a £420 credit for the next Away trip");
        WorldChoiceBar.DismissCurrent();
        GameManager.Save();
        var loaded=SaveDataInLocal.DataLoad();
        Check(loaded.MatchdayStreet?.arrivalWaves>0&&loaded.MatchdayStreet.gateChoice==2&&loaded.MatchdayStreet.gateAdmitted&&loaded.MatchdayTactics.planCommitted&&
            loaded.CityRoadIncidents.Count(road=>road.matchdayCordon)>=3,"Native save retains live crowd, cordon and objective state");
        rig.TutorialFrame(control.TutorialBounds);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/matchday-street-stadium.png");yield return null;
        SimpleShellLayout.ReturnToModes();yield return Until(()=>SceneManager.GetActiveScene().name=="MainMenu"&&Mode("AwayTrip"));yield return new WaitForSecondsRealtime(.4f);
        Check(!Mode("AwayTrip").IsInteractable(),"Starting Matchday alone does not unlock Away");
        data=GameManager.Data;data.CompletedCampaignMissions.Add(1);data.CurrentLevel=2;
        data.SelectedAwayAgentIds=data.RecruitedAgents.Where(member=>member.IsAlive).Select(member=>member.AgentId).ToList();
        data.DeploymentSelectionCustomized=true;
        GameManager.Save();SimpleShellLayout.RefreshModes();
        Check(Mode("AwayTrip").IsInteractable()&&CampaignMissions.HomeCleared(SaveDataInLocal.DataLoad()),"Earned Home completion unlocks Away and survives native serialization");
        Check(CampaignMissions.HomeCleared(new PlayerData{CurrentLevel=2}),"Legacy campaigns keep their previously unlocked Away access");
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/modes-unlocked.png");yield return null;
        int savedMoney=data.Money;Check(Click(Mode("AwayTrip")),"Unlocked Away mode receives its real menu click");
        yield return Until(()=>SceneManager.GetActiveScene().name=="DashboardScene"&&UnityEngine.Object.FindAnyObjectByType<LandscapeFrontEnd>());
        Check(GameManager.Data.LastSessionMode=="AwayPlan"&&GameManager.Data.Money==savedMoney&&GameManager.Data.CurrentLevel==2,"Away enters destination/traveller preparation without creating a new campaign");
        var shell=UnityEngine.Object.FindAnyObjectByType<LandscapeFrontEnd>();Check(shell.currentPage=="trips"&&shell.screenTitle.text=="AWAY MODE","Away has its own mode preparation screen");
        var planner=UnityEngine.Object.FindFirstObjectByType<PlanAwayTripController>(FindObjectsInactive.Include);
        if(planner&&planner.startTripBtn)
        {
            data=GameManager.Data;
            yield return new WaitForSecondsRealtime(.4f);
            var riverside=planner.destinations.First(destination=>destination.name=="Riverside");
            var fare=CampaignMissions.AwayFare(data,riverside.travelCost);
            var launch=planner.startTripBtn;
            Check(fare==riverside.travelCost-credit&&planner.detailTravelCostText.text.Contains(fare.ToString("N0")),
                "Away preparation shows the reduced fare earned by Matchday");
            Check(Click(launch),"Native planner launches the discounted Riverside trip");
            yield return Until(()=>SceneManager.GetActiveScene().name=="Gameplay"&&!CityGameplay.HomeMode&&
                BattleManager.instance&&BattleManager.instance.BattleActive&&AwayExtractionSystem.Instance,75);
            data=GameManager.Data;
            Check(!CityGameplay.HomeMode&&data.MatchdayTravelCredit==0&&data.Money==savedMoney-fare,
                "Actual Away departure consumes the earned credit exactly once");
            if(AwayExtractionSystem.Instance)
            {
                foreach(var step in CampaignMissions.Steps(data,2).Where(step=>step.operation.HasValue))
                    CampaignMissions.CompleteOperation(data,step.operation.Value);
                CampaignMissions.RecordRival(data,"Fixture rival A");CampaignMissions.RecordRival(data,"Fixture rival B");
                CampaignMissions.RecordTerritory(data,"Fixture zone");
                var arrival=CityGameplay.Instance.Home;
                foreach(var traveller in BattleManager.instance.PlayerAgents.Where(member=>member&&member.IsAlive))Warp(traveller,arrival);
                int beforeReturn=data.CitySupplies;
                AwayExtractionSystem.Instance.RequestExtraction();
                yield return Until(()=>data.LastAwayStockMission==2,28);
                Check(data.LastAwayStockMission==2&&data.CitySupplies==beforeReturn+CampaignMissions.AwayStockDelivery&&
                    !CampaignMissions.TryComplete(data,out _),
                    "Physical Riverside extraction sends one supply delivery to Home and unlocks the next destination");
                GameManager.instance.ReturnToDashboard();
                yield return Until(()=>CityGameplay.HomeMode&&ModernCityHud.Instance&&BattleManager.instance&&BattleManager.instance.BattleActive&&
                    ModernCityHud.Instance.CurrentInstruction.Contains("RIVERSIDE SUPPLIES"),75);
                Check(GameManager.Data.CitySupplies>=beforeReturn+CampaignMissions.AwayStockDelivery&&GameManager.Data.CurrentLevel==3&&
                    ModernCityHud.Instance&&ModernCityHud.Instance.CurrentInstruction.Contains("RIVERSIDE SUPPLIES"),
                    "Returned Home shows the delivered Away supplies and the next operation");
                savedMoney=GameManager.Data.Money;
            }
        }
        else Check(false,"Native Away planner is available for linked campaign check");
        int levelBeforePolice=GameManager.Data.CurrentLevel;
        SimpleShellLayout.ReturnToModes();yield return Until(()=>SceneManager.GetActiveScene().name=="MainMenu"&&Mode("PlayPolice"));yield return new WaitForSecondsRealtime(.4f);
        Check(Click(Mode("PlayPolice")),"Police mode receives its actual menu click");yield return Until(()=>PoliceCityMode.Active&&PoliceCityMode.Instance&&PoliceCityMode.Instance.Ready);
        Check(PoliceCityMode.Active&&PoliceCityMode.Instance.Ready&&(!StadiumMatchdayControl.Instance||!StadiumMatchdayControl.Instance.Visible),"Police opens its own career and suppresses the firm Matchday sign");
        PoliceCityMode.ExitToMenu();yield return Until(()=>!PoliceCityMode.Active&&SceneManager.GetActiveScene().name=="MainMenu");yield return new WaitForSecondsRealtime(.3f);
        Check(GameManager.Data.Money==savedMoney&&GameManager.Data.CurrentLevel==levelBeforePolice&&CampaignMissions.HomeCleared(GameManager.Data),"Returning from Police preserves firm cash, Home progress and Away unlock");
    }

    static IEnumerator TestEscort(StadiumMatchdayActivity owner,AgentController guide)
    {
        var data=GameManager.Data;
        var strategy=MatchdayStrategySystem.Instance;
        var selection=AgentSelectionManager.instance;
        Warp(guide,strategy.EscortPickup);
        selection.DeselectAll();selection.Select(guide);
        owner.StreetOperations.FocusEvent(0);
        yield return Until(()=>Hit(Choice("RALLY FANS"),out _),8);
        Check(Click(Choice("RALLY FANS"))&&data.MatchdayTactics.escortPositioned,"Native rally action commits the supporter group");
        Check(guide.TryCommandMoveTo(strategy.ResponsePoints[0]),"A real walking path leads the escort from pickup to its gate");
        yield return Until(()=>data.MatchdayTactics.escortComplete,90);
        Check(data.MatchdayTactics.escortComplete,"Three or more real followers reach the gate before escort credit");
        rows.Add($"Escort gate {strategy.ResponsePoints[0]}; guide {guide.transform.position}; nearby clash {CityBusinessSystem.HostilesNear(strategy.ResponsePoints[0],12)}.");
        rows.Add("Escort followers: "+string.Join(" | ",BattleManager.instance.EnemyAgents
            .Where(fan=>fan&&fan.MatchdayGroupName=="PLAYER SUPPORTER ESCORT")
            .Select(fan=>$"{fan.transform.position} {fan.LastEscortFailure}")));
    }

    static IEnumerator RunAutomaticMatchday()
    {
        LandscapeSceneBuilder.SetGameViewSize("1280x720");
        yield return Until(()=>Hit(Mode("HomeTerritory"),out _),8);
        Check(Click(Mode("HomeTerritory")),"Home opens through the native mode button");
        yield return Until(()=>BattleManager.instance&&BattleManager.instance.BattleActive&&StadiumMatchdayControl.Instance&&ModernCityHud.Instance,75);
        var owner=StadiumMatchdayActivity.Instance;
        if(!owner||!ModernCityHud.Instance){Check(false,"Home and stadium loaded");yield break;}
        var control=StadiumMatchdayControl.Instance;
        var hud=ModernCityHud.Instance;
        var frame=UnityEngine.Object.FindAnyObjectByType<LandscapeBattleHUD>().frame;
        var rig=CameraPanTouchOnly.Instance;
        var data=GameManager.Data;
        var city=CityGameplay.Instance;
        var battle=BattleManager.instance;
        var crew=battle.PlayerAgents.Where(actor=>actor&&actor.IsAlive).ToArray();
        int day=data.MatchDay,wins=data.BattleWins;
        control.Focus();
        yield return Until(()=>Hit(TutorialAnchor.Find("world.matchday"),out _),8);
        Check(Click(TutorialAnchor.Find("world.matchday"))&&!owner.HasStarted&&!Choice("START MATCHDAY"),
            "Stadium opens as a camera location without a start or setup button");
        rig.TutorialFocus(city.Home);
        Time.timeScale=0;
        float waitBeforePause=data.HomeMatchdayWait;
        yield return new WaitForSecondsRealtime(.65f);
        Check(data.HomeMatchdayWait==waitBeforePause,"Paused Home does not advance the Matchday clock");
        Time.timeScale=5;
        yield return Until(()=>data.HomeMatchdayWait>=StadiumMatchdayActivity.FirstMatchdayDelay-4,60);
        var cameraAtHome=Camera.main.transform.position;
        var hudText=hud.CurrentInstruction;
        int money=data.Money,stock=data.CitySupplies;
        yield return Until(()=>owner.HasStarted&&data.MatchdayStreet?.arrivalWaves>0,60);
        Check(owner.HasStarted&&owner.StreetOperations.Automatic&&data.MatchdayStreet.arrivalWaves>0,
            "First Matchday and rail arrivals start from active Home time");
        Check(Vector3.Distance(cameraAtHome,Camera.main.transform.position)<2,"Automatic start leaves the camera at Home");
        Check(hud.CurrentInstruction==hudText&&data.Money>=money&&data.CitySupplies>=stock&&!GamePopup.AnyOpen,
            "Automatic start preserves the Home goal with no setup charge or modal");
        var strip=frame.Find("LiveMatchday");
        yield return Until(()=>strip&&strip.gameObject.activeInHierarchy,5);
        Check(strip&&strip.gameObject.activeInHierarchy&&strip.Find("View"),"Live Matchday uses one compact View action");
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/automatic-matchday-home.png");
        yield return Until(()=>owner.ElapsedSeconds>=125,65);
        Check(data.MatchdayStreet.gateChoice>0,"Stadium opens its gate automatically");
        Check(data.CityRoadIncidents.Any(road=>road.day==day&&road.matchdayCordon&&road.cleared&&!road.clearanceRequested),
            "Police automatically clear an unattended roadblock");
        yield return Until(()=>data.MatchdayStreet.guardResolved,50);
        owner.CaptureState();
        Check(!data.MatchdayStreet.guarded&&data.MatchdayGroups.Any(group=>group.task=="REINFORCEMENTS"),
            "Ignoring the optional guard dispatches a named rival group into the city");
        yield return Until(()=>data.MatchDay>day,75);
        Check(data.MatchDay==day+1&&CityGameplay.Instance==city&&BattleManager.instance==battle&&
            data.MatchdayTravelCredit>=100&&CampaignMissions.ActionCount(data,1,"matchday")==1&&!GamePopup.AnyOpen,
            "Unattended Matchday completes once and advances the calendar in the same Home scene");
        Check(data.BattleWins==wins&&data.PoliceHeat<10,
            "Unattended crowd fights do not send idle crew on a citywide chase or max the player's heat");

        yield return Until(()=>owner.StreetOperations.GuardAvailable,80);
        Check(data.MatchDay==day+1&&owner.StreetOperations.GuardAvailable,"A second Matchday runs automatically");
        if(!owner.StreetOperations.GuardAvailable)yield break;
        Time.timeScale=1;
        var selection=AgentSelectionManager.instance;
        var guard=owner.StreetOperations.GuardPoint;
        var homePath=new NavMeshPath();
        var crewNav=crew[0].GetComponent<NavMeshAgent>();
        if(NavMesh.CalculatePath(crew[0].transform.position,guard,crewNav.areaMask,homePath)&&homePath.status==NavMeshPathStatus.PathComplete)
        {
            var corners=homePath.corners;
            float distance=0;
            for(int index=1;index<corners.Length;index++)distance+=Vector3.Distance(corners[index-1],corners[index]);
            rows.Add($"Crew to guard post is {distance:0}m on the walking mesh.");
        }
        Warp(crew[0],guard+Vector3.right*9);
        Warp(crew[1],city.Home);
        crew[1].CommandArea(city.Home,false);
        int homeOrder=crew[1].OrderRevision;
        selection.DeselectAll();selection.Select(crew[0]);
        var quick=frame.Find("QuickOrders/Guard")?.GetComponent<Button>();
        yield return Until(()=>Hit(quick,out _),6);
        Check(Click(quick)&&RtsGestureController.Instance.Mode==RtsGestureController.InputMode.GuardArea,
            "Selected crew can arm the real RTS Guard command directly from the HUD");
        RtsGestureController.Instance.Cancel();
        var view=strip.Find("View").GetComponent<Button>();
        Check(Click(view),"The Matchday View action opens the current guard opportunity");
        yield return Until(()=>Hit(Choice("GUARD HERE"),out _),8);
        Check(Click(Choice("GUARD HERE"))&&crew[0].CurrentOrder==AgentController.StandingOrder.Guard&&
            crew[1].OrderRevision==homeOrder,"Guard Here orders the chosen crew while the Home guard keeps working");
        yield return Until(()=>data.MatchdayStreet.guarded,35);
        Check(data.MatchdayStreet.guarded&&CityBusinessSystem.HorizontalDistance(crew[0].transform.position,guard)<12,
            "Actual guard presence protects home fans before resolving the optional event");
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/automatic-matchday-guard.png");
        GameManager.Save();
        var saved=SaveDataInLocal.DataLoad();
        Check(saved.MatchdayStreet.automatic&&saved.MatchdayStreet.guarded&&saved.MatchdayStartedDay==day+1,
            "Save retains the active fixture and resolved guard outcome");
    }
}
public sealed class ModeWorldTestRunner:MonoBehaviour{}
#endif
