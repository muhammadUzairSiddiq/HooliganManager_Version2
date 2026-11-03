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
    public Vector3 Destination {get;private set;}
    public string Status {get;private set;}="Select officers, then BOARD SELECTED. They walk to this van.";
    public IReadOnlyList<AgentController> Occupants=>passengers.Select(p=>p.actor).ToArray();
    public float Speed=11f;
    readonly List<Seat> passengers=new List<Seat>();
    readonly Dictionary<AgentController,int> boarding=new Dictionary<AgentController,int>();
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
        var model=Instantiate(prefab,host.transform);model.name="Patrol van model";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(0,-90,0);
        foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
        foreach(var collider in model.GetComponentsInChildren<Collider>())collider.enabled=false;
        foreach(var body in model.GetComponentsInChildren<Rigidbody>()){body.isKinematic=true;body.useGravity=false;}
        var renderers=model.GetComponentsInChildren<Renderer>();
        if(renderers.Length>0)
        {
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float span=Mathf.Max(bounds.size.x,bounds.size.z);if(span>.1f)model.transform.localScale*=5.2f/span;
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.position+=new Vector3(host.transform.position.x-bounds.center.x,host.transform.position.y-bounds.min.y+.08f,host.transform.position.z-bounds.center.z);
        }
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
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;if(route)Destroy(route.gameObject);}
    public static bool IsMounted(AgentController actor)=>Instance&&Instance.passengers.Any(p=>p.actor==actor);
    public void Select(bool focus=false)
    {
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
            if(passengers.Count+boarding.Count>=Capacity)break;
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
        if(!officer||IsMounted(officer)||passengers.Count>=Capacity)return;
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
    public void CancelBoarding(){boarding.Clear();Status="Boarding cancelled. Officers keep their last walking order.";}
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
    public PoliceVehicleSave Capture()=>new PoliceVehicleSave{position=transform.position,heading=transform.eulerAngles.y,destination=Destination,driving=IsDriving,passengers=passengers.Where(p=>p.actor&&p.actor.IsAlive).Select(p=>p.actor.Data.AgentId).ToList()};
    void Update()
    {
        if(!PoliceCityMode.Active)return;
        if(worldCaption&&Camera.main){worldCaption.transform.rotation=Camera.main.transform.rotation;worldCaption.text=Selected?$"PATROL VAN · {PassengerCount}/{Capacity}":"PATROL VAN";}
        if(Time.unscaledTime>=boardClock)
        {
            boardClock=Time.unscaledTime+.1f;
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
            Vector3 to=corners[corner]-transform.position;to.y=0;float step=Speed*Time.deltaTime;
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
