using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public enum CityOperationType
{
    ScoutDistrict, SupporterRally, GatherSupplies, CollectTickets, CommunityEvent,
    ObservePolice, PrepareTransport, FirstAid, PrepareStadium
}

/// <summary>Persistent resource rules for the non-combat live-city RTS loop.</summary>
public static class CityOperationsLedger
{
    public const int OperationCount = 9;

    public static void Ensure(PlayerData d)
    {
        if(d==null)return;
        CampaignMissions.Ensure(d);
        if(d.CompletedCityOperations==null)d.CompletedCityOperations=new List<string>();
        if(d.CampaignResourceMission==d.CurrentLevel)return;
        d.CampaignResourceMission=d.CurrentLevel;
        d.OperationsMatchday=d.MatchDay;
        d.CityIntel=0;d.CitySupplies=0;d.MatchTickets=0;d.SocialMomentum=0;
        d.TransportPrepared=false;d.StadiumAccessPrepared=false;
        d.CompletedCityOperations.Clear();
        if(d.RecruitedAgents!=null)
            for(int i=0;i<d.RecruitedAgents.Count;i++)
            {
                var member=d.RecruitedAgents[i];if(member==null)continue;
                member.EnsureManagementProfile(i);member.Stamina=Mathf.Min(100f,member.Stamina+35f);
            }
    }

    public static bool IsComplete(PlayerData d,CityOperationType type)
        =>CampaignMissions.OperationComplete(d,type);

    public static bool CanStart(PlayerData d,CityOperationType type,int livingCrew,out string reason)
    {
        reason=string.Empty;if(d==null){reason="Campaign data unavailable.";return false;}
        Ensure(d);
        if(IsComplete(d,type)){reason="Already completed for this mission.";return false;}
        switch(type)
        {
            case CityOperationType.GatherSupplies:
                if(d.Money<150){reason="Requires £150 for supplies.";return false;}break;
            case CityOperationType.CollectTickets:
                if(d.CityIntel<1){reason="Scout the district first.";return false;}
                if(d.Money<200){reason="Requires £200 for the ticket allocation.";return false;}break;
            case CityOperationType.ObservePolice:
                if(d.CityIntel<1){reason="Requires at least 1 intel.";return false;}break;
            case CityOperationType.PrepareTransport:
                if(d.CityIntel<2){reason="Requires 2 intel to plan a safe route.";return false;}break;
            case CityOperationType.FirstAid:
                if(d.CitySupplies<1){reason="Collect supplies first.";return false;}break;
            case CityOperationType.PrepareStadium:
                if(d.MatchTickets<Mathf.Max(1,livingCrew)){reason="Collect enough tickets for the active crew.";return false;}
                if(d.SocialMomentum<2){reason="Build social momentum through supporter/community activities.";return false;}
                if(!d.TransportPrepared){reason="Prepare the transport route first.";return false;}break;
        }
        return true;
    }

    public static void Apply(PlayerData d,CityOperationType type,int livingCrew)
    {
        Ensure(d);
        switch(type)
        {
            case CityOperationType.ScoutDistrict:d.CityIntel=Mathf.Min(10,d.CityIntel+2);break;
            case CityOperationType.SupporterRally:d.FanMorale=Mathf.Min(100,d.FanMorale+8);d.SocialMomentum+=1;break;
            case CityOperationType.GatherSupplies:d.Money-=150;d.CitySupplies+=2;break;
            case CityOperationType.CollectTickets:d.Money-=200;d.MatchTickets+=Mathf.Max(1,livingCrew);break;
            case CityOperationType.CommunityEvent:d.Reputation+=2;d.FanMorale=Mathf.Min(100,d.FanMorale+3);d.SocialMomentum+=2;d.PoliceHeat=Mathf.Max(0,d.PoliceHeat-1);break;
            case CityOperationType.ObservePolice:d.CityIntel=Mathf.Min(10,d.CityIntel+1);d.PoliceHeat=Mathf.Max(0,d.PoliceHeat-2);break;
            case CityOperationType.PrepareTransport:d.TransportPrepared=true;break;
            case CityOperationType.FirstAid:d.CitySupplies=Mathf.Max(0,d.CitySupplies-1);break;
            case CityOperationType.PrepareStadium:d.StadiumAccessPrepared=true;d.Reputation+=3;d.Money+=300;break;
        }
        CampaignMissions.CompleteOperation(d,type);
        if(!d.CompletedCityOperations.Contains(type.ToString()))d.CompletedCityOperations.Add(type.ToString());
    }

