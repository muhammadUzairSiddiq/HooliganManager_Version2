using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Compact city HUD — status, squad, objectives, and contextual commands only.</summary>
public sealed class CityMissionHUD : MonoBehaviour
{
    RectTransform frame, content;
    GameObject missionPanel;
    TextMeshProUGUI resources, objectives, events, missionHeading, missionBriefing;
    TextMeshProUGUI[] statusValues = new TextMeshProUGUI[5];
    Button captureButton, retreatButton, missionButton, talkButton, actionsButton;
    GameObject moveGo, attackGo, authoredRetreat;
    GameplayHudSideToggle sideToggle;
    float nextRefresh;
    string lastState;
    const float CaptureRadius = 14f;
    static Sprite[] statusIcons;

    public void Build(RectTransform root)
    {
        frame = root;
        LoadStatusIcons();
        var hud = frame.Find("BattleHUD");
        if (hud)
        {
            Place(hud, "TopBar", 10, 8, 1580, 126);
            Place(hud, "SquadRail", 16, 150, 286, 334);
            Place(hud, "SquadTitle", 30, 160, 258, 26);
            Place(hud, "MemberCount", 30, 190, 258, 22);
            Place(hud, "PortraitScroll", 24, 220, 268, 252);

            // Firm name — white so every HUD label stays readable on the dark bar.
            Place(hud, "Heading", 24, 17, 310, 36);
            var heading = hud.Find("Heading")?.GetComponent<TextMeshProUGUI>();
            if (heading)
            {
                heading.text = GameManager.Data?.FirmName.ToUpperInvariant() ?? "YOUR FIRM";
                heading.fontSize = 29;
                heading.color = Color.white;
                heading.fontStyle = FontStyles.Bold;
            }

            var squadTitle = hud.Find("SquadTitle")?.GetComponent<TextMeshProUGUI>();
            if (squadTitle) { squadTitle.text = "SQUAD"; squadTitle.fontSize = 18; squadTitle.color = Color.white; }

            // Restore classic cash / reputation.
            Place(hud, "Cash", 1232, 18, 172, 32);
            Place(hud, "Reputation", 1228, 52, 176, 22);
            Place(hud, "Pause", 1410, 16, 168, 60);
            var cash = hud.Find("Cash")?.GetComponent<TextMeshProUGUI>();
            if (cash)
            {
                cash.gameObject.SetActive(true);
                cash.color = Color.white;
                cash.fontSize = 26;
                cash.alignment = TextAlignmentOptions.Right;
            }
            var rep = hud.Find("Reputation")?.GetComponent<TextMeshProUGUI>();
            if (rep)
            {
                rep.gameObject.SetActive(true);
                rep.color = Color.white;
                rep.fontSize = 16;
                rep.alignment = TextAlignmentOptions.Right;
            }

            foreach (var name in new[] { "Timer", "Round", "Controls" })
                if (hud.Find(name)) hud.Find(name).gameObject.SetActive(false);

            Place(hud, "SelectionStatus", 420, 770, 760, 30);
            var status = hud.Find("SelectionStatus")?.GetComponent<TextMeshProUGUI>();
            if (status)
            {
                status.fontSize = 18;
                status.alignment = TextAlignmentOptions.Center;
                status.color = LandscapeUI.White;
                status.richText = true;
                status.text = "";
            }

            moveGo = hud.Find("Move")?.gameObject;
            attackGo = hud.Find("Attack")?.gameObject;
            if (moveGo) moveGo.SetActive(false);
            if (attackGo) attackGo.SetActive(false);

            authoredRetreat = hud.Find("Retreat")?.gameObject;
            if (authoredRetreat) authoredRetreat.SetActive(false);

            Place(hud, "EnemyCount", 1300, 150, 274, 28);
            Place(hud, "ObjectiveSlot", 1300, 184, 274, 128);
            Place(hud, "HeatSlot", 1300, 322, 274, 50);
            if (hud.Find("Joystick")) hud.Find("Joystick").gameObject.SetActive(false);
        }

        BuildStatusStrip();
        BuildTopNavigation(hud);

        // Objectives under squad.
        var objectivesCard = LandscapeUI.Panel("ObjectivesCard", frame, 16, 500, 286, 148, true);
        var objectivesTitle = LandscapeUI.Text("ObjectivesTitle", frame, "OBJECTIVES", 30, 510, 258, 24, 18, LandscapeUI.Gold, true);
        objectives = LandscapeUI.Text("LiveObjectives", frame, "", 30, 540, 258, 96, 15, LandscapeUI.White);
        missionButton = LandscapeUI.Button("MissionLedger", frame, "MISSION PLAN", 22, 658, 274, 52);
        missionButton.onClick.AddListener(() => { missionPanel.SetActive(true); RefreshMissions(); });

        retreatButton = LandscapeUI.Button("LeftRetreat", frame, "RETREAT", 22, 716, 274, 48, "red");
        retreatButton.onClick.AddListener(() =>
        {
            AgentSelectionManager.instance?.CommandSelectedRetreat();
            BattleUIController.instance?.ShowAlert(
                CityGameplay.HomeMode ? "REGROUPING AT HEADQUARTERS" : "RETREAT ORDER CONFIRMED", 1.4f);
        });

        var eventCard = LandscapeUI.Panel("EventCard", frame, 16, 772, 286, 76, true);
        var eventTitle = LandscapeUI.Text("EventTitle", frame, "LIVE FEED", 30, 780, 258, 20, 14, LandscapeUI.Gold, true);
        events = LandscapeUI.Text("LiveEvents", frame, "", 30, 802, 258, 38, 14, LandscapeUI.White);

        BuildLeftPanelToggle(hud,
            objectivesCard.gameObject, objectivesTitle.gameObject, objectives.gameObject,
            missionButton.gameObject, retreatButton.gameObject,
            eventCard.gameObject, eventTitle.gameObject, events.gameObject);

        talkButton=LandscapeUI.Button("Talk",hud?hud:frame,"TALK",645,812,130,54,"dark");
        talkButton.gameObject.SetActive(false);
        captureButton = LandscapeUI.Button("Capture", hud ? hud : frame, "CAPTURE", 785, 812, 130, 54, "green");
        captureButton.gameObject.SetActive(false);
        actionsButton=LandscapeUI.Button("Actions",hud?hud:frame,"ACTIONS",925,812,130,54,"outline");
        actionsButton.gameObject.SetActive(false);

        missionPanel = LandscapeUI.Panel("CityMissionLedger", frame, 310, 125, 980, 660, true).gameObject;
        missionHeading=LandscapeUI.Text("Title", missionPanel.transform, "MISSION PLAN", 22, 15, 700, 38, 28, null, true);
        LandscapeUI.Button("Close", missionPanel.transform, "CLOSE", 755, 14, 135, 42)
            .onClick.AddListener(() => missionPanel.SetActive(false));
        missionBriefing=LandscapeUI.Text("Briefing",missionPanel.transform,"",22,62,936,58,16,LandscapeUI.Muted);
        content = LandscapeUI.Scroll("MissionScroll", missionPanel.transform, 16, 126, 948, 514).content;
        missionPanel.SetActive(false);
        BuildTopPanelToggle(hud);
        BuildRightPanelToggle(hud);
        Style(frame);
        BattleUIController.instance?.SyncPauseButtonIcon(BattleManager.instance?.IsPaused ?? false);
        FindFirstObjectByType<LandscapeBattleHUD>()?.RefreshAuthoredLayout();
    }

