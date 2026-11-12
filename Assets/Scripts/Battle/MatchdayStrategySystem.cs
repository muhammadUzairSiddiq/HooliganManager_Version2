using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>Plan, pre-position, split crew and spend limited supplies during a living matchday.</summary>
public sealed class MatchdayStrategySystem:MonoBehaviour
{
    public const float ResponseHoldSeconds=20f;
    public static MatchdayStrategySystem Instance{get;private set;}
    public IReadOnlyList<Vector3> ResponsePoints=>points;
    public Vector3 EscortPickup=>pickup;
    public float EscortRouteMetres{get;private set;}
    public Vector3 StallPoint=>stallPoint;
    public bool IsOpen=>board&&board.activeInHierarchy;
    public bool ResponseComplete(int index)=>index>=0&&index<2&&GameManager.Data?.MatchdayCompletedResponses?.Contains(responseIds[index])==true;
    public string CurrentPurpose
    {
        get
        {
            var d=GameManager.Data;var s=MatchdayTactics.Ensure(d);
            if(!owner||!owner.HasStarted)return "MATCHDAY · start at the stadium when your crew is ready.";
            if(owner.StreetOperations&&owner.StreetOperations.IsCurrent)return owner.StreetOperations.Summary;
            if(owner.IsAftermath)return d.MatchdayIncidentResolved?"MATCHDAY COMPLETE · review your results and continue.":"MATCHDAY AFTERMATH · retry missed coverage to protect both locations.";
            int next=Enumerable.Range(0,2).FirstOrDefault(i=>!ResponseComplete(i));
            if(!ResponseComplete(next))return $"{titles[next]} · {CrewNear(points[next],11)}/{MatchdayTactics.CrewRequired(d,next)} crew · {Mathf.Max(0,ResponseDeadline(next)-owner.ElapsedSeconds):0}s left";
            return s.escortComplete?"Core locations protected. Staff your stall or defend your income through dispersal.":"Locations covered. Escort arriving supporters or staff trade while the city stays active.";
        }
    }
    StadiumMatchdayActivity owner;
    readonly Vector3[] points=new Vector3[2];
    readonly TextMeshPro[] markers=new TextMeshPro[2];
    readonly string[] responseIds={"stadium-approach","pub-security"};
    readonly string[] titles={"STADIUM APPROACH","PUB SECURITY"};
    readonly List<GameObject> responseObjects=new List<GameObject>();
    GameObject board,stallModel;
    RectTransform body;
    TextMeshProUGUI summary,escortText,stallText;
    readonly TextMeshProUGUI[] responseText=new TextMeshProUGUI[2],planText=new TextMeshProUGUI[3];
    readonly Button[] assign=new Button[2],supply=new Button[2],plans=new Button[3];
    Button startButton,retryButton,closeDayButton,escortButton,stallButton;
    readonly List<GameObject> planRows=new List<GameObject>();
    EnemyController supporter;
    readonly List<EnemyController> escortParty=new List<EnemyController>();
    Vector3 pickup,stallPoint;
    int focusedResponse=-1,stallRevision=-1;
    float refresh,followClock,saveClock;
    public static float ResponseDeadline(int index)=>index==0?270f:420f;
    public static MatchdayStrategySystem Ensure(StadiumMatchdayActivity stadium,Vector3 center)
    {
        var system=stadium.GetComponent<MatchdayStrategySystem>()??stadium.gameObject.AddComponent<MatchdayStrategySystem>();system.owner=stadium;
        var city=CityGameplay.Instance;
        system.points[0]=city.Locations[5];
        system.points[1]=CityGameplay.ReachableApproach(city.Home,city.Locations[1]);
        system.pickup=system.FindEscortMeetingPoint(city);
        system.stallPoint=CityGameplay.ReachableApproach(city.Home,system.points[1]+Vector3.right*20);
        MatchdayTactics.Ensure(GameManager.Data);system.ActivateResponses();return system;
    }
    Vector3 FindEscortMeetingPoint(CityGameplay city)
    {
        var prefab=BattleManager.instance.playerAgentPrefab;
        var nav=prefab?prefab.GetComponent<NavMeshAgent>():null;
        var filter=new NavMeshQueryFilter{agentTypeID=nav?nav.agentTypeID:0,areaMask=nav?nav.areaMask:NavMesh.AllAreas};
        var arrivalPath=new NavMeshPath();var returnPath=new NavMeshPath();
        // Ambient supporters still travel from the station. The controllable
        // escort begins on that route near the stadium so a starter walker can
        // finish within one matchday, rather than spending it crossing the map.
        if(NavMesh.CalculatePath(points[0],city.Locations[7],filter,arrivalPath)&&arrivalPath.status==NavMeshPathStatus.PathComplete)
        {
            float remaining=70f;
            for(int i=1;i<arrivalPath.corners.Length;i++)
            {
                var a=arrivalPath.corners[i-1];var b=arrivalPath.corners[i];float distance=Vector3.Distance(a,b);
                if(distance<remaining){remaining-=distance;continue;}
                var candidate=Vector3.Lerp(a,b,remaining/Mathf.Max(.001f,distance));
                if(NavMesh.SamplePosition(candidate,out var hit,2f,filter)&&NavMesh.CalculatePath(hit.position,points[0],filter,returnPath)&&returnPath.status==NavMeshPathStatus.PathComplete)
                {float length=0;for(int k=1;k<returnPath.corners.Length;k++)length+=Vector3.Distance(returnPath.corners[k-1],returnPath.corners[k]);if(length>=20&&length<=150){EscortRouteMetres=length;return hit.position;}}
                break;
            }
        }
        for(int n=0;n<24;n++)
        {
            float angle=n*Mathf.PI/12;var candidate=points[0]+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*45;
            if(!NavMesh.SamplePosition(candidate,out var hit,5,filter)||!NavMesh.CalculatePath(hit.position,points[0],filter,returnPath)||returnPath.status!=NavMeshPathStatus.PathComplete)continue;
            float length=0;for(int k=1;k<returnPath.corners.Length;k++)length+=Vector3.Distance(returnPath.corners[k-1],returnPath.corners[k]);
            if(length>=20&&length<=150){EscortRouteMetres=length;return hit.position;}
        }
        Debug.LogError("No starter-speed supporter escort corridor is available near the stadium.");EscortRouteMetres=0;return points[0];
    }
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public static bool CanStart(PlayerData d,out string reason)
    {
        reason="";if(d==null){reason="Campaign unavailable.";return false;}
        if((d.RecruitedAgents?.Count(a=>a!=null&&a.IsAlive)??0)<2){reason="Recruit or recover two living crew for Matchday.";return false;}
        return MatchdayTactics.CanCommit(d,out reason);
    }
    public static bool RoutesReachable(out string reason)
    {
        reason="";var city=CityGameplay.Instance;if(!city||city.Locations==null){reason="City navigation is loading.";return false;}
        var path=new NavMeshPath();
        foreach(var point in new[]{city.Locations[1],city.Locations[5]})if(!NavMesh.CalculatePath(city.Home,point,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete){reason="A mandatory city route is blocked. Matchday cannot start until navigation is repaired.";return false;}
        return true;
    }
    public void ActivateResponses()
    {
        if(responseObjects.Count>0)return;
        for(int i=0;i<2;i++)
        {
            var root=new GameObject(titles[i]+" RESPONSE");root.transform.SetParent(transform,true);root.transform.position=points[i];responseObjects.Add(root);
            // A named destination uses the map and contextual card, not another permanent circle.
            markers[i]=ZoneLabelUtil.Create(root.transform,titles[i],4.5f,5.4f);
        }
        if(MatchdayTactics.Ensure(GameManager.Data)?.stallBuilt==true)ShowStall();
    }
    int CrewNear(Vector3 point,float radius)=>BattleManager.instance?BattleManager.instance.PlayerAgents.Count(a=>a&&a.IsAlive&&!a.IsActivityLocked&&!PolicePatrolVehicle.IsMounted(a)&&CityBusinessSystem.HorizontalDistance(a.transform.position,point)<radius):0;
    public void Assign(int response)
    {
        if(!owner||response<0||response>1)return;focusedResponse=response;
        RtsGestureController.Instance?.Cancel();AgentSelectionManager.instance?.ArmGuardCommand();AgentSelectionManager.instance?.CommandSelectedGround(points[response]);
        int accepted=AgentSelectionManager.instance?.LastAcceptedOrderCount??0;
        CityGameplay.Instance?.PostEvent(accepted>0?$"{accepted} crew assigned to {titles[response]}. Other groups keep working.":"Select available crew first. A response needs a complete route.");
        CameraPanTouchOnly.Instance?.FocusOn(points[response]);CloseBoard();
    }
    public bool SpendSupplies(int response)
    {
        if(!owner||!owner.HasStarted||owner.IsAftermath||response<0||response>1||ResponseComplete(response)||GameManager.Data?.MatchdayFailedResponses?.Contains(responseIds[response])==true||!MatchdayTactics.Support(GameManager.Data,response))return false;
        GameManager.Save();CityGameplay.Instance?.PostEvent(titles[response]+" equipped: hold time reduced by 5 seconds. Supplies -2.");return true;
    }
    public bool AssignEscort()
    {
        var d=GameManager.Data;var s=MatchdayTactics.Ensure(d);if(s==null||s.escortComplete||owner.IsAftermath||EscortRouteMetres<20)return false;
        Vector3 meet=s.escortPositioned?s.escortPosition:pickup;
        var actor=AgentSelectionManager.instance?.SelectedAgents.FirstOrDefault(a=>a&&a.IsAlive&&!a.IsActivityLocked);
        if(!actor||!actor.TryCommandMoveTo(meet,6)){CityGameplay.Instance?.PostEvent("Select an available crew member for the supporter escort.");return false;}
        s.escortOfficerId=actor.Data.AgentId;GameManager.Save();CameraPanTouchOnly.Instance?.FocusOn(meet);CloseBoard();return true;
    }
    public bool BuildStall()
    {
        var d=GameManager.Data;var s=MatchdayTactics.Ensure(d);if(s==null||owner.IsAftermath||s.stallBuilt||s.stallBuildRequested||d.Money<250||d.CitySupplies<3)return false;
        var builder=AgentSelectionManager.instance?.SelectedAgents.FirstOrDefault(a=>a&&a.IsAlive&&!a.IsActivityLocked&&a.TryCommandMoveTo(stallPoint,6));
        if(!builder){CityGameplay.Instance?.PostEvent("Select crew to build a matchday stall. It needs £250, 3 supplies and 20 seconds on site.");return false;}
        s.stallBuildRequested=true;s.stallBuilderId=builder.Data.AgentId;stallRevision=builder.OrderRevision;GameManager.Save();CameraPanTouchOnly.Instance?.FocusOn(stallPoint);CloseBoard();return true;
    }
    void Update()
    {
        var d=GameManager.Data;if(!owner||d==null)return;var tactics=MatchdayTactics.Ensure(d);
        if(owner.HasStarted)
        {
            d.MatchdayCompletedResponses??=new List<string>();d.MatchdayFailedResponses??=new List<string>();d.MatchdayPenalizedResponses??=new List<string>();
            bool streetDay=owner.StreetOperations&&owner.StreetOperations.IsCurrent;
            if(streetDay)
            {
                for(int i=0;i<markers.Length;i++)if(markers[i])markers[i].gameObject.SetActive(false);
                TickRallyEscort(d,tactics);
                TickStall(d,tactics);
                return;
            }
            for(int i=0;i<2;i++)
            {
                bool done=ResponseComplete(i),failed=d.MatchdayFailedResponses.Contains(responseIds[i]);float progress=i==0?d.MatchdayStadiumResponseSeconds:d.MatchdayPubResponseSeconds;
                if(!done&&!failed&&owner.ElapsedSeconds>=ResponseDeadline(i))
                {
                    d.MatchdayFailedResponses.Add(responseIds[i]);failed=true;
                    if(!d.MatchdayPenalizedResponses.Contains(responseIds[i])){d.MatchdayPenalizedResponses.Add(responseIds[i]);d.FanMorale=Mathf.Max(0,d.FanMorale-5);if(tactics.plan!=1)d.PoliceHeat=Mathf.Min(10,d.PoliceHeat+1);if(i==1){var pub=CityBusinessLedger.Find(d,"pub");if(pub!=null)pub.disruptionSeconds=Mathf.Max(pub.disruptionSeconds,90);}}
                    GameFeedback.Emit(GameFeedbackEvent.TaskFailed,points[i]);CityGameplay.Instance?.PostEvent(titles[i]+" missed. Consequences saved; retry from aftermath.");GameManager.Save();
                }
                float needed=MatchdayTactics.HoldSeconds(d,i);
                if(!done&&!failed&&CrewNear(points[i],11)>=MatchdayTactics.CrewRequired(d,i)&&!CityBusinessSystem.HostilesNear(points[i],12))
                {
                    progress=Mathf.Min(needed,progress+Time.deltaTime);if(i==0)d.MatchdayStadiumResponseSeconds=progress;else d.MatchdayPubResponseSeconds=progress;
                    if(progress>=needed){d.MatchdayCompletedResponses.Add(responseIds[i]);d.Money+=MatchdayTactics.Reward(d,i);d.FanMorale=Mathf.Min(100,d.FanMorale+2);GameFeedback.Emit(GameFeedbackEvent.TaskComplete,points[i]);CityGameplay.Instance?.PostEvent(titles[i]+$" covered. £{MatchdayTactics.Reward(d,i)} earned; crew remains under your control.");GameManager.Save();}
                }
                if(markers[i]){markers[i].text=titles[i]+"\n"+(ResponseComplete(i)?"COVERED":failed?"MISSED":$"{CrewNear(points[i],11)}/{MatchdayTactics.CrewRequired(d,i)} CREW · HOLD {progress:0}/{needed:0}s");markers[i].GetComponent<ZoneLabelBillboard>().CommandPriority=focusedResponse==i;}
            }
            TickEscort(d,tactics);TickStall(d,tactics);
            if(owner.IsAftermath&&!d.MatchdayIncidentResolved&&responseIds.All(id=>d.MatchdayCompletedResponses.Contains(id)))
            {d.MatchdayIncidentResolved=true;d.PoliceHeat=Mathf.Max(0,d.PoliceHeat-1);CampaignMissions.RecordAction(d,"matchday","day-"+d.MatchDay);GameManager.Save();CityGameplay.Instance?.CheckCampaignCompletion();}
            saveClock+=Time.deltaTime;if(saveClock>=10){saveClock=0;GameManager.Save();}
        }
        else TickStall(d,tactics);
        if(IsOpen&&Time.unscaledTime>=refresh){refresh=Time.unscaledTime+.3f;RefreshBoard();}
    }
    public bool RallyEscort()
    {
        var d=GameManager.Data;
        var tactics=MatchdayTactics.Ensure(d);
        if(!owner.HasStarted||owner.IsAftermath||tactics.escortComplete)return false;
        var meet=tactics.escortPositioned?tactics.escortPosition:pickup;
        var actor=AgentSelectionManager.instance?.SelectedAgents.FirstOrDefault(member=>member&&member.IsAlive
            &&!member.IsActivityLocked&&CityBusinessSystem.HorizontalDistance(member.transform.position,meet)<12);
        if(!actor)
        {
            CityGameplay.Instance?.PostEvent("Select a crew member beside the waiting fans to rally them.");
            return false;
        }
        tactics.escortOfficerId=actor.Data.AgentId;
        if(!tactics.escortPositioned)
        {
            tactics.escortPosition=pickup;
            tactics.escortPositioned=true;
            tactics.escortPartyPositions=new List<Vector3>();
            for(int index=0;index<5;index++)
                tactics.escortPartyPositions.Add(pickup+new Vector3((index%3-1)*1.8f,0,(index/3)*2f));
        }
        CityGameplay.Instance?.PostEvent("FANS RALLIED · move your guide to the stadium gate. The group follows on foot.");
        GameManager.Save();
        return true;
    }

    void TickRallyEscort(PlayerData data,MatchdayTacticsState tactics)
    {
        if(owner.IsAftermath||tactics.escortComplete||!tactics.escortPositioned||string.IsNullOrEmpty(tactics.escortOfficerId))return;
        tactics.escortPartyPositions??=new List<Vector3>();
        if(tactics.escortPartyPositions.Count==0)return;
        while(escortParty.Count<tactics.escortPartyPositions.Count)escortParty.Add(null);
        followClock+=Time.deltaTime;
        if(followClock<.45f)return;
        followClock=0;
        var guide=BattleManager.instance.PlayerAgents.FirstOrDefault(actor=>actor&&actor.IsAlive
            &&!actor.IsActivityLocked&&actor.Data.AgentId==tactics.escortOfficerId);
        bool guideAtGate=guide&&CityBusinessSystem.HorizontalDistance(guide.transform.position,points[0])<=10;
        int alive=0,arrived=0;
        for(int index=0;index<escortParty.Count;index++)
        {
            var body=escortParty[index];
            var saved=tactics.escortPartyPositions[index];
            if(saved.y < -100)continue;
            if(!body)
            {
                body=BattleManager.instance.SpawnMatchdayFighter(saved,"HOME FANS",LandscapeUI.Green,65,0,
                    MatchdayUnitRole.HomeSupporter,"PLAYER SUPPORTER ESCORT",true);
                if(!body)continue;
                escortParty[index]=body;
                body.HoldFire(true);
                var march=body.GetComponent<MatchdayMarch>();
                if(march)march.enabled=false;
            }
            if(!body.IsAlive)
            {
                tactics.escortPartyPositions[index]=new Vector3(0,-1000,0);
                continue;
            }
            alive++;
            tactics.escortPartyPositions[index]=body.transform.position;
            if(CityBusinessSystem.HorizontalDistance(body.transform.position,points[0])<13)arrived++;
            if(guide)
            {
                tactics.escortPosition=body.transform.position;
                var formation=guide.transform.right*((index%3-1)*1.5f)-guide.transform.forward*(2+index/3);
                var target=guideAtGate?points[0]+formation*.35f:guide.transform.position+formation;
                if(!body.TryBeginEscort(target,true)&&guideAtGate)
                    body.TryBeginEscort(points[0],true);
            }
            else body.EndEscort();
        }
        if(alive<3&&tactics.escortPartyPositions.Count(point=>point.y>=-100)<3)
        {
            foreach(var body in escortParty)if(body&&body.IsAlive)BattleManager.instance.DespawnMatchdayFighter(body);
            escortParty.Clear();
            tactics.escortPositioned=false;
            tactics.escortOfficerId=null;
            tactics.escortPartyPositions.Clear();
            CityGameplay.Instance?.PostEvent("Escort dispersed. Rally a fresh group or choose another Matchday objective.");
            GameManager.Save();
            return;
        }
        if(!guideAtGate||arrived<3)return;
        tactics.escortComplete=true;
        data.Money+=175;
        data.Reputation+=2;
        data.FanMorale=Mathf.Min(100,data.FanMorale+3);
        foreach(var body in escortParty)if(body&&body.IsAlive)body.TryBeginEscort(points[0],false);
        GameFeedback.Emit(GameFeedbackEvent.TaskComplete,points[0]);
        CityGameplay.Instance?.PostEvent("FANS CHECKED IN · your group reached the stadium approach. Escort earned £175.");
        GameManager.Save();
    }

    void TickEscort(PlayerData d,MatchdayTacticsState s)
    {
        if(s.escortComplete||string.IsNullOrEmpty(s.escortOfficerId)||owner.IsAftermath)return;
        var actor=BattleManager.instance.PlayerAgents.FirstOrDefault(a=>a&&a.IsAlive&&a.Data.AgentId==s.escortOfficerId);
        if(!s.escortPositioned)
        {
            if(!actor||CityBusinessSystem.HorizontalDistance(actor.transform.position,pickup)>8)return;
            s.escortPosition=pickup;s.escortPositioned=true;GameManager.Save();CityGameplay.Instance?.PostEvent("Supporter group met. Keep this crew member close and move them to STADIUM APPROACH for an escort reward.");
        }
        if(!supporter)
        {
            supporter=BattleManager.instance.SpawnMatchdayFighter(s.escortPosition,"ARRIVING SUPPORTERS",new Color(.15f,.75f,.4f),100,0,MatchdayUnitRole.HomeSupporter,"PLAYER SUPPORTER ESCORT");
            if(supporter){supporter.isHostile=false;supporter.HoldFire(true);var march=supporter.GetComponent<MatchdayMarch>();if(march)march.enabled=false;}
        }
        if(!supporter)return;s.escortPosition=supporter.transform.position;
        if(!actor){supporter.SetCinematicIdle(true);return;}
        // A guide who gets ahead must not permanently freeze the follower.
        // Both actors still have to travel and arrive before any reward.
        followClock+=Time.deltaTime;if(followClock>.4f){followClock=0;supporter.SetCinematicIdle(false);supporter.BeginEscort(actor.transform.position+Vector3.back*2,true);}
        if(CityBusinessSystem.HorizontalDistance(actor.transform.position,points[0])>10||CityBusinessSystem.HorizontalDistance(supporter.transform.position,points[0])>12||CityBusinessSystem.HostilesNear(points[0],12))return;
        s.escortSeconds+=Time.deltaTime;if(s.escortSeconds<5)return;
        s.escortComplete=true;d.Money+=175;d.Reputation+=2;d.FanMorale=Mathf.Min(100,d.FanMorale+3);supporter.SetCinematicIdle(true);GameFeedback.Emit(GameFeedbackEvent.TaskComplete,points[0]);GameManager.Save();CityGameplay.Instance?.PostEvent("Supporters arrived safely. Escort +£175, reputation +2, morale +3. No duplicate reward.");
    }
    void TickStall(PlayerData d,MatchdayTacticsState s)
    {
        if(s.stallBuildRequested)
        {
            var builder=BattleManager.instance.PlayerAgents.FirstOrDefault(a=>a&&a.IsAlive&&a.Data.AgentId==s.stallBuilderId);
            if(!builder||builder.IsActivityLocked||(stallRevision>=0&&builder.OrderRevision!=stallRevision)){s.stallBuildRequested=false;s.stallWorkSeconds=0;return;}
            if(stallRevision<0){if(!builder.TryCommandMoveTo(stallPoint,6)){s.stallBuildRequested=false;return;}stallRevision=builder.OrderRevision;}
            if(CityBusinessSystem.HorizontalDistance(builder.transform.position,stallPoint)>7||builder.CurrentState!=AgentController.State.Idle||CityBusinessSystem.HostilesNear(stallPoint,12))return;
            s.stallWorkSeconds+=Time.deltaTime;if(s.stallWorkSeconds<20)return;
            s.stallBuildRequested=false;if(d.Money<250||d.CitySupplies<3){s.stallWorkSeconds=0;return;}
            d.Money-=250;d.CitySupplies-=3;s.stallBuilt=true;ShowStall();GameManager.Save();CityGameplay.Instance?.PostEvent("MATCHDAY STALL BUILT. Keep one crew here to earn trade. The build and reward clock are saved.");
        }
        if(!s.stallBuilt||!owner.HasStarted||owner.IsAftermath||CrewNear(stallPoint,7)<1||CityBusinessSystem.HostilesNear(stallPoint,12))return;
        s.stallIncomeSeconds+=Time.deltaTime;
        while(s.stallIncomeSeconds>=45){s.stallIncomeSeconds-=45;int cash=s.plan==2?90:60;d.Money+=cash;CityGameplay.Instance?.PostEvent($"STAFFED MATCHDAY STALL · £{cash} trade. Moving its crew stops income.");}
    }
    void ShowStall()
    {
        if(stallModel)return;var prefab=Resources.Load<GameObject>("HomeFacilities/TradeStall");if(!prefab)return;
        stallModel=Instantiate(prefab,transform);stallModel.name="PLAYER BUILT MATCHDAY STALL";stallModel.transform.position=stallPoint;
        foreach(var collider in stallModel.GetComponentsInChildren<Collider>())collider.enabled=false;
        ZoneLabelUtil.Create(stallModel.transform,"MATCHDAY STALL",4,5);
    }
    public void ResetFixtureActors()
    {
        foreach(var actor in escortParty)
            if(actor&&actor.IsAlive&&actor.gameObject.activeInHierarchy&&actor.MatchdayGroupName=="PLAYER SUPPORTER ESCORT")
                BattleManager.instance.DespawnMatchdayFighter(actor);
        if(supporter&&supporter.IsAlive&&supporter.gameObject.activeInHierarchy&&supporter.MatchdayGroupName=="PLAYER SUPPORTER ESCORT")
            BattleManager.instance.DespawnMatchdayFighter(supporter);
        escortParty.Clear();supporter=null;
        if(stallModel)Destroy(stallModel);
        stallRevision=-1;
    }
    public void OpenBoard()
    {
        if(owner&&owner.AutomaticMatchdaysEnabled){owner.OpenBriefing();return;}
        if(!board)BuildBoard();CityUiSheets.Open(this,CloseBoard);board.SetActive(true);RefreshBoard();
    }
    public void CloseBoard(){if(board)board.SetActive(false);}
    RectTransform Row(string name,float height)
    {var row=LandscapeUI.Panel(name,body,0,0,880,height,true).rectTransform;LandscapeUI.LayoutSize(row.gameObject,880,height);return row;}
    TextMeshProUGUI Detail(Transform row,string name,float height=68)=>LandscapeUI.Text("Detail",row,name,16,16,600,height,18,LandscapeUI.White);
    void BuildBoard()
    {
        var frame=WorldButtonLayer.MissionFrame();var root=LandscapeUI.Panel("MatchdayStrategy",frame,320,190,960,650,true).rectTransform;board=root.gameObject;
        LandscapeUI.Text("Title",root,"MATCHDAY COMMAND",20,16,690,40,27,LandscapeUI.White,true);LandscapeUI.Button("Close",root,"CLOSE",790,14,150,48).onClick.AddListener(CloseBoard);
        summary=LandscapeUI.Text("Summary",root,"",22,72,916,90,18,LandscapeUI.White);
        body=LandscapeUI.Scroll("Decisions",root,20,172,920,368).content;
        for(int i=0;i<3;i++){int plan=i;var row=Row("Plan"+i,132);planRows.Add(row.gameObject);planText[i]=Detail(row,"");plans[i]=TutorialAnchor.Tag(LandscapeUI.Button("Choose",row,"CHOOSE",658,36,204,56),"match.plan"+i);plans[i].onClick.AddListener(()=>{MatchdayTactics.Choose(GameManager.Data,plan);GameManager.Save();RefreshBoard();});}
        for(int i=0;i<2;i++)
        {
            int index=i;var row=Row("Response"+i,170);responseText[i]=Detail(row,"",120);
            assign[i]=TutorialAnchor.Tag(LandscapeUI.Button("Assign",row,"SEND SELECTED",658,18,204,54,"green"),"match.response"+i);assign[i].onClick.AddListener(()=>Assign(index));
            supply[i]=LandscapeUI.Button("Supply",row,"EQUIP · 2 SUPPLIES",658,88,204,54);supply[i].onClick.AddListener(()=>{SpendSupplies(index);RefreshBoard();});
        }
        var escort=Row("SupporterEscort",148);escortText=Detail(escort,"",110);escortButton=LandscapeUI.Button("Assign",escort,"ASSIGN ESCORT",658,44,204,58);escortButton.onClick.AddListener(()=>AssignEscort());
        var stall=Row("Stall",164);stallText=Detail(stall,"",130);stallButton=LandscapeUI.Button("Build",stall,"BUILD STALL",658,48,204,58);stallButton.onClick.AddListener(()=>{var s=MatchdayTactics.Ensure(GameManager.Data);if(s.stallBuilt){CameraPanTouchOnly.Instance?.FocusOn(stallPoint);AgentSelectionManager.instance?.CommandSelectedMoveTo(stallPoint);CloseBoard();}else BuildStall();});
        startButton=TutorialAnchor.Tag(LandscapeUI.Button("Start",root,"START MATCHDAY",20,566,920,62,"green"),"match.start");startButton.onClick.AddListener(()=>{if(owner.BeginMatchday())CloseBoard();else RefreshBoard();});
        retryButton=LandscapeUI.Button("RetryResponses",root,"RETRY MISSED COVERAGE",20,566,920,62);retryButton.onClick.AddListener(()=>{owner.RetryMissedResponses();CloseBoard();});
        closeDayButton=LandscapeUI.Button("CloseDay",root,"REVIEW & CONTINUE TO NEXT DAY",20,566,920,62,"green");closeDayButton.onClick.AddListener(()=>{CloseBoard();owner.CloseMatchday();});
    }
    void RefreshBoard()
    {
        var d=GameManager.Data;var s=MatchdayTactics.Ensure(d);bool ready=CanStart(d,out string reason);
        summary.text=$"{owner.CurrentPhase} · {MatchdayTactics.Names[s.plan]} · £{d.Money} · {d.CitySupplies} SUPPLIES\n"+(owner.HasStarted?"Split your limited crew between coverage, escort and trade.":ready?"Select groups outside this sheet, then SEND SELECTED before starting.":reason)+"\nSWIPE THIS LIST for plans, supporter escort and a buildable trade stall.";
        for(int i=0;i<3;i++){planRows[i].SetActive(!owner.HasStarted);planText[i].text=MatchdayTactics.Names[i]+"\n"+MatchdayTactics.Description(i);plans[i].GetComponentInChildren<TextMeshProUGUI>().text=s.plan==i?"SELECTED":"CHOOSE";plans[i].interactable=s.plan!=i;}
        for(int i=0;i<2;i++)
        {
            bool done=ResponseComplete(i),failed=d.MatchdayFailedResponses?.Contains(responseIds[i])==true;float held=i==0?d.MatchdayStadiumResponseSeconds:d.MatchdayPubResponseSeconds;
            responseText[i].text=$"{titles[i]} · {(done?"COVERED":failed?"MISSED":"NEEDS COVERAGE")}\n{CrewNear(points[i],11)}/{MatchdayTactics.CrewRequired(d,i)} crew on site · hold {held:0}/{MatchdayTactics.HoldSeconds(d,i):0}s\n"+(owner.HasStarted?$"{Mathf.Max(0,ResponseDeadline(i)-owner.ElapsedSeconds):0}s opportunity left · £{MatchdayTactics.Reward(d,i)} reward":"PRE-POSITION NOW. Coverage begins only when you start.");
            assign[i].interactable=!done&&!failed&&!owner.IsAftermath&&AgentSelectionManager.instance?.SelectedAgents.Count>0;supply[i].gameObject.SetActive(owner.HasStarted&&!owner.IsAftermath&&!done&&!failed);supply[i].interactable=d.CitySupplies>=2&&!s.supplySupport.Contains(i);
        }
        escortText.text="OPTIONAL · SUPPORTER ESCORT\n"+(s.escortComplete?"ARRIVED SAFELY · £175 and reputation saved.":s.escortPositioned?"Group follows its assigned member. Move them to STADIUM APPROACH and wait for the supporters to arrive.":$"Meet supporters on the stadium arrival corridor. Escort route {EscortRouteMetres:0}m; about {EscortRouteMetres/1.3f:0}s at starter walking speed. Reward £175, morale +3, reputation +2.");
        escortButton.interactable=!s.escortComplete&&!owner.IsAftermath;escortButton.GetComponentInChildren<TextMeshProUGUI>().text=s.escortPositioned?"MEET / TAKE OVER":"ASSIGN ESCORT";
        stallText.text="OPTIONAL · BUILD & STAFF A STALL\n"+(s.stallBuilt?$"BUILT. One nearby crew earns £{(s.plan==2?90:60)} / 45s while matchday is live. Staff elsewhere and this income stops.":s.stallBuildRequested?$"BUILDING {s.stallWorkSeconds:0}/20s. Selected builder must work on site.":"Invest £250 + 3 supplies and 20s crew work. A visible stall remains for this matchday. Trade competes for your limited crew.");
        stallButton.interactable=!owner.IsAftermath&&(s.stallBuilt||!s.stallBuildRequested&&d.Money>=250&&d.CitySupplies>=3);stallButton.GetComponentInChildren<TextMeshProUGUI>().text=s.stallBuilt?"STAFF SELECTED":"BUILD STALL";
        startButton.gameObject.SetActive(!owner.HasStarted);startButton.interactable=ready;retryButton.gameObject.SetActive(owner.IsAftermath&&!d.MatchdayIncidentResolved);closeDayButton.gameObject.SetActive(owner.IsAftermath&&d.MatchdayIncidentResolved);
    }
}
