using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Bounded selected/pinned identities; names never become extra input targets.</summary>
public sealed class CrewWorldLabels:MonoBehaviour
{
    public const int MaximumVisibleLabels=6;
    public int VisibleLabelCount{get;private set;}
    public int CachedLabelCount=>labels.Count;
    readonly Dictionary<AgentController,TextMeshProUGUI> labels=new Dictionary<AgentController,TextMeshProUGUI>();
    readonly List<AgentController> candidates=new List<AgentController>(32),removed=new List<AgentController>();
    readonly List<Rect> occupied=new List<Rect>(16);
    readonly RectTransform[] edgeIndicators=new RectTransform[5];
    readonly Vector3[] uiCorners=new Vector3[4];
    RectTransform frame,trackingBar;
    float nextRefresh,nextCleanup,nextUiLookup;
    public bool TryGetLabel(AgentController member,out TextMeshProUGUI label)=>labels.TryGetValue(member,out label);
    void LateUpdate()
    {
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;
        HideLabels();var battle=BattleManager.instance;var cam=Camera.main;
        if(!battle||!cam||AgentSelectionManager.BlocksWorldTap())return;
        if(!frame)frame=WorldButtonLayer.Frame();if(!frame)return;
        if(Time.unscaledTime>=nextCleanup){nextCleanup=Time.unscaledTime+1;Prune(battle.PlayerAgents);}
        RefreshUiReferences();occupied.Clear();AddOccupied(trackingBar);
        foreach(var indicator in edgeIndicators)AddOccupied(indicator);
        if(LiveMiniMap.Instance&&!LiveMiniMap.IsExpanded)AddOccupied(LiveMiniMap.Instance.MapFrame);
        candidates.Clear();var tracked=CrewTrackingSystem.TrackedMember;
        if(Eligible(tracked)&&ContainsCrew(battle.PlayerAgents,tracked))candidates.Add(tracked);
        foreach(var member in battle.PlayerAgents)
            if(Eligible(member)&&member.IsSelected&&member!=tracked&&candidates.Count<64)candidates.Add(member);
        foreach(var member in candidates)
        {
            var p=cam.WorldToScreenPoint(CrewTrackingSystem.WorldPosition(member)+Vector3.up*5.5f);
            if(p.z<=0)continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,p,null,out var local);
            float x=local.x-frame.rect.xMin,y=frame.rect.yMax-local.y;
            Rect area=default;bool placed=false;
            for(int slot=0;slot<3&&!placed;slot++)
            {
                area=new Rect(x-112,y-46-slot*46,224,44);
                if(area.xMin<70||area.xMax>frame.rect.width-70||area.yMin<205||area.yMax>frame.rect.height-155)continue;
                placed=!Overlaps(area);
            }
            if(!placed)continue;
            if(!labels.TryGetValue(member,out var label)||!label)
            {
                label=LandscapeUI.Text("CrewIdentity",frame,"",0,0,224,44,16,LandscapeUI.White,true);
                label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
                label.outlineColor=Color.black;label.outlineWidth=.24f;
                labels[member]=label;
            }
            label.text=(member.Data?.AgentName??"CREW")+(member==tracked?" · PINNED":" · SELECTED")+"\n"+DestinationDetail(member);
            label.color=PoliceCityMode.Active?new Color(.45f,.74f,1):CrewTrackingSystem.MemberColor(member);
            LandscapeUI.Place(label.rectTransform,area.x,area.y,area.width,area.height);
            label.gameObject.SetActive(true);occupied.Add(area);if(++VisibleLabelCount>=MaximumVisibleLabels)break;
        }
    }
    static bool Eligible(AgentController member)=>member&&member.IsAlive&&member.gameObject.activeInHierarchy&&!PolicePatrolVehicle.IsMounted(member);
    static bool ContainsCrew(IReadOnlyList<AgentController> crew,AgentController member)
    {for(int i=0;i<crew.Count;i++)if(crew[i]==member)return true;return false;}
    static string DestinationDetail(AgentController member)
    {
        var route=CrewTrackingSystem.Instance?CrewTrackingSystem.Instance.GetRoute(member):null;
        if(route!=null&&route.Revision==member.OrderRevision&&route.Count>1&&route.Metres>1&&!float.IsInfinity(route.Metres)&&!float.IsNaN(route.Metres))return "TO "+ShortPlace(route.To)+$" · {route.Metres:0}m";
        if(member.CurrentState==AgentController.State.MovingToPoint||member.CurrentState==AgentController.State.Retreating)return "TO "+ShortPlace(CrewTrackingSystem.PlaceName(member.CommandDestination));
        if(member.IsActivityLocked)return "WORKING ON SITE";
        if(member.CurrentState==AgentController.State.AutoAttacking)return "IN COMBAT";
        if(member.CurrentOrder!=AgentController.StandingOrder.None)return member.CurrentOrder.ToString().ToUpperInvariant()+" · "+ShortPlace(CrewTrackingSystem.PlaceName(member.transform.position));
        return "AT "+ShortPlace(CrewTrackingSystem.PlaceName(member.transform.position));
    }
    static string ShortPlace(string place)=>string.IsNullOrEmpty(place)?"STREET":place.Length<=20?place:place.Substring(0,17)+"...";
    bool Overlaps(Rect area){for(int i=0;i<occupied.Count;i++)if(occupied[i].Overlaps(area))return true;return false;}
    void RefreshUiReferences()
    {
        if(Time.unscaledTime<nextUiLookup)return;nextUiLookup=Time.unscaledTime+1;
        if(!trackingBar)trackingBar=frame.Find("IndividualTracking") as RectTransform;
        for(int i=0;i<edgeIndicators.Length;i++)if(!edgeIndicators[i])edgeIndicators[i]=frame.Find("CrewOffscreen"+i) as RectTransform;
    }
    void AddOccupied(RectTransform ui)
    {
        if(!ui||!ui.gameObject.activeInHierarchy)return;
        ui.GetWorldCorners(uiCorners);float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
        for(int i=0;i<4;i++)
        {
            var screen=RectTransformUtility.WorldToScreenPoint(null,uiCorners[i]);
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,screen,null,out var local))continue;
            float x=local.x-frame.rect.xMin,y=frame.rect.yMax-local.y;
            minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
        }
        if(minX<float.MaxValue)occupied.Add(Rect.MinMaxRect(minX-4,minY-4,maxX+4,maxY+4));
    }
    void HideLabels(){VisibleLabelCount=0;foreach(var label in labels.Values)if(label)label.gameObject.SetActive(false);}
    void Prune(IReadOnlyList<AgentController> crew)
    {
        removed.Clear();foreach(var entry in labels)
        {
            bool present=false;if(entry.Key&&entry.Key.IsAlive)for(int i=0;i<crew.Count;i++)if(crew[i]==entry.Key){present=true;break;}
            if(!present)removed.Add(entry.Key);
        }
        foreach(var member in removed){if(labels[member])Destroy(labels[member].gameObject);labels.Remove(member);}
    }
    void OnDisable(){HideLabels();}
    void OnDestroy(){foreach(var label in labels.Values)if(label)Destroy(label.gameObject);}
}