    void BuildStatusStrip()
    {
        var strip = LandscapeUI.Panel("StatusStrip", frame, 22, 75, 902, 34, false);
        strip.color = Color.white;
        string[] labels={"CREW","REPUTATION","POLICE HEAT","MISSION","INTEL"};
        Color[] colors={LandscapeUI.Green,LandscapeUI.Gold,new Color(1f,.48f,.25f),new Color(.22f,.90f,.88f),new Color(.45f,.78f,1f)};
        for(int i=0;i<labels.Length;i++)
        {
            float x=8+i*178;
            if(i>0)LandscapeUI.Image("Divider"+i,strip.transform,x-5,5,1,27,null,new Color(.22f,.72f,.75f,.32f));
            if(statusIcons!=null&&statusIcons.Length>i)
            {
                var icon=LandscapeUI.Image("Icon"+i,strip.transform,x+4,7,22,22,statusIcons[i],colors[i],true);icon.preserveAspect=true;
            }
            LandscapeUI.Text("Label"+i,strip.transform,labels[i],x+32,3,138,13,10,LandscapeUI.Muted,true);
            statusValues[i]=LandscapeUI.Text("Value"+i,strip.transform,"--",x+32,16,138,19,16,colors[i],true);
        }
        var resourceBand=LandscapeUI.Panel("ResourceBand",frame,22,112,902,22,false);
        resourceBand.color=new Color(.02f,.06f,.08f,.96f);
        resources=LandscapeUI.Text("LiveResources",resourceBand.transform,"",12,1,878,20,15,LandscapeUI.White,true,TextAlignmentOptions.Center);
    }

