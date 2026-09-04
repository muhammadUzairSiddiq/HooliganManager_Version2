using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dedicated controller for the "MATCHDAY RESULT" end-of-battle screen.
///
/// Matches the Result.png mockup exactly:
///
///  ┌─────────────────────────────────────────┐
///  │  MATCHDAY RESULT                        │  ← titleHeaderText
///  │  "You took the fight. You got…"         │  ← subtitleText
///  │  ┌─────────────────────────────────┐    │
///  │  │  [VICTORY / DEFEAT / TIME'S UP] │    │  ← outcomeLabel + outcomeBanner
///  │  │  [crest] 4 – 2 [crest]          │    │  ← playerCrestImg, scoreText, enemyCrestImg
///  │  │  SOUTH END        EAST SIDE      │    │  ← playerFirmText, enemyFirmText
///  │  │  HOOLIGANS        MOB            │    │  ← playerSubText,  enemySubText
///  │  │  ─── TRIP SUMMARY ───            │    │
///  │  │  💀 ENEMIES DEFEATED     23      │    │  ← enemiesDefeatedValue
///  │  │  🩹 UNITS LOST            3      │    │  ← unitsLostValue
///  │  │  💵 MONEY EARNED     £1,250      │    │  ← moneyEarnedValue
///  │  │  👑 REPUTATION GAINED   +18      │    │  ← reputationValue
///  │  │  👥 FANS GAINED         +56      │    │  ← fansGainedValue
///  │  │  🛡  POLICE HEAT CHANGE  +6      │    │  ← policeHeatValue
///  │  └─────────────────────────────────┘    │
///  │  [CONTINUE]   [VIEW RANKINGS]           │  ← continueBtn, viewRankingsBtn
///  │  [NEXT MATCHDAY]                        │  ← nextMatchdayBtn
///  └─────────────────────────────────────────┘
///
/// Attach this to the ResultPanel root GameObject in BattleScene.
/// The panel starts inactive; BattleUIController calls Show(data) to activate it.
/// </summary>
public class BattleResultController : MonoBehaviour
{
    // ── Header ────────────────────────────────────────────────────────────
    [Header("Header")]
    [Tooltip("'MATCHDAY RESULT' text at the very top")]
    public TextMeshProUGUI titleHeaderText;

    [Tooltip("Subtitle — e.g. 'YOU TOOK THE FIGHT. YOU GOT THE REWARD.'")]
    public TextMeshProUGUI subtitleText;

    // ── Outcome Banner ────────────────────────────────────────────────────
    [Header("Outcome Banner")]
    [Tooltip("The coloured banner behind VICTORY / DEFEAT / TIME'S UP")]
    public Image           outcomeBannerImage;

    [Tooltip("'VICTORY' / 'DEFEAT' / 'TIME'S UP' large text")]
    public TextMeshProUGUI outcomeLabel;

    [Header("Outcome Banner Colors")]
    public Color victoryColor     = new Color(0.18f, 0.55f, 0.18f, 1f);  // green
    public Color defeatColor      = new Color(0.65f, 0.08f, 0.08f, 1f);  // red
    public Color timerExpiredColor = new Color(0.35f, 0.35f, 0.10f, 1f); // dark yellow

    // ── Score Card ────────────────────────────────────────────────────────
    [Header("Score Card — Firm Crests & Names")]
    public Image           playerCrestImage;
    public Image           enemyCrestImage;

    [Tooltip("e.g. 'SOUTH END'")]
    public TextMeshProUGUI playerFirmText;

    [Tooltip("Sub-name below firm name, e.g. 'HOOLIGANS'")]
    public TextMeshProUGUI playerSubText;

    [Tooltip("e.g. 'EAST SIDE'")]
    public TextMeshProUGUI enemyFirmText;

    [Tooltip("Sub-name below rival firm name, e.g. 'MOB'")]
    public TextMeshProUGUI enemySubText;

    [Tooltip("Centre score, e.g. '4 - 2'")]
    public TextMeshProUGUI scoreText;

    [Header("Registries")]
    public ClubRegistry clubRegistry;
    public BotRegistry  botRegistry;

    // ── Trip Summary Rows ─────────────────────────────────────────────────
    [Header("Trip Summary — Value Labels")]
    [Tooltip("Right-side value: number of enemies killed")]
    public TextMeshProUGUI enemiesDefeatedValue;

