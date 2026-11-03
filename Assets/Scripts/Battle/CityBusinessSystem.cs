using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class CityBusinessState
{
    public string id;
    public bool controlled;
    public float disruptionSeconds;
    public float lootCooldownSeconds;
    public float rivalPressureSeconds;
    public float accruedIncome;
    public int investmentLevel;
}

/// <summary>Idempotent, saved economy rules. UI and scene reloads never award income.</summary>
public static class CityBusinessLedger
{
    public const float IncomeInterval = 90f;
    public const float LootCooldown = 300f;
    public const float LootDisruption = 180f;
    public const float TakeoverSeconds = 25f;

    public readonly struct Definition
    {
        public readonly string id, name;
        public readonly int income, loot, heat;
        public readonly float controlSeconds;
        public Definition(string id, string name, int income, int loot, int heat, float seconds)
        { this.id=id; this.name=name; this.income=income; this.loot=loot; this.heat=heat; controlSeconds=seconds; }
    }

    public static readonly Definition[] Definitions = {
        new Definition("burger", "BURGER TAKEAWAY", 90, 250, 2, 12),
        new Definition("bakery", "CORNER BAKERY", 100, 280, 2, 14),
        new Definition("pizza", "MATCHDAY PIZZA", 120, 350, 3, 16),
        new Definition("pub", "SUPPORTERS PUB", 150, 420, 3, 20),
        new Definition("cafe", "STREET CAFE", 90, 240, 2, 12),
        new Definition("shop", "CONVENIENCE SHOP", 110, 320, 2, 16)
    };
    static readonly string[] AwayTypes={"burger","pizza","shop"};
    public static bool IsHomeId(string id)=>!string.IsNullOrEmpty(id)&&Definitions.Any(d=>d.id==id);
    public static string BaseId(string id)=>string.IsNullOrEmpty(id)?"":id.Substring(id.LastIndexOf(':')+1);
    public static Definition[] DistrictDefinitions(int mission)
    {
        if(mission<=1)return Definitions;
        if(mission>CampaignMissions.MissionCount)return Array.Empty<Definition>();
        string district=CampaignMissions.Get(mission).destination.ToUpperInvariant();
        return AwayTypes.Select(type=>{
            var source=Definitions.First(d=>d.id==type);
            return new Definition($"M{mission}:{type}",district+" · "+source.name,Mathf.RoundToInt(source.income*.8f),source.loot+(mission-1)*25,source.heat,source.controlSeconds+2);
        }).ToArray();
    }
    public static bool TryResolveDefinition(string id,out Definition definition)
    {
        definition=Definitions.FirstOrDefault(d=>d.id==id);if(!string.IsNullOrEmpty(definition.id))return true;
        if(string.IsNullOrEmpty(id)||id.Length<5||id[0]!='M')return false;
        int separator=id.IndexOf(':');if(separator<2||!int.TryParse(id.Substring(1,separator-1),out int mission)||mission<2||mission>CampaignMissions.MissionCount)return false;
        definition=DistrictDefinitions(mission).FirstOrDefault(d=>d.id==id);return !string.IsNullOrEmpty(definition.id);
    }
    public static void EnsureDistrict(PlayerData d,int mission)
    {
        Ensure(d);if(d==null||mission<2||mission>CampaignMissions.MissionCount)return;
        foreach(var definition in DistrictDefinitions(mission))if(!d.CityBusinesses.Any(b=>b!=null&&b.id==definition.id))d.CityBusinesses.Add(new CityBusinessState{id=definition.id});
    }

    public static void Ensure(PlayerData d)
    {
        if (d == null) return;
        d.CityBusinesses ??= new List<CityBusinessState>();
        if (float.IsNaN(d.BusinessEconomySeconds) || float.IsInfinity(d.BusinessEconomySeconds) || d.BusinessEconomySeconds<0 || d.BusinessEconomySeconds>=IncomeInterval)
            d.BusinessEconomySeconds=0;
        foreach (var definition in Definitions)
            if (!d.CityBusinesses.Any(b => b != null && b.id == definition.id))
                d.CityBusinesses.Add(new CityBusinessState { id=definition.id });
        foreach(var state in d.CityBusinesses)if(state!=null)state.investmentLevel=Mathf.Clamp(state.investmentLevel,0,2);
    }

