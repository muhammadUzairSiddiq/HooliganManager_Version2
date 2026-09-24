using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Lightweight, mobile-safe matchday life around the home stadium.
/// Supporters stay local rather than consuming the city-wide pedestrian budget.
/// </summary>
public sealed class StadiumMatchdayActivity : MonoBehaviour
{
    const int SupporterCount = 8;
    readonly List<GameObject> supporters = new List<GameObject>();
    Vector3 stadiumCenter;
    enum MatchdayPhase { Arrival, BuildUp, RivalPressure, Kickoff, Aftermath }
    MatchdayPhase phase=MatchdayPhase.Arrival;
    float phaseClock;
    bool rivalFansSpawned;
    bool eventPopupShown;
    bool atmosphereEventResolved;
    readonly List<GameObject> rivalFans=new List<GameObject>();
    readonly List<GameObject> policePresence=new List<GameObject>();
    TextMeshPro phaseLabel, eventLabel;
    const float PhaseDuration=22f;

    public int ActiveFanCount
    {
        get
        {
            supporters.RemoveAll(x => !x);
            return supporters.Count;
        }
    }

    public bool AtmosphereReady { get; private set; }
    public string CurrentPhase => phase.ToString().ToUpperInvariant();
    public int RivalFanCount { get { rivalFans.RemoveAll(x=>!x); return rivalFans.Count; } }
    public int PolicePresenceCount { get { policePresence.RemoveAll(x=>!x); return policePresence.Count; } }
    public bool EscalationResolved => atmosphereEventResolved;
    public bool RealModelBound { get; private set; }

    public static StadiumMatchdayActivity Ensure(Vector3 center)
    {
        var existing = FindFirstObjectByType<StadiumMatchdayActivity>();
        if (existing) return existing;
        var root = new GameObject("Stadium Matchday Activity");
        root.transform.position = center;
        var activity = root.AddComponent<StadiumMatchdayActivity>();
        activity.stadiumCenter = center;
        return activity;
    }

    IEnumerator Start()
    {
        BuildAtmosphere();

        PedestrianSpawner spawner = null;
        float timeout = 12f;
        while (timeout > 0f)
        {
            spawner = FindFirstObjectByType<PedestrianSpawner>();
            if (spawner && spawner.IsInitialized) break;
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!spawner || !spawner.IsInitialized)
        {
            Debug.LogWarning("[StadiumMatchdayActivity] Pedestrian spawner was not ready; visual atmosphere remains active.");
            yield break;
        }

        for (int i = 0; i < SupporterCount; i++)
        {
            var supporter = spawner.SpawnActivityPedestrian(stadiumCenter, 24f, transform, "STADIUM", true);
            if (supporter) supporters.Add(supporter);
            yield return null;
        }

        var pub=FindObjectsByType<CitySocialActivity>(FindObjectsSortMode.None).FirstOrDefault(x=>x&&x.Venue.Contains("PUB"));
        pub?.IncreaseForMatchday(1);
        foreach(var life in FindObjectsByType<CityLifeActivity>(FindObjectsSortMode.None))
            life?.IncreaseForMatchday(1);
        CityGameplay.Instance?.PostEvent($"MATCHDAY {matchday:00} · ARRIVAL WINDOW · FANS ARE MOVING TOWARD THE STADIUM");
    }

    int matchday => GameManager.Data?.MatchDay ?? 1;

    bool pendingEscalation;

    void Update()
    {
        if(pendingEscalation)ShowEscalationChoice();
        if(!AtmosphereReady)return;
        phaseClock+=Time.deltaTime;
        if(phase==MatchdayPhase.Aftermath)return;
        if(phaseClock<PhaseDuration)return;
        phaseClock=0f;
        phase=(MatchdayPhase)Mathf.Min((int)MatchdayPhase.Aftermath,(int)phase+1);
        ApplyPhase(phase);
    }

    void ApplyPhase(MatchdayPhase next)
    {
        switch(next)
        {
            case MatchdayPhase.BuildUp:
                SetPhaseText("BUILD-UP",new Color(1f,.78f,.20f));
                CityGameplay.Instance?.PostEvent("MATCHDAY BUILD-UP · PUBS ARE FULL · STREET ACTIVITY IS RISING");
                SpawnPolicePresence(2);
                break;
            case MatchdayPhase.RivalPressure:
                SetPhaseText("RIVAL FANS ARRIVING",new Color(1f,.28f,.24f));
                SpawnRivalFans();SpawnPolicePresence(3);
                CityGameplay.Instance?.PostEvent("RIVAL FANS ARRIVE · CHOOSE HOW TO PROTECT THE HOME SUPPORTERS");
                ShowEscalationChoice();
                break;
            case MatchdayPhase.Kickoff:
                SetPhaseText("KICKOFF",new Color(.25f,1f,.60f));
                CityGameplay.Instance?.PostEvent("KICKOFF · STADIUM APPROACH IS ACTIVE · KEEP THE ROUTES CLEAR");
                break;
            case MatchdayPhase.Aftermath:
                SetPhaseText("POST-MATCH",new Color(.45f,.78f,1f));
                CityGameplay.Instance?.PostEvent("POST-MATCH WINDOW · FANS DISPERSE · POLICE ARE WATCHING THE EXITS");
                break;
        }
    }

