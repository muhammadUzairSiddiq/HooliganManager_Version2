using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct CityHeatSample
{
    public Vector3 position;public int count;
    public CityHeatSample(Vector3 position,int count){this.position=position;this.count=count;}
}
public static class CitySupportNetwork
{
    public static readonly int[] Packages={30,100,300};
    static readonly int[] Prices={2500,7500,20000};
    public static int Capacity(PlayerData data)=>2000+200*(data?.CityCapturedZones?.Count??0);
    public static int CampaignPrice(PlayerData data,int tier=0)=>Prices[Mathf.Clamp(tier,0,2)]+500*(data?.SupportCampaigns??0);
    public static void Earn(PlayerData data,int count)
    {
        if(data==null||count<=0)return;int added=Mathf.Min(count,Capacity(data)-data.StrategySupporters);if(added<=0)return;
        data.StrategySupporters+=added;Object.FindFirstObjectByType<StadiumMatchdayActivity>()?.AddHomeSupporters(added);
    }
    public static bool Promote(int tier=0)
    {
        var data=GameManager.Data;if(data==null||tier<0||tier>=Packages.Length)return false;
        int price=CampaignPrice(data,tier),count=Packages[tier];
        if(data.Money<price||data.StrategySupporters+count>Capacity(data))return false;
        data.Money-=price;data.SupportCampaigns++;Earn(data,count);GameManager.Save();
        CityGameplay.Instance?.PostEvent($"+{count} SUPPORTERS JOINED · £{price:N0} CAMPAIGN");return true;
    }
    public static void Lost(string firm,bool home)
    {
        var data=GameManager.Data;if(data==null)return;
        if(home)data.StrategySupporters=Mathf.Max(0,data.StrategySupporters-1);
        else{var street=data.RivalStreets?.Find(s=>s!=null&&RivalGrowthSystem.BaseName(s.firmName)==RivalGrowthSystem.BaseName(firm));if(street!=null)street.supporters=Mathf.Max(0,street.supporters-1);}
    }
}

