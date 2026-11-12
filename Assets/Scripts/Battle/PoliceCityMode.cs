using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Playable police career: patrol, respond, detain, escort, book, complete shift.</summary>
public sealed class PoliceCityMode : MonoBehaviour
{
    public static bool Active {get;private set;}
    public static PoliceCityMode Instance {get;private set;}
    public static PoliceCampaignData Career {get;private set;}
    static PlayerData firm;
    static bool priorHome;
    public bool Ready {get;private set;}
    public bool IsDispatchOpen=>sheet&&sheet.activeInHierarchy;
    public Vector3 Station {get;private set;}
    public IReadOnlyList<Vector3> PatrolPoints=>patrolPoints;
    public IReadOnlyList<PoliceCallData> Calls=>Career?.calls;
    public string CurrentObjective=>Goal();
    public int SelectedOfficerCount=>AgentSelectionManager.instance?.SelectedAgents.Count(a=>a&&a.IsAlive&&!PolicePatrolVehicle.IsMounted(a))??0;
    public bool IsVanSelected=>PolicePatrolVehicle.Instance&&PolicePatrolVehicle.Instance.Selected;
    public PoliceCrowdOperations CrowdOperations {get;private set;}
    public PoliceCallData PrimaryArrestCall=>Career?.calls.FirstOrDefault(c=>c.kind==1&&!c.id.Contains(":crowd:"));
    public int PrimaryArrestCallIndex=>PrimaryArrestCall!=null?Career.calls.IndexOf(PrimaryArrestCall):-1;
    public CityRoadIncidentSystem.RoadIncident TutorialRoadIncident=>CityRoadIncidentSystem.Instance?CityRoadIncidentSystem.Instance.GetIncident(Career.PoliceTutorialRoadId):null;
    public int TutorialRoadOfficerCount=>CityRoadIncidentSystem.Instance?CityRoadIncidentSystem.Instance.PresentOfficerCount(Career.PoliceTutorialRoadId):0;
    public string EnsureTutorialRoadIncident()
    {
        var system=CityRoadIncidentSystem.Ensure();var existing=TutorialRoadIncident;
        if(existing!=null)return existing.Id;
        existing=system.ActiveIncidents.FirstOrDefault(i=>i.RequiresTow);
        if(existing==null)for(int i=0;i<6&&existing==null;i++)system.TryCreateIncident(Station+new Vector3(i%2==0?35:-35,0,25+i*12),2,out existing);
        if(existing!=null){Career.PoliceTutorialRoadId=existing.Id;Save();}
        return existing?.Id;
    }
    public bool DriveVanToStation()
    {
        var van=PolicePatrolVehicle.Instance;if(!van)return false;
        bool accepted=van.DriveTo(Station);Report(accepted?"Patrol van returning to station. Park, then BOOK AT STATION to unload and walk the suspect to booking.":van.Status);return accepted;
    }
    readonly Vector3[] patrolPoints=new Vector3[3];
    readonly float[] patrolProgress=new float[3];
    readonly List<GameObject> markers=new List<GameObject>();
    readonly Dictionary<string,GameObject> callMarkers=new Dictionary<string,GameObject>();
    readonly Dictionary<string,EnemyController> suspects=new Dictionary<string,EnemyController>();
    readonly Dictionary<string,AgentController[]> responseCrews=new Dictionary<string,AgentController[]>();
    readonly Dictionary<string,int[]> responseRevisions=new Dictionary<string,int[]>();
    readonly Dictionary<string,int> escortRevisions=new Dictionary<string,int>();
    readonly Dictionary<string,float> escortNext=new Dictionary<string,float>();
    readonly List<TextMeshProUGUI> officerLabels=new List<TextMeshProUGUI>();
    readonly List<Button> officerButtons=new List<Button>();
    RectTransform frame;
    GameObject sheet,receiptPlate;
    RectTransform callRows;
    TextMeshProUGUI objective,caption,status,selectionCaption;
    Button primary;
    Button vehicleSelect,vehicleAction,vehicleStop;
    TextMeshProUGUI vehicleStatus;
    float saveClock,uiClock;
    float receiptUntil;
    string lastReceipt;
    int focusedCall;
    string receipt="Tap an officer, then tap the street to MOVE. Tap another officer to add them.";

