using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Paid, player-driven recovery truck. The incident system owns physical clearance work.</summary>
public sealed class PoliceRecoveryVehicle:MonoBehaviour
{
    public const int HireCost=450,DispatchCost=75;
    public static PoliceRecoveryVehicle Instance{get;private set;}
    public bool Selected{get;private set;}
    public bool IsDriving=>path!=null&&corner<path.Length;
    public string IncidentId{get;private set;}
    public Vector3 Destination{get;private set;}
    public string Status{get;private set;}="Recovery truck ready. Select it and tap a road to drive.";
    Vector3[] path;int corner;
    float nextCheck;
    TextMeshPro caption;
    Transform boom;
    static Material recoveryMaterial;
    public static void Restore(PoliceRecoveryVehicleSave saved)
    {
        if(Instance)Destroy(Instance.gameObject);
        if(saved==null||!saved.hired)return;
        var vehicle=Create(saved.position);if(!vehicle)return;
        vehicle.transform.rotation=Quaternion.Euler(0,saved.heading,0);vehicle.IncidentId=saved.incidentId;
        if(saved.driving)vehicle.DriveTo(saved.destination);
        if(!string.IsNullOrEmpty(saved.incidentId))CityRoadIncidentSystem.Ensure().RegisterTowVehicle(saved.incidentId,vehicle.transform);
    }
    static PoliceRecoveryVehicle Create(Vector3 requested)
    {
        if(!Resources.Load<GameObject>("CityIncidents/RecoveryTruck")||!NavMesh.SamplePosition(requested,out var road,80,PolicePatrolVehicle.RoadMask))return null;
        var host=new GameObject("PLAYER RECOVERY TRUCK");host.transform.position=road.position;
        var vehicle=host.AddComponent<PoliceRecoveryVehicle>();CreateNpcRecoveryModel(host.transform);vehicle.boom=host.transform.Find("RecoveryBoom");
        var hit=host.AddComponent<BoxCollider>();hit.center=Vector3.up*1.5f;hit.size=new Vector3(3.4f,3f,7f);hit.isTrigger=true;
        vehicle.caption=ZoneLabelUtil.Create(host.transform,"RECOVERY TRUCK",2.2f,5);var billboard=vehicle.caption.GetComponent<ZoneLabelBillboard>();if(billboard)billboard.enabled=false;
        return vehicle;
    }
    public static GameObject CreateNpcRecoveryModel(Transform parent)
    {
        var prefab=Resources.Load<GameObject>("CityIncidents/RecoveryTruck");if(!prefab)return null;
        var model=Instantiate(prefab,parent);model.name="Existing Megacity recovery chassis";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(0,-90,0);
        foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
        foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
        var renderers=model.GetComponentsInChildren<Renderer>();
        if(renderers.Length>0)
        {
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float span=Mathf.Max(bounds.size.x,bounds.size.z);if(span>.1f)model.transform.localScale*=6.8f/span;
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.position+=new Vector3(parent.position.x-bounds.center.x,parent.position.y-bounds.min.y+.08f,parent.position.z-bounds.center.z);
        }
        if(!recoveryMaterial){var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");recoveryMaterial=new Material(shader);recoveryMaterial.color=new Color(.95f,.65f,.08f);}
        var pivot=new GameObject("RecoveryBoom").transform;pivot.SetParent(parent,false);pivot.localPosition=new Vector3(0,2.1f,-1.6f);
        Part("Boom",pivot,new Vector3(0,.55f,-.85f),new Vector3(.28f,.28f,2.2f),new Vector3(-28,0,0));
        Part("WinchCable",pivot,new Vector3(0,.15f,-1.8f),new Vector3(.06f,1.4f,.06f),Vector3.zero);
        Part("RecoveryHook",pivot,new Vector3(.1f,-.57f,-1.8f),new Vector3(.3f,.1f,.12f),Vector3.zero);
        return model;
    }
    static void Part(string name,Transform parent,Vector3 point,Vector3 size,Vector3 rotation)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=point;part.transform.localScale=size;part.transform.localEulerAngles=rotation;
        var collider=part.GetComponent<Collider>();if(collider){collider.enabled=false;Destroy(collider);}part.GetComponent<Renderer>().sharedMaterial=recoveryMaterial;
    }
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    static bool RoadPath(Vector3 from,Vector3 target,out Vector3[] result,out Vector3 end)
    {
        result=null;end=default;
        if(!NavMesh.SamplePosition(from,out var start,5,PolicePatrolVehicle.RoadMask)||!NavMesh.SamplePosition(target,out var finish,20,PolicePatrolVehicle.RoadMask))return false;
        var route=new NavMeshPath();if(!NavMesh.CalculatePath(start.position,finish.position,PolicePatrolVehicle.RoadMask,route)||route.status!=NavMeshPathStatus.PathComplete)return false;
        end=finish.position;result=route.corners;return result.Length>0;
    }
    public static bool TryHireAndDispatch(string incidentId,Vector3 target)
    {
        var career=PoliceCityMode.Career;var system=CityRoadIncidentSystem.Instance;var incident=system?system.GetIncident(incidentId):null;
        if(!PoliceCityMode.Active||career==null||incident==null||incident.IsCleared||!incident.RequiresTow)return false;
        if(Instance&&Instance.IncidentId==incidentId){system.RegisterTowVehicle(incidentId,Instance.transform);return true;}
        if(Instance&&!string.IsNullOrEmpty(Instance.IncidentId)&&system.GetIncident(Instance.IncidentId)!=null&&!system.GetIncident(Instance.IncidentId).IsCleared)
        {PoliceCityMode.Instance.Report("Recovery truck is assigned to another active road incident. Finish it before purchasing another dispatch.");return false;}
        bool hired=career.recoveryVehicle!=null&&career.recoveryVehicle.hired;
        int cost=(hired?0:HireCost)+(career.recoveryDispatchReceipts.Contains(incidentId)?0:DispatchCost);
        Vector3 from=Instance?Instance.transform.position:hired?career.recoveryVehicle.position:PoliceCityMode.Instance.Station;
        var approach=target+(from-target).normalized*8f;
        if(!NavMesh.SamplePosition(from,out var spawn,80,PolicePatrolVehicle.RoadMask)||!RoadPath(spawn.position,approach,out var route,out var end)||CityBusinessSystem.HorizontalDistance(end,target)>12)
        {PoliceCityMode.Instance.Report("No complete road route for recovery. No payment taken; move the road response to a reachable site.");return false;}
        if(career.officers.Money<cost){PoliceCityMode.Instance.Report($"Recovery needs £{cost}. Earn shift funds or resolve calls; no payment taken.");return false;}
        var vehicle=Instance?Instance:Create(spawn.position);if(!vehicle){PoliceCityMode.Instance.Report("Recovery model or road spawn unavailable. No payment taken.");return false;}
        if(!system.RegisterTowVehicle(incidentId,vehicle.transform))return false;
        career.officers.Money-=cost;if(!career.recoveryDispatchReceipts.Contains(incidentId))career.recoveryDispatchReceipts.Add(incidentId);
        vehicle.IncidentId=incidentId;vehicle.path=route;vehicle.corner=route.Length>1?1:route.Length;vehicle.Destination=end;vehicle.Select();
        vehicle.Status=$"Recovery en route. £{cost} paid once. Assigned foot officers must secure the site; clearance needs 12s actual truck/crew work.";
        career.recoveryVehicle=vehicle.Capture();PoliceCityMode.Instance.Report(vehicle.Status);PoliceCityMode.Instance.SaveCareerProgress();return true;
    }
    public void Select(bool focus=false){Selected=true;PolicePatrolVehicle.Instance?.Deselect();AgentSelectionManager.instance?.DeselectAll();if(focus)CameraPanTouchOnly.Instance?.FocusOn(transform.position);}
    public void Deselect(){Selected=false;}
    public bool DriveTo(Vector3 target)
    {
        if(!RoadPath(transform.position,target,out var route,out var end)){Status="No complete road route. The previous recovery order stays active.";return false;}
        path=route;corner=route.Length>1?1:route.Length;Destination=end;Status="Recovery truck moving. Tap another road to redirect; work pauses while away from its assigned incident.";return true;
    }
    public static bool TryHandleTap(Vector2 point)
    {
        var truck=Instance;if(!truck||!PoliceCityMode.Active||!Camera.main)return false;
        var ray=Camera.main.ScreenPointToRay(point);var hits=Physics.RaycastAll(ray,2500,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).ToArray();
        if(hits.Any(h=>h.collider.GetComponentInParent<AgentController>())){truck.Deselect();return false;}
        if(hits.Any(h=>h.collider.GetComponentInParent<PoliceRecoveryVehicle>()==truck)){truck.Select();return true;}
        if(!truck.Selected)return false;
        var plane=new Plane(Vector3.up,truck.transform.position);if(plane.Raycast(ray,out float distance))truck.DriveTo(ray.GetPoint(distance));return true;
    }
    public PoliceRecoveryVehicleSave Capture()=>new PoliceRecoveryVehicleSave{hired=true,position=transform.position,heading=transform.eulerAngles.y,destination=Destination,driving=IsDriving,incidentId=IncidentId};
    void Update()
    {
        if(!PoliceCityMode.Active)return;
        if(caption&&Camera.main){caption.transform.rotation=Camera.main.transform.rotation;caption.text=Selected?"RECOVERY SELECTED · TAP ROAD":"RECOVERY TRUCK";}
        if(IsDriving&&Time.deltaTime>0)
        {
            var delta=path[corner]-transform.position;delta.y=0;float step=8.5f*CityRoadIncidentSystem.TrafficSpeedMultiplier(transform.position,transform.forward,IncidentId)*Time.deltaTime;
            if(delta.magnitude<=Mathf.Max(.2f,step)){transform.position=path[corner++];if(!IsDriving)Status="Recovery parked. Assigned officers must remain beside the road incident during 12s crane work.";}
            else{transform.position+=delta.normalized*step;transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*4);}
        }
        var system=CityRoadIncidentSystem.Instance;var incident=system&&!string.IsNullOrEmpty(IncidentId)?system.GetIncident(IncidentId):null;
        if(boom&&!GameFeedback.ReducedMotion)boom.localRotation=Quaternion.Euler(incident!=null&&incident.TowWorkSeconds>0&&!incident.IsCleared?Mathf.Sin(Time.time*1.8f)*7:0,0,0);
        if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.2f;
        if(incident!=null&&!incident.IsCleared){system.RegisterTowVehicle(IncidentId,transform);if(system.TryCompleteTow(IncidentId,transform.position)){Status="Road obstruction physically removed. Recovery truck remains owned and available for another incident.";IncidentId=null;PoliceCityMode.Instance.Report(Status);PoliceCityMode.Instance.SaveCareerProgress();}}
        else if(incident!=null&&incident.IsCleared)IncidentId=null;
    }
}