/// <summary>Full-screen pooled tactical UI; a small density raster updates only while open.</summary>
public sealed class CityStrategyOverview:MonoBehaviour
{
    sealed class Bubble
    {
        public string key;public RectTransform root;public FactionHeatBubble graphic;
        public TextMeshProUGUI name,total,hold,delta;public int previous=-1;
        public float deltaUntil,radius;public Vector2 position;public bool home;
    }
    GameObject panel;RectTransform safe,map,deltaLayer;TextMeshProUGUI cash,summary,notice;
    readonly List<Bubble> bubbles=new List<Bubble>(12);
    readonly List<CityHeatSample> samples=new List<CityHeatSample>(160);
    readonly Button[] purchaseButtons=new Button[3];readonly TextMeshProUGUI[] purchaseLabels=new TextMeshProUGUI[3];
    readonly float[] density=new float[192*108];readonly Color32[] pixels=new Color32[192*108];
    Texture2D heatTexture;GangArea[] areas;TerritoryControlPoint[] territories;
    StadiumCrowdRenderer crowd;StadiumMatchdayActivity activity;Bounds bounds;float nextRefresh,nextDiscovery;
    public bool IsOpen=>panel&&panel.activeSelf;
    public int OverlayOrder=>panel?panel.GetComponent<Canvas>().sortingOrder:0;
    public void Initialize(Transform frame)
    {
        panel=new GameObject("CityHeatmap",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=32767;
        LandscapeUI.ConfigureLandscapeScaler(panel.GetComponent<CanvasScaler>());
        var background=LandscapeUI.Image("Backdrop",panel.transform,0,0,1600,900,null,new Color(.025f,.035f,.055f));LandscapeUI.Stretch(background.rectTransform);background.raycastTarget=true;
        safe=LandscapeUI.Rect("SafeArea",panel.transform,0,0,1600,900);LandscapeUI.Stretch(safe);
        LandscapeUI.Text("HeatmapTitle",safe,"HEATMAP",38,20,520,55,38,Color.white,true);
        LandscapeUI.Text("Subtitle",safe,"LIVE CITY PRESSURE",40,76,650,24,17,LandscapeUI.Muted);
        cash=LandscapeUI.Text("Treasury",safe,"",1050,24,380,45,28,LandscapeUI.Gold,true,TextAlignmentOptions.Right);
        var close=LandscapeUI.Button("CloseHeatmap",safe,"X",1470,24,80,52,"dark");close.onClick.AddListener(Close);close.GetComponentInChildren<TextMeshProUGUI>().color=new Color(1,.2f,.2f);
        map=LandscapeUI.Rect("HeatmapField",safe,30,120,1540,540);
        LandscapeUI.Image("Field",map,0,0,1540,540,null,new Color(.035f,.065f,.085f));
        heatTexture=new Texture2D(192,108,TextureFormat.RGBA32,false){name="Live city density",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        var raster=LandscapeUI.Rect("Density",map,0,0,1540,540).gameObject.AddComponent<RawImage>();raster.texture=heatTexture;raster.raycastTarget=false;
        deltaLayer=LandscapeUI.Rect("LiveChanges",map,0,0,1540,540);
        summary=LandscapeUI.Text("MapSummary",safe,"",40,671,1500,28,18,LandscapeUI.Muted);
        string[] names={"PROMOTE","BOOST","MOBILIZE"};
        for(int i=0;i<3;i++)
        {
            int tier=i;float x=40+i*510;purchaseButtons[i]=LandscapeUI.Button("SupportPackage"+i,safe,names[i],x,721,490,61,"dark");
            purchaseButtons[i].onClick.AddListener(()=>{bool paid=CitySupportNetwork.Promote(tier);notice.text=paid?$"+{CitySupportNetwork.Packages[tier]} supporters are joining your city network.":"Not enough cash or support capacity.";notice.color=paid?LandscapeUI.Green:LandscapeUI.Red;Refresh();});
            purchaseLabels[i]=LandscapeUI.Text("PackageTerms"+i,safe,"",x,791,490,30,21,LandscapeUI.Gold,true,TextAlignmentOptions.Center);
        }
        notice=LandscapeUI.Text("HeatmapNotice",safe,"Rally and outreach earn support. Secured districts expand capacity.",40,846,1500,28,18,LandscapeUI.Muted,false,TextAlignmentOptions.Center);
        panel.SetActive(false);
    }
    public void Close(){if(panel)panel.SetActive(false);}
    public void Toggle(){if(IsOpen){Close();return;}panel.SetActive(true);FitSafeArea();Discover();Refresh();}
    void FitSafeArea()
    {
        Canvas.ForceUpdateCanvases();var area=Screen.safeArea;var rect=panel.GetComponent<RectTransform>().rect;
        float width=rect.width*area.width/Screen.width,height=rect.height*area.height/Screen.height;
        safe.anchorMin=safe.anchorMax=new Vector2(0,1);safe.pivot=new Vector2(0,1);safe.sizeDelta=new Vector2(1600,900);
        float scale=Mathf.Min(width/1600f,height/900f);safe.localScale=Vector3.one*scale;
        safe.anchoredPosition=new Vector2(rect.width*area.x/Screen.width+(width-1600*scale)*.5f,-rect.height*(Screen.height-area.yMax)/Screen.height-(height-900*scale)*.5f);
    }
    void Update()
    {
        if(!IsOpen)return;
        if(Time.unscaledTime>=nextDiscovery){nextDiscovery=Time.unscaledTime+5;Discover();FitSafeArea();}
        if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.5f;Refresh();}
        foreach(var b in bubbles)
        {
            if(!b.root.gameObject.activeSelf){b.delta.gameObject.SetActive(false);continue;}
            if(b.home)b.name.alpha=.65f+.35f*Mathf.Pow(Mathf.Sin(Time.unscaledTime*4),2);
            float remaining=b.deltaUntil-Time.unscaledTime;b.delta.gameObject.SetActive(remaining>0);
            if(remaining>0){b.delta.alpha=Mathf.Min(1,remaining);b.delta.rectTransform.anchoredPosition=new Vector2(b.position.x-130,-b.position.y+b.radius+30+(3-remaining)*12);}
        }
    }
    void Discover()
    {
        areas=FindObjectsByType<GangArea>(FindObjectsSortMode.None);territories=FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None);
        crowd=FindFirstObjectByType<StadiumCrowdRenderer>();activity=FindFirstObjectByType<StadiumMatchdayActivity>();
        bounds=new Bounds(CityGameplay.Instance?CityGameplay.Instance.Home:Vector3.zero,Vector3.one*100);
        if(CityGameplay.Instance)foreach(var p in CityGameplay.Instance.Locations)bounds.Encapsulate(p);
        foreach(var area in areas)if(area)bounds.Encapsulate(area.transform.position);bounds.Expand(50);
    }
    Vector2 Project(Vector3 p)=>new Vector2(70+Mathf.InverseLerp(bounds.min.x,bounds.max.x,p.x)*1400,55+(1-Mathf.InverseLerp(bounds.min.z,bounds.max.z,p.z))*430);
    void Refresh()
    {
        if(!IsOpen)return;var data=GameManager.Data;if(data==null)return;RivalGrowthSystem.Ensure(data);if(areas==null)Discover();
        foreach(var b in bubbles)b.root.gameObject.SetActive(false);
        int cityAreas=Mathf.Max(1,territories.Count(t=>t)+areas.Count(a=>a)+1),owned=territories.Count(t=>t&&t.IsCaptured)+1;
        int crew=BattleManager.instance?BattleManager.instance.PlayerAgents.Count(a=>a&&a.IsAlive):0;
        SetBubble("YOU","YOU",CityGameplay.Instance?CityGameplay.Instance.Home:Vector3.zero,LandscapeUI.Green,data.StrategySupporters,crew,owned/(float)cityAreas,true);
        foreach(var street in data.RivalStreets)
        {
            if(street==null||street.wiped)continue;var area=areas.FirstOrDefault(a=>a&&RivalGrowthSystem.BaseName(a.GangName)==RivalGrowthSystem.BaseName(street.firmName));if(!area)continue;
            int held=territories.Count(t=>t&&!t.IsCaptured&&RivalGrowthSystem.BaseName(t.owningFaction)==RivalGrowthSystem.BaseName(street.firmName))+1;
            SetBubble(street.firmName,street.firmName,area.transform.position,area.ZoneColor,street.supporters,street.members,held/(float)cityAreas,false);
        }
        ResolveOverlap();deltaLayer.SetAsLastSibling();DrawDensity();cash.text=$"£{data.Money:N0}";
        summary.text=$"ORANGE → RED: CROWD DENSITY                           CITY HOLD {owned*100/cityAreas}%     SUPPORT {data.StrategySupporters:N0}/{CitySupportNetwork.Capacity(data):N0}";
        for(int i=0;i<3;i++){int price=CitySupportNetwork.CampaignPrice(data,i),count=CitySupportNetwork.Packages[i];purchaseLabels[i].text=$"£{price:N0}   ·   +{count} SUPPORTERS";purchaseButtons[i].interactable=data.Money>=price&&data.StrategySupporters+count<=CitySupportNetwork.Capacity(data);}
    }
    void SetBubble(string key,string title,Vector3 world,Color color,int supporters,int members,float hold,bool home)
    {
        var b=bubbles.Find(item=>item.key==key);
        if(b==null)
        {
            b=new Bubble{key=key,home=home};bubbles.Add(b);b.root=LandscapeUI.Rect("Faction_"+key,map,0,0,200,200);
            b.graphic=b.root.gameObject.AddComponent<FactionHeatBubble>();b.graphic.raycastTarget=false;
            b.name=LandscapeUI.Text("FactionName",b.root,title,10,25,180,35,21,Color.white,true,TextAlignmentOptions.Center);
            b.total=LandscapeUI.Text("FactionNumbers",b.root,"",10,70,180,65,18,Color.white,false,TextAlignmentOptions.Center);
            b.hold=LandscapeUI.Text("CityHold",b.root,"",10,139,180,30,17,Color.white,true,TextAlignmentOptions.Center);
            b.delta=LandscapeUI.Text("LiveDelta",deltaLayer,"",0,0,260,38,26,LandscapeUI.Green,true,TextAlignmentOptions.Center);
        }
        b.root.gameObject.SetActive(true);int total=supporters+members;
        if(b.previous>=0&&total!=b.previous){int change=total-b.previous;b.delta.text=(change>0?"+":"")+change+(change>0?" JOINED":" LOST");b.delta.color=change>0?LandscapeUI.Green:new Color(1,.18f,.15f);b.deltaUntil=Time.unscaledTime+3;}
        b.previous=total;b.radius=Mathf.Clamp(66+Mathf.Sqrt(total)*3,78,135);b.position=Project(world);float diameter=b.radius*2;b.root.sizeDelta=new Vector2(diameter,diameter);
        LandscapeUI.Place(b.name.rectTransform,8,diameter*.16f,diameter-16,32);b.name.text=title.ToUpperInvariant();b.name.enableAutoSizing=true;b.name.fontSizeMin=13;b.name.fontSizeMax=21;
        LandscapeUI.Place(b.total.rectTransform,8,diameter*.37f,diameter-16,58);b.total.text=$"{supporters:N0} SUPPORT\n{members:N0} CREW";
        LandscapeUI.Place(b.hold.rectTransform,8,diameter*.74f,diameter-16,24);b.hold.text=$"HOLD {hold:P0}";b.graphic.Set(color,hold,home);
    }
    void ResolveOverlap()
    {
        for(int pass=0;pass<18;pass++)for(int i=0;i<bubbles.Count;i++)
        {
            var a=bubbles[i];if(!a.root.gameObject.activeSelf)continue;
            for(int j=i+1;j<bubbles.Count;j++){var b=bubbles[j];if(!b.root.gameObject.activeSelf)continue;var delta=b.position-a.position;float distance=delta.magnitude,needed=a.radius+b.radius+18;if(distance>=needed)continue;var direction=distance>.01f?delta/distance:new Vector2(i%2==0?1:-1,.3f).normalized;var push=direction*(needed-distance)*.52f;a.position-=push;b.position+=push;}
            a.position=new Vector2(Mathf.Clamp(a.position.x,a.radius+5,1540-a.radius-5),Mathf.Clamp(a.position.y,a.radius+5,540-a.radius-5));
        }
        foreach(var b in bubbles)if(b.root.gameObject.activeSelf)b.root.anchoredPosition=new Vector2(b.position.x-b.radius,-b.position.y+b.radius);
    }
    void DrawDensity()
    {
        System.Array.Clear(density,0,density.Length);samples.Clear();if(crowd)crowd.AppendHeat(samples);if(activity)activity.AppendHeat(samples);
        if(BattleManager.instance)foreach(var member in BattleManager.instance.PlayerAgents)if(member&&member.IsAlive)samples.Add(new CityHeatSample(member.transform.position,1));
        foreach(var sample in samples)
        {
            int x=Mathf.RoundToInt(Mathf.InverseLerp(bounds.min.x,bounds.max.x,sample.position.x)*191),y=Mathf.RoundToInt(Mathf.InverseLerp(bounds.min.z,bounds.max.z,sample.position.z)*107);
            const int radius=12;
            for(int py=Mathf.Max(0,y-radius);py<=Mathf.Min(107,y+radius);py++)for(int px=Mathf.Max(0,x-radius);px<=Mathf.Min(191,x+radius);px++){float r=((px-x)*(px-x)+(py-y)*(py-y))/(float)(radius*radius);if(r<1)density[py*192+px]+=sample.count*(1-r)*(1-r);}
        }
        for(int i=0;i<pixels.Length;i++){float amount=1-Mathf.Exp(-density[i]/18f);var color=Color.Lerp(new Color(1,.48f,.025f),new Color(.95f,.035f,.015f),amount);color.a=amount*.85f;pixels[i]=color;}
        heatTexture.SetPixels32(pixels);heatTexture.Apply(false,false);
    }
    void OnDestroy(){if(heatTexture)Destroy(heatTexture);if(panel)Destroy(panel);}
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class FactionHeatBubble:MaskableGraphic
{
    Color faction;float hold;bool player;
    public void Set(Color faction,float hold,bool player){this.faction=faction;this.hold=Mathf.Clamp01(hold);this.player=player;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var center=rectTransform.rect.center;float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;const int segments=64;
        var fill=new Color(faction.r*.14f,faction.g*.14f,faction.b*.14f,.96f);vh.AddVert(center,fill,Vector2.zero);
        for(int i=0;i<=segments;i++){float angle=i*Mathf.PI*2/segments;vh.AddVert(center+new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*(radius-5),fill,Vector2.zero);if(i>0)vh.AddTriangle(0,i,i+1);}
        for(int i=0;i<segments;i++)
        {
            float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;var tint=i/(float)segments<hold?Color.Lerp(faction,Color.white,.35f):new Color(faction.r,faction.g,faction.b,player?.9f:.65f);float thickness=i/(float)segments<hold?6:2;int k=vh.currentVertCount;
            vh.AddVert(center+new Vector2(Mathf.Sin(a),Mathf.Cos(a))*radius,tint,Vector2.zero);vh.AddVert(center+new Vector2(Mathf.Sin(b),Mathf.Cos(b))*radius,tint,Vector2.zero);vh.AddVert(center+new Vector2(Mathf.Sin(b),Mathf.Cos(b))*(radius-thickness),tint,Vector2.zero);vh.AddVert(center+new Vector2(Mathf.Sin(a),Mathf.Cos(a))*(radius-thickness),tint,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
        }
    }
}
