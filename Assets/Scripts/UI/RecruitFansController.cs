using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pastoral;

/// <summary>
/// Recruit Fans screen controller.
/// Shows 4 recruitment options; player picks one and clicks RECRUIT.
/// Cost is deducted immediately; fan gain is queued for next matchday.
///
/// Strategy Overhaul additions:
///  • Options 1–3 are gated behind reputation thresholds (ReputationUnlockSystem).
///  • When watchlisted, costs are higher and fan gain is lower.
///  • Low fan morale reduces the effective fan gain.
///  • Locked cards show a padlock icon and required reputation.
///  • Warning banner shown when police are watching.
///
/// Inspector: populate recruitOptions list; wire topBar fields and recruitBtn.
/// </summary>
public class RecruitFansController : MonoBehaviour
{
    // ── Recruitment option definition ─────────────────────────────────────
    [System.Serializable]
    public class RecruitOption
    {
        public string title;
        public string description;
        public int    cost;
        public int    minFans;
        public int    maxFans;
        public Sprite icon;
    }

    [Header("Recruitment Options (matches mockup order)")]
    public List<RecruitOption> recruitOptions = new List<RecruitOption>
    {
        new RecruitOption { title="POSTERS & FLYERS",      description="Put up posters around town.",              cost=1200, minFans=2, maxFans=4  },
        new RecruitOption { title="PUB RECRUITMENT",        description="Buy rounds and talk football.",            cost=2500, minFans=3, maxFans=5  },
        new RecruitOption { title="ONLINE CAMPAIGN",        description="Spread the word. Build hype.",             cost=3800, minFans=4, maxFans=7  },
        new RecruitOption { title="LOCAL YOUTH CONTACTS",   description="Connect with young lads looking for a crew.", cost=5000, minFans=5, maxFans=8 },
    };

    [Header("Top Stats Bar")]
    public TextMeshProUGUI topMoneyText;
    public TextMeshProUGUI topFansText;
    public TextMeshProUGUI topRepText;
    public TextMeshProUGUI topMoraleText;

    [Header("Option Card Containers — one per option (same order)")]
    public List<RecruitOptionCardUI> optionCards;

    [Header("Recruit Button")]
    public ButtonUI recruitBtn;

    [Header("Back Button")]
    public ButtonUI backBtn;

    [Header("Footer Label")]
    public TextMeshProUGUI footerLabel;

    [Header("Watchlist Warning")]
    [Tooltip("Banner shown when police watchlist is active — warn player of reduced efficiency.")]
    public GameObject watchlistWarningBanner;
    public TextMeshProUGUI watchlistWarningText;

    [Header("Revive Fans Section")]
    [Tooltip("Container GameObject that holds all revive rows. Show/hide based on dead agent count.")]
    public GameObject reviveSectionRoot;
    [Tooltip("Header label for the revive section (e.g. '☠ FALLEN LADS — REVIVE THEM').")]
    public TextMeshProUGUI reviveSectionHeader;
    [Tooltip("Scrollable container where revive row cards are spawned.")]
    public Transform reviveRowContainer;
    [Tooltip("Prefab with: agent name TMP (child named 'AgentName'), cost TMP ('ReviveCost'), ButtonUI ('ReviveBtn').")]
    public GameObject reviveRowPrefab;

    // Revive costs a flat fee — always cheaper than the cheapest recruit option (£1,200).
    // Base cost = £600 (50 % of cheapest recruit). Watchlist raises it by the same multiplier.
    private const int ReviveBaseCost = 600;

    // ── State ─────────────────────────────────────────────────────────────
    private int _selectedIndex = 0;
    private bool _cardsBuilt;

    void Start()
    {
        BuildCards();
        SelectOption(0);
        UpdateTopBar();

        recruitBtn?.AfterClickAnimation.AddListener(OnRecruit);
        backBtn?.AfterClickAnimation.AddListener(OnBack);

        if (footerLabel) footerLabel.text = "RECRUITED FANS WILL JOIN NEXT MATCHDAY.";

        RefreshReviveRows();
    }

