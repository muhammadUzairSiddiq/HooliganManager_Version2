using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class MatchdayStreetState
{
    public int day, arrivalWaves, homeBooked, rivalBooked, crewBooked, gateChoice;
    public bool routeReopened, pubStocked, pubTrading, gatesEnabled, gateAdmitted;
    public float tradeSeconds;
    [System.Runtime.Serialization.OptionalField] public bool automatic, guardResolved, guarded;
    [System.Runtime.Serialization.OptionalField] public float guardSeconds;
    public List<MatchdayDetention> detentions = new List<MatchdayDetention>();
}

[Serializable]
public sealed class MatchdayDetention
{
    public string memberId;
    public float releaseAt;
}

/// <summary>Runs the active street objectives and nonlethal police response for new Matchdays.</summary>
public sealed class MatchdayStreetOperations : MonoBehaviour
{
    static readonly string[] Districts = { "Rail district", "Pub district", "Stadium district" };
    const int RequiredObjectives = 2;
    const int BailCost = 100;
    const float GuardOpensAt = 45f;
    const float GuardClosesAt = 220f;
    const float GuardHoldSeconds = 12f;
    readonly Dictionary<EnemyController, Pursuit> pursuits = new Dictionary<EnemyController, Pursuit>();
    readonly HashSet<AgentController> heldCrew = new HashSet<AgentController>();
    readonly Dictionary<AgentController, TMPro.TextMeshPro> custodyLabels = new Dictionary<AgentController, TMPro.TextMeshPro>();
    readonly Dictionary<EnemyController, float> heldRivals = new Dictionary<EnemyController, float>();
    readonly Transform[] cards = new Transform[5];
    StadiumMatchdayActivity owner;
    float nextTick, nextRoadAttempt;
    int nextRoadWave;
    TMPro.TextMeshPro guardMarker;

    sealed class Pursuit
    {
        public EnemyController supporter;
        public AgentController member;
        public float contact;
        public int supporterLife;
        public Vector3 Point => member ? member.transform.position : supporter.transform.position;
        public bool Valid => member ? member.IsAlive && !member.IsActivityLocked :
            supporter && supporter.LifeRevision==supporterLife && supporter.IsAlive && supporter.isHostile && supporter.gameObject.activeInHierarchy;
    }

    MatchdayStreetState State => GameManager.Data?.MatchdayStreet;
    public bool IsCurrent => State != null && State.day == GameManager.Data.MatchDay;
    public bool Automatic => IsCurrent && State.automatic;
    public bool GuardAvailable => Automatic && owner.HasStarted && !owner.IsAftermath && !State.guardResolved &&
        owner.ElapsedSeconds >= GuardOpensAt && owner.ElapsedSeconds < GuardClosesAt;
    public Vector3 GuardPoint => Gates && Gates.Ready ? Gates.QueuePoint : owner.StadiumApproach;
    public string LiveHeadline => owner.IsAftermath ? "FULL TIME · FANS HEADING HOME" :
        GuardAvailable ? (State.guardSeconds>0 ? "GUARDING HOME FANS" : "GUARD THE STADIUM APPROACH") :
        State.guarded ? "HOME FANS PROTECTED" : State.guardResolved ? "RIVALS REINFORCED" :
        owner.ElapsedSeconds<GuardOpensAt ? "FANS ARRIVING" : "MATCH IN PROGRESS";
    public static bool Active => StadiumMatchdayActivity.IsMatchdayActive &&
        StadiumMatchdayActivity.Instance.StreetOperations && StadiumMatchdayActivity.Instance.StreetOperations.IsCurrent;
    public static bool IsDetained(AgentController actor) =>
        Active && actor && StadiumMatchdayActivity.Instance.StreetOperations.heldCrew.Contains(actor);
    public static bool IsDetained(EnemyController actor) =>
        Active && actor && StadiumMatchdayActivity.Instance.StreetOperations.heldRivals.ContainsKey(actor);
    public bool IsPursuing(EnemyController officer) => pursuits.ContainsKey(officer);
    public MatchdayGates Gates { get; private set; }
    public bool HasGateObjective => IsCurrent && State.gatesEnabled;
    public int ObjectivesCompleted => !IsCurrent ? 0 :
        (MatchdayTactics.Ensure(GameManager.Data).escortComplete ? 1 : 0) +
        (State.routeReopened ? 1 : 0) + (State.pubTrading ? 1 : 0) + (State.gateAdmitted ? 1 : 0);
    int ObjectiveCount => HasGateObjective ? 4 : 3;
    public static int PubStockCost(PlayerData data) => HomeTerritorySystem.Built(data,1) ? 1 : 2;

