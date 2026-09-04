using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    // ── Original fields (do not remove) ──────────────────────────────────
    public bool AcceptedPrivacyPolicy;
    public bool[] HelpInfoSeen = new bool[1];
    public string AccessToken = "";
    public LoginType LoginType = LoginType.None;
    public string PlayerID = "";
    public string PlayerName = "";
    public float MusicVolume = 1;
    public float SFXVolume = 1;
    public string UserProfileToken = "";
    public bool IsReviewSubmitted = false;

    // ── Hooligan Manager — Club / Firm ────────────────────────────────────
    public string ClubName        = "North City FC";
    public string ClubShortName   = "NCFC";
    public string PrimaryColor    = "#D71920";
    public string SecondaryColor  = "#FFFFFF";
    public string FirmName        = "North City Crew";
    public string RivalClubName   = "East Town FC";

    // ── Hooligan Manager — Runtime stats ─────────────────────────────────
    public int  Fans       = 1;
    public int  Strength   = 40;
    public int  Reputation = 10;
    public int  PoliceHeat = 0;
    public int  Ranking    = 5;
    public int  Money      = 5000;
    public int  MatchDay   = 1;
    public int  Wins       = 0;
    public int  Losses     = 0;

    // ── Hooligan Manager — Ranking progression ────────────────────────────
    /// <summary>Ranking at the start of last matchday — used to detect movement.</summary>
    public int  PreviousRanking      = 5;
    /// <summary>Best ranking ever reached — used to gate milestone unlocks.</summary>
    public int  HighestRankingReached = 20;

    // ── Hooligan Manager — Fan morale ─────────────────────────────────────
    /// <summary>
    /// Fan morale (0–100). Falls when agents die, rises on victories.
    /// Low morale makes recruiting harder; critically low morale triggers fan desertion.
    /// </summary>
    public int  FanMorale = 70;
    /// <summary>Running total of agents lost across all battles — shown on firm card.</summary>
    public int  TotalAgentsLostAllTime = 0;

    // ── Hooligan Manager — Police state ──────────────────────────────────
    /// <summary>True when heat ≥ 7 — recruits cost more, some locations become riskier.</summary>
    public bool PoliceWatchlisted = false;
    /// <summary>Money cost of the "Lay Low" action at current heat level (set by PoliceHeatSystem).</summary>
    public int  HeatCooldownCost = 1500;

    // ── Hooligan Manager — Location infamy tracking ───────────────────────
    /// <summary>Count of how many times each destination has been visited. Key = destination name.</summary>
    public SerializableDictionary<string, int> DestinationVisitCounts = new SerializableDictionary<string, int>();

    // ── Hooligan Manager — Pending queued effects ─────────────────────────
    public int PendingFansGain  = 0;
    public int PendingMoneyGain = 0;
    public int PendingHeatGain  = 0;

    // ── Hooligan Manager — Away trip ─────────────────────────────────────
    public string LastSelectedDestination = "";

    // ── Campaign level (1–5), saved across sessions ───────────────────────
    public int CurrentLevel = 1;

    // ── Battle session snapshot (leave = keep progress; Try Again = restore) ──
    /// <summary>True while a street battle session is in progress across loads.</summary>
    public bool BattleSessionActive = false;
    /// <summary>Agent IDs present when the current battle session began (Try Again restores these).</summary>
    public List<string> BattleStartAgentIds = new List<string>();
    /// <summary>Recruits already taken per recruitment center (A/B/C) this session.</summary>
    public int[] BattleRecruitSlotsUsed = new int[3];

    // ── Hooligan Manager — Recent event log ──────────────────────────────
    public string[] RecentEvents = new string[0];

    // ── Combat — Agent roster ─────────────────────────────────────────────
    /// <summary>All agents ever recruited. Use IsAlive to filter active ones.</summary>
    public List<AgentData> RecruitedAgents = new List<AgentData>();

    // ── Combat — Battle history ───────────────────────────────────────────
    public int BattleWins   = 0;
    public int BattleLosses = 0;

    // ── Combat — Pending battle outcome (set by BattleManager, applied on save) ──
    public bool  PendingBattleResult   = false; // true = victory
    public int   PendingBattleReward   = 0;     // money reward
    public int   PendingBattleHeatGain = 0;     // police heat from the fight
    public int   PendingBattleRepGain  = 0;     // reputation gained

    // ── Hooligan Manager — Persistent rival bots ──────────────────────────
    public List<BotData> RivalBots = new List<BotData>();
    public string LastOpponentFought = "";
}
