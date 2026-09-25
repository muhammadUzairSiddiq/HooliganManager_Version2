using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Compact city HUD — status, squad, objectives, and contextual commands only.</summary>
public sealed class CityMissionHUD : MonoBehaviour
{
    RectTransform frame, content;
    GameObject missionPanel;
    TextMeshProUGUI objectives, events, missionHeading, missionBriefing;
    TextMeshProUGUI[] statusValues = new TextMeshProUGUI[9];
    RectTransform statusStrip;
    readonly RectTransform[] statusCells = new RectTransform[9];
    float lastStatusWidth;
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
            Place(hud, "TopBar", 8, 6, 1584, 128);
            Place(hud, "SquadRail", 16, 150, 286, 334);
            Place(hud, "SquadTitle", 30, 160, 258, 26);
            Place(hud, "MemberCount", 30, 184, 258, 36);
            Place(hud, "PortraitScroll", 24, 224, 268, 248);
            var members = hud.Find("MemberCount")?.GetComponent<TextMeshProUGUI>();
            if (members)
            {
                members.richText = true;
                members.fontSize = 13;
                members.enableWordWrapping = true;
                members.color = Color.white;
            }

            // Firm name — white so every HUD label stays readable on the dark bar.
            Place(hud, "Heading", 20, 16, 340, 40);
            var heading = hud.Find("Heading")?.GetComponent<TextMeshProUGUI>();
            if (heading)
            {
                heading.text = GameManager.Data?.FirmName.ToUpperInvariant() ?? "YOUR FIRM";
                heading.fontSize = 28;
                heading.color = Color.white;
                heading.fontStyle = FontStyles.Bold;
                LandscapeUI.FitBoxed(heading, 16f);
            }

            var squadTitle = hud.Find("SquadTitle")?.GetComponent<TextMeshProUGUI>();
            if (squadTitle) { squadTitle.text = "SQUAD"; squadTitle.fontSize = 18; squadTitle.color = Color.white; }
            var manage = LandscapeUI.Button("ManageSquad", hud, "MANAGE", 168, 156, 118, 32, "dark");
            manage.onClick.AddListener(() =>
            {
                var board = SquadBoard.Instance ?? FindFirstObjectByType<CityGameplay>()?.gameObject.AddComponent<SquadBoard>();
                board?.ShowSquad();
            });

            // Restore classic cash / reputation.
            Place(hud, "Cash", 1210, 16, 196, 36);
            Place(hud, "Reputation", 1210, 52, 196, 24);
            Place(hud, "Pause", 1414, 14, 172, 64);
            var cash = hud.Find("Cash")?.GetComponent<TextMeshProUGUI>();
            if (cash)
            {
                cash.gameObject.SetActive(true);
                cash.color = Color.white;
                cash.fontSize = 24;
                cash.alignment = TextAlignmentOptions.Right;
                LandscapeUI.FitBoxed(cash, 16f);
            }
            var rep = hud.Find("Reputation")?.GetComponent<TextMeshProUGUI>();
            if (rep)
            {
                rep.gameObject.SetActive(true);
                rep.color = Color.white;
                rep.fontSize = 15;
                rep.alignment = TextAlignmentOptions.Right;
                LandscapeUI.FitBoxed(rep, 11f);
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
        statusStrip = LandscapeUI.Panel("StatusStrip", frame, 16, 70, 1568, 52, false).rectTransform;
        statusStrip.GetComponent<Image>().color = Color.white;
        string[] labels={"CREW","REPUTATION","HEAT","MISSION","INTEL","SUPPLIES","TICKETS","SOCIAL","TAXI"};
        Color[] colors={LandscapeUI.Green,LandscapeUI.Gold,new Color(1f,.48f,.25f),new Color(.22f,.90f,.88f),new Color(.45f,.78f,1f),new Color(.39f,.90f,1f),LandscapeUI.Gold,LandscapeUI.Green,new Color(.12f,1f,.55f)};
        Sprite[] icons=StatusSprites();
        for(int i=0;i<labels.Length;i++)
        {
            var cell=LandscapeUI.Rect("Stat"+i,statusStrip,0,4,160,44);
            statusCells[i]=cell;
            cell.gameObject.AddComponent<RectMask2D>();
            if(i>0)LandscapeUI.Image("Divider",cell,0,8,2,28,null,new Color(.22f,.72f,.75f,.38f));
            if(icons!=null&&i<icons.Length&&icons[i])
            {
                var icon=LandscapeUI.Image("Icon",cell,8,12,20,20,icons[i],colors[i],true);
                icon.preserveAspect=true;
            }
            var label=LandscapeUI.Text("Label",cell,labels[i],32,4,120,16,11,LandscapeUI.Muted,true);
            LandscapeUI.FitBoxed(label,9f);
            statusValues[i]=LandscapeUI.Text("Value",cell,"--",32,22,120,20,16,colors[i],true);
            LandscapeUI.FitBoxed(statusValues[i],11f);
        }
        LayoutStatusStrip(1568f);
    }