    public string Summary
    {
        get
        {
            if (!IsCurrent) return "MATCHDAY";
            int roads = CityRoadIncidentSystem.Instance
                ? CityRoadIncidentSystem.Instance.ActiveIncidents.Count(road => road.IsMatchdayCordon) : 0;
            if (owner.IsAftermath)
                return $"AFTERMATH | {ObjectivesCompleted}/{ObjectiveCount} OBJECTIVES | BOOKINGS: CREW {State.crewBooked}, HOME {State.homeBooked}, AWAY {State.rivalBooked}";
            return $"{ObjectivesCompleted}/{ObjectiveCount} OBJECTIVES (2 NEEDED) | {roads} CLOSURES | {State.crewBooked} CREW BOOKED";
        }
    }

    public void Initialize(StadiumMatchdayActivity activity)
    {
        owner = activity;
        if (HasGateObjective) CreateGates();
    }

    void CreateGates()
    {
        Gates = gameObject.AddComponent<MatchdayGates>();
        Gates.Initialize(owner);
    }

    public void AdoptAutomaticDay()
    {
        if(!IsCurrent)GameManager.Data.MatchdayStreet=new MatchdayStreetState { day=GameManager.Data.MatchDay };
        State.automatic=true;
    }

    public void StartDay(bool automatic)
    {
        GameManager.Data.MatchdayStreet = new MatchdayStreetState { day = GameManager.Data.MatchDay, automatic=automatic };
        CreateGates();
        State.gatesEnabled = Gates.Ready;
        nextTick = 0;
        nextRoadWave = 0;
    }

    public void ResetForNextDay()
    {
        ReleaseAll();
        if(Gates){Gates.RemoveWorldGates();Destroy(Gates);Gates=null;}
        if(guardMarker)Destroy(guardMarker.transform.parent.gameObject);
        foreach(var card in cards)if(card)Destroy(card.gameObject);
        nextRoadAttempt=0;
    }

    public void ResetRoadSchedule()
    {
        nextRoadWave = 0;
        nextTick = 0;
    }

    Vector3 DistrictPoint(int index)
    {
        var city = CityGameplay.Instance;
        if (index == 0) return city.Locations[7];
        if (index == 1) return city.Locations[1];
        return owner.StadiumApproach;
    }

    void Update()
    {
        if (!owner || !owner.HasStarted || !IsCurrent || GuidedCityTutorial.CombatSuppressed || Time.deltaTime <= 0)
            return;
        if (owner.IsAftermath)
        {
            ReleaseAll();
            return;
        }
        if (Time.time < nextTick) return;
        nextTick = Time.time + .65f;
        if(State.arrivalWaves<3&&owner.ElapsedSeconds>=State.arrivalWaves*42&&owner.DispatchArrivalWave(State.arrivalWaves))
        {
            State.arrivalWaves++;
            GameManager.Save();
        }
        TickRoads();
        if (HasGateObjective && Gates && owner.ElapsedSeconds < (Automatic?220:360))
        {
            if(Automatic&&State.gateChoice==0&&owner.ElapsedSeconds>=100)State.gateChoice=1;
            bool admitted = State.gateAdmitted;
            Gates.Tick(State);
            if (!Automatic && !admitted && State.gateAdmitted)
                Reward(130, "GATE OPEN · the waiting supporters made it through.", State.gateChoice == 1 ? Gates.MainGate : Gates.SideGate);
        }
        if(Automatic)TickAutomaticMatch();
        else TickObjectives();
        TickDetentions();
        TickArrests();
    }

