using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public sealed class StockRescueRun
{
    public string venueId, memberId;
    public int day;
}

/// <summary>Bounded rival decisions and actual timed shop raids by existing gang members.</summary>
public sealed class LivingRivalDirector:MonoBehaviour
{
    public sealed class ShopRaid
    {
        public CityBusiness Site;
        public string Firm;
        public EnemyController[] Crew;
        public Vector3[] ReturnPoints,Targets;
        public float TravelSeconds,WorkSeconds;
        public bool Working;
        internal TextMeshPro Label;
    }
    public const float RaidWorkSeconds=18;
    public static LivingRivalDirector Instance{get;private set;}
    public bool AutomaticDecisionsEnabled=true;
    public IReadOnlyList<ShopRaid> ActiveRaids=>raids;
    public int CompletedRaids{get;private set;}
    public string LastRaidResult{get;private set;}="No active shop raid.";
    readonly List<ShopRaid> raids=new List<ShopRaid>();
    float nextDecision,nextClash;
    int groupIndex;
    GangArea[] groups;
    TerritoryControlPoint[] businesses;
    AgentController stockCarrier;
    int stockOrderRevision;
    TextMeshPro stockLabel;
    void Awake(){Instance=this;}
    void Start()
    {
        groups=FindObjectsByType<GangArea>(FindObjectsSortMode.None);businesses=FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None);nextDecision=Time.time+45;
        CityLivingPresentation.Ensure(gameObject);
        CityRoadIncidentSystem.Ensure();
    }
    void Update()
    {
        if(GuidedCityTutorial.CombatSuppressed)return;
        var battle=BattleManager.instance;if(!battle)return;
        TickStockRescue();
        TickRaids(Time.deltaTime);if(!AutomaticDecisionsEnabled)return;
        if(Time.time>=nextClash)
        {
            nextClash=Time.time+2;int incidents=0;var used=new HashSet<string>();
            foreach(var a in battle.EnemyAgents)
            {
                if(!Eligible(a)||a.isHostile||Busy(a)||used.Contains(a.firmName))continue;
                foreach(var b in battle.EnemyAgents)
                {
                    if(!Eligible(b)||b.isHostile||Busy(b)||a.firmName==b.firmName||used.Contains(b.firmName))continue;
                    if((a.transform.position-b.transform.position).sqrMagnitude>100)continue;
                    a.isHostile=b.isHostile=true;a.AlertToTarget(b);b.AlertToTarget(a);used.Add(a.firmName);used.Add(b.firmName);incidents++;break;
                }
                if(incidents>=3)break;
            }
        }
        if(Time.time<nextDecision||groups==null||groups.Length<2)return;nextDecision=Time.time+24;
        // A new firm gets time to learn. Later raids create a visible reason to
        // allocate defenders rather than deducting money from an unseen timer.
        if(raids.Count==0&&HomeTerritorySystem.Built(GameManager.Data,0)&&CityBusinessSystem.Instance)
        {
            var target=CityBusinessSystem.Instance.Sites.Where(s=>s&&CityBusinessLedger.Find(GameManager.Data,s.Definition.id).controlled).OrderBy(s=>CityBusinessLedger.Find(GameManager.Data,s.Definition.id).rivalRaidCount).FirstOrDefault(s=>TryStartShopRaid(s));
            if(target)return;
        }
        int index=groupIndex++%groups.Length;var area=groups[index];var other=groups[(index+1)%groups.Length];if(!area||!other)return;
        Vector3 destination=(area.transform.position+other.transform.position)*.5f;
        foreach(var business in businesses)if(business&&business.IsCaptured&&(business.transform.position-area.transform.position).sqrMagnitude<140*140){destination=business.transform.position;break;}
        int count=0;foreach(var member in battle.EnemyAgents){if(!Eligible(member)||Busy(member)||member.firmName!=area.GangName||member.isHostile)continue;if(member.WalkToRally(destination))count++;if(count>=3)break;}
    }
    bool Busy(EnemyController actor)=>raids.Any(r=>r.Crew.Contains(actor));
    public static bool HasShopRaid(CityBusiness site)=>Instance&&Instance.raids.Any(r=>r.Site==site);

    public bool TryRescueStock(CityBusiness site)
    {
        var data=GameManager.Data;
        if(!site||data==null||!HasShopRaid(site)||data.ActiveStockRescue!=null)return false;
        var venue=CityBusinessLedger.Find(data,site.Definition.id);
        if(venue==null||!venue.controlled||venue.lastStockRescueDay==data.MatchDay)return false;
        var selection=AgentSelectionManager.instance;
        var actor=selection?.SelectedAgents.FirstOrDefault(member=>member&&member.IsAlive&&
            !member.IsActivityLocked&&member.CurrentState!=AgentController.State.AutoAttacking&&
            CityBusinessSystem.HorizontalDistance(member.transform.position,site.transform.position)<=20);
        if(!actor||!actor.TryCommandMoveTo(CityGameplay.Instance.Home,8))
        {
            CityGameplay.Instance?.PostEvent("STOCK RESCUE · select a free crew member within 20m of the threatened shop.");
            return false;
        }
        data.ActiveStockRescue=new StockRescueRun {
            venueId=site.Definition.id,memberId=actor.Data.AgentId,day=data.MatchDay
        };
        venue.lastStockRescueDay=data.MatchDay;
        TrackStockCarrier(actor);
        CityGameplay.Instance?.PostEvent("STOCK RESCUE · the carrier is heading home. Protect them or abandon the run to fight.");
        GameManager.Save();
        return true;
    }

    void TrackStockCarrier(AgentController actor)
    {
        stockCarrier=actor;
        stockOrderRevision=actor.OrderRevision;
        stockLabel=ZoneLabelUtil.Create(actor.transform,"CARRYING SHOP STOCK",3.2f,4.5f);
    }

    void TickStockRescue()
    {
        var data=GameManager.Data;
        var run=data?.ActiveStockRescue;
        if(run==null||!CityGameplay.HomeMode||PoliceCityMode.Active||!BattleManager.instance.BattleActive)return;
        if(!stockCarrier)
        {
            var actor=BattleManager.instance.PlayerAgents.FirstOrDefault(member=>member&&member.Data.AgentId==run.memberId);
            if(!actor||!actor.IsAlive||run.day!=data.MatchDay||!actor.TryCommandMoveTo(CityGameplay.Instance.Home,8))
            {
                EndStockRescue(false);
                return;
            }
            TrackStockCarrier(actor);
        }
        if(!stockCarrier.IsAlive||stockCarrier.OrderRevision!=stockOrderRevision||stockCarrier.IsActivityLocked)
            EndStockRescue(false);
        else if(CityBusinessSystem.HorizontalDistance(stockCarrier.transform.position,CityGameplay.Instance.Home)<9)
            EndStockRescue(true);
    }

    void EndStockRescue(bool delivered)
    {
        var data=GameManager.Data;
        data.ActiveStockRescue=null;
        if(stockLabel)Destroy(stockLabel.gameObject);
        if(delivered)
        {
            data.CitySupplies=Mathf.Min(CityCommerceRules.SupplyCapacity,data.CitySupplies+2);
            GameFeedback.Emit(GameFeedbackEvent.TaskComplete,stockCarrier.transform.position);
        }
        stockCarrier=null;
        CityGameplay.Instance?.PostEvent(delivered?"STOCK DELIVERED · two supplies rescued at Home.":"STOCK RUN ABANDONED · the shop stock was lost.");
        GameManager.Save();
    }
    public static string RaidStatus(CityBusiness site)
    {
        var raid=Instance?Instance.raids.FirstOrDefault(r=>r.Site==site):null;
        return raid==null?null:raid.Firm+" · "+(raid.Working?$"RAIDING {raid.WorkSeconds:0}/{RaidWorkSeconds:0}s":"MOVING TO RAID");
    }
    public bool TryStartShopRaid(CityBusiness site,string firm=null)
    {
        if(!enabled||!site||!BattleManager.instance||raids.Count>=1)return false;
        var state=CityBusinessLedger.Find(GameManager.Data,site.Definition.id);if(state==null||state.rivalRaidCooldownSeconds>0||Protected(site.transform.position))return false;
        var candidates=BattleManager.instance.EnemyAgents.Where(a=>Eligible(a)&&!a.isHostile&&!Busy(a)&&(string.IsNullOrEmpty(firm)||a.firmName==firm)).OrderBy(a=>(a.transform.position-site.transform.position).sqrMagnitude).ToArray();
        foreach(var grouping in candidates.GroupBy(a=>a.firmName))
        {
            var accepted=new List<EnemyController>();var goals=new List<Vector3>();
            foreach(var actor in grouping)
            {
                var nav=actor.GetComponent<NavMeshAgent>();if(!nav||!nav.enabled||!nav.isOnNavMesh)continue;
                var filter=new NavMeshQueryFilter{agentTypeID=nav.agentTypeID,areaMask=nav.areaMask};
                Vector3 offset=new Vector3((accepted.Count-1)*1.8f,0,accepted.Count%2*1.5f);
                if(!NavMesh.SamplePosition(site.transform.position+offset,out var goal,3,filter)||CityBusinessSystem.HorizontalDistance(goal.position,site.transform.position)>7)continue;
                var path=new NavMeshPath();if(!nav.CalculatePath(goal.position,path)||path.status!=NavMeshPathStatus.PathComplete||PathMetres(path)>260)continue;
                accepted.Add(actor);goals.Add(goal.position);if(accepted.Count>=3)break;
            }
            if(accepted.Count<2)continue;
            var raid=new ShopRaid{Site=site,Firm=grouping.Key,Crew=accepted.ToArray(),ReturnPoints=accepted.Select(a=>a.transform.position).ToArray(),Targets=goals.ToArray()};
            bool routed=true;
            for(int i=0;i<raid.Crew.Length;i++)
            {
                var actor=raid.Crew[i];actor.SetCinematicIdle(false);
                if(!actor.TryBeginEscort(raid.Targets[i],false)){routed=false;break;}
                actor.isHostile=true;raid.Targets[i]=actor.GetComponent<NavMeshAgent>().destination;
            }
            if(!routed){Release(raid);continue;}
            raids.Add(raid);var host=new GameObject("Rival shop raid progress");host.transform.SetParent(site.transform,false);
            raid.Label=ZoneLabelUtil.Create(host.transform,"RIVAL RAID INCOMING",5,5);raid.Label.GetComponent<ZoneLabelBillboard>().CommandPriority=true;
            LastRaidResult=raid.Firm+" is moving to raid "+site.Definition.name;
            CityGameplay.Instance?.PostEvent(LastRaidResult+". Send defenders or police before they finish.");GameFeedback.Emit(GameFeedbackEvent.Warning,site.transform.position);site.RefreshStatus();return true;
        }
        return false;
    }
    static float PathMetres(NavMeshPath path){float total=0;for(int i=1;i<path.corners.Length;i++)total+=Vector3.Distance(path.corners[i-1],path.corners[i]);return total;}
    static bool Protected(Vector3 point)
    {
        if(CityBusinessSystem.DefendersNear(point,12))return true;
        return BattleManager.instance&&BattleManager.instance.EnemyAgents.Any(a=>a&&a.IsAlive&&a.gameObject.activeInHierarchy&&a.firmName=="POLICE"&&CityBusinessSystem.HorizontalDistance(a.transform.position,point)<14);
    }
    void TickRaids(float dt)
    {
        for(int index=raids.Count-1;index>=0;index--)
        {
            var raid=raids[index];string blocked=null;
            if(!raid.Site)blocked="venue unavailable";
            else if(Protected(raid.Site.transform.position))blocked="defenders or police reached the venue";
            for(int i=0;blocked==null&&i<raid.Crew.Length;i++)
            {
                var actor=raid.Crew[i];var nav=actor?actor.GetComponent<NavMeshAgent>():null;
                if(!Eligible(actor)||actor.firmName!=raid.Firm||!nav||!nav.enabled||!nav.isOnNavMesh){blocked="raid member unavailable";break;}
                if(!actor.isHostile){blocked="rival stood down or accepted a different task";break;}
                if(nav.hasPath&&!nav.pathPending&&CityBusinessSystem.HorizontalDistance(nav.destination,raid.Targets[i])>3){blocked="a newer route replaced the raid order";break;}
            }
            if(blocked!=null){FinishRaid(index,false,blocked);continue;}
            raid.TravelSeconds+=dt;
            if(!raid.Working)
            {
                if(raid.TravelSeconds>210){FinishRaid(index,false,"crew did not reach the shop in time");continue;}
                if(!raid.Crew.All(a=>CityBusinessSystem.HorizontalDistance(a.transform.position,raid.Site.transform.position)<8))continue;
                raid.Working=true;
                for(int i=0;i<raid.Crew.Length;i++){raid.Crew[i].EndEscort();raid.Targets[i]=raid.Crew[i].transform.position;}
            }
            if(!raid.Crew.All(a=>CityBusinessSystem.HorizontalDistance(a.transform.position,raid.Site.transform.position)<8)){FinishRaid(index,false,"crew left the work area");continue;}
            raid.WorkSeconds+=dt;if(raid.Label)raid.Label.text=$"RIVAL SHOP RAID · {raid.WorkSeconds:0}/{RaidWorkSeconds:0}s";
            if(raid.WorkSeconds<RaidWorkSeconds)continue;
            bool complete=CityBusinessLedger.CompleteRivalRaid(GameManager.Data,raid.Site.Definition.id,raid.Firm);FinishRaid(index,complete,complete?"shop stock stolen; trade disrupted":"venue cooldown changed");
        }
    }
    void FinishRaid(int index,bool complete,string reason)
    {
        var raid=raids[index];raids.RemoveAt(index);
        if(raid.Site)
        {
            var state=CityBusinessLedger.Find(GameManager.Data,raid.Site.Definition.id);
            if(!complete&&state!=null)state.rivalRaidCooldownSeconds=Mathf.Max(state.rivalRaidCooldownSeconds,45);
            LastRaidResult=raid.Firm+" · "+raid.Site.Definition.name+": "+reason;
            CityGameplay.Instance?.PostEvent(LastRaidResult);
            if(complete){CompletedRaids++;CityLivingPresentation.Ensure(gameObject).RaidDamage(raid.Site.transform.position);GameFeedback.Emit(GameFeedbackEvent.TaskFailed,raid.Site.transform.position);}
            else GameFeedback.Emit(GameFeedbackEvent.TaskComplete,raid.Site.transform.position);
            raid.Site.RefreshStatus();GameManager.Save();
        }
        Release(raid);
    }
    void Release(ShopRaid raid)
    {
        if(raid.Label)Destroy(raid.Label.transform.parent.gameObject);
        for(int i=0;i<raid.Crew.Length;i++)
        {
            var actor=raid.Crew[i];if(!actor||!actor.IsAlive||actor.firmName!=raid.Firm)continue;
            var nav=actor.GetComponent<NavMeshAgent>();
            if(nav&&nav.enabled&&nav.isOnNavMesh&&nav.hasPath&&CityBusinessSystem.HorizontalDistance(nav.destination,raid.Targets[i])>3)continue;
            actor.SetCinematicIdle(false);actor.EndEscort();actor.HoldFire(false);actor.isHostile=false;actor.WalkToRally(raid.ReturnPoints[i]);
        }
    }
    void OnDisable(){foreach(var raid in raids)Release(raid);raids.Clear();}
    void OnDestroy(){if(Instance==this)Instance=null;}
    static bool Eligible(EnemyController member)=>member&&member.IsAlive&&member.gameObject.activeInHierarchy&&!member.IsAmbientMatchdayUnit&&member.firmName!="POLICE"&&!RivalSettlement.IsSettled(member.firmName);
}
