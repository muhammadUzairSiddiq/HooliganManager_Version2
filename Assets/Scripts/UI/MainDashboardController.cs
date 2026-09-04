using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pastoral;

/// <summary>
/// Main Dashboard screen controller (DashboardScene).
/// Reads live data from GameData.instance.PlayerData and drives all UI.
///
/// Additions (Strategy Overhaul):
///  • Police heat gradient bar + narrative description label
///  • "LAY LOW" button (visible when heat ≥ 5)
///  • Fan morale indicator dot next to fans count
///  • Firm STATUS line (unknown crew → top firm)
///  • Win/loss record on firm card
///  • Prestige badge at rep ≥ 80
///  • Rank change flash after matchday end
///  • Cost previews on action buttons
///
/// Inspector wiring — assign every field shown below.
/// </summary>
public class MainDashboardController : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
    public static MainDashboardController instance;

    // ── Top Status Bar ────────────────────────────────────────────────────
    [Header("Top Bar")]
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI fansText;
    public TextMeshProUGUI reputationText;
    public TextMeshProUGUI matchDayText;
    public Slider          policeHeatFill;
    public TextMeshProUGUI policeHeatLabel;
    [Tooltip("Narrative description of current heat level (e.g. 'CID has your firm flagged').")]
    public TextMeshProUGUI policeHeatDescriptionText;

    // ── Fan Morale ────────────────────────────────────────────────────────
    [Header("Fan Morale")]
    [Tooltip("Small dot/circle image next to fans — colour changes with morale.")]
    public Image           moraleDotImage;
    [Tooltip("Optional text label showing morale percentage or label.")]
    public TextMeshProUGUI moraleText;

    // ── Firm Card ─────────────────────────────────────────────────────────
    [Header("Firm Card")]
    public Image           firmShieldImage;
    public TextMeshProUGUI firmNameText;
    public TextMeshProUGUI clubLocationText;
    public TextMeshProUGUI hooliganRankText;
    public TextMeshProUGUI leaguePositionText;
    public TextMeshProUGUI nextRivalText;
    public TextMeshProUGUI nextRivalMatchText;
    [Tooltip("Dynamic status line: 'Unknown crew' → 'Top Firm'")]
    public TextMeshProUGUI firmStatusText;
    [Tooltip("Win / Loss / Draw record: '28W - 10L'")]
    public TextMeshProUGUI firmRecordText;
    [Tooltip("Prestige badge GameObject — shown when reputation ≥ 80.")]
    public GameObject      prestigeBadge;

    // ── Recent Events ─────────────────────────────────────────────────────
    [Header("Recent Events")]
    public Transform       eventsContainer;
    public GameObject      noEventsInfo;
    public GameObject      eventRowPrefab;

    // ── Action Buttons ────────────────────────────────────────────────────
    [Header("Action Buttons")]
    public ButtonUI recruitFansBtn;
    public ButtonUI planAwayTripBtn;
    public ButtonUI viewRankingsBtn;
    public ButtonUI clubFirmInfoBtn;
    [Tooltip("'Lay Low' button — spend money to reduce heat. Visible when heat ≥ 5.")]
    public ButtonUI layLowBtn;
    [Tooltip("Optional cost labels on each action button.")]
    public TextMeshProUGUI recruitCostHint;
    public TextMeshProUGUI tripCostHint;
    public TextMeshProUGUI layLowCostHint;

    // ── Sub-panels ────────────────────────────────────────────────────────
    [Header("Sub Panels")]
    public GameObject recruitFansPanel;
    public GameObject planAwayTripPanel;
    public GameObject rankingsPanel;
    public GameObject clubFirmInfoPanel;
    [Tooltip("The BribePolice panel root GameObject.")]
    public GameObject bribePolicePanel;

    // ── End Matchday ──────────────────────────────────────────────────────
    [Header("End Matchday")]
    public ButtonUI endMatchDayBtn;

    [Header("Club Data (shared ScriptableObject asset)")]
    [Tooltip("Drag the ClubRegistry asset here — same asset used by ClubSelectionController.")]
    public ClubRegistry clubRegistry;

    // ── Rank change notification ──────────────────────────────────────────
    [Header("Rank Change Notification")]
    [Tooltip("Animated popup text shown briefly when rank changes after matchday end.")]
    public TextMeshProUGUI rankChangePopupText;

    // ── Panel controllers (auto-found) ────────────────────────────────────
    private RecruitFansController   _recruitCtrl;
    private PlanAwayTripController  _awayTripCtrl;
    private RankingsController      _rankingsCtrl;

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    void OnEnable()
    {
        GameData.OnSavingData    += RefreshUI;
        GameData.OnMatchDayEnded += OnMatchDayEnded;
    }
    void OnDisable()
    {
        GameData.OnSavingData    -= RefreshUI;
        GameData.OnMatchDayEnded -= OnMatchDayEnded;
    }

    void Start()
    {
        _recruitCtrl  = recruitFansPanel?.GetComponent<RecruitFansController>();
        _awayTripCtrl = planAwayTripPanel?.GetComponent<PlanAwayTripController>();
        _rankingsCtrl = rankingsPanel?.GetComponent<RankingsController>();

        // Wire standard action buttons
        recruitFansBtn?.AfterClickAnimation.AddListener(()  => ShowPanel(recruitFansPanel));
        planAwayTripBtn?.AfterClickAnimation.AddListener(() => ShowPanel(planAwayTripPanel));
        viewRankingsBtn?.AfterClickAnimation.AddListener(() => ShowPanel(rankingsPanel));
        clubFirmInfoBtn?.AfterClickAnimation.AddListener(() => ShowPanel(clubFirmInfoPanel));

        // Wire Lay Low button
        layLowBtn?.AfterClickAnimation.AddListener(OnLayLow);

        endMatchDayBtn?.AfterClickAnimation.AddListener(OnEndMatchDay);

        // Handle "open rankings" deep-link from BattleResultController
        if (PlayerPrefs.GetInt("OpenRankingsOnLoad", 0) == 1)
        {
            PlayerPrefs.DeleteKey("OpenRankingsOnLoad");
            ShowPanel(rankingsPanel);
        }
        else
        {
            HideAllPanels();
        }

        // Police bribe UI is gameplay-only — never show on the dashboard.
        if (bribePolicePanel != null)
            bribePolicePanel.SetActive(false);

        // Change Team button (team selection moved here from the main menu).
        EnsureChangeTeamButton();

        RefreshUI();
    }

    private void EnsureChangeTeamButton()
    {
        var canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        if (parent.Find("ChangeTeamBtn_Runtime") != null) return;
        if (clubRegistry == null || clubRegistry.clubs == null || clubRegistry.clubs.Count == 0) return;

        var go = new GameObject("ChangeTeamBtn_Runtime", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(220f, 64f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.7f, 0.15f, 0.15f, 1f);
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "CHANGE TEAM";
        tmp.fontSize = 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        go.GetComponent<Button>().onClick.AddListener(OpenChangeTeamPopup);
    }

    private void OpenChangeTeamPopup()
    {
        if (clubRegistry == null || clubRegistry.clubs == null) return;
        var options = new System.Collections.Generic.List<GamePopup.Option>();
        for (int i = 0; i < clubRegistry.clubs.Count; i++)
        {
            int idx = i;
            var c = clubRegistry.clubs[i];
            options.Add(new GamePopup.Option(c.firmName.ToUpper(),
                ClubRegistry.ColorFromHex(c.primaryColor),
                () => ApplyClub(idx)));
        }
        options.Add(new GamePopup.Option("CANCEL", new Color(0.25f, 0.32f, 0.4f), null));
        GamePopup.Instance.Show("SELECT YOUR FIRM", "Pick the firm you want to manage.", options.ToArray());
    }

    private void ApplyClub(int index)
    {
        if (clubRegistry == null || index < 0 || index >= clubRegistry.clubs.Count) return;
        var c = clubRegistry.clubs[index];
        var d = GameData.instance?.PlayerData;
        if (d == null) return;

        d.ClubName = c.clubName;
        d.ClubShortName = c.clubShortName;
        d.PrimaryColor = c.primaryColor;
        d.SecondaryColor = c.secondaryColor;
        d.FirmName = c.firmName;
        d.RivalClubName = c.rivalClubName;
        // Keep money/progress — only swap identity.
        GameData.instance.SaveData();
        RefreshUI();
    }

    // ── Refresh all UI from live data ─────────────────────────────────────
    public void RefreshUI()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;

        var club = clubRegistry != null ? clubRegistry.FindPlayerClub(d) : null;

        // ── Top bar ───────────────────────────────────────────────────────
        if (moneyText)       moneyText.text       = $"£{d.Money:N0}";
        if (fansText)        fansText.text         = d.Fans.ToString("N0");
        if (reputationText)  reputationText.text   = d.Reputation.ToString();
        if (matchDayText)    matchDayText.text      = $"MD\n{d.MatchDay:00}";

        // Police heat / bribe / lay-low are gameplay-only — hide from dashboard.
        if (policeHeatFill) policeHeatFill.gameObject.SetActive(false);
        if (policeHeatLabel) policeHeatLabel.gameObject.SetActive(false);
        if (policeHeatDescriptionText) policeHeatDescriptionText.gameObject.SetActive(false);
        if (layLowBtn) layLowBtn.gameObject.SetActive(false);
        if (bribePolicePanel) bribePolicePanel.SetActive(false);

        // ── Fan morale indicator ───────────────────────────────────────────
        if (moraleDotImage)
            moraleDotImage.color = ReputationUnlockSystem.GetMoraleColor(d.FanMorale);
        if (moraleText)
            moraleText.text = $"MORALE: {ReputationUnlockSystem.GetMoraleLabel(d.FanMorale)}";

        // ── Firm card ─────────────────────────────────────────────────────
        if (firmShieldImage  && club != null) firmShieldImage.sprite = club.bannerSprite;
        if (firmNameText)        firmNameText.text        = d.FirmName.ToUpper();
        if (clubLocationText)    clubLocationText.text     = d.ClubName;
        if (hooliganRankText)    hooliganRankText.text     = $"#{d.Ranking}";
        if (leaguePositionText)  leaguePositionText.text   = $"{d.Ranking}th / {(d.RivalBots != null ? d.RivalBots.Count + 1 : 20)}";
        if (nextRivalText)       nextRivalText.text        = d.RivalClubName.ToUpper();
        if (nextRivalMatchText)  nextRivalMatchText.text   = $"AWAY · MD {d.MatchDay + 1:00}";

        // Status + record (new)
        if (firmStatusText)
            firmStatusText.text = ReputationUnlockSystem.GetFirmStatusText(d.Ranking, d.Reputation);
        if (firmRecordText)
            firmRecordText.text = $"{d.BattleWins}W - {d.BattleLosses}L";
        if (prestigeBadge)
            prestigeBadge.SetActive(ReputationUnlockSystem.HasPrestigeBadge(d.Reputation));

        // ── Cost hints on action buttons ──────────────────────────────────
        if (recruitCostHint) recruitCostHint.text = d.Money >= 1200 ? "from £1,200" : "LOW FUNDS";
        if (tripCostHint) tripCostHint.text = "COSTS VARY";

        // ── Recent events ─────────────────────────────────────────────────
        RefreshEvents(d.RecentEvents);
    }

    // ── Pulse a text label briefly (heat warning) ─────────────────────────
    private IEnumerator PulseTextBriefly(TextMeshProUGUI label, Color targetColor)
    {
        if (label == null) yield break;
        Color orig = label.color;
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            label.color = Color.Lerp(orig, targetColor, Mathf.PingPong(t * 4f, 1f));
            yield return null;
        }
        label.color = targetColor;
    }

    void RefreshEvents(string[] events)
    {
        if (eventsContainer == null || eventRowPrefab == null) return;

        for (int i = eventsContainer.childCount - 1; i >= 0; i--)
            Destroy(eventsContainer.GetChild(i).gameObject);

        if (events == null || events.Length == 0)
        {
            noEventsInfo?.SetActive(true);
            return;
        }
        noEventsInfo?.SetActive(false);

        int maxEventsToShow = 8;
        foreach (var ev in events)
        {
            maxEventsToShow--;
            if (maxEventsToShow < 0) break;
            var row = Instantiate(eventRowPrefab, eventsContainer);
            var txt = row.GetComponentInChildren<TextMeshProUGUI>();
            if (txt)
            {
                txt.text = ev;
                // Colour-code by emoji prefix
                if (ev.StartsWith("🔴"))      txt.color = new Color(0.9f, 0.25f, 0.25f);
                else if (ev.StartsWith("🟢")) txt.color = new Color(0.2f, 0.85f, 0.2f);
                else if (ev.StartsWith("🟡")) txt.color = new Color(0.95f, 0.75f, 0.1f);
                else if (ev.StartsWith("🔵")) txt.color = new Color(0.3f, 0.65f, 1.0f);
                else if (ev.StartsWith("💡")) txt.color = new Color(0.85f, 0.85f, 0.4f);
                else if (ev.StartsWith("⚡")) txt.color = new Color(0.9f, 0.6f, 0.1f);
                else if (ev.StartsWith("🏆")) txt.color = new Color(1f, 0.84f, 0.0f);
            }
        }
    }

    // ── Lay Low ───────────────────────────────────────────────────────────
    void OnLayLow()
    {
        if (GameData.instance == null) return;
        bool success = GameData.instance.LayLow();
        if (!success)
            Debug.Log("[Dashboard] Not enough money to Lay Low.");
        // RefreshUI is triggered by OnSavingData event automatically
    }

    // ── Panel management ──────────────────────────────────────────────────
    void ShowPanel(GameObject panel)
    {
        HideAllPanels();
        if (panel == null) return;

        if (panel == recruitFansPanel || panel == rankingsPanel)
            UIp.UITweeningInsideScreenViewFrom(
                this,
                panel.GetComponent<RectTransform>(), 
                GetComponent<RectTransform>(), 
                Vector2.left, 
                GameManager.SLIDE_ANIMATION_MULTIPLIER);
        if (panel == planAwayTripPanel || panel == clubFirmInfoPanel)
            UIp.UITweeningInsideScreenViewFrom(
                this,
                panel.GetComponent<RectTransform>(), 
                GetComponent<RectTransform>(), 
                Vector2.right, 
                GameManager.SLIDE_ANIMATION_MULTIPLIER);
    }

    void HideAllPanels()
    {
        HidePanel(recruitFansPanel);
        HidePanel(planAwayTripPanel);
        HidePanel(rankingsPanel);
        HidePanel(clubFirmInfoPanel);
    }

    void HidePanel(GameObject panel)
    {
        if (panel == null || !panel.activeSelf) return;
        if (panel == recruitFansPanel || panel == rankingsPanel)
            UIp.UITweeningOutsideScreenViewFrom(
                this,
                panel.GetComponent<RectTransform>(), 
                GetComponent<RectTransform>(), 
                Vector2.left, 
                GameManager.SLIDE_ANIMATION_MULTIPLIER);
        if (panel == planAwayTripPanel || panel == clubFirmInfoPanel)
            UIp.UITweeningOutsideScreenViewFrom(
                this,
                panel.GetComponent<RectTransform>(), 
                GetComponent<RectTransform>(), 
                Vector2.right, 
                GameManager.SLIDE_ANIMATION_MULTIPLIER);
    }

    // ── End Matchday ──────────────────────────────────────────────────────
    void OnEndMatchDay()
    {
        HideAllPanels();
        GameManager.instance?.OnEndMatchDay();
    }

    void OnMatchDayEnded()
    {
        RefreshUI();
        StartCoroutine(ShowRankChangePopup());
    }

    IEnumerator ShowRankChangePopup()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null || rankChangePopupText == null) yield break;

        int prev = d.PreviousRanking;
        int curr = d.Ranking;
        if (prev == curr) yield break;

        bool climbed = curr < prev;
        rankChangePopupText.text  = climbed
            ? $"↑ CLIMBED TO #{curr}"
            : $"↓ DROPPED TO #{curr}";
        rankChangePopupText.color = climbed
            ? new Color(0.2f, 0.85f, 0.2f)
            : new Color(0.9f, 0.25f, 0.25f);
        rankChangePopupText.gameObject.SetActive(true);

        // Fade in, hold, fade out
        CanvasGroup cg = rankChangePopupText.GetComponent<CanvasGroup>();
        if (cg == null) cg = rankChangePopupText.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        float t = 0f;
        while (t < 0.4f) { t += Time.deltaTime; cg.alpha = t / 0.4f; yield return null; }
        cg.alpha = 1f;

        yield return new WaitForSeconds(2.5f);

        t = 0f;
        while (t < 0.6f) { t += Time.deltaTime; cg.alpha = 1f - t / 0.6f; yield return null; }

        rankChangePopupText.gameObject.SetActive(false);
    }
}
