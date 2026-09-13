using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

public sealed class CityGameplay : MonoBehaviour
{
    public static CityGameplay Instance { get; private set; }
    public static bool HomeMode = true;
    public Vector3 Home => BattleManager.instance.playerSpawnRoot.position;
    public string[] LocationNames { get; private set; } = System.Array.Empty<string>();
    public Vector3[] Locations { get; private set; }
    RectTransform frame;
    GameObject cameraPanel, districtPanel;
    TextMeshProUGUI status;
    bool defending;
    float defenceTime;
    int initialRivals;
    float homeIntegrity;
    EnemyController[] intruders=System.Array.Empty<EnemyController>();
    readonly System.Collections.Generic.Queue<string> eventFeed=new System.Collections.Generic.Queue<string>();
    public string EventText=>string.Join("\n\n",eventFeed.Reverse());
    public void PostEvent(string message)
    { if(status)status.text=message;eventFeed.Enqueue(message);while(eventFeed.Count>3)eventFeed.Dequeue(); }
    public static void EnsureExists() { if (!Instance) BattleManager.instance.gameObject.AddComponent<CityGameplay>(); }
    void Awake() { Instance=this; }
    void OnDestroy() { if(Instance==this)Instance=null; }
    IEnumerator Start()
    {
        while(!BattleManager.instance.playerSpawnRoot)yield return null;
        ConfigureLocations();
        for(int i=0;i<Locations.Length;i++)
        {
            Locations[i]=ReachableApproach(Home,Locations[i]);
            var marker=new GameObject(LocationNames[i]); marker.transform.position=Locations[i];
            MiniMapIconFactory.Register(marker.transform,MiniMapIconFactory.Kind.Turf,LocationNames[i]);
            var label=ZoneLabelUtil.Create(marker.transform,LocationNames[i],4,6);
            marker.AddComponent<CityLocationLabel>().LocationIndex=i;
        }
        while(!FindFirstObjectByType<LandscapeBattleHUD>())yield return null;
        frame=FindFirstObjectByType<LandscapeBattleHUD>().frame;
        LandscapeUI.Button("CameraSetup",frame,"CAMERA",1145,800,150,52).onClick.AddListener(()=>cameraPanel.SetActive(!cameraPanel.activeSelf));
        LandscapeUI.Button("CityDistrict",frame,HomeMode?"HOME MAP":"AWAY MAP",280,110,155,48).onClick.AddListener(()=>districtPanel.SetActive(!districtPanel.activeSelf));
        LandscapeUI.Image("CityStatusBacking",frame,453,110,655,48,null,new Color(.025f,.055f,.07f,.90f));
        status=LandscapeUI.Text("CityStatus",frame,HomeMode?"HOME TERRITORY - HQ, pub, recruitment, training and recovery":"AWAY OPERATION - secure the travelled district",466,115,625,38,20,LandscapeUI.Green,true);
        BuildCameraPanel(); BuildDistrictPanel();
        gameObject.AddComponent<CityMissionHUD>().Build(frame);
        PostEvent(HomeMode ? "Welcome home. Prepare your firm at headquarters." : "Away trip started. Move from the arrival point and secure the district.");
        while(BattleManager.instance.PlayerAgents.Count==0)yield return null;
        CameraPanTouchOnly.Instance?.CenterOnSelection();
    }

