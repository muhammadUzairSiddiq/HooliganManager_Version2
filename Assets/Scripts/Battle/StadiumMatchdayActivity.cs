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
    const float PhaseDuration=90f;
    float nextStreamTick;

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
    public int SimulatedGroupCount => groups.Count(g=>g.Slots.Any(s=>s.Hp>0));
    public int StreamedBodyCount => groups.Sum(g=>g.Members.Count(m=>m&&m.IsAlive&&m.gameObject.activeInHierarchy));

    sealed class MemberState
    {
        public float Hp;
        public Vector3 Position;
        public EnemyController Body;
        public bool WasSpawned;
    }

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
        public readonly List<MemberState> Slots=new List<MemberState>();
        public Vector3 Destination;
        public string Task="ARRIVING IN GROUPS";
        public MatchdayGroupState Opponent;
        public float ConflictUntil;
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
        CityActivityStreaming.Ensure();
        BuildAtmosphere();
        StartCoroutine(LivingMatchday());
        CityGameplay.Instance?.PostEvent($"MATCHDAY {matchday:00} · ARRIVAL WINDOW · FANS ARE MOVING TOWARD THE STADIUM");
        yield return new WaitForSeconds(Random.Range(10f, 20f));
        pendingEscalation = true;
        ShowEscalationChoice();
    }

    int matchday => GameManager.Data?.MatchDay ?? 1;

    bool pendingEscalation;

    void Update()
    {
        // Matchday decisions are available on HOME; never interrupt camera control
        // with an unsolicited modal while the user is managing another task.
        StreamGroups();
        UpdateGroupTasks();
        ScanClashes();
        if (pendingEscalation && !atmosphereEventResolved) ShowEscalationChoice();
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
                pendingEscalation=true;
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
        if(atmosphereEventResolved)return;
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
        var d=GameManager.Data;
        if(choice=="ESCORT"&&(d==null||d.Money<200))
        {
            CityGameplay.Instance?.PostEvent("ESCORT NEEDS £200 · CHOOSE STEWARDS OR SAVE MORE FUNDS");
            return;
        }
        atmosphereEventResolved=true;
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
        if(pendingEscalation&&!atmosphereEventResolved){ShowEscalationChoice();return;}
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
        // The stadium model and destination pin already identify this area.
        // Phase, decisions and event detail belong in the command desk.

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
        while (wait > 0f && (BattleManager.instance == null || FindObjectsByType<GangArea>(FindObjectsSortMode.None).Length==0))
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
                foreach (var bot in bots.Where(b=>b!=null))
                {
                    Color color;
                    if (!ColorUtility.TryParseHtmlString(bot.primaryColor, out color))
                        color = new Color(.85f, .18f, .16f);
                    CreateSupporterGroup(bm, bot.firmName, color, MatchdayUnitRole.RivalSupporter,
                        StadiumPocket(groups.Count, 58f), 3);
                }
            }
        }
        else
        {
            for (int i = 0; i < activeGangs.Count; i++)
                CreateSupporterGroup(bm, activeGangs[i].GangName, activeGangs[i].ZoneColor,
                    MatchdayUnitRole.RivalSupporter, StadiumPocket(i, 58f), 3);
        }

        // The player's firm has the strongest visible presence: three separate
        // supporter groups and more members than each individual rival group.
        var data = GameManager.Data;
        string homeName = !string.IsNullOrWhiteSpace(data?.FirmName) ? data.FirmName : "HOME SUPPORT";
        Color homeColor = new Color(.12f,.85f,.25f);
        Vector3[] homeSpots =
        {
            stadiumCenter + new Vector3(35f, 0f, -30f),
            stadiumCenter + new Vector3(-35f, 0f, -30f),
            stadiumCenter + new Vector3(0f, 0f, -65f)
        };
        foreach (Vector3 homeSpot in homeSpots)
            CreateSupporterGroup(bm, homeName, homeColor, MatchdayUnitRole.HomeSupporter, homeSpot, 4);

        // Four independent foot-patrol groups continuously circulate around the ground.
        Color policeColor = new Color(0.15f, 0.38f, 0.95f);
        for (int i = 0; i < 4; i++)
            CreateSupporterGroup(bm, "POLICE", policeColor, MatchdayUnitRole.Police,
                StadiumPocket(i, 82f, 45f), 1);

        AssignPhaseTasks("ARRIVING IN GROUPS");

        CityGameplay.Instance?.PostEvent("MATCHDAY · RIVAL GROUPS, HOME SUPPORT AND POLICE ARE MOVING AROUND THE STADIUM");
    }

    MatchdayGroupState CreateSupporterGroup(BattleManager bm, string groupName, Color color,
                                             MatchdayUnitRole role, Vector3 pocket, int count)
    {
        pocket = SeparatePocket(pocket);
        var state = new MatchdayGroupState
        {
            Name = groupName,
            Color = color,
            Role = role,
            HomePocket = pocket,
            Destination = pocket,
            NextTaskAt = Time.time + Random.Range(5f, 10f),
            TaskIndex = groups.Count
        };

        var markerObject = new GameObject("Matchday Group · " + groupName);
        markerObject.transform.SetParent(transform, true);
        markerObject.transform.position = pocket;
        state.Marker = markerObject.AddComponent<MatchdayGroupMarker>();
        state.Marker.Setup(groupName, color, role, 3.8f);

        for (int member = 0; member < count; member++)
        {
            float angle = member * Mathf.PI * 2f / Mathf.Max(1, count);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.45f;
            state.Slots.Add(new MemberState { Hp=role==MatchdayUnitRole.Police?85f:65f, Position=pocket+offset });
        }
        state.Marker.Bind(state.Members);
        groups.Add(state);
        return state;
    }

    Vector3 SeparatePocket(Vector3 guess)
    {
        for(int ring=0;ring<12;ring++)
        for(int i=0;i<16;i++)
        {
            float angle=i*Mathf.PI/8f;
            var p=guess+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*8f;
            if(!CityActivityStreaming.TryStreetPoint(p,out p,5f))continue;
            if(groups.Any(g=>(g.HomePocket-p).sqrMagnitude<22f*22f))continue;
            return p;
        }
        return Sample(guess);
    }

    void StreamGroups()
    {
        if(Time.time<nextStreamTick||!BattleManager.instance)return;
        nextStreamTick=Time.time+.15f;
        foreach(var group in groups)
        {
            if(group.InConflict&&Time.time>=group.ConflictUntil)EndConflict(group);
            bool near=CityActivityStreaming.Interested(group.Destination,group.Members.Count>0);
            foreach(var slot in group.Slots)
            {
                if(slot.Body)
                {
                    slot.Hp=slot.Body.CurrentHp;slot.Position=slot.Body.transform.position;
                    if(!slot.Body.IsAlive||!slot.Body.gameObject.activeInHierarchy){slot.Hp=0;slot.Body=null;}
                    else if(!near&&!CityActivityStreaming.Interested(slot.Position,true))
                    {
                        BattleManager.instance.DespawnMatchdayFighter(slot.Body);slot.Body=null;
                    }
                }
                else if(slot.WasSpawned&&slot.Hp>0)
                {
                    // An explicitly streamed-out body keeps its HP; deaths are
                    // sampled before the combat pool reuses an instance below.
                }
                if(!near||slot.Body||slot.Hp<=0)continue;
                var spawn=Sample(group.Destination+(slot.Position-group.HomePocket).normalized*1.5f);
                var body=BattleManager.instance.SpawnMatchdayFighter(spawn,group.Name,group.Color,
                    group.Role==MatchdayUnitRole.Police?85f:65f,group.Role==MatchdayUnitRole.Police?8f:7f,group.Role,group.Name);
                if(!body)continue;
                slot.Body=body;slot.WasSpawned=true;body.RestoreStreamedHealth(slot.Hp);
                body.GetComponent<MatchdayMarch>()?.Bind(group.Destination,2.2f,group.Task);
            }
            group.Members.Clear();
            foreach(var slot in group.Slots)if(slot.Body&&slot.Body.IsAlive)group.Members.Add(slot.Body);
            group.Marker.Bind(group.Members);
            if(group.Members.Count==0)group.Marker.transform.position=group.Destination;
        }
        homeBodies.Clear();rivalBodies.Clear();
        foreach(var g in groups)
        {
            if(g.Role==MatchdayUnitRole.HomeSupporter)homeBodies.AddRange(g.Members);
            if(g.Role==MatchdayUnitRole.RivalSupporter)rivalBodies.AddRange(g.Members);
        }
    }

    void EndConflict(MatchdayGroupState group)
    {
        var other=group.Opponent;
        foreach(var g in new[]{group,other})
        {
            if(g==null)continue;
            g.InConflict=false;g.Opponent=null;
            foreach(var m in g.Members)if(m){m.StandDown();m.GetComponent<MatchdayMarch>()?.SetConflictActive(false);}
            SetGroupTask(g,"REGROUPING",g.HomePocket,2.2f);
            g.NextTaskAt=Time.time+18f;
        }
        CityGameplay.Instance?.PostEvent("POLICE SEPARATED A FLASHPOINT · GROUPS ARE REGROUPING");
    }

    public void NoteBodyDown(EnemyController body)
    {
        foreach(var group in groups)foreach(var slot in group.Slots)
            if(slot.Body==body){slot.Hp=0;slot.Body=null;}
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
            group.NextTaskAt = Time.time + Random.Range(18f, 28f);
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
            float angle=group.TaskIndex*1.7f;
            Vector3 destination = group.HomePocket+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(group.Role==MatchdayUnitRole.Police?9f:5f);
            SetGroupTask(group, tasks[group.TaskIndex % tasks.Length], destination, 2.2f);
        }
    }

    void SetGroupTask(MatchdayGroupState group, string task, Vector3 destination, float radius)
    {
        if (group == null) return;
        destination = Sample(destination);
        group.Destination=destination;group.Task=task;
        group.Marker?.SetTask(task);
        for(int i=0;i<group.Members.Count;i++)
        {
            var member=group.Members[i];
            if (!member || !member.IsAlive || member.isHostile) continue;
            float a=i*Mathf.PI*2f/Mathf.Max(1,group.Members.Count);
            member.GetComponent<MatchdayMarch>()?.Bind(destination+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*1.6f,.65f,task);
        }
    }

    void AssignPhaseTasks(string task)
    {
        foreach (var group in groups)
        {
            if (group == null || group.InConflict) continue;
            Vector3 destination = phase == MatchdayPhase.Aftermath
                ? group.HomePocket + (group.HomePocket - stadiumCenter).normalized * 16f
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
            Vector3 flashpoint = homes[i].HomePocket;
            QueueFlashpoint(rivals[i], homes[i], flashpoint);
        }
    }

    void QueueFlashpoint(MatchdayGroupState rival, MatchdayGroupState home, Vector3 location)
    {
        rival.InConflict = home.InConflict = true;
        rival.Opponent=home;home.Opponent=rival;
        rival.ConflictUntil=home.ConflictUntil=Time.time+55f;
        SetGroupTask(rival, "MOVING TO FLASHPOINT", location + Vector3.right * 1.6f, 2.2f);
        SetGroupTask(home, "HOLDING HOME ROUTE", location - Vector3.right * 1.6f, 2.2f);
        conflictsStarted++;
        CityGameplay.Instance?.PostEvent("LIVE FLASHPOINT · " + rival.Name.ToUpperInvariant() + " AND " + home.Name.ToUpperInvariant());
    }

    void DeescalateOneFlashpoint()
    {
        var group=groups.FirstOrDefault(g=>g.InConflict);
        if(group!=null)EndConflict(group);
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
            var group=groups.FirstOrDefault(g=>g.InConflict&&g.Members.Contains(rival));
            if(group?.Opponent==null)continue;
            EnemyController nearest = null;
            float best = 8f;
            foreach (var fan in group.Opponent.Members)
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
        // Named groups already represent every rival; no anonymous duplicate crowd.
        rivalFansSpawned=true;
    }

    void LegacySpawnRivalFans()
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
        // Patrol groups are data-driven; never spawn empty rings as police stand-ins.
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
        conflictActive=false;
        nextDestinationAt = 0f;
        if(nav&&nav.enabled&&nav.isOnNavMesh)
        {
            nav.ResetPath();
            if(UnityEngine.AI.NavMesh.SamplePosition(pocket,out var hit,4f,UnityEngine.AI.NavMesh.AllAreas))nav.SetDestination(hit.position);
        }
    }

    public void SetConflictActive(bool active)
    {
        conflictActive = active;
    }

    void Awake()
    {
        nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if(!GetComponent<SupporterGestureAnimation>())gameObject.AddComponent<SupporterGestureAnimation>();
    }

    void Update()
    {
        var body = GetComponent<EnemyController>();
        if (!body || !body.IsAmbientMatchdayUnit || !body.IsAlive || body.isHostile || conflictActive) return;
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
    LineRenderer ring;

    public void Setup(string groupName, Color color, MatchdayUnitRole role, float radius)
    {
        GroupName = string.IsNullOrWhiteSpace(groupName) ? "SUPPORTERS" : groupName;
        GroupColor = color;
        Role = role;
        Radius = radius;
        CurrentTask = role == MatchdayUnitRole.Police ? "FOOT PATROL" : "ARRIVING IN GROUPS";
        ring=ZoneVolumeFactory.BuildFlatCircle(transform,"GroupFootprint",radius,.10f,color,true,36);
        label = ZoneLabelUtil.Create(transform, LabelText(), 3.4f, 4f);
        label.name = "MatchdayGroupTitle";
        label.color = Color.Lerp(color, Color.white, .28f);
        label.GetComponent<ZoneLabelBillboard>().ForceWhite=false;
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

    string LabelText() => GroupName.ToUpperInvariant();

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
        if (ring) ring.enabled=alive>0;
    }
}
