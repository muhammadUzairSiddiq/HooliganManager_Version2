using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>Saved first-run teaching. The spotlight forwards only its exact approved target to real game commands.</summary>
public sealed class GuidedCityTutorial : MonoBehaviour
{
    public const int Version=2;
    public static GuidedCityTutorial Instance {get;private set;}
    public static bool BlocksInput=>Instance&&Instance.active;
    public static bool CombatSuppressed=>BlocksInput||Instance&&Instance.Eligible();
#if UNITY_EDITOR
    // Legacy isolated fixtures test gameplay independently. The dedicated tutorial
    // fixture opts in and drives the real spotlight; this never changes a player save.
    public static bool EditorExerciseTutorial;
#endif
    sealed class Lesson
    {
        public int part;public string title,text,id;
        public Action enter,action;public Func<bool> complete,underway;
        public Func<Vector3> point;public Func<Rect> screen;public bool wait,autoComplete;
        public TutorialGesture gesture;
    }
    readonly List<Lesson> lessons=new List<Lesson>();
    readonly HashSet<string> revealed=new HashSet<string>();
    TutorialSpotlight view;
    Lesson current;
    bool active,police,waiting,pausedByUs,matchdayStartGuide;
    float resumeScale=1,waitStarted;
    int index=-1,rosterBefore;
    int reviewReturn=-1;
    bool reviewWasResult;
    AgentController first,second;
    Vector3 street,patrolA,patrolB;
    AgentController[] moving;
    int[] revisions;
    readonly Dictionary<AgentController,int> beforeOrders=new Dictionary<AgentController,int>();
    Button target;
    bool actionAccepted;
    bool worldTargetValid=true;
    bool gestureDone,reviewing;
    bool showingResult,patrolVisitedA;
    float resultAt;
    string resultText;
    public bool ShowingResult=>showingResult;
    public int LessonCount=>lessons.Count;
    public Button ActualTarget=>target;
    Vector3 cameraStart;
    float zoomStart;
    public bool IsWaiting=>waiting;
    public int Step=>index;
    public int Part=>current?.part??0;
    public string CurrentTitle=>current?.title??"";
    public TutorialGesture CurrentGesture=>current?.gesture??TutorialGesture.Tap;
    public static bool AllowsFeature(string feature)=>!BlocksInput||Instance.revealed.Contains(feature);
    public static bool AllowsWorldChoice=>BlocksInput&&(Instance.current?.id=="rts.patrolConfirm"||Instance.current?.id=="rts.choiceCancel"||Instance.current?.id=="world.matchday"||Instance.current?.id?.StartsWith("world.matchday.")==true);
    public Rect AllowedScreenRect=>view?view.TargetScreenRect:default;
    public static void Ensure()
    {if(!Instance)new GameObject("Guided city tutorial").AddComponent<GuidedCityTutorial>();}
    void Awake(){if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;}
    public static void Replay(bool policeMode)
    {
        Ensure();Instance.End(false);var d=GameManager.Data;
        if(policeMode&&PoliceCityMode.Career!=null)
        {PoliceCityMode.Career.PoliceTutorialRequired=true;PoliceCityMode.Career.PoliceTutorialVersion=0;PoliceCityMode.Career.PoliceTutorialStep=0;PoliceCityMode.Instance.SaveCareerProgress();}
        else if(d!=null){d.MobileTutorialRequired=true;d.MobileTutorialVersion=0;d.MobileTutorialStep=0;d.FirstSessionCoachHidden=false;GameManager.Save();}
    }
    bool Eligible()
    {
#if UNITY_EDITOR
        if(!EditorExerciseTutorial&&!string.IsNullOrEmpty(SaveDataInLocal.EditorSaveOverride))return false;
#endif
        if(!BattleManager.instance||!BattleManager.instance.BattleActive||!AgentSelectionManager.instance||!CityGameplay.Instance)return false;
        if(PoliceCityMode.Active)return PoliceCityMode.Instance&&PoliceCityMode.Instance.Ready&&PoliceCityMode.Career?.PoliceTutorialRequired==true;
        var d=GameManager.Data;
        bool introduction=d?.MobileTutorialRequired==true;
        bool startGuide=StadiumMatchdayActivity.Instance&&!StadiumMatchdayActivity.Instance.AutomaticMatchdaysEnabled&&
            d?.MatchdayTutorialPending==true&&d.MobileTutorialVersion>=1&&d.TransportPrepared&&d.StadiumAccessPrepared
            &&MatchdayStrategySystem.Instance&&MatchdayStrategySystem.CanStart(d,out _);
        return CityGameplay.HomeMode&&ModernCityHud.Instance&&(introduction||startGuide);
    }
    void Update()
    {
        if(!active){if(Eligible()&&Time.timeScale>0&&!GamePopup.AnyOpen&&!GameFeedback.SettingsInputBlocked)Begin();return;}
        if(!Eligible()){End(false);return;}
        if(!first||!first.IsAlive||second&&!second.IsAlive){End(false);return;}
        if(showingResult)
        {
            view.Present(ProgressLabel(),current.title,resultText,default,true,false,canContinue:Time.unscaledTime-resultAt>.65f);return;
        }
        if(waiting)
        {
            if(current.complete?.Invoke()==true){ShowResult();return;}
            // A failed route/job never grants completion. Skip remains available.
            bool building=HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.IsBuilding;
            view.Present(ProgressLabel(),current.title,Time.unscaledTime-waitStarted>100?"Need more time? Skip this part.":building?"Building…":current.id=="rts.patrolConfirm"?"Watch your member reach both points.":"Watch your crew move.",default,true,false,canContinue:false);return;
        }
        if(!reviewing&&current.autoComplete&&current.complete?.Invoke()==true){ShowResult();return;}
        if(target&&(!target.gameObject.activeInHierarchy||!target.IsInteractable()))BindTarget(null);
        if(!target&&!string.IsNullOrEmpty(current.id)){var found=TutorialAnchor.Find(current.id);if(!found){PrepareTarget();found=TutorialAnchor.Find(current.id);}if(found){Reveal(found);BindTarget(found);}}
        Rect rect=default;bool hasTarget=false;
        if(current.screen!=null){rect=current.screen();hasTarget=rect.width>1;}
        else if(current.point!=null&&worldTargetValid)
        {
            var p=Camera.main?Camera.main.WorldToScreenPoint(current.point()+Vector3.up*.2f):Vector3.zero;
            if(p.z>0){float size=Mathf.Max(88,Screen.height*.13f);rect=new Rect(p.x-size*.5f,p.y-size*.5f,size,size);hasTarget=true;}
        }
        else if(target){rect=ScreenRect((RectTransform)target.transform);hasTarget=true;}
        string copy=current.text;
        if((!string.IsNullOrEmpty(current.id)||current.point!=null||current.screen!=null)&&!hasTarget)copy="Unavailable here. Tap SKIP PART.";
        bool ui=target&&current.point==null&&current.screen==null;
        view.Present(ProgressLabel(),current.title,copy,rect,false,hasTarget,rectangular:ui,passThrough:ui,canContinue:!hasTarget&&current.id==null&&current.point==null&&current.screen==null);
    }
    string ProgressLabel()=>$"{index+1:00} / {lessons.Count:00}";
    void Begin()
    {
        police=PoliceCityMode.Active;active=true;waiting=false;
        var crew=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();
        first=crew.FirstOrDefault();second=crew.Skip(1).FirstOrDefault();
        if(!first){End(false);return;}
        matchdayStartGuide=!police&&StadiumMatchdayActivity.Instance&&!StadiumMatchdayActivity.Instance.AutomaticMatchdaysEnabled&&
            GameManager.Data.MobileTutorialVersion>=1&&GameManager.Data.MatchdayTutorialPending;
        lessons.Clear();if(police)BuildPolice();else if(matchdayStartGuide)BuildMatchdayStart();else BuildFirm();
        revealed.Clear();view=TutorialSpotlight.Create();view.Accepted=AcceptTarget;view.Skipped=SkipPart;
        view.Previous=PreviousLesson;view.Next=NextLesson;view.Panned=PanLesson;view.Zoomed=ZoomLesson;
        int saved=police?PoliceCityMode.Career.PoliceTutorialStep:matchdayStartGuide?0:GameManager.Data.MobileTutorialStep;
        if((police?PoliceCityMode.Career.PoliceTutorialVersion:GameManager.Data.MobileTutorialVersion)<Version)saved=0;
        saved=Mathf.Clamp(saved,0,lessons.Count-1);
        // UI panels and targeting gestures are ephemeral. Continue restarts the
        // saved part at its entrance, never inside a missing sheet/PatrolReady.
        while(saved>0&&lessons[saved-1].part==lessons[saved].part)saved--;
        // UI reconstruction never selects an actor or awards learned skills.
        Enter(saved);
    }
    void Pause()
    {if(pausedByUs)return;resumeScale=Time.timeScale>0?Time.timeScale:1;Time.timeScale=0;pausedByUs=true;}
    void Resume()
    {if(!pausedByUs)return;if(Time.timeScale==0)Time.timeScale=resumeScale;pausedByUs=false;}
    void Enter(int step,bool review=false)
    {
        if(step>=lessons.Count){End(true);return;}
        BindTarget(null);index=step;current=lessons[index];waiting=false;showingResult=false;actionAccepted=false;worldTargetValid=true;gestureDone=false;reviewing=review;Pause();
        beforeOrders.Clear();foreach(var a in BattleManager.instance.PlayerAgents)if(a)beforeOrders[a]=a.OrderRevision;
        for(int i=0;i<=step;i++)RevealFeature(lessons[i].id);
        ModernCityHud.Instance?.ApplyTutorialVisibility();
        current.enter?.Invoke();PrepareTarget();view.SetLesson(step,current.gesture);
        cameraStart=Camera.main.transform.position;zoomStart=CameraPanTouchOnly.Instance?CameraPanTouchOnly.Instance.TutorialZoomValue:0;SaveStep();
        if(current.underway?.Invoke()==true){waiting=true;waitStarted=Time.unscaledTime;Resume();}
    }
    public void AcceptTarget()
    {
        if(!active||waiting||showingResult||actionAccepted)return;
        if(current.point!=null&&!worldTargetValid)return;
        if(current.screen==null&&current.point==null&&!string.IsNullOrEmpty(current.id)&&(!target||!target.IsInteractable()))return;
        if(current.gesture!=TutorialGesture.Tap&&current.gesture!=TutorialGesture.DoubleTap&&!gestureDone)return;
        actionAccepted=true;
        AgentSelectionManager.ConsumeUiPointer();
        if(current.action!=null)current.action();else if(target){target.onClick.Invoke();return;}
        EvaluateAction();
    }
    void BindTarget(Button button)
    {
        if(target)target.onClick.RemoveListener(ActualButtonClicked);
        target=button;if(target&&current?.point==null&&current?.screen==null)target.onClick.AddListener(ActualButtonClicked);
    }
    void ActualButtonClicked()
    {
        if(!active||waiting||showingResult)return;
        actionAccepted=true;
        if(current.wait)RememberMoving();
        // The real Button has already run its ordinary gameplay callback.
        EvaluateAction();
    }
    void EvaluateAction()
    {
        if(current.complete!=null&& !current.complete())
        {
            if(current.wait)
            {
                view.BlockInteraction();
                BindTarget(null);waiting=true;waitStarted=Time.unscaledTime;
                ModernCityHud.Instance?.CloseSheet();PoliceCityMode.Instance?.CloseDispatch();
                if(current.id=="rts.patrolConfirm"){patrolVisitedA=false;Focus((patrolA+patrolB)*.5f);}
                else if(current.point!=null)Focus((first.transform.position+current.point())*.5f);
                else Focus(first.transform.position);
                Resume();
            }
            else{actionAccepted=false;BindTarget(null);}
            return;
        }
        ShowResult();
    }
    void ShowResult()
    {
        view.BlockInteraction();
        waiting=false;showingResult=true;resultAt=Time.unscaledTime;Pause();BindTarget(null);
        var s=AgentSelectionManager.instance;
        resultText="Done. Tap NEXT.";
        if(current.title.Contains("PATROL")&&current.id=="rts.patrolConfirm")resultText="Both points reached. Patrol keeps running.";
        else if(current.id=="world.matchday")
        {
            resultText="On Matchday, fans and police arrive here automatically.";
        }
        else if(current.title=="DESELECT ALL")resultText="No members selected. Their orders continue.";
        else if(current.title.Contains("MEMBER")||current.title=="TAKE CONTROL AGAIN")
        {
            resultText=$"{s.SelectedAgents.Count} selected. See the green markers.";
            ModernCityHud.Instance?.CloseSheet();Focus(second?(first.transform.position+second.transform.position)*.5f:first.transform.position);
        }
        else if(current.wait)resultText="Complete. See the result, then tap NEXT.";
    }
    void PanLesson(Vector2 from,Vector2 to)
    {
        if(!active||current.gesture!=TutorialGesture.Pan)return;
        CameraPanTouchOnly.Instance?.TutorialPan(from,to);
        if(Vector3.Distance(cameraStart,Camera.main.transform.position)>3){gestureDone=true;AcceptTarget();}
    }
    void ZoomLesson(float amount)
    {
        if(!active||(current.gesture!=TutorialGesture.PinchIn&&current.gesture!=TutorialGesture.PinchOut))return;
        CameraPanTouchOnly.Instance?.TutorialZoom(amount);
        if(CameraPanTouchOnly.Instance&&Mathf.Abs(zoomStart-CameraPanTouchOnly.Instance.TutorialZoomValue)>=4){gestureDone=true;AcceptTarget();}
    }
    public void NextLesson()
    {
        if(!active)return;
        if(showingResult)
        {
            if(Time.unscaledTime-resultAt<=.65f)return;
            if(reviewReturn>=0)
            {
                index=reviewReturn;current=lessons[index];reviewReturn=-1;showingResult=reviewWasResult;resultAt=Time.unscaledTime;
                view.SetLesson(index,current.gesture);if(showingResult)ShowResult();else PrepareTarget();return;
            }
            Advance();return;
        }
        if(!waiting&&current.id==null&&current.point==null&&current.screen==null&&current.gesture==TutorialGesture.Tap){AcceptTarget();return;}
        // Skip is explicit. NEXT never bypasses an unperformed action or arrival.
    }
    public void PreviousLesson()
    {
        if(!active||index<=0||waiting)return;
        if(reviewReturn<0){reviewReturn=index;reviewWasResult=showingResult;}
        BindTarget(null);index--;current=lessons[index];showingResult=true;resultAt=Time.unscaledTime;
        resultText=current.text+" Tap NEXT to return.";Pause();view.SetLesson(index,current.gesture);
    }
    void RevealFeature(string id)
    {
        if(string.IsNullOrEmpty(id))return;
        if(id=="firm.crew"||id.StartsWith("firm.member")||id=="firm.deselectAll"){revealed.Add("crew");revealed.Add("selection");}
        if(id=="firm.deselectAll")revealed.Add("selection");
        if(id.StartsWith("firm.order")||id.StartsWith("rts."))revealed.Add("orders");
        if(id=="firm.home"||id.StartsWith("firm.build")||id=="firm.recruit"){revealed.Add("home");revealed.Add("resources");}
        if(id.StartsWith("city.map"))revealed.Add("map");
        if(id=="city.news")revealed.Add("news");
        if(id=="world.matchday"||id.StartsWith("world.matchday."))revealed.Add("matchday");
        if(id=="firm.away")revealed.Add("away");
    }
    void PrepareTarget()
    {
        string id=current.id??"";var hud=ModernCityHud.Instance;
        if((id=="firm.crew"||id=="firm.home")&&hud)hud.CloseSheet();
        if(id=="firm.orders"&&hud&&hud.OrdersOpen)hud.ToggleOrders();
        if(id.StartsWith("firm.member")&&hud&&!hud.SheetOpen)hud.OpenCrew();
        else if((id.StartsWith("firm.build")||id=="firm.recruit")&&hud&&!hud.SheetOpen)hud.OpenHome();
        else if(id.StartsWith("firm.order")&&id!="firm.orders"&&hud&&!hud.OrdersOpen)hud.ToggleOrders();
        else if(id.StartsWith("world.matchday.")&&!TutorialAnchor.Find(id)&&StadiumMatchdayControl.Instance)
        {if(id.StartsWith("world.matchday.plan"))StadiumMatchdayControl.Instance.ShowPlans();else StadiumMatchdayControl.Instance.Activate();}
        else if(id=="rts.choiceCancel"&&!TutorialAnchor.Find(id))CityGameplay.Instance?.CaptureNearest();
        ModernCityHud.Instance?.ApplyTutorialVisibility();
    }
    void Advance()
    {
        Resume();GameFeedback.Emit(GameFeedbackEvent.TaskComplete);
        Enter(index+1);
    }
    public void SkipPart()
    {
        if(!active)return;reviewReturn=-1;int part=current.part,step=index+1;
        while(step<lessons.Count&&lessons[step].part==part)step++;
        // Skip changes tutorial position only. It never grants a building, cash,
        // patrol check, matchday result, morality change or learned RTS flag.
        Resume();CloseSheets();Enter(step);
    }
    void SaveStep()
    {
        if(police)
        {
            PoliceCityMode.Career.PoliceTutorialVersion=Version;
            PoliceCityMode.Career.PoliceTutorialStep=index;
            PoliceCityMode.Instance.SaveCareerProgress();
        }
        else if(!matchdayStartGuide)
        {
            GameManager.Data.MobileTutorialVersion=Version;
            GameManager.Data.MobileTutorialStep=index;
            GameManager.Save();
        }
    }
    void End(bool completed)
    {
        BindTarget(null);Resume();active=false;waiting=false;showingResult=false;reviewReturn=-1;
        ModernCityHud.Instance?.ApplyTutorialVisibility();
        if(view)Destroy(view.gameObject);view=null;
        if(completed)
        {
            CloseSheets();
            if(police&&PoliceCityMode.Career!=null){PoliceCityMode.Career.PoliceTutorialVersion=Version;PoliceCityMode.Career.PoliceTutorialRequired=false;PoliceCityMode.Instance.SaveCareerProgress();}
            else if(GameManager.Data!=null){GameManager.Data.MobileTutorialVersion=Version;GameManager.Data.MobileTutorialRequired=false;GameManager.Data.FirstSessionCoachHidden=true;if(matchdayStartGuide)GameManager.Data.MatchdayTutorialPending=false;GameManager.Save();}
            CityGameplay.Instance?.PostEvent("GUIDE COMPLETE · Your city is live. Use HELP to replay any time.");
        }
    }
    void OnDisable(){End(false);}
    void OnDestroy(){End(false);if(Instance==this)Instance=null;}
    void CloseSheets()
    {
        ModernCityHud.Instance?.CloseSheet();if(ModernCityHud.Instance?.OrdersOpen==true)ModernCityHud.Instance.ToggleOrders();
        MatchdayStrategySystem.Instance?.CloseBoard();PoliceCityMode.Instance?.CloseDispatch();LiveMiniMap.Instance?.CloseTacticalMap();
        foreach(var hud in FindObjectsByType<CityMissionHUD>(FindObjectsSortMode.None))hud.CloseSheets();
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();
        WorldChoiceBar.DismissCurrent();RtsGestureController.Instance?.Cancel();AgentSelectionManager.instance?.CancelTargeting();
    }
    void Add(int part,string title,string text,string id=null,Action enter=null,Action action=null,Func<bool> complete=null,bool wait=false,Func<Vector3> point=null,bool auto=false,Func<bool> underway=null,TutorialGesture gesture=TutorialGesture.Tap,Func<Rect> screen=null)
    {lessons.Add(new Lesson{part=part,title=title,text=text,id=id,enter=enter,action=action,complete=complete,wait=wait,point=point,autoComplete=auto,underway=underway,gesture=gesture,screen=screen});}
    void Focus(Vector3 p)=>CameraPanTouchOnly.Instance?.TutorialFocus(p);
    void PickStreet(int turn=0,bool road=false,Vector3? vehicleOrigin=null)
    {
        var actor=AgentSelectionManager.instance.SelectedAgents.FirstOrDefault(a=>a&&a.IsAlive)??first;
        Vector3 start=vehicleOrigin??(road&&PolicePatrolVehicle.Instance?PolicePatrolVehicle.Instance.transform.position:actor.transform.position);
        var nav=actor.GetComponent<NavMeshAgent>();var filter=new NavMeshQueryFilter{agentTypeID=road?0:nav.agentTypeID,areaMask=road?PolicePatrolVehicle.RoadMask:nav.areaMask};
        var path=new NavMeshPath();street=start;worldTargetValid=false;
        for(int i=0;i<16;i++)
        {
            float a=(i*22.5f+turn*110)*Mathf.Deg2Rad;var guess=start+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*14;
            if(!NavMesh.SamplePosition(guess,out var hit,4,filter)||CityBusinessSystem.HorizontalDistance(hit.position,start)<7||!NavMesh.CalculatePath(start,hit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            street=hit.position;worldTargetValid=true;break;
        }
        Focus(street);
    }
    void MoveStreet()
    {
        var selection=AgentSelectionManager.instance;
        moving=selection.SelectedAgents.Where(a=>a&&a.IsAlive).ToArray();
        selection.CommandSelectedMoveTo(street);revisions=moving.Select(a=>a.OrderRevision).ToArray();
    }
    bool Arrived()
    {
        if(moving==null||moving.Length==0)return false;
        for(int i=0;i<moving.Length;i++)
        {
            var a=moving[i];if(!a||!a.IsAlive||a.OrderRevision!=revisions[i]||!beforeOrders.TryGetValue(a,out var prior)||a.OrderRevision<=prior)return false;
            var nav=a.GetComponent<NavMeshAgent>();
            if(!nav||!nav.isOnNavMesh||nav.pathPending||CityBusinessSystem.HorizontalDistance(a.transform.position,nav.destination)>Mathf.Max(2.5f,nav.stoppingDistance+1))return false;
        }
        return true;
    }
    Rect GestureRect()
    {
        float size=Screen.height*.42f;
        return new Rect((Screen.width-size)*.5f,Screen.height*.43f-size*.5f,size,size);
    }
    Rect MapMemberRect()
    {
        var map=LiveMiniMap.Instance;if(!map)return default;
        var rt=map.MapFrame;var uv=map.WorldToMap(CrewTrackingSystem.WorldPosition(first));
        var local=new Vector3(rt.rect.xMin+uv.x*rt.rect.width,rt.rect.yMin+uv.y*rt.rect.height);
        var point=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(local));
        return new Rect(point.x-40,point.y-40,80,80);
    }
    Rect NewsRect()
    {
        if(!CityNewsFeed.Instance||!CityNewsFeed.Instance.TryGetScreenBounds(out var rect))return default;
        var size=new Vector2(rect.width*.60f,rect.height*.36f);return new Rect(rect.center-size*.5f,size);
    }
    void BuildFirm()
    {
        var hud=ModernCityHud.Instance;var s=AgentSelectionManager.instance;var d=GameManager.Data;
        Add(1,"YOUR CREW","Tap CREW / FIND.","firm.crew",()=>{CloseSheets();s.DeselectAll();Focus(first.transform.position);});
        Add(1,"SELECT A MEMBER","Tap the real SELECT button.","firm.member."+first.Data.AgentId,complete:()=>first.IsSelected);
        Add(1,"FIND YOUR MEMBER","Double-tap to select and focus.",enter:()=>{CloseSheets();Focus(first.transform.position);},action:()=>s.SelectAndFocus(first),complete:()=>first.IsSelected,point:()=>first.transform.position,gesture:TutorialGesture.DoubleTap);
        Add(2,"LOOK AROUND","Drag one finger.",enter:CloseSheets,gesture:TutorialGesture.Pan,screen:GestureRect);
        Add(2,"ZOOM IN","Spread two fingers.",gesture:TutorialGesture.PinchOut,screen:GestureRect);
        Add(2,"ZOOM OUT","Pinch two fingers together.",gesture:TutorialGesture.PinchIn,screen:GestureRect);
        Add(2,"MOVE","Tap this street. Watch your member arrive.",enter:()=>PickStreet(),action:MoveStreet,complete:Arrived,wait:true,point:()=>street);
        Add(3,"MAKE A GROUP","Tap CREW / FIND.","firm.crew",CloseSheets);
        Add(3,"ADD A MEMBER","Tap SELECT for another member.",second?"firm.member."+second.Data.AgentId:null,complete:()=>!second||s.SelectedAgents.Count>=2);
        Add(4,"REMOVE ONE MEMBER","Tap DESELECT. The other stays selected.","firm.member."+first.Data.AgentId,enter:hud.OpenCrew,complete:()=>!first.IsSelected&&s.SelectedAgents.Count>0);
        Add(4,"DESELECT ALL","Clear selection. Orders keep running.","firm.deselectAll",enter:CloseSheets,complete:()=>s.SelectedAgents.Count==0);
        Add(4,"TAKE CONTROL AGAIN","Tap this member.",enter:()=>Focus(first.transform.position),action:()=>s.ToggleSelect(first),complete:()=>first.IsSelected,point:()=>first.transform.position);
        Add(5,"BUILD YOUR HOME","Tap HOME.","firm.home",()=>{CloseSheets();Focus(CityGameplay.Instance.Home);});
        Add(5,"HEADQUARTERS","Build your crew's base.","firm.build0",complete:()=>HomeTerritorySystem.Built(d,0),wait:true,auto:true,underway:()=>HomeTerritorySystem.Instance.IsBuilding&&HomeTerritorySystem.Instance.ActiveSite==0);
        Add(8,"MEMBER COMMANDS","Tap ORDERS.","firm.orders",CloseSheets);
        Add(8,"GUARD","Choose GUARD, then a post.","firm.order2");
        Add(8,"PLACE YOUR GUARD","Tap here. Watch your member take position.",enter:()=>PickStreet(),action:()=>{RtsGestureController.Instance.AcceptTargetPoint(street);RememberMoving();},complete:Arrived,wait:true,point:()=>street);
        Add(8,"PATROL","Tap ORDERS.","firm.orders");
        Add(8,"PATROL ROUTE","Choose PATROL.","firm.order3");
        Add(8,"FIRST PATROL POINT","Tap the first end of the route.",enter:()=>{PickStreet();patrolA=street;},action:()=>RtsGestureController.Instance.AcceptTargetPoint(patrolA),complete:()=>RtsGestureController.Instance.Mode==RtsGestureController.InputMode.PatrolB,point:()=>patrolA);
        Add(8,"SECOND PATROL POINT","Tap the other end.",enter:()=>{PickStreet(1);patrolB=street;},action:()=>RtsGestureController.Instance.AcceptTargetPoint(patrolB),complete:()=>RtsGestureController.Instance.Mode==RtsGestureController.InputMode.PatrolReady,point:()=>patrolB);
        Add(8,"START PATROL","Confirm. Watch both points being reached.","rts.patrolConfirm",complete:PatrolWalked,wait:true);
        Add(8,"STOP","Cancel this member's current job.","firm.order7",complete:()=>s.LastAcceptedOrderCount>0);
        Add(8,"SCOUT","Choose SCOUT, then a street.","firm.order5");
        Add(8,"SCOUT THIS STREET","Watch your member reach the intel marker.",enter:()=>PickStreet(),action:()=>{s.CommandSelectedGround(street);RememberMoving();},complete:Arrived,wait:true,point:()=>street);
        Add(9,"REGROUP","Return selected crew to Home.","firm.order4",complete:Arrived,wait:true);
        Add(9,"GROW YOUR ROSTER","Tap HOME.","firm.home",enter:()=>{CloseSheets();Focus(CityGameplay.Instance.Home);});
        Add(9,"RECRUIT ONE","Add one real member to your crew.","firm.recruit",()=>rosterBefore=d.RecruitedAgents.Count,complete:()=>d.MobileTutorialRecruitDone||d.RecruitedAgents.Count>rosterBefore,auto:true);
        Add(10,"STADIUM","Tap STADIUM. Fans arrive automatically on Matchday.","world.matchday",()=>{CloseSheets();StadiumMatchdayControl.Instance.FocusTutorial();});
        Add(11,"YOUR MINIMAP","Tap for the whole city and real crew routes.","city.map",enter:CloseSheets,action:()=>LiveMiniMap.Instance?.OpenTacticalMap(),complete:()=>LiveMiniMap.IsExpanded,screen:()=>LiveMiniMap.Instance?ScreenRect(LiveMiniMap.Instance.CompactTapTarget):default);
        Add(11,"YOUR CITY AT A GLANCE","Dots are crew. Lines are routes. Close the map.","city.mapClose",complete:()=>!LiveMiniMap.IsExpanded);
        Add(11,"CITY NEWS","Tap for injuries and events. Secure Home to unlock Away.","city.news",enter:()=>{CloseSheets();LiveMiniMap.Instance?.CloseTacticalMap();});
    }
    void RememberMoving()
    {
        moving=AgentSelectionManager.instance.SelectedAgents.Where(a=>a&&a.IsAlive).ToArray();
        revisions=moving.Select(a=>a.OrderRevision).ToArray();
    }
    bool PatrolWalked()
    {
        if(!first||!first.HasPatrolRoute(patrolA,patrolB))return false;
        if(CityBusinessSystem.HorizontalDistance(first.transform.position,patrolA)<3)patrolVisitedA=true;
        return patrolVisitedA&&CityBusinessSystem.HorizontalDistance(first.transform.position,patrolB)<3;
    }
    void AddMapAndNews(int part)
    {
        Add(part,"YOUR MINIMAP","Tap for the whole city and real crew routes.","city.map",enter:CloseSheets,action:()=>LiveMiniMap.Instance?.OpenTacticalMap(),complete:()=>LiveMiniMap.IsExpanded,screen:()=>LiveMiniMap.Instance?ScreenRect(LiveMiniMap.Instance.CompactTapTarget):default);
        Add(part,"YOUR CITY AT A GLANCE","Dots are crew. Lines are routes. Close the map.","city.mapClose",complete:()=>!LiveMiniMap.IsExpanded);
        Add(part,"CITY NEWS","Tap for named injuries and events.","city.news",enter:()=>{CloseSheets();LiveMiniMap.Instance?.CloseTacticalMap();});
    }
    void BuildMatchdayStart()
    {
        var hud=ModernCityHud.Instance;var s=AgentSelectionManager.instance;
        Add(10,"STADIUM MATCHDAY","Tap MATCHDAY to open the event controls.","world.matchday",
            ()=>{CloseSheets();StadiumMatchdayControl.Instance.FocusTutorial();});
        Add(10,"START WHEN READY","Start the event in your city.","world.matchday.start",()=>StadiumMatchdayControl.Instance.Activate(),complete:()=>StadiumMatchdayActivity.Instance.HasStarted,auto:true);
    }
    void BuildPolice()
    {
        var s=AgentSelectionManager.instance;var p=PoliceCityMode.Instance;
        Add(1,"YOU COMMAND THE POLICE","Tap an officer.","police.officer0",()=>{CloseSheets();s.DeselectAll();Focus(p.Station);},complete:()=>s.SelectedAgents.Count>=1);
        Add(1,"TAKE A TEAM","Add a second officer.","police.officer1",complete:()=>s.SelectedAgents.Count>=2);
        Add(2,"BOARD THE PATROL VAN","Tap BOARD. Wait for arrival.","police.boardExit",complete:()=>PolicePatrolVehicle.Instance.PassengerCount>=2,wait:true,auto:true,underway:()=>PolicePatrolVehicle.Instance.BoardingCount>0);
        Add(2,"SELECT THE VAN","Tap PATROL VAN.","police.van",complete:()=>PolicePatrolVehicle.Instance.Selected,auto:true);
        Add(2,"DRIVE ON ROADS","Tap this road to drive.",enter:()=>PickStreet(0,true),action:()=>PolicePatrolVehicle.Instance.DriveTo(street),complete:()=>!PolicePatrolVehicle.Instance.IsDriving&&CityBusinessSystem.HorizontalDistance(PolicePatrolVehicle.Instance.transform.position,street)<5,wait:true,point:()=>street);
        Add(2,"EXIT AT THE CURB","Tap EXIT OFFICERS.","police.boardExit",complete:()=>PolicePatrolVehicle.Instance.PassengerCount==0,wait:true,auto:true);
        Add(3,"READ DISPATCH","Tap DISPATCH.","police.dispatch");
        int arrestIndex=p.PrimaryArrestCallIndex;
        if(arrestIndex>=0)
        {
            var call=p.PrimaryArrestCall;
            Add(3,"ASSESS THIS CALL","Tap RESPOND. Identify the suspect.","police.call"+arrestIndex,complete:()=>call.state>=1,wait:true,auto:true);
            Add(3,"DETAIN THE SUSPECT","Tap DETAIN.","police.call"+arrestIndex,()=>p.OpenDispatch(),complete:()=>call.state>=3,wait:true,auto:true);
            Add(3,"LOAD INTO THE VAN","Tap LOAD SUSPECT.","police.load"+arrestIndex,()=>p.OpenDispatch(),complete:()=>call.inVehicle||call.state==4,wait:true,auto:true);
            Add(3,"RETURN TO THE STATION","Drive back to the station.","police.returnStation",()=>p.OpenDispatch(),complete:()=>!PolicePatrolVehicle.Instance.IsDriving&&CityBusinessSystem.HorizontalDistance(PolicePatrolVehicle.Instance.transform.position,p.Station)<18,wait:true,auto:true);
            Add(3,"BOOK THE SUSPECT","Unload to finish booking.","police.book",()=>p.OpenDispatch(),complete:()=>call.state==4,wait:true,auto:true);
        }
        Add(4,"CROWD CONTROL","Split teams between live incidents.",enter:()=>{p.CloseDispatch();Focus(CityGameplay.Instance.Locations[5]);});
        Add(4,"A CROWD RESPONSE TEAM","Select a foot officer.","police.officer0",()=>s.DeselectAll(),complete:()=>s.SelectedAgents.Count>=1);
        Add(4,"WORK TOGETHER","Add a second officer.","police.officer1",complete:()=>s.SelectedAgents.Count>=2);
        Add(4,"SEPARATE THE GANGS","Send both officers to separate the crowd.","police.crowdAssign",()=>p.OpenDispatch(),complete:()=>p.CrowdOperations.CurrentState>=2,wait:true,auto:true);
        Add(4,"A BLOCKED ROAD","A truck needs officers on site.",enter:()=>{p.EnsureTutorialRoadIncident();if(p.TutorialRoadIncident!=null)Focus(p.TutorialRoadIncident.Location);});
        Add(4,"COVER THE RECOVERY WORK","Send foot officers here.","police.roadAssign",()=>{p.OpenDispatch();},complete:()=>p.TutorialRoadIncident!=null&&(p.TutorialRoadIncident.IsCleared||CityRoadIncidentSystem.Instance.HasOfficerCoverage(p.TutorialRoadIncident.Id)),wait:true,auto:true);
        Add(4,"DISPATCH THE CRANE","Dispatch recovery to clear this road.",PoliceRecoveryVehicle.Instance?"police.recoveryDispatch":"police.recoveryHire",()=>p.OpenDispatch(),complete:()=>p.TutorialRoadIncident==null||p.TutorialRoadIncident.IsCleared,wait:true,auto:true);
        Add(4,"CONTROL THE RECOVERY TRUCK","Tap DRIVE RECOVERY.","police.recoverySelect",()=>p.OpenDispatch(),complete:()=>PoliceRecoveryVehicle.Instance&&PoliceRecoveryVehicle.Instance.Selected,auto:true);
        Add(4,"DRIVE THE TRUCK YOURSELF","Tap a road to drive.",enter:()=>{p.CloseDispatch();if(PoliceRecoveryVehicle.Instance)PickStreet(1,true,PoliceRecoveryVehicle.Instance.transform.position);else worldTargetValid=false;},action:()=>PoliceRecoveryVehicle.Instance?.DriveTo(street),complete:()=>PoliceRecoveryVehicle.Instance&&!PoliceRecoveryVehicle.Instance.IsDriving&&CityBusinessSystem.HorizontalDistance(PoliceRecoveryVehicle.Instance.transform.position,street)<5,wait:true,point:()=>street);
        Add(5,"BRIBE OR ARREST","Bribes pay cash but cost public trust.",enter:()=>p.OpenDispatch());
        Add(5,"YOUR SHIFT HAS CONSEQUENCES","Lawful arrests build trust and support.");
        AddMapAndNews(6);
    }
    public static Rect ScreenRect(RectTransform rt)
    {var c=new Vector3[4];rt.GetWorldCorners(c);var a=RectTransformUtility.WorldToScreenPoint(null,c[0]);var b=RectTransformUtility.WorldToScreenPoint(null,c[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
    static void Reveal(Button b)
    {
        Canvas.ForceUpdateCanvases();var scroll=b.GetComponentInParent<ScrollRect>();if(!scroll||!scroll.viewport||!scroll.content)return;
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,b.transform);
        var viewport=scroll.viewport.rect;float delta=bounds.max.y-viewport.yMax+12;
        if(bounds.min.y<viewport.yMin)delta=bounds.min.y-viewport.yMin-12;
        if(Mathf.Abs(delta)>1){var p=scroll.content.anchoredPosition;p.y-=delta;scroll.content.anchoredPosition=p;Canvas.ForceUpdateCanvases();}
    }
}
