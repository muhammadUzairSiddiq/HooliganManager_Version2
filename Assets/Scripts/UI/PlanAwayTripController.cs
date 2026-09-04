using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pastoral;

/// <summary>
/// Simplified Plan Away Trip — pick a destination and start.
/// Police heat / bribe / reputation gates live in gameplay only.
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
        SelectDestination(0);
    }

    void OnEnable() => SelectDestination(_selectedIndex);

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
        AnimateSelected(index);
        _selectedIndex = index;
        if (index < 0 || index >= destinations.Count) return;

        var dest = destinations[index];
        var d = GameData.instance?.PlayerData;

        // Always unlocked — police/rep gates removed from this screen.
        if (detailLockOverlay) detailLockOverlay.SetActive(false);
        if (detailLockReasonText) detailLockReasonText.text = "";

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

        if (detailNameText) detailNameText.text = dest.name.ToUpper();
        if (detailDescriptionText) detailDescriptionText.text = dest.description;
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

        int aliveAgents = 0;
        if (d?.RecruitedAgents != null)
            foreach (var agent in d.RecruitedAgents)
                if (agent.IsAlive) aliveAgents++;

        if (startTripBtn != null)
            startTripBtn.interactable = d != null && d.Money >= dest.travelCost && aliveAgents > 0 && d.Fans > 0;

        if (d != null)
        {
            d.LastSelectedDestination = dest.name;
            GameData.instance.SaveData();
        }
    }

    void OnStartTrip()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;

        var dest = destinations[_selectedIndex];
        if (d.Money < dest.travelCost) return;

        int aliveAgents = 0;
        if (d.RecruitedAgents != null)
            foreach (var agent in d.RecruitedAgents)
                if (agent.IsAlive) aliveAgents++;
        if (aliveAgents <= 0 || d.Fans <= 0) return;

        BotData bot = null;
        if (d.RivalBots != null)
            bot = d.RivalBots.Find(b => b.firmName == dest.rivalFirmName);

        d.Money -= dest.travelCost;
        d.LastSelectedDestination = dest.name;
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