    public static CityBusinessState Find(PlayerData d, string id)
    { Ensure(d); return d?.CityBusinesses?.Find(b => b != null && b.id == id); }

    public static int Yield(Definition definition, bool matchday)
        => definition.income * (matchday && (BaseId(definition.id) == "pizza" || BaseId(definition.id) == "pub") ? 2 : 1);

    public static int Yield(PlayerData d, Definition definition, bool matchday)
        => Mathf.RoundToInt(Yield(definition,matchday)*(1f+.35f*Mathf.Clamp(Find(d,definition.id)?.investmentLevel??0,0,2)));

    public static bool Control(PlayerData d, string id)
    {
        var site=Find(d,id);
        if (site == null || site.controlled) return false;
        site.controlled=true; site.rivalPressureSeconds=0;
        return true;
    }

    public static bool CanLoot(PlayerData d, string id, out string reason)
    {
        var site=Find(d,id);
        if (site == null) { reason="Business unavailable."; return false; }
        if (site.lootCooldownSeconds > 0) { reason=$"Loot cooldown: {Mathf.CeilToInt(site.lootCooldownSeconds)} seconds."; return false; }
        reason="Timed raid: police heat rises and trade stops for three minutes.";
        return true;
    }

    public static bool Loot(PlayerData d, string id)
    {
        if (!CanLoot(d,id,out _)) return false;
        if(!TryResolveDefinition(id,out var definition))return false;
        var site=Find(d,id);
        site.lootCooldownSeconds=LootCooldown;
        site.disruptionSeconds=LootDisruption;
        d.Money+=definition.loot;
        d.PoliceHeat=Mathf.Clamp(d.PoliceHeat+definition.heat,0,10);
        return true;
    }

    public static int Tick(PlayerData d, float seconds, bool matchday,int activeAwayMission=0)
    {
        if (d == null || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return 0;
        Ensure(d);
        if(activeAwayMission>1)EnsureDistrict(d,activeAwayMission);
        var active=activeAwayMission>1?Definitions.Concat(DistrictDefinitions(activeAwayMission)).ToArray():Definitions;
        // Pay only time during which a site was actually trading. A disruption
        // expiring on this update cannot retrospectively earn the whole interval.
        int total=0;
        while (seconds > 0)
        {
            float slice=Mathf.Min(seconds,IncomeInterval-d.BusinessEconomySeconds);
            foreach (var definition in active)
            {
                var site=d.CityBusinesses.Find(x=>x!=null&&x.id==definition.id);
                float tradingSeconds=site.controlled&&site.rivalPressureSeconds<=0?Mathf.Max(0,slice-site.disruptionSeconds):0;
                site.accruedIncome+=Yield(d,definition,matchday&&IsHomeId(definition.id))*(tradingSeconds/IncomeInterval);
                site.disruptionSeconds=Mathf.Max(0,site.disruptionSeconds-slice);
                site.lootCooldownSeconds=Mathf.Max(0,site.lootCooldownSeconds-slice);
            }
            d.BusinessEconomySeconds+=slice; seconds-=slice;
            if (d.BusinessEconomySeconds < IncomeInterval-.001f) continue;
            d.BusinessEconomySeconds=0;
            int income=0;
            foreach (var site in d.CityBusinesses)
            {
                if(site==null||!TryResolveDefinition(site.id,out _))continue;
                int earned=Mathf.FloorToInt(site.accruedIncome+.001f);
                site.accruedIncome=Mathf.Max(0,site.accruedIncome-earned);
                income+=earned;
            }
            d.Money+=income; d.BusinessIncomeEarned+=income; total+=income;
        }
        return total;
    }
}

/// <summary>World businesses: real crew travel, contested presence, saved income and optional loot.</summary>
public sealed class CityBusinessSystem : MonoBehaviour
{
    public static CityBusinessSystem Instance { get; private set; }
    public IReadOnlyList<CityBusiness> Sites => sites;
    public bool IsOpen => ledger && ledger.activeInHierarchy;
    readonly List<CityBusiness> sites=new List<CityBusiness>();
    readonly Dictionary<CityBusiness,Assignment> work=new Dictionary<CityBusiness,Assignment>();
    readonly List<CityBusiness> finished=new List<CityBusiness>();
    GameObject ledger;
    RectTransform rows;
    float tick,refresh,saveClock;

