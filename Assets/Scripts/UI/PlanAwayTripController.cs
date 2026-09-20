using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pastoral;

/// <summary>
/// Four sequential away missions. Only the active campaign destination can launch;
/// the remaining three stay visibly locked until the previous mission is complete.
/// </summary>
public class PlanAwayTripController : MonoBehaviour
{
    [System.Serializable]
    public class Destination
    {
        public string name;
        public int    distanceKm;
        public int    potentialReward;
        public string rivalStrength;
        public string policePresence;
        public int    travelCost;
        public string description;
        public Sprite locationImage;
        public Button mapButton;
        public string rivalFirmName;
    }

    [Header("Destinations")]
    public List<Destination> destinations = new List<Destination>
    {
        new Destination { name="East Docks",  distanceKm=45, potentialReward=4200,
            rivalStrength="HIGH", policePresence="MEDIUM", travelCost=1100,
            description="Rival firm active. Big payout if we smash them.",
            rivalFirmName="East End Crew" },
        new Destination { name="North End",   distanceKm=70, potentialReward=3600,
            rivalStrength="MEDIUM", policePresence="HIGH", travelCost=1400,
            description="Rough neighbourhood. Old rivalry.",
            rivalFirmName="Northside Boys" },
        new Destination { name="Riverside",   distanceKm=58, potentialReward=3900,
            rivalStrength="LOW", policePresence="LOW", travelCost=900,
            description="Easy trip, good warm-up for the lads.",
            rivalFirmName="South City Firm" },
        new Destination { name="Old Town",    distanceKm=82, potentialReward=5100,
            rivalStrength="VERY HIGH", policePresence="HIGH", travelCost=1700,
            description="Hardest trip. Big reward if we pull it off.",
            rivalFirmName="West Lions" },
    };

    [Header("Selected Destination Detail Panel")]
    public Image           detailLocationImage;
    public TextMeshProUGUI detailNameText;
    public TextMeshProUGUI detailDescriptionText;
    public TextMeshProUGUI detailRewardText;
    public TextMeshProUGUI detailRivalStrengthText;
    public TextMeshProUGUI detailPoliceText;
    public TextMeshProUGUI detailTravelCostText;
    public TextMeshProUGUI detailArchetypeText;
    public TextMeshProUGUI detailMottoText;
    public TextMeshProUGUI detailNetResultText;
    public TextMeshProUGUI detailInfamyText;
    public GameObject      detailLockOverlay;
    public TextMeshProUGUI detailLockReasonText;
    public RectTransform   deploymentContent;
    public TextMeshProUGUI deploymentSummaryText;
    public Button          selectAllMembersBtn;
    public Button          clearMembersBtn;
    public int             maxDeployMembers = 12;

    [Header("Buttons")]
    public ButtonUI startTripBtn;
    public ButtonUI backBtn;

    private int _selectedIndex = 0;

    void Start()
    {
        for (int i = 0; i < destinations.Count; i++)
        {
            int idx = i;
            destinations[i].mapButton?.onClick.AddListener(() => SelectDestination(idx));
        }

        startTripBtn?.AfterClickAnimation.AddListener(OnStartTrip);
        backBtn?.AfterClickAnimation.AddListener(OnBack);
        selectAllMembersBtn?.onClick.AddListener(SelectAllMembers);
        clearMembersBtn?.onClick.AddListener(ClearMemberSelection);
        EnsureDeploymentPanel();
        FitDeploymentPanel();
        EnsureDeploymentSelection();
        BuildDeploymentList();
        RefreshDestinationCardImages();
        SelectDestination(0);
    }

    void OnEnable()
    {
        EnsureDeploymentSelection();
        BuildDeploymentList();
        SelectDestination(_selectedIndex);
    }

    void AnimateSelected(int index)
    {
        Destination dest;
        RectTransform currentRect;
        if (_selectedIndex != index)
        {
            dest = destinations[_selectedIndex];
            if (dest.mapButton != null)
            {
                currentRect = dest.mapButton.GetComponent<RectTransform>();
                StartCoroutine(UIp.UIScaleTweening(currentRect, currentRect.localScale, Vector3.one, DisableOnScaleComplete: false, GameManager.BUTTON_ANIMATION_MULTIPLIER));
            }
        }
        dest = destinations[index];
        if (dest.mapButton != null)
        {
            currentRect = dest.mapButton.GetComponent<RectTransform>();
            StartCoroutine(UIp.UIScaleTweening(currentRect, Vector3.one, Vector3.one * 1.05f, DisableOnScaleComplete: false, GameManager.BUTTON_ANIMATION_MULTIPLIER));
        }
    }