    public static string Resources(PlayerData d)
    {
        if(d==null)return "";Ensure(d);
        var progress=CampaignMissions.Progress(d);
        return $"MISSION {d.CurrentLevel:00}  {progress.complete}/{progress.total}     INTEL {d.CityIntel}/10     SUPPLIES {d.CitySupplies}     TICKETS {d.MatchTickets}     SOCIAL {d.SocialMomentum}";
    }
}

/// <summary>Coordinates parallel role-based city operations and their operations board.</summary>
public sealed class CityOperationsSystem : MonoBehaviour
{
    public static CityOperationsSystem Instance {get;private set;}
    readonly List<CityOperationNode> nodes=new List<CityOperationNode>();
    RectTransform frame,content;
    GameObject panel;
    Button boardButton;
    float nextUiRefresh;
    bool automatedPlaytest;
    public IReadOnlyList<CityOperationNode> Nodes=>nodes;
    public bool AutomatedPresentationDisabled=>automatedPlaytest;
    public int CompletedCount=>CampaignMissions.Progress(GameManager.Data).complete;
    public int RunningCount=>nodes.Count(n=>n&&n.IsRunning);
    public string ResourceSummary=>CityOperationsLedger.Resources(GameManager.Data);
    public bool IsBoardOpen=>panel&&panel.activeInHierarchy;

    public static CityOperationsSystem Ensure(CityGameplay city)
    {
        if(Instance)return Instance;
        var go=new GameObject("City RTS Operations");
        var system=go.AddComponent<CityOperationsSystem>();
        system.CreateNodes(city);
        return system;
    }

    void Awake(){Instance=this;CityOperationsLedger.Ensure(GameManager.Data);}
    void OnDestroy(){if(Instance==this)Instance=null;}