    [Tooltip("Right-side value: number of player agents lost")]
    public TextMeshProUGUI unitsLostValue;

    [Tooltip("Right-side value: e.g. '£1,250' — green when positive")]
    public TextMeshProUGUI moneyEarnedValue;

    [Tooltip("Right-side value: e.g. '+18' — green positive, red negative")]
    public TextMeshProUGUI reputationValue;

    [Tooltip("Right-side value: e.g. '+56' — always green")]
    public TextMeshProUGUI fansGainedValue;

    [Tooltip("Right-side value: e.g. '+6' — red (heat is bad)")]
    public TextMeshProUGUI policeHeatValue;

    [Tooltip("Right-side value: fan morale change from this battle.")]
    public TextMeshProUGUI moraleChangeValue;

    [Header("Fan Morale Warning")]
    [Tooltip("'LADS ARE SHAKEN' banner — shown when morale drops below 30.")]
    public GameObject ladsAreShakenbanner;
    public TextMeshProUGUI ladsAreShakenText;

    // ── Buttons ───────────────────────────────────────────────────────────
    [Header("Buttons")]
    [Tooltip("Returns to the main dashboard (GameScene)")]
    public Button continueBtn;

    [Tooltip("Goes directly to the Rankings screen on the dashboard")]
    public Button viewRankingsBtn;

    [Tooltip("Ends the current matchday and returns to dashboard")]
    public Button nextMatchdayBtn;

    // ── Colour helpers ────────────────────────────────────────────────────
    [Header("Value Colors")]
    public Color positiveColor  = new Color(0.20f, 0.85f, 0.20f); // green  — money/rep/fans gained
    public Color negativeColor  = new Color(0.90f, 0.15f, 0.15f); // red    — losses / heat
    public Color neutralColor   = new Color(0.85f, 0.85f, 0.85f); // white-ish — neutral

    // ── Trip Targets section ──────────────────────────────────────────────
    [System.Serializable]
    public class TargetRow
    {
        [Tooltip("The root GameObject of the entire row (used to show/hide it)")]
        public UnityEngine.GameObject rowRoot;

        [Tooltip("Label text — e.g. 'WIN THE MATCH'")]
        public TextMeshProUGUI labelText;

        [Tooltip("Sub-description text below the label (optional, can be null)")]
        public TextMeshProUGUI descriptionText;

        [Tooltip("'✓' or '✗' text shown on the right side of the row")]
        public TextMeshProUGUI statusIcon;
    }

    [Header("Trip Targets Section")]
    [Tooltip("'TRIP TARGETS' section header text")]
    public TextMeshProUGUI tripTargetsHeaderText;

    [Tooltip("Up to 3 target rows. Each row contains label, description, status icon.")]
    public TargetRow[] targetRows = new TargetRow[3];

    [Header("Target Row Colors")]
    public Color targetAchievedColor = new Color(0.20f, 0.85f, 0.20f); // green
    public Color targetFailedColor   = new Color(0.90f, 0.15f, 0.15f); // red

    // ── Internal state ────────────────────────────────────────────────────
    private BattleResultData _lastResult;

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        continueBtn?.onClick.AddListener(OnContinue);
        viewRankingsBtn?.onClick.AddListener(OnViewRankings);
        nextMatchdayBtn?.onClick.AddListener(OnNextMatchday);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Activate and populate the result screen from BattleResultData.
    /// Called by BattleUIController after BattleManager.OnBattleComplete fires.
    /// </summary>
    public void Show(BattleResultData data)
    {
        _lastResult = data;
        gameObject.SetActive(true);

        PopulateHeader(data);
        PopulateOutcomeBanner(data);
        PopulateScoreCard(data);
        PopulateTripSummary(data);
        PopulateTripTargets(data);
    }

    // ── Section populators ────────────────────────────────────────────────

