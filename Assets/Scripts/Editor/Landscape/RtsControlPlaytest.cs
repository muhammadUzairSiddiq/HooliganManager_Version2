#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Runs real navigation and command regressions with a disposable campaign save.</summary>
[InitializeOnLoad]
public static class RtsControlPlaytest
{
    const string Key = "HM.RtsControlPlaytest";
    const string Report = "Artifacts/CityQA/rts-controls-playtest.txt";
    const string Save = "Artifacts/CityQA/rts-controls-test-save.json";
    static readonly List<string> rows = new List<string>();
    static double deadline;
    static bool running;
    public static void BeginBatch() { SessionState.SetBool(Key + ".batch",true); Begin(); }

    static RtsControlPlaytest()
    {
        if (SessionState.GetBool(Key, false)) SaveDataInLocal.EditorSaveOverride = Path.GetFullPath(Save);
        EditorApplication.playModeStateChanged += StateChanged;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Hooligan/Run RTS Control Playtest")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run from edit mode; this check starts a disposable campaign.");
        Directory.CreateDirectory("Artifacts/CityQA");
        SessionState.SetString(Key + ".override", SaveDataInLocal.EditorSaveOverride ?? "");
        SessionState.SetBool(Key + ".marker", PlayerPrefs.HasKey(GameCacheBootstrap.InstallMarkerKey));
        // The local first-install bootstrap otherwise deletes all prefs on entering play.
        PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey, 1);
        SaveDataInLocal.EditorSaveOverride = Path.GetFullPath(Save);
        var data = new PlayerData { Money = 10000, Fans = 4, PlayerName = "RTS QA", AcceptedPrivacyPolicy = true };
        data.RecruitedAgents.Add(new AgentData("Brick", hp: 200, speed: 8));
        data.RecruitedAgents.Add(new AgentData("Dex", 1, hp: 200, speed: 8));
        SaveDataInLocal.DataSave(data);
        SessionState.SetBool(Key, true);
        File.WriteAllText(Report, "RUNNING " + DateTime.UtcNow.ToString("O"));
        EditorApplication.isPlaying = true;
    }

    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            rows.Clear(); running = false;
            deadline = EditorApplication.timeSinceStartup + 120;
            CityGameplay.HomeMode = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            SaveDataInLocal.EditorSaveOverride = SessionState.GetString(Key + ".override", "");
            if (!SessionState.GetBool(Key + ".marker", false)) PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);
            if(SessionState.GetBool(Key+".batch",false))
            {
                SessionState.SetBool(Key+".batch",false);
                EditorApplication.Exit(File.ReadAllText(Report).Contains("FAIL ")?1:0);
            }
        }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || running) return;
        var battle = BattleManager.instance;
        if (battle && battle.BattleActive && CityGameplay.Instance && battle.PlayerAgents.Count >= 2 && AgentSelectionManager.instance)
        {
            running = true;
            battle.StartCoroutine(Observe(Run()));
        }
        else if (EditorApplication.timeSinceStartup > deadline) Finish("City did not become ready within 120 seconds.");
    }

    static IEnumerator Observe(IEnumerator test)
    {
        while (true)
        {
            object next = null;
            bool more = false;
            try { more = test.MoveNext(); if (more) next = test.Current; }
            catch (Exception error) { Finish(error.ToString()); yield break; }
            if (!more) { Finish(null); yield break; }
            yield return next;
        }
    }

    static void Check(bool value, string title) => rows.Add((value ? "PASS " : "FAIL ") + title);
    static void Finish(string error)
    {
        if (error != null) rows.Add("FAIL " + error);
        rows.Add($"RESULT {rows.Count(r => r.StartsWith("PASS"))} passed, {rows.Count(r => r.StartsWith("FAIL"))} failed");
        rows.Add("UTC " + DateTime.UtcNow.ToString("O"));
        File.WriteAllLines(Report, rows);
        EditorApplication.isPlaying = false;
    }

    static Vector3 Reachable(AgentController agent, float distance)
    {
        var nav = agent.GetComponent<NavMeshAgent>();
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8;
            var guess = agent.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(guess, out var hit, 3, nav.areaMask) && nav.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                return hit.position;
        }
        throw new InvalidOperationException("No reachable test street near crew.");
    }

    static void Scan(AgentController agent)
    {
        typeof(AgentController).GetField("_scanTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(agent, 1f);
    }
    static float SkeletonLength(Animator rig)
    {
        var bones=new[]{HumanBodyBones.Head,HumanBodyBones.Neck,HumanBodyBones.Chest,HumanBodyBones.Spine,HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes};
        float length=.18f;Transform previous=null;
        foreach(var bone in bones){var next=rig.GetBoneTransform(bone);if(!next)continue;if(previous)length+=Vector3.Distance(previous.position,next.position);previous=next;}
        return length;
    }

    static IEnumerator Run()
    {
        var battle = BattleManager.instance;
        var selection = AgentSelectionManager.instance;
        var brick = battle.PlayerAgents[0];
        var dex = battle.PlayerAgents[1];
        var models=RoleCharacterModels.Current;
        yield return null;
        var top=UnityEngine.Object.FindObjectsByType<GameplayHudSideToggle>(FindObjectsSortMode.None).FirstOrDefault(t=>t.hideInitially);
        Check(top&&top.label.text=="PANEL","Top controls start collapsed behind PANEL");
        if(top){top.Toggle();Check(top.label.text=="X","PANEL opens with a close cross");top.Toggle();}
        var overview=UnityEngine.Object.FindFirstObjectByType<CityStrategyOverview>();
        Check(overview&&overview.OverlayOrder==32767,"Full-screen HEATMAP has priority over all gameplay canvases");
        var selectButton=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b=>b.name=="BoxSelect");
        var deselectButton=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b=>b.name=="Deselect");
        var dragButton=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b=>b.name=="DragSelect");
        selectButton.onClick.Invoke();Check(selection.SelectedAgents.Count==battle.PlayerAgents.Count(a=>a&&a.IsAlive),"Squad SELECT ALL selects the entire live crew");
        dragButton.onClick.Invoke();
        Check(RtsGestureController.Instance.Mode==RtsGestureController.InputMode.BoxSelect,"DRAG enables rectangle selection");
        deselectButton.onClick.Invoke();Check(selection.SelectedAgents.Count==0&&RtsGestureController.Instance.Mode==RtsGestureController.InputMode.Camera,"DESELECT ALL clears crew and returns drag to camera mode");
        var data=GameManager.Data;RivalGrowthSystem.Ensure(data);
        int priorMoney=data.Money,priorSupport=data.StrategySupporters,priorCampaigns=data.SupportCampaigns;
        int campaignPrice=CitySupportNetwork.CampaignPrice(data);
        Check(CitySupportNetwork.Promote()&&data.Money==priorMoney-campaignPrice&&data.StrategySupporters==priorSupport+30,"Promotion charges once and increases persistent support");
        data.StrategySupporters=CitySupportNetwork.Capacity(data);int fullCash=data.Money;
        Check(!CitySupportNetwork.Promote()&&data.Money==fullCash,"Support capacity prevents payment for no reward");
        data.StrategySupporters=priorSupport;data.Money=priorMoney;data.SupportCampaigns=priorCampaigns;
        if(overview){overview.Toggle();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Artifacts/CityQA/rts-strategy-bubbles.png");yield return null;overview.Toggle();}
        Check(models&&models.civilians&&models.gangs&&models.police,"All three supplied role models are registered");
        Check(brick.GetComponentInChildren<Animator>().avatar.isHuman,"Crew uses a valid Mixamo humanoid avatar");
        StreetCombatPresentation.LeaveBody(brick.transform);
        var corpse=UnityEngine.Object.FindObjectsByType<BakedBodyLifetime>(FindObjectsSortMode.None).Last();
        var rig=brick.GetComponentInChildren<Animator>();
        float skeletonHeight=SkeletonLength(rig);
        var corpseBounds=corpse.GetComponentInChildren<MeshRenderer>().bounds;
        Check(corpseBounds.size.y<skeletonHeight*1.15f&&corpseBounds.size.y>skeletonHeight*.65f,$"Corpse size agrees with independent skeleton dimensions ({corpseBounds.size.y:F2}m / {skeletonHeight:F2}m)");
        Check(Mathf.Abs(corpseBounds.min.y-brick.transform.position.y)<.15f,"Corpse rests on the ground instead of floating");
        UnityEngine.Object.Destroy(corpse.gameObject);
        foreach (var enemy in battle.EnemyAgents) if (enemy && enemy.IsAlive) enemy.SetCinematicIdle(true);
        brick.SetCinematicIdle(false); dex.SetCinematicIdle(false);
        selection.DeselectAll(); selection.ToggleSelect(brick);
        var cards = BattleUIController.instance.portraitStrip.GetComponentsInChildren<AgentPortraitCard>();
        var pointA = Reachable(brick, 5);
        selection.CommandSelectedMoveTo(pointA);
        Check(brick.IsSelected && selection.SelectedAgents.Count == 1, "Brick stays selected immediately after MOVE");
        float until = Time.time + 12;
        while (brick.CurrentState != AgentController.State.Idle && Time.time < until) yield return null;
        Check(brick.CurrentState == AgentController.State.Idle && Vector3.Distance(brick.transform.position, pointA) < 1, "Brick arrives and completes MOVE at navigation stopping distance");
        var pointB = Reachable(brick, 5);
        selection.CommandSelectedGround(pointB);
        Check(brick.IsSelected && Vector3.Distance(brick.CommandDestination, pointB) < .5f, "Next ground order redirects Brick without selecting again");
        selection.ToggleSelect(dex);
        selection.CommandSelectedMoveTo(Reachable(brick, 6));
        Check(brick.IsSelected && dex.IsSelected && selection.SelectedAgents.Count == 2, "Brick + Dex keep their group selection after MOVE");
        Check(dex.CurrentState == AgentController.State.MovingToPoint, "Dex receives the group movement order");
        selection.ToggleSelect(brick);
        Check(!brick.IsSelected && dex.IsSelected && selection.SelectedAgents.Count == 1, "Tapping Brick removes only Brick");
        var brickOrder = brick.CommandDestination;
        selection.CommandSelectedGround(Reachable(dex, 5));
        Check(brick.CommandDestination == brickOrder, "An order for Dex does not redirect deselected Brick");
        Check(cards.SequenceEqual(BattleUIController.instance.portraitStrip.GetComponentsInChildren<AgentPortraitCard>()), "Selection reuses the same squad portraits");
        selection.ArmGuardCommand(); selection.CommandSelectedGround(Reachable(dex, 3));
        Check(dex.CurrentOrder == AgentController.StandingOrder.Guard && dex.IsSelected, "GUARD is assigned without losing selection");
        selection.CommandSelectedGround(Reachable(dex, 4));
        Check(dex.CurrentOrder == AgentController.StandingOrder.None, "A subsequent ground MOVE overrides GUARD");
        Vector3 patrolStart = dex.transform.position;
        selection.ArmPatrolCommand(); selection.CommandSelectedGround(Reachable(dex, 5));
        Check(dex.CurrentOrder == AgentController.StandingOrder.Patrol && dex.IsSelected, "PATROL starts without a separate task mode");
        until = Time.time + 12;
        while (Vector3.Distance(dex.CommandDestination, patrolStart) > 1 && Time.time < until) yield return null;
        Check(Vector3.Distance(dex.CommandDestination, patrolStart) < 1, "PATROL reverses at its endpoint automatically");

        var rival = battle.EnemyAgents.First(e => e && e.IsAlive && e.firmName != "POLICE" && !e.IsAmbientMatchdayUnit);
        var rivalNav = rival.GetComponent<NavMeshAgent>();
        var oldPosition = rival.transform.position;
        rivalNav.Warp(Reachable(dex, 4)); rival.SetCinematicIdle(true);
        Scan(dex); yield return null; yield return null;
        Check(dex.CurrentState == AgentController.State.AutoAttacking && dex.CurrentOrder == AgentController.StandingOrder.Patrol, "Patrol automatically engages a nearby rival intrusion and retains route");
        rivalNav.Warp(oldPosition);
        yield return null; yield return null;
        Check(dex.CurrentState == AgentController.State.MovingToPoint && dex.CurrentOrder == AgentController.StandingOrder.Patrol, "Patrol resumes when the threat leaves the protected route");

        selection.ArmGuardCommand(); selection.CommandSelectedGround(dex.transform.position);
        rivalNav.Warp(Reachable(dex, 3));
        dex.TakeDamage(1, rival);
        Check(dex.CurrentState == AgentController.State.AutoAttacking && dex.CurrentOrder == AgentController.StandingOrder.Guard, "Guard immediately defends against the actual attacker");
        rivalNav.Warp(oldPosition); yield return null; yield return null;
        Check(dex.CurrentOrder == AgentController.StandingOrder.Guard && dex.CurrentState != AgentController.State.AutoAttacking, "Guard stops pursuit and returns to its area");

        string firm = rival.firmName;
        rival.firmName = "POLICE"; rival.isHostile = false;
        rivalNav.Warp(Reachable(dex, 3)); Scan(dex); yield return null; yield return null;
        Check(dex.CurrentState != AgentController.State.AutoAttacking, "Guard leaves neutral police alone");
        rival.isHostile = true; Scan(dex); yield return null; yield return null;
        Check(dex.CurrentState == AgentController.State.AutoAttacking && dex.CurrentOrder == AgentController.StandingOrder.Guard, "Guard reacts automatically to nearby hostile police");
        rival.firmName = firm; rivalNav.Warp(oldPosition);
        yield return null; yield return null;
        BattleUIController.instance.attackBtn.onClick.Invoke();
        Check(selection.PendingCommand == AgentSelectionManager.TargetCommand.Attack, "ATTACK waits for an explicit target");
        Check(BattleUIController.instance.selectedUnitsLabel.text.Contains("TAP A RIVAL"), "ATTACK button explains target selection instead of claiming the order already happened");
        selection.CommandSelectedAttackTarget(rival);
        Check(dex.CurrentOrder == AgentController.StandingOrder.None && dex.CurrentState == AgentController.State.AutoAttacking && dex.IsSelected, "Targeted ATTACK replaces guard and retains selection");
        selection.CommandSelectedMoveTo(Reachable(dex, 5));
        Check(dex.CurrentState == AgentController.State.MovingToPoint && dex.IsSelected, "Manual MOVE immediately replaces ATTACK");
        int quoted=RivalSettlement.Price(firm),money=GameManager.Data.Money;
        Check(RivalSettlement.Pay(firm,quoted)&&GameManager.Data.Money==money-quoted,"PAY charges the quoted settlement once");
        Check(battle.EnemyAgents.Where(e=>e&&e.firmName==firm).All(e=>!e.isHostile),"PAY stops every member of the rival firm");
        Check(!RivalSettlement.Pay(firm,quoted)&&GameManager.Data.Money==money-quoted,"Repeated PAY cannot charge twice");
        yield return new WaitForSeconds(1.5f);
        Check(battle.EnemyAgents.Where(e=>e&&e.firmName==firm).All(e=>!e.isHostile),"Settled rivals do not resume chasing");
        RivalSettlement.Break(firm);
        selection.CommandSelectedMoveTo(Reachable(dex,5));
        dex.TakeDamage(1, rival);
        Check(dex.CurrentState == AgentController.State.MovingToPoint, "Taking another hit does not cancel a manual withdrawal MOVE");
        var destination = dex.CommandDestination;
        Check(!dex.TryCommandMoveTo(new Vector3(99999, 99999, 99999)) && dex.CommandDestination == destination, "Unreachable MOVE preserves the last valid order");
        selection.ArmGuardCommand(); selection.CommandSelectedGround(dex.transform.position);
        var guardDestination = dex.CommandDestination;
        foreach (var member in battle.EnemyAgents.Where(e => e && e.firmName == rival.firmName).ToArray())
            typeof(EnemyController).GetProperty("CurrentHp").SetValue(member, 0f);
        if (GamePopup.AnyOpen) GamePopup.Instance.Hide();
        battle.OnEnemyDied(rival);
        Check(dex.CurrentOrder == AgentController.StandingOrder.Guard && dex.CommandDestination == guardDestination, "Another gang's defeat preserves an unrelated guard order");
        Check(!GamePopup.AnyOpen, "Gang defeat does not open a routine result popup");
        dex.SetActivityLocked(true, dex.transform.position);
        Check(!dex.TryCommandMoveTo(Reachable(dex, 3)), "Activity-locked crew reject MOVE instead of reporting success");
        dex.SetActivityLocked(false, dex.transform.position);

        var hud = UnityEngine.Object.FindFirstObjectByType<LandscapeBattleHUD>();
        yield return new WaitForSeconds(.6f);
        foreach (string name in new[] { "Move", "Attack", "Guard", "Patrol" })
            Check(!hud.frame.Find("BattleHUD/" + name).gameObject.activeInHierarchy, name + " removed from bottom command strip");
        var gestures=RtsGestureController.Instance;
        Check(gestures, "World gesture owner exists");
        gestures.BeginBoxSelection();
        Check(RtsGestureController.BlocksCamera, "Box selection prevents camera drag");
        gestures.Cancel();
        gestures.BeginGuard();
        Check(gestures.Mode==RtsGestureController.InputMode.GuardArea&&RtsGestureController.BlocksCamera,"Guard area owns drag exclusively");
        gestures.Cancel();
        var guardArea=new Bounds(Reachable(dex,4),new Vector3(12,8,12));
        dex.SetCinematicIdle(false);
        dex.SetActivityLocked(false,dex.transform.position);
        Check(dex.CommandGuard(guardArea)&&dex.GuardRegion==guardArea,"Guard rectangle reaches the agent duty system");
        Check(dex.CommandPatrol(dex.transform.position,Reachable(dex,6)),"Explicit A/B patrol validates connected route");
        Check(!dex.CommandPatrol(dex.transform.position,new Vector3(99999,99999,99999)),"Invalid patrol endpoint rejected");
        var crowd=Resources.Load<CrowdPoseLibrary>("FloreswaCrowd/Poses");
        Check(crowd&&crowd.variants.Length==10&&crowd.variants[9].name=="Dwarf Walk","Crowds use nine supporter gestures and the requested Dwarf Walk");
        Check(RtsBuildingVisibility.Instance,"City building visibility active");
        var stadium = UnityEngine.Object.FindFirstObjectByType<StadiumMatchdayActivity>();
        if (GamePopup.AnyOpen) GamePopup.Instance.Hide();
        stadium.OpenBriefing();
        Check(!GamePopup.AnyOpen && stadium.GetComponent<WorldChoiceBar>()?.IsOpen == true && Time.timeScale > 0, "Matchday choices open locally without pausing or a modal");
        stadium.GetComponent<WorldChoiceBar>()?.Hide();
        Check(!AgentSelectionManager.BlocksWorldTap(), "Closing the matchday choice leaves world commands available");
        var police = LivePoliceSystem.Instance;
        typeof(LivePoliceSystem).GetMethod("ShowPopup", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(police, null);
        var policeAnchor = (GameObject)typeof(LivePoliceSystem).GetField("localPoliceChoice", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(police);
        var policeBubble = policeAnchor.GetComponentInChildren<WorldInteractBubble>(true);
        Check(policeBubble && policeBubble.IsLive && !policeBubble.gameObject.activeSelf && !GamePopup.AnyOpen, "Police intermediary dot is hidden");
        Check(policeAnchor.GetComponent<WorldChoiceBar>().IsOpen && !GamePopup.AnyOpen && Time.timeScale > 0, "Police choices open immediately and leave simulation running");
        policeAnchor.GetComponent<WorldChoiceBar>().Hide();
        Check(stadium.SimulatedGroupCount >= 7, "Independent home, rival and police matchday groups exist");
        CameraPanTouchOnly.Instance?.FocusOn(dex.transform.position);
        yield return new WaitForSeconds(1);
        ScreenCapture.CaptureScreenshot("Artifacts/CityQA/rts-controls-hud.png");
        yield return null;
        foreach(var choice in UnityEngine.Object.FindObjectsByType<WorldChoiceBar>(FindObjectsSortMode.None))choice.Hide();
        selection.DeselectAll();selection.Select(dex);dex.SetCinematicIdle(false);dex.SetActivityLocked(false,dex.transform.position);
        var gestureType=typeof(RtsGestureController);
        var begin=gestureType.GetMethod("Begin",BindingFlags.NonPublic|BindingFlags.Instance);
        var track=gestureType.GetMethod("Track",BindingFlags.NonPublic|BindingFlags.Instance);
        var end=gestureType.GetMethod("End",BindingFlags.NonPublic|BindingFlags.Instance);
        Vector2 press=Camera.main.WorldToScreenPoint(Reachable(dex,5));
        gestures.Cancel();begin.Invoke(gestures,new object[]{press});
        gestureType.GetField("began",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(gestures,Time.unscaledTime-1);
        track.Invoke(gestures,new object[]{press});end.Invoke(gestures,new object[]{press,false});
        Check(gestures.Mode==RtsGestureController.InputMode.Menu,"Stationary hold opens the direct command menu and survives release");
        gestures.Cancel();begin.Invoke(gestures,new object[]{press});
        track.Invoke(gestures,new object[]{press+Vector2.right*50});
        gestureType.GetField("began",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(gestures,Time.unscaledTime-1);
        track.Invoke(gestures,new object[]{press+Vector2.right*50});end.Invoke(gestures,new object[]{press+Vector2.right*50,false});
        Check(gestures.Mode==RtsGestureController.InputMode.Camera,"A camera drag cannot become a long-press command");
        var occluder=GameObject.CreatePrimitive(PrimitiveType.Cube);occluder.name="QA occluding building";
        occluder.transform.position=Vector3.Lerp(Camera.main.transform.position,dex.transform.position+Vector3.up*2,.65f);occluder.transform.localScale=Vector3.one*8;
        var renderer=occluder.GetComponent<Renderer>();
        var buildings=(List<Renderer>)typeof(RtsBuildingVisibility).GetField("buildings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(RtsBuildingVisibility.Instance);buildings.Add(renderer);
        Check(buildings.Count>1,"Real city buildings are registered for camera occlusion");
        var rails=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name.ToLowerInvariant().StartsWith("railway_")).ToArray();
        Check(rails.Length>0&&rails.All(r=>!r.enabled),"Legacy elevated railway decks and columns are disabled");
        Check(UnityEngine.Object.FindFirstObjectByType<GroundRailTrack>(),"Flat ground railway replaces the removed viaduct");
        yield return new WaitForSeconds(.2f);Check(renderer.forceRenderingOff,"Building hiding exposes units along the camera ray");
        occluder.transform.position+=Vector3.right*1000;yield return new WaitForSeconds(.2f);
        Check(!renderer.forceRenderingOff,"Occluder restores when it no longer hides units");UnityEngine.Object.Destroy(occluder);
        CityServiceChecks.Run();Check(!File.ReadAllText("Artifacts/CityQA/service-checks.txt").Contains("FAIL"),"Home services and recruitment persist through the save serializer");
        foreach(var enemy in battle.EnemyAgents)if(enemy)enemy.SetCinematicIdle(true);
        brick.SetCinematicIdle(false);dex.SetCinematicIdle(false);brick.SetActivityLocked(false,brick.transform.position);dex.SetActivityLocked(false,dex.transform.position);
        var area=new Bounds(Reachable(brick,4),new Vector3(12,8,12));
        if(brick.CommandGuard(area))RtsDutyBoundary.Create(area,new List<AgentController>{brick});
        Vector3 a=dex.transform.position,b=Reachable(dex,12);
        if(dex.CommandPatrol(a,b))RtsOrderVisual.Patrol(a,b,new[]{dex});
        CameraPanTouchOnly.Instance.FocusOn((area.center+a+b)/3);
        yield return new WaitForSeconds(1f);
        ScreenCapture.CaptureScreenshot("Artifacts/CityQA/rts-order-markers.png");yield return null;
        var stadiumPoint=CityGameplay.Instance.Locations[5];CameraPanTouchOnly.Instance.FocusOn(stadiumPoint);
        yield return new WaitForSeconds(12f);
        var crowdRenderer=UnityEngine.Object.FindFirstObjectByType<StadiumCrowdRenderer>();
        Check(crowdRenderer&&crowdRenderer.Population>=450&&crowdRenderer.Population<=500,"Supporter population is halved to a 500-person cap");
        Check(crowdRenderer.GroupSizesValid&&crowdRenderer.MovingGroupCount>crowdRenderer.GroupCount*.7f,"Supporters form groups of 3–15 and most groups move");
        var kits=(Material[])typeof(StadiumCrowdRenderer).GetField("kits",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(crowdRenderer);
        Check(kits.Select(m=>m.GetColor("_BaseColor")).Distinct().Count()==4,"Instanced spectators render four distinct faction shirt colours");
        ScreenCapture.CaptureScreenshot("Artifacts/CityQA/rts-stadium-crowd.png");yield return null;
        if(overview)
        {
            overview.Toggle();yield return new WaitForSeconds(.6f);
            int beforeSupport=data.StrategySupporters;CitySupportNetwork.Earn(data,4);
            yield return new WaitForSeconds(.6f);
            Check(UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None).Any(t=>t.name=="LiveDelta"&&t.text.Contains("+4")),"Heatmap displays live supporter gains on the faction bubble");
            ScreenCapture.CaptureScreenshot("Artifacts/CityQA/rts-heatmap.png");yield return null;
            for(int lost=0;lost<4;lost++)CitySupportNetwork.Lost(data.FirmName,true);
            yield return new WaitForSeconds(.6f);
            Check(UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None).Any(t=>t.name=="LiveDelta"&&t.text.Contains("-4")),"Heatmap displays live red supporter losses");
            overview.Toggle();
        }
        typeof(StadiumMatchdayActivity).GetMethod("BeginConcurrentFlashpoints",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(stadium,new object[]{3});
        Check(stadium.ActiveConflictCount>=2,"Multiple independent supporter flashpoints can run together");
        Check(RecruitPackages.All[0].MinFans==8&&RecruitPackages.All[3].MaxFans==32,"Recruitment packages offer four times the previous quantities");
        var map=LiveMiniMap.Instance;
        typeof(LiveMiniMap).GetMethod("Expand",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(map,null);
        yield return null;
        Check(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Toggle>(FindObjectsSortMode.None).Count(t=>t.transform.parent.name=="Map layers")==6,"Expanded minimap exposes six checkmarked visibility layers");
        ScreenCapture.CaptureScreenshot("Artifacts/CityQA/rts-map-layers.png");yield return null;
        typeof(LiveMiniMap).GetMethod("Collapse",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(map,null);
        foreach(var member in battle.PlayerAgents)if(member){member.StopAllCoroutines();member.SetCinematicIdle(true);member.enabled=false;}
        stadium.enabled=false;
        LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;
        foreach(var march in UnityEngine.Object.FindObjectsByType<MatchdayMarch>(FindObjectsSortMode.None))march.enabled=false;
        foreach(var enemy in battle.EnemyAgents)if(enemy){enemy.StopAllCoroutines();enemy.SetCinematicIdle(true);enemy.enabled=false;}
        var victims=battle.EnemyAgents.Where(e=>e&&e.IsAlive).GroupBy(e=>e.GetComponentInChildren<Animator>().avatar.GetInstanceID()).Select(g=>g.First()).Take(3).ToArray();
        foreach(var victim in victims)
        {
            victim.SetCinematicIdle(true);
            CameraPanTouchOnly.Instance.FocusOn(victim.transform.position);
            yield return new WaitForSeconds(.3f);
            var animation=victim.GetComponentInChildren<Animator>();
            float height=SkeletonLength(animation);
            var oldBodies=new HashSet<BakedBodyLifetime>(UnityEngine.Object.FindObjectsByType<BakedBodyLifetime>(FindObjectsSortMode.None));
            typeof(LivePoliceSystem).GetField("_heat",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(LivePoliceSystem.Instance,0);
            GameManager.Data.PoliceHeat=0;
            int heatBefore=GameManager.Data.PoliceHeat;
            var npcAttacker=battle.EnemyAgents.First(e=>e&&e!=victim&&e.IsAlive);
            victim.TakeDamage(99999,npcAttacker);
            yield return new WaitForSeconds(victim.GetComponentInChildren<CharacterVisualProfile>().deathSeconds+.15f);
            var fallen=UnityEngine.Object.FindObjectsByType<BakedBodyLifetime>(FindObjectsSortMode.None).FirstOrDefault(c=>!oldBodies.Contains(c));
            if(fallen)
            {
                var bounds=fallen.GetComponentInChildren<MeshRenderer>().bounds;
                float span=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                Check(span<height*1.15f&&bounds.size.y<height*.7f,$"Real death preserves body proportions and lies down ({span:F2}m, skeleton chain {height:F2}m)");
                Check(Mathf.Abs(bounds.min.y-victim.transform.position.y)<.2f,"Real fallen body remains grounded");
            }
            else Check(false,"Real death creates a visible resting body");
            Check(GameManager.Data.PoliceHeat<=heatBefore,"NPC-inflicted death cannot increase player police heat");
            ScreenCapture.CaptureScreenshot("Artifacts/CityQA/rts-death-"+animation.avatar.name+".png");yield return null;
        }
        Check(victims.Length==3,"Real death transitions exercised all three role models");
        stadium.enabled=true;
        foreach(var enemy in battle.EnemyAgents)if(enemy)enemy.enabled=true;
        for(int visit=0;visit<6;visit++)
        {
            CameraPanTouchOnly.Instance.FocusOn(CityGameplay.Instance.Locations[visit%CityGameplay.Instance.Locations.Length]);
            yield return new WaitForSeconds(1.5f);
            Check(CityActivityStreaming.Instance.LiveAmbientBodies<=CityActivityStreaming.BodyBudget,"Camera preloading respects the actor cap on district visit "+visit);
        }
        Check(RivalBodyPool.PooledCount<=(Application.isMobilePlatform?36:48),"Returning districts reuse a bounded role-specific actor pool");
    }
}
#endif

