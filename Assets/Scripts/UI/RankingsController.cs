using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pastoral;

/// <summary>
/// Rankings screen controller.
/// Shows "Your Club" summary card and a ranked leaderboard table.
/// Player's row is highlighted in red (primaryColor).
///
/// Strategy Overhaul additions:
///  • Rank change arrows (↑ / ↓) per firm between matchdays.
///  • Milestone badges on threshold rows (#1, #3, #5).
///  • Challenger indicator on rivals that have overtaken the player's rep.
///  • Tooltip on player row: current unlocked perks based on HighestRankingReached.
///
/// Inspector: wire yourClub fields, leaderboard container, rowPrefab, backBtn.
/// </summary>
public class RankingsController : MonoBehaviour
{
    // ── Leaderboard entry ─────────────────────────────────────────────────
    [System.Serializable]
    public class LeaderboardEntry
    {
        public string firmName;
        public int    reputation;
        public int    wins;
        public Sprite logo;
        public bool   isPlayer;
        public bool   isChallenger;   // rival whose rep surpassed the player
        public int    previousRank;   // for movement arrows
        public string archetype;      // displayed as a sub-tag
    }

    [Header("Your Club Summary Card")]
    public TextMeshProUGUI yourFirmNameText;
    public TextMeshProUGUI yourRankText;
    public TextMeshProUGUI yourReputationText;
    public TextMeshProUGUI yourFansText;
    public Image           yourFirmLogo;
    [Tooltip("Unlock info based on HighestRankingReached (e.g. 'TOP 5 — Tier-2 recruits available').")]
    public TextMeshProUGUI yourUnlockStatusText;
    [Tooltip("Fan morale shown on the player card.")]
    public TextMeshProUGUI yourMoraleText;

    [Header("Leaderboard")]
    public Transform   leaderboardContainer;
    public GameObject  leaderboardRowPrefab;

    [Header("Club & Bot Registries (for logos/crests)")]
    public ClubRegistry clubRegistry;
    public BotRegistry  botRegistry;

    [Header("Back Button")]
    public ButtonUI backBtn;

    // ── Highlight colours ─────────────────────────────────────────────────
    [Header("Highlight Colors")]
    public Color playerRowColor     = new Color(0.65f, 0.08f, 0.08f, 1f);
    public Color defaultRowColor    = new Color(0.12f, 0.14f, 0.18f, 1f);
    public Color challengerRowColor = new Color(0.55f, 0.15f, 0.05f, 1f); // orange-red for challengers
    public Color milestoneColor     = new Color(0.8f,  0.7f,  0.05f, 1f); // gold for milestone rows

    void Start()
    {
        backBtn?.AfterClickAnimation.AddListener(OnBack);
        RefreshLeaderboard();
    }

    void OnEnable() => RefreshLeaderboard();

    void RefreshLeaderboard()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;

        Sprite playerLogo = null;
        if (clubRegistry != null)
        {
            var playerClub = clubRegistry.FindPlayerClub(d);
            if (playerClub != null) playerLogo = playerClub.crestSprite;
        }

        // ── Update Your Club card ─────────────────────────────────────────
        if (yourFirmNameText)   yourFirmNameText.text   = d.FirmName.ToUpper();
        if (yourRankText)       yourRankText.text        = $"#{d.Ranking}";
        if (yourReputationText) yourReputationText.text  = d.Reputation.ToString("N0");
        if (yourFansText)       yourFansText.text        = d.Fans.ToString("N0");
        if (yourFirmLogo && playerLogo != null) yourFirmLogo.sprite = playerLogo;
        if (yourMoraleText)
        {
            yourMoraleText.text  = $"MORALE: {ReputationUnlockSystem.GetMoraleLabel(d.FanMorale)}";
            yourMoraleText.color = ReputationUnlockSystem.GetMoraleColor(d.FanMorale);
        }