    private void PopulateHeader(BattleResultData data)
    {
        // Contextual narrative subtitle — varies by result, agent losses, and morale.
        if (subtitleText)
        {
            var d = GameData.instance?.PlayerData;
            int morale    = d?.FanMorale ?? 70;
            int unitsLost = data.UnitsLost;

            subtitleText.text = data.Result switch
            {
                GameData.BattleResult.Victory when unitsLost == 0
                    => "THE LADS WERE IMMENSE. WORD WILL SPREAD.",
                GameData.BattleResult.Victory when unitsLost > 0 && morale < 40
                    => "WON THE BATTLE, BUT IT COST US. MORALE IS SHAKEN.",
                GameData.BattleResult.Victory
                    => "YOU TOOK THE FIGHT. YOU GOT THE REWARD.",
                GameData.BattleResult.Defeat when morale >= 50
                    => "THE LADS GAVE EVERYTHING. REGROUP AND COME BACK HARDER.",
                GameData.BattleResult.Defeat when morale < 30
                    => "ANOTHER HAMMERING. SOME LADS ARE THINKING OF WALKING.",
                GameData.BattleResult.Defeat
                    => "YOU GAVE IT YOUR BEST. REGROUP AND COME BACK.",
                _   => "TIME RAN OUT. A SCRAPPY AFFAIR."
            };
        }
        // titleHeaderText ("MATCHDAY RESULT") is static — leave as set in Inspector
    }

    private void PopulateOutcomeBanner(BattleResultData data)
    {
        string label;
        Color  bannerColor;

        switch (data.Result)
        {
            case GameData.BattleResult.Victory:
                label       = "VICTORY";
                bannerColor = victoryColor;
                break;
            case GameData.BattleResult.Defeat:
                label       = "DEFEAT";
                bannerColor = defeatColor;
                break;
            default:
                label       = "TIME'S UP";
                bannerColor = timerExpiredColor;
                break;
        }

        if (outcomeLabel)       outcomeLabel.text  = label;
        if (outcomeBannerImage) outcomeBannerImage.color = bannerColor;
    }

    private void PopulateScoreCard(BattleResultData data)
    {
        // Firm names
        if (playerFirmText) playerFirmText.text = data.PlayerFirmName.ToUpper();
        if (playerSubText)  playerSubText.text  = data.PlayerFirmSubName.ToUpper();
        if (enemyFirmText)  enemyFirmText.text  = data.EnemyFirmName.ToUpper();
        if (enemySubText)   enemySubText.text   = data.EnemyFirmSubName.ToUpper();

        // Score = rounds won by each side (e.g. "2 - 1" out of 3 rounds)
        if (scoreText)
            scoreText.text = $"{data.PlayerRoundsWon} - {data.EnemyRoundsWon}";

        // Show how many rounds were played in the sub-text if desired
        // (sub-texts already set above; override here only when round count is meaningful)
        if (data.TotalRounds > 0)
        {
            if (playerSubText) playerSubText.text = $"ROUNDS WON";
            if (enemySubText)  enemySubText.text  = $"ROUNDS WON";
        }

        // Crests
        Sprite playerLogo = null;
        if (clubRegistry == null)
        {
            clubRegistry = Resources.Load<ClubRegistry>("ClubRegistry");
        }
        if (clubRegistry != null && GameData.instance?.PlayerData != null)
        {
            var playerClub = clubRegistry.FindPlayerClub(GameData.instance.PlayerData);
            if (playerClub != null) playerLogo = playerClub.crestSprite;
        }

        Sprite enemyLogo = null;
        if (botRegistry == null)
        {
            botRegistry = Resources.Load<BotRegistry>("BotRegistry");
        }
        if (botRegistry != null && !string.IsNullOrEmpty(data.EnemyFirmName))
        {
            var rivalBot = botRegistry.FindByFirmName(data.EnemyFirmName);
            if (rivalBot != null) enemyLogo = rivalBot.crestSprite;
        }

        if (playerCrestImage && playerLogo != null)
            playerCrestImage.sprite = playerLogo;
        if (enemyCrestImage && enemyLogo != null)
            enemyCrestImage.sprite = enemyLogo;
    }