    void BuildTopNavigation(Transform hud)
    {
        if(hud)
        {
            var home=hud.Find("../CityDistrict") as RectTransform;
            if(!home)home=frame.Find("CityDistrict") as RectTransform;
            if(home){LandscapeUI.Place(home,350,22,130,50);AddIcon(home.GetComponent<Button>(),LandscapeTheme.Current?.shield);}
        }
        var mission=LandscapeUI.Button("TopMission",frame,"MISSION",646,22,150,50,"dark");
        mission.onClick.AddListener(()=>{missionPanel.SetActive(true);RefreshMissions();});
        var intel=LandscapeUI.Button("TopIntel",frame,"INTEL",804,22,120,50,"dark");
        intel.onClick.AddListener(()=>CityGameplay.Instance?.ShowIntelReport());
        var vehicles=LandscapeUI.Button("TopVehicles",frame,"VEHICLES",932,22,140,50,"dark");
        vehicles.onClick.AddListener(()=>CityActionSystem.Instance?.FocusNearestTaxi());
        var settings=LandscapeUI.Button("TopSettings",frame,"SETTINGS",1080,22,140,50,"dark");
        settings.onClick.AddListener(()=>CityGameplay.Instance?.ToggleCameraSettings());
        AddIcon(mission,LandscapeTheme.Current?.star);
        AddIcon(intel,LandscapeTheme.Current?.medkit);
        AddIcon(vehicles,LandscapeTheme.Current?.energy);
        AddIcon(settings,LandscapeTheme.Current?.settings);
    }

    static void AddIcon(Button button,Sprite sprite)
    {
        if(!button||!sprite)return;
        var buttonRect=button.transform as RectTransform;
        float width=buttonRect?buttonRect.rect.width:130f;
        float height=buttonRect?buttonRect.rect.height:50f;
        var image=LandscapeUI.Image("Icon",button.transform,9,(height-20f)*.5f,20,20,sprite,Color.white,true);
        image.raycastTarget=false;
        var label=button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if(label)
        {
            LandscapeUI.Place(label.rectTransform,30,4,Mathf.Max(50,width-34),height-8);
            label.color=Color.white;
            label.fontSizeMin=13;
            label.fontSize=Mathf.Max(label.fontSize,18f);
        }
    }