    void TickRoads()
    {
        var roads = CityRoadIncidentSystem.Instance;
        if (!roads || !roads.AutomaticIncidentsEnabled || owner.ElapsedSeconds >= 360 || nextRoadWave > 2) return;
        if(Automatic)
        {
            if(owner.ElapsedSeconds<nextRoadWave*30f||Time.time<nextRoadAttempt)return;
            nextRoadAttempt=Time.time+4;
            if(roads.TryCreateMatchdayCordon(Districts[nextRoadWave],0,DistrictPoint(nextRoadWave),owner.ElapsedSeconds+48))
                nextRoadWave++;
            return;
        }
        if (owner.ElapsedSeconds < nextRoadWave * 70f || Time.time < nextRoadAttempt) return;
        nextRoadAttempt=Time.time+4;
        int created = 0;
        for (int district = 0; district < Districts.Length; district++)
            if (roads.TryCreateMatchdayCordon(Districts[district], nextRoadWave, DistrictPoint(district)))
                created++;
        // Retry unavailable sites once the current batch has space again.
        if (created == Districts.Length) nextRoadWave++;
    }

    void TickAutomaticMatch()
    {
        if(!State.guardResolved&&owner.ElapsedSeconds>=GuardClosesAt)ResolveGuard(false);
        if(GuardAvailable)
        {
            if(!guardMarker)
            {
                var host=new GameObject("Matchday guard post");
                host.transform.SetParent(transform);
                host.transform.position=GuardPoint;
                guardMarker=ZoneLabelUtil.Create(host.transform,"GUARD HOME FANS",4,5);
                guardMarker.GetComponent<ZoneLabelBillboard>().CommandPriority=true;
            }
            bool guarding=BattleManager.instance.PlayerAgents.Any(crew=>crew&&crew.IsAlive&&!crew.IsActivityLocked&&
                crew.CurrentOrder==AgentController.StandingOrder.Guard&&
                CityBusinessSystem.HorizontalDistance(crew.transform.position,GuardPoint)<12);
            State.guardSeconds=guarding?State.guardSeconds+.65f:0;
            if(State.guardSeconds>=GuardHoldSeconds)ResolveGuard(true);
        }
        if(!State.pubTrading&&HomeTerritorySystem.Built(GameManager.Data,1)&&owner.ElapsedSeconds>=110&&
            !CityBusinessSystem.HostilesNear(DistrictPoint(1),14))
        {
            State.pubTrading=true;
            CityGameplay.Instance?.PostEvent("MATCHDAY · Home stock kept the pub serving fans.");
            GameManager.Save();
        }
    }

    void ResolveGuard(bool guarded)
    {
        if(State.guardResolved)return;
        State.guardResolved=true;
        State.guarded=guarded;
        if(guardMarker)Destroy(guardMarker.transform.parent.gameObject);
        if(guarded)
        {
            GameManager.Data.FanMorale=Mathf.Min(100,GameManager.Data.FanMorale+3);
            GameFeedback.Emit(GameFeedbackEvent.TaskComplete,GuardPoint);
            CityGameplay.Instance?.PostEvent("HOME FANS SAFE · your guard held the stadium approach.");
        }
        else
        {
            var firm=RivalGrowthSystem.ReinforceFromMatchday(GameManager.Data);
            if(!string.IsNullOrEmpty(firm))
            {
                owner.SendRivalReinforcements(firm);
                BattleManager.instance?.ReinforceLivingRivals();
                CityGameplay.Instance?.PostEvent($"{firm.ToUpperInvariant()} · reinforcements are marching to the stadium.");
            }
        }
        GameManager.Save();
    }

    public void ViewLiveMatch()
    {
        if(!GuardAvailable)
        {
            if(owner.ElapsedSeconds<GuardOpensAt)
                CameraPanTouchOnly.Instance?.TutorialFrame(new Bounds(DistrictPoint(0),new Vector3(65,12,65)));
            else StadiumMatchdayControl.Instance?.Focus();
            return;
        }
        CameraPanTouchOnly.Instance?.TutorialFrame(new Bounds(GuardPoint,new Vector3(52,10,52)));
        ShowAt(0,GuardPoint,"GUARD HOME FANS",
            ("GUARD HERE",LandscapeUI.Green,OrderGuard));
    }