    void ShowEscalationChoice()
    {
        if(eventPopupShown||atmosphereEventResolved)return;
        if(CityActionSystem.TaxiSessionActive||GamePopup.AnyOpen)
        {
            pendingEscalation=true;
            return;
        }
        pendingEscalation=false;
        eventPopupShown=true;
        var d=GameManager.Data;
        GamePopup.Instance.Show("MATCHDAY ESCALATION",
            "Rival fans have reached the stadium approach. This is the Home Territory's live strategic event.\n\n"+
            "The choice changes morale, money and police heat. Resolve it before kickoff.",
            new GamePopup.Option("ORGANIZE STEWARDS",new Color(.12f,.70f,.55f),()=>ResolveEscalation("STEWARDS")),
            new GamePopup.Option("ESCORT SUPPORTERS",new Color(.12f,.55f,.85f),()=>ResolveEscalation("ESCORT")),
            new GamePopup.Option("CONFRONT RIVALS",LandscapeUI.Red,()=>ResolveEscalation("CONFRONT")));
    }

    void ResolveEscalation(string choice)
    {
        if(atmosphereEventResolved)return;
        atmosphereEventResolved=true;
        var d=GameManager.Data;
        if(d!=null)
        {
            switch(choice)
            {
                case "STEWARDS": d.FanMorale=Mathf.Min(100,d.FanMorale+6);d.SocialMomentum+=1;d.PoliceHeat=Mathf.Clamp(d.PoliceHeat+1,0,10);break;
                case "ESCORT": d.Money=Mathf.Max(0,d.Money-200);d.FanMorale=Mathf.Min(100,d.FanMorale+4);d.PoliceHeat=Mathf.Max(0,d.PoliceHeat-1);break;
                default: d.FanMorale=Mathf.Max(0,d.FanMorale-3);d.PoliceHeat=Mathf.Clamp(d.PoliceHeat+3,0,10);break;
            }
            CampaignMissions.RecordAction(d,"matchday","rival-arrival");
            GameManager.Save();
        }
        CityGameplay.Instance?.PostEvent("MATCHDAY DECISION · "+choice+" · THE HOME DISTRICT REMEMBERS");
        CityGameplay.Instance?.CheckCampaignCompletion();
        if(eventLabel)eventLabel.text="EVENT RESOLVED · "+choice;
    }