    public void AttachHud(RectTransform root)
    {
        if(frame||!root)return;frame=root;
        boardButton=LandscapeUI.Button("CityOperations",frame,"OPERATIONS",488,22,150,50,"dark");
        boardButton.onClick.AddListener(OpenBoard);
        var opIcon=LandscapeTheme.Current?.lootBat;
        if(opIcon)
        {
            var icon=LandscapeUI.Image("Icon",boardButton.transform,9,15,20,20,opIcon,Color.white,true);icon.raycastTarget=false;
            var label=boardButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if(label)LandscapeUI.Place(label.rectTransform,30,4,116,42);
        }
        // Use a dedicated overlay canvas. Gameplay contains several independently
        // sorted canvases (minimap, conversation UI), so a nested panel can be
        // logically active yet render behind them on some landscape resolutions.
        var canvasObject=new GameObject("CityOperationsCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform,false);
        var overlay=canvasObject.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=25000;
        LandscapeUI.ConfigureLandscapeScaler(canvasObject.GetComponent<CanvasScaler>());
        var safe=LandscapeUI.Rect("SafeArea",canvasObject.transform,0,0,1600,900);LandscapeUI.Stretch(safe);
        var overlayFrame=LandscapeUI.Rect("Frame",safe,0,0,1600,900);
        var viewport=safe.gameObject.AddComponent<LandscapeViewport>();viewport.frame=overlayFrame;viewport.Fit();
        var modal=LandscapeUI.Rect("CityOperationsModal",overlayFrame,0,0,1600,900);
        LandscapeUI.Image("Dimmer",modal,0,0,1600,900,null,new Color(.01f,.02f,.025f,.64f)).raycastTarget=true;
        var card=LandscapeUI.Panel("CityOperationsBoard",modal,260,125,1080,650,true);
        panel=modal.gameObject;
        LandscapeUI.Text("Title",panel.transform,"MISSION OPERATIONS",24,18,750,42,29,null,true);
        // The board content belongs to the centered card, while the modal covers
        // the complete safe area to block accidental world commands underneath.
        var cardRoot=card.transform;
        var title=panel.transform.Find("Title");if(title)title.SetParent(cardRoot,false);
        LandscapeUI.Text("Help",cardRoot,"Assign specialists. START automatically routes selected crew to the task; matching roles finish 30% faster.",24,61,830,48,18,LandscapeUI.Muted);
        LandscapeUI.Button("Close",cardRoot,"CLOSE",884,22,170,48).onClick.AddListener(()=>panel.SetActive(false));
        content=LandscapeUI.Scroll("OperationsScroll",cardRoot,20,119,1040,510).content;
        panel.SetActive(false);RefreshBoard();
    }

    void Update()
    {
        if(Time.unscaledTime<nextUiRefresh)return;nextUiRefresh=Time.unscaledTime+.35f;
        if(boardButton)
        {
            var label=boardButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if(label)
            {
                label.text="OPERATIONS";
            }
        }
        if(panel&&panel.activeSelf)RefreshBoard();
    }

    void CreateNodes(CityGameplay city)
    {
        if(city==null||city.Locations==null||city.Locations.Length<9)return;
        Add(CityOperationType.ScoutDistrict,"SCOUT DISTRICT","Reveal routes and build intel.",city.Locations[6],9f,14f,"SCOUT");
        Add(CityOperationType.SupporterRally,"ORGANIZE SUPPORTERS","Coordinate chants and supporter groups.",city.Locations[1]+new Vector3(9,0,-7),10f,13f,"ORGANIZER");
        Add(CityOperationType.GatherSupplies,"SUPPLY RUN","Collect food, water and first-aid stock.",city.Locations[8],8f,12f,"RUNNER");
        Add(CityOperationType.CollectTickets,"TICKET ALLOCATION","Secure entry for the active crew.",city.Locations[5]+new Vector3(-12,0,-10),8f,13f,"ORGANIZER");
        Add(CityOperationType.CommunityEvent,"COMMUNITY EVENT","Build reputation through local social activity.",city.Locations[2]+new Vector3(9,0,8),11f,15f,"ORGANIZER");
        Add(CityOperationType.ObservePolice,"OBSERVE PATROLS","Track police movement and reduce risk.",city.Locations[7],9f,14f,"SCOUT");
        Add(CityOperationType.PrepareTransport,"PREPARE TRANSPORT","Plan the arrival and return route.",city.Locations[7]+new Vector3(10,0,-8),10f,15f,"RUNNER");
        Add(CityOperationType.FirstAid,"FIRST-AID POST","Use one supply to treat assigned members.",city.Locations[4]+new Vector3(8,0,0),7f,11f,"LEADER");
        Add(CityOperationType.PrepareStadium,"STADIUM ENTRY PLAN","Combine tickets, transport and supporter momentum.",city.Locations[5]+new Vector3(12,0,9),12f,17f,"LEADER");
    }

    void Add(CityOperationType type,string title,string description,Vector3 point,float duration,float stamina,string role)
    {
        Vector3 location=CityGameplay.ReachableApproach(CityGameplay.Instance.Home,point);
        var go=new GameObject(title);go.transform.position=location;
        var node=go.AddComponent<CityOperationNode>();node.Configure(this,type,title,description,duration,stamina,role);
        nodes.Add(node);
    }

    public void TryStart(CityOperationNode node)
    {
        if(!node||node.IsRunning)return;
        var d=GameManager.Data;int living=BattleManager.instance?.PlayerAgents.Count(a=>a&&a.IsAlive)??0;
        if(!CityOperationsLedger.CanStart(d,node.Type,living,out string reason))
        {CityGameplay.Instance?.PostEvent(node.Title+" - "+reason.ToUpperInvariant());return;}
        var selected=AgentSelectionManager.instance?.SelectedAgents
            .Where(a=>a&&a.IsAlive&&!a.IsActivityLocked&&a.Data!=null&&a.Data.Stamina>=node.StaminaCost)
            .Take(2).ToArray()??Array.Empty<AgentController>();
        if(selected.Length==0)
        {
            var fallback=BattleManager.instance?.PlayerAgents
                .Where(a=>a&&a.IsAlive&&!a.IsActivityLocked&&a.Data!=null&&a.Data.Stamina>=node.StaminaCost)
                .OrderByDescending(a=>RoleMatches(a.Data.ManagementRole,node.RecommendedRole))
                .ThenBy(a=>Horizontal(a.transform.position,node.transform.position)).FirstOrDefault();
            if(fallback){AgentSelectionManager.instance?.DeselectAll();AgentSelectionManager.instance?.Select(fallback);selected=new[]{fallback};}
        }
        if(selected.Length==0)
        {Feedback("NO RESTED MEMBER AVAILABLE FOR "+node.Title);return;}
        if(node.Type==CityOperationType.FirstAid&&selected.All(a=>a.CurrentHp>=a.Data.MaxHp))
        {Feedback("ASSIGN AN INJURED MEMBER TO FIRST AID");return;}
        if(selected.Any(a=>Horizontal(a.transform.position,node.transform.position)>10f))
        {
            node.Queue(selected);
            AgentSelectionManager.instance?.CommandSelectedMoveTo(node.transform.position);
            CameraPanTouchOnly.Instance?.FocusOn(node.transform.position);
            AgentSelectionManager.CreateCommandMarker(node.transform.position,LandscapeUI.Green,"TASK");
            Feedback(node.Title+" ASSIGNED - CREW MOVING INTO POSITION");
            if(!automatedPlaytest)GameplayOutcomePresentation.TaskStarted("CREW MOVING",node.Title+"\nFollow the cyan task ring.");
            return;
        }
        node.Begin(selected);
    }

    public void Complete(CityOperationNode node,IReadOnlyList<AgentController> members)
    {
        var d=GameManager.Data;int living=BattleManager.instance?.PlayerAgents.Count(a=>a&&a.IsAlive)??0;
        if(!CityOperationsLedger.CanStart(d,node.Type,living,out string reason))
        {CityGameplay.Instance?.PostEvent(node.Title+" CANCELLED - "+reason.ToUpperInvariant());return;}
        int heatBefore=d.PoliceHeat;
        CityOperationsLedger.Apply(d,node.Type,living);
        foreach(var member in members)
        {
            if(!member||member.Data==null)continue;
            member.Data.Stamina=Mathf.Max(0,member.Data.Stamina-node.StaminaCost);
            member.Data.OperationsExperience+=RoleMatches(member.Data.ManagementRole,node.RecommendedRole)?2:1;
            if(node.Type==CityOperationType.FirstAid)member.HealAmount(20f);
        }
        GameManager.Save();
        AgentSelectionManager.CreateCommandMarker(node.transform.position,LandscapeUI.Green,"COMPLETE");
        CityGameplay.Instance?.PostEvent(node.Title+" COMPLETE - "+RewardText(node.Type));
        if(!automatedPlaytest)GameplayOutcomePresentation.TaskComplete(node.Title,RewardText(node.Type),heatBefore,d.PoliceHeat);
        CityGameplay.Instance?.CheckCampaignCompletion();
        RefreshBoard();
    }

    public void PresentExecutionChoice(CityOperationNode node,IReadOnlyList<AgentController> members)
    {
        if(!node)return;
        if(automatedPlaytest){node.StartExecution(1f,0,0,"TEST PLAN CONFIRMED");return;}
        bool specialist=members.Any(a=>a&&a.Data!=null&&RoleMatches(a.Data.ManagementRole,node.RecommendedRole));
        var options=new List<GamePopup.Option>
        {
            new GamePopup.Option("SAFE",new Color(.10f,.58f,.82f),()=>node.StartExecution(1f,0,0,"SAFE APPROACH")),
            new GamePopup.Option("FAST +1 HEAT",LandscapeUI.Red,()=>node.StartExecution(.55f,1,0,"FAST APPROACH · HEAT +1"))
        };
        if(specialist)
            options.Add(new GamePopup.Option("USE "+node.RecommendedRole,LandscapeUI.Green,()=>node.StartExecution(.72f,0,75,"SPECIALIST BONUS +£75")));
        GamePopup.Instance.Show(node.Title,
            $"REWARD: {RewardText(node.Type)}\n\n{ExecutionBrief(node.Type)}\n\nChoose SAFE, FAST, or use your {node.RecommendedRole}.",options.ToArray());
    }

    static string ExecutionBrief(CityOperationType type)=>type switch
    {
        CityOperationType.ScoutDistrict=>"Reveal safe routes before moving the crew.",
        CityOperationType.SupporterRally=>"Organize supporters and raise morale.",
        CityOperationType.GatherSupplies=>"Secure food, water and first-aid stock.",
        CityOperationType.CollectTickets=>"Secure match entry for the active crew.",
        CityOperationType.CommunityEvent=>"Run a local event and cool police attention.",
        CityOperationType.ObservePolice=>"Watch the patrol. Completion lowers heat by 2.",
        CityOperationType.PrepareTransport=>"Set the van route for safe travel.",
        CityOperationType.FirstAid=>"Restore injured crew members.",
        CityOperationType.PrepareStadium=>"Combine tickets, transport and supporters for stadium entry.",
        _=>"Complete the task with the assigned crew."
    };

    public void MoveSelectedTo(CityOperationNode node)
    {
        if(AgentSelectionManager.instance==null)return;
        if(AgentSelectionManager.instance.SelectedAgents.Count==0)
        {CityGameplay.Instance?.PostEvent("SELECT ONE OR TWO CREW MEMBERS FIRST");return;}
        AgentSelectionManager.instance.CommandSelectedMoveTo(node.transform.position);
        CameraPanTouchOnly.Instance?.FocusOn(node.transform.position);
    }

    public void OpenBoard(){if(!panel)return;panel.SetActive(true);panel.transform.SetAsLastSibling();RefreshBoard();}
    public void CloseBoard(){if(panel)panel.SetActive(false);}

#if UNITY_EDITOR
    public void BeginAutomatedPlaytest(string reportPath)
    {
        automatedPlaytest=true;
        StopCoroutine(nameof(AutomatedPlaytest));
        StartCoroutine(AutomatedPlaytest(reportPath));
    }

    IEnumerator AutomatedPlaytest(string reportPath)
    {
        var rows=new List<string>();
        void Check(bool passed,string label)=>rows.Add((passed?"PASS ":"FAIL ")+label);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)??"Artifacts/CityQA");
        rows.Add("RUNNING City operations live playtest");File.WriteAllLines(reportPath,rows);
        var d=GameManager.Data;
        var crew=BattleManager.instance?.PlayerAgents.Where(a=>a&&a.IsAlive).Take(3).ToArray()??Array.Empty<AgentController>();
        if(d==null||crew.Length<2||AgentSelectionManager.instance==null)
        {rows.Add("FAIL Live campaign, selection manager and two crew members are required");File.WriteAllLines(reportPath,rows);yield break;}

