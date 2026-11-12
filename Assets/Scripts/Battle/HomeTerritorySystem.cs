using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

[Serializable] public sealed class HomeBuildingState
{
    public int site,level,targetLevel;
    public bool complete,constructing;
    public float workSeconds;
    public List<string> workerIds=new List<string>();
}

/// <summary>Permanent, visible Home development built by actual assigned crew.</summary>
public sealed class HomeTerritorySystem:MonoBehaviour
{
    public static HomeTerritorySystem Instance{get;private set;}
    public static readonly string[] Names={"FIRM HEADQUARTERS","SUPPLY YARD","TRAINING GROUND"};
    public static readonly int[] Costs={0,300,450};
    public static readonly float[] Durations={12,18,20};
    public const float WorkRadius=7f;
    public readonly Vector3[] Sites=new Vector3[3];
    public bool IsBuilding=>assigned!=null;
    public int ActiveSite{get;private set;}=-1;
    public float Progress{get;private set;}
    public string Status{get;private set;}="Select crew, open HOME, then BUILD your headquarters.";
    /// <summary>The same animated XZ footprint rendered in the world and read by the tactical map.</summary>
    public Bounds WorldBounds{get;private set;}
    public Bounds TargetBounds{get;private set;}
    public float DevelopmentFraction
    {
        get
        {
            var d=GameManager.Data;Ensure(d);
            float levels=d?.HomeBuildings?.Where(b=>b!=null).Sum(b=>b.level)??0;
            if(IsBuilding)levels+=Mathf.Clamp01(Progress/WorkDuration(d,ActiveSite));
            else
            {
                var saved=d?.HomeBuildings?.FirstOrDefault(b=>b!=null&&b.constructing);
                if(saved!=null)levels+=Mathf.Clamp01(saved.workSeconds/WorkDuration(d,saved.site));
            }
            return Mathf.Clamp01(levels/9f);
        }
    }
    AgentController[] assigned;
    int[] revisions;
    float travel,saveClock;
    readonly GameObject[] facilities=new GameObject[3];
    readonly MeshRenderer[][] facilityRenderers=new MeshRenderer[3][];
    readonly Vector3[] borderPoints=new Vector3[4];
    readonly Vector3[] primaryAnchors=new Vector3[3];
    readonly TextMeshPro[] plotLabels=new TextMeshPro[3];
    static readonly string[] PlotNames={"HQ","SUPPLY YARD","TRAINING"};
    GameObject workModel;
    Vector3 workScale;
    TextMeshPro sign,workLabel;
    Bounds reservedBounds;
    LineRenderer homeBorder;
    Transform homeTint;
    Material homeTintMaterial;
    Mesh homeTintMesh;
    GameObject scaffold;
    ParticleSystem workDust;
    Material dustMaterial;
    Material scaffoldMaterial;
    float nextFootprint;
    public static void Ensure(PlayerData d)
    {
        if(d==null)return;d.HomeBuildings??=new List<HomeBuildingState>();
        for(int i=0;i<3;i++)if(!d.HomeBuildings.Any(b=>b!=null&&b.site==i))d.HomeBuildings.Add(new HomeBuildingState{site=i,complete=d.CurrentLevel>1,level=d.CurrentLevel>1?1:0});
        foreach(var b in d.HomeBuildings){if(b==null)continue;b.workerIds??=new List<string>();if(b.complete&&b.level==0)b.level=1;b.level=Mathf.Clamp(b.level,0,3);b.complete=b.level>0;}
    }
    public static int Level(PlayerData d,int site){Ensure(d);return d?.HomeBuildings?.Find(b=>b!=null&&b.site==site)?.level??0;}
    public static bool Built(PlayerData d,int site)=>Level(d,site)>0;
    public static int BuiltCount(PlayerData d){Ensure(d);return d?.HomeBuildings?.Count(b=>b!=null&&b.complete)??0;}
    public static int Cost(PlayerData d,int site)=>Level(d,site)==0?Costs[site]:(Level(d,site)+1)*(site==0?375:site==1?300:400);
    public static int SuppliesCost(PlayerData d,int site)=>Level(d,site)*2;
    public static float WorkDuration(PlayerData d,int site)=>Level(d,site)==0?Durations[site]:30+Level(d,site)*10;
    public static int RecruitCost(PlayerData d)=>300-Mathf.Max(0,Level(d,0)-1)*25;
    public static string Benefit(PlayerData d,int site)
    {
        int next=Mathf.Min(3,Level(d,site)+1);
        return site==0?$"Level {next}: individual recruitment costs £{300-(next-1)*25}; permanent Home planning and care.":site==1?$"Level {next}: {next*2} supplies every two minutes. Matchday pub restock costs 1 supply.":$"Level {next}: roster gains +5 maximum HP and +1 strength once for this upgrade.";
    }
    void Awake(){Instance=this;Ensure(GameManager.Data);}
    void OnDestroy(){if(Instance==this)Instance=null;if(homeTintMaterial)Destroy(homeTintMaterial);if(homeTintMesh)Destroy(homeTintMesh);if(dustMaterial)Destroy(dustMaterial);if(scaffoldMaterial)Destroy(scaffoldMaterial);}
    void Start()
    {
        var city=CityGameplay.Instance;if(!CityGameplay.HomeMode||!city){enabled=false;return;}
        var zone=new GameObject("HOME TERRITORY - permanent base");zone.transform.SetParent(transform);zone.transform.position=city.Home;
        homeBorder=ZoneVolumeFactory.BuildFlatCircle(zone.transform,"Home boundary",31,.24f,new Color(.1f,1f,.64f,.95f),true,4);
        homeBorder.transform.localRotation=Quaternion.identity;homeBorder.useWorldSpace=true;homeBorder.alignment=LineAlignment.View;
        sign=ZoneLabelUtil.Create(zone.transform,"HOME TERRITORY",6,9);
        sign.GetComponent<ZoneLabelBillboard>().enabled=false;sign.transform.localPosition=new Vector3(0,5,22);sign.fontSize=15f;
        for(int i=0;i<3;i++)
        {
            Sites[i]=CityGameplay.ReachableApproach(city.Home,city.Home+new Vector3((i-1)*14f,0,14));
            primaryAnchors[i]=FindPrimaryAnchor(i,new Vector3(0,0,7.5f),i==1?10:11);
            var plot=new GameObject(Names[i]);plot.transform.SetParent(zone.transform,true);plot.transform.position=Sites[i];
            plotLabels[i]=ZoneLabelUtil.Create(plot.transform,PlotNames[i]+(Built(GameManager.Data,i)?$" · L{Level(GameManager.Data,i)}":" · BUILD"),4,6);
            plotLabels[i].fontSize=9.5f;plotLabels[i].transform.localPosition=new Vector3(0,3.5f,-2);plotLabels[i].GetComponent<ZoneLabelBillboard>().CommandPriority=true;
            var tap=plot.AddComponent<HomePlotInteraction>();tap.Site=i;
            var hit=plot.AddComponent<BoxCollider>();hit.isTrigger=true;hit.center=new Vector3(0,1,0);hit.size=new Vector3(6,2,6);
            for(int corner=0;corner<4;corner++)
            {
                var post=GameObject.CreatePrimitive(PrimitiveType.Cube);post.name="Home plot marker";post.transform.SetParent(plot.transform,false);
                post.transform.localPosition=new Vector3((corner%2==0?-1:1)*2.7f,.35f,(corner<2?-1:1)*2.7f);post.transform.localScale=new Vector3(.18f,.7f,.18f);
                Destroy(post.GetComponent<Collider>());post.GetComponent<Renderer>().material.color=new Color(.1f,.75f,.45f);
            }
            if(Built(GameManager.Data,i))ShowFacility(i);
        }
        reservedBounds=new Bounds(city.Home+new Vector3(0,0,8),new Vector3(42,1,30));
        foreach(var site in Sites)reservedBounds.Encapsulate(new Bounds(site+Vector3.forward*3,new Vector3(8,1,12)));
        BuildHomeTint(zone.transform);RefreshFootprint(true);
        StartCoroutine(ResumeWhenReady());
    }
    IEnumerator ResumeWhenReady()
    {
        float until=Time.realtimeSinceStartup+90;
        while((!BattleManager.instance||!BattleManager.instance.BattleActive||BattleManager.instance.PlayerAgents.Count==0)&&Time.realtimeSinceStartup<until)yield return null;
        yield return null;
        if(BattleManager.instance&&BattleManager.instance.BattleActive)ResumeConstruction();
    }
    void ResumeConstruction()
    {
        var saved=GameManager.Data?.HomeBuildings?.FirstOrDefault(b=>b!=null&&b.constructing);
        if(saved==null||IsBuilding)return;
        var crew=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive&&saved.workerIds.Contains(a.Data.AgentId)).ToArray();
        if(!Assign(saved.site,crew,true)){saved.constructing=false;saved.workSeconds=0;saved.workerIds.Clear();Report("Saved construction needs available crew. Select crew and restart HOME construction; no payment was taken.");}
    }
    public bool BeginBuild(int site)
    {
        var selected=AgentSelectionManager.instance?.SelectedAgents;
        return Assign(site,selected?.Where(a=>a).ToArray()??Array.Empty<AgentController>(),false);
    }
    bool Assign(int site,AgentController[] crew,bool resume)
    {
        var d=GameManager.Data;if(!CityGameplay.HomeMode||site<0||site>2||d==null||IsBuilding||Level(d,site)>=3)return false;
        if(site>0&&!Built(d,0)){Report("Build headquarters first.");return false;}
        if(Level(d,site)>0&&BuiltCount(d)<3){Report("Establish all three Home facilities before upgrading them.");return false;}
        if(d.Money<Cost(d,site)||d.CitySupplies<SuppliesCost(d,site)){Report($"Needs £{Cost(d,site)} and {SuppliesCost(d,site)} supplies. Control or trade at a shop first.");return false;}
        if(crew.Length==0){Report("Select a living crew member, then press BUILD. They must reach the plot.");return false;}
        var accepted=crew.Where(a=>a&&a.IsAlive&&!a.IsActivityLocked&&a.TryCommandMoveTo(Sites[site],WorkRadius-.5f)).ToArray();
        if(accepted.Length==0){Report("No selected crew can reach this Home plot.");return false;}
        var state=d.HomeBuildings.Find(b=>b.site==site);state.constructing=true;state.targetLevel=Level(d,site)+1;
        if(!resume)state.workSeconds=0;state.workerIds=accepted.Select(a=>a.Data.AgentId).ToList();
        assigned=accepted;revisions=accepted.Select(a=>a.OrderRevision).ToArray();ActiveSite=site;Progress=state.workSeconds;travel=saveClock=0;
        if(plotLabels[site])plotLabels[site].gameObject.SetActive(false);
        CreateWorkModel(site);Report($"{Names[site]} LEVEL {state.targetLevel}: crew travelling. Payment on completion. A new order cancels.");
        if(!resume)GameFeedback.Emit(GameFeedbackEvent.BuildStarted,Sites[site]);GameManager.Save();return true;
    }
    void Update()
    {
        var d=GameManager.Data;
        if(sign)sign.transform.rotation=Camera.main?Camera.main.transform.rotation:Quaternion.identity;
        if(workDust)SetDustWorking(false);
        if(!IsBuilding||d==null)return;
        for(int i=0;i<assigned.Length;i++)if(!assigned[i]||!assigned[i].IsAlive||assigned[i].IsActivityLocked||assigned[i].OrderRevision!=revisions[i]){CancelBuild("Assigned crew became unavailable or received another order.");return;}
        travel+=Time.deltaTime;
        bool present=assigned.All(a=>CityBusinessSystem.HorizontalDistance(a.transform.position,Sites[ActiveSite])<WorkRadius);
        if(!present){if(travel>120)CancelBuild("Crew could not reach the plot.");return;}
        if(CityBusinessSystem.HostilesNear(Sites[ActiveSite],12)){Status="Construction contested. Clear local hostile pressure.";return;}
        if(assigned.Any(a=>a.CurrentState!=AgentController.State.Idle))return;
        SetDustWorking(true);
        var state=d.HomeBuildings.Find(b=>b.site==ActiveSite);
        Progress+=Time.deltaTime;state.workSeconds=Progress;
        float fraction=Mathf.Clamp01(Progress/WorkDuration(d,ActiveSite));Status=$"BUILDING {Names[ActiveSite]} L{state.targetLevel} · {fraction*100:0}%";
        if(workModel)workModel.transform.localScale=new Vector3(workScale.x,workScale.y*Mathf.Lerp(.12f,1,fraction),workScale.z);
        if(workLabel){workLabel.text=$"BUILDING L{state.targetLevel} · {fraction*100:0}%";workLabel.transform.rotation=Camera.main?Camera.main.transform.rotation:Quaternion.identity;}
        saveClock+=Time.deltaTime;if(saveClock>=8){saveClock=0;GameManager.Save();}
        if(fraction<1)return;
        int cost=Cost(d,ActiveSite),supplies=SuppliesCost(d,ActiveSite);
        if(d.Money<cost||d.CitySupplies<supplies){CancelBuild("Construction funds were spent elsewhere.");return;}
        d.Money-=cost;d.CitySupplies-=supplies;state.level=state.targetLevel;state.complete=true;state.constructing=false;state.workSeconds=0;state.workerIds.Clear();
        if(ActiveSite==1){CityDevelopmentLedger.Ensure(d);d.CityDevelopment.Find(p=>p.site==1).level=Mathf.Max(state.level,d.CityDevelopment.Find(p=>p.site==1).level);}
        if(ActiveSite==2&&d.HomeTrainingLevel<state.level)
        {
            int upgrades=state.level-d.HomeTrainingLevel;d.HomeTrainingLevel=state.level;
            foreach(var member in d.RecruitedAgents.Where(a=>a!=null)){member.MaxHp+=5*upgrades;member.CurrentHp=Mathf.Min(member.MaxHp,member.CurrentHp+5*upgrades);member.Strength+=upgrades;}
            foreach(var member in BattleManager.instance.PlayerAgents)if(member)member.ApplyRosterStats();
        }
        int done=ActiveSite;assigned=null;ActiveSite=-1;ClearWorkModel();ShowFacility(done);RefreshFootprint(false);
        Report(Names[done]+$" LEVEL {state.level} COMPLETE. Saved permanent development.");GameFeedback.Emit(GameFeedbackEvent.BuildComplete,Sites[done]);GameManager.Save();
    }
    public void CancelBuild(string reason="")
    {
        Vector3 point=ActiveSite>=0?Sites[ActiveSite]:transform.position;
        var state=GameManager.Data?.HomeBuildings?.Find(b=>b.site==ActiveSite);
        if(state!=null){state.constructing=false;state.workSeconds=0;state.workerIds.Clear();}
        if(ActiveSite>=0&&plotLabels[ActiveSite])plotLabels[ActiveSite].gameObject.SetActive(true);
        assigned=null;ActiveSite=-1;Progress=0;ClearWorkModel();Report("Construction cancelled; no payment taken. "+reason+" Other groups retain their orders.");GameFeedback.Emit(GameFeedbackEvent.TaskFailed,point);GameManager.Save();
    }
    string ResourceKey(int site)=>site==0?"Headquarters":site==1?"SupplyYard":"TrainingGround";
    GameObject Model(int site,string name,Vector3 offset,float footprint=0)
    {
        var prefab=Resources.Load<GameObject>("HomeFacilities/"+ResourceKey(site));if(!prefab)return null;
        var anchor=Sites[site]+offset;
        if(footprint<=0)
        {
            if(primaryAnchors[site]==Vector3.zero)primaryAnchors[site]=FindPrimaryAnchor(site,offset,site==1?10:11);
            anchor=primaryAnchors[site];
        }
        else if(primaryAnchors[site]!=Vector3.zero)anchor=primaryAnchors[site]+(offset-new Vector3(0,0,7.5f));
        var model=Instantiate(prefab,transform);model.name=name;model.transform.position=anchor;model.transform.rotation=Quaternion.identity;
        foreach(var node in model.GetComponentsInChildren<Transform>(true))node.gameObject.layer=0;
        foreach(var c in model.GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
        foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))
        {r.enabled=true;r.forceRenderingOff=false;r.allowOcclusionWhenDynamic=false;r.renderingLayerMask=1;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
        if(TryModelBounds(model,out var bounds))
        {
            float span=Mathf.Max(bounds.size.x,bounds.size.z);
            float desired=footprint>0?footprint:site==1?10:11;
            if(span>.001f)model.transform.localScale*=desired/span;
            if(TryModelBounds(model,out bounds))
            {
                // Imported mesh pivots are not guaranteed to be at their floor.
                // Align the real minimum, not the prefab's transform, to the site.
                anchor.y=Sites[site].y+.09f;
                model.transform.position+=new Vector3(anchor.x-bounds.center.x,anchor.y-bounds.min.y,anchor.z-bounds.center.z);
            }
        }
        var building=model.transform.Find("Facility building body");if(building)RtsBuildingVisibility.RegisterFacility(building.gameObject);
        return model;
    }
    Vector3 FindPrimaryAnchor(int site,Vector3 preferred,float footprint)
    {
        Vector3 best=Sites[site]+preferred;float bestScore=float.MaxValue;
        Vector3[] offsets={preferred,new Vector3(0,0,-8),new Vector3(8,0,0),new Vector3(-8,0,0),new Vector3(5,0,8),new Vector3(-5,0,8)};
        foreach(var offset in offsets)
        {
            var point=Sites[site]+offset;float score=Vector3.Distance(offset,preferred)*.12f;
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                var sample=point+new Vector3(x,0,z)*footprint*.4f;
                // The authored road network is Area 3, also used by the patrol
                // van. Prefer park-side land; do not put a new facade on a road.
                if(NavMesh.SamplePosition(sample,out var road,.8f,1<<3)&&CityBusinessSystem.HorizontalDistance(road.position,sample)<.8f)score+=10;
            }
            for(int i=0;i<site;i++)if(primaryAnchors[i]!=Vector3.zero&&CityBusinessSystem.HorizontalDistance(primaryAnchors[i],point)<footprint+1)score+=25;
            if(score>=bestScore)continue;bestScore=score;best=point;
        }
        return best;
    }
    void ShowFacility(int site)
    {
        if(facilities[site])Destroy(facilities[site]);int level=Level(GameManager.Data,site);
        var root=new GameObject("BUILT "+Names[site]);root.transform.SetParent(transform);root.transform.position=Sites[site];facilities[site]=root;
        for(int i=0;i<level;i++)
        {
            Vector3 offset=i==0?new Vector3(0,0,7.5f):new Vector3(i==1?6.5f:-6.5f,0,10.5f);
            var model=Model(site,$"{Names[site]} upgrade {i+1}",offset,i==0?0:4.3f);if(model)model.transform.SetParent(root.transform,true);
        }
        facilityRenderers[site]=root.GetComponentsInChildren<MeshRenderer>(true);
        if(plotLabels[site]){plotLabels[site].text=PlotNames[site]+$" · L{level}";plotLabels[site].gameObject.SetActive(true);}
    }
    void CreateWorkModel(int site)
    {
        ClearWorkModel();int level=Level(GameManager.Data,site);
        Vector3 offset=level==0?new Vector3(0,0,7.5f):new Vector3(level==1?6.5f:-6.5f,0,10.5f);
        workModel=Model(site,"HOME CONSTRUCTION IN PROGRESS",offset,level==0?0:4.3f);
        if(workModel){workScale=workModel.transform.localScale;workModel.transform.localScale=new Vector3(workScale.x,.12f*workScale.y,workScale.z);}
        Vector3 workPoint=workModel?workModel.transform.position:Sites[site]+offset;
        if(workModel&&TryModelBounds(workModel,out var workBounds))workPoint=new Vector3(workBounds.center.x,Sites[site].y+.09f,workBounds.center.z);
        BuildScaffold(workPoint,level==0?11:4.3f);PrepareDust(workPoint);
        var labelHost=new GameObject("Home construction progress");labelHost.transform.SetParent(transform);labelHost.transform.position=Sites[site];
        workLabel=ZoneLabelUtil.Create(labelHost.transform,"CREW EN ROUTE",4,5);workLabel.fontSize=9.5f;workLabel.GetComponent<ZoneLabelBillboard>().CommandPriority=true;
    }
    void ClearWorkModel(){if(workModel)Destroy(workModel);if(workLabel)Destroy(workLabel.transform.parent.gameObject);if(scaffold)Destroy(scaffold);SetDustWorking(false);workModel=null;workLabel=null;scaffold=null;}
    public bool TryGetFacilityBounds(int site,out Bounds bounds)
    {
        bounds=default;if(site<0||site>=facilities.Length||!facilities[site]||facilityRenderers[site]==null)return false;bool found=false;
        foreach(var r in facilityRenderers[site])if(r&&r.gameObject.activeInHierarchy){if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);}
        return found;
    }
    static bool TryModelBounds(GameObject model,out Bounds bounds)
    {
        bounds=default;bool found=false;if(!model)return false;
        foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(!r||r.GetComponent<TMP_Text>()||!r.gameObject.activeInHierarchy)continue;
            if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);
        }
        return found;
    }
    void BuildHomeTint(Transform parent)
    {
        var go=new GameObject("Home owned ground tint",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);homeTint=go.transform;
        homeTintMesh=new Mesh{name="Home area XZ surface"};
        homeTintMesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
        homeTintMesh.triangles=new[]{0,1,2,0,2,3};homeTintMesh.RecalculateNormals();homeTintMesh.RecalculateBounds();go.GetComponent<MeshFilter>().sharedMesh=homeTintMesh;
        homeTintMaterial=TransparentMaterial("Home subtle ownership",new Color(.07f,.8f,.35f,.055f));
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=homeTintMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.allowOcclusionWhenDynamic=false;
    }
    static Material TransparentMaterial(string name,Color color,bool particles=false)
    {
        var shader=particles?(Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Sprites/Default")):(Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Sprites/Default"));
        var material=new Material(shader){name=name,color=color};
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Surface"))material.SetFloat("_Surface",1);
        if(material.HasProperty("_SrcBlend"))material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
        if(material.HasProperty("_DstBlend"))material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        if(material.HasProperty("_ZWrite"))material.SetFloat("_ZWrite",0);
        if(material.HasProperty("_Cull"))material.SetFloat("_Cull",(float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;return material;
    }
    void RefreshFootprint(bool snap)
    {
        if(!CityGameplay.Instance||reservedBounds.size.x<=0)return;
        Bounds bounds=reservedBounds;
        for(int i=0;i<facilities.Length;i++)if(TryGetFacilityBounds(i,out var built))bounds.Encapsulate(built);
        // Every earned level increases the useful district footprint, and real
        // work eases toward the next extent. Reload derives it from saved work.
        float padding=1.5f+DevelopmentFraction*8;
        bounds.Expand(new Vector3(padding*2,0,padding*2));
        bounds.center=new Vector3(bounds.center.x,CityGameplay.Instance.Home.y+.08f,bounds.center.z);bounds.size=new Vector3(bounds.size.x,1,bounds.size.z);TargetBounds=bounds;
        if(snap||WorldBounds.size.x<=0)WorldBounds=TargetBounds;
    }
    void LateUpdate()
    {
        if(!CityGameplay.HomeMode||!homeBorder)return;
        if(Time.unscaledTime>=nextFootprint){nextFootprint=Time.unscaledTime+.15f;RefreshFootprint(false);}
        float blend=1-Mathf.Exp(-3*Time.deltaTime);
        WorldBounds=new Bounds(Vector3.Lerp(WorldBounds.center,TargetBounds.center,blend),Vector3.Lerp(WorldBounds.size,TargetBounds.size,blend));
        var min=WorldBounds.min;var max=WorldBounds.max;float y=WorldBounds.center.y+.12f;
        borderPoints[0]=new Vector3(min.x,y,min.z);borderPoints[1]=new Vector3(min.x,y,max.z);borderPoints[2]=new Vector3(max.x,y,max.z);borderPoints[3]=new Vector3(max.x,y,min.z);homeBorder.SetPositions(borderPoints);
        if(homeTint){homeTint.position=new Vector3(WorldBounds.center.x,WorldBounds.center.y-.025f,WorldBounds.center.z);homeTint.localScale=new Vector3(WorldBounds.size.x,1,WorldBounds.size.z);}
        if(sign)sign.transform.position=new Vector3(WorldBounds.center.x,WorldBounds.center.y+5.5f,WorldBounds.max.z-1);
    }
    void BuildScaffold(Vector3 point,float width)
    {
        scaffold=new GameObject("Home active construction scaffold");scaffold.transform.SetParent(transform);scaffold.transform.position=point;
        if(!scaffoldMaterial){scaffoldMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));scaffoldMaterial.color=new Color(.32f,.4f,.4f);}
        float height=3;
        if(workModel&&TryModelBounds(workModel,out var bounds))height=Mathf.Clamp(bounds.size.y/.12f,2.7f,7.5f);
        float half=width*.5f;
        for(int i=0;i<4;i++)
        {
            float x=i%2==0?-half:half,z=i<2?-half:half;
            ScaffoldBar("Scaffold upright",new Vector3(x,height*.5f,z),new Vector3(.13f,height,.13f));
        }
        for(int i=0;i<2;i++)
        {
            float side=i==0?-half:half;
            ScaffoldBar("Scaffold crossbeam",new Vector3(0,height,side),new Vector3(width,.13f,.13f));
            ScaffoldBar("Scaffold crossbeam",new Vector3(side,height,0),new Vector3(.13f,.13f,width));
        }
    }
    void ScaffoldBar(string name,Vector3 point,Vector3 scale)
    {
        var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);bar.name=name;bar.transform.SetParent(scaffold.transform,false);bar.transform.localPosition=point;bar.transform.localScale=scale;
        var collider=bar.GetComponent<Collider>();collider.enabled=false;Destroy(collider);bar.GetComponent<Renderer>().sharedMaterial=scaffoldMaterial;
    }
    void PrepareDust(Vector3 point)
    {
        if(!workDust)
        {
            var go=new GameObject("Pooled Home construction dust");go.transform.SetParent(transform);workDust=go.AddComponent<ParticleSystem>();workDust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=workDust.main;main.loop=true;main.duration=1;main.startLifetime=.7f;main.startSpeed=.35f;main.startSize=.3f;main.startColor=new Color(.64f,.57f,.4f,.75f);main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;main.playOnAwake=false;
            var emission=workDust.emission;emission.rateOverTime=8;emission.enabled=false;
            var shape=workDust.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(3,.1f,3);
            var color=workDust.colorOverLifetime;color.enabled=true;color.color=new ParticleSystem.MinMaxGradient(new Gradient{colorKeys=new[]{new GradientColorKey(new Color(.64f,.57f,.4f),0),new GradientColorKey(new Color(.64f,.57f,.4f),1)},alphaKeys=new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.3f,.2f),new GradientAlphaKey(0,1)}});
            dustMaterial=TransparentMaterial("Home working dust",Color.white,true);var renderer=workDust.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=dustMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        workDust.transform.position=point+Vector3.up*.2f;workDust.Clear();SetDustWorking(false);
    }
    void SetDustWorking(bool active)
    {
        if(!workDust)return;var emission=workDust.emission;emission.enabled=active;
        if(active&&!workDust.isPlaying)workDust.Play();
    }
    public bool RecruitOne()
    {
        var d=GameManager.Data;int price=RecruitCost(d);if(!CityGameplay.HomeMode){Report("Home recruitment is available after returning from the Away operation.");return false;}
        if(!Built(d,0)||d.Money<price){Report($"Recruiting needs headquarters and £{price}.");return false;}
        if(AgentSelectionManager.instance?.SelectedAgents.Any(a=>a&&a.IsAlive&&CityBusinessSystem.HorizontalDistance(a.transform.position,CityGameplay.Instance.Home)<32)!=true){Report("Bring a selected crew member inside Home to meet a recruit.");return false;}
        var actor=BattleManager.instance.SpawnRecruitedAgentAt(Sites[0],"Home Recruit");if(!actor){Report("Active crew is full. No payment taken.");return false;}
        d.Money-=price;
        if(GuidedCityTutorial.BlocksInput&&GuidedCityTutorial.Instance.Part==9)d.MobileTutorialRecruitDone=true;
        GameManager.Save();Report($"ONE MEMBER JOINED AT HOME · £{price}. Assign them independently.");return true;
    }
    void Report(string value){Status=value;CityGameplay.Instance?.PostEvent(value);}
}

public sealed class HomePlotInteraction:MonoBehaviour
{
    public int Site;
    public void Open(){ModernCityHud.Instance?.OpenHome();}
}