    sealed class Assignment
    {
        public AgentController[] crew;
        public int[] revisions;
        public bool loot;
        public bool invest;
        public float elapsed,travel;
    }

    public static CityBusinessSystem Ensure(CityGameplay city)
    {
        var system=city.GetComponent<CityBusinessSystem>()??city.gameObject.AddComponent<CityBusinessSystem>();
        if (system.sites.Count==0) system.BuildSites(city);
        CityCommerceSystem.Ensure(system);
        return system;
    }
    void Awake() { Instance=this; CityBusinessLedger.Ensure(GameManager.Data); }
    void OnDestroy() { if (Instance==this) Instance=null; }

    void BuildSites(CityGameplay city)
    {
        if (city.Locations==null || city.Locations.Length<3) return;
        int mission=CityGameplay.HomeMode?1:CampaignMissions.ContextMission(GameManager.Data);
        CityBusinessLedger.EnsureDistrict(GameManager.Data,mission);
        var offsets=new[] {new Vector3(0,0,12),new Vector3(18,0,12),new Vector3(-18,0,12),
            new Vector3(0,0,12),new Vector3(22,0,-14),new Vector3(-22,0,-14)};
        string[][] hints={new[]{"burger_shop_001","burger_shop"},new[]{"bakery","baker_shop","ice_cream_shop_001"},new[]{"pizzeria","pizza_shop","pizza"},
            new[]{"pub_bar_area","pub_bar","bar_001"},new[]{"coffee_shop_001","coffee_shop","cafe"},new[]{"supermarket_001","supermarket_002","mall_001"}};
        foreach(var definition in CityBusinessLedger.DistrictDefinitions(mission))
        {
            int i=Array.FindIndex(CityBusinessLedger.Definitions,d=>d.id==CityBusinessLedger.BaseId(definition.id));
            Vector3 fallback=CityBusinessLedger.BaseId(definition.id)=="pub"?city.Locations[1]:city.Locations[2];
            var landmark=NearestLandmark(definition.name,hints[i],city.Home,fallback);
            Vector3 origin=landmark.Point;
            Vector3 offset=landmark.Building?Vector3.zero:offsets[i];
            Vector3 point=CityGameplay.ReachableApproach(city.Home,origin+offset);
            // A fallback sample can cluster venues; require a distinct approach.
            for (int attempt=0;attempt<10 && sites.Any(s=>Vector3.Distance(s.transform.position,point)<9f);attempt++)
                point=CityGameplay.ReachableApproach(city.Home,origin+offset+new Vector3((attempt+1)*9f,0,-(attempt+1)*8f));
            var root=new GameObject(definition.name);
            root.transform.SetParent(transform,false); root.transform.position=point;
            var site=root.AddComponent<CityBusiness>(); site.Configure(this,definition); site.Storefront=landmark.Building; sites.Add(site);
        }
    }

    CityLandmarks.Spot NearestLandmark(string label,string[] hints,Vector3 home,Vector3 fallback)
    {
        // Existing building instances are authoritative. The older locator
        // chose the largest/first named model anywhere in the city, which could
        // send the first income lesson needlessly across several districts.
        var candidates=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t=>t&&!t.GetComponentInParent<Canvas>()&&hints.Any(h=>t.name.IndexOf(h,StringComparison.OrdinalIgnoreCase)>=0)&&
                t.GetComponentInChildren<MeshRenderer>()&&!sites.Any(s=>s.Storefront&&(t==s.Storefront||t.IsChildOf(s.Storefront)||s.Storefront.IsChildOf(t))))
            .OrderBy(t=>HorizontalDistance(t.position,home)).Take(16);
        foreach(var building in candidates)
        {
            Vector3 approach=CityLandmarks.OpenApproach(building,building.position);
            Vector3 connected=CityGameplay.ReachableApproach(home,approach);
            var renderers=building.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            if(HorizontalDistance(connected,bounds.center)>Mathf.Max(bounds.size.x,bounds.size.z)+35f)continue;
            return new CityLandmarks.Spot{PlaceName=label,Point=connected,Building=building};
        }
        return CityLandmarks.Locate(label,hints,fallback);
    }