        int oldIntel=d.CityIntel,oldSocial=d.SocialMomentum,oldMorale=d.FanMorale,oldHeat=d.PoliceHeat;
        var oldCompleted=d.CompletedCityOperations==null?new List<string>():new List<string>(d.CompletedCityOperations);
        var oldCampaignSteps=d.CompletedCampaignSteps==null?new List<string>():new List<string>(d.CompletedCampaignSteps);
        var oldMemberState=crew.Select(a=>(a.Data.Stamina,a.Data.OperationsExperience)).ToArray();
        foreach(var type in new[]{CityOperationType.ScoutDistrict,CityOperationType.SupporterRally,CityOperationType.CommunityEvent})
        { d.CompletedCityOperations.Remove(type.ToString());d.CompletedCampaignSteps.Remove(CampaignMissions.OperationKey(d.CurrentLevel,type)); }

        var scout=nodes.First(n=>n.Type==CityOperationType.ScoutDistrict);
        MoveForTest(crew[0],scout);SelectOnly(crew[0]);float scoutStamina=crew[0].Data.Stamina;
        TryStart(scout);
        Check(scout.IsRunning&&crew[0].IsActivityLocked,"Selected nearby crew member starts a timed scouting task");
        float deadline=Time.time+20f;while(scout.IsRunning&&Time.time<deadline)yield return null;
        Check(CityOperationsLedger.IsComplete(d,CityOperationType.ScoutDistrict)&&d.CityIntel>=oldIntel+2,"Scouting completes and produces district intel");
        Check(crew[0].Data.Stamina<scoutStamina&&crew[0].Data.OperationsExperience>oldMemberState[0].OperationsExperience,"Task consumes stamina and awards operations experience");
        Check(!crew[0].IsActivityLocked,"Crew member is released after task completion");
        File.WriteAllLines(reportPath,rows);

