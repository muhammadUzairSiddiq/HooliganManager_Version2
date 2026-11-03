using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

/// <summary>Saved resource trade and investment rules; opening a shop never pays.</summary>
public static class CityCommerceRules
{
    public const int BuySupplyPrice=120, SellSupplyPrice=70, SupplyCapacity=99;
    public static int InvestmentCost(CityBusinessState state)=>(state?.investmentLevel??0)==0?350:650;
    public static int InvestmentSupplies(CityBusinessState state)=>(state?.investmentLevel??0)==0?2:3;
    public static float InvestmentSeconds(CityBusinessState state)=>(state?.investmentLevel??0)==0?20:30;

    public static bool CanTrade(PlayerData d,string id,bool buying,out string reason)
    {
        var state=CityBusinessLedger.Find(d,id);
        if(d==null||state==null){reason="Venue unavailable.";return false;}
        if(!state.controlled){reason="Control this venue before buying, selling or investing.";return false;}
        if(state.disruptionSeconds>0||state.rivalPressureSeconds>0){reason="Trade is suspended while the venue is disrupted or contested.";return false;}
        if(buying&&d.Money<BuySupplyPrice){reason="One supply costs £"+BuySupplyPrice+".";return false;}
        if(buying&&d.CitySupplies>=SupplyCapacity){reason="Supply storage is full (99).";return false;}
        if(!buying&&d.CitySupplies<1){reason="You have no supplies to sell. Keep reserves for construction and matchday.";return false;}
        reason="";return true;
    }
    public static bool Trade(PlayerData d,string id,bool buying)
    {
        if(!CanTrade(d,id,buying,out _))return false;
        if(buying){d.Money-=BuySupplyPrice;d.CitySupplies++;}
        else{d.Money+=SellSupplyPrice;d.CitySupplies--;}
        return true;
    }
    public static bool CanInvest(PlayerData d,string id,out string reason)
    {
        var state=CityBusinessLedger.Find(d,id);
        if(d==null||state==null){reason="Venue unavailable.";return false;}
        if(!state.controlled){reason="Secure control of this venue before investing.";return false;}
        if(state.investmentLevel>=2){reason="This venue is fully developed.";return false;}
        if(state.disruptionSeconds>0||state.rivalPressureSeconds>0){reason="Clear pressure and restore trading before investing.";return false;}
        if(d.Money<InvestmentCost(state)||d.CitySupplies<InvestmentSupplies(state))
        {reason=$"Needs £{InvestmentCost(state)} and {InvestmentSupplies(state)} supplies. Payment is taken after crew finish work.";return false;}
        reason="";return true;
    }
    public static bool Invest(PlayerData d,string id)
    {
        if(!CanInvest(d,id,out _))return false;
        var state=CityBusinessLedger.Find(d,id);
        d.Money-=InvestmentCost(state);d.CitySupplies-=InvestmentSupplies(state);state.investmentLevel++;
        return true;
    }
}

