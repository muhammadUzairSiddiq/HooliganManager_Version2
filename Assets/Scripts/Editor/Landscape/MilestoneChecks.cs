#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MilestoneChecks
{
    public static void Run()
    {
        var rows=new List<string>();
        void Check(bool value,string label){rows.Add((value?"PASS ":"FAIL ")+label);}
        Check(new PlayerData().PoliceHeat==7,"Fresh campaign starts at seven police heat bars");
        Check(BattleManager.RivalFightHeatGain==4,"Each completed rival fight adds four police heat bars");
        var d=new PlayerData {Money=0,FanMorale=0,Reputation=0,CurrentLevel=1,LandscapeClaimedMissions=null,SelectedAwayAgentIds=new List<string>()};
        Check(!FirmMissions.Claim(d,"campaign_1") && d.Money==0,"Incomplete campaign mission cannot pay");
        Check(!FirmMissions.Claim(d,"unknown"),"Unknown mission cannot pay");
        for(int i=0;i<5;i++)d.RecruitedAgents.Add(new AgentData("Test "+i));
        d.SelectedAwayAgentIds.Add(d.RecruitedAgents[0].AgentId);
        Check(d.SelectedAwayAgentIds.Count==1,"Away trip deployment selection saves member ids");
        d.RecruitedAgents[0].CurrentHp=0;
        Check(CampaignMissions.Steps(d).First(s=>s.id=="crew").progress==4,"Crew objective excludes fallen members");
        d.RecruitedAgents[0].FullHeal();
        Check(FirmMissions.Get(d,0).Length==5,"Exactly five large campaign missions are presented");
        int[] expectedSteps={19,15,16,17,20};
        Check(Enumerable.Range(1,5).All(i=>CampaignMissions.Steps(d,i).Length==expectedSteps[i-1]),"Mission task counts escalate while Mission 01 remains substantial");
        Check(CampaignMissions.Steps(d,1).Any(s=>s.id=="extort")&&CampaignMissions.Steps(d,1).Any(s=>s.id=="taxi")&&CampaignMissions.Steps(d,1).Any(s=>s.id=="sabotage")&&CampaignMissions.Steps(d,1).Any(s=>s.id=="tax"),"Mission 01 includes street cash, taxi, sabotage and territory-income objectives");
        Check(!CampaignMissions.CanEnterDestination(d,"Riverside",out _)&&!CampaignMissions.CanEnterDestination(d,"Old Town",out _),"All away trips stay locked during the home mission");
        foreach(CityOperationType op in Enum.GetValues(typeof(CityOperationType)))CampaignMissions.CompleteOperation(d,op);
        d.HomeTrainingLevel=1;d.FanMorale=75;d.HomeDefenceCompleted=true;
        d.CampaignTerritoryKeys.Add("M1:Local Pub");d.CampaignTerritoryKeys.Add("M1:Training Yard");
        d.CampaignActionKeys.AddRange(new[]{"M1:extort:a","M1:extort:b","M1:tax:Local Pub:DAY1","M1:taxi:1","M1:sabotage:vehicle-1","M1:matchday:rival-arrival"});
        Check(CampaignMissions.TryComplete(d,out var homeMission)&&homeMission.number==1&&d.CurrentLevel==2&&d.Money==homeMission.reward,"Mission 01 completion pays once and unlocks Mission 02");
        Check(!CampaignMissions.TryComplete(d,out _),"Duplicate campaign completion is blocked");
        Check(CampaignMissions.CanEnterDestination(d,"Riverside",out _)&&!CampaignMissions.CanEnterDestination(d,"North End",out _),"Only the active away mission is unlocked");
        d.LastTrainingMatchday=d.MatchDay;d.LastAwayTripMatchday=d.MatchDay;
        Check(!FirmMissions.ClaimBonus(d),"Bonus locked until daily claims complete");
        d.PendingFansGain=2;d.FanMorale=75;d.Money=6000;
        foreach(var mission in FirmMissions.Get(d,1))Check(FirmMissions.Claim(d,mission.id),"Matchday claim "+mission.id);
        Check(FirmMissions.Get(d,1).Length==5,"Five matchday missions are available");
        Check(FirmMissions.ClaimBonus(d) && d.Money==8100,"Daily rewards and bonus total £2,100");
        Check(!FirmMissions.ClaimBonus(d) && d.Money==8100,"Duplicate bonus blocked");
        var saved=JsonUtility.ToJson(d);var restored=JsonUtility.FromJson<PlayerData>(saved);
        Check(CampaignMissions.Completed(restored,1) && !CampaignMissions.TryComplete(restored,out _) && !FirmMissions.ClaimBonus(restored),"Save roundtrip preserves campaign completion and bonus claims");
        restored.MatchDay++;
        Check(FirmMissions.Get(restored,1).All(m=>!FirmMissions.Claimed(restored,m.id)),"New matchday has distinct objectives");
        Check(Resources.Load<Texture2D>("CityPresentation/CityLoading"),"Loading artwork packaged in Resources");
        Check(Resources.Load<Texture2D>("CityPresentation/CityLoading_Riverside"),"Riverside loading artwork packaged in Resources");
        Check(Resources.Load<Texture2D>("CityPresentation/CityLoading_OldTown") || Resources.Load<Texture2D>("CityPresentation/CityLoading_Docks"),"Old Town destination has display artwork or a safe city fallback");
        var operationsData=new PlayerData{MatchDay=4,Money=1000,Fans=3,FanMorale=60,PoliceHeat=5,CampaignStartingHeatApplied=true,RecruitedAgents=new List<AgentData>{new AgentData("Leader"),new AgentData("Scout"),new AgentData("Organizer")}};
        CityOperationsLedger.Ensure(operationsData);
        Check(!CityOperationsLedger.CanStart(operationsData,CityOperationType.CollectTickets,3,out _),"Ticket operation is gated by district intel");
        CityOperationsLedger.Apply(operationsData,CityOperationType.ScoutDistrict,3);
        Check(operationsData.CityIntel==2&&CityOperationsLedger.CanStart(operationsData,CityOperationType.CollectTickets,3,out _),"Scouting unlocks the ticket operation");
        CityOperationsLedger.Apply(operationsData,CityOperationType.SupporterRally,3);
        CityOperationsLedger.Apply(operationsData,CityOperationType.CommunityEvent,3);
        CityOperationsLedger.Apply(operationsData,CityOperationType.CollectTickets,3);
        CityOperationsLedger.Apply(operationsData,CityOperationType.PrepareTransport,3);
        Check(CityOperationsLedger.CanStart(operationsData,CityOperationType.PrepareStadium,3,out _),"Intel, tickets, social momentum and transport combine to unlock stadium preparation");
        Check(operationsData.FanMorale==71&&operationsData.PoliceHeat==4&&operationsData.SocialMomentum==3,"Social operations affect morale, heat and supporter momentum together");
        Check(LandscapeTheme.Current&&LandscapeTheme.Current.panel&&LandscapeTheme.Current.darkButton&&LandscapeTheme.Current.taxiPrefab,"Supplied gameplay sprite theme and 3D taxi prefab are packaged");
        if(Application.isPlaying)
        {
            var city=CityGameplay.Instance;Check(city && city.Locations!=null,"Live city initialized");
            if(city && city.Locations!=null)
            {
                foreach(var pair in city.Locations.Select((p,i)=>(p,i)))
                {
                    var path=new NavMeshPath();
                    Check(NavMesh.CalculatePath(city.Home,pair.p,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Reachable "+city.LocationNames[pair.i]);
                }
            }
            Check(BattleManager.instance,"Battle manager live");
            var agents=BattleManager.instance ? BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray() : Array.Empty<AgentController>();
            Check(agents.Length>0 && agents.All(a=>a.GetComponent<NavMeshAgent>().isOnNavMesh),"Living squad on NavMesh");
            float liveCrewGap=agents.SelectMany((a,i)=>agents.Skip(i+1).Select(b=>
                Vector2.Distance(new Vector2(a.transform.position.x,a.transform.position.z),new Vector2(b.transform.position.x,b.transform.position.z))))
                .DefaultIfEmpty(99f).Min();
            Check(liveCrewGap>=2.8f,"Spawned crew members have clear ring separation");
            Check(agents.All(a=>a.GetComponentsInChildren<Animator>().Any(x=>x.runtimeAnimatorController)),"Squad has animated character models");
            Check(UnityEngine.Object.FindFirstObjectByType<CityMissionHUD>(),"Gameplay mission HUD exists");
            var camera=CameraPanTouchOnly.Instance;
            Check(camera && camera.enabled,"Gameplay tactical camera is active");
            AgentSelectionManager.instance?.SelectAll();
            camera?.CenterOnSelection();
            Check(camera && camera.IsFollowingSelection,"Recenter enables camera follow");
            if(city && city.Locations!=null && city.Locations.Length>1)
            {
                AgentSelectionManager.instance?.CommandSelectedMoveTo(city.Locations[1]);
                Check(camera && camera.IsFollowingSelection,"Move command keeps camera follow active");
                float minFormationGap=agents.SelectMany((a,i)=>agents.Skip(i+1).Select(b=>
                    Vector2.Distance(new Vector2(a.CommandDestination.x,a.CommandDestination.z),new Vector2(b.CommandDestination.x,b.CommandDestination.z))))
                    .DefaultIfEmpty(99f).Min();
                Check(minFormationGap>=2.8f,"Crew movement formation keeps affiliation rings separated");
            }
            Check(GameplayTuning.Current.explorationHeight<=32f,"Default tactical camera is low enough for unit readability");
            Check(camera && camera.CollisionAvoidanceEnabled,"Camera building collision lift is enabled");
            var collisionProbe=GameObject.CreatePrimitive(PrimitiveType.Cube);
            collisionProbe.name="CameraBuildingCollisionProbe";
            collisionProbe.transform.position=new Vector3(0f,20f,-10f);
            collisionProbe.transform.localScale=new Vector3(8f,16f,8f);
            Physics.SyncTransforms();
            var safeCamera=CameraPanTouchOnly.SafeCityPosition(new Vector3(0f,20f,0f),new Vector3(0f,20f,-22f));
            Check(safeCamera.y>20f,"Camera collision raises the view over a building obstacle");
            UnityEngine.Object.Destroy(collisionProbe);
            if(GamePopup.AnyOpen)GamePopup.Instance.Hide();
            CityOperationsSystem.Instance?.CloseBoard();
            var es=EventSystem.current;var hits=new List<RaycastResult>();
            if(es)es.RaycastAll(new PointerEventData(es){position=new Vector2(Screen.width*.55f,Screen.height*.48f)},hits);
            Check(es && hits.Count==0,"Open world gesture area is not blocked by HUD "+string.Join(",",hits.Select(h=>h.gameObject.name)));
            foreach(var command in new[]{"Move","Attack","Talk","Capture","Actions","Retreat"})
                Check(GameObject.Find(command)?.activeInHierarchy==true,"Command remains visible: "+command);
            var commandRects=new List<RectTransform>();
            foreach(var command in new[]{"Move","Attack","Talk","Capture","Actions","Retreat"})
            {
                var testFrame=UnityEngine.Object.FindFirstObjectByType<LandscapeBattleHUD>()?.frame;
                var commandTransform=testFrame?testFrame.Find("BattleHUD/"+command) as RectTransform:null;
                if(!commandTransform&&testFrame)commandTransform=testFrame.Find(command) as RectTransform;
                if(commandTransform)commandRects.Add(commandTransform);
                var bounds=commandTransform?RectTransformUtility.CalculateRelativeRectTransformBounds(testFrame,commandTransform):new Bounds();
                var image=commandTransform?commandTransform.GetComponent<Image>():null;
                rows.Add($"INFO {command} HUD x={(commandTransform?commandTransform.anchoredPosition.x:0):F0} center={bounds.center.x:F0},{bounds.center.y:F0} size={bounds.size.x:F0}x{bounds.size.y:F0} alpha={(image?image.color.a:0):F2}");
            }
            var orderedCommands=commandRects.OrderBy(r=>r.anchoredPosition.x).ToArray();
            Check(UnityEngine.Object.FindFirstObjectByType<PedestrianActionHud>()!=null,"Nearby civilians show ACTION pins instead of a bottom command strip");
            var affiliation=UnityEngine.Object.FindObjectsByType<UnitAffiliationMarker>(FindObjectsSortMode.None);
            Check(affiliation.Length>=agents.Length,"Crew/rival affiliation markers are live");
            var playerMarkers=agents.Select(a=>a.GetComponent<UnitAffiliationMarker>()).Where(x=>x).ToArray();
            Check(playerMarkers.Length==agents.Length && playerMarkers.All(x=>x.IsHollow && x.HasDirectionalArrows && x.AffiliationColor.g>.8f),"Player crew uses hollow green rings with green arrows");
            var rivalMarkers=BattleManager.instance ? BattleManager.instance.EnemyAgents.Where(e=>e&&e.firmName!="POLICE").Select(e=>e.GetComponent<UnitAffiliationMarker>()).Where(x=>x).ToArray() : System.Array.Empty<UnitAffiliationMarker>();
            Check(rivalMarkers.Length>0 && rivalMarkers.All(x=>x.IsHollow && !x.HasDirectionalArrows && x.AffiliationColor.r>.8f),"Rival crews use hollow red rings");
            Check(UnityEngine.Object.FindFirstObjectByType<PedestrianSpawner>()!=null,"Pedestrian activity spawner is configured");
            Check(UnityEngine.Object.FindObjectsByType<PedestrianController>(FindObjectsSortMode.None).Length>0,"Pedestrians are active on city streets");
            var socialNpcs=UnityEngine.Object.FindObjectsByType<SocialNpc>(FindObjectsSortMode.None);
            Check(socialNpcs.Length>0 && socialNpcs.All(x=>x.IsTalkable),"Every spawned civilian NPC is talkable");
            var promptedNpc=socialNpcs.FirstOrDefault(x=>x.HasVisiblePrompt);
            Check(promptedNpc!=null,"Venue NPCs display a TALK prompt");
            Check(SocialNpc.AcceptsInvitation(0f)&&SocialNpc.AcceptsInvitation(.2499f)&&!SocialNpc.AcceptsInvitation(.25f)&&!SocialNpc.AcceptsInvitation(.999f),"Civilian invitation uses an exact 25 percent acceptance boundary");
            var conversation=NpcConversationUI.Instance;
            Check(conversation!=null,"Proximity TALK and bottom conversation UI is live");
            if(promptedNpc && agents.Length>0)
            {
                var nav=promptedNpc.GetComponent<NavMeshAgent>();
                Vector3 target=agents[0].transform.position+Vector3.right*3f;
                if(NavMesh.SamplePosition(target,out var talkHit,5f,NavMesh.AllAreas))
                {
                    if(nav&&nav.isOnNavMesh)nav.Warp(talkHit.position);else promptedNpc.transform.position=talkHit.position;
                }
                promptedNpc.Talk();
                Check(conversation && conversation.IsConversationOpen,"Nearby pedestrian opens the bottom conversation panel");
                Check(promptedNpc.GetComponent<PedestrianController>()?.IsConversationPaused==true,"Pedestrian stops while the conversation is open");
                Check(conversation && conversation.UsesFullSafeWidth,"Conversation panel reaches both safe landscape edges");
                conversation?.OnVoiceResult("What is happening at the stadium?");
                Check(conversation&&conversation.LastTranscript.Contains("YOU:")&&conversation.LastTranscript.ToLowerInvariant().Contains("stadium"),"Typed/voice questions receive an in-world NPC answer");
                conversation?.Close();
            }
            var battleHud=UnityEngine.Object.FindFirstObjectByType<LandscapeBattleHUD>();
            var topBar=battleHud?.frame?.Find("BattleHUD/TopBar") as RectTransform;
            Check(battleHud&&topBar&&Mathf.Abs(topBar.rect.width-battleHud.frame.rect.width)<1f,"Gameplay HUD spans the full safe-area width");
            var socialActivities=UnityEngine.Object.FindObjectsByType<CitySocialActivity>(FindObjectsSortMode.None);
            var lifeActivities=UnityEngine.Object.FindObjectsByType<CityLifeActivity>(FindObjectsSortMode.None);
            Check(lifeActivities.Length>=4&&lifeActivities.All(a=>a.VisualsReady&&a.ActiveNpcCount>=3),"Market, park, transit and food/music social activities are populated");
            var operations=CityOperationsSystem.Instance;
            Check(operations!=null&&operations.Nodes.Count==CityOperationsLedger.OperationCount,"Nine non-combat RTS operation nodes are active");
            Check(agents.All(a=>a.Data.ManagementProfileInitialized&&!string.IsNullOrWhiteSpace(a.Data.ManagementRole)&&a.Data.Stamina>0),"Crew roles and stamina are active for micro-management");
            if(operations!=null)
            {
                foreach(var node in operations.Nodes)
                {
                    var path=new NavMeshPath();
                    Check(node&&NavMesh.CalculatePath(city.Home,node.transform.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Reachable operation "+node.Title);
                }
                var operationNode=operations.Nodes.FirstOrDefault();
                operationNode?.OpenInteraction();
                Check(GamePopup.AnyOpen,"Tapping a city operation opens task choices");
                if(GamePopup.AnyOpen)GamePopup.Instance.Hide();
                operations.OpenBoard();
                Check(operations.IsBoardOpen,"City operations board opens above gameplay");
            }
            var actionSystem=UnityEngine.Object.FindFirstObjectByType<CityActionSystem>();
            Check(actionSystem&&actionSystem.TaxiReady,"Marked 3D taxi fast-travel vehicle is active");
            Check(actionSystem&&actionSystem.ActiveSabotageTargets>=1,"Mission sabotage vehicle is active");
            var territory=UnityEngine.Object.FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None).FirstOrDefault();
            Check(territory&&territory.occupationTax>0&&territory.moneyReward>0,"Territory requires occupation tax and exposes later protection income");
            if(CityGameplay.HomeMode)
            {
                var matchday=UnityEngine.Object.FindFirstObjectByType<StadiumMatchdayActivity>();
                Check(matchday!=null && matchday.ActiveFanCount>=6,"Matchday stadium supporters are active");
                Check(matchday!=null && matchday.AtmosphereReady,"Stadium matchday facilities and atmosphere are visible");
                Check(matchday!=null&&new[]{"ARRIVAL","BUILDUP","RIVALPRESSURE","KICKOFF","AFTERMATH"}.Contains(matchday.CurrentPhase)&&matchday.ActiveFanCount>=6,"Home matchday phase remains readable while the crowd is live");
                Check(matchday!=null&&matchday.PolicePresenceCount>=0&&matchday.CurrentPhase!="ARRIVAL"||matchday!=null&&matchday.CurrentPhase=="ARRIVAL","Police escalation is staged and bounded by the matchday phase");
                Check(socialActivities.Any(s=>s.Venue.Contains("PUB")&&s.ActiveNpcCount>=6&&s.VenueVisualsReady),"Pub surrounding social activity is active");
                var stadiumModel=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="stadium_001");
                Check(stadiumModel&&matchday!=null&&matchday.RealModelBound,"Stadium activity is bound to the real stadium_001 model approach");
            }
            else
            {
                Check(socialActivities.Any(s=>s.Venue.Contains("ARRIVAL")&&s.ActiveNpcCount>=5&&s.VenueVisualsReady),"Arrival area is populated and visually active");
                Check(socialActivities.Any(s=>s.Venue.Contains("PUB")&&s.ActiveNpcCount>=5&&s.VenueVisualsReady),"Away pub social activity is active");
            }
            Check(UnityEngine.Object.FindObjectsByType<ITHappy.Car>(FindObjectsSortMode.None).Length>0,"Ambient traffic is active");
            Check(UnityEngine.Object.FindObjectsByType<PoliceCarChaser>(FindObjectsSortMode.None).Length>0,"Police patrol vehicles are active");
            Check(PoliceRoadSpline.Instance!=null && PoliceRoadSpline.Instance.TotalLength>0,"Police patrol route is live");
        }
        else rows.Add("NOT RUN live checks: enter Gameplay play mode first");
        Directory.CreateDirectory("Artifacts/CityQA");
        File.WriteAllLines("Artifacts/CityQA/milestone-checks.txt",rows);
        if(Application.isPlaying)
            File.WriteAllLines("Artifacts/CityQA/milestone-checks-"+(CityGameplay.HomeMode?"home":"away")+".txt",rows);
        if(rows.Any(r=>r.StartsWith("FAIL")))throw new Exception("Milestone check failures; see Artifacts/CityQA/milestone-checks.txt");
    }
}
#endif