        var rally=nodes.First(n=>n.Type==CityOperationType.SupporterRally);
        var community=nodes.First(n=>n.Type==CityOperationType.CommunityEvent);
        MoveForTest(crew[0],rally);SelectOnly(crew[0]);TryStart(rally);
        MoveForTest(crew[1],community);SelectOnly(crew[1]);TryStart(community);
        Check(rally.IsRunning&&community.IsRunning&&RunningCount>=2,"Two different crew members run social operations in parallel");
        deadline=Time.time+24f;while((rally.IsRunning||community.IsRunning)&&Time.time<deadline)yield return null;
        Check(CityOperationsLedger.IsComplete(d,CityOperationType.SupporterRally)&&CityOperationsLedger.IsComplete(d,CityOperationType.CommunityEvent),"Parallel supporter and community activities complete");
        Check(d.SocialMomentum>=oldSocial+3&&d.FanMorale>=oldMorale+11,"Social activities feed supporter momentum and morale");

        d.CityIntel=oldIntel;d.SocialMomentum=oldSocial;d.FanMorale=oldMorale;d.PoliceHeat=oldHeat;
        d.CompletedCityOperations.Clear();d.CompletedCityOperations.AddRange(oldCompleted);
        d.CompletedCampaignSteps.Clear();d.CompletedCampaignSteps.AddRange(oldCampaignSteps);
        for(int i=0;i<crew.Length;i++){crew[i].Data.Stamina=oldMemberState[i].Item1;crew[i].Data.OperationsExperience=oldMemberState[i].Item2;}
        AgentSelectionManager.instance.DeselectAll();GameManager.Save();
        rows[0]="COMPLETE City operations live playtest";File.WriteAllLines(reportPath,rows);
        automatedPlaytest=false;
        if(rows.Any(r=>r.StartsWith("FAIL")))Debug.LogError("City operations playtest failed; see "+reportPath);
    }

    void MoveForTest(AgentController member,CityOperationNode node)
    {
        var nav=member.GetComponent<NavMeshAgent>();Vector3 target=node.transform.position+Vector3.right*3f;
        if(NavMesh.SamplePosition(target,out var hit,6f,NavMesh.AllAreas)&&nav&&nav.isOnNavMesh)nav.Warp(hit.position);
        else member.transform.position=target;
    }

    void SelectOnly(AgentController member)
    {AgentSelectionManager.instance.DeselectAll();AgentSelectionManager.instance.Select(member);}
