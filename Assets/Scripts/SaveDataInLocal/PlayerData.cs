using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    [System.Runtime.Serialization.OptionalField] public List<RoadIncidentSave> CityRoadIncidents;
    [System.Runtime.Serialization.OptionalField] public MatchdayStreetState MatchdayStreet;
    [System.Runtime.Serialization.OptionalField] public float HomeMatchdayWait;
    [System.Runtime.Serialization.OptionalField] public int MatchdayTravelCredit;
    [System.Runtime.Serialization.OptionalField] public int LastAwayStockMission;
    [System.Runtime.Serialization.OptionalField] public StockRescueRun ActiveStockRescue;
    [System.Runtime.Serialization.OptionalField] public bool MobileTutorialRequired;
    [System.Runtime.Serialization.OptionalField] public string TutorialAppVersion;
    [System.Runtime.Serialization.OptionalField] public int MobileTutorialVersion;
    [System.Runtime.Serialization.OptionalField] public int MobileTutorialStep;
    [System.Runtime.Serialization.OptionalField] public bool MatchdayTutorialPending;
    [System.Runtime.Serialization.OptionalField] public bool MobileTutorialRecruitDone;
    [System.Runtime.Serialization.OptionalField] public List<string> FulfilledPurchaseTransactions;
    [System.Runtime.Serialization.OptionalField] public bool RemoveAdsEntitlement;
    [System.Runtime.Serialization.OptionalField] public string PurchaseWalletId;
    [System.Runtime.Serialization.OptionalField] public MatchdayTacticsState MatchdayTactics;
    [System.Runtime.Serialization.OptionalField] public bool FirstSessionCoachHidden;
    [System.Runtime.Serialization.OptionalField] public int AdSupplyRewardMatchday;
    [System.Runtime.Serialization.OptionalField] public List<HomeBuildingState> HomeBuildings;
    // Recovery v1 is additive; old campaigns migrate without losing earned state.
    [System.Runtime.Serialization.OptionalField] public int RecoveryVersion;
    [System.Runtime.Serialization.OptionalField] public bool RecoveryGuidedStart;
    [System.Runtime.Serialization.OptionalField] public bool LearnedSelection;
    [System.Runtime.Serialization.OptionalField] public int LearnedMoveOrders;
    [System.Runtime.Serialization.OptionalField] public bool LearnedMultiSelection;
    [System.Runtime.Serialization.OptionalField] public bool LearnedSplitSelection;
    [System.Runtime.Serialization.OptionalField] public bool LearnedGuard;
    [System.Runtime.Serialization.OptionalField] public bool LearnedPatrol;
    [System.Runtime.Serialization.OptionalField] public List<CityBusinessState> CityBusinesses;
    [System.Runtime.Serialization.OptionalField] public float BusinessEconomySeconds;
    [System.Runtime.Serialization.OptionalField] public int BusinessIncomeEarned;
    [System.Runtime.Serialization.OptionalField] public int MatchdayStartedDay;
    [System.Runtime.Serialization.OptionalField] public float MatchdayPhaseSeconds;
    [System.Runtime.Serialization.OptionalField] public List<string> MatchdayCompletedResponses;
    [System.Runtime.Serialization.OptionalField] public float MatchdayStadiumResponseSeconds;
    [System.Runtime.Serialization.OptionalField] public float MatchdayPubResponseSeconds;
    [System.Runtime.Serialization.OptionalField] public List<CityResourceState> CityResourceStates;
    [System.Runtime.Serialization.OptionalField] public List<string> ActiveAwayAgentIds;
    [System.Runtime.Serialization.OptionalField] public float AwayExtractionSeconds;
    [System.Runtime.Serialization.OptionalField] public float HomePressureSeconds;
    [System.Runtime.Serialization.OptionalField] public float MatchdayElapsedSeconds;
    [System.Runtime.Serialization.OptionalField] public List<string> MatchdayFailedResponses;
    [System.Runtime.Serialization.OptionalField] public List<string> MatchdayPenalizedResponses;
    [System.Runtime.Serialization.OptionalField] public int MatchdayLastClosedDay;
    [System.Runtime.Serialization.OptionalField] public List<MatchdayGroupSave> MatchdayGroups;
    [System.Runtime.Serialization.OptionalField] public int MatchdayGroupsDay;
    [System.Runtime.Serialization.OptionalField] public int StrategySupporters;
    [System.Runtime.Serialization.OptionalField] public int SupportCampaigns;
    [System.Runtime.Serialization.OptionalField] public int StrategyVersion;
    [System.Runtime.Serialization.OptionalField] public List<CityDevelopmentProject> CityDevelopment;
    [System.Runtime.Serialization.OptionalField] public float CityEconomySeconds;
    [System.Runtime.Serialization.OptionalField] public float CityRivalGrowthSeconds;
    public int PowerPackagesPurchased;
    [System.Runtime.Serialization.OptionalField] public List<string> SettledRivalFirms;
    /// <summary>Playable side selected before the campaign starts: "Firm" or "Police".</summary>
    [System.Runtime.Serialization.OptionalField] public string PlayerFaction;
    [System.Runtime.Serialization.OptionalField] public List<string> CityCapturedZones;
    [System.Runtime.Serialization.OptionalField] public int HomeTrainingLevel;
    [System.Runtime.Serialization.OptionalField] public bool HomeDefenceCompleted;
    [System.Runtime.Serialization.OptionalField]
    public List<string> LandscapeClaimedMissions;
    [System.Runtime.Serialization.OptionalField]
    public int LandscapeBonusMatchday;
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
    public int  Fans       = 3;
    public int  Strength   = 40;
    public int  Reputation = 10;
    public int  PoliceHeat = 1;
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
    [System.Runtime.Serialization.OptionalField]
    public int LastAwayTripMatchday;
    [System.Runtime.Serialization.OptionalField]
    public List<string> SelectedAwayAgentIds = new List<string>();
    [System.Runtime.Serialization.OptionalField]
    public bool DeploymentSelectionCustomized = false;

    // ── Campaign level (1–5), saved across sessions ───────────────────────
    public int CurrentLevel = 1;
    [System.Runtime.Serialization.OptionalField]
    public int LastTrainingMatchday;

    // ── Live-city RTS operations ledger (resets each matchday) ───────────
    [System.Runtime.Serialization.OptionalField] public int OperationsMatchday;
    [System.Runtime.Serialization.OptionalField] public int CityIntel;
    [System.Runtime.Serialization.OptionalField] public int CitySupplies;
    [System.Runtime.Serialization.OptionalField] public int MatchTickets;
    [System.Runtime.Serialization.OptionalField] public int SocialMomentum;
    [System.Runtime.Serialization.OptionalField] public bool TransportPrepared;
    [System.Runtime.Serialization.OptionalField] public bool StadiumAccessPrepared;
    [System.Runtime.Serialization.OptionalField] public List<string> CompletedCityOperations;

    // ── Five-mission campaign (home + four sequential away operations) ───
    [System.Runtime.Serialization.OptionalField] public int CampaignResourceMission;
    [System.Runtime.Serialization.OptionalField] public List<int> CompletedCampaignMissions;
    [System.Runtime.Serialization.OptionalField] public List<string> CompletedCampaignSteps;
    [System.Runtime.Serialization.OptionalField] public List<string> CampaignRivalKeys;
    [System.Runtime.Serialization.OptionalField] public List<string> CampaignTerritoryKeys;
    [System.Runtime.Serialization.OptionalField] public List<string> CampaignActionKeys;
    [System.Runtime.Serialization.OptionalField] public bool CampaignStartingHeatApplied;
    [System.Runtime.Serialization.OptionalField] public string MatchdayPhase;
    [System.Runtime.Serialization.OptionalField] public bool MatchdayIncidentResolved;
    [System.Runtime.Serialization.OptionalField] public int MatchdayRivalPressure;
    [System.Runtime.Serialization.OptionalField] public int MatchdayPolicePresence;

    // ── Battle session snapshot (leave = keep progress; Try Again = restore) ──
    /// <summary>True while a street battle session is in progress across loads.</summary>
    public bool BattleSessionActive = false;
    /// <summary>Agent IDs present when the current battle session began (Try Again restores these).</summary>
    public List<string> BattleStartAgentIds = new List<string>();
    /// <summary>Recruits already taken per recruitment center (A/B/C) this session.</summary>
    public int[] BattleRecruitSlotsUsed = new int[3];
    /// <summary>Campaign level when the battle session began (Try Again restores this).</summary>
    [System.Runtime.Serialization.OptionalField]
    public int BattleStartLevel = 1;
    /// <summary>Whether the session began in home district (Try Again restores this).</summary>
    [System.Runtime.Serialization.OptionalField]
    public bool BattleStartHomeMode = true;
    /// <summary>Where CONTINUE should return: Home, Away, AwayPlan, or Police.</summary>
    [System.Runtime.Serialization.OptionalField]
    public string LastSessionMode;
    /// <summary>Heat captured at session start, including a valid zero.</summary>
    [System.Runtime.Serialization.OptionalField]
    public int BattleStartPoliceHeat = 1;

    // ── Hooligan Manager — Recent event log ──────────────────────────────
    public string[] RecentEvents = new string[0];
    [System.Runtime.Serialization.OptionalField] public List<CityNewsRecord> CityNews = new List<CityNewsRecord>();

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
    [System.Runtime.Serialization.OptionalField] public int RecruitEchoRemainder;
    [System.Runtime.Serialization.OptionalField] public List<RivalStreetState> RivalStreets;
    public string LastOpponentFought = "";
}