/// <summary>One readable venue card. Trading needs selected people at the shop.</summary>
public sealed class CityCommerceSystem:MonoBehaviour
{
    public static CityCommerceSystem Instance{get;private set;}
    public bool IsOpen=>panel&&panel.activeInHierarchy;
    public CityBusiness ActiveVenue=>active;
    CityBusinessSystem owner;
    CityBusiness active;
    GameObject panel;
    TextMeshProUGUI summary,receipt;
    Button control,loot,buy,sell,invest,travel;
    float refresh;
    public static CityCommerceSystem Ensure(CityBusinessSystem business)
    {
        if(!business)return null;
        var commerce=business.GetComponent<CityCommerceSystem>()??business.gameObject.AddComponent<CityCommerceSystem>();
        commerce.owner=business;return commerce;
    }
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public void Open(CityBusiness venue)
    {
        if(!venue)return;active=venue;if(!panel)Build();
        CityUiSheets.Open(this,Close);panel.SetActive(true);receipt.text="Select crew and tap TRAVEL HERE. Actions need your people at this venue.";
        Refresh();
    }
    public void Close(){if(panel)panel.SetActive(false);}
    static bool HasCrewAt(CityBusiness venue)
        => venue&&AgentSelectionManager.instance?.SelectedAgents.Any(a=>a&&a.IsAlive&&!a.IsActivityLocked&&!a.IsOnLiveJob&&a.CurrentState!=AgentController.State.AutoAttacking&&
            CityBusinessSystem.HorizontalDistance(a.transform.position,venue.transform.position)<=8)==true;
    public bool TryTrade(CityBusiness venue,bool buying)
    {
        if(!HasCrewAt(venue)){Message("Bring a selected available member within 8 metres of the shop entrance.");return false;}
        if(CityBusinessSystem.HostilesNear(venue.transform.position,12)){Message("Trade blocked by hostile pressure. Protect the venue first.");return false;}
        if(!CityCommerceRules.CanTrade(GameManager.Data,venue.Definition.id,buying,out string reason)){Message(reason);return false;}
        if(!CityCommerceRules.Trade(GameManager.Data,venue.Definition.id,buying))return false;
        GameManager.Save();Message(buying?$"Bought 1 supply for £{CityCommerceRules.BuySupplyPrice}. Saved.":$"Sold 1 supply for £{CityCommerceRules.SellSupplyPrice}. Saved. Keep enough for matchday and development.");Refresh();return true;
    }
    public bool TravelTo(CityBusiness venue)
    {
        if(!venue)return false;var selection=AgentSelectionManager.instance;
        if(selection==null||selection.SelectedAgents.Count==0){Message("Select a crew member first.");return false;}
        selection.CommandSelectedMoveTo(venue.transform.position);
        if(selection.LastAcceptedOrderCount<=0){Message("No selected member can reach this venue.");return false;}
        Close();CameraPanTouchOnly.Instance?.FocusOn(venue.transform.position);return true;
    }
    public static bool TryHandleTap(Vector2 screenPoint)
    {
        if(!Instance||!Instance.enabled||!Camera.main||PoliceCityMode.Active)return false;
        var ray=Camera.main.ScreenPointToRay(screenPoint);CityBusiness closest=null;float distance=float.PositiveInfinity;
        foreach(var venue in Instance.owner.Sites)
        {
            if(!venue.Storefront)continue;
            foreach(var renderer in venue.Storefront.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer||!renderer.enabled||renderer.forceRenderingOff)continue;
                if(renderer.bounds.IntersectRay(ray,out float hit)&&hit<distance){distance=hit;closest=venue;}
            }
        }
        if(!closest)return false;
        if(Physics.Raycast(ray,out var obstruction,2000,~0,QueryTriggerInteraction.Ignore)&&obstruction.distance+.5f<distance&&
            !obstruction.transform.IsChildOf(closest.Storefront)&&obstruction.transform!=closest.Storefront)return false;
        closest.OpenInteraction();return true;
    }
    string TravelEstimate(CityBusiness venue)
    {
        var member=AgentSelectionManager.instance?.SelectedAgents.FirstOrDefault(a=>a&&a.IsAlive);
        if(!member)return "SELECT CREW FOR ROUTE / TRAVEL TIME";
        var nav=member.GetComponent<NavMeshAgent>();var path=new NavMeshPath();
        if(!nav||!nav.isOnNavMesh||!nav.CalculatePath(venue.transform.position,path)||path.status!=NavMeshPathStatus.PathComplete)return "Route must be checked from this crew member";
        float distance=0;for(int i=1;i<path.corners.Length;i++)distance+=Vector3.Distance(path.corners[i-1],path.corners[i]);
        float speed=Mathf.Max(.5f,member.Data.Speed*(CrewMovementPreference.RunOnTap?1:.52f));
        return $"ROUTE {distance:0}m · ABOUT {Mathf.CeilToInt(distance/speed)}s AT CURRENT SPEED";
    }
    void Message(string value){if(receipt)receipt.text=value;CityGameplay.Instance?.PostEvent(value);}
    void Update(){if(IsOpen&&Time.unscaledTime>=refresh){refresh=Time.unscaledTime+.5f;Refresh();}}
    Button Action(string name,string label,float x,float y,float width,System.Action clicked)
    {
        var button=LandscapeUI.Button(name,panel.transform,label,x,y,width,58,"dark");
        button.onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();clicked();});return button;
    }
    void Build()
    {
        var frame=WorldButtonLayer.MissionFrame();
        panel=LandscapeUI.Panel("VenueActions",frame,710,196,866,580,true).gameObject;
        LandscapeUI.Text("Title",panel.transform,"VENUE · TRADE & INVEST",22,16,640,40,27,LandscapeUI.White,true);
        Action("Close","CLOSE",690,12,154,Close);
        summary=LandscapeUI.Text("Summary",panel.transform,"",22,74,820,108,20,LandscapeUI.White,true);
        travel=Action("Travel","TRAVEL HERE",22,194,252,()=>TravelTo(active));
        control=Action("Control","CONTROL",286,194,264,()=>{
            var state=CityBusinessLedger.Find(GameManager.Data,active.Definition.id);
            if(state.controlled){AgentSelectionManager.instance?.ArmGuardCommand();AgentSelectionManager.instance?.CommandSelectedGround(active.transform.position);Close();}
            else if(owner.TryAssign(active,false))Close();});
        loot=Action("Loot","LOOT",562,194,282,()=>{if(owner.TryAssign(active,true))Close();});
        buy=Action("Buy","BUY 1 SUPPLY · £120",22,266,400,()=>TryTrade(active,true));
        sell=Action("Sell","SELL 1 SUPPLY · +£70",438,266,406,()=>TryTrade(active,false));
        invest=Action("Invest","INVEST IN VENUE",22,338,822,()=>{if(owner.TryInvest(active))Close();});
        LandscapeUI.Text("Tradeoffs",panel.transform,"Supplies fund construction and matchday. Investing adds visible trade space and +35% income per level. Loot pays now but raises heat and stops trade for 3 minutes.",22,412,820,66,18,LandscapeUI.Muted);
        receipt=LandscapeUI.Text("Receipt",panel.transform,"",22,490,820,70,18,LandscapeUI.Gold);
        panel.SetActive(false);
    }
    void Refresh()
    {
        if(!active||!summary)return;var d=GameManager.Data;var state=CityBusinessLedger.Find(d,active.Definition.id);if(state==null)return;
        summary.text=active.Definition.name+$" · LEVEL {state.investmentLevel+1}\n"+
            (state.controlled?"YOUR CONTROL":"UNCONTROLLED")+$" · £{CityBusinessLedger.Yield(d,active.Definition,StadiumMatchdayActivity.IsMatchdayActive)} / 90s · SUPPLIES {d.CitySupplies}/99\n"+
            (state.rivalPressureSeconds>0?"CONTESTED - send defenders":state.disruptionSeconds>0?$"TRADING PAUSED · {state.disruptionSeconds:0}s":HasCrewAt(active)?"SELECTED CREW AT VENUE · ready to trade":TravelEstimate(active));
        control.GetComponentInChildren<TextMeshProUGUI>().text=state.controlled?"GUARD THIS VENUE":$"CONTROL · {active.Definition.controlSeconds:0}s";
        loot.GetComponentInChildren<TextMeshProUGUI>().text=$"LOOT +£{active.Definition.loot} · HEAT +{active.Definition.heat}";
        bool free=!owner.HasAssignment(active);
        control.interactable=free;loot.interactable=free&&CityBusinessLedger.CanLoot(d,active.Definition.id,out _);
        buy.interactable=CityCommerceRules.CanTrade(d,active.Definition.id,true,out _);
        sell.interactable=CityCommerceRules.CanTrade(d,active.Definition.id,false,out _);
        invest.GetComponentInChildren<TextMeshProUGUI>().text=state.investmentLevel>=2?"FULLY DEVELOPED":$"IMPROVE TO LEVEL {state.investmentLevel+2} · £{CityCommerceRules.InvestmentCost(state)} + {CityCommerceRules.InvestmentSupplies(state)} SUPPLIES · {CityCommerceRules.InvestmentSeconds(state):0}s";
        invest.interactable=free&&CityCommerceRules.CanInvest(d,active.Definition.id,out _);
    }
}