    void SelectDestination(int index)
    {
        if (index < 0 || index >= destinations.Count) return;
        AnimateSelected(index);
        _selectedIndex = index;
        if (index < 0 || index >= destinations.Count) return;

        var dest = destinations[index];
        var d = GameData.instance?.PlayerData;

        int missionNumber=CampaignMissions.MissionForDestination(dest.name);
        bool unlocked=CampaignMissions.CanEnterDestination(d,dest.name,out string lockReason);
        if (detailLockOverlay) detailLockOverlay.SetActive(!unlocked);
        if (detailLockReasonText) detailLockReasonText.text = unlocked ? "" : lockReason.ToUpperInvariant();

        BotData bot = null;
        if (d?.RivalBots != null)
            bot = d.RivalBots.Find(b => b.firmName == dest.rivalFirmName);

        string strengthLabel = dest.rivalStrength;
        if (bot != null)
        {
            strengthLabel = bot.strength switch
            {
                < 30 => "LOW",
                < 55 => "MEDIUM",
                < 75 => "HIGH",
                _ => "VERY HIGH"
            };
        }

        int dynamicReward = dest.potentialReward;
        if (bot != null)
        {
            float strengthRatio = Mathf.Clamp(bot.strength / 20f, 0.7f, 2.0f);
            dynamicReward = (Mathf.RoundToInt(dest.potentialReward * strengthRatio) / 100) * 100;
        }

        if (detailNameText) detailNameText.text = $"MISSION {missionNumber:00}  ·  {dest.name.ToUpper()}";
        if (detailDescriptionText)
        {
            var mission=CampaignMissions.Get(missionNumber);
            detailDescriptionText.text=mission.briefing+"\n\n"+(unlocked?"ACTIVE MISSION":lockReason.ToUpperInvariant());
        }
        if (detailLocationImage)
        {
            detailLocationImage.sprite = dest.locationImage != null ? dest.locationImage : CreateDestinationSprite(dest.name);
            detailLocationImage.color = detailLocationImage.sprite != null ? Color.white : detailLocationImage.color;
        }
        if (detailRewardText) detailRewardText.text = $"£{dynamicReward:N0}";
        if (detailRivalStrengthText) detailRivalStrengthText.text = strengthLabel;
        if (detailTravelCostText) detailTravelCostText.text = $"£{dest.travelCost:N0}";
        SetRiskColor(detailRivalStrengthText, strengthLabel);

        // Cut the noise — hide police / heat / infamy / net chrome.
        if (detailPoliceText) detailPoliceText.gameObject.SetActive(false);
        if (detailNetResultText) detailNetResultText.gameObject.SetActive(false);
        if (detailInfamyText) detailInfamyText.gameObject.SetActive(false);
        if (detailArchetypeText) detailArchetypeText.gameObject.SetActive(false);
        if (detailMottoText) detailMottoText.gameObject.SetActive(false);

        int aliveAgents = CountAliveAgents(d);
        int selectedAgents = CountSelectedAliveAgents(d);

        if (startTripBtn != null)
            startTripBtn.interactable = unlocked && d != null && d.Money >= dest.travelCost && aliveAgents > 0 && selectedAgents > 0 && d.Fans > 0;

        if (d != null)
        {
            d.LastSelectedDestination = dest.name;
            GameData.instance.SaveData();
        }
        RefreshDeploymentSummary();
    }

    void RefreshDestinationCardImages()
    {
        for (int i = 0; i < destinations.Count; i++)
        {
            var dest = destinations[i];
            if (dest == null || dest.mapButton == null) continue;
            var location = dest.mapButton.transform.parent?.Find("Location")?.GetComponent<Image>();
            if (location == null) continue;
            location.sprite = dest.locationImage != null ? dest.locationImage : CreateDestinationSprite(dest.name);
            if (location.sprite != null) location.color = Color.white;
        }
    }

    void OnStartTrip()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;

        var dest = destinations[_selectedIndex];
        if(!CampaignMissions.CanEnterDestination(d,dest.name,out string lockReason))
        {
            GamePopup.Instance?.Show("MISSION LOCKED",lockReason,new GamePopup.Option("BACK",LandscapeUI.PanelColor,null));
            return;
        }
        if (d.Money < dest.travelCost) return;

