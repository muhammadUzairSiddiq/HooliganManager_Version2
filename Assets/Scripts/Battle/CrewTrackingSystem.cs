using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>World-view crew tracking. Routes come from the live navigation path, never a straight-line guess.</summary>
public sealed class CrewTrackingSystem:MonoBehaviour
{
    public static CrewTrackingSystem Instance {get;private set;}
    static AgentController tracked;
    public static AgentController TrackedMember=>tracked&&tracked.IsAlive?tracked:null;
    public const string AllRoutesKey="Crew.ShowAllRoutes";
    public static bool ShowAllRoutes {get=>PlayerPrefs.GetInt(AllRoutesKey,0)!=0;set{PlayerPrefs.SetInt(AllRoutesKey,value?1:0);PlayerPrefs.Save();}}
    public const int MaximumRoutes=24,MaximumCorners=256;
    public int VisibleRouteCount {get;private set;}
    public int VisibleEdgeCount {get;private set;}
    public int CachedMemberCount=>records.Count;
    public sealed class RouteRecord
    {
        public AgentController Member;
        public NavMeshAgent Nav;
        public readonly Vector3[] Corners=new Vector3[MaximumCorners];
        public int Count,Revision=-1;
        public float Metres;
        public Vector3 Destination,Origin;
        public string From="",To="";
        internal LineRenderer Line;
    }
    readonly Dictionary<AgentController,RouteRecord> records=new Dictionary<AgentController,RouteRecord>();
    readonly List<AgentController> removed=new List<AgentController>();
    readonly List<Rect> occupiedEdges=new List<Rect>(5);
    readonly Button[] edges=new Button[5];
    readonly AgentController[] edgeMembers=new AgentController[5];
    RectTransform frame;
    RectTransform trackingBar;
    TextMeshProUGUI trackingText;
    Material routeMaterial;
    float nextRefresh,nextRoster;
    public static void Ensure(){if(!Instance)new GameObject("Crew world routes and tracking").AddComponent<CrewTrackingSystem>();}
    void Awake(){Instance=this;}
    void OnDestroy()
    {
        if(Instance==this){Instance=null;tracked=null;}
        foreach(var row in records.Values)if(row.Line)Destroy(row.Line.gameObject);
        foreach(var button in edges)if(button)Destroy(button.gameObject);
        if(trackingBar)Destroy(trackingBar.gameObject);
        if(routeMaterial)Destroy(routeMaterial);
    }
    public static void FocusMember(AgentController member)
    {
        if(!member||!member.IsAlive)return;Ensure();
        CameraPanTouchOnly.Instance?.FocusOn(WorldPosition(member));
    }
    public static void TrackMember(AgentController member)
    {
        if(!member||!member.IsAlive)return;Ensure();tracked=member;
        CameraPanTouchOnly.Instance?.FollowMember(member);
        Instance.nextRefresh=0;
    }
    public static void StopTracking(){tracked=null;CameraPanTouchOnly.Instance?.ClearFollow();}
    public static bool IsWatched(AgentController member)=>member&&member.IsAlive&&(member.IsSelected||member==TrackedMember||ShowAllRoutes);
    public static Vector3 WorldPosition(AgentController member)=>PolicePatrolVehicle.IsMounted(member)?PolicePatrolVehicle.Instance.transform.position:member?member.transform.position:Vector3.zero;
    public static Color MemberColor(AgentController member)
    {
        string id=member?.Data?.AgentId??member?.name??"crew";uint hash=2166136261;
        for(int i=0;i<id.Length;i++)hash=(hash^id[i])*16777619;
        return Color.HSVToRGB(.28f+(hash%1000)/1000f*.26f,.62f,1f);
    }
    public RouteRecord GetRoute(AgentController member){records.TryGetValue(member,out var route);return route;}
    public static string PlaceName(Vector3 point)
    {
        var city=CityGameplay.Instance;
        if(!city)return "STREET";
        if(CityGameplay.HomeMode&&HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.WorldBounds.Contains(new Vector3(point.x,HomeTerritorySystem.Instance.WorldBounds.center.y,point.z)))return "HOME";
        float distance=45f*45f;string best="STREET";
        if(city.Locations!=null)for(int i=0;i<city.Locations.Length&&i<city.LocationNames.Length;i++)
        {
            var delta=city.Locations[i]-point;delta.y=0;if(delta.sqrMagnitude>=distance)continue;
            distance=delta.sqrMagnitude;best=city.LocationNames[i];
        }
        return best;
    }
    public static string Describe(AgentController member)
    {
        if(!member)return "UNAVAILABLE";
        if(PolicePatrolVehicle.IsMounted(member))return "IN PATROL VAN";
        var route=Instance?Instance.GetRoute(member):null;
        if(route!=null&&route.Count>1)return $"{route.From} → {route.To} · {route.Metres:0}m";
        if(member.CurrentState==AgentController.State.MovingToPoint||member.CurrentState==AgentController.State.Retreating)
        {
            float distance=route?.Nav&&route.Nav.enabled&&route.Nav.isOnNavMesh?route.Nav.remainingDistance:0;
            return "TO "+PlaceName(member.CommandDestination)+(float.IsInfinity(distance)||distance<1?"":$" · {distance:0}m");
        }
        if(member.IsActivityLocked)return "WORKING ON SITE";
        return member.CurrentOrder!=AgentController.StandingOrder.None?member.CurrentOrder.ToString().ToUpperInvariant():member.CurrentState==AgentController.State.AutoAttacking?"IN COMBAT":"AT "+PlaceName(member.transform.position);
    }
    void Update()
    {
        if(!BattleManager.instance)return;
        if(tracked&&!tracked.IsAlive)StopTracking();
        if(Time.unscaledTime>=nextRoster){nextRoster=Time.unscaledTime+1f;RefreshRoster();}
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.18f;
        if(!frame)frame=WorldButtonLayer.Frame();
        VisibleRouteCount=0;
        foreach(var row in records.Values)RefreshRoute(row);
        UpdateIndicators();
    }
    void RefreshRoster()
    {
        var actors=BattleManager.instance.PlayerAgents;
        foreach(var actor in actors)
        {
            if(!actor||!actor.IsAlive||records.ContainsKey(actor)||records.Count>=MaximumRoutes)continue;
            records.Add(actor,new RouteRecord{Member=actor,Nav=actor.GetComponent<NavMeshAgent>()});
        }
        removed.Clear();foreach(var pair in records)
        {
            bool present=false;foreach(var actor in actors)if(actor==pair.Key){present=true;break;}
            if(!pair.Key||!pair.Key.IsAlive||!present)removed.Add(pair.Key);
        }
        foreach(var actor in removed){if(records[actor].Line)Destroy(records[actor].Line.gameObject);records.Remove(actor);}
    }
    void RefreshRoute(RouteRecord row)
    {
        var actor=row.Member;var nav=row.Nav;
        bool valid=IsWatched(actor)&&actor.gameObject.activeInHierarchy&&!PolicePatrolVehicle.IsMounted(actor)&&nav&&nav.enabled&&nav.isOnNavMesh&&nav.hasPath&&!nav.pathPending&&nav.pathStatus==NavMeshPathStatus.PathComplete;
        row.Count=0;row.Metres=0;
        if(valid)
        {
            row.Destination=nav.destination;
            if(row.Revision!=actor.OrderRevision){row.Revision=actor.OrderRevision;row.Origin=actor.transform.position;row.From=PlaceName(row.Origin);}
            row.To=PlaceName(row.Destination);
            row.Count=nav.path.GetCornersNonAlloc(row.Corners);
            if(row.Count>0)row.Corners[0]=actor.transform.position;
            for(int i=1;i<row.Count;i++)row.Metres+=Vector3.Distance(row.Corners[i-1],row.Corners[i]);
        }
        bool show=valid&&row.Count>1&&row.Metres>1f&&!LiveMiniMap.IsExpanded;
        if(!show){if(row.Line)row.Line.enabled=false;return;}
        if(!row.Line)row.Line=CreateLine(actor);
        row.Line.enabled=true;row.Line.positionCount=row.Count;
        for(int i=0;i<row.Count;i++)row.Line.SetPosition(i,row.Corners[i]+Vector3.up*.28f);
        VisibleRouteCount++;
    }
    LineRenderer CreateLine(AgentController member)
    {
        if(!routeMaterial)
        {
            var source=Resources.Load<Material>("Presentation/CrewRoute");
            routeMaterial=source?new Material(source):new Material(Shader.Find("Sprites/Default")??Shader.Find("Universal Render Pipeline/Unlit"));
        }
        var host=new GameObject(member.Data.AgentName+" actual remaining route");host.transform.SetParent(transform,false);
        var line=host.AddComponent<LineRenderer>();line.sharedMaterial=routeMaterial;line.useWorldSpace=true;line.alignment=LineAlignment.View;
        line.startWidth=.32f;line.endWidth=.22f;line.numCornerVertices=2;line.numCapVertices=2;
        var color=MemberColor(member);line.startColor=color;line.endColor=new Color(color.r,color.g,color.b,.55f);
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        return line;
    }
    void EnsureIndicators()
    {
        if(!frame)return;
        for(int i=0;i<edges.Length;i++)if(!edges[i])
        {
            int index=i;edges[i]=LandscapeUI.Button("CrewOffscreen"+i,frame,"",0,0,196,48,"dark");
            edges[i].onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();FocusMember(edgeMembers[index]);});
        }
        if(trackingBar)return;
        trackingBar=LandscapeUI.Panel("IndividualTracking",frame,920,204,585,62,true).rectTransform;
        trackingText=LandscapeUI.Text("Member",trackingBar,"",12,5,420,50,16,LandscapeUI.White,true);
        LandscapeUI.Button("StopFollow",trackingBar,"UNPIN",438,9,135,42,"dark").onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();StopTracking();});
    }
    void UpdateIndicators()
    {
        EnsureIndicators();if(!frame||!Camera.main)return;
        bool visible=!LiveMiniMap.IsExpanded&&!AgentSelectionManager.BlocksWorldTap();
        foreach(var button in edges)if(button)button.gameObject.SetActive(false);
        VisibleEdgeCount=0;
        if(trackingBar)
        {
            trackingBar.gameObject.SetActive(visible&&TrackedMember);
            if(TrackedMember)trackingText.text=(CameraPanTouchOnly.Instance&&CameraPanTouchOnly.Instance.IsFollowingMember?"FOLLOWING ":"PINNED ")+TrackedMember.Data.AgentName.ToUpperInvariant()+"\n"+Describe(TrackedMember);
        }
        if(!visible)return;
        occupiedEdges.Clear();
        for(int pass=0;pass<2;pass++)foreach(var row in records.Values)
        {
            var member=row.Member;
            if(!IsWatched(member)||(pass==0?member!=TrackedMember:member==TrackedMember)||VisibleEdgeCount>=edges.Length)continue;
            var p=Camera.main.WorldToScreenPoint(WorldPosition(member)+Vector3.up*2f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,p,null,out var local);
            var screen=new Vector2(local.x-frame.rect.xMin,frame.rect.yMax-local.y);
            var safe=new Rect(220,224,frame.rect.width-440,frame.rect.height-365);
            if(p.z>0&&safe.Contains(screen))continue;
            var center=safe.center;var direction=screen-center;if(p.z<=0)direction=-direction;
            if(direction.sqrMagnitude<.1f)direction=Vector2.down;
            float scale=Mathf.Min((safe.width*.5f)/Mathf.Max(.01f,Mathf.Abs(direction.x)),(safe.height*.5f)/Mathf.Max(.01f,Mathf.Abs(direction.y)));
            var edge=center+direction*scale;
            bool verticalEdge=Mathf.Abs(direction.x)>Mathf.Abs(direction.y),found=false;
            Rect area=default;
            for(int attempt=0;attempt<13;attempt++)
            {
                int offset=attempt==0?0:(attempt+1)/2*(attempt%2==1?1:-1);
                var candidate=edge;
                if(verticalEdge)candidate.y=Mathf.Clamp(edge.y+offset*54,safe.yMin,safe.yMax);
                else candidate.x=Mathf.Clamp(edge.x+offset*204,safe.xMin,safe.xMax);
                var proposed=new Rect(candidate.x-100,candidate.y-26,200,52);
                if(TrackedMember&&proposed.Overlaps(new Rect(916,200,593,70)))continue;
                bool overlaps=false;foreach(var placed in occupiedEdges)if(proposed.Overlaps(placed)){overlaps=true;break;}
                if(overlaps)continue;area=proposed;found=true;break;
            }
            if(!found)continue;occupiedEdges.Add(area);
            string arrow=Mathf.Abs(direction.x)>Mathf.Abs(direction.y)?(direction.x>0?">":"<"):(direction.y>0?"v":"^");
            var button=edges[VisibleEdgeCount];edgeMembers[VisibleEdgeCount]=member;
            var label=button.GetComponentInChildren<TextMeshProUGUI>();
            label.text=arrow+" "+member.Data.AgentName.ToUpperInvariant()+" · "+Vector3.Distance(WorldPosition(member),CityActivityStreaming.GroundFocus(Camera.main)).ToString("0")+"m\nTAP TO FIND";
            label.fontSize=15;label.color=MemberColor(member);
            LandscapeUI.Place((RectTransform)button.transform,area.x+2,area.y+2,196,48);button.gameObject.SetActive(true);VisibleEdgeCount++;
        }
    }
}
