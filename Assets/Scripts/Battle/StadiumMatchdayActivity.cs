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
    const int SupporterCount = 10;
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
    readonly List<MatchdayGroupState> groups = new List<MatchdayGroupState>();
    int conflictsStarted;
    TextMeshPro phaseLabel, eventLabel;
    const float PhaseDuration=22f;

    public int ActiveFanCount
    {
        get
        {
            supporters.RemoveAll(x => !x);
            homeBodies.RemoveAll(x => !x || !x.IsAlive);
            return supporters.Count + homeBodies.Count;
        }
    }

    public bool AtmosphereReady { get; private set; }
    public string CurrentPhase => phase.ToString().ToUpperInvariant();
    public int RivalFanCount
    {
        get
        {
            rivalFans.RemoveAll(x => !x);
            rivalBodies.RemoveAll(x => !x || !x.IsAlive);
            return rivalFans.Count + rivalBodies.Count;
        }
    }
    public int PolicePresenceCount
    {
        get
        {
            policePresence.RemoveAll(x => !x);
            return policePresence.Count + groups
                .Where(g => g.Role == MatchdayUnitRole.Police)
                .Sum(g => g.Members.Count(x => x && x.IsAlive));
        }
    }
    public bool EscalationResolved => atmosphereEventResolved;
    public bool RealModelBound { get; private set; }
    public int ActiveGroupCount => groups.Count(g => g != null && g.Members.Any(m => m && m.IsAlive));
    public int ActiveConflictCount => groups.Count(g => g != null && g.InConflict) / 2;

    sealed class MatchdayGroupState
    {
        public string Name;
        public Color Color;
        public MatchdayUnitRole Role;
        public Vector3 HomePocket;
        public readonly List<EnemyController> Members = new List<EnemyController>();
        public MatchdayGroupMarker Marker;
        public float NextTaskAt;
        public int TaskIndex;
        public bool InConflict;
    }

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
        StartCoroutine(LivingMatchday());

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
        pub?.IncreaseForMatchday(4);
        foreach(var life in FindObjectsByType<CityLifeActivity>(FindObjectsSortMode.None))
            life?.IncreaseForMatchday(1);
        CityGameplay.Instance?.PostEvent($"MATCHDAY {matchday:00} · ARRIVAL WINDOW · FANS ARE MOVING TOWARD THE STADIUM");
    }

    int matchday => GameManager.Data?.MatchDay ?? 1;

    bool pendingEscalation;

    void Update()
    {
        if(pendingEscalation)ShowEscalationChoice();
        UpdateGroupTasks();
        ScanClashes();
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
                BeginConcurrentFlashpoints(1);
                break;
            case MatchdayPhase.RivalPressure:
                SetPhaseText("RIVAL FANS ARRIVING",new Color(1f,.28f,.24f));
                SpawnRivalFans();SpawnPolicePresence(3);
                BeginConcurrentFlashpoints(2);
                CityGameplay.Instance?.PostEvent("RIVAL FANS ARRIVE · CHOOSE HOW TO PROTECT THE HOME SUPPORTERS");
                ShowEscalationChoice();
                break;
            case MatchdayPhase.Kickoff:
                SetPhaseText("KICKOFF",new Color(.25f,1f,.60f));
                CityGameplay.Instance?.PostEvent("KICKOFF · STADIUM APPROACH IS ACTIVE · KEEP THE ROUTES CLEAR");
                AssignPhaseTasks("TURNSTILE MOVEMENT");
                break;
            case MatchdayPhase.Aftermath:
                SetPhaseText("POST-MATCH",new Color(.45f,.78f,1f));
                CityGameplay.Instance?.PostEvent("POST-MATCH WINDOW · FANS DISPERSE · POLICE ARE WATCHING THE EXITS");
                AssignPhaseTasks("POST-MATCH DISPERSAL");
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
        GamePopup.Instance.Show("MATCHDAY",
            "Stewards — calm the crowd. Morale up.\nEscort — walk fans in. Costs £200.\nConfront — fight the rivals. Heat up.",
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
        if (choice == "STEWARDS") DeescalateOneFlashpoint();
        else if (choice == "ESCORT") AssignPhaseTasks("ESCORTED TO TURNSTILES");
        else BeginConcurrentFlashpoints(2);
        CityGameplay.Instance?.CheckCampaignCompletion();
        if(eventLabel)eventLabel.text="EVENT RESOLVED · "+choice;
    }

    public void OpenBriefing()
    {
        var d=GameManager.Data;
        GamePopup.Instance.Show("HOME MATCHDAY · "+matchday.ToString("00"),
            $"PHASE: {CurrentPhase}\n\nSupporters at stadium: {ActiveFanCount}\nActive groups: {ActiveGroupCount}\nLive flashpoints: {ActiveConflictCount}\nRival arrivals: {RivalFanCount}\nPolice presence: {PolicePresenceCount}\nPolice heat: {d?.PoliceHeat??0}/10\n\n{(atmosphereEventResolved?"Rival-arrival event resolved. Keep the routes clear through the post-match window.":"Rival-arrival escalation is pending. Choose a response when the rival fans reach the stadium.")}",
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

        var d = GameManager.Data;
        Color primary = ReadClubColor(d?.PrimaryColor, new Color(.82f, .12f, .16f));
        Color secondary = ReadClubColor(d?.SecondaryColor, Color.white);
        BuildMatchdayFacilities(primary, secondary);
        BuildFlag(new Vector3(-10f, 0f, -8f), primary, secondary, false);
        BuildFlag(new Vector3(10f, 0f, -8f), primary, secondary, true);
        BuildApproachLights(primary);

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
        if(phaseLabel){phaseLabel.text=value+"\n<size=66%>HOME STADIUM MATCHDAY</size>";phaseLabel.color=color;}
        if(eventLabel&&!atmosphereEventResolved)eventLabel.text="LIVE EVENT\n<size=66%>DECIDE HOW TO HANDLE THE CROWD</size>";
    }

    readonly List<EnemyController> rivalBodies = new List<EnemyController>();
    readonly List<EnemyController> homeBodies = new List<EnemyController>();
    bool livingStarted;
    float clashScan;

    IEnumerator LivingMatchday()
    {
        float wait = 10f;
        while (wait > 0f && BattleManager.instance == null)
        {
            wait -= Time.deltaTime;
            yield return null;
        }
        var bm = BattleManager.instance;
        if (!bm || livingStarted) yield break;
        livingStarted = true;

        // Every active campaign gang gets its own named, colour-matched away group.
        // This keeps the stadium representative of the current RTS map instead of
        // inventing unrelated anonymous crowds.
        var activeGangs = FindObjectsByType<GangArea>(FindObjectsSortMode.None)
            .Where(g => g && !string.IsNullOrWhiteSpace(g.GangName))
            .GroupBy(g => g.GangName)
            .Select(g => g.First())
            .ToList();
        if (activeGangs.Count == 0)
        {
            var bots = GameManager.Data?.RivalBots;
            if (bots != null)
            {
                foreach (var bot in bots.Take(4))
                {
                    Color color;
                    if (!ColorUtility.TryParseHtmlString(bot.primaryColor, out color))
                        color = new Color(.85f, .18f, .16f);
                    CreateSupporterGroup(bm, bot.firmName, color, MatchdayUnitRole.RivalSupporter,
                        StadiumPocket(groups.Count, 27f), 3);
                }
            }
        }
        else
        {
            for (int i = 0; i < activeGangs.Count; i++)
                CreateSupporterGroup(bm, activeGangs[i].GangName, activeGangs[i].ZoneColor,
                    MatchdayUnitRole.RivalSupporter, StadiumPocket(i, 27f), 3);
        }

        // The player's firm has the strongest visible presence: three separate
        // supporter groups and more members than each individual rival group.
        var data = GameManager.Data;
        string homeName = !string.IsNullOrWhiteSpace(data?.FirmName) ? data.FirmName : "HOME SUPPORT";
        Color homeColor = ReadClubColor(data?.PrimaryColor, new Color(.25f, .95f, .45f));
        Vector3[] homeSpots =
        {
            stadiumCenter + new Vector3(15f, 0f, -10f),
            stadiumCenter + new Vector3(-15f, 0f, -11f),
            stadiumCenter + new Vector3(0f, 0f, -18f)
        };
        foreach (Vector3 homeSpot in homeSpots)
            CreateSupporterGroup(bm, homeName, homeColor, MatchdayUnitRole.HomeSupporter, homeSpot, 4);

        // Four independent foot-patrol groups continuously circulate around the ground.
        Color policeColor = new Color(0.15f, 0.38f, 0.95f);
        for (int i = 0; i < 4; i++)
            CreateSupporterGroup(bm, "POLICE", policeColor, MatchdayUnitRole.Police,
                StadiumPocket(i, 20f, 45f), 1);

        AssignPhaseTasks("ARRIVING IN GROUPS");

        CityGameplay.Instance?.PostEvent("MATCHDAY · RIVAL GROUPS, HOME SUPPORT AND POLICE ARE MOVING AROUND THE STADIUM");
    }

    MatchdayGroupState CreateSupporterGroup(BattleManager bm, string groupName, Color color,
                                             MatchdayUnitRole role, Vector3 pocket, int count)
    {
        pocket = Sample(pocket);
        var state = new MatchdayGroupState
        {
            Name = groupName,
            Color = color,
            Role = role,
            HomePocket = pocket,
            NextTaskAt = Time.time + Random.Range(5f, 10f),
            TaskIndex = groups.Count
        };

        var markerObject = new GameObject("Matchday Group · " + groupName);
        markerObject.transform.SetParent(transform, true);
        markerObject.transform.position = pocket;
        state.Marker = markerObject.AddComponent<MatchdayGroupMarker>();
        state.Marker.Setup(groupName, color, role, 5.2f);

        for (int member = 0; member < count; member++)
        {
            float angle = member * Mathf.PI * 2f / Mathf.Max(1, count);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.45f;
            var body = bm.SpawnMatchdayFighter(pocket + offset, groupName, color,
                role == MatchdayUnitRole.Police ? 85f : 65f,
                role == MatchdayUnitRole.Police ? 8f : 7f, role, groupName);
            if (!body) continue;
            body.GetComponent<MatchdayMarch>()?.Bind(pocket, 4.6f, "ARRIVING IN GROUPS");
            state.Members.Add(body);
            if (role == MatchdayUnitRole.HomeSupporter) homeBodies.Add(body);
            else if (role == MatchdayUnitRole.RivalSupporter) rivalBodies.Add(body);
        }
        state.Marker.Bind(state.Members);
        groups.Add(state);
        return state;
    }

    Vector3 StadiumPocket(int index, float radius, float offsetDegrees = 20f)
    {
        int rivalCount = Mathf.Max(1, FindObjectsByType<GangArea>(FindObjectsSortMode.None).Length);
        float angle = (offsetDegrees + index * 360f / Mathf.Max(4, rivalCount)) * Mathf.Deg2Rad;
        return stadiumCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    void UpdateGroupTasks()
    {
        if (!livingStarted || groups.Count == 0) return;
        foreach (var group in groups)
        {
            if (group == null || group.InConflict || Time.time < group.NextTaskAt) continue;
            group.NextTaskAt = Time.time + Random.Range(7f, 12f);
            group.TaskIndex++;

            string[] tasks;
            float radius;
            switch (group.Role)
            {
                case MatchdayUnitRole.HomeSupporter:
                    tasks = new[] { "CHANT & RALLY", "QUEUE AT TURNSTILES", "ESCORT HOME FANS", "WATCH HOME END" };
                    radius = phase == MatchdayPhase.Aftermath ? 31f : 13f + group.TaskIndex % 3 * 3f;
                    break;
                case MatchdayUnitRole.RivalSupporter:
                    tasks = new[] { "ARRIVING TOGETHER", "REGROUP AT AWAY END", "SCOUT STADIUM ROUTE", "MOVE TO TURNSTILES" };
                    radius = phase == MatchdayPhase.Aftermath ? 36f : 22f + group.TaskIndex % 2 * 4f;
                    break;
                default:
                    tasks = new[] { "FOOT PATROL", "WATCH APPROACH", "SEPARATE GROUPS", "PROTECT GATES" };
                    radius = 18f + group.TaskIndex % 3 * 4f;
                    break;
            }
            Vector3 destination = StadiumPocket(group.TaskIndex + groups.IndexOf(group), radius, group.Role == MatchdayUnitRole.HomeSupporter ? 210f : 30f);
            SetGroupTask(group, tasks[group.TaskIndex % tasks.Length], destination, group.Role == MatchdayUnitRole.Police ? 8f : 4.8f);
        }
    }

    void SetGroupTask(MatchdayGroupState group, string task, Vector3 destination, float radius)
    {
        if (group == null) return;
        destination = Sample(destination);
        group.Marker?.SetTask(task);
        foreach (var member in group.Members)
        {
            if (!member || !member.IsAlive || member.isHostile) continue;
            member.GetComponent<MatchdayMarch>()?.Bind(destination, radius, task);
        }
    }

    void AssignPhaseTasks(string task)
    {
        foreach (var group in groups)
        {
            if (group == null || group.InConflict) continue;
            Vector3 destination = phase == MatchdayPhase.Aftermath
                ? stadiumCenter + (group.HomePocket - stadiumCenter).normalized * 38f
                : group.HomePocket;
            SetGroupTask(group, task, destination, group.Role == MatchdayUnitRole.Police ? 9f : 5f);
        }
    }

    void BeginConcurrentFlashpoints(int desired)
    {
        var rivals = groups.Where(g => g.Role == MatchdayUnitRole.RivalSupporter && !g.InConflict).ToList();
        var homes = groups.Where(g => g.Role == MatchdayUnitRole.HomeSupporter && !g.InConflict).ToList();
        int count = Mathf.Min(desired, Mathf.Min(rivals.Count, homes.Count));
        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector3 flashpoint = Sample(stadiumCenter + new Vector3(side * (18f + i * 4f), 0f, 6f + i * 9f));
            QueueFlashpoint(rivals[i], homes[i], flashpoint);
        }
    }

    void QueueFlashpoint(MatchdayGroupState rival, MatchdayGroupState home, Vector3 location)
    {
        rival.InConflict = home.InConflict = true;
        SetGroupTask(rival, "MOVING TO FLASHPOINT", location + Vector3.right * 1.6f, 2.2f);
        SetGroupTask(home, "HOLDING HOME ROUTE", location - Vector3.right * 1.6f, 2.2f);
        conflictsStarted++;
        CityGameplay.Instance?.PostEvent("LIVE FLASHPOINT · " + rival.Name.ToUpperInvariant() + " AND " + home.Name.ToUpperInvariant());
    }

    void DeescalateOneFlashpoint()
    {
        var pair = groups.Where(g => g.InConflict).Take(2).ToArray();
        foreach (var group in pair)
        {
            group.InConflict = false;
            foreach (var member in group.Members) member?.StandDown();
            SetGroupTask(group, "STEWARDS DE-ESCALATING", group.HomePocket, 5f);
        }
    }

    void ScanClashes()
    {
        if (!livingStarted) return;
        clashScan -= Time.deltaTime;
        if (clashScan > 0f) return;
        clashScan = 1.4f;
        foreach (var rival in rivalBodies)
        {
            if (!rival || !rival.IsAlive || rival.isHostile) continue;
            EnemyController nearest = null;
            float best = 8f;
            foreach (var fan in homeBodies)
            {
                if (!fan || !fan.IsAlive) continue;
                float distance = Vector3.Distance(rival.transform.position, fan.transform.position);
                if (distance < best) { best = distance; nearest = fan; }
            }
            if (!nearest) continue;
            rival.isHostile = true;
            nearest.isHostile = true;
            rival.AlertToTarget(nearest);
            nearest.AlertToTarget(rival);
            rival.GetComponent<MatchdayMarch>()?.SetConflictActive(true);
            nearest.GetComponent<MatchdayMarch>()?.SetConflictActive(true);
            CityGameplay.Instance?.PostEvent(rival.firmName + " IS CLASHING WITH HOME SUPPORT");
        }
    }

    static Vector3 Sample(Vector3 guess)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(guess, out var hit, 14f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;
        return guess;
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

/// <summary>Keeps a matchday group walking its own pocket until a fight starts.</summary>
public sealed class MatchdayMarch : MonoBehaviour
{
    public Vector3 Pocket;
    public float Radius = 5f;
    public string CurrentTask { get; private set; }
    UnityEngine.AI.NavMeshAgent nav;
    bool conflictActive;
    float nextDestinationAt;

    public void Bind(Vector3 pocket, float radius, string task = "MOVING AS GROUP")
    {
        Pocket = pocket;
        Radius = radius;
        CurrentTask = task;
        nextDestinationAt = 0f;
    }

    public void SetConflictActive(bool active)
    {
        conflictActive = active;
    }

    void Awake()
    {
        nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }

    void Update()
    {
        var body = GetComponent<EnemyController>();
        if (!body || !body.IsAlive || body.isHostile || conflictActive) return;
        if (!nav || !nav.enabled || !nav.isOnNavMesh) return;
        if (Time.time < nextDestinationAt) return;
        if (nav.pathPending || (nav.hasPath && nav.remainingDistance > 0.8f)) return;
        nextDestinationAt = Time.time + Random.Range(1.2f, 3.2f);
        Vector2 ring = Random.insideUnitCircle * Radius;
        Vector3 guess = Pocket + new Vector3(ring.x, 0f, ring.y);
        if (UnityEngine.AI.NavMesh.SamplePosition(guess, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
            nav.SetDestination(hit.position);
    }
}

/// <summary>World-space circle and live task title for one matchday group.</summary>
public sealed class MatchdayGroupMarker : MonoBehaviour
{
    public string GroupName { get; private set; }
    public Color GroupColor { get; private set; }
    public MatchdayUnitRole Role { get; private set; }
    public string CurrentTask { get; private set; }
    public float Radius { get; private set; }

    readonly List<EnemyController> members = new List<EnemyController>();
    TextMeshPro label;

    public void Setup(string groupName, Color color, MatchdayUnitRole role, float radius)
    {
        GroupName = string.IsNullOrWhiteSpace(groupName) ? "SUPPORTERS" : groupName;
        GroupColor = color;
        Role = role;
        Radius = radius;
        CurrentTask = role == MatchdayUnitRole.Police ? "FOOT PATROL" : "ARRIVING IN GROUPS";
        ZoneVolumeFactory.Create(transform, color, radius);
        label = ZoneLabelUtil.Create(transform, LabelText(), 3.7f, 6.2f);
        label.name = "MatchdayGroupTitle";
        label.color = Color.Lerp(color, Color.white, .28f);
        MiniMapIconFactory.Register(transform,
            role == MatchdayUnitRole.Police ? MiniMapIconFactory.Kind.Police : MiniMapIconFactory.Kind.Gang,
            GroupName);
    }

    public void Bind(IEnumerable<EnemyController> source)
    {
        members.Clear();
        if (source != null) members.AddRange(source.Where(x => x));
    }

    public void SetTask(string task)
    {
        CurrentTask = string.IsNullOrWhiteSpace(task) ? "MOVING" : task;
        if (label) label.text = LabelText();
    }

    string LabelText() => GroupName.ToUpperInvariant() + "\n<size=62%>" + CurrentTask + "</size>";

    void LateUpdate()
    {
        Vector3 center = Vector3.zero;
        int alive = 0;
        foreach (var member in members)
        {
            if (!member || !member.IsAlive) continue;
            center += member.transform.position;
            alive++;
        }
        if (alive > 0) transform.position = Vector3.Lerp(transform.position, center / alive, 1f - Mathf.Exp(-6f * Time.deltaTime));
        if (label) label.gameObject.SetActive(alive > 0);
    }
}