    void OrderGuard()
    {
        if(!GuardAvailable||!AgentSelectionManager.instance)return;
        var region=new Bounds(GuardPoint,new Vector3(18,5,18));
        var selection=AgentSelectionManager.instance.SelectedAgents
            .Where(crew=>crew&&crew.IsAlive&&!crew.IsActivityLocked).ToArray();
        int accepted=0;
        for(int index=0;index<selection.Length;index++)
        {
            var post=GuardPoint+Vector3.right*((index%5-2)*1.3f);
            if(selection[index].CommandGuard(region,post))accepted++;
        }
        if(accepted>0)
        {
            CityGameplay.Instance?.PostEvent("GUARD ORDER · selected crew are moving to the stadium approach.");
            return;
        }
        BattleUIController.instance?.ShowAlert(selection.Length==0?"Select crew, then tap GUARD HERE.":"Selected crew have no walking route to this post.",3);
    }

    void TickObjectives()
    {
        var requested = GameManager.Data.CityRoadIncidents?.FirstOrDefault(road=>road!=null&&
            road.day==State.day&&road.matchdayCordon&&road.clearanceRequested&&road.cleared);
        if (!State.routeReopened && requested != null)
        {
            State.routeReopened = true;
            Reward(100, "ROUTE REOPENED · police cleared the barriers and traffic can move.", requested.location);
        }
        if (!State.pubStocked || State.pubTrading) return;
        var pub = DistrictPoint(1);
        if (CityBusinessSystem.HostilesNear(pub, 14)) return;
        State.tradeSeconds += .65f;
        if (State.tradeSeconds < 22) return;
        State.pubTrading = true;
        Reward(MatchdayTactics.Ensure(GameManager.Data).plan==2?260:200, "PUB TRADING · the pub served its Matchday crowd.", pub);
    }

    public void FinishDay()
    {
        ReleaseAll();
        var data = GameManager.Data;
        if (!IsCurrent || data.MatchdayIncidentResolved) return;
        if(Automatic)
        {
            data.MatchdayIncidentResolved=true;
            int credit=100+(State.guarded?150:0)+(State.pubTrading?50:0);
            data.MatchdayTravelCredit=Mathf.Max(data.MatchdayTravelCredit,credit);
            CampaignMissions.RecordAction(data,"matchday","day-"+data.MatchDay);
            CityGameplay.Instance?.PostEvent($"FULL TIME · £{credit} Away fare credit from Matchday.");
            GameManager.Save();
            CityGameplay.Instance?.CheckCampaignCompletion(false);
            return;
        }
        if (ObjectivesCompleted < RequiredObjectives)
        {
            Report($"MATCHDAY MISSED · completed {ObjectivesCompleted}/{ObjectiveCount} objectives. Retry from the stadium.");
            return;
        }
        data.MatchdayIncidentResolved = true;
        CampaignMissions.ApplyMatchdayResult(data,State);
        data.PoliceHeat = Mathf.Max(0, data.PoliceHeat - 1);
        CampaignMissions.RecordAction(data, "matchday", "day-" + data.MatchDay);
        Reward(150, $"MATCHDAY COMPLETE · £{data.MatchdayTravelCredit} credit for your next Away fare.", owner.StadiumApproach);
        CityGameplay.Instance?.CheckCampaignCompletion();
    }