    private void PopulateTripSummary(BattleResultData data)
    {
        // Enemies defeated — neutral number
        SetValueLabel(enemiesDefeatedValue,
            data.EnemiesDefeated.ToString(), neutralColor);

        // Units lost — red (bad)
        SetValueLabel(unitsLostValue,
            data.UnitsLost.ToString(), data.UnitsLost > 0 ? negativeColor : neutralColor);

        // Money earned — green positive, red negative
        bool moneyPos = data.MoneyEarned >= 0;
        SetValueLabel(moneyEarnedValue,
            moneyPos ? $"£{data.MoneyEarned:N0}" : $"-£{Mathf.Abs(data.MoneyEarned):N0}",
            moneyPos ? positiveColor : negativeColor);

        // Reputation — green positive, red negative
        bool repPos = data.ReputationGained >= 0;
        SetValueLabel(reputationValue,
            $"{(repPos ? "+" : "")}{data.ReputationGained}",
            repPos ? positiveColor : negativeColor);

        // Fans gained — always green
        SetValueLabel(fansGainedValue,
            $"+{data.FansGained}", positiveColor);

        // Police heat — always red (heat is never good)
        SetValueLabel(policeHeatValue,
            $"+{data.PoliceHeatChange}", negativeColor);

        // Morale change — calculated from ReputationUnlockSystem
        if (moraleChangeValue != null)
        {
            var d          = GameData.instance?.PlayerData;
            bool isVictory = data.Result == GameData.BattleResult.Victory;
            int totalAgents = data.UnitsLost + (GameData.instance?.PlayerData?.RecruitedAgents?.Count ?? 1);
            int moraleDelta = ReputationUnlockSystem.GetMoraleDeltaForBattle(isVictory, data.UnitsLost, totalAgents);
            string sign     = moraleDelta >= 0 ? "+" : "";
            SetValueLabel(moraleChangeValue,
                $"{sign}{moraleDelta}",
                moraleDelta >= 0 ? positiveColor : negativeColor);
        }

        // LADS ARE SHAKEN banner
        if (ladsAreShakenbanner != null)
        {
            var d       = GameData.instance?.PlayerData;
            bool shaken = (d?.FanMorale ?? 70) < 30;
            ladsAreShakenbanner.SetActive(shaken);
            if (shaken && ladsAreShakenText != null)
                ladsAreShakenText.text = $"⚠ LADS ARE SHAKEN — morale critical ({d.FanMorale}/100). Win urgently or lads will walk.";
        }
    }

    // ── Trip Targets populator ────────────────────────────────────────────

    private void PopulateTripTargets(BattleResultData data)
    {
        // Hide all rows first
        if (targetRows != null)
            foreach (var row in targetRows)
                if (row?.rowRoot != null) row.rowRoot.SetActive(false);

        if (data.TripTargets == null || data.TripTargets.Length == 0)
        {
            // No targets — also hide the section header
            if (tripTargetsHeaderText) tripTargetsHeaderText.gameObject.SetActive(false);
            return;
        }

        // Show section header
        if (tripTargetsHeaderText)
        {
            tripTargetsHeaderText.gameObject.SetActive(true);
            string dest = string.IsNullOrEmpty(data.DestinationName)
                ? "TRIP" : data.DestinationName.ToUpper();
            tripTargetsHeaderText.text = $"───  {dest} TARGETS  ───";
        }

        int max = Mathf.Min(data.TripTargets.Length,
                            targetRows != null ? targetRows.Length : 0);

        for (int i = 0; i < max; i++)
        {
            var tgt = data.TripTargets[i];
            var row = targetRows[i];
            if (row == null) continue;

            // Activate row
            if (row.rowRoot != null) row.rowRoot.SetActive(true);

            // Label
            if (row.labelText)
            {
                row.labelText.text  = tgt.Label;
                row.labelText.color = tgt.Achieved ? targetAchievedColor : targetFailedColor;
            }

            // Description (optional)
            if (row.descriptionText)
                row.descriptionText.text = tgt.Description;

            // Status icon ✔ / ✘
            if (row.statusIcon)
            {
                row.statusIcon.text  = tgt.Achieved ? "✔" : "✘";
                row.statusIcon.color = tgt.Achieved ? targetAchievedColor : targetFailedColor;
            }
        }
    }

    // ── Button handlers ───────────────────────────────────────────────────

    private void OnContinue()
    {
        Time.timeScale = 1f;
        GameData.instance?.EndMatchDay();
        GameManager.instance?.ReturnToDashboard();
    }

    private void OnViewRankings()
    {
        Time.timeScale = 1f;
        // Return to dashboard and auto-open Rankings panel
        PlayerPrefs.SetInt("OpenRankingsOnLoad", 1);
        GameData.instance?.EndMatchDay();
        GameManager.instance?.ReturnToDashboard();
    }

    private void OnNextMatchday()
    {
        Time.timeScale = 1f;
        GameData.instance?.EndMatchDay();
        GameManager.instance?.ReturnToDashboard();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetValueLabel(TextMeshProUGUI label, string text, Color color)
    {
        if (label == null) return;
        label.text  = text;
        label.color = color;
    }
}