        // ── Unlock status based on best ranking ever reached ──────────────
        if (yourUnlockStatusText)
        {
            if (RankingProgressionSystem.HasReachedRank1(d))
                yourUnlockStatusText.text = "🏆 #1 ACHIEVED — all perks unlocked.";
            else if (RankingProgressionSystem.HasReachedTop3(d))
                yourUnlockStatusText.text = "⚡ TOP 3 REACHED — Hard rivals engaged.";
            else if (RankingProgressionSystem.HasReachedTop5(d))
                yourUnlockStatusText.text = "📈 TOP 5 REACHED — Better recruits available.";
            else
                yourUnlockStatusText.text = $"Reach Top 5 to unlock better recruits. (Need rank ≤5, currently #{d.Ranking})";
        }

        // ── Build leaderboard ─────────────────────────────────────────────
        var all = new List<LeaderboardEntry>();

        if (d.RivalBots != null)
        {
            foreach (var bot in d.RivalBots)
            {
                Sprite botLogo = null;
                if (botRegistry != null)
                {
                    var entry = botRegistry.FindByFirmName(bot.firmName);
                    if (entry != null) botLogo = entry.crestSprite;
                }

                all.Add(new LeaderboardEntry
                {
                    firmName     = bot.firmName,
                    reputation   = bot.reputation,
                    wins         = bot.wins,
                    logo         = botLogo,
                    isPlayer     = false,
                    isChallenger = bot.isChallenger,
                    archetype    = bot.archetype.ToString().ToUpper()
                });
            }
        }

        // Player entry
        all.Add(new LeaderboardEntry
        {
            firmName   = d.FirmName,
            reputation = d.Reputation,
            wins       = d.Wins,
            logo       = playerLogo,
            isPlayer   = true,
            archetype  = "YOUR FIRM"
        });

        // Sort by reputation descending
        all.Sort((a, b) => b.reputation.CompareTo(a.reputation));

        // ── Clear old rows ────────────────────────────────────────────────
        for (int i = leaderboardContainer.childCount - 1; i >= 0; i--)
            Destroy(leaderboardContainer.GetChild(i).gameObject);

        // ── Spawn rows ────────────────────────────────────────────────────
        int playerNewRank = d.Ranking;

        for (int i = 0; i < all.Count; i++)
        {
            var entry = all[i];
            int rank  = i + 1;

            // Milestone badge logic
            bool isMilestone = rank == 1 || rank == 3 || rank == 5;

            // Row background colour
            Color rowColor = entry.isPlayer       ? playerRowColor    :
                             entry.isChallenger   ? challengerRowColor :
                             isMilestone          ? milestoneColor     :
                                                    defaultRowColor;

            var row   = Instantiate(leaderboardRowPrefab, leaderboardContainer);
            var rowUI = row.GetComponent<LeaderboardRowUI>();
            if (rowUI != null)
            {
                // Build display name with rank arrow and challenger badge
                string displayName = entry.firmName;

                // Rank change arrow (compare against previous ranking for player)
                if (entry.isPlayer && d.PreviousRanking > 0)
                {
                    if (rank < d.PreviousRanking)      displayName = "↑ " + displayName;
                    else if (rank > d.PreviousRanking) displayName = "↓ " + displayName;
                    playerNewRank = rank;
                }

                // Challenger badge
                if (entry.isChallenger) displayName = "⚡ " + displayName;

                // Milestone badge
                string milestoneTag = isMilestone ? GetMilestoneLabel(rank) : "";

                rowUI.Setup(rank, displayName + milestoneTag, entry.reputation, entry.wins,
                            entry.logo, rowColor);
            }

            // Update player rank live
            if (entry.isPlayer && d.Ranking != rank)
            {
                d.Ranking = rank;
                GameData.instance.SaveData();
            }
        }
    }

    private string GetMilestoneLabel(int rank)
    {
        return rank switch
        {
            1 => " 🏆",
            3 => " ⚡",
            5 => " 📈",
            _ => ""
        };
    }

    public void OnBack()
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
}