    public bool HasAssignment(CityBusiness site)=>site&&work.ContainsKey(site);
    public float AssignmentSeconds(CityBusiness site)=>site&&work.TryGetValue(site,out var assignment)?assignment.elapsed:0;

    public bool TryAssign(CityBusiness site, bool loot)
        => BeginAssignment(site,loot,false);

    public bool TryInvest(CityBusiness site) => BeginAssignment(site,false,true);

    bool BeginAssignment(CityBusiness site,bool loot,bool invest)
    {
        var d=GameManager.Data;
        if (!site || d==null) return false;
        if (work.ContainsKey(site)) { Report("This business already has a crew assignment."); return false; }
        var state=CityBusinessLedger.Find(d,site.Definition.id);
        if(invest&&!CityCommerceRules.CanInvest(d,site.Definition.id,out string investmentReason)){Report(investmentReason);return false;}
        if (!loot && !invest && state.controlled) { Report("Business already controlled. Leave a GUARD to protect its income."); return false; }
        if (loot && !CityBusinessLedger.CanLoot(d,site.Definition.id,out string reason)) { Report(reason); return false; }
        var selected=AgentSelectionManager.instance?.SelectedAgents;
        if (selected==null) { Report("Select available crew first."); return false; }
        var accepted=new List<AgentController>();
        foreach (var member in selected)
        {
            if (!member || !member.IsAlive || member.IsActivityLocked) continue;
            float radius=Mathf.Min(3.5f,Mathf.Sqrt(accepted.Count)*.8f),angle=accepted.Count*2.39996f;
            Vector3 target=site.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
            // Every accepted route must end inside the venue's real 8m work
            // area, including larger selected groups and nav fallback offsets.
            if (member.TryCommandMoveTo(target,7.5f-radius)) accepted.Add(member);
        }
        if (accepted.Count==0) { Report("Order blocked: select available crew and a reachable business approach."); return false; }
        work[site]=new Assignment {crew=accepted.ToArray(),revisions=accepted.Select(a=>a.OrderRevision).ToArray(),loot=loot,invest=invest};
        CloseLedger(); Report($"{site.Definition.name}: {accepted.Count} members en route to {(invest?"INVEST":loot?"LOOT":"CONTROL")}. Selection retained.");
        return true;
    }