#endif

    void RefreshBoard()
    {
        if(!content)return;
        foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        var d=GameManager.Data;CityOperationsLedger.Ensure(d);
        foreach(var node in nodes)
        {
            if(!node)continue;
            var row=LandscapeUI.Panel(node.Title,content,0,0,1002,100,true);LandscapeUI.LayoutSize(row.gameObject,1002,100);
            bool complete=CityOperationsLedger.IsComplete(d,node.Type);
            string state=complete?"COMPLETE":node.IsRunning?$"ACTIVE {node.Progress01*100:0}%":node.RequirementText;
            LandscapeUI.Text("Name",row.transform,node.Title+"  ·  "+node.RecommendedRole,16,9,570,29,20,complete?LandscapeUI.Green:LandscapeUI.White,true);
            LandscapeUI.Text("Info",row.transform,node.Description,16,41,575,46,16,LandscapeUI.Muted);
            LandscapeUI.Text("State",row.transform,state,596,17,170,60,15,complete?LandscapeUI.Green:LandscapeUI.Gold,true,TextAlignmentOptions.Center);
            var focus=LandscapeUI.Button("Focus",row.transform,"FOCUS",772,18,98,58,"dark");
            focus.onClick.AddListener(()=>{panel.SetActive(false);CameraPanTouchOnly.Instance?.FocusOn(node.transform.position);node.OpenInteraction();});
            var start=LandscapeUI.Button("Start",row.transform,complete?"DONE":"START",878,18,110,58,complete?"dark":"green");
            start.interactable=!complete&&!node.IsRunning;
            start.onClick.AddListener(()=>{panel.SetActive(false);TryStart(node);});
        }
        CityMissionHUD.Style(panel.transform);
    }

    static bool RoleMatches(string role,string recommended)=>role==recommended||role=="LEADER";
    public static float DurationFor(AgentController[] members,string recommended,float baseDuration)
        =>members.Any(a=>a&&a.Data!=null&&RoleMatches(a.Data.ManagementRole,recommended))?baseDuration*.7f:baseDuration;
    static float Horizontal(Vector3 a,Vector3 b){a.y=0;b.y=0;return Vector3.Distance(a,b);}
    static void Feedback(string message)
    {
        CityGameplay.Instance?.PostEvent(message);
        BattleUIController.instance?.ShowAlert(message,2.2f);
    }
    internal static string RewardText(CityOperationType type)=>type switch
    {
        CityOperationType.ScoutDistrict=>"INTEL +2",CityOperationType.SupporterRally=>"MORALE +8 / SOCIAL +1",
        CityOperationType.GatherSupplies=>"SUPPLIES +2",CityOperationType.CollectTickets=>"CREW TICKETS SECURED",
        CityOperationType.CommunityEvent=>"REP +2 / SOCIAL +2 / HEAT -1",CityOperationType.ObservePolice=>"INTEL +1 / HEAT -2",
        CityOperationType.PrepareTransport=>"TRANSPORT READY",CityOperationType.FirstAid=>"20 HP RESTORED",
        CityOperationType.PrepareStadium=>"STADIUM READY / £300 / REP +3",_=>"COMPLETE"
    };
}

