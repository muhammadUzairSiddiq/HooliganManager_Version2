using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

[Serializable] public sealed class RoadIncidentSave
{
    public string id;public int day,severity;public Vector3 location,forward;
    public float officerWorkSeconds,towWorkSeconds;public bool cleared;
    public bool matchdayCordon,clearanceRequested;
    [System.Runtime.Serialization.OptionalField] public float autoClearAt;
    public string district;
    public List<string> workerIds=new List<string>();
}

/// <summary>Real road-side incidents, growing physical queues and interruptible clearance. Roads are never hidden or carved.</summary>
public sealed class CityRoadIncidentSystem:MonoBehaviour
{
    public enum ClearanceState{Blocked,OfficersEnRoute,Working,AwaitingTow,Towing,Cleared}
    public sealed class RoadIncident
    {
        public string Id=>Saved.id;public Vector3 Location=>Saved.location;public int Severity=>Saved.severity;
        public bool RequiresTow=>Severity>=2;public bool IsCleared=>Saved.cleared;
        public bool IsMatchdayCordon=>Saved.matchdayCordon;
        public string District=>Saved.district;
        public Vector3 Direction=>Saved.forward;
        public float CrowdPressure{get;internal set;}public int QueuedPeople{get;internal set;}
        public ClearanceState State{get;internal set;}
        public float ClearanceProgress=>IsCleared?1:RequiresTow?Mathf.Clamp01(Saved.towWorkSeconds/TowSeconds):Mathf.Clamp01(Saved.officerWorkSeconds/OfficerSeconds);
        public float TowWorkSeconds=>Saved.towWorkSeconds;
        public Vector3 WalkingBypassStart=>BypassA;public Vector3 WalkingBypassEnd=>BypassB;
        public Vector3 OfficerWorkPoint=>(BypassA+BypassB)*.5f;
        public Transform RecoveryVehicle=>Tow;
        public MatchdayPoliceResponse PoliceResponse{get;internal set;}
        internal RoadIncidentSave Saved;internal GameObject Props;internal TextMeshPro Label;
        internal readonly List<Transform> Barriers=new List<Transform>(4);
        internal AgentController[] Officers=Array.Empty<AgentController>();internal int[] Revisions=Array.Empty<int>();
        internal EnemyController[] NpcOfficers=Array.Empty<EnemyController>();internal Transform Tow;
        internal Vector3 BypassA,BypassB;internal float NextNpcAttempt;
    }
    sealed class QueueEntry
    {
        public EnemyController Actor;public RoadIncident Incident;public Vector3 Destination,Point;
        public int Life;
        public bool Current=>Actor&&Actor.gameObject.activeInHierarchy&&Actor.IsAlive&&Actor.LifeRevision==Life;
    }
    public const int MaximumActive=3,MaximumQueuedPerIncident=10;
    public const int MaximumMatchdayActive=6;
    public const float OfficerSeconds=16,TowSeconds=12;
    public static CityRoadIncidentSystem Instance{get;private set;}
    public bool AutomaticIncidentsEnabled=true,AutomaticClearanceEnabled=true;
    public IReadOnlyList<RoadIncident> ActiveIncidents=>activeIncidents;
    public string Status{get;private set;}="Roads clear.";
    public int ClearedCount{get;private set;}
    readonly List<RoadIncident> incidents=new List<RoadIncident>(),activeIncidents=new List<RoadIncident>();readonly List<QueueEntry> queue=new List<QueueEntry>();
    float nextIncident,nextQueue,nextSave;int sequence;
    bool ready;
    ITHappy.SplineConnector trafficLanes;
    Transform npcTow;Vector3[] towRoute;int towCorner;Vector3 towOrigin;RoadIncident towIncident;bool towReturning;
    public static CityRoadIncidentSystem Ensure()
    {return Instance?Instance:new GameObject("CITY ROAD INCIDENTS").AddComponent<CityRoadIncidentSystem>();}
    void Awake(){Instance=this;}
    IEnumerator Start()
    {
        while(GameManager.Data==null||!BattleManager.instance||!BattleManager.instance.BattleActive)yield return null;
        GameManager.Data.CityRoadIncidents??=new List<RoadIncidentSave>();
        ClearedCount=GameManager.Data.CityRoadIncidents.Count(s=>s!=null&&s.cleared&&s.day==GameManager.Data.MatchDay);
        foreach(var saved in GameManager.Data.CityRoadIncidents.Where(s=>s!=null&&!s.cleared&&s.day==GameManager.Data.MatchDay).Take(MaximumMatchdayActive))
        {
            if(TrySite(saved.location,out var point,out var forward,out var a,out var b,saved.matchdayCordon?saved.forward:default))
            {saved.location=point;saved.forward=forward;var incident=Create(saved,a,b);StartCoroutine(RestoreOfficers(incident));}
        }
        nextIncident=Time.time+45;nextSave=Time.time+15;
        ready=true;
    }
    IEnumerator RestoreOfficers(RoadIncident incident)
    {
        yield return null;
        if(!PoliceCityMode.Active||incident.Saved.workerIds==null)yield break;
        var actors=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive&&incident.Saved.workerIds.Contains(a.Data.AgentId)).ToArray();
        if(actors.Length>0)TryAssignOfficers(incident.Id,actors);
    }
    public RoadIncident GetIncident(string id)=>incidents.FirstOrDefault(i=>i.Id==id);
    public static float TrafficSpeedMultiplier(Vector3 position,Vector3 forward,string exemptIncidentId=null)
    {
        if(!Instance||forward.sqrMagnitude<.1f)return 1;
        forward.y=0;forward.Normalize();
        foreach(var incident in Instance.activeIncidents)
        {
            if(incident.Id==exemptIncidentId)continue;
            var delta=incident.Location-position;delta.y=0;float ahead=Vector3.Dot(delta,forward);
            float halfWidth=incident.IsMatchdayCordon?5.8f:3.2f;
            if(ahead>-.5f&&ahead<13&&Vector3.Cross(delta,forward).sqrMagnitude<halfWidth*halfWidth)return 0;
        }
        return 1;
    }
    public bool TryCreateIncident(Vector3 requested,int severity,out RoadIncident incident)
    {
        incident=null;if(GameManager.Data==null||incidents.Count(i=>!i.IsCleared)>=MaximumActive||!Resources.Load<GameObject>("CityIncidents/TrafficCone")||!Resources.Load<GameObject>("CityIncidents/RoadBarricade"))return false;
        if(!TrySite(requested,out var point,out var forward,out var a,out var b)||incidents.Any(i=>!i.IsCleared&&Distance(i.Location,point)<28))return false;
        var saved=new RoadIncidentSave{id=$"ROAD-{GameManager.Data.MatchDay}-{Guid.NewGuid():N}",day=GameManager.Data.MatchDay,severity=Mathf.Clamp(severity,1,3),location=point,forward=forward};
        GameManager.Data.CityRoadIncidents??=new List<RoadIncidentSave>();GameManager.Data.CityRoadIncidents.Add(saved);
        incident=Create(saved,a,b);Status=incident.RequiresTow?"Road obstruction: send officers and recovery truck.":"Blocked lane: send officers to remove the barriers.";
        CityGameplay.Instance?.PostEvent(Status+" Pedestrians keep a connected bypass.");GameFeedback.Emit(GameFeedbackEvent.Warning,point);GameManager.Save();return true;
    }
    public bool TryCreateMatchdayCordon(string district, int wave, Vector3 center,float autoClearAt=0)
    {
        if(!ready)return false;
        var data=GameManager.Data;
        string id=$"MATCHDAY-{data.MatchDay}-{district}-{wave}";
        if(data.CityRoadIncidents.Any(record=>record!=null&&record.id==id))return true;
        if(activeIncidents.Count>=MaximumMatchdayActive)return false;
        var sites=TrafficLaneSites(center).Concat(Enumerable.Range(0,13).Select(sample=>
        {
            float angle=(sample+wave*2)*Mathf.PI/6;
            return (point:center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(sample==0?0:24+wave*18),
                forward:Vector3.zero);
        }));
        foreach(var site in sites)
        {
            if(!TrySite(site.point,out var point,out var forward,out var a,out var b,site.forward))continue;
            if(Distance(point,center)>85||activeIncidents.Any(i=>Distance(i.Location,point)<35))continue;
            var record=new RoadIncidentSave {
                id=id,day=data.MatchDay,severity=wave==2?2:1,location=point,forward=forward,
                matchdayCordon=true,district=district,autoClearAt=autoClearAt
            };
            data.CityRoadIncidents.Add(record);
            Create(record,a,b);
            CityGameplay.Instance?.PostEvent($"{district.ToUpperInvariant()} · POLICE ROAD CLOSURE");
            GameFeedback.Emit(GameFeedbackEvent.Warning,point);
            GameManager.Save();
            return true;
        }
        return false;
    }

    IEnumerable<(Vector3 point,Vector3 forward)> TrafficLaneSites(Vector3 center)
    {
        if(!trafficLanes)trafficLanes=FindFirstObjectByType<ITHappy.SplineConnector>();
        if(!trafficLanes||trafficLanes.Splines==null)yield break;
        var nearby=new List<(Vector3 point,Vector3 forward)>();
        foreach(var lane in trafficLanes.Splines)
        {
            if(lane.length<18||lane.crossIndex>=0||Distance(lane.p0,center)>130||Mathf.Abs(lane.p0.y-center.y)>2)continue;
            var direction=Vector3.ProjectOnPlane(lane.p3-lane.p0,Vector3.up).normalized;
            if(Mathf.Max(Mathf.Abs(direction.x),Mathf.Abs(direction.z))<.96f)continue;
            for(int i=1;i<=3;i++)
            {
                var point=ITHappy.MathExtension.EvaluateCuibic(lane.p0,lane.p1,lane.p2,lane.p3,i*.25f);
                if(Distance(point,center)<=82)nearby.Add((point,direction));
            }
        }
        foreach(var lane in nearby.OrderBy(lane=>Distance(lane.point,center)))
        {
            // Put the obstacle between the opposing lanes where both are authored.
            var opposite=nearby.Where(other=>Vector3.Dot(lane.forward,other.forward)<-.95f&&
                Mathf.Abs(Vector3.Dot(other.point-lane.point,lane.forward))<9&&
                Vector3.Cross(other.point-lane.point,lane.forward).magnitude is > 2 and < 10)
                .OrderBy(other=>Distance(lane.point,other.point)).FirstOrDefault();
            var mid=lane.point;
            if(opposite.forward!=Vector3.zero)
                mid+=Vector3.ProjectOnPlane(opposite.point-lane.point,lane.forward)*.5f;
            yield return (mid,lane.forward);
        }
    }
    public bool RequestMatchdayClearance(RoadIncident incident)
    {
        if(incident==null||!incident.IsMatchdayCordon||incident.IsCleared)return false;
        incident.Saved.clearanceRequested=true;
        incident.NextNpcAttempt=0;
        incident.PoliceResponse?.SignalResponse();
        GameManager.Save();
        return true;
    }
    RoadIncident Create(RoadIncidentSave saved,Vector3 a,Vector3 b)
    {
        var incident=new RoadIncident{Saved=saved,BypassA=a,BypassB=b,State=ClearanceState.Blocked};incidents.Add(incident);activeIncidents.Add(incident);
        incident.Props=new GameObject("ROAD INCIDENT "+saved.id);incident.Props.transform.SetParent(transform);incident.Props.transform.position=saved.location;incident.Props.transform.rotation=Quaternion.LookRotation(saved.forward);
        for(int i=0;i<4;i++)Prop("TrafficCone",incident.Props.transform,new Vector3((i%2==0?-1:1)*1.7f,0,(i<2?-1:1)*3.2f));
        if(saved.matchdayCordon)
        {
            for(int lane=0;lane<4;lane++)
                incident.Barriers.Add(CordonBarrier.Create(incident.Props.transform,new Vector3((lane-1.5f)*3.28f,0,1.3f)));
            for(int side=-1;side<=1;side+=2)
            for(int row=0;row<3;row++)
                Prop("TrafficCone",incident.Props.transform,new Vector3(side*(4.6f-row*.8f),0,-.4f-row*2.4f));
            incident.PoliceResponse=MatchdayPoliceResponse.Create(incident,transform);
            MiniMapIconFactory.Register(incident.Props.transform,MiniMapIconFactory.Kind.Police,saved.district+" closure");
        }
        else Prop("RoadBarricade",incident.Props.transform,new Vector3(0,0,1.3f));
        incident.Label=ZoneLabelUtil.Create(incident.Props.transform,"ROAD BLOCKED",4,5);incident.Label.fontSize=9;incident.Label.GetComponent<ZoneLabelBillboard>().CommandPriority=true;
        return incident;
    }
    static void Prop(string key,Transform parent,Vector3 point)
    {
        var prefab=Resources.Load<GameObject>("CityIncidents/"+key);if(!prefab)return;var model=Instantiate(prefab,parent);model.transform.localPosition=point;
        foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
    }
    static float Distance(Vector3 a,Vector3 b)=>CityBusinessSystem.HorizontalDistance(a,b);
    static bool TrySite(Vector3 requested,out Vector3 point,out Vector3 forward,out Vector3 a,out Vector3 b,Vector3 preferred=default)
    {
        point=a=b=default;forward=Vector3.forward;
        if(!NavMesh.SamplePosition(requested,out var road,preferred==Vector3.zero?38:3,1<<3)||Mathf.Abs(road.position.y-requested.y)>3)return false;point=road.position;
        var source=BattleManager.instance?BattleManager.instance.enemyAgentPrefab:null;var nav=source?source.GetComponent<NavMeshAgent>():null;
        var filter=new NavMeshQueryFilter{agentTypeID=nav?nav.agentTypeID:0,areaMask=nav?nav.areaMask:NavMesh.AllAreas};var path=new NavMeshPath();
        for(int axis=0;axis<2;axis++)for(int side=-1;side<=1;side+=2)
        {
            var along=axis==0?Vector3.forward:Vector3.right;var across=Vector3.Cross(Vector3.up,along)*side;
            if(preferred!=Vector3.zero)
            {
                float alignment=Vector3.Dot(along,preferred);
                if(Mathf.Abs(alignment)<.9f)continue;
                if(alignment<0){along=-along;across=-across;}
            }
            if(!NavMesh.SamplePosition(point-along*9,out _,1.5f,1<<3)||!NavMesh.SamplePosition(point+along*9,out _,1.5f,1<<3))continue;
            if(!NavMesh.SamplePosition(point+across*6-along*10,out var before,5,filter)||!NavMesh.SamplePosition(point+across*6+along*10,out var after,5,filter))continue;
            if(Mathf.Abs(before.position.y-point.y)>2||Mathf.Abs(after.position.y-point.y)>2||!NavMesh.CalculatePath(before.position,after.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            forward=along;a=before.position;b=after.position;return true;
        }
        return false;
    }
    public bool TryAssignOfficers(string id,IEnumerable<AgentController> officers)
    {
        var incident=GetIncident(id);if(incident==null||incident.IsCleared||!PoliceCityMode.Active)return false;
        var accepted=(officers??Array.Empty<AgentController>()).Where(a=>a&&a.IsAlive&&!a.IsActivityLocked&&!PolicePatrolVehicle.IsMounted(a)&&a.TryCommandMoveTo(incident.OfficerWorkPoint,2)).Distinct().ToArray();
        if(accepted.Length==0){Status="Select available foot officers with a complete route to the road-side work area.";return false;}
        incident.Officers=accepted;incident.Revisions=accepted.Select(a=>a.OrderRevision).ToArray();incident.Saved.workerIds=accepted.Select(a=>a.Data.AgentId).ToList();incident.State=ClearanceState.OfficersEnRoute;
        Status=$"{accepted.Length} officers travelling to clear the road. New orders interrupt their work.";GameManager.Save();return true;
    }
    public bool TryDispatchTow(string id)
    {var incident=GetIncident(id);return PoliceCityMode.Active&&incident!=null&&!incident.IsCleared&&incident.RequiresTow&&PoliceRecoveryVehicle.TryHireAndDispatch(id,incident.Location);}
    public bool RegisterTowVehicle(string id,Transform vehicle)
    {var incident=GetIncident(id);if(incident==null||incident.IsCleared||!incident.RequiresTow||!vehicle||vehicle.GetComponentsInChildren<Renderer>().Length==0)return false;incident.Tow=vehicle;return true;}
    void ValidateOfficers(RoadIncident incident)
    {
        if(!PoliceCityMode.Active||incident.Officers.Length==0)return;
        bool changed=false;for(int i=0;i<incident.Officers.Length;i++){var a=incident.Officers[i];if(!a||!a.IsAlive||a.IsActivityLocked||PolicePatrolVehicle.IsMounted(a)||a.OrderRevision!=incident.Revisions[i]){changed=true;break;}}
        if(!changed)return;
        var remaining=new List<AgentController>();var revisions=new List<int>();
        for(int i=0;i<incident.Officers.Length;i++){var a=incident.Officers[i];if(a&&a.IsAlive&&!a.IsActivityLocked&&!PolicePatrolVehicle.IsMounted(a)&&a.OrderRevision==incident.Revisions[i]){remaining.Add(a);revisions.Add(incident.Revisions[i]);}}
        incident.Officers=remaining.ToArray();incident.Revisions=revisions.ToArray();incident.Saved.workerIds=remaining.Select(a=>a.Data.AgentId).ToList();
        if(remaining.Count==0){incident.State=ClearanceState.Blocked;Status="Road work interrupted. Officers keep their newer orders; reassign when ready.";}
    }
    public int PresentOfficerCount(string id)
    {
        var incident=GetIncident(id);if(incident==null||incident.IsCleared)return 0;int count=0;
        if(PoliceCityMode.Active)
        {
            for(int i=0;i<incident.Officers.Length;i++)
            {var a=incident.Officers[i];if(a&&a.IsAlive&&!a.IsActivityLocked&&!PolicePatrolVehicle.IsMounted(a)&&a.OrderRevision==incident.Revisions[i]&&a.CurrentState==AgentController.State.Idle&&Distance(a.transform.position,incident.Location)<9)count++;}
            return count;
        }
        return incident.NpcOfficers.Count(a=>a&&a.IsAlive&&!a.isHostile&&a.holdFire&&a.gameObject.activeInHierarchy&&a.firmName=="POLICE"&&Distance(a.transform.position,incident.Location)<9);
    }
    public bool HasOfficerCoverage(string id)=>PresentOfficerCount(id)>0;
    bool OfficerPresent(RoadIncident incident)=>PresentOfficerCount(incident.Id)>0;
    public bool TryCompleteTow(string id,Vector3 vehiclePosition)
    {
        var incident=GetIncident(id);if(incident==null||incident.IsCleared||!incident.RequiresTow||!incident.Tow||Distance(vehiclePosition,incident.Tow.position)>1||Distance(incident.Tow.position,incident.Location)>12||!OfficerPresent(incident)||incident.Saved.towWorkSeconds<TowSeconds)return false;
        Complete(incident);return true;
    }
    public void FinishMatchdayCordons()
    {
        foreach(var road in activeIncidents.Where(road=>road.IsMatchdayCordon).ToArray())Complete(road,false);
    }

    void Complete(RoadIncident incident,bool announce=true)
    {
        if(incident.IsCleared)return;incident.Saved.cleared=true;incident.State=ClearanceState.Cleared;activeIncidents.Remove(incident);ClearedCount++;
        if(incident.Props)Destroy(incident.Props);ReleaseQueue(incident);Status="ROAD REOPENED · waiting people resume their routes.";
        if(incident.IsMatchdayCordon)StadiumMatchdayActivity.Instance?.ReleaseCordonQueue(incident);
        var detail=incident.PoliceResponse;
        foreach(var officer in incident.NpcOfficers)if(officer&&officer.IsAlive&&(!detail||!detail.Officers.Contains(officer))){officer.EndEscort();officer.HoldFire(false);officer.WalkToRally(incident.BypassB);}
        if(detail)detail.Withdraw();
        if(announce)
        {
            GameFeedback.Emit(GameFeedbackEvent.TaskComplete,incident.Location);
            CityGameplay.Instance?.PostEvent(Status);
            GameManager.Save();
        }
    }
    void Update()
    {
        if(!ready||GameManager.Data==null||!BattleManager.instance||!BattleManager.instance.BattleActive)return;
        bool active=PoliceCityMode.Active||StadiumMatchdayActivity.IsMatchdayActive;
        if(AutomaticIncidentsEnabled&&active&&!MatchdayStreetOperations.Active&&Time.time>=nextIncident)
        {
            nextIncident=Time.time+95;var city=CityGameplay.Instance;
            if(city&&city.Locations.Length>7)TryCreateIncident(city.Locations[(sequence%2==0)?7:5]+new Vector3(12,0,8),(sequence++%3)==0?1:2,out _);
        }
        TickNpcTow();
        if(Time.time>=nextQueue){nextQueue=Time.time+.4f;TickQueue();}
        foreach(var incident in incidents)
        {
            if(incident.IsCleared)continue;
            ValidateOfficers(incident);
            var stadium=StadiumMatchdayActivity.Instance;
            bool scheduled=incident.IsMatchdayCordon&&stadium&&stadium.StreetOperations&&stadium.StreetOperations.Automatic&&
                stadium.ElapsedSeconds>=(incident.Saved.autoClearAt>0?incident.Saved.autoClearAt:65);
            if(scheduled&&AutomaticClearanceEnabled&&!CityActivityStreaming.Interested(incident.Location,true))
            {
                incident.Saved.officerWorkSeconds+=Time.deltaTime;
                if(incident.Saved.officerWorkSeconds>=OfficerSeconds)Complete(incident);
                continue;
            }
            if(!PoliceCityMode.Active&&AutomaticClearanceEnabled&&(!incident.IsMatchdayCordon||incident.Saved.clearanceRequested||scheduled||!MatchdayStreetOperations.Active))
                AutoDispatch(incident);
            bool present=OfficerPresent(incident);
            if(present)
            {
                if(incident.RequiresTow)
                {
                    bool towing=incident.Tow&&Distance(incident.Tow.position,incident.Location)<=12;
                    incident.State=towing?ClearanceState.Towing:ClearanceState.AwaitingTow;
                    if(towing){incident.Saved.towWorkSeconds+=Time.deltaTime;if(!PoliceCityMode.Active)TryCompleteTow(incident.Id,incident.Tow.position);}
                }
                else{incident.State=ClearanceState.Working;incident.Saved.officerWorkSeconds+=Time.deltaTime*(incident.IsMatchdayCordon&&MatchdayTactics.Ensure(GameManager.Data).plan==1?1.5f:1f);if(incident.Saved.officerWorkSeconds>=OfficerSeconds)Complete(incident);}
            }
            else if(incident.State==ClearanceState.Working||incident.State==ClearanceState.Towing)incident.State=ClearanceState.Blocked;
            if(incident.IsMatchdayCordon&&!incident.IsCleared)MoveClearedBarriers(incident);
            if(incident.Label)incident.Label.text=$"{(incident.IsMatchdayCordon?incident.District.ToUpperInvariant():"ROAD")} · {incident.State.ToString().ToUpperInvariant()}\n{incident.QueuedPeople} WAITING";
        }
        if(incidents.Count>16)incidents.RemoveAll(i=>i.IsCleared&&incidents.IndexOf(i)<incidents.Count-12);
        var records=GameManager.Data.CityRoadIncidents;
        if(records!=null&&records.Count>16){var retired=records.Where(r=>r!=null&&r.cleared).Take(records.Count-16).ToArray();foreach(var record in retired)records.Remove(record);}
        if(Time.time>=nextSave){nextSave=Time.time+15;if(incidents.Any(i=>!i.IsCleared))GameManager.Save();}
    }
    static void MoveClearedBarriers(RoadIncident incident)
    {
        float progress=incident.ClearanceProgress;
        for(int index=0;index<incident.Barriers.Count;index++)
        {
            var barrier=incident.Barriers[index];
            float start=(index-1.5f)*3.28f;
            float moved=Mathf.SmoothStep(0,1,progress*4-index);
            barrier.localPosition=new Vector3(Mathf.Lerp(start,Mathf.Sign(start)*7.2f,moved),0,1.3f+(index%2)*2.2f*moved);
            barrier.localRotation=Quaternion.Euler(0,moved*90,0);
        }
    }
    void AutoDispatch(RoadIncident incident)
    {
        if(Time.time<incident.NextNpcAttempt)return;incident.NextNpcAttempt=Time.time+8;
        if(!incident.NpcOfficers.Any(a=>a&&a.IsAlive))
        {
            var officers=BattleManager.instance.EnemyAgents
                .Where(a=>a&&a.IsAlive&&a.firmName=="POLICE"&&!a.IsEscorting&&!incidents.Any(i=>i!=incident&&!i.IsCleared&&
                    (i.NpcOfficers.Contains(a)||(i.PoliceResponse&&i.PoliceResponse.Officers.Contains(a)))))
                .OrderBy(a=>!(incident.PoliceResponse&&incident.PoliceResponse.Officers.Contains(a)))
                .ThenBy(a=>Distance(a.transform.position,incident.Location)).Take(2).ToList();
            if(officers.Count==0&&CityActivityStreaming.Interested(incident.Location))
            {
                var approach=incident.BypassA-incident.Saved.forward*24;
                var officer=BattleManager.instance.SpawnMatchdayFighter(approach,"POLICE",new Color(.15f,.4f,.85f),120,0,MatchdayUnitRole.Police,"ROAD CLEARANCE "+incident.Id);
                if(officer){var march=officer.GetComponent<MatchdayMarch>();if(march)march.enabled=false;officers.Add(officer);}
            }
            var routed=new List<EnemyController>();foreach(var officer in officers){officer.SetCinematicIdle(false);if(officer.TryBeginEscort(incident.OfficerWorkPoint,false))routed.Add(officer);}
            incident.NpcOfficers=routed.ToArray();incident.State=ClearanceState.OfficersEnRoute;
        }
        if(incident.RequiresTow&&!incident.Tow)CreateNpcTow(incident);
    }
    void CreateNpcTow(RoadIncident incident)
    {
        if(npcTow&&npcTow.gameObject.activeSelf)return;var prefab=Resources.Load<GameObject>("CityIncidents/RecoveryTruck");if(!prefab)return;
        if(!NavMesh.SamplePosition(incident.Location-incident.Saved.forward*55,out var road,30,1<<3))return;
        var path=new NavMeshPath();if(!NavMesh.CalculatePath(road.position,incident.Location,1<<3,path)||path.status!=NavMeshPathStatus.PathComplete)return;
        if(!npcTow){npcTow=new GameObject("NPC POLICE ROAD RECOVERY").transform;npcTow.SetParent(transform);PoliceRecoveryVehicle.CreateNpcRecoveryModel(npcTow);foreach(var collider in npcTow.GetComponentsInChildren<Collider>())collider.enabled=false;}
        npcTow.gameObject.SetActive(true);npcTow.position=road.position;towOrigin=road.position;towRoute=path.corners;towCorner=1;towIncident=incident;towReturning=false;RegisterTowVehicle(incident.Id,npcTow);
    }
    void TickNpcTow()
    {
        if(!npcTow||!npcTow.gameObject.activeSelf)return;
        if(towRoute!=null&&towCorner<towRoute.Length)
        {
            var target=towRoute[towCorner];var delta=target-npcTow.position;delta.y=0;if(delta.sqrMagnitude>.05f)npcTow.rotation=Quaternion.Slerp(npcTow.rotation,Quaternion.LookRotation(delta),Time.deltaTime*5);
            npcTow.position=Vector3.MoveTowards(npcTow.position,target,8*TrafficSpeedMultiplier(npcTow.position,npcTow.forward,towIncident?.Id)*Time.deltaTime);if(Distance(npcTow.position,target)<.35f)towCorner++;return;
        }
        if(towReturning){npcTow.gameObject.SetActive(false);towIncident=null;return;}
        if(towIncident!=null&&towIncident.IsCleared)
        {var path=new NavMeshPath();if(NavMesh.CalculatePath(npcTow.position,towOrigin,1<<3,path)&&path.status==NavMeshPathStatus.PathComplete){towRoute=path.corners;towCorner=1;towReturning=true;}else npcTow.gameObject.SetActive(false);}
    }
    public bool TryQueueArrival(EnemyController actor,Vector3 destination,string cordonId=null)
    {
        if(!actor||!actor.IsAlive||actor.firmName=="POLICE"||queue.Any(q=>q.Actor==actor&&q.Current))return false;
        var nav=actor.GetComponent<NavMeshAgent>();if(!nav||!nav.enabled||!nav.isOnNavMesh)return false;
        var path=new NavMeshPath();if(!nav.CalculatePath(destination,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
        var incident=cordonId==null?incidents.FirstOrDefault(i=>!i.IsCleared&&PathNear(path,i.Location,9)):GetIncident(cordonId);
        if(incident==null||incident.IsCleared)return false;
        int slot=queue.Count(q=>q.Incident==incident);if(slot>=MaximumQueuedPerIncident)return false;
        var side=Vector3.Cross(Vector3.up,incident.Saved.forward);var requested=incident.BypassA-incident.Saved.forward*(slot/3*1.6f)+side*(slot%3-1)*1.4f;
        var filter=new NavMeshQueryFilter{agentTypeID=nav.agentTypeID,areaMask=nav.areaMask};
        if(!NavMesh.SamplePosition(requested,out var point,3,filter)||!actor.TryBeginEscort(point.position,false))return false;
        var march=actor.GetComponent<MatchdayMarch>();if(march)march.enabled=false;
        queue.Add(new QueueEntry{Actor=actor,Incident=incident,Destination=destination,Point=nav.destination,Life=actor.LifeRevision});return true;
    }
    public static bool IsQueued(EnemyController actor)=>Instance&&Instance.queue.Any(q=>q.Actor==actor&&q.Current);
    void TickQueue()
    {
        for(int index=queue.Count-1;index>=0;index--)
        {
            var q=queue[index];var nav=q.Actor?q.Actor.GetComponent<NavMeshAgent>():null;
            if(!q.Current||q.Incident.IsCleared||!nav||!nav.enabled||!nav.isOnNavMesh||q.Actor.isHostile){queue.RemoveAt(index);continue;}
            if(nav.hasPath&&Distance(nav.destination,q.Point)>3){queue.RemoveAt(index);continue;}
        }
        foreach(var incident in incidents){incident.QueuedPeople=queue.Count(q=>q.Incident==incident&&q.Actor&&Distance(q.Actor.transform.position,q.Point)<3);incident.CrowdPressure=Mathf.Clamp01(incident.QueuedPeople/(float)MaximumQueuedPerIncident);}
    }
    void ReleaseQueue(RoadIncident incident)
    {
        foreach(var q in queue.Where(q=>q.Incident==incident).ToArray())
        {
            if(q.Current&&!q.Actor.isHostile&&!q.Actor.TryBeginEscort(q.Destination,false))
                q.Actor.TryBeginEscort(incident.WalkingBypassEnd,false);
            queue.Remove(q);
        }
    }
    static bool PathNear(NavMeshPath path,Vector3 point,float radius)
    {
        var corners=path.corners;for(int i=1;i<corners.Length;i++){var a=corners[i-1];var segment=corners[i]-a;segment.y=0;var delta=point-a;delta.y=0;float t=segment.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(delta,segment)/segment.sqrMagnitude):0;if(Distance(a+segment*t,point)<radius)return true;}return false;
    }
    void OnDestroy(){if(Instance==this)Instance=null;}
}
