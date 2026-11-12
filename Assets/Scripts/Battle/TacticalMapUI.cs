using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>The same top-down projection is used by the feed, markers, routes and input.</summary>
public static class TacticalMapProjection
{
    public static Vector2 WorldToMap(Camera camera,Vector3 point)
    {
        if(!camera)return Vector2.one*.5f;
        float height=Mathf.Max(.01f,camera.orthographicSize)*2f;
        return new Vector2(.5f+(point.x-camera.transform.position.x)/(height*Mathf.Max(.01f,camera.aspect)),
            .5f+(point.z-camera.transform.position.z)/height);
    }
    public static Vector3 MapToWorld(Camera camera,Vector2 point,float y=0)
    {
        if(!camera)return new Vector3(0,y,0);
        float height=Mathf.Max(.01f,camera.orthographicSize)*2f;
        return new Vector3(camera.transform.position.x+(point.x-.5f)*height*Mathf.Max(.01f,camera.aspect),y,
            camera.transform.position.z+(point.y-.5f)*height);
    }
}

/// <summary>Live individual selection and deliberate map actions; the city keeps simulating.</summary>
public sealed class TacticalMapUI:MonoBehaviour
{
    LiveMiniMap owner;
    GameObject root;
    RectTransform crewContent;
    RectTransform crewPanel;
    ScrollRect crewScroll;
    TextMeshProUGUI legend;
    Vector2 lastLayout;
    TextMeshProUGUI status,routeCaption,rerouteCaption,homeCaption;
    float next;
    readonly List<CrewRow> rows=new List<CrewRow>();
    sealed class CrewRow
    {
        public AgentController actor;
        public GameObject root;
        public Button select;
        public TextMeshProUGUI name,detail,track;
        public Image identity;
    }
    public int VisibleCrewCount{get;private set;}
    public void Initialize(LiveMiniMap map,Transform parent)
    {
        owner=map;root=LandscapeUI.Rect("TacticalMapControls",parent,0,0,1600,900).gameObject;
        LandscapeUI.Stretch(root.GetComponent<RectTransform>());
        Text("Title","TACTICAL CITY",20,18,300,36,28,LandscapeUI.White);
        Text("Subtitle","Live positions · your crew, your decisions",20,58,310,24,15,LandscapeUI.Muted);
        homeCaption=Action("Home","SHOW HOME",336,20,128,54,owner.ShowHomeOnMap).GetComponentInChildren<TextMeshProUGUI>();
        Action("City","WHOLE CITY",476,20,128,54,owner.ShowWholeCity);
        var routes=Action("Routes","ROUTES: SELECTED",616,20,230,54,()=>{
            owner.TacticalOverlay.CycleRouteMode();RefreshCrew();
            SetStatus(owner.TacticalOverlay.RouteMode==TacticalMapOverlay.Routes.Selected?"Showing actual routes for selected and tracked crew.":"Route view: "+owner.TacticalOverlay.RouteModeLabel+". Existing crew orders stay unchanged.");});
        routeCaption=routes.GetComponentInChildren<TextMeshProUGUI>();
        var reroute=Action("Reroute","REROUTE SELECTED",860,20,244,54,()=>owner.SetRerouteMode(!owner.RerouteMode));
        rerouteCaption=reroute.GetComponentInChildren<TextMeshProUGUI>();
        var panel=crewPanel=LandscapeUI.Panel("TacticalCrew",root.transform,1230,98,350,650,true).rectTransform;
        LandscapeUI.Text("Heading",panel,"CREW · LIVE POSITIONS",14,12,322,32,21,LandscapeUI.White,true);
        crewScroll=LandscapeUI.Scroll("CrewList",panel,10,58,330,576);crewContent=crewScroll.content;
        status=Text("Status","Explore the map. Tap a crew marker/name to add or remove that member.",20,754,1560,42,19,LandscapeUI.White);
        legend=Text("Legend","GREEN BOX: HOME / OWNED TURF    AMBER: UNCLAIMED    RED: RIVAL    BLUE: POLICE    WHITE: CAMERA    LINE + SQUARE: ACTUAL ROUTE / DESTINATION",20,804,1560,28,16,LandscapeUI.Muted);
        Layout();
    }
    TextMeshProUGUI Text(string name,string text,float x,float y,float w,float h,int size,Color color)
        =>LandscapeUI.Text(name,root.transform,text,x,y,w,h,size,color,true);
    Button Action(string name,string caption,float x,float y,float w,float h,UnityEngine.Events.UnityAction action)
    {
        var button=LandscapeUI.Button(name,root.transform,caption,x,y,w,h,"dark");
        button.onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();action();});return button;
    }
    public void SetVisible(bool visible)
    {
        if(root)root.SetActive(visible);
        if(visible){Layout();SetStatus("Explore the map. Crew markers and names toggle individual selection; REROUTE arms one street order.");RefreshCrew();}
    }
    public void SetStatus(string text){if(status)status.text=text;}
    public void Tick(){if(!LiveMiniMap.IsExpanded)return;Layout();if(Time.unscaledTime>=next){next=Time.unscaledTime+.2f;RefreshCrew();}}
    void Layout()
    {
        if(!root||!crewPanel)return;var size=root.GetComponent<RectTransform>().rect.size;
        if(size==lastLayout)return;lastLayout=size;
        crewPanel.anchorMin=crewPanel.anchorMax=new Vector2(1,1);crewPanel.pivot=new Vector2(1,1);
        crewPanel.anchoredPosition=new Vector2(-20,-98);crewPanel.sizeDelta=new Vector2(350,Mathf.Max(300,size.y-252));
        var scroll=crewScroll.GetComponent<RectTransform>();scroll.anchorMin=Vector2.zero;scroll.anchorMax=Vector2.one;scroll.offsetMin=new Vector2(10,14);scroll.offsetMax=new Vector2(-10,-58);
        var indicator=scroll.Find("ScrollIndicator") as RectTransform;
        if(indicator){indicator.anchorMin=new Vector2(1,0);indicator.anchorMax=new Vector2(1,1);indicator.pivot=new Vector2(1,.5f);indicator.offsetMin=new Vector2(-13,6);indicator.offsetMax=new Vector2(-3,-6);}
        PinBottom(status.rectTransform,96,42);PinBottom(legend.rectTransform,62,28);
    }
    static void PinBottom(RectTransform rect,float bottom,float height)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,bottom);rect.sizeDelta=new Vector2(-40,height);}
    public void RefreshCrew()
    {
        if(!root||!root.activeSelf||!BattleManager.instance)return;
        var crew=BattleManager.instance.PlayerAgents;int used=0;
        for(int i=0;i<crew.Count&&used<64;i++)
        {
            var actor=crew[i];if(!actor||!actor.IsAlive)continue;
            CrewRow row=used<rows.Count?rows[used]:CreateRow();used++;row.actor=actor;row.root.SetActive(true);
            bool mounted=PolicePatrolVehicle.IsMounted(actor);
            row.name.text=actor.Data.AgentName+(mounted?" · IN VAN":actor.IsSelected?" · SELECTED":"");
            row.select.interactable=!mounted;
            row.select.GetComponent<Image>().color=actor.IsSelected?new Color(.08f,.42f,.28f):new Color(.07f,.12f,.14f);
            row.detail.text=$"{actor.CurrentHp:0} HP · "+CrewTrackingSystem.Describe(actor);row.identity.color=CrewTrackingSystem.MemberColor(actor);
            row.track.text=CrewTrackingSystem.TrackedMember==actor?"TRACKING":"TRACK";
        }
        VisibleCrewCount=used;for(int i=used;i<rows.Count;i++)rows[i].root.SetActive(false);
        if(routeCaption)routeCaption.text="ROUTES: "+owner.TacticalOverlay.RouteModeLabel;
        if(rerouteCaption)rerouteCaption.text=owner.RerouteMode?"CANCEL REROUTE":"REROUTE SELECTED";
        if(homeCaption)homeCaption.text=PoliceCityMode.Active?"STATION":CityGameplay.HomeMode?"SHOW HOME":"ARRIVAL";
    }
    CrewRow CreateRow()
    {
        var row=new CrewRow();var panel=LandscapeUI.Panel("CrewMember",crewContent,0,0,306,164,true);
        row.root=panel.gameObject;LandscapeUI.LayoutSize(row.root,306,164);
        row.identity=LandscapeUI.Image("MemberColor",panel.transform,0,4,4,156,null,Color.white);row.identity.raycastTarget=false;
        row.select=LandscapeUI.Button("ToggleSelection",panel.transform,"",8,7,290,46,"dark");
        row.name=row.select.GetComponentInChildren<TextMeshProUGUI>();row.name.fontSize=18;
        row.select.onClick.AddListener(()=>owner.ToggleMapMember(row.actor));
        var focus=LandscapeUI.Button("FocusMember",panel.transform,"FOCUS",8,60,136,44,"dark");
        focus.onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();owner.FocusMember(row.actor);});
        var track=LandscapeUI.Button("TrackMember",panel.transform,"TRACK",154,60,144,44,"dark");
        track.onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();owner.TrackMember(row.actor);});row.track=track.GetComponentInChildren<TextMeshProUGUI>();
        row.detail=LandscapeUI.Text("Duty",panel.transform,"",8,112,290,40,15,LandscapeUI.Muted,true);
        rows.Add(row);return row;
    }
}