    public void FocusEvent(int index)
    {
        if (!IsCurrent || !CityGameplay.Instance) return;
        var strategy = MatchdayStrategySystem.Instance;
        if (index == 0 && strategy)
        {
            var point = strategy.EscortPickup;
            ShowAt(0, point, "FANS WAITING · select a nearby guide and rally the group",
                ("RALLY FANS", LandscapeUI.Green, () => strategy.RallyEscort()),
                ("CHECKPOINT", LandscapeUI.PanelColor, () => CameraPanTouchOnly.Instance?.TutorialFocus(strategy.ResponsePoints[0])));
        }
        else if (index == 1)
        {
            var roads = CityRoadIncidentSystem.Instance;
            var active = roads ? roads.ActiveIncidents.Where(road => road.IsMatchdayCordon).ToArray() : Array.Empty<CityRoadIncidentSystem.RoadIncident>();
            if (active.Length == 0) { Report("No active Matchday road closure."); return; }
            var choices = active.GroupBy(road=>road.District).Select(group=>group.First()).Select(road =>
                (road.District.ToUpperInvariant(), LandscapeUI.PanelColor, (Action)(() => OpenRoad(road)))).ToArray();
            ShowAt(1, StadiumMatchdayControl.Instance.TapPoint, "ROAD CLOSURES · choose a district", choices);
        }
        else if (index == 2)
        {
            ShowAt(2, DistrictPoint(1), "PUB TRADE · restock the bar and keep rivals clear",
                ($"USE {PubStockCost(GameManager.Data)} SUPPLY", LandscapeUI.Green, () => RestockPub(true)),
                ("PAY £120", LandscapeUI.PanelColor, () => RestockPub(false)));
        }
        else if (index == 3)
        {
            var member = heldCrew.FirstOrDefault(actor => actor && actor.IsAlive);
            if (!member) { Report("No crew in custody."); return; }
            ShowAt(3, member.transform.position, $"{member.Data.AgentName} · short detention or pay £{BailCost} for immediate release",
                ("PAY BAIL", LandscapeUI.Green, () => Bail(member)));
        }
        else if (index == 4 && HasGateObjective && Gates && Gates.Ready)
        {
            if (State.gateChoice > 0 || owner.ElapsedSeconds >= 360)
            {
                bool side = State.gateChoice == 2;
                string title = State.gateAdmitted ? "SUPPORTERS THROUGH" :
                    State.gateChoice > 0
                        ? $"{owner.GateQueueAt(side ? Gates.SideGate : Gates.MainGate)}/{MatchdayGates.AdmissionTarget} WALKED THROUGH"
                        : "GATE QUEUE HAS DISPERSED";
                ShowAt(4, side ? Gates.SideGate : Gates.MainGate, title,
                    ("CUSTODY", LandscapeUI.PanelColor, () => FocusEvent(3)));
                return;
            }
            ShowAt(4, Gates.QueuePoint, "STADIUM QUEUE · staff the main gate or pay for the side gate",
                ("MAIN · 2 CREW", LandscapeUI.Green, () => OpenGate(1)),
                ("SIDE · £80", LandscapeUI.Gold, () => OpenGate(2)),
                ("CUSTODY", LandscapeUI.PanelColor, () => FocusEvent(3)));
        }
    }

    void OpenGate(int choice)
    {
        if (State.gateChoice != 0 || owner.ElapsedSeconds >= 360) return;
        if (choice == 1 && SelectedCrewAt(Gates.QueuePoint) < 2)
        {
            Report("Select two available crew beside the waiting queue.");
            return;
        }
        if (choice == 2)
        {
            if (!CrewAt(Gates.QueuePoint)) { Report("Select crew beside the waiting queue."); return; }
            if (GameManager.Data.Money < MatchdayGates.SideGateCost) { Report("The side gate needs £80."); return; }
            GameManager.Data.Money -= MatchdayGates.SideGateCost;
        }
        State.gateChoice = choice;
        CameraPanTouchOnly.Instance?.TutorialFocus(choice == 1 ? Gates.MainGate : Gates.SideGate);
        Report("GATE RELEASED · watch the waiting group file through.");
        GameManager.Save();
    }

    void OpenRoad(CityRoadIncidentSystem.RoadIncident road)
    {
        ShowAt(1, road.OfficerWorkPoint, $"{road.District} · crew within 15m can ask police to reopen",
            ("OPEN ROAD", LandscapeUI.Green, () =>
            {
                if (!CrewAt(road.OfficerWorkPoint)) { Report("Select crew beside this road closure first."); return; }
                if (!CityRoadIncidentSystem.Instance.RequestMatchdayClearance(road)) return;
                Report("Police are clearing the cordon. The road opens when the crew and equipment arrive.");
                GameManager.Save();
            }));
    }

    void ShowAt(int index, Vector3 point, string title, params (string, Color, Action)[] choices)
    {
        if (!cards[index])
        {
            cards[index] = new GameObject("Matchday interaction").transform;
            cards[index].SetParent(transform);
        }
        cards[index].position = point;
        CameraPanTouchOnly.Instance?.TutorialFocus(point);
        WorldChoiceBar.Present(cards[index], title, choices);
    }

