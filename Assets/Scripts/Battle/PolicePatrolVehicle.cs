using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Player patrol transport. Roads are authoritative; boarding and exit are physical.</summary>
public sealed class PolicePatrolVehicle : MonoBehaviour
{
    public static PolicePatrolVehicle Instance {get;private set;}
    public const int Capacity=6;
    public const int RoadMask=1<<3;
    public bool Selected {get;private set;}
    public bool IsDriving=>corners!=null&&corner<corners.Length;
    public int PassengerCount=>passengers.Count(p=>p.actor&&p.actor.IsAlive);
    public int BoardingCount=>boarding.Count;
    public int SuspectCount=>prisoners.Count;
    public int LoadingSuspectCount=>loading.Count;
    public Vector3 Destination {get;private set;}
    public string Status {get;private set;}="Select officers, then BOARD SELECTED. They walk to this van.";
    public IReadOnlyList<AgentController> Occupants=>passengers.Select(p=>p.actor).ToArray();
    public float Speed=11f;
    readonly List<Seat> passengers=new List<Seat>();
    readonly Dictionary<AgentController,int> boarding=new Dictionary<AgentController,int>();
    readonly List<PrisonerSeat> prisoners=new List<PrisonerSeat>();
    readonly Dictionary<string,LoadRequest> loading=new Dictionary<string,LoadRequest>();
    Vector3[] corners;
    int corner;
    LineRenderer route;
    TextMeshPro worldCaption;
    float boardClock;
    sealed class Seat
    {
        public AgentController actor;
        public Renderer[] renderers;
        public Collider[] colliders;
        public NavMeshAgent nav;
    }
    sealed class PrisonerSeat {public string callId;public EnemyController actor;public NavMeshAgent nav;public Renderer[] renderers;public Collider[] colliders;}
    sealed class LoadRequest {public EnemyController suspect;public AgentController officer;public int revision;}
    public bool HasSuspect(string callId)=>prisoners.Any(p=>p.callId==callId&&p.actor);
    public void RestoreSuspects(IEnumerable<string> callIds)
    {
        foreach(var id in callIds??new string[0])
        {
            var call=PoliceCityMode.Career.calls.FirstOrDefault(c=>c.id==id&&c.state==3);
            var actor=call!=null?PoliceCityMode.Instance.GetSuspect(call):null;
            if(actor)SeatSuspect(id,actor);
        }
    }
    public bool RequestSuspectLoading(string callId,EnemyController suspect,AgentController officer)
    {
        if(IsDriving||loading.ContainsKey(callId)||HasSuspect(callId)||prisoners.Count+loading.Count>=2||passengers.Count+prisoners.Count+loading.Count>=Capacity||!suspect||!officer||!officer.IsAlive||IsMounted(officer))
        {Status="Park with a free custody seat, then bring the escort and detained suspect beside the van.";return false;}
        var curb=CityGameplay.ReachableApproach(officer.transform.position,transform.position);
        if(CityBusinessSystem.HorizontalDistance(curb,transform.position)>6||!officer.TryCommandMoveTo(curb,1f))
        {Status="No connected walking approach to this van. Park beside an open curb.";return false;}
        loading[callId]=new LoadRequest{suspect=suspect,officer=officer,revision=officer.OrderRevision};
        Status="Escort and detained suspect are walking to the van. Loading happens only when both reach the curb.";return true;
    }
    void SeatSuspect(string callId,EnemyController actor)
    {
        if(!actor||HasSuspect(callId)||prisoners.Count>=2||passengers.Count+prisoners.Count>=Capacity)return;
        actor.SetCinematicIdle(true);actor.HoldFire(true);
        var seat=new PrisonerSeat{callId=callId,actor=actor,nav=actor.GetComponent<NavMeshAgent>(),renderers=actor.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray(),colliders=actor.GetComponentsInChildren<Collider>().Where(c=>c.enabled).ToArray()};
        if(seat.nav)seat.nav.enabled=false;foreach(var renderer in seat.renderers)renderer.enabled=false;foreach(var collider in seat.colliders)collider.enabled=false;
        actor.transform.position=transform.position;prisoners.Add(seat);
        var call=PoliceCityMode.Career.calls.FirstOrDefault(c=>c.id==callId);if(call!=null){call.inVehicle=true;call.progress=0;}
    }
    public bool UnloadSuspectsAtStation()
    {
        if(IsDriving||CityBusinessSystem.HorizontalDistance(transform.position,PoliceCityMode.Instance.Station)>18||prisoners.Count==0)
        {Status="Park at the police station with a detained suspect aboard before BOOK AT STATION.";return false;}
        if(PassengerCount>0&&!ExitAll())return false;
        int unloaded=0;
        foreach(var seat in prisoners.ToArray())
        {
            if(!seat.actor){prisoners.Remove(seat);continue;}
            var filter=new NavMeshQueryFilter{agentTypeID=seat.nav?seat.nav.agentTypeID:0,areaMask=seat.nav?seat.nav.areaMask:NavMesh.AllAreas};
            if(!NavMesh.SamplePosition(PoliceCityMode.Instance.Station,out var station,12,filter))continue;
            bool found=false;Vector3 drop=default;var path=new NavMeshPath();
            for(int i=0;i<16&&!found;i++)
            {
                float angle=i*Mathf.PI/4;var candidate=transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(3+i/8*2);
                if(NavMesh.SamplePosition(candidate,out var hit,2,filter)&&CityBusinessSystem.HorizontalDistance(hit.position,transform.position)<7&&NavMesh.CalculatePath(hit.position,station.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete){drop=hit.position;found=true;}
            }
            if(!found)continue;
            seat.actor.transform.position=drop;if(seat.nav){seat.nav.enabled=true;if(!seat.nav.isOnNavMesh||!seat.nav.Warp(drop)){seat.nav.enabled=false;seat.actor.transform.position=transform.position;continue;}}
            foreach(var renderer in seat.renderers)if(renderer)renderer.enabled=true;foreach(var collider in seat.colliders)if(collider)collider.enabled=true;
            seat.actor.SetCinematicIdle(false);seat.actor.HoldFire(true);prisoners.Remove(seat);
            var call=PoliceCityMode.Career.calls.FirstOrDefault(c=>c.id==seat.callId);if(call!=null){call.inVehicle=false;call.progress=0;PoliceCityMode.Instance.Escort(PoliceCityMode.Career.calls.IndexOf(call));}
            unloaded++;
        }
        Status=unloaded>0?"Suspects disembarked at the station curb. Escort physically to booking; 4s verification remains.":"No connected station curb. Reposition the van before booking.";
        GameManager.Save();return unloaded>0;
    }
    public static PolicePatrolVehicle Create(PoliceCityMode mode,PoliceVehicleSave saved)
    {
        if(Instance)Destroy(Instance.gameObject);
        var prefab=PoliceManager.instance?PoliceManager.instance.policeCarPrefab:null;
        if(!prefab){Debug.LogError("Playable patrol transport needs the existing police vehicle prefab.");return null;}
        Vector3 requested=saved!=null?saved.position:mode.Station;
        if(!NavMesh.SamplePosition(requested,out var road,80f,RoadMask))
        {Debug.LogError("No road near the police station for patrol transport.");return null;}
        var host=new GameObject("PLAYER POLICE PATROL VAN");host.transform.position=road.position;
        var vehicle=host.AddComponent<PolicePatrolVehicle>();
        CreatePatrolModel(prefab,host.transform);
        var hit=host.AddComponent<BoxCollider>();hit.center=new Vector3(0,1.25f,0);hit.size=new Vector3(2.7f,2.5f,5.5f);hit.isTrigger=true;
        vehicle.worldCaption=ZoneLabelUtil.Create(host.transform,"PATROL VAN",1.8f,4f);
        var billboard=vehicle.worldCaption.GetComponent<ZoneLabelBillboard>();if(billboard)billboard.enabled=false;
        vehicle.worldCaption.fontSize=2.4f;vehicle.worldCaption.outlineWidth=.2f;vehicle.worldCaption.outlineColor=Color.black;
        if(saved!=null)
        {
            host.transform.rotation=Quaternion.Euler(0,saved.heading,0);
            foreach(var id in saved.passengers??new List<string>())
            {
                var officer=BattleManager.instance.PlayerAgents.FirstOrDefault(a=>a&&a.IsAlive&&a.Data.AgentId==id);
                if(officer)vehicle.SeatOfficer(officer);
            }
            if(saved.driving&&vehicle.PassengerCount>0)vehicle.DriveTo(saved.destination);
        }
        return vehicle;
    }
    internal static Bounds CreatePatrolModel(GameObject prefab,Transform host)
    {
        var model=Instantiate(prefab,host);model.name="Patrol van model";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(0,-90,0);
        foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
        foreach(var collider in model.GetComponentsInChildren<Collider>())collider.enabled=false;
        foreach(var body in model.GetComponentsInChildren<Rigidbody>()){body.isKinematic=true;body.useGravity=false;}
        var renderers=model.GetComponentsInChildren<Renderer>();
        if(renderers.Length>0)
        {
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float span=Mathf.Max(bounds.size.x,bounds.size.z);if(span>.1f)model.transform.localScale*=5.2f/span;
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.position+=new Vector3(host.position.x-bounds.center.x,host.position.y-bounds.min.y+.08f,host.position.z-bounds.center.z);
            return new Bounds(host.position+Vector3.up*(bounds.extents.y+.08f),bounds.size);
        }
        return new Bounds(host.position,Vector3.zero);
    }
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;if(route)Destroy(route.gameObject);}
    public static bool IsMounted(AgentController actor)=>Instance&&Instance.passengers.Any(p=>p.actor==actor);
    public void Select(bool focus=false)
    {
        PoliceRecoveryVehicle.Instance?.Deselect();
        Selected=true;RtsGestureController.Instance?.Cancel();
        if(PassengerCount>0)AgentSelectionManager.instance?.DeselectAll();
        if(focus)CameraPanTouchOnly.Instance?.FocusOn(transform.position);
    }
    public void Deselect(){Selected=false;}
    public static bool TryHandleTap(Vector2 screenPoint)
    {
        var van=Instance;if(!van||!PoliceCityMode.Active||!Camera.main)return false;
        var ray=Camera.main.ScreenPointToRay(screenPoint);
        var hits=Physics.RaycastAll(ray,2500f,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).ToArray();
        // A visible officer remains a direct selection target even with the
        // large van tap collider behind them. Mounted officers have no collider.
        if(hits.Any(h=>{var actor=h.collider.GetComponentInParent<AgentController>();return actor&&actor.IsAlive&&!IsMounted(actor);})){van.Deselect();return false;}
        if(hits.Any(h=>h.collider.GetComponentInParent<PolicePatrolVehicle>()==van)){van.Select();return true;}
        if(!van.Selected)return false;
        var plane=new Plane(Vector3.up,new Vector3(0,van.transform.position.y,0));
        if(plane.Raycast(ray,out float distance))van.DriveTo(ray.GetPoint(distance));
        return true;
    }
    public bool BoardSelected()
    {
        if(IsDriving){Status="Stop the van before boarding.";return false;}
        int count=0;
        foreach(var officer in AgentSelectionManager.instance?.SelectedAgents.ToArray()??new AgentController[0])
        {
            if(passengers.Count+boarding.Count+prisoners.Count+loading.Count>=Capacity)break;
            if(!officer||!officer.IsAlive||officer.IsActivityLocked||boarding.ContainsKey(officer)||IsMounted(officer))continue;
            // The walking network chooses the curb; never collect distant actors instantly.
            var approach=CityGameplay.ReachableApproach(officer.transform.position,transform.position);
            if(CityBusinessSystem.HorizontalDistance(approach,transform.position)>5f||!officer.TryCommandMoveTo(approach,1f))continue;
            boarding.Add(officer,officer.OrderRevision);count++;
        }
        Status=count>0?$"{count} officers walking to the van. New orders cancel their boarding.":"Select available officers with a walking route to the parked van.";
        return count>0;
    }
    void SeatOfficer(AgentController officer)
    {
        if(!officer||IsMounted(officer)||passengers.Count+prisoners.Count>=Capacity)return;
        officer.SetActivityLocked(true,transform.position);
        var seat=new Seat{actor=officer,nav=officer.GetComponent<NavMeshAgent>(),renderers=officer.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray(),colliders=officer.GetComponentsInChildren<Collider>().Where(c=>c.enabled).ToArray()};
        if(seat.nav)seat.nav.enabled=false;
        foreach(var renderer in seat.renderers)renderer.enabled=false;
        foreach(var collider in seat.colliders)collider.enabled=false;
        officer.transform.position=transform.position;AgentSelectionManager.instance?.Deselect(officer);passengers.Add(seat);
    }
    public bool DriveTo(Vector3 target)
    {
        if(PassengerCount==0){Status="The van needs an officer aboard to drive. Select officers and BOARD SELECTED.";return false;}
        if(boarding.Count>0){Status="Wait for boarding, or CANCEL BOARDING first.";return false;}
        if(loading.Count>0){Status="Wait for the escort and suspect to reach the van, or CANCEL first.";return false;}
        if(!NavMesh.SamplePosition(transform.position,out var from,4f,RoadMask)||!NavMesh.SamplePosition(target,out var to,20f,RoadMask))
        {Status="Choose a nearby road. Vehicles cannot drive through parks or buildings.";return false;}
        var path=new NavMeshPath();
        if(!NavMesh.CalculatePath(from.position,to.position,RoadMask,path)||path.status!=NavMeshPathStatus.PathComplete||path.corners.Length<2)
        {Status="No complete road route. The previous van order stays active.";return false;}
        corners=path.corners;corner=1;Destination=to.position;Selected=true;
        Status="VAN MOVING · tap another road to redirect. EXIT OFFICERS stops and unloads at the curb.";DrawRoute();return true;
    }
    public void Stop()
    {
        corners=null;corner=0;if(route)route.gameObject.SetActive(false);
        Status="Van parked. EXIT OFFICERS to assess calls, detain suspects or hold patrol checks.";
    }
    public void CancelBoarding(){boarding.Clear();loading.Clear();Status="Boarding/loading cancelled. Officers keep their last walking order.";}
    public bool ExitAll()
    {
        if(passengers.Count==0){Status="No officers are aboard.";return false;}
        Stop();int unloaded=0;var exited=new List<AgentController>();
        foreach(var seat in passengers.ToArray())
        {
            if(!seat.actor){passengers.Remove(seat);continue;}
            Vector3 drop=Vector3.zero;bool found=false;
            var filter=new NavMeshQueryFilter{agentTypeID=seat.nav?seat.nav.agentTypeID:0,areaMask=seat.nav?seat.nav.areaMask:NavMesh.AllAreas};
            for(int i=0;i<16&&!found;i++)
            {
                float angle=i*Mathf.PI/4;float radius=3f+(i/8)*2f;
                var guess=transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if(NavMesh.SamplePosition(guess,out var hit,2f,filter)&&CityBusinessSystem.HorizontalDistance(hit.position,transform.position)<7f){drop=hit.position;found=true;}
            }
            if(!found)continue;
            seat.actor.transform.position=drop;
            if(seat.nav){seat.nav.enabled=true;if(seat.nav.isOnNavMesh)seat.nav.Warp(drop);}
            seat.actor.SetActivityLocked(false,drop);
            foreach(var renderer in seat.renderers)if(renderer)renderer.enabled=true;
            foreach(var collider in seat.colliders)if(collider)collider.enabled=true;
            passengers.Remove(seat);exited.Add(seat.actor);unloaded++;
        }
        Selected=false;AgentSelectionManager.instance?.DeselectAll();foreach(var actor in exited)if(actor.IsAlive)AgentSelectionManager.instance?.Select(actor);
        Status=unloaded>0?$"{unloaded} officers disembarked at the curb. Tap a street or choose DISPATCH.":"No safe curb here. Drive to an open road before unloading.";
        GameManager.Save();return unloaded>0;
    }
    public PoliceVehicleSave Capture()=>new PoliceVehicleSave{position=transform.position,heading=transform.eulerAngles.y,destination=Destination,driving=IsDriving,passengers=passengers.Where(p=>p.actor&&p.actor.IsAlive).Select(p=>p.actor.Data.AgentId).ToList(),suspectCallIds=prisoners.Where(p=>p.actor).Select(p=>p.callId).ToList()};
    void Update()
    {
        if(!PoliceCityMode.Active)return;
        if(worldCaption&&Camera.main){worldCaption.transform.rotation=Camera.main.transform.rotation;worldCaption.text=Selected?$"PATROL VAN · {PassengerCount} OFFICERS · {SuspectCount} CUSTODY":"PATROL VAN";}
        if(Time.unscaledTime>=boardClock)
        {
            boardClock=Time.unscaledTime+.1f;
            foreach(var pair in loading.ToArray())
            {
                var request=pair.Value;var call=PoliceCityMode.Career.calls.FirstOrDefault(c=>c.id==pair.Key);
                if(call==null||call.state!=3||!request.officer||!request.officer.IsAlive||request.officer.OrderRevision!=request.revision||!request.suspect){loading.Remove(pair.Key);continue;}
                if(CityBusinessSystem.HorizontalDistance(request.officer.transform.position,transform.position)>6.5f||CityBusinessSystem.HorizontalDistance(request.suspect.transform.position,transform.position)>6.5f||request.officer.CurrentState!=AgentController.State.Idle)continue;
                SeatSuspect(pair.Key,request.suspect);loading.Remove(pair.Key);
                if(HasSuspect(pair.Key)){SeatOfficer(request.officer);Status="Suspect secured in custody seat. Tap a road to drive to the station, then BOOK AT STATION.";GameManager.Save();}
            }
            foreach(var pair in boarding.ToArray())
            {
                var actor=pair.Key;
                if(!actor||!actor.IsAlive||actor.IsActivityLocked||actor.OrderRevision!=pair.Value){boarding.Remove(actor);continue;}
                if(CityBusinessSystem.HorizontalDistance(actor.transform.position,transform.position)<6.5f&&actor.CurrentState==AgentController.State.Idle){boarding.Remove(actor);SeatOfficer(actor);}
            }
            if(boarding.Count==0&&PassengerCount>0&&AgentSelectionManager.instance?.SelectedAgents.Count==0)Selected=true;
        }
        if(IsDriving&&PassengerCount==0){Stop();Status="Patrol van stopped: no living officer aboard to drive.";}
        if(IsDriving&&Time.deltaTime>0)
        {
            Vector3 to=corners[corner]-transform.position;to.y=0;float step=Speed*CityRoadIncidentSystem.TrafficSpeedMultiplier(transform.position,transform.forward)*Time.deltaTime;
            if(to.magnitude<=Mathf.Max(.2f,step))
            {
                transform.position=corners[corner];corner++;
                if(corner>=corners.Length){Stop();GameManager.Save();}
            }
            else
            {
                transform.position+=to.normalized*step;
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(to.normalized),Time.deltaTime*5f);
            }
        }
        foreach(var seat in passengers)if(seat.actor)seat.actor.transform.position=transform.position;
        foreach(var seat in prisoners)if(seat.actor)seat.actor.transform.position=transform.position;
        if(route)
        {
            route.gameObject.SetActive(Selected&&IsDriving);
            if(Selected&&IsDriving)
            {
                int count=corners.Length-corner;route.positionCount=count+1;
                route.SetPosition(0,transform.position+Vector3.up*.18f);
                for(int i=0;i<count;i++)route.SetPosition(i+1,corners[corner+i]+Vector3.up*.18f);
            }
        }
    }
    void DrawRoute()
    {
        if(!route){var go=new GameObject("Selected patrol van road route");route=go.AddComponent<LineRenderer>();route.startWidth=route.endWidth=.22f;route.useWorldSpace=true;ZoneVolumeFactory.ApplyDoubleSidedMaterial(route,new Color(.2f,.7f,1f,.8f));}
        route.gameObject.SetActive(true);route.positionCount=corners.Length;route.SetPositions(corners.Select(p=>p+Vector3.up*.18f).ToArray());
    }
}