    public static void Launch(bool fresh=false)
    {
        if(Active){Instance?.OpenDispatch();return;}
        if(!GameData.instance){Debug.LogError("Police mode requires the loaded campaign service.");return;}
        if(BattleManager.instance)BattleManager.instance.PersistBattleProgress();
        firm=GameData.instance.PlayerData;priorHome=CityGameplay.HomeMode;
        Career=fresh?PoliceCampaignStore.Create():PoliceCampaignStore.LoadOrCreate();
        Active=true;CityGameplay.HomeMode=true;GameData.instance.PlayerData=Career.officers;
        var host=new GameObject("Police Career Session");DontDestroyOnLoad(host);host.AddComponent<PoliceCityMode>();
        GameManager.PendingBattleMode=BattleManager.BattleMode.RivalFight;GameManager.PendingEnemyCount=6;GameManager.PendingEnemyStrength=12;
        GameManager.PendingBattleReward=0;GameManager.PendingEnemyFirmName="CITY INCIDENTS";
        GameManager.LoadScene("Gameplay","POLICE PATROL","Your officers. Your patrol routes. Your dispatch decisions.");
    }
    void Awake(){Instance=this;SceneManager.sceneLoaded+=Loaded;}
    void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(!Active)return;
        if(scene.name=="Gameplay"){Ready=false;StartCoroutine(PrepareCity());}
        else if(scene.name==GameManager.SCENE_MAIN_MENU)RestoreFirm();
    }
    IEnumerator PrepareCity()
    {
        float until=Time.realtimeSinceStartup+90;
        while((!BattleManager.instance||!BattleManager.instance.BattleActive||!CityGameplay.Instance||CityGameplay.Instance.Locations?.Length<9)&&Time.realtimeSinceStartup<until)yield return null;
        if(!BattleManager.instance||!CityGameplay.Instance){Debug.LogError("Police city did not become ready.");yield break;}
        var city=CityGameplay.Instance;
        Station=CityGameplay.ReachableApproach(city.Home,CityLandmarks.Police(city.Home).Point);
        // The first patrol is a short walk. Subsequent checks grow into the city.
        for(int i=0;i<3;i++)patrolPoints[i]=ConnectedPatrolPoint(i);
        foreach(var enemy in BattleManager.instance.EnemyAgents.ToArray())if(enemy)enemy.gameObject.SetActive(false);
        SuppressFirmPresentation();
        foreach(var member in BattleManager.instance.PlayerAgents)
        {
            if(!member)continue;member.SetCinematicIdle(false);member.SetActivityLocked(false,Station);
            CrewKit.PaintShirt(member.transform,new Color(.12f,.38f,.85f));
            UnitAffiliationMarker.Attach(member.transform,new Color(.15f,.65f,1f),1.25f,true);
            var saved=Career.positions.FirstOrDefault(p=>p.id==member.Data.AgentId);
            if(saved==null){var nav=member.GetComponent<NavMeshAgent>();if(nav&&nav.isOnNavMesh)nav.Warp(CityGameplay.ReachableApproach(Station,Station+Vector3.right*BattleManager.instance.PlayerAgents.ToList().IndexOf(member)*2));}
            if(saved!=null)
            {
                var point=CityGameplay.ReachableApproach(Station,saved.position);
                var nav=member.GetComponent<NavMeshAgent>();if(nav&&nav.isOnNavMesh)nav.Warp(point);
                if(saved.duty=="Guard")
                {
                    if(saved.dutyVersion>0&&saved.rectangularGuard)member.CommandGuard(new Bounds(saved.guardCenter,saved.guardSize),saved.guardPost);
                    else if(saved.dutyVersion>0)member.CommandArea(saved.guardPost,false);
                    else member.CommandGuard(new Bounds(saved.destination,new Vector3(12,8,12)));
                }
                else if(saved.duty=="Patrol")
                {
                    if(saved.dutyVersion>0)member.RestorePatrol(saved.patrolA,saved.patrolB,saved.patrolReturning);
                    else member.CommandPatrol(point,saved.destination);
                }
                else if(saved.duty=="MovingToPoint")member.TryCommandMoveTo(saved.destination);
            }
        }
        foreach(var call in Career.calls)if(call.state==2){call.state=1;call.progress=0;}
        AgentSelectionManager.instance?.DeselectAll();
        PolicePatrolVehicle.Create(this,Career.vehicle);
        BuildMarkers();
        PolicePatrolVehicle.Instance?.RestoreSuspects(Career.vehicle?.suspectCallIds);
        foreach(var call in Career.calls)if(call.inVehicle&&!(PolicePatrolVehicle.Instance&&PolicePatrolVehicle.Instance.HasSuspect(call.id)))call.inVehicle=false;
        PoliceRecoveryVehicle.Restore(Career.recoveryVehicle);
        CrowdOperations=gameObject.GetComponent<PoliceCrowdOperations>();if(!CrowdOperations)CrowdOperations=gameObject.AddComponent<PoliceCrowdOperations>();CrowdOperations.Initialize(this);
        BuildHud();
        CameraPanTouchOnly.Instance?.FocusOn(Station);
        Ready=true;Save();GuidedCityTutorial.Ensure();
    }
    void SuppressFirmPresentation()
    {
        // Keep scene, traffic, pedestrian ambience, free camera and input backend.
        foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            string type=behaviour.GetType().Name;
            if(type=="ModernCityHud"||type=="HomeTerritorySystem"||type=="CityMissionHUD"||type=="CityGameplay"||
                type=="CityOperationsSystem"||type=="CityBusinessSystem"||type=="CityDevelopmentSystem"||
                type=="LivePoliceSystem"||type=="StadiumMatchdayActivity"||
                type=="MatchdayStrategySystem"||type=="CityActionSystem"||type=="CityLifeActivity"||type=="CityLocationLabel"||
                type=="RecruitmentPin"||type=="RecoveryWardPin"||type=="GangArea"||type=="GangScreenChoice"||type=="TerritoryControlPoint")behaviour.enabled=false;
        }
        foreach(var bubble in FindObjectsByType<WorldInteractBubble>(FindObjectsSortMode.None))bubble.Hide();
        foreach(var bar in FindObjectsByType<WorldChoiceBar>(FindObjectsSortMode.None))bar.Hide();
        foreach(var hud in FindObjectsByType<LandscapeBattleHUD>(FindObjectsSortMode.None))if(hud.frame)hud.frame.gameObject.SetActive(false);
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if(new[]{"WorldButtonCanvas","MissionPanelCanvas","CityOperationsCanvas","CityDevelopmentCanvas"}.Contains(canvas.name))canvas.gameObject.SetActive(false);
        foreach(var text in FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None))
            if(text.name=="ZoneLabel")text.gameObject.SetActive(false);
        foreach(var marker in FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
            if(marker.name.Contains("Zone")||marker.name.Contains("Ring"))
                if(!marker.GetComponentInParent<AgentController>())marker.enabled=false;
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();CityUiSheets.CloseCurrent();
    }
    Vector3 ConnectedPatrolPoint(int ordinal)
    {
        for(int ring=0;ring<5;ring++)for(int n=0;n<12;n++)
        {
            float angle=(ordinal*4+n)*Mathf.PI/6;
            var guess=Station+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(28+ring*18);
            var point=CityGameplay.ReachableApproach(Station,guess);
            if(CityBusinessSystem.HorizontalDistance(point,Station)<16)continue;
            bool distinct=true;for(int previous=0;previous<ordinal;previous++)if(CityBusinessSystem.HorizontalDistance(point,patrolPoints[previous])<12)distinct=false;
            if(distinct)return point;
        }
        Debug.LogError("No distinct connected police patrol point "+ordinal);return Station;
    }
    void BuildMarkers()
    {
        responseCrews.Clear();responseRevisions.Clear();escortRevisions.Clear();escortNext.Clear();Array.Clear(patrolProgress,0,patrolProgress.Length);
        foreach(var old in markers)if(old)Destroy(old);markers.Clear();callMarkers.Clear();suspects.Clear();
        AddMarker("POLICE STATION\nBOOKING & SHIFT BRIEF",Station,new Color(.12f,.5f,1f),8);
        for(int i=0;i<3;i++)AddMarker("PATROL CHECK "+(i+1),patrolPoints[i],new Color(.2f,.75f,1f),4);
        var city=CityGameplay.Instance;
        foreach(var call in Career.calls)
        {
            Vector3 location=CityGameplay.ReachableApproach(Station,city.Locations[Mathf.Clamp(call.location,0,city.Locations.Length-1)]);
            // Saved locations are validated; no mandatory target uses a one-way street.
            if(!call.positioned){call.suspectPosition=location;call.positioned=true;}
            else call.suspectPosition=CityGameplay.ReachableApproach(Station,call.suspectPosition);
            if(call.state>=4)continue;
            callMarkers[call.id]=AddMarker(call.title,call.suspectPosition,new Color(1f,.72f,.15f),6);
            if(call.kind==1&&call.state>=1)SpawnSuspect(call);
        }
    }
    GameObject AddMarker(string label,Vector3 point,Color color,float radius)
    {
        var root=new GameObject("Police "+label);root.transform.position=point;markers.Add(root);
        ZoneVolumeFactory.Create(root.transform,color,radius,.08f);
        var text=ZoneLabelUtil.Create(root.transform,label,3.5f,5f);text.color=color;
        return root;
    }
    EnemyController SpawnSuspect(PoliceCallData call)
    {
        if(suspects.TryGetValue(call.id,out var existing)&&existing)return existing;
        var suspect=BattleManager.instance.SpawnMatchdayFighter(call.suspectPosition,"IDENTIFIED SUSPECT",new Color(.9f,.45f,.12f),100,0,MatchdayUnitRole.RivalSupporter,"REPORTED SHOPLIFTING");
        if(suspect){suspect.HoldFire(true);suspect.SetCinematicIdle(true);suspect.isHostile=false;var march=suspect.GetComponent<MatchdayMarch>();if(march)march.enabled=false;suspects[call.id]=suspect;}
        return suspect;
    }
    public EnemyController GetSuspect(PoliceCallData call)=>SpawnSuspect(call);
    public void SaveCareerProgress()=>Save();
    public void Report(string message){receipt=message;CityGameplay.Instance?.PostEvent(message);}
    public PoliceCallData RegisterCrowdSuspect(string id,EnemyController actor)
    {
        var existing=Career.calls.FirstOrDefault(c=>c.id==id);if(existing!=null)return existing;
        if(!actor||!actor.IsAlive)return null;
        var call=new PoliceCallData{id=id,title="CLASH SUSPECT",detail="Identified after crowd control. Arrest or accept the offered bribe with integrity consequences.",kind=1,state=1,positioned=true,suspectPosition=actor.transform.position,bribeOffer=240};
        Career.calls.Add(call);suspects[id]=actor;actor.HoldFire(true);actor.SetCinematicIdle(true);actor.isHostile=false;
        callMarkers[id]=AddMarker(call.title,call.suspectPosition,LandscapeUI.Gold,4);Save();return call;
    }
    public void CaptureState()
    {
        if(!Active||Career==null)return;
        foreach(var pair in suspects)if(pair.Value)
        {var call=Career.calls.Find(c=>c.id==pair.Key);if(call!=null)call.suspectPosition=pair.Value.transform.position;}
        if(BattleManager.instance&&Ready)
        {
            if(PolicePatrolVehicle.Instance)Career.vehicle=PolicePatrolVehicle.Instance.Capture();
            if(PoliceRecoveryVehicle.Instance)Career.recoveryVehicle=PoliceRecoveryVehicle.Instance.Capture();
            Career.positions.Clear();
            foreach(var member in BattleManager.instance.PlayerAgents)if(member)
            {
                member.SyncHpToData();
                Career.positions.Add(new PoliceOfficerSave{id=member.Data.AgentId,position=member.transform.position,destination=member.CommandDestination,
                    duty=member.CurrentOrder!=AgentController.StandingOrder.None?member.CurrentOrder.ToString():member.CurrentState.ToString(),
                    dutyVersion=1,patrolA=member.DutyStart,patrolB=member.DutyEnd,patrolReturning=member.PatrolReturning,
                    rectangularGuard=member.IsRectangularGuard,guardCenter=member.GuardRegion.center,guardSize=member.GuardRegion.size,guardPost=member.DutyEnd});
            }
        }
    }
    static void Save(){if(Active)PoliceCampaignStore.Save(GameManager.Data);}
    void Update()
    {
        if(!Active||!Ready||Career==null||!BattleManager.instance)return;
        float dt=Time.deltaTime;
        foreach(var actor in BattleManager.instance.PlayerAgents)if(actor&&actor.IsAlive)actor.FightPace=Career.morale<35?.8f:1.05f;
        Career.officers.FanMorale=Career.morale;
        PatrolTick(dt);CallTick(dt);saveClock+=dt;
        if(saveClock>=10){saveClock=0;Save();}
        if(Time.unscaledTime>=uiClock){uiClock=Time.unscaledTime+.2f;RefreshHud();}
    }
    void PatrolTick(float dt)
    {
        for(int i=0;i<3;i++)
        {
            if(Career.patrolChecks.Contains(i))continue;
            bool covered=BattleManager.instance.PlayerAgents.Any(a=>a&&a.IsAlive&&!PolicePatrolVehicle.IsMounted(a)&&Vector3.Distance(a.transform.position,patrolPoints[i])<9);
            if(covered)patrolProgress[i]+=dt;
            if(patrolProgress[i]<6)continue;
            Career.patrolChecks.Add(i);receipt="Patrol check "+(i+1)+" complete. Other officers keep their duties.";Save();
        }
    }
    AgentController Assigned(PoliceCallData call)=>BattleManager.instance.PlayerAgents.FirstOrDefault(a=>a&&a.IsAlive&&!PolicePatrolVehicle.IsMounted(a)&&a.Data.AgentId==call.assignedOfficerId);
    public Vector3 CallActionPoint(PoliceCallData call)
    {
        if(call==null)return Station;
        // Booking belongs at the station only while there is a living escort.
        // A handover belongs beside the waiting suspect, wherever they stopped.
        if(call.state==3&&Assigned(call))return Station;
        return suspects.TryGetValue(call.id,out var suspect)&&suspect?suspect.transform.position:call.suspectPosition;
    }
    void CallTick(float dt)
    {
        foreach(var call in Career.calls)
        {
            if(call.state>=4)continue;
            call.deadlineSeconds=Mathf.Max(0,call.deadlineSeconds-dt);
            if(call.deadlineSeconds<=0&&call.state<2)
            {
                // Missing a call changes trust, never a surprise game-over.
                call.deadlineSeconds=300;Career.trust=Mathf.Max(0,Career.trust-5);receipt="DISPATCH DELAY: public trust -5. The call remains available.";
            }
            if(call.state==0&&responseCrews.TryGetValue(call.id,out var crew))
            {
                bool revised=crew.Where((a,i)=>!a||!a.IsAlive||PolicePatrolVehicle.IsMounted(a)||a.OrderRevision!=responseRevisions[call.id][i]).Any();
                if(revised){responseCrews.Remove(call.id);call.progress=0;receipt="Response cancelled by a new order. Redispatch when ready.";continue;}
                bool nearby=crew.All(a=>Vector3.Distance(a.transform.position,call.suspectPosition)<10);
                if(!nearby)continue;
                call.progress+=dt;
                float duration=call.kind==2?15:8;
                if(call.progress<duration)continue;
                call.progress=0;
                if(call.kind==2){Resolve(call,"Station approach protected. Public trust +2.");}
                else{call.state=1;SpawnSuspect(call);receipt="Report assessed. Suspect identified. Select a nearby officer and choose DETAIN.";Save();}
            }
            if(call.state==2)
            {
                var officer=Assigned(call);
                if(!officer||!suspects.TryGetValue(call.id,out var suspect)||!suspect||Vector3.Distance(officer.transform.position,suspect.transform.position)>6)
                {call.state=1;call.progress=0;receipt="Detention interrupted. Return to the identified suspect.";continue;}
                if(!escortRevisions.TryGetValue(call.id,out int revision)||officer.OrderRevision!=revision){call.state=1;call.progress=0;receipt="New order cancelled detention.";continue;}
                call.progress+=dt;if(call.progress<5)continue;
                call.state=3;Career.arrests++;call.progress=0;
                GameFeedback.Emit(GameFeedbackEvent.Arrest,suspect.transform.position);
                suspect.SetCinematicIdle(false);suspect.HoldFire(true);
                receipt="Suspect detained. Escorting officer must physically return to the station for booking. Choose ESCORT TO STATION.";Save();
            }
            if(call.state==3)
            {
                if(call.inVehicle)
                {
                    call.progress=0;
                    if(!(PolicePatrolVehicle.Instance&&PolicePatrolVehicle.Instance.HasSuspect(call.id)))call.inVehicle=false;
                    else continue;
                }
                var suspect=SpawnSuspect(call);if(!suspect)continue;
                var officer=Assigned(call);
                if(!officer)
                {
                    // A partial squad loss must not strand an already detained
                    // suspect. Keep the suspect visible and waiting for an
                    // explicit handover to another selected officer nearby.
                    suspect.SetCinematicIdle(true);call.progress=0;
                    receipt="Escort officer down. Bring another officer beside the detained suspect, select them, then TAKE OVER ESCORT.";
                    continue;
                }
                if(!escortNext.TryGetValue(call.id,out float followAt)||Time.time>=followAt){escortNext[call.id]=Time.time+.4f;suspect.SetCinematicIdle(false);suspect.BeginEscort(officer.transform.position+Vector3.back*2,true);}
                if(Vector3.Distance(officer.transform.position,Station)>12||Vector3.Distance(suspect.transform.position,Station)>15){call.progress=0;continue;}
                call.progress+=dt;
                if(call.progress<4)continue;
                Career.booked++;call.resolution="BOOKED";Career.integrity=Mathf.Min(100,Career.integrity+2);Career.morale=Mathf.Min(100,Career.morale+2);suspect.gameObject.SetActive(false);Resolve(call,"Suspect physically booked at the station. Integrity +2, morale +2; other patrols continue.");
            }
        }
    }
    void Resolve(PoliceCallData call,string message)
    {call.state=4;call.progress=0;Career.resolvedCalls++;Career.trust=Mathf.Min(100,Career.trust+2);receipt=message;Save();}
    public bool Respond(int index)
    {
        if(!Ready||index<0||index>=Career.calls.Count)return false;
        focusedCall=index;var call=Career.calls[index];
        var van=PolicePatrolVehicle.Instance;
        if(van&&van.Selected&&van.PassengerCount>0)
        {
            bool driven=van.DriveTo(CallActionPoint(call));
            receipt=driven?"Patrol van responding. EXIT OFFICERS at the curb, then select the team and assess the call.":van.Status;
            if(driven)CloseDispatch();return driven;
        }
        var selected=AgentSelectionManager.instance?.SelectedAgents.Where(a=>a&&a.IsAlive&&!a.IsActivityLocked).ToArray();
        if(selected==null||selected.Length==0){receipt="Select one or more officers first.";return false;}
        if(call.state!=0){receipt="This call is ready for "+(call.state==1?"detention.":call.state==3?"escort and booking.":"its next step.");return false;}
        var accepted=selected.Where(a=>a.TryCommandMoveTo(call.suspectPosition)).ToArray();
        if(accepted.Length==0){receipt="No selected officer has a complete route to this call.";return false;}
        responseCrews[call.id]=accepted;responseRevisions[call.id]=accepted.Select(a=>a.OrderRevision).ToArray();
        receipt=accepted.Length+" officers responding to "+call.title+". Other patrols continue.";
        CloseDispatch();Save();return true;
    }
    public bool Detain(int index)
    {
        if(!Ready||index<0||index>=Career.calls.Count)return false;
        var call=Career.calls[index];if(call.kind!=1||call.state!=1)return false;
        call.bribeDecisionMade=true;
        var suspect=SpawnSuspect(call);if(!suspect)return false;
        var officer=AgentSelectionManager.instance?.SelectedAgents.FirstOrDefault(a=>a&&a.IsAlive&&!a.IsActivityLocked&&Vector3.Distance(a.transform.position,suspect.transform.position)<=6);
        if(!officer){receipt="Select a living officer within 6 metres of the identified suspect. Tap the nearby street to approach.";return false;}
        officer.TryCommandMoveTo(officer.transform.position);call.assignedOfficerId=officer.Data.AgentId;
        escortRevisions[call.id]=officer.OrderRevision;call.state=2;call.progress=0;receipt="Detaining identified suspect - stay nearby for 5 seconds.";Save();return true;
    }
    public bool TryAcceptBribe(int index)
    {
        if(!Ready||index<0||index>=Career.calls.Count)return false;
        var call=Career.calls[index];var suspect=call.state==1?SpawnSuspect(call):null;
        bool nearby=suspect&&AgentSelectionManager.instance&&AgentSelectionManager.instance.SelectedAgents.Any(a=>a&&a.IsAlive&&!a.IsActivityLocked&&!PolicePatrolVehicle.IsMounted(a)&&Vector3.Distance(a.transform.position,suspect.transform.position)<=6);
        if(!nearby){Report("Bring a selected on-foot officer within 6m of the identified suspect before deciding.");return false;}
        if(!PoliceCampaignStore.AcceptBribe(Career,call))return false;
        suspect.SetCinematicIdle(false);suspect.HoldFire(true);suspect.TryBeginEscort(CityGameplay.ReachableApproach(suspect.transform.position,suspect.transform.position+Vector3.right*18),true);
        Report($"Bribe +£{Mathf.Clamp(call.bribeOffer,80,600)} accepted once. Integrity -18, trust -8, morale -6; £150 disciplinary debt due at shift end. Low integrity blocks backup.");
        GameFeedback.Emit(GameFeedbackEvent.Warning,suspect.transform.position);Save();RefreshHud();return true;
    }
    public bool LoadSuspect(int index)
    {
        if(!Ready||index<0||index>=Career.calls.Count)return false;
        var call=Career.calls[index];var van=PolicePatrolVehicle.Instance;
        if(call.state!=3||call.inVehicle||!van)return false;
        var officer=Assigned(call);var suspect=SpawnSuspect(call);
        if(!officer||!suspect){Report("A living escort must take over beside the detained suspect before loading.");return false;}
        bool accepted=van.RequestSuspectLoading(call.id,suspect,officer);Report(van.Status);Save();return accepted;
    }
    public bool BookVanSuspects()
    {var van=PolicePatrolVehicle.Instance;if(!van)return false;bool accepted=van.UnloadSuspectsAtStation();Report(van.Status);return accepted;}
    public bool RequestReinforcements()
    {
        if(!Ready||!PoliceCampaignStore.ReinforcementEligible(Career)){Report("Backup needs £500, integrity 55+, trust 55+, morale 35+, an open roster slot and no earlier backup this shift.");return false;}
        var data=new AgentData("Officer Backup "+(Career.officers.RecruitedAgents.Count+1),1,hp:160,strength:18,speed:5);
        BattleManager.instance.RestoreRecoveredAgent(data);var actor=BattleManager.instance.PlayerAgents.FirstOrDefault(a=>a&&a.Data==data);
        if(!actor){Report("Backup could not find a safe station spawn. No payment taken.");return false;}
        Career.officers.RecruitedAgents.Add(data);Career.officers.Money-=500;Career.reinforcementReceipts.Add("shift-"+Career.shift);
        var nav=actor.GetComponent<NavMeshAgent>();if(nav&&nav.isOnNavMesh)nav.Warp(CityGameplay.ReachableApproach(Station,Station+Vector3.right*4));
        CrewKit.PaintShirt(actor.transform,new Color(.12f,.38f,.85f));Report("Backup officer deployed at the station. £500 paid once; use the roster strip or world/map selection.");Save();BuildHud();return true;
    }
    public bool Escort(int index)
    {
        if(!Ready||index<0||index>=Career.calls.Count)return false;
        var call=Career.calls[index];if(call.state!=3||call.inVehicle)return false;
        var officer=Assigned(call);
        bool transfer=!officer;
        EnemyController suspect=null;
        if(transfer)
        {
            suspect=SpawnSuspect(call);
            if(!suspect){receipt="The detained suspect is not available yet. Focus the call and retry.";return false;}
            officer=AgentSelectionManager.instance?.SelectedAgents
                .Where(a=>a&&a.IsAlive&&!a.IsActivityLocked&&Vector3.Distance(a.transform.position,suspect.transform.position)<=6)
                .OrderBy(a=>(a.transform.position-suspect.transform.position).sqrMagnitude).FirstOrDefault();
            if(!officer){receipt="Bring a living officer within 6 metres of the detained suspect, select them, then TAKE OVER ESCORT.";return false;}
        }
        bool accepted=officer.TryCommandMoveTo(Station);
        if(!accepted){receipt="Station route blocked. Move the escort onto a connected street.";return false;}
        if(transfer)
        {
            call.assignedOfficerId=officer.Data.AgentId;call.progress=0;
            suspect.SetCinematicIdle(false);suspect.BeginEscort(officer.transform.position+Vector3.back*2,true);
            Save();
        }
        receipt=transfer?"Escort handed over. The existing detention stays recorded once. Return together to station for booking.":"Escort returning to station. Suspect follows on foot. Book inside the blue station area.";
        return true;
    }
    public bool AssignPatrol(int index)
    {
        if(!Ready||index<0||index>=3)return false;
        var van=PolicePatrolVehicle.Instance;
        if(van&&van.Selected&&van.PassengerCount>0)
        {
            bool driven=van.DriveTo(patrolPoints[index]);receipt=driven?"Drive the patrol team to the beat, then EXIT OFFICERS and hold the marked street on foot.":van.Status;
            if(driven)CloseDispatch();return driven;
        }
        var selected=AgentSelectionManager.instance?.SelectedAgents;if(selected==null)return false;int count=0;
        foreach(var officer in selected)if(officer&&officer.IsAlive&&officer.CommandPatrol(officer.transform.position,patrolPoints[index]))count++;
        receipt=count>0?count+" officers patrol the marked route and return automatically.":"Select an available officer and a connected street.";CloseDispatch();return count>0;
    }
    public bool CompleteShift()
    {
        if(!PoliceCampaignStore.FinishShift(Career)){receipt="Complete three patrol checks, resolve both calls, and book the identified suspect before ending this shift.";return false;}
        receipt=$"SHIFT COMPLETE: £750 grant; £{Career.lastDisciplineCost} disciplinary deductions. Integrity {Career.integrity}, morale {Career.morale}. Continue when ready.";GameFeedback.Emit(GameFeedbackEvent.TaskComplete,Station);Save();RefreshHud();return true;
    }
    public void BeginNextShift()
    {
        if(!Career.completedShiftKeys.Contains("shift-"+Career.shift))return;
        // Healing is a saved transition. The previous live actors may still be
        // injured/down until scene unload; do not capture their old HP over it.
        Ready=false;
        PoliceCampaignStore.NextShift(Career);Save();
        GameManager.LoadScene("Gameplay","NEXT POLICE SHIFT","New dispatch calls. The same officer career.");
    }
    public void RecoverOfficers()
    {
        if(!Active||Career.officers.RecruitedAgents.Any(a=>a!=null&&a.IsAlive))return;
        Ready=false;Career.positions.Clear();Career.vehicle=null;
        foreach(var officer in Career.officers.RecruitedAgents)officer.FullHeal();Career.trust=Mathf.Max(20,Career.trust-10);
        Save();GameManager.LoadScene("Gameplay","REGROUP AT STATION","Officers recovered. Trust -10; your shift remains saved.");
    }
    public static void ExitToMenu()
    {if(!Active)return;Save();RestoreFirm();GameManager.LoadScene(GameManager.SCENE_MAIN_MENU,"SHIFT SAVED","Firm and police careers are stored separately.");}
    static void RestoreFirm()
    {
        if(!Active)return;Save();Active=false;
        if(GameData.instance&&firm!=null)GameData.instance.PlayerData=firm;
        CityGameplay.HomeMode=priorHome;firm=null;
        if(Instance){SceneManager.sceneLoaded-=Instance.Loaded;Destroy(Instance.gameObject);}Instance=null;Career=null;
    }
    void OnApplicationPause(bool paused){if(paused)Save();}
    void OnApplicationQuit(){Save();}
    void OnDestroy(){SceneManager.sceneLoaded-=Loaded;if(Instance==this)Instance=null;}

    void BuildHud()
    {
        if(frame)Destroy(frame.GetComponentInParent<Canvas>().gameObject);
        var canvas=new GameObject("PoliceCommandCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=19000;
        LandscapeUI.ConfigureLandscapeScaler(canvas.GetComponent<CanvasScaler>());
        var safe=LandscapeUI.Rect("Safe",canvas.transform,0,0,1600,900);LandscapeUI.Stretch(safe);
        frame=LandscapeUI.Rect("Frame",safe,0,0,1600,900);var viewport=safe.gameObject.AddComponent<LandscapeViewport>();viewport.frame=frame;viewport.Fit();
        LandscapeUI.Panel("Top",frame,16,14,1568,70,true);
        caption=LandscapeUI.Text("Career",frame,"",36,26,720,44,25,LandscapeUI.White,true);
        Tag(LandscapeUI.Button("Dispatch",frame,"DISPATCH",1110,24,220,48),"police.dispatch").onClick.AddListener(OpenDispatch);
        LandscapeUI.Button("Menu",frame,"SAVE & MENU",1340,24,226,48).onClick.AddListener(ExitToMenu);
        var transport=LandscapeUI.Panel("Transport",frame,18,102,280,244,true).rectTransform;
        vehicleSelect=LandscapeUI.Button("SelectVan",transport,"PATROL VAN",12,12,256,48);
        Tag(vehicleSelect,"police.van");
        vehicleSelect.onClick.AddListener(()=>{PolicePatrolVehicle.Instance?.Select(true);RefreshHud();});
        vehicleStatus=LandscapeUI.Text("VanState",transport,"",14,70,252,64,16,LandscapeUI.White);
        vehicleStatus.overflowMode=TextOverflowModes.Ellipsis;
        vehicleAction=LandscapeUI.Button("BoardExit",transport,"BOARD SELECTED",12,140,256,44,"green");
        Tag(vehicleAction,"police.boardExit");
        vehicleAction.onClick.AddListener(()=>{var van=PolicePatrolVehicle.Instance;if(van){if(van.PassengerCount>0)van.ExitAll();else van.BoardSelected();receipt=van.Status;}RefreshHud();});
        vehicleStop=LandscapeUI.Button("StopVan",transport,"STOP / CANCEL",12,190,256,40);
        vehicleStop.onClick.AddListener(()=>{var van=PolicePatrolVehicle.Instance;if(van){van.Stop();van.CancelBoarding();receipt=van.Status;}RefreshHud();});
        var goal=LandscapeUI.Panel("Objective",frame,322,102,940,90,true).rectTransform;
        objective=LandscapeUI.Text("NextAction",goal,"",18,12,734,65,19,LandscapeUI.White,true);
        LandscapeUI.Button("Guide",goal,"SHOW ME",770,20,150,50).onClick.AddListener(FocusNext);
        var receiptRoot=LandscapeUI.Panel("ReceiptPlate",frame,322,206,940,44,true).rectTransform;
        receiptPlate=receiptRoot.gameObject;
        status=LandscapeUI.Text("Receipt",receiptRoot,receipt,16,8,908,28,17,LandscapeUI.White);
        status.textWrappingMode=TextWrappingModes.NoWrap;status.overflowMode=TextOverflowModes.Ellipsis;
        selectionCaption=LandscapeUI.Text("Selection",frame,"",322,724,940,38,18,LandscapeUI.White,true);
        var board=LandscapeUI.Panel("Officers",frame,322,770,940,96,true).rectTransform;
        officerLabels.Clear();officerButtons.Clear();
        var roster=LandscapeUI.Scroll("OfficerRoster",board,12,12,654,72,horizontal:true).content;
        for(int i=0;i<Mathf.Min(6,BattleManager.instance.PlayerAgents.Count);i++)
        {
            int index=i;var button=LandscapeUI.Button("Officer"+i,roster,"",0,0,210,64);LandscapeUI.LayoutSize(button.gameObject,210,64);Tag(button,"police.officer"+i);
            button.onClick.AddListener(()=>{var agents=BattleManager.instance.PlayerAgents;if(index<agents.Count&&!PolicePatrolVehicle.IsMounted(agents[index])){PolicePatrolVehicle.Instance?.Deselect();AgentSelectionManager.instance.ToggleSelect(agents[index]);}});
            officerLabels.Add(button.GetComponentInChildren<TextMeshProUGUI>());officerButtons.Add(button);
        }
        primary=LandscapeUI.Button("Context",board,"PATROL",684,12,240,72,"green");primary.onClick.AddListener(PrimaryAction);
        var panel=LandscapeUI.Panel("DispatchSheet",frame,290,190,1020,566,true).rectTransform;sheet=panel.gameObject;
        LandscapeUI.Text("Heading",panel,"POLICE DISPATCH",20,16,700,40,28,LandscapeUI.White,true);
        LandscapeUI.Button("Close",panel,"CLOSE",830,14,170,46).onClick.AddListener(CloseDispatch);
        callRows=LandscapeUI.Scroll("Calls",panel,20,74,980,408).content;
        LandscapeUI.Button("EndShift",panel,"COMPLETE SHIFT",20,500,470,48,"green").onClick.AddListener(()=>{CompleteShift();OpenDispatch();});
        LandscapeUI.Button("NextShift",panel,"NEXT SHIFT",510,500,490,48).onClick.AddListener(BeginNextShift);
        sheet.SetActive(false);RefreshHud();
    }
    string Goal()
    {
        if(Career==null)return "Enter police patrol.";
        if(Career.officers.RecruitedAgents.All(a=>a==null||!a.IsAlive))return "All officers are down. Regroup at the station to recover your team.";
        if(Career.completedShiftKeys.Contains("shift-"+Career.shift))return "Shift complete. Budget and trust are saved. Choose NEXT SHIFT in Dispatch.";
        var van=PolicePatrolVehicle.Instance;
        if(van&&van.SuspectCount>0)return "CUSTODY TRANSPORT: drive to the police station, park, then DISPATCH > BOOK AT STATION. Suspects must walk from the curb to booking.";
        if(van&&van.Selected&&van.PassengerCount>0)return "PATROL VAN SELECTED: tap a road to drive your team. EXIT OFFICERS at the curb for patrol checks, assessment and arrests.";
        if(Career.patrolChecks.Count<3)return "PATROL YOUR BEAT: select officers and BOARD SELECTED to travel together, or walk. Hold each patrol check on foot for 6s. "+Career.patrolChecks.Count+"/3 checked.";
        var call=Career.calls.FirstOrDefault(x=>x.state<4);
        if(call==null)return "All calls resolved. Return to Dispatch and COMPLETE SHIFT.";
        return call.state==0?"DISPATCH: "+call.title+". Send selected officers and assess on foot.":call.state==1?"IDENTIFIED SUSPECT: approach with an officer. DETAIN, or accept a bribe with integrity/trust/discipline consequences.":call.state==2?"DETAINING: keep your officer beside the suspect for 5s.":!Assigned(call)?"ESCORT OFFICER DOWN: bring another officer beside the detained suspect, select them, then TAKE OVER ESCORT.":"CUSTODY: use DISPATCH > LOAD SUSPECT to walk to the parked van, or ESCORT ON FOOT to the station.";
    }
    void RefreshHud()
    {
        if(!frame||Career==null)return;
        foreach(var markerCall in Career.calls)
        {
            if(!callMarkers.TryGetValue(markerCall.id,out var marker)||!marker)continue;
            marker.SetActive(markerCall.state<4);
            if(markerCall.state<4)marker.transform.position=CallActionPoint(markerCall);
            // A booking marker can share the station ring without duplicating
            // the permanent station caption. A waiting handover needs its label.
            var markerLabel=marker.GetComponentInChildren<TextMeshPro>(true);
            if(markerLabel)markerLabel.gameObject.SetActive(!markerCall.inVehicle&&!(markerCall.state==3&&Assigned(markerCall)));
        }
        caption.text=$"POLICE {Career.shift:00} · £{Career.officers.Money:N0} · TRUST {Career.trust} · INT {Career.integrity} · MORALE {Career.morale}";
        string goalText=Goal();objective.text=goalText;status.text=receipt;
        if(lastReceipt!=receipt){lastReceipt=receipt;receiptUntil=Time.unscaledTime+6f;}
        bool duplicate=string.Equals((receipt??"").Trim(),goalText.Trim(),StringComparison.OrdinalIgnoreCase)
            ||goalText.StartsWith("ESCORT OFFICER DOWN",StringComparison.Ordinal)&&receipt.StartsWith("Escort officer down.",StringComparison.Ordinal);
        if(receiptPlate)receiptPlate.SetActive(!string.IsNullOrWhiteSpace(receipt)&&!duplicate&&!(PolicePatrolVehicle.Instance&&PolicePatrolVehicle.Instance.Selected&&receipt.StartsWith("Tap an officer"))&&Time.unscaledTime<receiptUntil);
        var select=AgentSelectionManager.instance;var van=PolicePatrolVehicle.Instance;
        selectionCaption.text=van&&van.Selected?$"PATROL VAN SELECTED · {van.PassengerCount}/{PolicePatrolVehicle.Capacity} ABOARD · Tap a road = DRIVE · EXIT OFFICERS = work on foot":$"{select?.SelectedAgents.Count??0} OFFICERS SELECTED · Tap street = MOVE · drag = CAMERA · tap officer = ADD / REMOVE";
        if(vehicleStatus)vehicleStatus.text=van?$"{van.PassengerCount} officers · {van.SuspectCount} custody · {van.BoardingCount} boarding\n{van.Status}":"Patrol van unavailable: on-foot duties remain playable.";
        if(vehicleAction){vehicleAction.interactable=van;vehicleAction.GetComponentInChildren<TextMeshProUGUI>().text=van&&van.PassengerCount>0?"EXIT OFFICERS":"BOARD SELECTED";}
        if(vehicleStop)vehicleStop.gameObject.SetActive(van&&(van.IsDriving||van.BoardingCount>0));
        for(int i=0;i<officerLabels.Count;i++)
        {
            var agents=BattleManager.instance.PlayerAgents;var agent=i<agents.Count?agents[i]:null;
            officerLabels[i].text=agent?agent.Data.AgentName.Replace("Officer ","")+"\n"+(PolicePatrolVehicle.IsMounted(agent)?"ABOARD VAN":(agent.IsSelected?"SELECTED · ":"")+(agent.IsAlive?agent.CurrentOrder.ToString().ToUpperInvariant():"DOWN")):"OFFICER UNAVAILABLE";
            officerLabels[i].color=agent&&agent.IsSelected?new Color(.2f,.85f,1f):LandscapeUI.White;
        }
        var call=Career.calls.ElementAtOrDefault(focusedCall);
        primary.GetComponentInChildren<TextMeshProUGUI>().text=Career.completedShiftKeys.Contains("shift-"+Career.shift)?"NEXT SHIFT":Career.calls.All(c=>c.state>=4)&&Career.patrolChecks.Count==3?"COMPLETE SHIFT":Career.officers.RecruitedAgents.All(a=>a==null||!a.IsAlive)?"RECOVER OFFICERS":van&&van.SuspectCount>0?(CityBusinessSystem.HorizontalDistance(van.transform.position,Station)>18?"RETURN TO STATION":"BOOK AT STATION"):call?.state==1?"DETAIN SUSPECT":call?.state==3?(!Assigned(call)?"TAKE OVER ESCORT":"ESCORT TO STATION"):Career.patrolChecks.Count<3?"PATROL NEXT CHECK":"RESPOND TO CALL";
    }
    void PrimaryAction()
    {
        var van=PolicePatrolVehicle.Instance;if(van&&van.SuspectCount>0){if(CityBusinessSystem.HorizontalDistance(van.transform.position,Station)>18)DriveVanToStation();else BookVanSuspects();return;}
        if(Career.completedShiftKeys.Contains("shift-"+Career.shift)){BeginNextShift();return;}
        if(Career.calls.All(c=>c.state>=4)&&Career.patrolChecks.Count==3){CompleteShift();return;}
        if(Career.officers.RecruitedAgents.All(a=>a==null||!a.IsAlive)){RecoverOfficers();return;}
        var call=Career.calls.ElementAtOrDefault(focusedCall);
        if(call?.state==1){Detain(focusedCall);return;}if(call?.state==3){Escort(focusedCall);return;}
        if(Career.patrolChecks.Count<3){AssignPatrol(Enumerable.Range(0,3).First(i=>!Career.patrolChecks.Contains(i)));return;}
        if(call?.state==0)Respond(focusedCall);else OpenDispatch();
    }
    public void FocusNext()
    {
        if(Career.patrolChecks.Count<3){CameraPanTouchOnly.Instance?.FocusOn(patrolPoints[Enumerable.Range(0,3).First(i=>!Career.patrolChecks.Contains(i))]);return;}
        focusedCall=Career.calls.FindIndex(c=>c.state<4);if(focusedCall<0){OpenDispatch();return;}
        var call=Career.calls[focusedCall];CameraPanTouchOnly.Instance?.FocusOn(CallActionPoint(call));OpenDispatch();
    }
    public void OpenDispatch()
    {
        if(!sheet)return;sheet.SetActive(true);LandscapeUI.Clear(callRows);
        bool done=Career.completedShiftKeys.Contains("shift-"+Career.shift);sheet.transform.Find("EndShift").gameObject.SetActive(!done);sheet.transform.Find("NextShift").gameObject.SetActive(done);
        var support=DispatchRow("Support",150);
        LandscapeUI.Text("Text",support,$"Integrity {Career.integrity}/100 · morale {Career.morale}/100 · discipline debt £{Career.disciplineDebt}\nBackup: £500, integrity/trust 55+, morale 35+. One officer per shift, six maximum.",16,12,620,110,18,LandscapeUI.White,true);
        var backup=LandscapeUI.Button("Backup",support,"REQUEST BACKUP £500",666,20,260,50);backup.interactable=PoliceCampaignStore.ReinforcementEligible(Career);backup.onClick.AddListener(()=>{RequestReinforcements();OpenDispatch();});
        if(PolicePatrolVehicle.Instance&&PolicePatrolVehicle.Instance.SuspectCount>0)Tag(LandscapeUI.Button("Book",support,"BOOK AT STATION",666,82,260,50,"green"),"police.book").onClick.AddListener(()=>{BookVanSuspects();CloseDispatch();});
        if(PolicePatrolVehicle.Instance&&PolicePatrolVehicle.Instance.SuspectCount>0)
        {
            var stationRow=DispatchRow("StationReturn",90);LandscapeUI.Text("Text",stationRow,"CUSTODY TRANSPORT\nDrive to station before unloading suspects for physical booking.",16,10,620,68,19,LandscapeUI.White,true);
            Tag(LandscapeUI.Button("Station",stationRow,"DRIVE TO STATION",666,18,260,54),"police.returnStation").onClick.AddListener(()=>{DriveVanToStation();CloseDispatch();});
        }
        for(int i=0;i<3;i++)
        {
            int index=i;var row=LandscapeUI.Panel("Patrol"+i,callRows,0,0,944,90,true);LandscapeUI.LayoutSize(row.gameObject,944,90);
            LandscapeUI.Text("Text",row.transform,$"PATROL CHECK {i+1} · {(Career.patrolChecks.Contains(i)?"COMPLETE":"HOLD 6s")}",16,14,620,56,21,LandscapeUI.White,true);
            Tag(LandscapeUI.Button("Assign",row.transform,"ASSIGN PATROL",666,20,260,50),"police.patrolAssign"+i).onClick.AddListener(()=>AssignPatrol(index));
        }
        for(int i=0;i<Career.calls.Count;i++)
        {
            int index=i;var call=Career.calls[i];var row=LandscapeUI.Panel(call.id,callRows,0,0,944,174,true);LandscapeUI.LayoutSize(row.gameObject,944,174);
            string state=call.state==0?"AWAITING RESPONSE":call.state==1?"IDENTIFIED SUSPECT":call.state==2?"DETAINING":call.state==3?(!Assigned(call)?"ESCORT OFFICER DOWN - HANDOVER REQUIRED":"ESCORT TO STATION"):"RESOLVED";
            LandscapeUI.Text("Text",row.transform,call.title+" · "+state+"\n"+call.detail,16,14,620,140,19,LandscapeUI.White,true);
            var action=LandscapeUI.Button("Action",row.transform,call.state==0?"RESPOND":call.state==1?"DETAIN":call.inVehicle?"SHOW VAN":call.state==3?(!Assigned(call)?"TAKE OVER ESCORT":"ESCORT"):"VIEW",666,30,260,54);
            Tag(action,"police.call"+i);
            action.onClick.AddListener(()=>{focusedCall=index;CameraPanTouchOnly.Instance?.FocusOn(CallActionPoint(call));if(call.state==0)Respond(index);else if(call.state==1)Detain(index);else if(call.inVehicle)PolicePatrolVehicle.Instance?.Select(true);else if(call.state==3)Escort(index);CloseDispatch();});
            if(call.state==1)Tag(LandscapeUI.Button("Bribe",row.transform,$"BRIBE +£{call.bribeOffer} / INT -18",666,98,260,50),"police.bribe"+i).onClick.AddListener(()=>{TryAcceptBribe(index);CloseDispatch();});
            if(call.state==3&&!call.inVehicle)Tag(LandscapeUI.Button("Load",row.transform,"LOAD SUSPECT",666,98,260,50,"green"),"police.load"+i).onClick.AddListener(()=>{LoadSuspect(index);CloseDispatch();});
        }
        if(CrowdOperations)
        {
            var row=DispatchRow("CrowdOperation",156);LandscapeUI.Text("Text",row,"STADIUM PUBLIC ORDER\n"+CrowdOperations.Description,16,12,620,122,19,LandscapeUI.White,true);
            Tag(LandscapeUI.Button("AssignCrowd",row,"ASSIGN CROWD TEAM",666,18,260,50),"police.crowdAssign").onClick.AddListener(()=>{CrowdOperations.AssignSelected();CloseDispatch();});
            LandscapeUI.Button("FocusCrowd",row,"SHOW CLASH",666,82,260,50).onClick.AddListener(()=>{CameraPanTouchOnly.Instance?.FocusOn(CrowdOperations.Location);CloseDispatch();});
        }
        if(CityRoadIncidentSystem.Instance)foreach(var incident in CityRoadIncidentSystem.Instance.ActiveIncidents)
        {
            string id=incident.Id;var row=DispatchRow("Road "+id,156);
            LandscapeUI.Text("Text",row,"ROAD RESPONSE · "+id+"\nSelect an on-foot officer to secure the site. Recovery truck: £450 hire once + £75 dispatch; physical arrival and crew coverage required.",16,12,620,122,18,LandscapeUI.White,true);
            string anchorSuffix=id==Career.PoliceTutorialRoadId?"":":"+id;
            Tag(LandscapeUI.Button("SecureRoad",row,"ASSIGN ROAD TEAM",666,18,260,50),"police.roadAssign"+anchorSuffix).onClick.AddListener(()=>{CityRoadIncidentSystem.Instance.TryAssignOfficers(id,AgentSelectionManager.instance.SelectedAgents);CloseDispatch();});
            var tow=Tag(LandscapeUI.Button("Recovery",row,"DISPATCH RECOVERY",666,82,260,50,"green"),(PoliceRecoveryVehicle.Instance?"police.recoveryDispatch":"police.recoveryHire")+anchorSuffix);
            tow.onClick.AddListener(()=>{CityRoadIncidentSystem.Instance.TryDispatchTow(id);CloseDispatch();});
        }
        if(PoliceRecoveryVehicle.Instance)
        {
            var row=DispatchRow("RecoveryControl",118);LandscapeUI.Text("Text",row,"RECOVERY TRUCK\n"+PoliceRecoveryVehicle.Instance.Status,16,10,620,94,18,LandscapeUI.White,true);
            Tag(LandscapeUI.Button("SelectRecovery",row,"DRIVE RECOVERY",666,24,260,54),"police.recoverySelect").onClick.AddListener(()=>{PoliceRecoveryVehicle.Instance.Select(true);CloseDispatch();});
        }
        var help=DispatchRow("Guide",100);LandscapeUI.Text("Text",help,"POLICE FIELD GUIDE\nReplay controls and real station/dispatch tasks.",16,10,620,80,19,LandscapeUI.White,true);
        Tag(LandscapeUI.Button("Replay",help,"REPLAY GUIDE",666,22,260,54),"police.replay").onClick.AddListener(()=>{CloseDispatch();GuidedCityTutorial.Replay(true);});
    }
    RectTransform DispatchRow(string name,float height){var row=LandscapeUI.Panel(name,callRows,0,0,944,height,true);LandscapeUI.LayoutSize(row.gameObject,944,height);return row.rectTransform;}
    static Button Tag(Button button,string id){TutorialAnchor.Tag(button,id);return button;}
    public void CloseDispatch(){if(sheet)sheet.SetActive(false);}
}