    static bool CrewAt(Vector3 point) => SelectedCrewAt(point) > 0;
    static int SelectedCrewAt(Vector3 point) => AgentSelectionManager.instance
        ? AgentSelectionManager.instance.SelectedAgents.Count(actor => actor && actor.IsAlive &&
            !actor.IsActivityLocked && CityBusinessSystem.HorizontalDistance(actor.transform.position, point) < 15) : 0;

    void RestockPub(bool useSupplies)
    {
        if (State.pubStocked) { Report("The pub is already stocked."); return; }
        if (!CrewAt(DistrictPoint(1))) { Report("Select crew beside the pub first."); return; }
        var data = GameManager.Data;
        if (useSupplies)
        {
            int required=PubStockCost(data);
            if (data.CitySupplies < required) { Report($"The pub needs {required} supply."); return; }
            data.CitySupplies -= required;
        }
        else
        {
            if (data.Money < 120) { Report("The pub needs £120."); return; }
            data.Money -= 120;
        }
        State.pubStocked = true;
        GameManager.Save();
        Report("Pub stocked. Keep rival fights away while the crowd trades.");
    }

    void TickArrests()
    {
        var battle = BattleManager.instance;
        if (!battle) return;
        foreach (var pair in pursuits.ToArray())
        {
            var officer = pair.Key;
            var pursuit = pair.Value;
            if (!officer || !officer.IsAlive || !officer.gameObject.activeInHierarchy || !pursuit.Valid ||
                CityBusinessSystem.HorizontalDistance(officer.transform.position, pursuit.Point) > 34)
            {
                EndPursuit(officer);
                continue;
            }
            if (Vector3.Distance(officer.transform.position, pursuit.Point) <= 3.2f)
            {
                pursuit.contact += .65f;
                if (pursuit.contact < 1.9f) continue;
                if (pursuit.member) DetainCrew(pursuit.member, officer.transform.position);
                else if(!owner.BookSupporter(pursuit.supporter, State))
                {
                    var rival=pursuit.supporter;
                    rival.HoldFire(true);
                    rival.SetCinematicIdle(true);
                    heldRivals.Add(rival,owner.ElapsedSeconds+35);
                    State.rivalBooked++;
                    Report($"POLICE BOOKED A {rival.firmName.ToUpperInvariant()} GANG MEMBER");
                }
                EndPursuit(officer);
                GameManager.Save();
            }
            else
            {
                pursuit.contact = 0;
                if (!officer.TryBeginEscort(pursuit.Point, true)) EndPursuit(officer);
            }
        }
        var suspects = battle.EnemyAgents.Where(actor=>actor&&actor.IsAlive&&actor.isHostile&&
            actor.gameObject.activeInHierarchy&&actor.firmName!="POLICE"&&!heldRivals.ContainsKey(actor)).ToArray();
        var responses = CityRoadIncidentSystem.Instance?.ActiveIncidents
            .Select(road => road.PoliceResponse).Where(response => response).ToArray() ?? Array.Empty<MatchdayPoliceResponse>();
        foreach (var officer in responses.SelectMany(response => response.Officers).Concat(owner.StreetPatrols()))
        {
            if (pursuits.Count >= 6) break;
            if (pursuits.ContainsKey(officer) || officer.IsEscorting) continue;
            var origin = officer.transform.position;
            var member = battle.PlayerAgents.Where(actor => actor && actor.IsAlive && !actor.IsActivityLocked &&
                actor.CurrentState == AgentController.State.AutoAttacking && !pursuits.Values.Any(p => p.member == actor))
                .OrderBy(actor => (actor.transform.position - origin).sqrMagnitude).FirstOrDefault();
            var supporter = suspects.Where(actor => !pursuits.Values.Any(p => p.supporter == actor))
                .OrderBy(actor => (actor.transform.position - origin).sqrMagnitude).FirstOrDefault();
            if (member && (member.transform.position - origin).sqrMagnitude <= 24 * 24 &&
                officer.TryBeginEscort(member.transform.position, true))
                pursuits.Add(officer, new Pursuit { member = member });
            else if (supporter && (supporter.transform.position - origin).sqrMagnitude <= 24 * 24 &&
                officer.TryBeginEscort(supporter.transform.position, true))
                pursuits.Add(officer, new Pursuit { supporter = supporter, supporterLife = supporter.LifeRevision });
            if(pursuits.ContainsKey(officer))
                foreach(var response in responses)
                    if(response.Officers.Contains(officer))response.SignalResponse();
        }
    }