    static void LoadStatusIcons()
    {
        if (statusIcons != null) return;
        var theme=LandscapeTheme.Current;
        if(theme)
        {
            statusIcons=new[]{theme.crew,theme.star,theme.lootBattery,theme.shield,theme.medkit};
            return;
        }
        var tex = Resources.Load<Texture2D>("UI/hud_status_icons");
        if (tex == null) { statusIcons = System.Array.Empty<Sprite>(); return; }
        statusIcons = new Sprite[6];
        float cell = tex.width / 6f;
        float h = tex.height;
        for (int i = 0; i < 6; i++)
        {
            var rect = new Rect(i * cell + cell * 0.08f, h * 0.08f, cell * 0.84f, h * 0.84f);
            statusIcons[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
        }
    }

    public static void Style(Transform parent)
    {
        foreach (var button in parent.GetComponentsInChildren<Button>(true))
        {
            var bg = button.targetGraphic as Image;
            if (!bg) continue;
            var theme=LandscapeTheme.Current;
            if(!bg.sprite&&theme)
                bg.sprite=button.name=="Capture"?theme.greenButton:
                    button.name=="Retreat"||button.name=="Attack"?theme.redButton:
                    button.name=="Actions"?theme.outlineButton:theme.darkButton;
            if(bg.sprite)bg.type=Image.Type.Sliced;
            bg.color=Color.white;
            button.GetComponent<LandscapeButtonPolish>()?.SetBaseColor(bg.color);
            var outline = bg.GetComponent<Outline>() ?? bg.gameObject.AddComponent<Outline>();
            outline.effectColor = button.name=="Capture"?new Color(.10f,1f,.52f,.48f):
                button.name=="Retreat"||button.name=="Attack"?new Color(1f,.18f,.22f,.42f):new Color(.10f,.78f,1f,.34f);
            outline.effectDistance = new Vector2(1, -1);
        }
        foreach (var bg in parent.GetComponentsInChildren<Image>(true))
        {
            if (bg.sprite == LandscapeTheme.Current?.panel)
            {
                bg.type=Image.Type.Sliced;
                bg.color = Color.white;
            }
        }
    }

    static void Place(Transform parent, string name, float x, float y, float w, float h)
    {
        var r = parent.Find(name) as RectTransform;
        if (r) LandscapeUI.Place(r, x, y, w, h);
    }

    void Update()
    {
        if (!resources || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        var d = GameManager.Data;
        if (d == null) return;

        UpdateCommandButtons();

        var progress=CampaignMissions.Progress(d);var mission=CampaignMissions.Active(d);
        int activeCrew=BattleManager.instance?.PlayerAgents.Count(a=>a&&a.IsAlive)??d.RecruitedAgents?.Count(a=>a!=null&&a.IsAlive)??0;
        string[] values={activeCrew.ToString(),d.Reputation.ToString(),$"{d.PoliceHeat}/10",$"{mission.number:00}  {progress.complete}/{progress.total}",$"{d.CityIntel}/10"};
        for(int i=0;i<statusValues.Length;i++)if(statusValues[i])statusValues[i].text=values[i];
        resources.text=$"SUPPLIES  <color=#65E5FF>{d.CitySupplies}</color>     TICKETS  <color=#FFD36A>{d.MatchTickets}</color>     SOCIAL  <color=#70F2A0>{d.SocialMomentum}</color>     TAXI  <color=#70F2A0>READY</color>";
        if(missionButton)
        {
            var label=missionButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if(label)label.text=$"MISSION {mission.number:00}  {progress.complete}/{progress.total}";
        }

        var visible = CampaignMissions.Steps(d).Where(s=>!s.Complete).Take(3);
        objectives.text = string.Join("\n", visible.Select(s =>
            $"<color=#E8BA5A>{s.title}</color>  <color=#E8F0F4>{Mathf.Min(s.progress,s.target)}/{s.target}</color>"));
        if (string.IsNullOrEmpty(objectives.text))
            objectives.text = "MISSION READY TO COMPLETE";

        string feed = CityGameplay.Instance?.EventText ?? "";
        int cut = feed.IndexOf("\n\n");
        events.text = cut > 0 ? feed.Substring(0, cut) : feed;
        if (string.IsNullOrEmpty(events.text)) events.text = "District quiet.";

        var state = string.Join("|", CampaignMissions.Steps(d).Select(s=>$"{s.id}:{s.progress}"));
        if (missionPanel.activeSelf && state != lastState) RefreshMissions();
        lastState = state;
    }

    void RefreshMissions()
    {
        foreach (Transform child in content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        var d = GameManager.Data;
        if (d == null) return;
        var mission=CampaignMissions.Active(d);var missionProgress=CampaignMissions.Progress(d);
        if(missionHeading)missionHeading.text=$"MISSION {mission.number:00}  ·  {mission.title}";
        if(missionBriefing)missionBriefing.text=$"{mission.briefing}   PROGRESS {missionProgress.complete}/{missionProgress.total}   REWARD £{mission.reward:N0}";
        foreach (var step in CampaignMissions.Steps(d))
        {
            var current=step;
            var row = LandscapeUI.Panel(current.id, content, 0, 0, 916, 120, true);
            LandscapeUI.LayoutSize(row.gameObject, 916, 120);
            bool ready=current.Complete;
            LandscapeUI.Text("Name", row.transform, (ready?"✓  ":"")+current.title, 17, 10, 650, 28, 20, ready?LandscapeUI.Green:LandscapeUI.White, true);
            LandscapeUI.Text("Body", row.transform, current.body, 17, 40, 650, 42, 15, LandscapeUI.Muted);
            LandscapeUI.Text("Progress", row.transform,
                $"{Mathf.Min(current.progress,current.target)}/{current.target}", 17, 88, 570, 22, 17, ready?LandscapeUI.Green:LandscapeUI.Gold);
            var button = LandscapeUI.Button("Action", row.transform,
                ready ? "COMPLETE" : current.operation.HasValue?"OPEN TASK":"SHOW GUIDE", 680, 30, 218, 58, ready ? "green" : "dark");
            button.interactable = !ready;
            button.onClick.AddListener(() =>
            {
                missionPanel.SetActive(false);
                if(current.operation.HasValue)
                {
                    var node=CityOperationsSystem.Instance?.Nodes.FirstOrDefault(n=>n&&n.Type==current.operation.Value);
                    if(node){CameraPanTouchOnly.Instance?.FocusOn(node.transform.position);node.OpenInteraction();}
                    else CityOperationsSystem.Instance?.OpenBoard();
                }
                else Guide(current);
            });
        }
        Style(missionPanel.transform);
    }

    void Guide(CampaignMissions.Step step)
    {
        CityGameplay.Instance?.PostEvent(step.title+" - "+step.body);
        if(step.id=="crew")CityGameplay.Instance?.OpenLocation(2);
        else if(step.id=="training")CityGameplay.Instance?.OpenLocation(3);
        else if(step.id=="morale")CityGameplay.Instance?.OpenLocation(1);
        else if(step.id=="defence")CityGameplay.Instance?.OpenLocation(0);
        else if(step.id=="territory")CityGameplay.Instance?.CaptureNearest();
        else if(step.id=="extort"||step.id=="tax"||step.id=="sabotage")CityActionSystem.Instance?.OpenActions();
        else if(step.id=="taxi")CityActionSystem.Instance?.FocusNearestTaxi();
        else BattleUIController.instance?.ShowAlert(step.title+"  "+step.progress+"/"+step.target,2.4f);
    }

    void UpdateCommandButtons()
    {
        if(moveGo)moveGo.SetActive(false);
        if(attackGo)attackGo.SetActive(false);
        if(authoredRetreat)authoredRetreat.SetActive(false);
        SetCommandVisible(talkButton, false);
        SetCommandVisible(captureButton, false);
        SetCommandVisible(actionsButton, false);
        SetCommandVisible(retreatButton, true);
    }

    static void SetCommandVisible(Button button, bool visible)
    {
        if (!button) return;
        if (button.gameObject.activeSelf != visible)
            button.gameObject.SetActive(visible);
        button.interactable = visible;
    }

    static bool HasNearbyCapturePoint(System.Collections.Generic.IEnumerable<AgentController> selected, float radius)
    {
        if (selected == null) return false;
        float r2 = radius * radius;
        var points = FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None);
        foreach (var agent in selected)
        {
            if (!agent || !agent.IsAlive) continue;
            foreach (var point in points)
            {
                if (!point || !point.gameObject.activeInHierarchy || point.IsCaptured) continue;
                Vector3 d = point.transform.position - agent.transform.position;
                d.y = 0;
                if (d.sqrMagnitude <= r2) return true;
            }
        }
        return false;
    }

    void BuildLeftPanelToggle(Transform hud, params GameObject[] extraTargets)
    {
        var button = LandscapeUI.Button("LeftHudToggle", frame, "<", 0, 414, 36, 72, "dark");
        sideToggle = button.gameObject.AddComponent<GameplayHudSideToggle>();
        sideToggle.label = button.GetComponentInChildren<TextMeshProUGUI>();
        sideToggle.shownGlyph="<";sideToggle.hiddenGlyph=">";sideToggle.root=frame;
        if (sideToggle.label){sideToggle.label.fontSize=25;sideToggle.label.color=Color.white;}
        var targets = new System.Collections.Generic.List<GameObject>();
        if (hud)
        {
            foreach (var name in new[] { "SquadRail", "SquadTitle", "MemberCount", "PortraitScroll" })
            {
                var t = hud.Find(name);
                if (t) targets.Add(t.gameObject);
            }
        }
        if (extraTargets != null) targets.AddRange(extraTargets.Where(t => t != null));
        sideToggle.targets = targets.ToArray();
        button.onClick.AddListener(sideToggle.Toggle);
    }

    void BuildTopPanelToggle(Transform hud)
    {
        var button=LandscapeUI.Button("TopHudToggle",frame,"^",782,0,36,30,"dark");
        var toggle=button.gameObject.AddComponent<GameplayHudSideToggle>();toggle.label=button.GetComponentInChildren<TextMeshProUGUI>();
        toggle.shownGlyph="^";toggle.hiddenGlyph="v";toggle.root=frame;
        toggle.targetNames=new[]{"TopBar","Heading","Cash","Reputation","StatusStrip","ResourceBand","LiveResources","CityDistrict","CityOperations","TopMission","TopIntel","TopVehicles","TopSettings"};
        if(toggle.label){toggle.label.fontSize=21;toggle.label.color=Color.white;}
        button.onClick.AddListener(toggle.Toggle);
    }

    void BuildRightPanelToggle(Transform hud)
    {
        var button=LandscapeUI.Button("RightHudToggle",frame,">",1564,414,36,72,"dark");
        var toggle=button.gameObject.AddComponent<GameplayHudSideToggle>();toggle.label=button.GetComponentInChildren<TextMeshProUGUI>();
        toggle.shownGlyph=">";toggle.hiddenGlyph="<";toggle.root=frame;toggle.controlsMinimap=true;
        toggle.targetNames=new[]{"EnemyCount","ObjectiveSlot","HeatSlot","CameraSetup","RecenterCamera"};
        if(toggle.label){toggle.label.fontSize=25;toggle.label.color=Color.white;}
        button.onClick.AddListener(toggle.Toggle);
    }
}

public sealed class GameplayHudSideToggle : MonoBehaviour
{
    public GameObject[] targets;
    public TextMeshProUGUI label;
    public Transform root;
    public string[] targetNames;
    public string shownGlyph="<",hiddenGlyph=">";
    public bool controlsMinimap;
    bool hidden;

    public void Toggle()
    {
        hidden = !hidden;
        if (targets != null)
            foreach (var target in targets)
                if (target != null) target.SetActive(!hidden);
        if(root&&targetNames!=null)
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(t&&targetNames.Contains(t.name)&&t.gameObject!=gameObject)t.gameObject.SetActive(!hidden);
        if(controlsMinimap)LiveMiniMap.Instance?.SetCompactVisible(!hidden);
        if (label != null) label.text = hidden ? hiddenGlyph : shownGlyph;
    }
}