public sealed class CityOperationNode:MonoBehaviour
{
    CityOperationsSystem owner;
    readonly List<AgentController> assigned=new List<AgentController>();
    TextMeshPro label;
    float duration,baseDuration,elapsed;
    bool waitingForArrival,awaitingDecision;
    public CityOperationType Type{get;private set;}
    public string Title{get;private set;}
    public string Description{get;private set;}
    public string RecommendedRole{get;private set;}
    public float StaminaCost{get;private set;}
    public bool IsRunning{get;private set;}
    public float Progress01=>duration<=0?0:Mathf.Clamp01(elapsed/duration);
    public string RequirementText
    {
        get
        {
            int living=BattleManager.instance?.PlayerAgents.Count(a=>a&&a.IsAlive)??0;
            return CityOperationsLedger.CanStart(GameManager.Data,Type,living,out string reason)?$"READY · {StaminaCost:0} STA":reason.ToUpperInvariant();
        }
    }

    public void Configure(CityOperationsSystem system,CityOperationType type,string title,string description,float seconds,float stamina,string role)
    {
        owner=system;Type=type;Title=title;Description=description;baseDuration=duration=seconds;StaminaCost=stamina;RecommendedRole=role;
        BuildVisual();
    }

    void BuildVisual()
    {
        ZoneVolumeFactory.Create(transform,new Color(.10f,.90f,.82f,1),3.2f,.12f);
        label=ZoneLabelUtil.Create(transform,Title,5.2f,6.6f);
        MiniMapIconFactory.Register(transform,MiniMapIconFactory.Kind.Turf,Title);
    }

