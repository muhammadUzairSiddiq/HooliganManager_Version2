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
        ResolveLandmarkLocations();
        ClearRailDistrictTrees();
        for(int i=0;i<Locations.Length;i++)
        {
            Locations[i]=ReachableApproach(Home,Locations[i]);
            var marker=new GameObject(LocationNames[i]); marker.transform.position=Locations[i];
            MiniMapIconFactory.Register(marker.transform,MiniMapIconFactory.Kind.Turf,LocationNames[i]);
            ZoneLabelUtil.Create(marker.transform,LocationNames[i],3.6f,6.8f);
            marker.AddComponent<CityLocationLabel>().LocationIndex=i;
            BuildLocationMarker(marker.transform,i);
        }
        yield return null;
        AlignControlPointsToLandmarks();
        if(HomeMode && Locations.Length>5)
        {
            StadiumMatchdayActivity.Ensure(Locations[5]);
            CitySocialActivity.EnsurePub(Locations[1]);
            CityLifeActivity.Ensure("SHOPS",Locations[2],2);
            CityLifeActivity.Ensure("GYM",Locations[3],2);
            CityLifeActivity.Ensure("HOSPITAL",Locations[4],2);
            CityLifeActivity.Ensure("CHURCH",Locations[6],2);
            CityLifeActivity.Ensure("STATION",Locations[7],2);
            CityLifeActivity.Ensure("DOLPHINARIUM",Locations[8],2);
            CityLifeActivity.Ensure("BARBER",CityLandmarks.Barber(Locations[2]+new Vector3(-14f,0f,10f)).Point,2);
            CityLifeActivity.Ensure("FIRE STATION",CityLandmarks.FireStation(Locations[7]+new Vector3(16f,0f,-12f)).Point,2);
            CityLifeActivity.Ensure("SCHOOL",CityLandmarks.School(Locations[6]+new Vector3(18f,0f,8f)).Point,2);
            CityLifeActivity.Ensure("STADIUM APPROACH",Locations[5]+new Vector3(0,0,-16),3);
        }
        else if(!HomeMode && Locations.Length>0)
        {
            CitySocialActivity.EnsureArrival(Locations[0],LocationNames[0]);
            CitySocialActivity.EnsurePub(Locations[1]);
            CityLifeActivity.Ensure(LocationNames[2],Locations[2],2);
            CityLifeActivity.Ensure(LocationNames[3],Locations[3],2);
            CityLifeActivity.Ensure(LocationNames[4],Locations[4],2);
            CityLifeActivity.Ensure(LocationNames[6],Locations[6],2);
            CityLifeActivity.Ensure(LocationNames[7],Locations[7],2);
            CityLifeActivity.Ensure(LocationNames[8],Locations[8],2);
            CityLifeActivity.Ensure("AWAY STAND",Locations[5]+new Vector3(-8,0,-8),3);
        }
        var operations=CityOperationsSystem.Ensure(this);
        CityActionSystem.Ensure(this);
        while(!FindFirstObjectByType<LandscapeBattleHUD>())yield return null;
        frame=FindFirstObjectByType<LandscapeBattleHUD>().frame;
        NpcConversationUI.Ensure(frame);
        PedestrianActionHud.Ensure(frame);
        HideLegacyCameraControls();
        BuildCompactCameraControls();
        LandscapeUI.Button("CityDistrict",frame,HomeMode?"HOME":"AWAY",560,18,100,36).onClick.AddListener(()=>
        {
            if(HomeMode)
            {
                var matchday=FindFirstObjectByType<StadiumMatchdayActivity>();
                if(matchday)matchday.OpenBriefing();else districtPanel.SetActive(!districtPanel.activeSelf);
            }
            else districtPanel.SetActive(!districtPanel.activeSelf);
        });
        status=null;
        BuildCameraPanel(); BuildDistrictPanel();
        gameObject.AddComponent<CityMissionHUD>().Build(frame);
        operations.AttachHud(frame);
        PostEvent(HomeMode ? "MATCHDAY "+(GameManager.Data?.MatchDay??1).ToString("00")+" - SUPPORTERS ARE GATHERING AT THE STADIUM" : "Away trip started. Move from the arrival point and secure the district.");
        while(BattleManager.instance.PlayerAgents.Count==0)yield return null;
        CameraPanTouchOnly.Instance?.CenterOnSelection();
        float wait=8f;
        while(wait>0f&&FindObjectsByType<GangArea>(FindObjectsSortMode.None).Length==0)
        {
            wait-=Time.deltaTime;
            yield return null;
        }
        yield return null;
        CityZoneClearance.Resolve();
    }

    void HideLegacyCameraControls()
    {
        foreach(var name in new[]{"CenterCamera","CenterCameraButton","CameraZoomInButton","CameraZoomOutButton","CameraRotateButton"})
        {
            var t=FindHudChild(name);
            if(t)t.gameObject.SetActive(false);
        }
    }

    Transform FindHudChild(string name)
    {
        if(!frame)return null;
        foreach(var t in frame.GetComponentsInChildren<Transform>(true))
            if(t.name==name)return t;
        return null;
    }

    void BuildCompactCameraControls()
    {
        // Stacked under the minimap (bottom-right), matching map width with real padding.
        var camera = CameraTextButton("CameraSetup", "CAMERA", 24f, 70f, 200f, 40f);
        camera.onClick.AddListener(() => cameraPanel.SetActive(!cameraPanel.activeSelf));

        var recenter = CameraTextButton("RecenterCamera", "RECENTRE", 24f, 22f, 200f, 40f);
        recenter.onClick.AddListener(() => CameraPanTouchOnly.Instance?.CenterOnSelection());
    }

    Button CameraTextButton(string name, string label, float right, float bottom, float w, float h)
    {
        var button = LandscapeUI.Button(name, frame, label, 0, 0, w, h, "dark");
        button.gameObject.name = name;
        var rt = button.transform as RectTransform;
        if (rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-right, bottom);
            rt.sizeDelta = new Vector2(w, h);
        }
        var text = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (text)
        {
            text.fontSize = 17;
            text.fontSizeMin = 14;
            text.fontSizeMax = 17;
            text.enableAutoSizing = false;
            text.color = LandscapeUI.White;
            text.fontStyle = FontStyles.Bold;
            text.overflowMode = TextOverflowModes.Overflow;
        }
        var image = button.targetGraphic as Image;
        if (image)
        {
            image.color = new Color(.045f, .075f, .095f, .96f);
            var outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.45f, .65f, .72f, .70f);
            outline.effectDistance = new Vector2(1, -1);
        }
        return button;
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
    void ResolveLandmarkLocations()
    {
        if(Locations==null||Locations.Length<=5)return;
        var stadium=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .FirstOrDefault(t=>t&&string.Equals(t.name,"stadium_001",System.StringComparison.OrdinalIgnoreCase));
        if(stadium)
        {
        var renderers=stadium.GetComponentsInChildren<Renderer>(true);
        Bounds bounds=renderers.Length>0?renderers[0].bounds:new Bounds(stadium.position,new Vector3(40,4,40));
        for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
        float radius=Mathf.Max(bounds.extents.x,bounds.extents.z)+8f;
        Vector3 best=Locations[5];float bestDistance=float.MaxValue;
        for(int i=0;i<32;i++)
        {
            float angle=i*Mathf.PI*2f/32f;
            Vector3 candidate=bounds.center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            if(!NavMesh.SamplePosition(candidate,out var hit,14f,NavMesh.AllAreas))continue;
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(Home,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            float distance=(hit.position-bounds.center).sqrMagnitude;
            if(distance<bestDistance){bestDistance=distance;best=hit.position;}
        }
        Locations[5]=best;
        LocationNames[5]=HomeMode?"CITY STADIUM":"STADIUM AWAY END";
        Debug.Log($"[CityGameplay] Stadium destination bound to real stadium_001 at {best} (model center {bounds.center}).");
        }
        BindDistrictBuildings();
    }

    void BindDistrictBuildings()
    {
        if(Locations==null||Locations.Length<9)return;
        Snap(1,CityLandmarks.Pub(Locations[1]),HomeMode?"LOCAL PUB":LocationNames[1]);
        Snap(2,CityLandmarks.Mall(Locations[2]),HomeMode?"SHOPS":LocationNames[2]);
        Snap(3,CityLandmarks.Gym(Locations[3]),HomeMode?"GYM":LocationNames[3]);
        Snap(4,CityLandmarks.Hospital(Locations[4]),HomeMode?"HOSPITAL":LocationNames[4]);
        Snap(6,CityLandmarks.Church(Locations[6]),HomeMode?"CHURCH":LocationNames[6]);
        Snap(7,CityLandmarks.Station(Locations[7]),HomeMode?"STATION":LocationNames[7]);
        Snap(8,CityLandmarks.Dolphinarium(Locations[8]),HomeMode?"DOLPHINARIUM":LocationNames[8]);
    }

    void Snap(int index,CityLandmarks.Spot spot,string name)
    {
        if(index<0||index>=Locations.Length)return;
        Locations[index]=spot.Point;
        if(!string.IsNullOrEmpty(name))LocationNames[index]=name;
    }

    void ClearRailDistrictTrees()
    {
        if(!HomeMode||Locations==null||Locations.Length<=7)return;
        Vector3 rail=Locations[7];int removed=0;
        foreach(var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(!candidate||candidate.parent==null||!candidate.name.ToLowerInvariant().Contains("tree"))continue;
            Vector3 delta=candidate.position-rail;delta.y=0;
            if(delta.sqrMagnitude>58f*58f)continue;
            candidate.gameObject.SetActive(false);removed++;
        }
        if(removed>0)Debug.Log($"[CityGameplay] Cleared {removed} tree objects around Rail District for tactical visibility.");
    }
    void AlignControlPointsToLandmarks()
    {
        var points=FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None);
        Vector3[] offsets={new Vector3(16f,0,-10f),new Vector3(14f,0,12f),new Vector3(-16f,0,10f)};
        int placed=0;
        foreach(var point in points)
        {
            if(!point)continue;
            int index=LocationIndexFor(point.zoneName);if(index<0)continue;
            Vector3 offset=offsets[placed%offsets.Length];
            Vector3 target=ReachableApproach(Home,Locations[index]+offset);
            point.transform.position=target;
            placed++;
            float holdRadius=point.detectionRadius;
            foreach(var gang in FindObjectsByType<GangArea>(FindObjectsSortMode.None))
            {
                if(!gang)continue;
                float need=holdRadius+gang.Radius+CityZoneClearance.Gap;
                Vector3 delta=gang.transform.position-target;delta.y=0f;
                if(delta.sqrMagnitude>=need*need)continue;
                Vector3 dir=delta.sqrMagnitude<.01f?Vector3.right:delta.normalized;
                Vector3 clear=target+dir*need;
                if(NavMesh.SamplePosition(clear,out var hit,16f,NavMesh.AllAreas))clear=hit.position;
                Vector3 old=gang.transform.position;
                gang.transform.position=clear;
                var guards=BattleManager.instance?.EnemyAgents
                    .Where(e=>e&&e.IsAlive&&e.firmName==gang.GangName).ToArray();
                if(guards==null)continue;
                Vector3 shift=clear-old;shift.y=0f;
                for(int i=0;i<guards.Length;i++)
                {
                    Vector3 desired=guards[i].transform.position+shift;
                    if(NavMesh.SamplePosition(desired,out var memberHit,8f,NavMesh.AllAreas))desired=memberHit.position;
                    var nav=guards[i].GetComponent<NavMeshAgent>();if(nav&&nav.enabled&&nav.isOnNavMesh)nav.Warp(desired);else guards[i].transform.position=desired;
                    guards[i].SetGangCenter(clear);
                }
            }
        }
        CityZoneClearance.Resolve();
    }

    int LocationIndexFor(string zone)
    {
        string value=(zone??string.Empty).ToLowerInvariant();
        if(value.Contains("stadium")||value.Contains("stand"))return 5;
        if(value.Contains("pub"))return 1;
        if(value.Contains("training"))return 3;
        for(int i=0;i<LocationNames.Length;i++)
        {
            string location=LocationNames[i].ToLowerInvariant();
            if(location.Contains(value)||value.Contains(location))return i;
            string first=value.Split(' ').FirstOrDefault();if(!string.IsNullOrEmpty(first)&&location.Contains(first))return i;
        }
        return -1;
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
        cameraPanel=LandscapeUI.Panel("CameraSettings",frame,970,300,350,500,true).gameObject;
        LandscapeUI.Text("Heading",cameraPanel.transform,"TACTICAL CAMERA",22,14,270,36,24,null,true);
        float fov=PlayerPrefs.GetFloat("CityCameraFov",65),height=PlayerPrefs.GetFloat("CityCameraHeight",65),pitch=PlayerPrefs.GetFloat("CityCameraPitch",65),yaw=PlayerPrefs.GetFloat("CityCameraYaw",45);
        SliderRow(cameraPanel.transform,"FIELD OF VIEW",65,45,85,fov,v=>{fov=v;Apply();});
        SliderRow(cameraPanel.transform,"HEIGHT",135,35,180,height,v=>{height=v;Apply();});
        SliderRow(cameraPanel.transform,"ANGLE",205,50,85,pitch,v=>{pitch=v;Apply();});
        SliderRow(cameraPanel.transform,"360 ROTATE",275,0,360,yaw,v=>{yaw=v;Apply();});
        LandscapeUI.Button("Rotate45",cameraPanel.transform,"ROTATE 45",20,365,145,48).onClick.AddListener(()=>{yaw=Mathf.Repeat(yaw+45,360);Apply();});
        LandscapeUI.Button("Center",cameraPanel.transform,"RECENTRE",182,365,145,48).onClick.AddListener(()=>CameraPanTouchOnly.Instance?.CenterOnSelection());
        LandscapeUI.Button("Close",cameraPanel.transform,"DONE",20,425,307,48).onClick.AddListener(()=>{PlayerPrefs.Save();cameraPanel.SetActive(false);});
        cameraPanel.SetActive(false);
        void Apply()=>CameraPanTouchOnly.Instance?.ConfigureCity(fov,height,pitch,yaw);
    }
    public void ToggleCameraSettings(){if(cameraPanel)cameraPanel.SetActive(!cameraPanel.activeSelf);}
    public void ShowIntelReport()
    {
        var d=GameManager.Data;if(d==null)return;
        GamePopup.Instance.Show("DISTRICT INTELLIGENCE",
            $"Intel: {d.CityIntel}/10\nSupplies: {d.CitySupplies}\nTickets: {d.MatchTickets}\nSocial momentum: {d.SocialMomentum}\nTransport: {(d.TransportPrepared?"READY":"NOT PREPARED")}\nPolice heat: {d.PoliceHeat}/10\n\nScouting unlocks ticket, police and transport decisions. Use the mission plan to see what each resource enables.",
            new GamePopup.Option("OPEN OPERATIONS",LandscapeUI.Green,()=>CityOperationsSystem.Instance?.OpenBoard()),
            new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));
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

    void BuildLocationMarker(Transform marker,int index)
    {
        var color=index==0?new Color(.15f,.78f,1f,1f):
            HomeMode&&index<5?new Color(.20f,.90f,.48f,1f):new Color(1f,.72f,.12f,1f);
        ZoneVolumeFactory.Create(marker,color,index==0?4.2f:3.1f);
    }
    public void OpenLocation(int index)
    {
        if(Locations==null || index<0 || index>=Locations.Length)return;
        districtPanel.SetActive(false);
        CameraPanTouchOnly.Instance?.FocusOn(Locations[index]);
        var options=new System.Collections.Generic.List<GamePopup.Option>();
        options.Add(new GamePopup.Option("MOVE SELECTED",LandscapeUI.Green,()=>AgentSelectionManager.instance?.CommandSelectedMoveTo(Locations[index])));
        if(HomeMode && index==0)
        {
            options.Add(new GamePopup.Option("MANAGE / AWAY TRIPS",LandscapeUI.PanelColor,()=>{BattleManager.instance.PersistBattleProgress();GameManager.LoadScene(GameManager.SCENE_DASHBOARD);}));
            options.Add(new GamePopup.Option("DEFEND HOME",LandscapeUI.Red,BeginDefence));
        }
        if(HomeMode && index==1)options.Add(new GamePopup.Option("RALLY CREW - 100",LandscapeUI.Green,()=>Service(index,100)));
        if(HomeMode && index==2)
        {
            OpenRecruitment(Locations[index],LocationNames[index]);
            return;
        }
        if(HomeMode && index==3)options.Add(new GamePopup.Option("TRAIN SQUAD - 300",LandscapeUI.Green,()=>Service(index,300)));
        if(HomeMode && index==4)options.Add(new GamePopup.Option("RECOVER ALL - " + GameplayTuning.Current.recoveryCost + " EACH",LandscapeUI.Green,()=>Service(index,GameplayTuning.Current.recoveryCost)));
        if(!HomeMode && index>0)options.Add(new GamePopup.Option("SECURE THIS AREA",LandscapeUI.Green,CaptureNearest));
        options.Add(new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));
        string description=HomeMode
            ? index==0?"Prepare the firm, organize the squad and protect your district.":index<5?"Services are available to your squad at this location.":index==5?"The marker is attached to the real stadium model. Organize tickets, transport and supporters around its matchday approach.":"Home city destination"
            : index==5?"This approach is attached to the real stadium model. Secure the away end after preparing tickets and transport.":"Travelled territory. Move carefully, find the rival pressure points and get the crew back out.";
        if (HomeMode && index == 0) description += "\n\nCONTROLLED TERRITORY\n" + (GameManager.Data?.CityCapturedZones?.Count > 0 ? string.Join("\n", GameManager.Data.CityCapturedZones) : "Your home district. Secure away territory to expand your firm.");
        GamePopup.Instance.Show(LocationNames[index],description,options.ToArray());
    }
    void Service(int index,int cost)
    {
        var bm=BattleManager.instance;var d=GameManager.Data;
        if(d==null)return;
        var nearby=bm.PlayerAgents.Where(a=>a&&a.IsAlive&&Vector3.Distance(a.transform.position,Locations[index])<16).ToArray();
        if(nearby.Length==0 && index!=4){PostEvent("MOVE YOUR SQUAD TO "+LocationNames[index]);return;}
        if(d.Money<cost){PostEvent("INSUFFICIENT FUNDS");return;}
        if(index==2 && bm.PlayerAgents.Count(a=>a&&a.IsAlive)>=bm.maxPlayerAgents){PostEvent("SQUAD FULL");return;}
        if(index==3 && d.HomeTrainingLevel>=5){PostEvent("TRAINING COMPLETE");return;}
        if(index==4)
        {
            bm.PersistBattleProgress();
            // Keep the persisted roster authoritative even if it was reloaded while the city scene stayed alive.
            foreach(var live in bm.PlayerAgents.Where(a=>a&&a.Data!=null))
            {
                var saved=d.RecruitedAgents?.FirstOrDefault(a=>a!=null&&a.AgentId==live.Data.AgentId);
                if(saved!=null)saved.CurrentHp=live.CurrentHp;
            }
        }
        if(index==4 && (d.RecruitedAgents==null || d.RecruitedAgents.All(a=>a==null || a.CurrentHp>=a.MaxHp))){PostEvent("SQUAD ALREADY HEALTHY");return;}
        if(index==4)
        {
            var injured=d.RecruitedAgents.Where(a=>a!=null && a.CurrentHp<a.MaxHp).ToArray();
            int total=injured.Length*cost;
            if(d.Money<total){PostEvent("INSUFFICIENT FUNDS");return;}
            if (!SquadCare.RecoverAll(total)) return;
            bm.PersistBattleProgress();
            PostEvent("RECOVERY COMPLETE - "+injured.Length+" MEMBER"+(injured.Length==1?"":"S")+" RESTORED");
            GamePopup.Instance.Show("RECOVERY COMPLETE",$"{injured.Length} injured member{(injured.Length==1?"":"s")} restored and ready for selection.\nCost: £{total:N0}",new GamePopup.Option("BACK TO MAP",LandscapeUI.Green,()=>CameraPanTouchOnly.Instance?.FocusOn(Locations[index])));
            return;
        }
        d.Money-=cost;
        if(index==1)d.FanMorale=Mathf.Min(100,d.FanMorale+10);
        if(index==2)bm.SpawnRecruitedAgentAt(Locations[index],"Local Recruit");
        if(index==3){foreach(var a in nearby)a.Data.Strength+=2;d.HomeTrainingLevel++;d.LastTrainingMatchday=d.MatchDay;}
        bm.PersistBattleProgress();PostEvent(LocationNames[index]+" - COMPLETE");CheckCampaignCompletion();
    }
    /// <summary>
    /// Opens the same four-campaign recruitment board used at headquarters.
    /// New members spawn at this venue; the crew is sent here if they are not already on site.
    /// </summary>
    public void OpenRecruitment(Vector3 at,string venue)
    {
        var bm=BattleManager.instance;
        if(bm!=null&&!bm.PlayerAgents.Any(a=>a&&a.IsAlive&&Vector3.Distance(a.transform.position,at)<18f))
        {
            AgentSelectionManager.instance?.CommandSelectedMoveTo(at);
            AgentSelectionManager.CreateCommandMarker(at,LandscapeUI.Green,"RECRUITMENT");
            PostEvent("CREW MOVING TO "+venue);
        }
        CameraPanTouchOnly.Instance?.FocusOn(at);
        RecruitPackagePanel.Instance.Show(at,venue);
    }
    // Legacy quick-queue kept for the editor service checks.
    void QueueFans()
    {
        var d=GameManager.Data;
        if(d==null)return;
        if(!BattleManager.instance.PlayerAgents.Any(a=>a&&a.IsAlive&&Vector3.Distance(a.transform.position,Locations[2])<16))
        {PostEvent("MOVE YOUR SQUAD TO RECRUITMENT");return;}
        if(d.Money<300){PostEvent("INSUFFICIENT FUNDS");return;}
        d.Money-=300;d.PendingFansGain+=2;RivalGrowthSystem.NoteQueuedRecruits(2);GameManager.Save();PostEvent("2 FANS QUEUED FOR NEXT MATCHDAY");
    }
    public void CaptureNearest()
    {
        var squad=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();
        if(squad.Length==0)return;
        var target=FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None)
            .Where(p=>!p.IsCaptured).OrderBy(p=>Vector3.Distance(p.transform.position,squad[0].transform.position)).FirstOrDefault();
        if(!target){PostEvent("ALL AVAILABLE TERRITORY SECURED");return;}
        AgentSelectionManager.instance.CommandSelectedMoveTo(target.transform.position);
        AgentSelectionManager.CreateCommandMarker(target.transform.position,new Color(1f,.72f,.12f,.95f),"CAPTURE");
        BattleUIController.instance?.ShowAlert("CAPTURE ORDER - CLEAR AND HOLD",1.8f);
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
        CameraPanTouchOnly.Instance?.FocusOn(Home);
    }
    void Update()
    {
        if(!defending)return;
        defenceTime-=Time.deltaTime;
        int remaining=intruders.Count(e=>e&&e.IsAlive);
        homeIntegrity-=intruders.Count(e=>e&&e.IsAlive&&Vector3.Distance(e.transform.position,Home)<7)*Time.deltaTime*3;
        if(status)status.text=$"DEFEND HQ  {initialRivals-remaining}/{initialRivals}  HQ {Mathf.CeilToInt(homeIntegrity)}%  {Mathf.CeilToInt(defenceTime)}s";
        if(remaining==0)
        {
            defending=false;var d=GameManager.Data;
            if(!d.HomeDefenceCompleted){d.HomeDefenceCompleted=true;d.Money+=1500;d.Reputation+=5;GameManager.Save();}
            PostEvent("HOME DEFENDED +£1,500 / +5 REP");
            if(!CheckCampaignCompletion())
                GamePopup.Instance.Show("HEADQUARTERS SECURED","The intrusion is over. Your district is safe.\n£1,500 and 5 reputation saved. Finish the remaining mission steps to unlock Riverside.",new GamePopup.Option("RETURN TO HQ",LandscapeUI.Green,()=>CameraPanTouchOnly.Instance?.FocusOn(Home)));
        }
        else if(defenceTime<=0 || homeIntegrity<=0 || !BattleManager.instance.PlayerAgents.Any(a=>a&&a.IsAlive))
        {
            defending=false;
            foreach(var enemy in intruders)if(enemy){enemy.defenceObjective=null;enemy.isHostile=false;}
            PostEvent("DEFENCE FAILED - REGROUP AT HEADQUARTERS");
        }
    }

    public bool CheckCampaignCompletion()
    {
        var d=GameManager.Data;
        if(!CampaignMissions.TryComplete(d,out var mission))return false;
        BattleManager.instance?.PersistBattleProgress();GameManager.Save();
        bool campaignComplete=d.CompletedCampaignMissions!=null&&d.CompletedCampaignMissions.Count>=CampaignMissions.MissionCount;
        string next=campaignComplete?"The five-mission campaign is complete.":$"Mission {d.CurrentLevel:00} is now unlocked: {CampaignMissions.Active(d).title}.";
        PostEvent($"MISSION {mission.number:00} COMPLETE +£{mission.reward:N0}");
        GamePopup.Instance.Show(
            campaignComplete?"CAMPAIGN COMPLETE":$"MISSION {mission.number:00} COMPLETE",
            mission.title+"\n\nAll linked tasks are complete. Reward: £"+mission.reward.ToString("N0")+"\n"+next,
            new GamePopup.Option(campaignComplete?"RETURN TO HQ":"PLAN NEXT MISSION",LandscapeUI.Green,()=>
            {
                BattleManager.instance?.PersistBattleProgress();
                GameManager.instance?.ReturnToDashboard();
            }));
        return true;
    }
}
