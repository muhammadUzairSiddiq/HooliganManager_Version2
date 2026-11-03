using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

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
    AgentController[] assigned;
    int[] revisions;
    float travel,saveClock;
    readonly GameObject[] facilities=new GameObject[3];
    readonly TextMeshPro[] plotLabels=new TextMeshPro[3];
    static readonly string[] PlotNames={"HQ","SUPPLY YARD","TRAINING"};
    GameObject workModel;
    Vector3 workScale;
    TextMeshPro sign,workLabel;
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
        return site==0?$"Level {next}: individual recruitment costs £{300-(next-1)*25}; permanent Home planning and care.":site==1?$"Level {next}: produces {next*2} supplies every two minutes of active play.":$"Level {next}: roster gains +5 maximum HP and +1 strength once for this upgrade.";
    }
    void Awake(){Instance=this;Ensure(GameManager.Data);}
    void OnDestroy(){if(Instance==this)Instance=null;}
    void Start()
    {
        var city=CityGameplay.Instance;if(!CityGameplay.HomeMode||!city){enabled=false;return;}
        var zone=new GameObject("HOME TERRITORY - permanent base");zone.transform.SetParent(transform);zone.transform.position=city.Home;
        var border=ZoneVolumeFactory.BuildFlatCircle(zone.transform,"Home boundary",31,.3f,new Color(.1f,1f,.64f,.95f),true,4);
        border.transform.localRotation=Quaternion.identity;
        border.transform.localPosition=Vector3.up*.4f;border.alignment=LineAlignment.View;
        border.SetPositions(new[]{new Vector3(-28,.08f,-22),new Vector3(-28,.08f,28),new Vector3(28,.08f,28),new Vector3(28,.08f,-22)});
        sign=ZoneLabelUtil.Create(zone.transform,"HOME TERRITORY",6,9);
        sign.GetComponent<ZoneLabelBillboard>().enabled=false;sign.transform.localPosition=new Vector3(0,5,22);sign.fontSize=15f;
        for(int i=0;i<3;i++)
        {
            Sites[i]=CityGameplay.ReachableApproach(city.Home,city.Home+new Vector3((i-1)*14f,0,14));
            var plot=new GameObject(Names[i]);plot.transform.SetParent(zone.transform,true);plot.transform.position=Sites[i];
            plotLabels[i]=ZoneLabelUtil.Create(plot.transform,PlotNames[i]+(Built(GameManager.Data,i)?$" · L{Level(GameManager.Data,i)}":" · BUILD"),4,6);
            plotLabels[i].fontSize=12;plotLabels[i].transform.localPosition=new Vector3(0,4,6);plotLabels[i].GetComponent<ZoneLabelBillboard>().CommandPriority=true;
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
        CreateWorkModel(site);Report($"{Names[site]} LEVEL {state.targetLevel}: crew travelling. Payment on completion. A new order cancels.");GameManager.Save();return true;
    }
    void Update()
    {
        var d=GameManager.Data;
        if(sign)sign.transform.rotation=Camera.main?Camera.main.transform.rotation:Quaternion.identity;
        if(!IsBuilding||d==null)return;
        for(int i=0;i<assigned.Length;i++)if(!assigned[i]||!assigned[i].IsAlive||assigned[i].IsActivityLocked||assigned[i].OrderRevision!=revisions[i]){CancelBuild("Assigned crew became unavailable or received another order.");return;}
        travel+=Time.deltaTime;
        bool present=assigned.All(a=>CityBusinessSystem.HorizontalDistance(a.transform.position,Sites[ActiveSite])<WorkRadius);
        if(!present){if(travel>120)CancelBuild("Crew could not reach the plot.");return;}
        if(CityBusinessSystem.HostilesNear(Sites[ActiveSite],12)){Status="Construction contested. Clear local hostile pressure.";return;}
        if(assigned.Any(a=>a.CurrentState!=AgentController.State.Idle))return;
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
        int done=ActiveSite;assigned=null;ActiveSite=-1;ClearWorkModel();ShowFacility(done);Report(Names[done]+$" LEVEL {state.level} COMPLETE. Saved permanent development.");GameManager.Save();
    }
    public void CancelBuild(string reason="")
    {
        var state=GameManager.Data?.HomeBuildings?.Find(b=>b.site==ActiveSite);
        if(state!=null){state.constructing=false;state.workSeconds=0;state.workerIds.Clear();}
        if(ActiveSite>=0&&plotLabels[ActiveSite])plotLabels[ActiveSite].gameObject.SetActive(true);
        assigned=null;ActiveSite=-1;Progress=0;ClearWorkModel();Report("Construction cancelled; no payment taken. "+reason+" Other groups retain their orders.");GameManager.Save();
    }
    string ResourceKey(int site)=>site==0?"Headquarters":site==1?"SupplyYard":"TrainingGround";
    GameObject Model(int site,string name,Vector3 offset)
    {
        var prefab=Resources.Load<GameObject>("HomeFacilities/"+ResourceKey(site));if(!prefab)return null;
        var model=Instantiate(prefab,transform);model.name=name;model.transform.position=Sites[site]+offset;
        foreach(var c in model.GetComponentsInChildren<Collider>())c.enabled=false;
        var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length>0){var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);float span=Mathf.Max(bounds.size.x,bounds.size.z);if(span>0)model.transform.localScale*=Mathf.Min(1,7/span);}
        return model;
    }
    void ShowFacility(int site)
    {
        if(facilities[site])Destroy(facilities[site]);int level=Level(GameManager.Data,site);
        var root=new GameObject("BUILT "+Names[site]);root.transform.SetParent(transform);root.transform.position=Sites[site];facilities[site]=root;
        for(int i=0;i<level;i++){var model=Model(site,$"{Names[site]} upgrade {i+1}",new Vector3(i==0?0:(i%2==0?-5:5),0,5.5f+(i>0?4:0)));if(model)model.transform.SetParent(root.transform,true);}
        if(plotLabels[site]){plotLabels[site].text=PlotNames[site]+$" · L{level}";plotLabels[site].gameObject.SetActive(true);}
    }
    void CreateWorkModel(int site)
    {
        ClearWorkModel();workModel=Model(site,"HOME CONSTRUCTION IN PROGRESS",new Vector3(0,0,7));
        if(workModel){workScale=workModel.transform.localScale;workModel.transform.localScale=new Vector3(workScale.x,.12f*workScale.y,workScale.z);}
        var labelHost=new GameObject("Home construction progress");labelHost.transform.SetParent(transform);labelHost.transform.position=Sites[site];
        workLabel=ZoneLabelUtil.Create(labelHost.transform,"CREW EN ROUTE",4,5);workLabel.fontSize=11;workLabel.GetComponent<ZoneLabelBillboard>().CommandPriority=true;
    }
    void ClearWorkModel(){if(workModel)Destroy(workModel);if(workLabel)Destroy(workLabel.transform.parent.gameObject);workModel=null;workLabel=null;}
    public bool RecruitOne()
    {
        var d=GameManager.Data;int price=RecruitCost(d);if(!CityGameplay.HomeMode){Report("Home recruitment is available after returning from the Away operation.");return false;}
        if(!Built(d,0)||d.Money<price){Report($"Recruiting needs headquarters and £{price}.");return false;}
        if(AgentSelectionManager.instance?.SelectedAgents.Any(a=>a&&a.IsAlive&&CityBusinessSystem.HorizontalDistance(a.transform.position,CityGameplay.Instance.Home)<32)!=true){Report("Bring a selected crew member inside Home to meet a recruit.");return false;}
        var actor=BattleManager.instance.SpawnRecruitedAgentAt(Sites[0],"Home Recruit");if(!actor){Report("Active crew is full. No payment taken.");return false;}
        d.Money-=price;GameManager.Save();Report($"ONE MEMBER JOINED AT HOME · £{price}. Assign them independently.");return true;
    }
    void Report(string value){Status=value;CityGameplay.Instance?.PostEvent(value);}
}

public sealed class HomePlotInteraction:MonoBehaviour
{
    public int Site;
    public void Open(){ModernCityHud.Instance?.OpenHome();}
}