    public void OpenInteraction()
    {
        if(IsRunning){CityGameplay.Instance?.PostEvent(Title+" - TASK IN PROGRESS");return;}
        if(CityOperationsLedger.IsComplete(GameManager.Data,Type))
        {GamePopup.Instance.Show(Title,"Completed for this matchday.\n\n"+Description,new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));return;}
        GamePopup.Instance.Show(Title,"REWARD: "+CityOperationsSystem.RewardText(Type)+"\nCOST: "+StaminaCost.ToString("0")+" stamina\nROLE: "+RecommendedRole+"\n\n"+Description,
            new GamePopup.Option("GO TO TASK",LandscapeUI.PanelColor,()=>owner.MoveSelectedTo(this)),
            new GamePopup.Option("START TASK",LandscapeUI.Green,()=>owner.TryStart(this)),
            new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));
    }

    public void Begin(AgentController[] members)
    {
        assigned.Clear();assigned.AddRange(members);elapsed=0;waitingForArrival=false;
        duration=CityOperationsSystem.DurationFor(members,RecommendedRole,baseDuration);
        foreach(var member in assigned)member.SetActivityLocked(true,transform.position);
        IsRunning=true;awaitingDecision=true;
        CityGameplay.Instance?.PostEvent(Title+" READY - CHOOSE AN EXECUTION PLAN");
        owner.PresentExecutionChoice(this,assigned);
    }

    public void StartExecution(float durationMultiplier,int heatDelta,int cashDelta,string outcome)
    {
        if(!IsRunning||!awaitingDecision)return;
        awaitingDecision=false;duration=Mathf.Max(2f,duration*durationMultiplier);
        var d=GameManager.Data;int heatBefore=d?.PoliceHeat??0;if(d!=null){d.PoliceHeat=Mathf.Clamp(d.PoliceHeat+heatDelta,0,10);d.Money+=cashDelta;GameManager.Save();}
        CityGameplay.Instance?.PostEvent(Title+" STARTED · "+outcome+" · "+string.Join(", ",assigned.Where(a=>a&&a.Data!=null).Select(a=>a.Data.AgentName)));
        if(!owner.AutomatedPresentationDisabled)
            GameplayOutcomePresentation.Message("TASK IN PROGRESS",Title,outcome+(heatDelta!=0?$"\nPolice heat: {heatBefore}/10 → {d.PoliceHeat}/10":""),heatDelta>0?LandscapeUI.Gold:new Color(.16f,.68f,1f),1.45f);
    }

    public void Queue(AgentController[] members)
    {
        assigned.Clear();assigned.AddRange(members);elapsed=0;waitingForArrival=true;
        foreach(var member in assigned)if(member)member.SetActivityLocked(false,transform.position);
    }

    void Update()
    {
        if(label&&Camera.main)label.transform.rotation=Camera.main.transform.rotation;
        if(waitingForArrival)
        {
            if(assigned.Any(a=>!a||!a.IsAlive)){Cancel("ASSIGNED MEMBER UNAVAILABLE");return;}
            if(label)label.text=Title+"\nCREW EN ROUTE";
            if(assigned.All(a=>HorizontalDistance(a.transform.position,transform.position)<=10f))Begin(assigned.ToArray());
            return;
        }
        if(!IsRunning)
        {
            if(label)label.text=CityOperationsLedger.IsComplete(GameManager.Data,Type)?Title+"\nCOMPLETE":Title+"\nTAP FOR TASK";
            return;
        }
        if(awaitingDecision){if(label)label.text=Title+"\nCHOOSE EXECUTION PLAN";return;}
        if(assigned.Any(a=>!a||!a.IsAlive)){Cancel("ASSIGNED MEMBER UNAVAILABLE");return;}
        elapsed+=Time.deltaTime;
        if(label)label.text=$"{Title}\n{Progress01*100:0}%";
        if(elapsed<duration)return;
        IsRunning=false;owner.Complete(this,assigned);
        Release();
    }

    void Cancel(string reason){IsRunning=false;waitingForArrival=false;awaitingDecision=false;CityGameplay.Instance?.PostEvent(Title+" CANCELLED - "+reason);Release();}
    void Release(){foreach(var member in assigned)if(member)member.SetActivityLocked(false,transform.position);assigned.Clear();}
    static float HorizontalDistance(Vector3 a,Vector3 b){a.y=0;b.y=0;return Vector3.Distance(a,b);}
    void OnDestroy(){Release();}
}
