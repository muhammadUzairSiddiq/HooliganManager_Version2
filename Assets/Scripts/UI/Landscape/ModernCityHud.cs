using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>City-first presentation: one purpose, tap-to-move, explained orders.</summary>
public sealed class ModernCityHud : MonoBehaviour
{
    public static ModernCityHud Instance {get;private set;}
    public static bool InspectWorld {get;private set;}
    public bool OrdersOpen=>orders&&orders.activeSelf;
    public bool SheetOpen=>sheet&&sheet.activeSelf;
    public string CurrentInstruction=>goal?goal.text:"";
    static bool AwayReady(PlayerData data) => CityGameplay.HomeMode&&CampaignMissions.HomeCleared(data)&&
        !CampaignMissions.Completed(data,CampaignMissions.MissionCount);
    static bool ShowMatchday(PlayerData data)
    {
        var stadium=StadiumMatchdayActivity.Instance;
        return CityGameplay.HomeMode&&stadium&&!stadium.AutomaticMatchdaysEnabled&&stadium.HasStarted&&
            !(stadium.IsAftermath&&data.MatchdayIncidentResolved&&data.LastAwayStockMission>1&&data.MatchdayTravelCredit==0);
    }
    RectTransform frame,body;
    GameObject orders,sheet,selectionCard,matchdayStrip,quickOrders;
    TextMeshProUGUI goal,phase,selectionText,hint,resources,title,matchdayHeadline;
    Button go,orderButton;
    CityMissionHUD legacy;
    float next;
    readonly List<AgentController>[] savedGroups={new List<AgentController>(),new List<AgentController>(),new List<AgentController>()};
    public void Initialize(RectTransform root,CityMissionHUD previous)
    {
        Instance=this;frame=root;legacy=previous;
        foreach(var name in new[]{"CurrentObjective","Manage","LeftHudToggle","StatusStrip","CommandRail","ManagementDrawer"})
            if(root.Find(name))root.Find(name).gameObject.SetActive(false);
        var hud=root.Find("BattleHUD");
        if(hud)foreach(var name in new[]{"TopBar","Heading","Cash","CompactHeat","Pause","SquadRail","SquadTitle","MemberCount","PortraitScroll","ManageSquad","DragSelect","BoxSelect","Deselect","SquadFocusHint","SelectionStatus","Move","Attack","Guard","Patrol","Retreat","CameraSetup","RecenterCamera"})
            if(hud.Find(name))hud.Find(name).gameObject.SetActive(false);
        foreach(var name in new[]{"CameraSetup","RecenterCamera"})if(root.Find(name))root.Find(name).gameObject.SetActive(false);
        var top=Panel("CityTop",16,12,1568,68);
        title=Text(top,"Place",CityGameplay.HomeMode?"HOME TERRITORY":"AWAY OPERATION",18,8,420,28,25);
        Text(top,"Sub",GameManager.Data?.FirmName??"Build your firm",18,37,420,22,15,LandscapeUI.Muted);
        resources=Text(top,"Resources","",740,15,400,40,21);
        Button(top,"Base",CityGameplay.HomeMode?"HOME":"ARRIVAL",440,10,140,48,()=>{if(CityGameplay.HomeMode)OpenHome();else FocusHome();});
        Button(top,"Manage","MANAGE",590,10,140,48,OpenManagement);
        Button(top,"Pause","PAUSE",1400,10,150,48,()=>BattleUIController.instance?.pauseButton?.onClick.Invoke());
        var objective=Panel("Journey",310,94,970,104);
        phase=Text(objective,"Stage","YOUR NEXT STEP",18,10,720,24,15,LandscapeUI.Gold);
        goal=Text(objective,"Goal","",18,37,740,52,21);
        go=Button(objective,"Next","SHOW ME",780,25,170,56,Guide);
        var live=Panel("LiveMatchday",310,206,688,54);
        matchdayStrip=live.gameObject;
        matchdayHeadline=Text(live,"Headline","",16,5,525,42,17,LandscapeUI.Gold);
        Button(live,"View","VIEW",549,4,126,46,()=>StadiumMatchdayControl.Instance?.FocusAndOpen());
        matchdayStrip.SetActive(false);
        selectionCard=Panel("SelectionCoach",315,780,850,96).gameObject;
        selectionText=Text(selectionCard.transform,"Selection","",18,10,398,27,20);
        TutorialAnchor.Tag(Button(selectionCard.transform,"DeselectAll","DESELECT ALL",424,18,206,58,()=>AgentSelectionManager.instance?.DeselectAll()),"firm.deselectAll");
        hint=Text(selectionCard.transform,"Hint","Tap a street to move. Tap crew to add or remove them.",18,42,398,42,17,LandscapeUI.Muted);
        orderButton=TutorialAnchor.Tag(Button(selectionCard.transform,"Orders","ORDERS",640,18,188,58,ToggleOrders),"firm.orders");
        var shortcuts=Panel("QuickOrders",647,727,518,50);
        quickOrders=shortcuts.gameObject;
        Button(shortcuts,"Attack","ATTACK",6,3,160,44,()=>QuickOrder(()=>AgentSelectionManager.instance?.CommandSelectedAttack()));
        Button(shortcuts,"Guard","GUARD",179,3,160,44,()=>QuickOrder(()=>RtsGestureController.Instance?.BeginGuard()));
        Button(shortcuts,"Regroup","REGROUP",352,3,160,44,()=>QuickOrder(()=>AgentSelectionManager.instance?.CommandSelectedRetreat()));
        Button(root,"CrewAccess","CREW / FIND",18,102,190,56,OpenCrew);
        Button(root,"MapDetails","MAP DETAILS",18,170,190,48,()=>{InspectWorld=!InspectWorld;CityGameplay.Instance?.PostEvent(InspectWorld?"Map detail labels enabled. Tap MAP DETAILS again to reduce annotations.":"Focused city view restored.");});
        TutorialAnchor.Tag(top.Find("Base").GetComponent<Button>(),"firm.home");
        TutorialAnchor.Tag(root.Find("CrewAccess").GetComponent<Button>(),"firm.crew");
        BuildOrders();CrewTrackingSystem.Ensure();FirstSessionGuide.Ensure(this,root);Refresh();
    }
    void OnDestroy(){if(Instance==this){Instance=null;InspectWorld=false;}}
    RectTransform Panel(string name,float x,float y,float w,float h)=>LandscapeUI.Panel(name,frame,x,y,w,h,true).rectTransform;
    static TextMeshProUGUI Text(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color? color=null)
        =>LandscapeUI.Text(name,parent,value,x,y,w,h,size,color??LandscapeUI.White,true);
    static Button Button(Transform parent,string name,string caption,float x,float y,float w,float h,Action action)
    {var b=LandscapeUI.Button(name,parent,caption,x,y,w,h,"dark");b.onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();action?.Invoke();});return b;}
    void Update(){if(Time.unscaledTime>=next){next=Time.unscaledTime+.2f;Refresh();}}
    void Refresh()
    {
        var d=GameManager.Data;if(d==null||!goal)return;
        ApplyTutorialVisibility();
        resources.text=$"£{d.Money:N0} · {d.CitySupplies} SUPPLY · HEAT {d.PoliceHeat}/10";
        var step=CampaignMissions.Steps(d).FirstOrDefault(s=>!s.Complete);
        phase.text=CityGameplay.HomeMode?"HOME OBJECTIVE":CampaignMissions.Active(d).title;
        goal.text=!RecoveryProgression.OrientationComplete(d)?RecoveryProgression.OrientationHint(d):
            HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.IsBuilding?HomeTerritorySystem.Instance.Status:
            string.IsNullOrEmpty(step.id)?"Your district is secure. Plan your next operation.":step.title;
        bool matchday=ShowMatchday(d);
        var stadium=StadiumMatchdayActivity.Instance;
        bool liveMatch=CityGameplay.HomeMode&&stadium&&stadium.HasStarted&&stadium.StreetOperations&&
            stadium.StreetOperations.Automatic&&!GuidedCityTutorial.BlocksInput;
        matchdayStrip.SetActive(liveMatch);
        if(liveMatch)matchdayHeadline.text="MATCHDAY · "+stadium.StreetOperations.LiveHeadline;
        bool awayReady=!matchday&&AwayReady(d);
        bool extractionReady=!CityGameplay.HomeMode&&CampaignMissions.ReadyForExtraction(d);
        if(matchday&&MatchdayStrategySystem.Instance){phase.text="MATCHDAY · "+StadiumMatchdayActivity.Instance.CurrentPhase;goal.text=MatchdayStrategySystem.Instance.CurrentPurpose;}
        else if(awayReady)
        {
            phase.text="AWAY READY · "+CampaignMissions.Get(d.CurrentLevel).destination.ToUpperInvariant();
            goal.text=d.MatchdayTravelCredit>0?$"£{d.MatchdayTravelCredit} MATCHDAY TRAVEL CREDIT":
                d.LastAwayStockMission>1?$"{CampaignMissions.Get(d.LastAwayStockMission).destination.ToUpperInvariant()} SUPPLIES DELIVERED TO HOME":
                "CHOOSE TRAVELLERS AND HOME RESERVES";
        }
        else if(extractionReady)goal.text=AwayExtractionSystem.Instance&&AwayExtractionSystem.Instance.IsExtracting
            ?AwayExtractionSystem.Instance.Status:$"EXTRACT SURVIVORS · +{CampaignMissions.AwayStockDelivery} HOME SUPPLY";
        else if(FirstSessionGuide.TryLesson(d,out var lesson)){phase.text=lesson.title;goal.text=lesson.text;}
        go.GetComponentInChildren<TextMeshProUGUI>().text=matchday?"SHOW STADIUM":awayReady?"PLAN AWAY":extractionReady?"EXTRACT":
            FirstSessionGuide.TryLesson(d,out var hintLesson)?hintLesson.button:"SHOW ME";
        goal.fontSize=FirstSessionGuide.TryLesson(d,out _)?18:21;
        var selection=AgentSelectionManager.instance?.SelectedAgents;
        int count=selection?.Count(a=>a&&a.IsAlive)??0;
        quickOrders.SetActive(count>0&&!GuidedCityTutorial.BlocksInput&&!SheetOpen&&!OrdersOpen);
        selectionText.text=count==0?"SELECT YOUR CREW":count==1?selection.First(a=>a&&a.IsAlive).Data.AgentName.ToUpperInvariant()+" SELECTED":count+" CREW SELECTED";
        var gestures=RtsGestureController.Instance;
        hint.text=gestures&&gestures.Mode==RtsGestureController.InputMode.GuardArea?"GUARD: drag the area to protect, or tap a post. Cancel with ORDERS.":
            gestures&&gestures.Mode==RtsGestureController.InputMode.PatrolA?"PATROL: tap the first point of your route.":
            gestures&&gestures.Mode==RtsGestureController.InputMode.PatrolB?"PATROL: tap the second point, then confirm the route.":
            AgentSelectionManager.instance?.PendingCommand==AgentSelectionManager.TargetCommand.Attack?"ATTACK: tap the rival you want this group to engage.":
            AgentSelectionManager.instance?.PendingCommand==AgentSelectionManager.TargetCommand.Scout?"SCOUT: tap the street you want to inspect. Crew earns intel on arrival.":
            count==0?"Tap a green member. CREW / FIND locates anyone. Drag ground to pan.":"Tap a street to move along the coloured route. CREW / FIND focuses or follows a named member.";
        orderButton.interactable=count>0;orderButton.GetComponentInChildren<TextMeshProUGUI>().text=OrdersOpen?"CLOSE ORDERS":"ORDERS";
    }
    public void ApplyTutorialVisibility()
    {
        if(!frame)return;
        bool teaching=GuidedCityTutorial.BlocksInput;
        Visible("Journey",!teaching);
        if(teaching){matchdayStrip.SetActive(false);quickOrders.SetActive(false);}
        Visible("CrewAccess",GuidedCityTutorial.AllowsFeature("crew"));
        Visible("MapDetails",GuidedCityTutorial.AllowsFeature("map"));
        Visible("SelectionCoach",GuidedCityTutorial.AllowsFeature("orders")||GuidedCityTutorial.AllowsFeature("selection"));
        // The existing top buttons retain their real hitboxes during teaching.
        var top=frame.Find("CityTop");
        if(top)
        {
            var image=top.GetComponent<Image>();if(image)image.enabled=!teaching;
            var outline=top.GetComponent<Outline>();if(outline)outline.enabled=!teaching;
            foreach(string name in new[]{"Place","Sub","Pause"})Visible("CityTop/"+name,!teaching);
            Visible("CityTop/Base",GuidedCityTutorial.AllowsFeature("home"));
            Visible("CityTop/Resources",!teaching);
            Visible("CityTop/Manage",!teaching);
        }
        void Visible(string path,bool show){var t=frame.Find(path);if(t&&t.gameObject.activeSelf!=show)t.gameObject.SetActive(show);}
    }
    void BuildOrders()
    {
        var panel=Panel("ContextOrders",650,260,620,518);orders=panel.gameObject;
        Text(panel,"Title","ORDERS FOR SELECTED CREW",20,16,580,38,25);
        var names=new[]{"MOVE · tap a street","ATTACK · choose a rival","GUARD · tap a post or draw an area","PATROL · choose two route points","REGROUP · return this group to HQ","SCOUT · tap a street to gain intel","CAPTURE · clear and hold nearby turf","STOP · cancel this group's current job"};
        Action[] actions={
            ()=>AgentSelectionManager.instance?.ArmMoveCommand(),()=>AgentSelectionManager.instance?.CommandSelectedAttack(),
            ()=>RtsGestureController.Instance?.BeginGuard(),()=>RtsGestureController.Instance?.BeginPatrol(),
            ()=>AgentSelectionManager.instance?.CommandSelectedRetreat(),()=>AgentSelectionManager.instance?.ArmScoutCommand(),
            ()=>CityGameplay.Instance?.CaptureNearest(),()=>AgentSelectionManager.instance?.CommandSelectedStop()};
        for(int i=0;i<names.Length;i++){int index=i;TutorialAnchor.Tag(Button(panel,"Order"+i,names[i],20,66+i*48,580,42,()=>{orders.SetActive(false);RtsGestureController.Instance?.Cancel();actions[index]();Refresh();}),"firm.order"+i);}
        Button(panel,"Cancel","CANCEL TARGETING / CLOSE",20,466,580,42,()=>{orders.SetActive(false);RtsGestureController.Instance?.Cancel();AgentSelectionManager.instance?.ArmMoveCommand();});
        orders.SetActive(false);
    }
    public void ToggleOrders(){if(!orders)return;CloseSheet();orders.SetActive(!orders.activeSelf);Refresh();}
    void QuickOrder(Action issue)
    {
        RtsGestureController.Instance?.Cancel();
        issue();
        Refresh();
    }
    public void CloseSheet(){if(sheet){sheet.SetActive(false);Destroy(sheet);}sheet=null;}
    RectTransform Sheet(string heading)
    {
        orders.SetActive(false);CityUiSheets.Open(this,CloseSheet);CloseSheet();
        var panel=Panel("FocusedSheet",450,210,820,540);sheet=panel.gameObject;
        Text(panel,"Title",heading,22,16,610,38,27);
        Button(panel,"Close","CLOSE",660,14,138,46,CloseSheet);
        body=LandscapeUI.Scroll("Contents",panel,18,78,784,440).content;return body;
    }
    Button Row(string name,string detail,string action,Action clicked,bool enabled=true)
    {
        var row=LandscapeUI.Panel("Row",body,0,0,750,114,true);LandscapeUI.LayoutSize(row.gameObject,750,114);
        Text(row.transform,"Name",name,16,12,510,28,21);
        Text(row.transform,"Detail",detail,16,46,510,54,16,LandscapeUI.Muted);
        var button=Button(row.transform,"Action",action,545,28,188,58,clicked);button.interactable=enabled;return button;
    }
    public void OpenManagement()
    {
        Sheet("YOUR FIRM · CHOOSE A PURPOSE");
        if(CityGameplay.HomeMode)Row("HOME TERRITORY","Build permanent facilities, recruit, train and recover.","ENTER HOME",OpenHome);
        else Row("AWAY FIELD COMMAND","Home remains saved. Complete this district's goals and regroup at ARRIVAL to extract.","JOURNAL",()=>{CloseSheet();legacy.OpenJourney();});
        if(HomeTerritorySystem.Built(GameManager.Data,0)||!CityGameplay.HomeMode)
        {
            Row("BUSINESS & INCOME","Take control of a named shop. Protect trade or choose a risky raid.","BUSINESSES",()=>{CloseSheet();CityBusinessSystem.Instance?.OpenLedger();InspectWorld=true;});
            Row("PREPARE THE DISTRICT","Scout, gather supplies and organise supporters. Assign separate groups.","OPERATIONS",()=>{CloseSheet();CityOperationsSystem.Instance?.OpenBoard();});
            if(CityGameplay.HomeMode)Row("STADIUM","Fans arrive and police manage the streets during Matchday.","SHOW STADIUM",()=>{CloseSheet();StadiumMatchdayControl.Instance?.Focus();});
        }
        Row("CAMPAIGN JOURNEY","See completed goals, current requirements and the next unlock.","JOURNAL",()=>{CloseSheet();legacy.OpenJourney();});
        if(!CityGameplay.HomeMode)Row("EXTRACTION","Regroup every surviving traveller at ARRIVAL once the district is secure.","EXTRACT",()=>{CloseSheet();legacy.OpenTripPlanner();});
        Row("HOME · AWAY · POLICE","Away unlocks after securing Home territory.","GAME MODES",()=>{CloseSheet();SimpleShellLayout.ReturnToModes();});
        Row("CAMERA & MAP","Free pan and zoom. Recenter deliberately; other groups keep working.","CAMERA",()=>{CloseSheet();CityGameplay.Instance?.ToggleCameraSettings();});
        Row("SOUND, PACE & MOTION","Set separate music/effects volume, faster travel and decorative motion. Your campaign stays open.","SETTINGS",()=>{CloseSheet();GameFeedback.OpenSettings();});
        Row("HOW TO PLAY","Selection, groups, building, income and matchday explained in one short guide.","HELP",OpenHelp);
        Row("OPTIONAL STORE","Optional purchases. Every core command, recovery and campaign goal remains playable without buying.","STORE",()=>{CloseSheet();GamePurchasesService.OpenShop();});
        if(GameAdsService.Instance.PrivacyOptionsRequired)Row("AD PRIVACY","Review your advertising consent choices.","PRIVACY",()=>GameAdsService.Instance.ShowPrivacyOptions());
        // Optional advertising placement is integrated by the ads module.
        Row("OPTIONAL SUPPLY SUPPORT",GameAdRewards.Availability,"WATCH AD",()=>{CloseSheet();GameAdRewards.TryOfferSupplyGrant();},GameAdRewards.CanClaim());
    }
    public void AddOptionalRow(string name,string detail,string action,Action clicked,bool enabled=true)=>Row(name,detail,action,clicked,enabled);
    public void OpenHome()
    {
        if(!CityGameplay.HomeMode){FocusHome();return;}
        FocusHome();Refresh();Sheet("HOME TERRITORY · BUILD YOUR BASE");var d=GameManager.Data;
        for(int i=0;i<3;i++)
        {
            int site=i,level=HomeTerritorySystem.Level(d,i);
            string benefit=HomeTerritorySystem.Benefit(d,i);
            TutorialAnchor.Tag(Row(HomeTerritorySystem.Names[i]+$" · LEVEL {level}/3",benefit+(level>=3?"\nMAXIMUM LEVEL":$"\n£{HomeTerritorySystem.Cost(d,i)} + {HomeTerritorySystem.SuppliesCost(d,i)} supplies · {HomeTerritorySystem.WorkDuration(d,i):0}s crew work"),level>=3?"MAX LEVEL":level==0?"BUILD":"UPGRADE",()=>{if(HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.BeginBuild(site))CloseSheet();},level<3&&HomeTerritorySystem.Instance&&!HomeTerritorySystem.Instance.IsBuilding),"firm.build"+i);
        }
        if(HomeTerritorySystem.Built(d,0))
        {
            TutorialAnchor.Tag(Row($"RECRUIT ONE MEMBER · £{HomeTerritorySystem.RecruitCost(d)}","Bring selected crew inside Home to meet one new member. They join immediately; upgraded HQ reduces the price.","RECRUIT ONE",()=>{HomeTerritorySystem.Instance?.RecruitOne();OpenHome();}),"firm.recruit");
            Row("RECOVER & TRAIN","Inspect health, restore fallen members and improve your active roster.","SQUAD",()=>{CloseSheet();SquadBoard.Instance?.ShowWard();});
        }
    }
    public void OpenCrew()
    {
        Sheet("CREW · SELECT, FIND OR FOLLOW");
        foreach(var member in BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive))
        {
            var actor=member;
            var row=LandscapeUI.Panel("CrewRow",body,0,0,750,116,true);LandscapeUI.LayoutSize(row.gameObject,750,116);
            Text(row.transform,"Name",actor.Data.AgentName,16,9,480,28,21,CrewTrackingSystem.MemberColor(actor));
            Text(row.transform,"Journey",$"{actor.CurrentHp:0} HP · "+CrewTrackingSystem.Describe(actor),16,42,380,60,16,LandscapeUI.Muted);
            TutorialAnchor.Tag(Button(row.transform,"Action",actor.IsSelected?"DESELECT":"SELECT",550,9,180,42,()=>{
                var data=GameManager.Data;bool selectionLearned=data.LearnedSelection,splitLearned=data.LearnedSplitSelection;
                AgentSelectionManager.instance.ToggleSelect(actor);
                if(data.RecoveryGuidedStart&&(!selectionLearned&&data.LearnedSelection||!splitLearned&&data.LearnedSplitSelection))CloseSheet();else OpenCrew();}),"firm.member."+actor.Data.AgentId);
            Button(row.transform,"Focus","FOCUS",408,64,155,40,()=>{CloseSheet();CrewTrackingSystem.FocusMember(actor);});
            Button(row.transform,"Follow","FOLLOW",575,64,155,40,()=>{CloseSheet();CrewTrackingSystem.TrackMember(actor);});
        }
        Row("WORLD ROUTES",CrewTrackingSystem.ShowAllRoutes?"Every living member's remaining street route is shown. Individual focus never changes selection.":"Selected and pinned members show their remaining route. Edge names find people outside your view.",CrewTrackingSystem.ShowAllRoutes?"SELECTED ONLY":"SHOW ALL",()=>{CrewTrackingSystem.ShowAllRoutes=!CrewTrackingSystem.ShowAllRoutes;OpenCrew();});
        if(CrewTrackingSystem.TrackedMember)Row("PINNED · "+CrewTrackingSystem.TrackedMember.Data.AgentName,"Drag the camera to look elsewhere. The pin stays available; FOLLOW resumes its camera track.","UNPIN",()=>{CrewTrackingSystem.StopTracking();OpenCrew();});
        Row("SELECT THE WHOLE CREW","Tap selected members again to remove only them.","SELECT ALL",()=>{AgentSelectionManager.instance?.SelectAll();OpenCrew();});
        Row("CLEAR YOUR SELECTION","Members keep their orders when deselected.","DESELECT ALL",()=>{AgentSelectionManager.instance?.DeselectAll();OpenCrew();});
        Row("DRAG A GROUP","Draw a box around crew. Camera panning returns after selection.","BOX SELECT",()=>{CloseSheet();RtsGestureController.Instance?.BeginBoxSelection();});
        for(int i=0;i<3;i++)
        {
            int group=i;Row("GROUP "+(i+1),"Save the current selection or recall it without changing their orders.",savedGroups[i].Count==0?"SAVE GROUP":"RECALL",()=>{
                if(savedGroups[group].Count==0)savedGroups[group].AddRange(AgentSelectionManager.instance.SelectedAgents.Where(a=>a&&a.IsAlive));
                else{AgentSelectionManager.instance.DeselectAll();foreach(var a in savedGroups[group])if(a&&a.IsAlive)AgentSelectionManager.instance.Select(a);}
                CloseSheet();});
        }
    }
    public void Guide()
    {
        var d=GameManager.Data;
        if(d!=null&&ShowMatchday(d)){StadiumMatchdayControl.Instance?.FocusAndOpen();return;}
        if(AwayReady(d)){legacy.OpenTripPlanner();return;}
        if(!CityGameplay.HomeMode&&CampaignMissions.ReadyForExtraction(d)){AwayExtractionSystem.Instance?.RequestExtraction();return;}
        if(FirstSessionGuide.TryLesson(d,out _)){FirstSessionGuide.Instance?.Guide();return;}
        if(!RecoveryProgression.OrientationComplete(d)){OpenCrew();return;}
        if(CityGameplay.HomeMode&&!HomeTerritorySystem.Built(d,0)){OpenHome();return;}
        InspectWorld=true;legacy.GuideCurrentStep();
    }
    public void FocusHome()
    {
        var city=CityGameplay.Instance;if(!city)return;
        var offset=Camera.main&&CityGameplay.HomeMode?Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized*12f:Vector3.zero;
        CameraPanTouchOnly.Instance?.FocusOn(city.Home+offset);
    }
    public void OpenHelp()
    {
        Sheet("QUICK GUIDE · YOUR CITY, YOUR DECISIONS");
        Row("1 · SELECT, MOVE AND FIND","CREW / FIND selects names. FOCUS locates one; FOLLOW keeps that person in view. Coloured lines show actual remaining streets and edge names find offscreen crew. Drag ground to pan; pinch to zoom.","CREW / FIND",OpenCrew);
        Row("2 · GIVE A PURPOSE","ORDERS explains Guard (tap a post or draw an area), Patrol (two points), Attack (tap rival), and Regroup. Commands keep your selection; unselected groups keep working.","ORDERS",()=>{CloseSheet();ToggleOrders();});
        Row("3 · BUILD AND INVEST","Use selected crew at Home plots. Finished facilities and upgrades visibly grow and remain saved. Supplies support businesses and matchday; guards protect income.","HOME",OpenHome);
        Row("4 · EARN, TRADE OR LOOT","Control shops for repeated trade, buy/sell supplies locally, or loot once for cash plus heat and disrupted income. Invest while crew works on site to raise future yield.","BUSINESSES",()=>{CloseSheet();CityBusinessSystem.Instance?.OpenLedger();});
        Row("5 · HOME TO AWAY","Matchday runs while you build Home. Guard the stadium approach to protect fans and earn more Away fare credit.","SHOW STADIUM",()=>{CloseSheet();StadiumMatchdayControl.Instance?.Focus();});
        Row("REPLAY GUIDED TUTORIAL","Paused, focused lessons. Skip any part. Existing buildings, cash and campaign progress stay saved.","REPLAY",()=>{CloseSheet();GuidedCityTutorial.Replay(false);});
    }
}