    public void OpenBriefing()
    {
        var d=GameManager.Data;
        GamePopup.Instance.Show("HOME MATCHDAY · "+matchday.ToString("00"),
            $"PHASE: {CurrentPhase}\n\nFans at stadium: {ActiveFanCount}\nRival fans: {RivalFanCount}\nPolice presence: {PolicePresenceCount}\nPolice heat: {d?.PoliceHeat??0}/10\n\n{(atmosphereEventResolved?"Rival-arrival event resolved. Keep the routes clear through the post-match window.":"Rival-arrival escalation is pending. Choose a response when the rival fans reach the stadium.")}",
            new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));
    }

    void BuildAtmosphere()
    {
        var model=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t&&t.name=="stadium_001");
        if(model)
        {
            var renderers=model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds=renderers.Length>0?renderers[0].bounds:new Bounds(model.position,new Vector3(40,4,40));
            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
            RealModelBound=Vector2.Distance(new Vector2(bounds.center.x,bounds.center.z),new Vector2(stadiumCenter.x,stadiumCenter.z))<=Mathf.Max(bounds.extents.x,bounds.extents.z)+80f;
        }
        int matchday = GameManager.Data?.MatchDay ?? 1;
        var title = ZoneLabelUtil.Create(transform, "MATCHDAY " + matchday.ToString("00"), 5.5f, 6.6f);
        title.name = "MatchdayAtmosphereLabel";
        title.alignment = TextAlignmentOptions.Center;
        phaseLabel=ZoneLabelUtil.Create(transform,"ARRIVAL WINDOW\n<size=66%>FANS MOVING TO STADIUM</size>",4.8f,8.4f);
        phaseLabel.name="MatchdayPhaseLabel";
        eventLabel=ZoneLabelUtil.Create(transform,"MATCHDAY EVENT\n<size=66%>WATCH THE STADIUM APPROACH</size>",4.0f,10.2f);
        eventLabel.name="MatchdayEventLabel";

        AtmosphereReady = true;
    }

    void BuildMatchdayFacilities(Color primary, Color secondary)
    {
        // Original lightweight props: ticket/food stalls and queue lanes make
        // the stadium approach read as a functioning matchday destination.
        for (int i = -1; i <= 1; i++)
        {
            var stall = new GameObject("Matchday Vendor Stall");
            stall.transform.SetParent(transform, false);
            stall.transform.localPosition = new Vector3(i * 7f, 0f, -13f);
            Primitive(stall.transform, PrimitiveType.Cube, "Vendor Counter", new Vector3(0f, 1f, 0f), new Vector3(4.2f, 2f, 2.1f), i % 2 == 0 ? primary : secondary);
            Primitive(stall.transform, PrimitiveType.Cube, "Striped Awning", new Vector3(0f, 2.45f, 0f), new Vector3(4.8f, .28f, 2.6f), i % 2 == 0 ? secondary : primary);
        }

        for (int lane = -2; lane <= 2; lane++)
        {
            Primitive(transform, PrimitiveType.Cube, "Stadium Queue Rail", new Vector3(lane * 3f, .55f, 6f), new Vector3(.10f, 1.1f, 10f), new Color(.72f, .74f, .76f));
        }
    }

    static void Primitive(Transform parent, PrimitiveType type, string objectName, Vector3 localPosition, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;
        RemoveCollider(go);
        Paint(go, color);
    }

    void BuildFlag(Vector3 localPosition, Color primary, Color secondary, bool flip)
    {
        var root = new GameObject("Supporter Banner");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = localPosition;
        root.transform.LookAt(transform.position);

        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Banner Pole";
        pole.transform.SetParent(root.transform, false);
        pole.transform.localPosition = Vector3.up * 2.2f;
        pole.transform.localScale = new Vector3(.08f, 2.2f, .08f);
        RemoveCollider(pole);
        Paint(pole, new Color(.65f, .68f, .70f));

        var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flag.name = "Club Colour Flag";
        flag.transform.SetParent(root.transform, false);
        flag.transform.localPosition = new Vector3(flip ? -.7f : .7f, 3.65f, 0f);
        flag.transform.localScale = new Vector3(1.35f, .72f, .06f);
        RemoveCollider(flag);
        Paint(flag, flip ? primary : secondary);
    }

    void BuildApproachLights(Color clubColor)
    {
        for (int i = -3; i <= 3; i++)
        {
            var light = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            light.name = "Matchday Approach Light";
            light.transform.SetParent(transform, false);
            light.transform.localPosition = new Vector3(i * 4f, .08f, 12f);
            light.transform.localScale = new Vector3(.55f, .08f, .55f);
            RemoveCollider(light);
            Paint(light, Color.Lerp(clubColor, Color.white, .25f));
        }
    }

    void SetPhaseText(string value,Color color)
    {
        if(phaseLabel){phaseLabel.text=value+"\n<size=66%>HOME STADIUM MATCHDAY</size>";phaseLabel.color=Color.white;}
        if(eventLabel&&!atmosphereEventResolved)eventLabel.text="LIVE EVENT\n<size=66%>DECIDE HOW TO HANDLE THE CROWD</size>";
    }

    void SpawnRivalFans()
    {
        if(rivalFansSpawned)return;rivalFansSpawned=true;
        var spawner=FindFirstObjectByType<PedestrianSpawner>();if(!spawner||!spawner.IsInitialized)return;
        for(int i=0;i<3;i++)
        {
            float angle=Mathf.PI*2f*i/3f;Vector3 pos=stadiumCenter+new Vector3(Mathf.Cos(angle)*30f,0,Mathf.Sin(angle)*30f);
            var fan=spawner.SpawnActivityPedestrian(pos,5f,transform,"RIVAL FANS",true);
            if(!fan)continue;rivalFans.Add(fan);
            ZoneVolumeFactory.Create(fan.transform,new Color(1f,.15f,.12f,1f),2.2f);
            var label=ZoneLabelUtil.Create(fan.transform,"RIVAL FAN",3.8f,5.6f);
        }
    }

    void SpawnPolicePresence(int count)
    {
        int current=PolicePresenceCount;for(int i=current;i<count;i++)
        {
            float angle=Mathf.PI*2f*i/Mathf.Max(1,count);var go=new GameObject("Matchday Police Presence");go.transform.position=stadiumCenter+new Vector3(Mathf.Cos(angle)*20f,.04f,Mathf.Sin(angle)*20f);
            ZoneVolumeFactory.Create(go.transform,new Color(.20f,.48f,1f,1f),3.4f);
            var label=ZoneLabelUtil.Create(go.transform,"POLICE WATCH",3.4f,5.8f);
            MiniMapIconFactory.Register(go.transform,MiniMapIconFactory.Kind.Police,"POLICE");policePresence.Add(go);
        }
        if(GameManager.Data!=null)GameManager.Data.MatchdayPolicePresence=count;
    }

    static Color ReadClubColor(string html, Color fallback)
    {
        return !string.IsNullOrWhiteSpace(html) && ColorUtility.TryParseHtmlString(html, out Color parsed) ? parsed : fallback;
    }

    static void RemoveCollider(GameObject go)
    {
        var collider = go.GetComponent<Collider>();
        if (collider) Destroy(collider);
    }

    static void Paint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (!renderer) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader) renderer.material = new Material(shader) { color = color };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