    public void ReleaseCordonOfficer(EnemyController officer)
    {
        if (pursuits.ContainsKey(officer)) EndPursuit(officer);
    }

    void EndPursuit(EnemyController officer)
    {
        pursuits.Remove(officer);
        if (!officer) return;
        officer.EndEscort();
        officer.StandDown();
        officer.HoldFire(true);
        var nav=officer.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if(nav&&nav.enabled&&nav.isOnNavMesh)nav.isStopped=false;
    }

    void DetainCrew(AgentController actor, Vector3 face)
    {
        if (heldCrew.Contains(actor)) return;
        State.crewBooked++;
        State.detentions.Add(new MatchdayDetention { memberId = actor.Data.AgentId, releaseAt = owner.ElapsedSeconds + 40 });
        HoldCrew(actor, face);
        Report($"POLICE BOOKED {actor.Data.AgentName.ToUpperInvariant()} · 40s custody");
    }

    void HoldCrew(AgentController actor, Vector3 face)
    {
        if (!heldCrew.Add(actor)) return;
        AgentSelectionManager.instance?.Deselect(actor);
        actor.SetActivityLocked(true, face);
        custodyLabels.Add(actor, ZoneLabelUtil.Create(actor.transform, "POLICE CUSTODY", 3, 4));
    }

    void TickDetentions()
    {
        foreach(var pair in heldRivals.ToArray())
            if(!pair.Key||!pair.Key.IsAlive||pair.Value<=owner.ElapsedSeconds)ReleaseRival(pair.Key);
        foreach (var record in State.detentions.ToArray())
        {
            var actor = BattleManager.instance.PlayerAgents.FirstOrDefault(member => member && member.Data.AgentId == record.memberId);
            if (!actor) continue;
            if (!actor.IsAlive || record.releaseAt <= owner.ElapsedSeconds)
            {
                Release(actor);
                GameManager.Save();
            }
            else HoldCrew(actor, actor.transform.position + actor.transform.forward);
        }
    }

    void Bail(AgentController actor)
    {
        if (!heldCrew.Contains(actor)) return;
        if (GameManager.Data.Money < BailCost) { Report("Bail needs £100."); return; }
        GameManager.Data.Money -= BailCost;
        Release(actor);
        GameManager.Save();
    }

    void Release(AgentController actor)
    {
        if (heldCrew.Remove(actor) && actor && actor.IsAlive) actor.SetActivityLocked(false, actor.transform.position);
        if (custodyLabels.TryGetValue(actor, out var label) && label) Destroy(label.gameObject);
        custodyLabels.Remove(actor);
        if (actor && IsCurrent) State.detentions.RemoveAll(record => record.memberId == actor.Data.AgentId);
    }

    void ReleaseAll()
    {
        foreach (var actor in heldCrew.ToArray()) Release(actor);
        if(IsCurrent)State.detentions.Clear();
        foreach(var rival in heldRivals.Keys.ToArray())ReleaseRival(rival);
        foreach (var officer in pursuits.Keys.ToArray()) EndPursuit(officer);
    }

    void ReleaseRival(EnemyController rival)
    {
        heldRivals.Remove(rival);
        if(!rival||!rival.IsAlive)return;
        rival.SetCinematicIdle(false);
        rival.HoldFire(false);
        rival.StandDown();
    }

    void OnDestroy()
    {
        // Scene unload must leave saved detention receipts intact.
        foreach(var actor in heldCrew)
            if(actor&&actor.IsAlive)actor.SetActivityLocked(false,actor.transform.position);
        foreach(var label in custodyLabels.Values)
            if(label)Destroy(label.gameObject);
    }

    static void Report(string message)
    {
        CityGameplay.Instance?.PostEvent(message);
        BattleUIController.instance?.ShowAlert(message, 3f);
    }

    static void Reward(int cash, string message, Vector3 location)
    {
        var data = GameManager.Data;
        data.Money += cash;
        data.FanMorale = Mathf.Min(100, data.FanMorale + 2);
        GameFeedback.Emit(GameFeedbackEvent.TaskComplete, location);
        Report(message);
        GameManager.Save();
    }
}