    void ConfigureLocations()
    {
        if (HomeMode)
        {
            LocationNames = new [] {"HEADQUARTERS","LOCAL PUB","RECRUITMENT","TRAINING","RECOVERY","STADIUM","CENTRAL PARK","RAIL DISTRICT","WATERFRONT"};
            Locations = new [] { Home, Home+new Vector3(-30,0,-30), Home+new Vector3(30,0,0),
                Home+new Vector3(0,0,30), Home+new Vector3(-15,0,0),new Vector3(97,0,-285),
                new Vector3(187.5f,0,75),new Vector3(165,0,-180),new Vector3(120,0,225) };
            return;
        }

        string dest = GameManager.Data?.LastSelectedDestination ?? "East Docks";
        if (dest == "North End")
        {
            LocationNames = new [] {"COACH DROP-OFF","NORTH END PUB","MARKET CUT","ESTATE COURT","RAIL ARCHES","AWAY STAND","BUS ROUTE","SOCIAL CLUB","BACK STREETS"};
            Locations = new [] { Home, new Vector3(82,0,104), new Vector3(44,0,116), new Vector3(112,0,136), new Vector3(64,0,162), new Vector3(98,0,28), new Vector3(28,0,60), new Vector3(136,0,88), new Vector3(34,0,154) };
            return;
        }
        if (dest == "Riverside")
        {
            LocationNames = new [] {"RIVER ARRIVAL","RIVERSIDE PUB","PROMENADE","BOATYARD","PARK GATES","BRIDGE ROUTE","WATERFRONT","STATION WALK","SOUTH ALLEY"};
            Locations = new [] { Home, new Vector3(150,0,198), new Vector3(122,0,228), new Vector3(188,0,216), new Vector3(186,0,74), new Vector3(146,0,154), new Vector3(120,0,225), new Vector3(165,0,-180), new Vector3(96,0,184) };
            return;
        }
        if (dest == "Old Town")
        {
            LocationNames = new [] {"OLD TOWN GATE","CROWN PUB","CATHEDRAL LANE","BACK MARKET","TOWN SQUARE","OLD STAND","SIDE STREET","RIVAL LOCAL","ESCAPE ROUTE"};
            Locations = new [] { Home, new Vector3(-86,0,128), new Vector3(-124,0,154), new Vector3(-54,0,104), new Vector3(-112,0,78), new Vector3(-160,0,34), new Vector3(-44,0,148), new Vector3(-136,0,116), new Vector3(-72,0,54) };
            return;
        }

        LocationNames = new [] {"DOCK ARRIVAL","EAST DOCKS PUB","WAREHOUSE ROW","FERRY ROAD","CONTAINER YARD","AWAY STAND","CANAL BRIDGE","LOCK GATES","SERVICE LANE"};
        Locations = new [] { Home, new Vector3(244,0,-82), new Vector3(270,0,-56), new Vector3(218,0,-118), new Vector3(286,0,-132), new Vector3(196,0,-188), new Vector3(250,0,-32), new Vector3(304,0,-84), new Vector3(216,0,-52) };
    }
    public static Vector3 ReachableApproach(Vector3 origin,Vector3 destination)
    {
        var path=new NavMeshPath();
        for(int ring=0;ring<=8;ring++)
        for(int direction=0;direction<(ring==0?1:16);direction++)
        {
            float angle=direction*Mathf.PI/8;
            var candidate=destination+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*8;
            if(NavMesh.SamplePosition(candidate,out var hit,5,NavMesh.AllAreas) &&
                NavMesh.CalculatePath(origin,hit.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete)
                return hit.position;
        }
        Debug.LogError("No reachable city approach near "+destination);
        return destination;
    }
    void BuildCameraPanel()
    {
        cameraPanel=LandscapeUI.Panel("CameraSettings",frame,970,360,350,405,true).gameObject;
        LandscapeUI.Text("Heading",cameraPanel.transform,"TACTICAL CAMERA",22,14,270,36,24,null,true);
        float fov=PlayerPrefs.GetFloat("CityCameraFov",65),height=PlayerPrefs.GetFloat("CityCameraHeight",65),pitch=PlayerPrefs.GetFloat("CityCameraPitch",65);
        SliderRow(cameraPanel.transform,"FIELD OF VIEW",65,45,85,fov,v=>{fov=v;Apply();});
        SliderRow(cameraPanel.transform,"HEIGHT",145,35,180,height,v=>{height=v;Apply();});
        SliderRow(cameraPanel.transform,"ANGLE",225,50,85,pitch,v=>{pitch=v;Apply();});
        LandscapeUI.Button("Center",cameraPanel.transform,"RECENTRE",20,320,145,54).onClick.AddListener(()=>CameraPanTouchOnly.Instance?.CenterOnSelection());
        LandscapeUI.Button("Close",cameraPanel.transform,"DONE",182,320,145,54).onClick.AddListener(()=>{PlayerPrefs.Save();cameraPanel.SetActive(false);});
        cameraPanel.SetActive(false);
        void Apply()=>CameraPanTouchOnly.Instance?.ConfigureCity(fov,height,pitch);
    }
    static void SliderRow(Transform p,string name,float y,float min,float max,float value,UnityEngine.Events.UnityAction<float> changed)
    {
        var text=LandscapeUI.Text(name,p,name+"  "+value.ToString("0"),22,y,300,30,19);
        var rt=LandscapeUI.Rect(name+" Slider",p,26,y+37,294,25);
        var background=rt.gameObject.AddComponent<Image>();background.color=new Color(.18f,.21f,.23f);
        var slider=rt.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;
        var handle=LandscapeUI.Image("Handle",rt,0,0,24,30,null,LandscapeUI.Green);
        slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.value=value;
        slider.onValueChanged.AddListener(v=>{text.text=name+"  "+v.ToString("0");changed(v);});
    }
    void BuildDistrictPanel()
    {
        districtPanel=LandscapeUI.Panel("DistrictLocations",frame,280,170,430,570,true).gameObject;
        LandscapeUI.Text("Title",districtPanel.transform,HomeMode?"HOME DISTRICT":"AWAY DISTRICT",20,12,365,36,25,null,true);
        for(int i=0;i<LocationNames.Length;i++)
        {
            int location=i;
            LandscapeUI.Button("Location"+i,districtPanel.transform,LocationNames[i],20,58+i*49,390,44).onClick.AddListener(()=>OpenLocation(location));
        }
        LandscapeUI.Button("CloseDistrict",districtPanel.transform,"CLOSE",20,510,390,44).onClick.AddListener(()=>districtPanel.SetActive(false));
        districtPanel.SetActive(false);
    }
    public void OpenLocation(int index)
    {
        if(Locations==null || index<0 || index>=Locations.Length)return;
        districtPanel.SetActive(false);
        CameraPanTouchOnly.Instance?.FocusOn(Locations[index]);
        var options=new System.Collections.Generic.List<GamePopup.Option>();
        options.Add(new GamePopup.Option("MOVE SQUAD",LandscapeUI.Green,()=>{AgentSelectionManager.instance?.SelectAll();AgentSelectionManager.instance?.CommandSelectedMoveTo(Locations[index]);}));
        if(HomeMode && index==0)
        {
            options.Add(new GamePopup.Option("MANAGE / AWAY TRIPS",LandscapeUI.PanelColor,()=>{BattleManager.instance.PersistBattleProgress();GameManager.LoadScene(GameManager.SCENE_DASHBOARD);}));
            options.Add(new GamePopup.Option("DEFEND HOME",LandscapeUI.Red,BeginDefence));
        }
        if(HomeMode && index==1)options.Add(new GamePopup.Option("RALLY CREW - 100",LandscapeUI.Green,()=>Service(index,100)));
        if(HomeMode && index==2)options.Add(new GamePopup.Option("RECRUIT - 500",LandscapeUI.Green,()=>Service(index,500)));
        if(HomeMode && index==2)options.Add(new GamePopup.Option("QUEUE 2 FANS - 300",LandscapeUI.Green,QueueFans));
        if(HomeMode && index==3)options.Add(new GamePopup.Option("TRAIN SQUAD - 300",LandscapeUI.Green,()=>Service(index,300)));
        if(HomeMode && index==4)options.Add(new GamePopup.Option("HEAL SQUAD - 200",LandscapeUI.Green,()=>Service(index,200)));
        if(!HomeMode && index>0)options.Add(new GamePopup.Option("SECURE THIS AREA",LandscapeUI.Green,CaptureNearest));
        options.Add(new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));
        string description=HomeMode
            ? index==0?"Prepare the firm, organize the squad and protect your district.":index<5?"Services are available to your squad at this location.":"Home city destination"
            : "Travelled territory. Move carefully, find the rival pressure points and get the crew back out.";
        GamePopup.Instance.Show(LocationNames[index],description,options.ToArray());
    }
    void Service(int index,int cost)
    {
        var bm=BattleManager.instance;var d=GameManager.Data;
        if(d==null)return;
        var nearby=bm.PlayerAgents.Where(a=>a&&a.IsAlive&&Vector3.Distance(a.transform.position,Locations[index])<16).ToArray();
        if(nearby.Length==0){status.text="MOVE YOUR SQUAD TO "+LocationNames[index];return;}
        if(d.Money<cost){status.text="INSUFFICIENT FUNDS";return;}
        if(index==2 && bm.PlayerAgents.Count(a=>a&&a.IsAlive)>=bm.maxPlayerAgents){status.text="SQUAD FULL";return;}
        if(index==3 && d.HomeTrainingLevel>=5){status.text="TRAINING COMPLETE";return;}
        if(index==4 && nearby.All(a=>a.CurrentHp>=a.Data.MaxHp)){status.text="SQUAD ALREADY HEALTHY";return;}
        d.Money-=cost;
        if(index==1)d.FanMorale=Mathf.Min(100,d.FanMorale+10);
        if(index==2)bm.SpawnRecruitedAgentAt(Locations[index],"Local Recruit");
        if(index==3){foreach(var a in nearby)a.Data.Strength+=2;d.HomeTrainingLevel++;d.LastTrainingMatchday=d.MatchDay;}
        if(index==4)foreach(var a in nearby){a.HealAmount(a.Data.MaxHp);a.SyncHpToData();}
        bm.PersistBattleProgress();PostEvent(LocationNames[index]+" - COMPLETE");
    }
    void QueueFans()
    {
        var d=GameManager.Data;
        if(d==null)return;
        if(!BattleManager.instance.PlayerAgents.Any(a=>a&&a.IsAlive&&Vector3.Distance(a.transform.position,Locations[2])<16))
        {PostEvent("MOVE YOUR SQUAD TO RECRUITMENT");return;}
        if(d.Money<300){PostEvent("INSUFFICIENT FUNDS");return;}
        d.Money-=300;d.PendingFansGain+=2;GameManager.Save();PostEvent("2 FANS QUEUED FOR NEXT MATCHDAY");
    }
    public void CaptureNearest()
    {
        var squad=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();
        if(squad.Length==0)return;
        var target=FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None)
            .OrderBy(p=>Vector3.Distance(p.transform.position,squad[0].transform.position)).FirstOrDefault();
        if(!target){PostEvent("ALL AVAILABLE TERRITORY SECURED");return;}
        AgentSelectionManager.instance.SelectAll();
        AgentSelectionManager.instance.CommandSelectedMoveTo(target.transform.position);
        PostEvent("CAPTURE "+target.zoneName.ToUpperInvariant()+" - CLEAR RIVALS AND HOLD THE POINT");
    }
    void BeginDefence()
    {
        if(!HomeMode || defending)return;
        if(GameManager.Data.HomeDefenceCompleted){PostEvent("HOME DEFENCE COMPLETE - DISTRICT SECURE");return;}
        foreach(var old in intruders)if(old)Destroy(old.gameObject);
        var bm=BattleManager.instance;
        if(!bm.enemyAgentPrefab){PostEvent("DEFENCE UNAVAILABLE - MISSING RIVAL PREFAB");return;}
        intruders=new EnemyController[3];
        for(int i=0;i<intruders.Length;i++)
        {
            var point=ReachableApproach(Home,Home+new Vector3(36+i*2,0,-25));
            var enemy=Instantiate(bm.enemyAgentPrefab,point,Quaternion.identity).GetComponent<EnemyController>();
            enemy.Initialise(35,5,3,1,bm.portraitRegistry);enemy.firmName="Home Intruders";
            enemy.isHostile=true;enemy.detectionRadius=12;enemy.defenceObjective=bm.playerSpawnRoot;
            enemy.SetCinematicIdle(false);bm.RegisterEnemy(enemy);intruders[i]=enemy;
        }
        defending=true;defenceTime=150;initialRivals=intruders.Length;homeIntegrity=100;
        PostEvent("RIVAL INTRUSION - PROTECT HEADQUARTERS");
        AgentSelectionManager.instance?.SelectAll();CameraPanTouchOnly.Instance?.FocusOn(Home);
    }
    void Update()
    {
        if(!defending)return;
        defenceTime-=Time.deltaTime;
        int remaining=intruders.Count(e=>e&&e.IsAlive);
        homeIntegrity-=intruders.Count(e=>e&&e.IsAlive&&Vector3.Distance(e.transform.position,Home)<7)*Time.deltaTime*3;
        status.text=$"DEFEND HQ  {initialRivals-remaining}/{initialRivals}  HQ {Mathf.CeilToInt(homeIntegrity)}%  {Mathf.CeilToInt(defenceTime)}s";
        if(remaining==0)
        {
            defending=false;var d=GameManager.Data;
            if(!d.HomeDefenceCompleted){d.HomeDefenceCompleted=true;d.Money+=1500;d.Reputation+=5;GameManager.Save();}
            PostEvent("HOME DEFENDED +£1,500 / +5 REP");
            GamePopup.Instance.Show("HEADQUARTERS SECURED","The intrusion is over. Your district is safe.\n£1,500 and 5 reputation saved.",new GamePopup.Option("RETURN TO HQ",LandscapeUI.Green,()=>CameraPanTouchOnly.Instance?.FocusOn(Home)));
        }
        else if(defenceTime<=0 || homeIntegrity<=0 || !BattleManager.instance.PlayerAgents.Any(a=>a&&a.IsAlive))
        {
            defending=false;
            foreach(var enemy in intruders)if(enemy){enemy.defenceObjective=null;enemy.isHostile=false;}
            PostEvent("DEFENCE FAILED - REGROUP AT HEADQUARTERS");
        }
    }
}