    Sprite[] StatusSprites()
    {
        var theme=LandscapeTheme.Current;
        if(theme)return new[]{theme.crew,theme.star,theme.lootBattery,theme.shield,theme.medkit,theme.lootBat,theme.star,theme.crew,theme.energy};
        LoadStatusIcons();
        if(statusIcons==null||statusIcons.Length==0)return System.Array.Empty<Sprite>();
        return new[]{statusIcons[0],statusIcons[1],statusIcons[2],statusIcons[3],statusIcons[4],statusIcons.Length>2?statusIcons[2]:null,statusIcons[1],statusIcons[0],statusIcons.Length>1?statusIcons[1]:null};
    }

    void LayoutStatusStrip(float width)
    {
        if(!statusStrip)return;
        LandscapeUI.Place(statusStrip,16,70,width,52);
        float col=width/statusCells.Length;
        for(int i=0;i<statusCells.Length;i++)
        {
            if(!statusCells[i])continue;
            LandscapeUI.Place(statusCells[i],i*col,4,col,44);
            var label=statusCells[i].Find("Label") as RectTransform;
            var value=statusCells[i].Find("Value") as RectTransform;
            float textW=Mathf.Max(48f,col-40f);
            if(label)LandscapeUI.Place(label,32,4,textW,16);
            if(value)LandscapeUI.Place(value,32,22,textW,20);
        }
        lastStatusWidth=width;
    }

    void BuildTopNavigation(Transform hud)
    {
        if(hud)
        {
            var home=hud.Find("../CityDistrict") as RectTransform;
            if(!home)home=frame.Find("CityDistrict") as RectTransform;
            if(home){LandscapeUI.Place(home,360,18,112,50);FitNavButton(home.GetComponent<Button>(),LandscapeTheme.Current?.shield);}
        }
        var leftover=frame.Find("TopSettings");
        if(leftover)leftover.gameObject.SetActive(false);
        var mission=LandscapeUI.Button("TopMission",frame,"MISSION",634,18,122,50,"dark");
        mission.onClick.AddListener(()=>{missionPanel.SetActive(true);RefreshMissions();});
        var intel=LandscapeUI.Button("TopIntel",frame,"INTEL",762,18,108,50,"dark");
        intel.onClick.AddListener(()=>CityGameplay.Instance?.ShowIntelReport());
        var vehicles=LandscapeUI.Button("TopVehicles",frame,"VEHICLES",876,18,128,50,"dark");
        vehicles.onClick.AddListener(()=>CityActionSystem.Instance?.FocusNearestTaxi());
        var destinations=LandscapeUI.Button("TopDestinations",frame,"DESTINATIONS",1010,18,176,50,"dark");
        destinations.onClick.AddListener(()=>CityGameplay.Instance?.ToggleDestinations());
        var cash=FindFirstObjectByType<LandscapeBattleHUD>()?.cash;
        if(cash)LandscapeUI.Place(cash.rectTransform,1204,24,180,32);
        FitNavButton(mission,LandscapeTheme.Current?.star);
        FitNavButton(intel,LandscapeTheme.Current?.medkit);
        FitNavButton(vehicles,LandscapeTheme.Current?.energy);
        FitNavButton(destinations,LandscapeTheme.Current?.shield);
    }

    static void FitNavButton(Button button,Sprite sprite)
    {
        if(!button)return;
        var buttonRect=button.transform as RectTransform;
        float width=buttonRect?buttonRect.rect.width:130f;
        float height=buttonRect?buttonRect.rect.height:50f;
        if(sprite)
        {
            var existing=button.transform.Find("Icon")?.GetComponent<Image>();
            if(existing)existing.sprite=sprite;
            else
            {
                var image=LandscapeUI.Image("Icon",button.transform,8,(height-20f)*.5f,20,20,sprite,Color.white,true);
                image.raycastTarget=false;
            }
        }
        var label=button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if(label)
        {
            LandscapeUI.Place(label.rectTransform,sprite?30:8,6,Mathf.Max(48,width-(sprite?38:16)),height-12);
            label.color=Color.white;
            LandscapeUI.FitBoxed(label,11f);
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

    void LateUpdate()
    {
        if(!frame||!statusStrip)return;
        float width=Mathf.Max(1568f,frame.rect.width-32f);
        if(Mathf.Abs(width-lastStatusWidth)>1f)LayoutStatusStrip(width);
    }

    void Update()
    {
        if (statusValues[0] == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        var d = GameManager.Data;
        if (d == null) return;

        UpdateCommandButtons();

        var progress=CampaignMissions.Progress(d);var mission=CampaignMissions.Active(d);
        int activeCrew=BattleManager.instance?.PlayerAgents.Count(a=>a&&a.IsAlive)??d.RecruitedAgents?.Count(a=>a!=null&&a.IsAlive)??0;
        string[] values={
            activeCrew.ToString(),
            d.Reputation.ToString(),
            $"{d.PoliceHeat}/10",
            $"{mission.number:00}  {progress.complete}/{progress.total}",
            $"{d.CityIntel}/10",
            d.CitySupplies.ToString(),
            d.MatchTickets.ToString(),
            d.SocialMomentum.ToString(),
            "READY"
        };
        for(int i=0;i<statusValues.Length;i++)if(statusValues[i])statusValues[i].text=values[i];
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
        toggle.targetNames=new[]{"TopBar","Heading","Cash","Reputation","StatusStrip","CityDistrict","CityOperations","TopMission","TopIntel","TopVehicles"};
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