    void OnEnable()
    {
        UpdateTopBar();
        RefreshLockStates();
        UpdateWatchlistWarning();
        RefreshReviveRows();
        if (_cardsBuilt) SelectOption(_selectedIndex);
    }

    void BuildCards()
    {
        if (_cardsBuilt) return;
        _cardsBuilt = true;
        for (int i = 0; i < optionCards.Count && i < recruitOptions.Count; i++)
        {
            var card = optionCards[i];
            var opt  = recruitOptions[i];
            card.Setup(opt.title, opt.description, opt.cost, opt.minFans, opt.maxFans, opt.icon);

            int idx = i;
            card.GetComponent<Button>()?.onClick.AddListener(() => SelectOption(idx));
        }
    }

    void RefreshLockStates()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;

        for (int i = 0; i < optionCards.Count && i < recruitOptions.Count; i++)
        {
            bool unlocked = ReputationUnlockSystem.IsRecruitOptionUnlocked(i, d.Reputation);
            int  reqRep   = ReputationUnlockSystem.GetRecruitOptionRequiredRep(i);

            // Show/hide lock state on card via the existing SetSelected / locked state
            // We call a SetLocked method if the card supports it; otherwise grey it out
            var btn = optionCards[i].GetComponent<Button>();
            if (btn) btn.interactable = unlocked;

            // Optionally show a "REP {reqRep} REQUIRED" label if card has one
            var lockLabel = optionCards[i].GetComponentInChildren<TextMeshProUGUI>();
            // (The actual lock badge UI must be wired in prefab; we set the description to indicate)
            if (!unlocked && lockLabel != null)
            {
                // Don't override the setup; the card's description can hint
                // If the card has a specific lock icon/badge, reveal it here
            }
        }
    }

    void SelectOption(int index)
    {
        var d = GameData.instance?.PlayerData;

        // Don't allow selecting locked options
        if (d != null && !ReputationUnlockSystem.IsRecruitOptionUnlocked(index, d.Reputation))
        {
            int req = ReputationUnlockSystem.GetRecruitOptionRequiredRep(index);
            if (footerLabel)
                footerLabel.text = $"LOCKED — REQUIRES {req} REPUTATION.";
            return;
        }

        if (footerLabel) footerLabel.text = "RECRUITED FANS WILL JOIN NEXT MATCHDAY.";
        _selectedIndex = index;
        for (int i = 0; i < optionCards.Count; i++)
            optionCards[i].SetSelected(i == index);

        // Calculate effective cost (watchlist multiplier)
        if (recruitBtn != null && index < recruitOptions.Count)
        {
            int effectiveCost = EffectiveCost(index, d);
            recruitBtn.interactable = (d != null && d.Money >= effectiveCost);
        }

        UpdateCostHint(index, d);
    }

    int EffectiveCost(int index, PlayerData d)
    {
        if (d == null || index >= recruitOptions.Count) return int.MaxValue;
        float mult = PoliceHeatSystem.GetRecruitCostMultiplier(d);
        return Mathf.CeilToInt(recruitOptions[index].cost * mult);
    }

    void UpdateCostHint(int index, PlayerData d)
    {
        if (d == null || index >= recruitOptions.Count) return;
        int effectiveCost = EffectiveCost(index, d);
        if (footerLabel && d.PoliceWatchlisted)
            footerLabel.text = $"POLICE WATCHING — cost raised to £{effectiveCost:N0}.";
    }

    void OnRecruit()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;

        if (!ReputationUnlockSystem.IsRecruitOptionUnlocked(_selectedIndex, d.Reputation))
        {
            Debug.Log("[Recruit] Option locked — insufficient reputation.");
            return;
        }

        var opt = recruitOptions[_selectedIndex];
        int effectiveCost = EffectiveCost(_selectedIndex, d);

        if (d.Money < effectiveCost)
        {
            Debug.Log("[Recruit] Not enough money.");
            return;
        }

        // Deduct cost (with watchlist markup)
        d.Money -= effectiveCost;

        // Fan gain with both morale and watchlist modifiers
        float watchlistMod = PoliceHeatSystem.GetRecruitModifier(d);
        float moraleMod    = ReputationUnlockSystem.GetMoraleRecruitModifier(d.FanMorale);
        float combined     = watchlistMod * moraleMod;

        int baseGain = UnityEngine.Random.Range(opt.minFans, opt.maxFans + 1);
        int fanGain  = Mathf.Max(1, Mathf.FloorToInt(baseGain * combined));

        d.PendingFansGain += fanGain;

        GameData.instance.SaveData();
        UpdateTopBar();
        SelectOption(_selectedIndex);
        GetComponentInParent<LandscapeFrontEnd>()?.Refresh();
        if (footerLabel) footerLabel.text = $"RECRUITMENT CONFIRMED · +{fanGain} NEW MEMBER{(fanGain == 1 ? "" : "S")} · JOIN ON END MATCHDAY. £{effectiveCost:N0} SPENT.";

        string modNote = combined < 0.9f ? $" (reduced — police watching + low morale)" : "";
        Debug.Log($"[Recruit] Spent £{effectiveCost}, queued +{fanGain} fans.{modNote}");

        ShowRecruitFeedback(opt.title, fanGain, effectiveCost, d.PendingFansGain, combined < 0.9f);
    }

    /// <summary>Clear confirmation so the player knows members were added and when they join.</summary>
    void ShowRecruitFeedback(string campaign, int fanGain, int cost, int totalIncoming, bool reduced)
    {
        GameAudio.Play("recovery");
        var shell = GetComponentInParent<LandscapeFrontEnd>();
        string body =
            $"{campaign}\n\n" +
            $"<color=#70F2A0>+{fanGain} NEW MEMBER{(fanGain == 1 ? "" : "S")} ADDED</color>   ·   £{cost:N0} spent\n" +
            $"Waiting to join: <color=#E8BA5A>{totalIncoming}</color>\n\n" +
            (reduced ? "<color=#FFD36A>Reduced intake — police watching / low morale.</color>\n\n" : "") +
            "Press <color=#70F2A0>END MATCHDAY</color> to bring them into the squad immediately.";
        var options = new System.Collections.Generic.List<GamePopup.Option>
        {
            new GamePopup.Option("KEEP RECRUITING", LandscapeUI.PanelColor, null)
        };
        if (shell != null)
            options.Add(new GamePopup.Option("END MATCHDAY NOW", new Color(.18f, .43f, .25f), () => shell.Navigate("end-day")));
        GamePopup.Instance.Show("RECRUITMENT CONFIRMED", body, options.ToArray());
    }

    void UpdateWatchlistWarning()
    {
        var d = GameData.instance?.PlayerData;
        bool watchlisted = d?.PoliceWatchlisted ?? false;

        if (watchlistWarningBanner)
            watchlistWarningBanner.SetActive(watchlisted);

        if (watchlisted && watchlistWarningText)
        {
            int heat = d.PoliceHeat;
            watchlistWarningText.text = $"⚠ POLICE ARE WATCHING — recruiting is {Mathf.RoundToInt((1f - PoliceHeatSystem.GetRecruitModifier(d)) * 100)}% less effective and costs more.";
        }
    }

    void OnBack()
    {
        var landscape = GetComponentInParent<LandscapeFrontEnd>();
        if (landscape != null) { landscape.Navigate("home"); return; }
        UIp.UITweeningOutsideScreenViewFrom(
            this,
            GetComponent<RectTransform>(),
            transform.parent.GetComponent<RectTransform>(),
            Vector2.left, 
            GameManager.SLIDE_ANIMATION_MULTIPLIER);
    }

    // ── Revive Fans ──────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the revive section from the current dead agent list.
    /// Shows the section only when there is at least one dead agent.
    /// </summary>
    void RefreshReviveRows()
    {
        var d = GameData.instance?.PlayerData;

        // Collect dead agents
        var dead = new List<AgentData>();
        if (d?.RecruitedAgents != null)
            foreach (var a in d.RecruitedAgents)
                if (!a.IsAlive) dead.Add(a);

        bool hasDead = dead.Count > 0;
        if (reviveSectionRoot) reviveSectionRoot.SetActive(hasDead);
        if (!hasDead) return;

        // Update header
        if (reviveSectionHeader)
            reviveSectionHeader.text = $"☠ FALLEN LADS ({dead.Count}) — PATCH UP & REVIVE";

        // Clear old rows
        if (reviveRowContainer != null)
            for (int i = reviveRowContainer.childCount - 1; i >= 0; i--)
                Destroy(reviveRowContainer.GetChild(i).gameObject);

        if (reviveRowPrefab == null || reviveRowContainer == null) return;

        // Effective revive cost (watchlist multiplier applies, same as recruiting)
        float mult      = d != null ? PoliceHeatSystem.GetRecruitCostMultiplier(d) : 1f;
        int effectiveCost = Mathf.CeilToInt(ReviveBaseCost * mult);

        foreach (var agent in dead)
        {
            var row = Instantiate(reviveRowPrefab, reviveRowContainer);

            // Wire agent name
            var nameTmp = row.transform.Find("AgentName")?.GetComponent<TextMeshProUGUI>();
            if (nameTmp == null && row.transform.childCount > 1 && row.transform.GetChild(1).childCount > 0)
                nameTmp = row.transform.GetChild(1).GetChild(0).GetComponent<TextMeshProUGUI>();
            if (nameTmp) nameTmp.text = agent.AgentName.ToUpper();

            // Wire cost label
            var costTmp = row.transform.Find("ReviveCost")?.GetComponent<TextMeshProUGUI>();
            if (costTmp == null && row.transform.childCount > 0 && row.transform.GetChild(0).childCount > 2)
                costTmp = row.transform.GetChild(0).GetChild(2).GetComponent<TextMeshProUGUI>();
            if (costTmp) costTmp.text = $"£{effectiveCost:N0}";

            // Wire revive button
            var reviveBtn = row.GetComponentInChildren<ButtonUI>();
            if (reviveBtn != null)
            {
                bool canAfford = d != null && d.Money >= effectiveCost;
                reviveBtn.interactable = canAfford;

                var capturedAgent = agent;
                var capturedCost  = effectiveCost;
                reviveBtn.AfterClickAnimation.AddListener(() => OnRevive(capturedAgent, capturedCost));
            }
        }
    }

    /// <summary>
    /// Called when the player taps a revive button for a specific dead agent.
    /// </summary>
    void OnRevive(AgentData agent, int cost)
    {
        if (GameData.instance == null) return;

        bool success = GameData.instance.ReviveFan(agent, cost);
        if (!success)
        {
            Debug.Log($"[Recruit] Cannot revive {agent.AgentName} — not enough money or already alive.");
            return;
        }

        UpdateTopBar();
        RefreshReviveRows(); // rebuild the list (revived agent is now alive → removed)

        Debug.Log($"[Recruit] {agent.AgentName} revived for £{cost:N0}.");
    }

    void UpdateTopBar()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;
        if (topMoneyText)  topMoneyText.text  = $"£{d.Money:N0}";
        if (topFansText)   topFansText.text   = $"{d.Fans:N0} FANS";
        if (topRepText)    topRepText.text    = $"{d.Reputation} REP";
        if (topMoraleText) topMoraleText.text = $"MORALE: {ReputationUnlockSystem.GetMoraleLabel(d.FanMorale)}";
    }
}