        EnsureDeploymentSelection();
        int aliveAgents = CountAliveAgents(d);
        int selectedAgents = CountSelectedAliveAgents(d);
        if (aliveAgents <= 0 || selectedAgents <= 0 || d.Fans <= 0) return;

        BotData bot = null;
        if (d.RivalBots != null)
            bot = d.RivalBots.Find(b => b.firmName == dest.rivalFirmName);

        d.Money -= dest.travelCost;
        d.LastSelectedDestination = dest.name;
        d.LastAwayTripMatchday = d.MatchDay;
        if (d.DestinationVisitCounts == null)
            d.DestinationVisitCounts = new SerializableDictionary<string, int>();
        int visits = d.DestinationVisitCounts.GetOrDefault(dest.name, 0);
        d.DestinationVisitCounts.Set(dest.name, visits + 1);
        GameData.instance.SaveData();

        int rivalCount = bot != null ? Mathf.Max(4, bot.fans) : EnemyCountFromStrength(dest.rivalStrength);
        int rivalStr = bot != null ? bot.strength : EnemyStrengthValue(dest.rivalStrength);
        string rivalFirmName = bot != null ? bot.firmName : dest.rivalFirmName;

        // Always go to gameplay — police pressure is in-scene only.
        GameManager.instance?.StartRivalFight(rivalCount, rivalStr, dest.potentialReward, rivalFirmName);
    }

    void EnsureDeploymentPanel()
    {
        if (deploymentContent != null) return;
        var parent = transform as RectTransform;
        if (parent == null) return;

        var panel = LandscapeUI.Panel("DeploySelection", parent, 286, 596, 666, 88, true);
        LandscapeUI.Text("Title", panel.transform, "TRAVELLING CREW", 18, 10, 210, 28, 22, null, true);
        deploymentSummaryText = LandscapeUI.Text("Summary", panel.transform, "", 18, 41, 210, 28, 16, LandscapeUI.Muted);
        selectAllMembersBtn = LandscapeUI.Button("SelectAllMembers", panel.transform, "ALL", 244, 16, 70, 28, "green");
        clearMembersBtn = LandscapeUI.Button("ClearMembers", panel.transform, "NONE", 244, 48, 70, 28, "dark");
        deploymentContent = LandscapeUI.Scroll("DeployList", panel.transform, 326, 11, 326, 66, true).content;
        selectAllMembersBtn.onClick.AddListener(SelectAllMembers);
        clearMembersBtn.onClick.AddListener(ClearMemberSelection);
    }

    void FitDeploymentPanel()
    {
        if (deploymentContent == null) return;
        Transform panel = deploymentContent;
        while (panel != null && panel.name != "DeploySelection")
            panel = panel.parent;
        if (panel is RectTransform panelRt)
            LandscapeUI.Place(panelRt, 286, 596, 666, 88);

        PlaceChild(panel, "Title", 18, 10, 210, 28);
        PlaceChild(panel, "DeployTitle", 18, 10, 210, 28);
        PlaceChild(panel, "Summary", 18, 41, 210, 28);
        PlaceChild(panel, "DeploySummary", 18, 41, 210, 28);

        var scroll = deploymentContent.GetComponentInParent<ScrollRect>();
        if (scroll != null)
        {
            scroll.horizontal = true;
            scroll.vertical = false;
            if (scroll.transform is RectTransform scrollRt)
                LandscapeUI.Place(scrollRt, 326, 11, 326, 66);
        }

        if (selectAllMembersBtn && selectAllMembersBtn.transform is RectTransform allRt)
        {
            LandscapeUI.Place(allRt, 244, 16, 70, 28);
            SetButtonLabel(selectAllMembersBtn, "ALL");
        }
        if (clearMembersBtn && clearMembersBtn.transform is RectTransform clearRt)
        {
            LandscapeUI.Place(clearRt, 244, 48, 70, 28);
            SetButtonLabel(clearMembersBtn, "NONE");
        }

        var vertical = deploymentContent.GetComponent<VerticalLayoutGroup>();
        if (vertical) Destroy(vertical);
        var horizontal = deploymentContent.GetComponent<HorizontalLayoutGroup>() ?? deploymentContent.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 8;
        horizontal.padding = new RectOffset(4, 4, 4, 4);
        horizontal.childControlWidth = false;
        horizontal.childControlHeight = false;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;
        var fitter = deploymentContent.GetComponent<ContentSizeFitter>() ?? deploymentContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    void PlaceChild(Transform parent, string childName, float x, float y, float w, float h)
    {
        var child = parent != null ? parent.Find(childName) as RectTransform : null;
        if (child != null) LandscapeUI.Place(child, x, y, w, h);
    }

    void SetButtonLabel(Button button, string label)
    {
        var text = button ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
        if (text != null) text.text = label;
    }

    void BuildDeploymentList()
    {
        if (deploymentContent == null) return;
        LandscapeUI.Clear(deploymentContent);
        var d = GameData.instance?.PlayerData;
        if (d?.RecruitedAgents == null) return;
        EnsureDeploymentSelection();

        int shown = 0;
        foreach (var agent in d.RecruitedAgents)
        {
            if (agent == null || !agent.IsAlive) continue;
            var a = agent;
            bool selected = IsMemberSelected(d, a);
            var row = LandscapeUI.Panel("Deploy_" + a.AgentId, deploymentContent, 0, 0, 154, 54, true);
            LandscapeUI.LayoutSize(row.gameObject, 154, 54);
            LandscapeUI.Image("State", row.transform, 8, 13, 28, 28, null, selected ? LandscapeUI.Green : new Color(.22f, .26f, .29f));
            LandscapeUI.Text("Check", row.transform, selected ? "ON" : "", 8, 13, 28, 28, 15, Color.black, true, TextAlignmentOptions.Center);
            LandscapeUI.Text("Name", row.transform, a.AgentName.ToUpper(), 43, 6, 98, 22, 17, null, true);
            LandscapeUI.Text("Stats", row.transform, $"{a.CurrentHp:0}/{a.MaxHp:0}  STR {a.Strength:0}", 43, 29, 98, 18, 13, LandscapeUI.Muted);
            var toggle = row.gameObject.AddComponent<Button>();
            toggle.targetGraphic = row;
            LandscapeUI.Colors(toggle);
            toggle.onClick.AddListener(() => ToggleMember(a));
            shown++;
        }

        if (shown == 0)
        {
            var empty = LandscapeUI.Text("NoMembers", deploymentContent, "NO MATCH-READY MEMBERS\nRecruit or revive someone before travelling.", 0, 0, 278, 120, 20, LandscapeUI.Muted, false, TextAlignmentOptions.Center);
            LandscapeUI.LayoutSize(empty.gameObject, 278, 120);
        }

        RefreshDeploymentSummary();
    }

    void EnsureDeploymentSelection()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;
        bool initializeFromRoster = d.SelectedAwayAgentIds == null || !d.DeploymentSelectionCustomized;
        if (d.SelectedAwayAgentIds == null) d.SelectedAwayAgentIds = new List<string>();

        for (int i = d.SelectedAwayAgentIds.Count - 1; i >= 0; i--)
            if (!IsLivingAgentId(d, d.SelectedAwayAgentIds[i]))
                d.SelectedAwayAgentIds.RemoveAt(i);

        if (initializeFromRoster && d.RecruitedAgents != null)
        {
            d.SelectedAwayAgentIds.Clear();
            foreach (var agent in d.RecruitedAgents)
            {
                if (agent == null || !agent.IsAlive) continue;
                if (d.SelectedAwayAgentIds.Count >= maxDeployMembers) break;
                d.SelectedAwayAgentIds.Add(agent.AgentId);
            }
        }
    }

    void SelectAllMembers()
    {
        var d = GameData.instance?.PlayerData;
        if (d?.RecruitedAgents == null) return;
        d.SelectedAwayAgentIds = new List<string>();
        d.DeploymentSelectionCustomized = false;
        foreach (var agent in d.RecruitedAgents)
        {
            if (agent == null || !agent.IsAlive) continue;
            if (d.SelectedAwayAgentIds.Count >= maxDeployMembers) break;
            d.SelectedAwayAgentIds.Add(agent.AgentId);
        }
        GameData.instance.SaveData();
        BuildDeploymentList();
        SelectDestination(_selectedIndex);
    }

    void ClearMemberSelection()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;
        if (d.SelectedAwayAgentIds == null) d.SelectedAwayAgentIds = new List<string>();
        d.SelectedAwayAgentIds.Clear();
        d.DeploymentSelectionCustomized = true;
        GameData.instance.SaveData();
        BuildDeploymentList();
        SelectDestination(_selectedIndex);
    }

    void ToggleMember(AgentData agent)
    {
        var d = GameData.instance?.PlayerData;
        if (d == null || agent == null || !agent.IsAlive) return;
        if (d.SelectedAwayAgentIds == null) d.SelectedAwayAgentIds = new List<string>();
        if (d.SelectedAwayAgentIds.Contains(agent.AgentId))
        {
            d.SelectedAwayAgentIds.Remove(agent.AgentId);
            d.DeploymentSelectionCustomized = true;
        }
        else if (d.SelectedAwayAgentIds.Count < maxDeployMembers)
        {
            d.SelectedAwayAgentIds.Add(agent.AgentId);
            d.DeploymentSelectionCustomized = true;
        }
        GameData.instance.SaveData();
        BuildDeploymentList();
        SelectDestination(_selectedIndex);
    }

    bool IsMemberSelected(PlayerData d, AgentData agent)
    {
        return d?.SelectedAwayAgentIds != null && agent != null && d.SelectedAwayAgentIds.Contains(agent.AgentId);
    }

    bool IsLivingAgentId(PlayerData d, string agentId)
    {
        if (d?.RecruitedAgents == null || string.IsNullOrEmpty(agentId)) return false;
        foreach (var agent in d.RecruitedAgents)
            if (agent != null && agent.IsAlive && agent.AgentId == agentId)
                return true;
        return false;
    }

    int CountAliveAgents(PlayerData d)
    {
        int count = 0;
        if (d?.RecruitedAgents != null)
            foreach (var agent in d.RecruitedAgents)
                if (agent != null && agent.IsAlive) count++;
        return count;
    }

    int CountSelectedAliveAgents(PlayerData d)
    {
        int count = 0;
        if (d?.SelectedAwayAgentIds == null) return 0;
        foreach (string id in d.SelectedAwayAgentIds)
            if (IsLivingAgentId(d, id)) count++;
        return count;
    }

    void RefreshDeploymentSummary()
    {
        var d = GameData.instance?.PlayerData;
        if (deploymentSummaryText == null || d == null) return;
        int selected = CountSelectedAliveAgents(d);
        int alive = CountAliveAgents(d);
        deploymentSummaryText.text = $"{selected} / {alive} selected for this away trip.";
    }

    Sprite CreateDestinationSprite(string destination)
    {
        string key = destination switch
        {
            "North End" => "CityPresentation/CityLoading_NorthEnd",
            "Riverside" => "CityPresentation/CityLoading_Riverside",
            "Old Town" => "CityPresentation/CityLoading_OldTown",
            _ => "CityPresentation/CityLoading_Docks",
        };
        var texture = Resources.Load<Texture2D>(key) ?? Resources.Load<Texture2D>("CityPresentation/CityLoading_Docks");
        return texture ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f)) : null;
    }

    int EnemyCountFromStrength(string rivalStrength) => rivalStrength switch
    {
        "LOW" => 6, "MEDIUM" => 9, "HIGH" => 13, "VERY HIGH" => 17, _ => 8
    };

    int EnemyStrengthValue(string rivalStrength) => rivalStrength switch
    {
        "LOW" => 10, "MEDIUM" => 20, "HIGH" => 32, "VERY HIGH" => 42, _ => 20
    };

    void OnBack()
    {
        var landscape = GetComponentInParent<LandscapeFrontEnd>();
        if (landscape != null) { landscape.Navigate("home"); return; }
        UIp.UITweeningOutsideScreenViewFrom(
            this,
            GetComponent<RectTransform>(),
            transform.parent.GetComponent<RectTransform>(),
            Vector2.right,
            GameManager.SLIDE_ANIMATION_MULTIPLIER);
    }

    void SetRiskColor(TextMeshProUGUI label, string risk)
    {
        if (label == null) return;
        label.color = risk switch
        {
            "LOW" => new Color(0.2f, 0.85f, 0.2f),
            "MEDIUM" => new Color(0.9f, 0.75f, 0.1f),
            "HIGH" => new Color(0.9f, 0.35f, 0.1f),
            "VERY HIGH" => new Color(0.9f, 0.1f, 0.1f),
            _ => Color.white
        };
    }
}