    void Update()
    {
        var d=GameManager.Data;
        if (d==null) return;
        float dt=Time.deltaTime;
        finished.Clear();
        foreach (var entry in work)
        {
            var site=entry.Key; var assignment=entry.Value;
            bool invalid=!site;
            for (int i=0;!invalid && i<assignment.crew.Length;i++)
                invalid=!assignment.crew[i] || !assignment.crew[i].IsAlive || assignment.crew[i].IsActivityLocked || assignment.crew[i].IsOnLiveJob || assignment.crew[i].OrderRevision!=assignment.revisions[i];
            if (invalid) { finished.Add(site); Report("Business assignment cancelled: crew unavailable or given another order."); continue; }
            bool arrived=assignment.crew.All(a=>HorizontalDistance(a.transform.position,site.transform.position)<=8f);
            assignment.travel+=dt;
            if (!arrived)
            {
                if (assignment.elapsed>0 || assignment.travel>240f)
                { finished.Add(site); Report(site.Definition.name+": assignment cancelled because crew left or could not arrive."); }
                continue;
            }
            if (HostilesNear(site.transform.position,12f)) { site.SetStatus("CONTESTED - CLEAR THE AREA"); continue; }
            if(assignment.elapsed<=0&&assignment.crew.Any(a=>a.CurrentState!=AgentController.State.Idle))continue;
            assignment.elapsed+=dt;
            float duration=assignment.invest?CityCommerceRules.InvestmentSeconds(CityBusinessLedger.Find(d,site.Definition.id)):assignment.loot?18f:site.Definition.controlSeconds;
            site.SetStatus($"{(assignment.invest?"IMPROVING":assignment.loot?"LOOT":"CONTROL")} {Mathf.Clamp01(assignment.elapsed/duration)*100:0}%");
            if (assignment.elapsed<duration) continue;
            bool completed=assignment.invest?CityCommerceRules.Invest(d,site.Definition.id):assignment.loot?CityBusinessLedger.Loot(d,site.Definition.id):CityBusinessLedger.Control(d,site.Definition.id);
            finished.Add(site);
            if (!completed) continue;
            site.RefreshStatus();
            Report(site.Definition.name+(assignment.invest?": INVESTMENT COMPLETE - visible trading expansion and higher income saved.":assignment.loot?$": +£{site.Definition.loot} / HEAT +{site.Definition.heat}; trade disrupted.":": CONTROL SECURED - income now active."));
            GameManager.Save(); CityGameplay.Instance?.CheckCampaignCompletion();
        }
        foreach (var site in finished) work.Remove(site);
        tick+=dt; saveClock+=dt;
        if (tick>=1f)
        {
            bool matchday=StadiumMatchdayActivity.IsMatchdayActive;
            int income=CityBusinessLedger.Tick(d,tick,matchday,CityGameplay.HomeMode?0:CampaignMissions.ContextMission(d));
            if(!CityGameplay.HomeMode)ApplyHomePressureWhileAway(d,tick);
            foreach (var site in sites)
            {
                var state=CityBusinessLedger.Find(d,site.Definition.id);
                bool pressure=state.controlled && HostilesNear(site.transform.position,18f) && !DefendersNear(site.transform.position,12f);
                if (pressure && state.rivalPressureSeconds<=0) Report(site.Definition.name+": RIVALS CONTESTING - SEND DEFENDERS.");
                state.rivalPressureSeconds=pressure?state.rivalPressureSeconds+tick:0;
                if (state.rivalPressureSeconds>=CityBusinessLedger.TakeoverSeconds)
                { state.controlled=false;state.rivalPressureSeconds=0;Report(site.Definition.name+": CONTROL LOST TO RIVAL PRESSURE.");GameManager.Save(); }
                if (!work.ContainsKey(site)) site.RefreshStatus();
            }
            tick=0;
            if (income>0) { Report($"BUSINESS INCOME +£{income:N0} - defend your trading venues."); GameManager.Save(); CityGameplay.Instance?.CheckCampaignCompletion(); }
        }
        if (saveClock>=15f) { saveClock=0;GameManager.Save(); }
        if (IsOpen && Time.unscaledTime>=refresh) {refresh=Time.unscaledTime+1f;RefreshLedger();}
    }

    void ApplyHomePressureWhileAway(PlayerData d,float dt)
    {
        bool exposed=CampaignMissions.HomeReserves(d)==0&&d.PoliceHeat>=3&&d.CityBusinesses.Any(b=>b!=null&&CityBusinessLedger.IsHomeId(b.id)&&b.controlled);
        d.HomePressureSeconds=exposed?d.HomePressureSeconds+dt:0f;
        if(exposed&&d.HomePressureSeconds-dt<120f&&d.HomePressureSeconds>=120f)
            Report("HOME BUSINESSES UNGUARDED - rival pressure will disrupt trade in 60 seconds. Return or leave reserves next trip.");
        if(d.HomePressureSeconds>=180f)
        {
            var threatened=d.CityBusinesses.FirstOrDefault(b=>b!=null&&CityBusinessLedger.IsHomeId(b.id)&&b.controlled&&b.disruptionSeconds<=0);
            if(threatened!=null){threatened.disruptionSeconds=CityBusinessLedger.LootDisruption;Report("HOME PRESSURE - "+threatened.id.ToUpperInvariant()+" trade disrupted; no reserve crew was left behind.");}
            d.HomePressureSeconds=0;GameManager.Save();
        }
    }

