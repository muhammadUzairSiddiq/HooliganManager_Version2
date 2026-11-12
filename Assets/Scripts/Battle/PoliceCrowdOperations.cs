using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>A bounded real clash inside the larger instanced stadium crowd.</summary>
public sealed class PoliceCrowdOperations:MonoBehaviour
{
    PoliceCityMode mode;
    PoliceCrowdOperationSave state;
    readonly List<EnemyController> fighters=new List<EnemyController>();
    bool spawned,held;
    float next;
    float lastWork;
    GameObject marker;
    TextMeshPro label;
    public Vector3 Location=>state!=null?state.point:Vector3.zero;
    public int CurrentState=>state?.state??0;
    public int ControlledCount=>CurrentState>=2?1:0;
    public IReadOnlyList<EnemyController> Fighters=>fighters;
    public string Description=>state==null?"Stadium watch not ready.":state.state==0?"Two rival groups are gathering. Send at least two officers before the clash escalates.":state.state==1?$"CLASH ACTIVE · {state.controlProgress:0}/18s control. Two assigned officers must remain on foot within 12m. Protect life; casualties cost trust.":state.state==2?"Groups separated. Two identified live suspects are in Dispatch; choose arrest/transport or a consequential bribe.":"Crowd operation resolved. Other patrols remain under your control.";
    public void Initialize(PoliceCityMode owner)
    {
        mode=owner;
        if(PoliceCityMode.Career.crowdOperation==null)
        {
            var city=CityGameplay.Instance;var target=city.Locations.Length>5?city.Locations[5]:owner.Station+Vector3.forward*80;
            PoliceCityMode.Career.crowdOperation=new PoliceCrowdOperationSave{id="S"+PoliceCityMode.Career.shift+":crowd",point=CityGameplay.ReachableApproach(owner.Station,target)};
        }
        state=PoliceCityMode.Career.crowdOperation;
        state.officerIds??=new List<string>();state.orderRevisions??=new List<int>();
        state.orderRevisions.Clear();foreach(var id in state.officerIds){var actor=BattleManager.instance.PlayerAgents.FirstOrDefault(a=>a&&a.Data.AgentId==id);state.orderRevisions.Add(actor?actor.OrderRevision:-1);}
        fighters.Clear();spawned=false;held=false;
        lastWork=Time.time;
        if(marker)Destroy(marker);marker=new GameObject("POLICE STADIUM CROWD RESPONSE");marker.transform.position=state.point;
        label=ZoneLabelUtil.Create(marker.transform,"STADIUM PUBLIC ORDER",2.5f,5);label.color=LandscapeUI.Gold;
        marker.SetActive(state.state<2);
    }
    public bool AssignSelected()
    {
        if(state==null||state.state>=2)return false;
        var van=PolicePatrolVehicle.Instance;
        if(van&&van.Selected&&van.PassengerCount>0){bool moved=van.DriveTo(Location);mode.Report(moved?"Van responding to the clash. Exit at the curb and assign two on-foot officers to separate the groups.":van.Status);return moved;}
        var selected=AgentSelectionManager.instance?.SelectedAgents.Where(a=>a&&a.IsAlive&&!a.IsActivityLocked&&!PolicePatrolVehicle.IsMounted(a)).ToArray();
        if(selected==null||selected.Length<2){mode.Report("Select at least two available on-foot officers for crowd control. Another patrol can keep working.");return false;}
        var accepted=new List<AgentController>();
        foreach(var actor in selected)if(actor.TryCommandMoveTo(Location+Vector3.right*(accepted.Count-1)*2))accepted.Add(actor);
        if(accepted.Count<2){mode.Report("Two officers need complete walking routes to this stadium approach. No crowd control started.");return false;}
        state.officerIds=accepted.Select(a=>a.Data.AgentId).ToList();state.orderRevisions=accepted.Select(a=>a.OrderRevision).ToList();
        mode.Report("Crowd team en route. Stay beside the clash for 18s; changing their orders cancels control.");mode.SaveCareerProgress();return true;
    }
    void SpawnClash()
    {
        if(spawned||state.state>=2)return;
        for(int index=fighters.Count;index<8;index++)
        {
            int team=index/4,i=index%4;
            var actor=BattleManager.instance.SpawnMatchdayFighter(Location+new Vector3(team==0?-4:4,0,(i-1.5f)*1.8f),team==0?"RIVERSIDE CROWD":"IRON CROWD",team==0?new Color(.15f,.65f,.35f):new Color(.8f,.18f,.12f),180,3,team==0?MatchdayUnitRole.HomeSupporter:MatchdayUnitRole.RivalSupporter,"STADIUM CLASH");
            if(!actor)continue;
            bool calm=held||state.state==0;actor.HoldFire(calm);actor.SetCinematicIdle(calm);actor.isHostile=!calm;
            actor.GetComponent<MatchdayMarch>()?.SetConflictActive(!calm);fighters.Add(actor);
        }
        spawned=fighters.Count>=2;
    }
    void Update()
    {
        if(!mode||!mode.Ready||!PoliceCityMode.Active||state==null||Time.deltaTime<=0)return;
        state.elapsed+=Time.deltaTime;
        if(state.state==0&&state.elapsed>=45)
        {
            state.state=1;foreach(var actor in fighters)if(actor){actor.SetCinematicIdle(false);actor.HoldFire(false);actor.isHostile=true;actor.GetComponent<MatchdayMarch>()?.SetConflictActive(true);}
            mode.Report("Stadium clash reported. Split your team: two officers can separate the groups while another covers patrol or road recovery.");GameFeedback.Emit(GameFeedbackEvent.Warning,Location);mode.SaveCareerProgress();
        }
        if(Time.unscaledTime<next)return;next=Time.unscaledTime+.2f;float workStep=Time.time-lastWork;lastWork=Time.time;
        if(state.state<2&&CityActivityStreaming.Interested(Location,true))SpawnClash();
        if(label)label.text=state.state==0?"STADIUM GATHERING":$"CROWD CONTROL {state.controlProgress:0}/18s";
        if(state.state==1&&!state.penaltyApplied&&state.elapsed>210)
        {state.penaltyApplied=true;PoliceCityMode.Career.trust=Mathf.Max(0,PoliceCityMode.Career.trust-4);mode.Report("Delayed stadium response: trust -4. The incident remains playable.");mode.SaveCareerProgress();}
        if(state.state<2&&state.officerIds.Count>=2)
        {
            var crew=state.officerIds.Select(id=>BattleManager.instance.PlayerAgents.FirstOrDefault(a=>a&&a.Data.AgentId==id)).ToArray();
            bool valid=crew.Select((a,i)=>a&&a.IsAlive&&!PolicePatrolVehicle.IsMounted(a)&&a.OrderRevision==state.orderRevisions[i]).All(v=>v);
            if(!valid){state.officerIds.Clear();state.orderRevisions.Clear();state.controlProgress=0;mode.Report("Crowd assignment cancelled by changed orders or an unavailable officer.");return;}
            bool near=crew.All(a=>CityBusinessSystem.HorizontalDistance(a.transform.position,Location)<12);
            if(!near)return;
            if(!spawned)SpawnClash();
            if(!held){held=true;foreach(var actor in fighters)if(actor&&actor.IsAlive){actor.HoldFire(true);actor.SetCinematicIdle(true);actor.GetComponent<MatchdayMarch>()?.SetConflictActive(false);}}
            state.controlProgress+=workStep*(PoliceCityMode.Career.morale<35?.65f:1f);
            if(state.controlProgress>=18)
            {
                var living=fighters.Where(a=>a&&a.IsAlive).ToArray();
                if(living.Length<1){mode.Report("No living clash suspects remain. Casualties reduce public trust.");state.state=3;}
                else
                {
                    int count=Mathf.Min(2,living.Length);for(int i=0;i<count;i++)mode.RegisterCrowdSuspect(state.id+":suspect"+i,living[i]);
                    for(int i=count;i<living.Length;i++){living[i].HoldFire(true);living[i].SetCinematicIdle(false);living[i].TryBeginEscort(CityGameplay.ReachableApproach(Location,Location+Vector3.forward*24),false);}
                    state.state=2;
                }
                state.casualties=fighters.Count(a=>!a||!a.IsAlive);PoliceCityMode.Career.trust=Mathf.Max(0,PoliceCityMode.Career.trust-state.casualties*2);
                marker.SetActive(false);GameFeedback.Emit(GameFeedbackEvent.TaskComplete,Location);mode.Report("Crowd separated. Live suspects are identified in Dispatch; transport/booking is a separate decision. Casualties: "+state.casualties+".");mode.SaveCareerProgress();
            }
        }
        if(state.state==2&&PoliceCityMode.Career.calls.Where(c=>c.id.StartsWith(state.id+":")).All(c=>c.state>=4))
        {state.state=3;PoliceCityMode.Career.trust=Mathf.Min(100,PoliceCityMode.Career.trust+4);PoliceCityMode.Career.morale=Mathf.Min(100,PoliceCityMode.Career.morale+3);mode.Report("Stadium public-order operation closed. Trust +4, morale +3; custody decisions remain on record.");mode.SaveCareerProgress();}
    }
    void OnDestroy(){if(marker)Destroy(marker);}
}