/// <summary>A clickable marker never turns selection into a hidden movement command.</summary>
public sealed class TacticalMapMemberTarget:MonoBehaviour,IPointerClickHandler,IPointerDownHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
{
    LiveMiniMap owner;
    AgentController actor;
    public AgentController Member=>actor;
    Vector2 pressed;
    bool dragged;
    public void Bind(LiveMiniMap map,AgentController member){owner=map;actor=member;}
    public void OnPointerDown(PointerEventData data){AgentSelectionManager.ConsumeUiPointer();pressed=data.position;dragged=false;}
    public void OnBeginDrag(PointerEventData data){dragged=true;owner.OnMapPointerDown(pressed);}
    public void OnDrag(PointerEventData data){if(dragged)owner.OnMapPointerDrag(data.position);}
    public void OnEndDrag(PointerEventData data){if(dragged)owner.OnMapPointerUp();}
    public void OnPointerClick(PointerEventData data)
    {
        AgentSelectionManager.ConsumeUiPointer();
        if(LiveMiniMap.IsExpanded&&actor&&!dragged){owner.ToggleMapMember(actor);data.Use();}
    }
}

/// <summary>Bounded, cached real navigation paths plus authoritative world footprints.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TacticalMapOverlay:MaskableGraphic
{
    public const int MaximumCompactRoutes=3,MaximumExpandedRoutes=32;
    public enum Routes{Selected,Tracked,All,Off}
    public Routes RouteMode{get;private set;}=Routes.Selected;
    public string RouteModeLabel=>RouteMode.ToString().ToUpperInvariant();
    public int VisibleRouteCount{get;private set;}
    public bool HasHomeFootprint=>CityGameplay.HomeMode&&!PoliceCityMode.Active&&HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.WorldBounds.size.x>0;
    LiveMiniMap owner;
    float nextSnapshot;
    readonly Dictionary<AgentController,RouteCache> cache=new Dictionary<AgentController,RouteCache>();
    readonly List<RouteCache> visible=new List<RouteCache>(MaximumExpandedRoutes);
    readonly List<AgentController> expired=new List<AgentController>();
    Vector3 lastPosition,lastMainPosition;
    Quaternion lastMainRotation;
    float lastZoom,lastAspect;
    readonly Vector2[] cameraCorners=new Vector2[4];
    sealed class RouteCache
    {
        public AgentController actor;
        public NavMeshAgent nav;
        public readonly Vector3[] corners=new Vector3[CrewTrackingSystem.MaximumCorners];
        public int count,next;
    }
    protected override void Awake(){base.Awake();raycastTarget=false;}
    public void Initialize(LiveMiniMap map){owner=map;raycastTarget=false;}
    public void CycleRouteMode(){RouteMode=(Routes)(((int)RouteMode+1)%4);RefreshSnapshot();}
    public void SetRouteMode(Routes mode){RouteMode=mode;RefreshSnapshot();}
    public void Tick()
    {
        if(!owner||!owner.MapCamera)return;
        if(Time.unscaledTime>=nextSnapshot){nextSnapshot=Time.unscaledTime+.15f;RefreshSnapshot();}
        var camera=owner.MapCamera;var main=Camera.main;
        if(lastPosition!=camera.transform.position||lastZoom!=camera.orthographicSize||lastAspect!=camera.aspect||
            main&&(lastMainPosition!=main.transform.position||lastMainRotation!=main.transform.rotation))
        {
            lastPosition=camera.transform.position;lastZoom=camera.orthographicSize;lastAspect=camera.aspect;
            if(main){lastMainPosition=main.transform.position;lastMainRotation=main.transform.rotation;}
            SetVerticesDirty();
        }
    }
    public void RefreshSnapshot()
    {
        visible.Clear();VisibleRouteCount=0;
        var battle=BattleManager.instance;
        bool expanded=LiveMiniMap.IsExpanded;
        int limit=expanded?MaximumExpandedRoutes:MaximumCompactRoutes;
        if(battle&&RouteMode!=Routes.Off)
        {
            var crew=battle.PlayerAgents;
            // The small map remains useful without becoming an all-crew dump:
            // pin first, then selected members. All is an expanded-map option.
            for(int pass=0;pass<(expanded?1:2);pass++)
            for(int i=0;i<crew.Count&&visible.Count<limit;i++)
            {
                var actor=crew[i];if(!actor||!actor.IsAlive||PolicePatrolVehicle.IsMounted(actor))continue;
                bool tracked=CrewTrackingSystem.TrackedMember==actor;
                if(expanded)
                {if(RouteMode==Routes.Tracked&&!tracked||RouteMode==Routes.Selected&&!actor.IsSelected&&!tracked)continue;}
                else if(pass==0?!tracked:tracked||!actor.IsSelected||RouteMode==Routes.Tracked)continue;
                if(!cache.TryGetValue(actor,out var route))
                {route=new RouteCache{actor=actor,nav=actor.GetComponent<NavMeshAgent>()};cache.Add(actor,route);}
                var nav=route.nav;
                if(!nav||!nav.enabled||!nav.isOnNavMesh||!nav.hasPath||nav.pathPending||nav.pathStatus!=NavMeshPathStatus.PathComplete)continue;
                var path=nav.path;route.count=path.GetCornersNonAlloc(route.corners);
                if(route.count<2||route.count==route.corners.Length&&(route.corners[route.count-1]-nav.destination).sqrMagnitude>1)continue;
                // The steering corner is the next unvisited part of the real path.
                // Do not draw a straight destination line through buildings or
                // redraw completed legs behind a moving person.
                int next=1;float nearest=float.MaxValue;Vector3 steer=nav.steeringTarget;
                for(int corner=1;corner<route.count;corner++)
                {float distance=(route.corners[corner]-steer).sqrMagnitude;if(distance<nearest){nearest=distance;next=corner;}}
                route.next=next;visible.Add(route);
            }
        }
        expired.Clear();foreach(var entry in cache)if(!entry.Key)expired.Add(entry.Key);foreach(var actor in expired)cache.Remove(actor);
        VisibleRouteCount=visible.Count;SetVerticesDirty();
    }
    public bool TryGetRoute(AgentController actor,out Vector3 start,out Vector3 destination,out int segments)
    {
        foreach(var route in visible)if(route.actor==actor)
        {start=actor.transform.position;destination=route.corners[route.count-1];segments=route.count-route.next;return true;}
        start=destination=default;segments=0;return false;
    }
    Vector2 Point(Vector3 world)
    {
        var uv=owner.WorldToMap(world);var rect=rectTransform.rect;
        return new Vector2(rect.xMin+uv.x*rect.width,rect.yMin+uv.y*rect.height);
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();if(!owner||!owner.MapCamera)return;
        var home=HomeTerritorySystem.Instance;
        if(home&&CityGameplay.HomeMode&&!PoliceCityMode.Active)BoundsBox(mesh,home.WorldBounds,new Color(.12f,1,.48f,.95f),true);
        if(LiveMiniMap.IsExpanded&&owner.IsLayerVisible("AREAS"))
        foreach(var territory in owner.Territories)
        {
            if(!territory||!territory.gameObject.activeInHierarchy)continue;
            var color=territory.IsCaptured?new Color(.15f,1,.48f,.85f):string.IsNullOrEmpty(territory.owningFaction)?new Color(1,.75f,.15f,.75f):new Color(1,.25f,.16f,.8f);
            BoundsBox(mesh,new Bounds(territory.transform.position,new Vector3(territory.detectionRadius*2,0,territory.detectionRadius*2)),color,false);
        }
        foreach(var route in visible)
        {
            if(!route.actor||!route.actor.IsAlive)continue;
            var color=CrewTrackingSystem.MemberColor(route.actor);color.a=(route.actor.IsSelected||CrewTrackingSystem.TrackedMember==route.actor) ? .95f : .55f;
            var previous=Point(route.actor.transform.position);
            float width=LiveMiniMap.IsExpanded?2.4f:1.4f,size=LiveMiniMap.IsExpanded?8:4;
            for(int i=route.next;i<route.count;i++){var current=Point(route.corners[i]);Line(mesh,previous,current,color,width);previous=current;}
            Box(mesh,previous,new Vector2(size,size),color);
        }
        var main=Camera.main;if(!main)return;
        var plane=new Plane(Vector3.up,Vector3.zero);var points=cameraCorners;
        for(int i=0;i<4;i++)
        {var ray=main.ViewportPointToRay(new Vector3(i==1||i==2?1:0,i>=2?1:0,0));if(!plane.Raycast(ray,out var distance))return;points[i]=Point(ray.GetPoint(distance));}
        for(int i=0;i<4;i++)Line(mesh,points[i],points[(i+1)%4],new Color(1,1,1,.85f),1.5f);
    }
    void BoundsBox(VertexHelper mesh,Bounds bounds,Color color,bool home)
    {
        var a=Point(new Vector3(bounds.min.x,0,bounds.min.z));var b=Point(new Vector3(bounds.max.x,0,bounds.min.z));
        var c=Point(new Vector3(bounds.max.x,0,bounds.max.z));var d=Point(new Vector3(bounds.min.x,0,bounds.max.z));
        if(home){var tint=color;tint.a=.09f;Quad(mesh,a,b,c,d,tint);}
        Line(mesh,a,b,color,home?3:1.5f);Line(mesh,b,c,color,home?3:1.5f);Line(mesh,c,d,color,home?3:1.5f);Line(mesh,d,a,color,home?3:1.5f);
    }
    static void Box(VertexHelper mesh,Vector2 center,Vector2 size,Color color)
    {var half=size*.5f;Quad(mesh,center-half,center+new Vector2(half.x,-half.y),center+half,center+new Vector2(-half.x,half.y),color);}
    static void Line(VertexHelper mesh,Vector2 a,Vector2 b,Color color,float width)
    {
        if((b-a).sqrMagnitude<.01f)return;var dir=(b-a).normalized;var n=new Vector2(-dir.y,dir.x)*width*.5f;
        Quad(mesh,a+n,b+n,b-n,a-n,color);
    }
    static void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
    {
        int start=mesh.currentVertCount;mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);mesh.AddVert(c,color,Vector2.zero);mesh.AddVert(d,color,Vector2.zero);
        mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
    }
}