    public static bool HostilesNear(Vector3 point,float radius)
        => BattleManager.instance && BattleManager.instance.EnemyAgents.Any(e=>e&&e.IsAlive&&e.gameObject.activeInHierarchy&&
            e.isHostile&&!e.IsHomeMatchdaySupporter&&e.firmName!="POLICE"&&HorizontalDistance(e.transform.position,point)<=radius);
    public static string AssignmentFor(AgentController member)
    {
        if (!Instance || !member) return null;
        foreach(var entry in Instance.work)
            if(entry.Value.crew.Contains(member))return (entry.Value.invest?"IMPROVING ":entry.Value.loot?"LOOT ":"CONTROL ")+entry.Key.Definition.name;
        return null;
    }
    public static bool DefendersNear(Vector3 point,float radius)
        => BattleManager.instance && BattleManager.instance.PlayerAgents.Any(a=>a&&a.IsAlive&&a.gameObject.activeInHierarchy&&HorizontalDistance(a.transform.position,point)<=radius);
    public static float HorizontalDistance(Vector3 a,Vector3 b) {a.y=0;b.y=0;return Vector3.Distance(a,b);}
    static void Report(string text) {CityGameplay.Instance?.PostEvent(text);BattleUIController.instance?.ShowAlert(text,2.4f);}

    public void FocusFirst()
    {
        var site=sites.FirstOrDefault(s=>!CityBusinessLedger.Find(GameManager.Data,s.Definition.id).controlled)??sites.FirstOrDefault();
        if (!site) return;
        CameraPanTouchOnly.Instance?.FocusOn(site.transform.position);site.OpenInteraction();
    }
    public void OpenLedger()
    {
        if (!ledger) BuildLedger();
        CityUiSheets.Open(this,CloseLedger);
        ledger.SetActive(true);RefreshLedger();
    }
    public void CloseLedger() {if(ledger)ledger.SetActive(false);}
    void BuildLedger()
    {
        var frame=WorldButtonLayer.MissionFrame();
        ledger=LandscapeUI.Panel("BusinessLedger",frame,250,120,1100,690,true).gameObject;
        LandscapeUI.Text("Title",ledger.transform,"BUSINESSES & INCOME",24,18,800,40,28,LandscapeUI.White,true);
        LandscapeUI.Button("Close",ledger.transform,"CLOSE",902,18,174,46).onClick.AddListener(CloseLedger);
        LandscapeUI.Text("Help",ledger.transform,"Select crew, then CONTROL a venue. Hold it to earn income. LOOT is optional: heat rises and trading stops.",24,70,1052,54,18,LandscapeUI.Muted);
        rows=LandscapeUI.Scroll("BusinessScroll",ledger.transform,20,134,1060,534).content;
    }
    void RefreshLedger()
    {
        if (rows.childCount==0)
        foreach (var definition in CityBusinessLedger.DistrictDefinitions(CityGameplay.HomeMode?1:CampaignMissions.ContextMission(GameManager.Data)))
        {
            var site=sites.FirstOrDefault(x=>x.Definition.id==definition.id);
            var row=LandscapeUI.Panel(definition.id,rows,0,0,1032,156,true);
            LandscapeUI.LayoutSize(row.gameObject,1032,156);
            LandscapeUI.Text("Name",row.transform,definition.name,18,12,640,30,22,LandscapeUI.White,true);
            LandscapeUI.Text("Detail",row.transform,"",18,48,690,90,17,LandscapeUI.Muted);
            var button=LandscapeUI.Button("View",row.transform,site?"VIEW / ACTIONS":"AT HOME",752,20,258,50);
            button.interactable=site;
            button.onClick.AddListener(()=>{CloseLedger();CameraPanTouchOnly.Instance?.FocusOn(site.transform.position);site.OpenInteraction();});
        }
        foreach (var definition in CityBusinessLedger.DistrictDefinitions(CityGameplay.HomeMode?1:CampaignMissions.ContextMission(GameManager.Data)))
        {
            var state=CityBusinessLedger.Find(GameManager.Data,definition.id);
            var text=rows.Find(definition.id)?.Find("Detail")?.GetComponent<TextMeshProUGUI>();
            if(text)text.text=$"{(state.controlled?"YOUR CONTROL":"UNCONTROLLED")} | LEVEL {state.investmentLevel+1} | Income £{CityBusinessLedger.Yield(GameManager.Data,definition,StadiumMatchdayActivity.IsMatchdayActive)} / 90 sec\n"+
                $"Loot £{definition.loot} / heat +{definition.heat} | {(state.lootCooldownSeconds>0?$"cooldown {Mathf.CeilToInt(state.lootCooldownSeconds)}s":"available")}\n"+
                (state.disruptionSeconds>0?$"Trade disrupted: {Mathf.CeilToInt(state.disruptionSeconds)}s":$"Next income tick: {Mathf.CeilToInt(CityBusinessLedger.IncomeInterval-(GameManager.Data?.BusinessEconomySeconds??0))}s");
        }
    }
}

