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
    RectTransform frame,body;
    GameObject orders,sheet,selectionCard;
    TextMeshProUGUI goal,phase,selectionText,hint,resources,title;
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
        selectionCard=Panel("SelectionCoach",315,780,960,96).gameObject;
        selectionText=Text(selectionCard.transform,"Selection","",18,10,665,27,20);
        hint=Text(selectionCard.transform,"Hint","Tap a street to move. Tap crew to add or remove them.",18,42,680,42,17,LandscapeUI.Muted);
        orderButton=Button(selectionCard.transform,"Orders","ORDERS",730,18,208,58,ToggleOrders);
        Button(root,"CrewAccess","CREW",18,102,190,56,OpenCrew);
        Button(root,"MapDetails","MAP DETAILS",18,170,190,48,()=>{InspectWorld=!InspectWorld;CityGameplay.Instance?.PostEvent(InspectWorld?"Map detail labels enabled. Tap MAP DETAILS again to reduce annotations.":"Focused city view restored.");});
        BuildOrders();FirstSessionGuide.Ensure(this,root);Refresh();
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
        resources.text=$"£{d.Money:N0} · {d.CitySupplies} SUPPLY · HEAT {d.PoliceHeat}/10";
        var step=CampaignMissions.Steps(d).FirstOrDefault(s=>!s.Complete);
        var progress=CampaignMissions.Progress(d);
        phase.text=CityGameplay.HomeMode?$"BUILD YOUR HOME · {progress.complete}/{progress.total} MILESTONES":CampaignMissions.Active(d).title;
        goal.text=!RecoveryProgression.OrientationComplete(d)?RecoveryProgression.OrientationHint(d):
            HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.IsBuilding?HomeTerritorySystem.Instance.Status:
            string.IsNullOrEmpty(step.id)?"Your district is secure. Plan your next operation.":step.title;
        bool matchday=StadiumMatchdayActivity.Instance&&StadiumMatchdayActivity.Instance.HasStarted&&CityGameplay.HomeMode;
        if(matchday&&MatchdayStrategySystem.Instance){phase.text="MATCHDAY · "+StadiumMatchdayActivity.Instance.CurrentPhase;goal.text=MatchdayStrategySystem.Instance.CurrentPurpose;}
        else if(FirstSessionGuide.TryLesson(d,out var lesson)){phase.text=lesson.title;goal.text=lesson.text;}
        go.GetComponentInChildren<TextMeshProUGUI>().text=matchday?"COMMAND":FirstSessionGuide.TryLesson(d,out var hintLesson)?hintLesson.button:"SHOW ME";
        goal.fontSize=FirstSessionGuide.TryLesson(d,out _)?18:21;
        var selection=AgentSelectionManager.instance?.SelectedAgents;
        int count=selection?.Count(a=>a&&a.IsAlive)??0;
        selectionText.text=count==0?"SELECT YOUR CREW":count==1?selection.First(a=>a&&a.IsAlive).Data.AgentName.ToUpperInvariant()+" SELECTED":count+" CREW SELECTED";
        var gestures=RtsGestureController.Instance;
        hint.text=gestures&&gestures.Mode==RtsGestureController.InputMode.GuardArea?"GUARD: drag the area to protect, or tap a post. Cancel with ORDERS.":
            gestures&&gestures.Mode==RtsGestureController.InputMode.PatrolA?"PATROL: tap the first point of your route.":
            gestures&&gestures.Mode==RtsGestureController.InputMode.PatrolB?"PATROL: tap the second point, then confirm the route.":
            AgentSelectionManager.instance?.PendingCommand==AgentSelectionManager.TargetCommand.Attack?"ATTACK: tap the rival you want this group to engage.":
            AgentSelectionManager.instance?.PendingCommand==AgentSelectionManager.TargetCommand.Scout?"SCOUT: tap the street you want to inspect. Crew earns intel on arrival.":
            count==0?"Tap a green crew member. Drag empty ground to move the camera.":"Tap a street to move. Tap crew to add or remove them. Hold ground for local orders.";
        orderButton.interactable=count>0;orderButton.GetComponentInChildren<TextMeshProUGUI>().text=OrdersOpen?"CLOSE ORDERS":"ORDERS";
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
        for(int i=0;i<names.Length;i++){int index=i;Button(panel,"Order"+i,names[i],20,66+i*48,580,42,()=>{orders.SetActive(false);RtsGestureController.Instance?.Cancel();actions[index]();Refresh();});}
        Button(panel,"Cancel","CANCEL TARGETING / CLOSE",20,466,580,42,()=>{orders.SetActive(false);RtsGestureController.Instance?.Cancel();AgentSelectionManager.instance?.ArmMoveCommand();});
        orders.SetActive(false);
    }
    public void ToggleOrders(){if(!orders)return;CloseSheet();orders.SetActive(!orders.activeSelf);Refresh();}
    public void CloseSheet(){if(sheet)Destroy(sheet);sheet=null;}
    RectTransform Sheet(string heading)
    {
        orders.SetActive(false);CityUiSheets.Open(this,CloseSheet);CloseSheet();
        var panel=Panel("FocusedSheet",450,210,820,540);sheet=panel.gameObject;
        Text(panel,"Title",heading,22,16,610,38,27);
        Button(panel,"Close","CLOSE",660,14,138,46,CloseSheet);
        body=LandscapeUI.Scroll("Contents",panel,18,78,784,440).content;return body;
    }
    void Row(string name,string detail,string action,Action clicked,bool enabled=true)
    {
        var row=LandscapeUI.Panel("Row",body,0,0,750,114,true);LandscapeUI.LayoutSize(row.gameObject,750,114);
        Text(row.transform,"Name",name,16,12,510,28,21);
        Text(row.transform,"Detail",detail,16,46,510,54,16,LandscapeUI.Muted);
        var button=Button(row.transform,"Action",action,545,28,188,58,clicked);button.interactable=enabled;
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
            Row("MATCHDAY","Prepare first, then cover simultaneous situations with real crew.","MATCHDAY",()=>{CloseSheet();StadiumMatchdayActivity.Instance?.OpenBriefing();});
        }
        Row("CAMPAIGN JOURNEY","See completed goals, current requirements and the next unlock.","JOURNAL",()=>{CloseSheet();legacy.OpenJourney();});
        Row(CityGameplay.HomeMode?"AWAY OPERATIONS":"EXTRACTION",CityGameplay.HomeMode?"Unlock Riverside after securing Home. Select travellers and reserves.":"Regroup every surviving traveller at ARRIVAL once the district is secure.",CityGameplay.HomeMode?"PLAN TRIP":"EXTRACT",()=>{CloseSheet();legacy.OpenTripPlanner();});
        Row("CAMERA & MAP","Free pan and zoom. Recenter deliberately; other groups keep working.","CAMERA",()=>{CloseSheet();CityGameplay.Instance?.ToggleCameraSettings();});
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
            Row(HomeTerritorySystem.Names[i]+$" · LEVEL {level}/3",benefit+(level>=3?"\nMAXIMUM LEVEL":$"\n£{HomeTerritorySystem.Cost(d,i)} + {HomeTerritorySystem.SuppliesCost(d,i)} supplies · {HomeTerritorySystem.WorkDuration(d,i):0}s crew work"),level>=3?"MAX LEVEL":level==0?"BUILD":"UPGRADE",()=>{if(HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.BeginBuild(site))CloseSheet();},level<3&&HomeTerritorySystem.Instance&&!HomeTerritorySystem.Instance.IsBuilding);
        }
        if(HomeTerritorySystem.Built(d,0))
        {
            Row($"RECRUIT ONE MEMBER · £{HomeTerritorySystem.RecruitCost(d)}","Bring selected crew inside Home to meet one new member. They join immediately; upgraded HQ reduces the price.","RECRUIT ONE",()=>{HomeTerritorySystem.Instance?.RecruitOne();OpenHome();});
            Row("RECOVER & TRAIN","Inspect health, restore fallen members and improve your active roster.","SQUAD",()=>{CloseSheet();SquadBoard.Instance?.ShowWard();});
        }
    }
    public void OpenCrew()
    {
        Sheet("CREW · TAP TO CHANGE SELECTION");
        foreach(var member in BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive))
        {
            var actor=member;Row(actor.Data.AgentName,$"{actor.CurrentHp:0} HP · {actor.CurrentOrder} · {actor.CurrentState}",actor.IsSelected?"DESELECT":"SELECT",()=>{
                var data=GameManager.Data;bool selectionLearned=data.LearnedSelection,splitLearned=data.LearnedSplitSelection;
                AgentSelectionManager.instance.ToggleSelect(actor);
                if(data.RecoveryGuidedStart&&(!selectionLearned&&data.LearnedSelection||!splitLearned&&data.LearnedSplitSelection))CloseSheet();else OpenCrew();});
        }
        Row("SELECT THE WHOLE CREW","Tap selected members again to remove only them.","SELECT ALL",()=>{AgentSelectionManager.instance?.SelectAll();OpenCrew();});
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
        if(StadiumMatchdayActivity.Instance&&StadiumMatchdayActivity.Instance.HasStarted){MatchdayStrategySystem.Instance?.OpenBoard();return;}
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
        Row("1 · SELECT AND MOVE","Press CREW to select names. Tap a street to move that selection. Tap another name to add/remove only that person. Drag ground to pan; pinch to zoom.","CREW",OpenCrew);
        Row("2 · GIVE A PURPOSE","ORDERS explains Guard (tap a post or draw an area), Patrol (two points), Attack (tap rival), and Regroup. Commands keep your selection; unselected groups keep working.","ORDERS",()=>{CloseSheet();ToggleOrders();});
        Row("3 · BUILD AND INVEST","Use selected crew at Home plots. Finished facilities and upgrades visibly grow and remain saved. Supplies support businesses and matchday; guards protect income.","HOME",OpenHome);
        Row("4 · EARN, TRADE OR LOOT","Control shops for repeated trade, buy/sell supplies locally, or loot once for cash plus heat and disrupted income. Invest while crew works on site to raise future yield.","BUSINESSES",()=>{CloseSheet();CityBusinessSystem.Instance?.OpenLedger();});
        Row("5 · DIRECT THE MATCHDAY","Prepare routes, position groups, choose an approach, then START. Two locations need coverage while a spare crew may escort supporters or staff a built stall.","MATCHDAY",()=>{CloseSheet();MatchdayStrategySystem.Instance?.OpenBoard();});
        Row("TUTORIAL COACH",GameManager.Data.FirstSessionCoachHidden?"The coach is hidden. Show it again; existing learning stays saved.":"The short coach follows actual actions. It never completes a lesson just from opening a menu.",GameManager.Data.FirstSessionCoachHidden?"SHOW COACH":"HIDE COACH",()=>{GameManager.Data.FirstSessionCoachHidden=!GameManager.Data.FirstSessionCoachHidden;GameManager.Save();OpenHelp();});
    }
}