public sealed class CityBusiness : MonoBehaviour
{
    CityBusinessSystem owner;
    TextMeshPro label;
    public CityBusinessLedger.Definition Definition {get;private set;}
    public Transform Storefront {get;set;}
    GameObject expansion;
    int visualLevel=-1;
    public void Configure(CityBusinessSystem system,CityBusinessLedger.Definition definition)
    {
        owner=system;Definition=definition;
        label=ZoneLabelUtil.Create(transform,definition.name,4f,6f);
        // WorldInteractBubble supplies the input collider. The venue marker never
        // adds a solid road obstacle or changes the navigation mesh.
        WorldInteractBubble.Create(transform).Show(definition.name,OpenInteraction);
        RefreshStatus();
    }
    public void SetStatus(string value) {if(label)label.text=Definition.name+"\n"+value;}
    public void RefreshStatus()
    {
        var state=CityBusinessLedger.Find(GameManager.Data,Definition.id);
        if(state==null)return;
        SetStatus(state.rivalPressureSeconds>0?"CONTESTED":state.disruptionSeconds>0?"TRADE DISRUPTED":state.controlled?$"YOUR BUSINESS · LEVEL {state.investmentLevel+1}":"CONTROL / LOOT");
        RefreshExpansion(state.investmentLevel);
    }
    public void OpenInteraction()
    {
        CityCommerceSystem.Ensure(owner).Open(this);
    }

    void RefreshExpansion(int level)
    {
        level=Mathf.Clamp(level,0,2);if(level==visualLevel)return;visualLevel=level;
        if(expansion)Destroy(expansion);if(level==0)return;
        expansion=new GameObject("BUSINESS EXPANSION LEVEL "+(level+1));expansion.transform.SetParent(transform,false);
        for(int i=0;i<level;i++)
        {
            var prefab=Resources.Load<GameObject>("HomeFacilities/SupplyYard");
            if(prefab)
            {
                var table=Instantiate(prefab,expansion.transform);table.transform.localPosition=new Vector3(4+i*2.5f,0,3);
                var renderers=table.GetComponentsInChildren<Renderer>();
                if(renderers.Length>0){var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);float span=Mathf.Max(bounds.size.x,bounds.size.z);if(span>0)table.transform.localScale*=Mathf.Min(.6f,4.5f/span);}
            }
            for(int box=0;box<2+i;box++)
            {
                var crate=GameObject.CreatePrimitive(PrimitiveType.Cube);crate.name="Venue supply crate";crate.transform.SetParent(expansion.transform,false);
                crate.transform.localPosition=new Vector3(4+i*2.5f,.45f+box*.75f,4.8f);crate.transform.localScale=Vector3.one*.75f;
                Destroy(crate.GetComponent<Collider>());crate.GetComponent<Renderer>().material.color=new Color(.58f,.36f,.14f);
            }
        }
        var sign=ZoneLabelUtil.Create(expansion.transform,"INVESTED · "+(level==1?"EXPANDED TRADE":"DISTRICT VENUE"),2.8f,4f);sign.color=LandscapeUI.Gold;
    }
}
