using UnityEngine;
using System;
using System.Collections.Generic;

public class GameData : MonoBehaviour
{
    public static GameData instance;
    public PlayerData PlayerData;
    public BotRegistry botRegistry;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
            GameObject.DontDestroyOnLoad(this.gameObject);
            LoadData();
        }
    }

    private void Start()
    {
        long millisecondsSinceEpoch = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
        int intValue = (int)millisecondsSinceEpoch;
        Debug.Log($"intValue {intValue}");
        UnityEngine.Random.InitState(intValue);
    }

    public void LoadData()
    {
        PlayerData Player = SaveDataInLocal.DataLoad();
        if (Player != null)
        {
            Debug.Log("local data found");
            PlayerData = Player;

            if (MigrateDisabledPoliceCampaign())
                SaveData();

            // Ensure new fields are initialised for existing saves
            if (PlayerData.DestinationVisitCounts == null)
                PlayerData.DestinationVisitCounts = new SerializableDictionary<string, int>();

            if (PlayerData.RivalBots == null || PlayerData.RivalBots.Count == 0)
            {
                InitializeRivalBots();
                SaveData();
            }
            else
            {
                // Migration: old saves may have nextProgressionMatchDay == 0 for every bot.
                bool migrated = false;
                int stagger = 0;
                foreach (var bot in PlayerData.RivalBots)
                {
                    if (bot.nextProgressionMatchDay == 0)
                    {
                        bot.nextProgressionMatchDay = PlayerData.MatchDay + stagger + UnityEngine.Random.Range(2, 6);
                        stagger = (stagger + 1) % 3;
                        migrated = true;
                    }
                }
                if (migrated) SaveData();
            }
        }
        else
        {
            Debug.Log("No local data found. So creating new data");
            NewGamePlayerData();
        }

        NormalizeCampaignData(save: Player != null);
    }

    private bool MigrateDisabledPoliceCampaign()
    {
        if (PlayerData == null) return false;
        bool isPoliceSave =
            string.Equals(PlayerData.PlayerFaction, "Police", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(PlayerData.ClubName, "City Police", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(PlayerData.FirmName, "CITY POLICE", StringComparison.OrdinalIgnoreCase);
        if (!isPoliceSave) return false;

        PlayerData.PlayerFaction = "Firm";
        PlayerData.ClubName = "North City FC";
        PlayerData.ClubShortName = "NCFC";
        PlayerData.PrimaryColor = "#D71920";
        PlayerData.SecondaryColor = "#FFFFFF";
        PlayerData.FirmName = "North City Crew";
        PlayerData.RivalClubName = "East Town FC";

        if (PlayerData.RecruitedAgents != null)
        {
            for (int i = 0; i < PlayerData.RecruitedAgents.Count; i++)
            {
                var agent = PlayerData.RecruitedAgents[i];
                if (agent == null) continue;
                if (string.IsNullOrEmpty(agent.AgentName) ||
                    agent.AgentName.StartsWith("Officer", StringComparison.OrdinalIgnoreCase))
                    agent.AgentName = $"Member {i + 1:00}";
            }
        }

        AddEventLog("Police side is scheduled for Milestone 3. Save migrated back to the firm campaign.");
        return true;
    }

    // â”€â”€ New Game â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Resets to chosen club stats, generates starting agent roster, and saves.
    /// </summary>
    public void StartNewGame(string clubName, string clubShortName,
                             string primaryColor, string secondaryColor,
                             string firmName, string rivalClubName,
                             int fans, int strength, int reputation,
                             int policeHeat, int ranking, int money)
    {
        PlayerData.ClubName       = clubName;
        PlayerData.ClubShortName  = clubShortName;
        PlayerData.PrimaryColor   = primaryColor;
        PlayerData.SecondaryColor = secondaryColor;
        PlayerData.FirmName       = firmName;
        PlayerData.RivalClubName  = rivalClubName;
        // Default keeps old entry points and existing campaign creation compatible.
        PlayerData.PlayerFaction  = "Firm";

        PlayerData.Fans       = fans;
        PlayerData.Strength   = strength;
        PlayerData.Reputation = reputation;
        PlayerData.PoliceHeat = policeHeat;
        PlayerData.Ranking    = ranking;
        PlayerData.Money      = money;

        PlayerData.MatchDay     = 1;
        PlayerData.LandscapeClaimedMissions = new List<string>();
        PlayerData.LandscapeBonusMatchday = 0;
        PlayerData.Wins         = 0;
        PlayerData.Losses       = 0;
        PlayerData.BattleWins   = 0;
        PlayerData.BattleLosses = 0;
        PlayerData.PendingFansGain  = 0;
        PlayerData.PendingMoneyGain = 0;
        PlayerData.PendingHeatGain  = 0;
        PlayerData.RecentEvents = new string[0];
        PlayerData.FanMorale    = 70;
        PlayerData.TotalAgentsLostAllTime = 0;
        PlayerData.PreviousRanking = ranking;
        PlayerData.HighestRankingReached = ranking;
        PlayerData.PoliceWatchlisted = false;
        PlayerData.DestinationVisitCounts = new SerializableDictionary<string, int>();
        PlayerData.CurrentLevel = 1;
        PlayerData.BattleSessionActive = false;
        PlayerData.BattleStartAgentIds = new List<string>();
        PlayerData.BattleRecruitSlotsUsed = new int[3];
        PlayerData.CityCapturedZones = new List<string>();
        PlayerData.HomeDefenceCompleted = false;
        PlayerData.HomeTrainingLevel = 0;
        PlayerData.PowerPackagesPurchased = 0;
        PlayerData.DeploymentSelectionCustomized = false;

        // Initialize rival bots
        InitializeRivalBots();

        // Generate starting agent roster from initial fan count
        PlayerData.RecruitedAgents = new List<AgentData>();
        GenerateAgentsForFans(fans);
        PlayerData.SelectedAwayAgentIds = new List<string>();
        foreach (var agent in PlayerData.RecruitedAgents)
            if (agent != null && agent.IsAlive)
                PlayerData.SelectedAwayAgentIds.Add(agent.AgentId);

        SaveData();
    }

    // â”€â”€ Agent Generation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static readonly string[] AgentFirstNames =
    {
        "Brick", "Jay", "Snake", "Knuckles", "Tank", "Razor",
        "Bull",  "Ace", "Duke",  "Steel",    "Viper","Grim",
        "Rex",   "Crow","Fang",  "Wolf",     "Axe",  "Shade",
        "Iron",  "Pike","Sledge","Bolt",     "Ghost","Spike"
    };

    /// <summary>
    /// Creates AgentData records for the given fan count and appends to roster.
    /// Called on new game and after Recruit Fans is applied.
    /// </summary>
    public void GenerateAgentsForFans(int count)
    {
        int existingCount = PlayerData.RecruitedAgents.Count;
        for (int i = 0; i < count; i++)
        {
            int nameIndex = (existingCount + i) % AgentFirstNames.Length;
            string name   = AgentFirstNames[nameIndex];
            int portrait  = UnityEngine.Random.Range(0, 4);

            float hp       = UnityEngine.Random.Range(45f, 80f);
            float strength = UnityEngine.Random.Range(7f, 14f);
            float speed    = UnityEngine.Random.Range(2.0f, 3.2f);

            var agent = new AgentData(name, portrait, hp, strength, speed);
            PlayerData.RecruitedAgents.Add(agent);
            if (!PlayerData.DeploymentSelectionCustomized)
                AddAgentToAwaySelection(agent);
        }
    }

    // â”€â”€ Matchday End â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Advances matchday: applies all pending effects, runs all strategy systems, saves.
    /// Orchestrates: fans, money, heat, morale, ranking, advisor tips, rival simulation, bot progression.
    /// </summary>
    public void EndMatchDay()
    {
        var d = PlayerData;
        var events = new List<string>(d.RecentEvents ?? new string[0]);

        // â”€â”€ 1. Apply pending fan gains â†’ generate AgentData â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (d.PendingFansGain > 0)
        {
            GenerateAgentsForFans(d.PendingFansGain);
            d.Fans += d.PendingFansGain;
            events.Insert(0, $"ðŸŸ¢ +{d.PendingFansGain} new lads joined the firm.");
            d.PendingFansGain = 0;
        }

        RivalGrowthSystem.AdvanceMatchday(d);

        // â”€â”€ 2. Apply pending money â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (d.PendingMoneyGain != 0)
        {
            d.Money += d.PendingMoneyGain;
            events.Insert(0, d.PendingMoneyGain > 0
                ? $"ðŸŸ¢ +Â£{d.PendingMoneyGain:N0} earned from away trip."
                : $"ðŸ”´ -Â£{Mathf.Abs(d.PendingMoneyGain):N0} lost.");
            d.PendingMoneyGain = 0;
        }

        // â”€â”€ 3. Apply pending heat â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (d.PendingHeatGain != 0)
        {
            d.PoliceHeat = Mathf.Clamp(d.PoliceHeat + d.PendingHeatGain, 0, 10);
            events.Insert(0, d.PendingHeatGain > 0
                ? $"ðŸ”´ Police spotted near the pub. Heat is now {d.PoliceHeat}/10."
                : $"ðŸŸ¢ Heat cooled down to {d.PoliceHeat}/10.");
            d.PendingHeatGain = 0;
        }

        // â”€â”€ 4. Gradual heat cool-down â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (d.PoliceHeat > 0)
            d.PoliceHeat = Mathf.Max(0, d.PoliceHeat - 1);

        // â”€â”€ 5. Update police watchlist + heat cooldown cost â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        bool watchlistChanged = PoliceHeatSystem.EvaluateWatchlist(d);
        d.HeatCooldownCost    = PoliceHeatSystem.GetLayLowCost(d.PoliceHeat);
        if (watchlistChanged)
        {
            events.Insert(0, d.PoliceWatchlisted
                ? "ðŸ”´ POLICE WATCHLIST â€” firm is under surveillance. Recruiting is harder."
                : "ðŸŸ¢ Off the watchlist. Police pressure easing.");
        }

        // â”€â”€ 6. Fan morale decay / recovery passively â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Morale drifts toward 50 naturally each matchday (+1 if below, -1 if well above)
        if (d.FanMorale < 50)      d.FanMorale = Mathf.Min(50, d.FanMorale + 1);
        else if (d.FanMorale > 75) d.FanMorale = Mathf.Max(75, d.FanMorale - 1);

        // Check for deserters when morale is critically low
        int deserters = ReputationUnlockSystem.GetMoraleDeserterCount(d.FanMorale);
        if (deserters > 0)
        {
            d.Fans = Mathf.Max(1, d.Fans - deserters);
            events.Insert(0, $"ðŸ”´ LADS WALKING OUT â€” {deserters} fan(s) left the firm. Morale is rock bottom.");
        }

        // â”€â”€ 7. Heal agents â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        HealAgentsOnMatchDay(0.30f);

        // â”€â”€ 8. Simulate rival matches for non-fought bots â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        SimulateRivalMatches(d.LastOpponentFought);

        // â”€â”€ 9. Recalculate ranking from live leaderboard data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        int newRanking = RecalculateRanking(d);
        d.Ranking = newRanking;

        // â”€â”€ 10. Ranking progression system â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var rankEvents = RankingProgressionSystem.Evaluate(d, newRanking);
        foreach (var e in rankEvents)
            events.Insert(0, e);

        // â”€â”€ 11. Advisor tips â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var tips = MatchdayAdvisorSystem.GenerateTips(d, d.MatchDay);
        foreach (var tip in tips)
            events.Insert(0, tip);

        // â”€â”€ 12. Advance matchday â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        d.MatchDay++;

        NormalizeCampaignData(save: false);

        // Keep last 8 events (expanded from 5 to accommodate new system entries)
        if (events.Count > 8) events = events.GetRange(0, 8);
        d.RecentEvents = events.ToArray();

        SaveData();
        OnMatchDayEnded?.Invoke();

        // Clear last opponent fought after progression has completed
        d.LastOpponentFought = "";
        SaveData();
    }

    /// <summary>Heal every alive agent by a percentage of their max HP.</summary>
    public void HealAgentsOnMatchDay(float percent)
    {
        foreach (var agent in PlayerData.RecruitedAgents)
            if (agent.IsAlive)
                agent.HealPercent(percent);
    }

    // â”€â”€ Fan / Agent sync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Rebuilds Fans from the live agent roster â€” Fans == number of alive agents.
    /// Call this whenever agents can die or be revived to keep data consistent.
    /// </summary>
    public void SyncFanCountWithAgents()
    {
        if (PlayerData == null || PlayerData.RecruitedAgents == null) return;
        int alive = 0;
        foreach (var a in PlayerData.RecruitedAgents)
            if (a != null && a.IsAlive) alive++;
        PlayerData.Fans = alive;
    }

    public void NormalizeCampaignData(bool save = true)
    {
        if (PlayerData == null) return;
        PlayerData.PoliceHeat = Mathf.Clamp(PlayerData.PoliceHeat, 0, 10);
        if (PlayerData.RecruitedAgents == null)
            PlayerData.RecruitedAgents = new List<AgentData>();
        if (PlayerData.SelectedAwayAgentIds == null)
            PlayerData.SelectedAwayAgentIds = new List<string>();
        if (PlayerData.DestinationVisitCounts == null)
            PlayerData.DestinationVisitCounts = new SerializableDictionary<string, int>();
        if (PlayerData.BattleStartAgentIds == null)
            PlayerData.BattleStartAgentIds = new List<string>();
        if (PlayerData.BattleRecruitSlotsUsed == null || PlayerData.BattleRecruitSlotsUsed.Length < 3)
            PlayerData.BattleRecruitSlotsUsed = new int[3];
        if (PlayerData.CityCapturedZones == null)
            PlayerData.CityCapturedZones = new List<string>();
        if (PlayerData.LandscapeClaimedMissions == null)
            PlayerData.LandscapeClaimedMissions = new List<string>();
        if (PlayerData.CompletedCityOperations == null)
            PlayerData.CompletedCityOperations = new List<string>();
        for (int i = 0; i < PlayerData.RecruitedAgents.Count; i++)
            PlayerData.RecruitedAgents[i]?.EnsureManagementProfile(i);

        SyncFanCountWithAgents();
        SyncAwaySelectionWithRoster();
        if (save) SaveData();
    }

    public void AddAgentToAwaySelection(AgentData agent, int maxSelected = 12)
    {
        if (PlayerData == null || agent == null || !agent.IsAlive || string.IsNullOrEmpty(agent.AgentId)) return;
        if (PlayerData.SelectedAwayAgentIds == null)
            PlayerData.SelectedAwayAgentIds = new List<string>();
        if (PlayerData.SelectedAwayAgentIds.Contains(agent.AgentId)) return;
        if (PlayerData.SelectedAwayAgentIds.Count >= maxSelected) return;
        PlayerData.SelectedAwayAgentIds.Add(agent.AgentId);
    }

    public void SyncAwaySelectionWithRoster(int maxSelected = 12)
    {
        if (PlayerData == null) return;
        if (PlayerData.SelectedAwayAgentIds == null)
            PlayerData.SelectedAwayAgentIds = new List<string>();

        for (int i = PlayerData.SelectedAwayAgentIds.Count - 1; i >= 0; i--)
            if (!IsLivingAgentId(PlayerData.SelectedAwayAgentIds[i]))
                PlayerData.SelectedAwayAgentIds.RemoveAt(i);

        if (PlayerData.DeploymentSelectionCustomized) return;

        PlayerData.SelectedAwayAgentIds.Clear();
        foreach (var agent in PlayerData.RecruitedAgents)
        {
            if (agent == null || !agent.IsAlive) continue;
            if (PlayerData.SelectedAwayAgentIds.Count >= maxSelected) break;
            PlayerData.SelectedAwayAgentIds.Add(agent.AgentId);
        }
    }

    private bool IsLivingAgentId(string agentId)
    {
        if (PlayerData?.RecruitedAgents == null || string.IsNullOrEmpty(agentId)) return false;
        foreach (var agent in PlayerData.RecruitedAgents)
            if (agent != null && agent.IsAlive && agent.AgentId == agentId)
                return true;
        return false;
    }

    /// <summary>
    /// Revives a dead agent for the given cash cost, restoring them to full HP.
    /// Syncs Fans immediately. Returns false if the player can't afford it or
    /// the agent is already alive.
    /// </summary>
    public bool ReviveFan(AgentData agent, int cost)
    {
        return SquadCare.Recover(agent, cost);
    }

    // â”€â”€ Ranking calculation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Recalculates the player's ranking among all bots based on reputation.
    /// Returns the new 1-based rank (1 = best).
    /// </summary>
    public int RecalculateRanking(PlayerData d)
    {
        if (d.RivalBots == null || d.RivalBots.Count == 0) return 1;

        int rank = 1;
        foreach (var bot in d.RivalBots)
        {
            if (bot.reputation > d.Reputation)
                rank++;
        }
        return rank;
    }

    // â”€â”€ Reputation Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Adds street reputation (e.g. wiping a rival gang) and refreshes ranking.
    /// Does not overwrite existing rep with win-rate math.
    /// </summary>
    public void AddReputation(int amount, bool save = true)
    {
        if (amount == 0 || PlayerData == null) return;
        var d = PlayerData;
        d.Reputation = Mathf.Max(0, d.Reputation + amount);
        d.Ranking = RecalculateRanking(d);
        if (d.Ranking < d.HighestRankingReached)
            d.HighestRankingReached = d.Ranking;
        if (save) SaveData();
    }

    private void RecalculatePlayerReputation()
    {
        // Keep additive street reputation â€” do not overwrite with win-rate %.
        if (PlayerData == null) return;
        PlayerData.Ranking = RecalculateRanking(PlayerData);
    }

    private static void RecalculateBotReputation(BotData bot)
    {
        int total = bot.wins + bot.losses;
        bot.reputation = total > 0
            ? Mathf.RoundToInt((float)bot.wins / total * 100f)
            : 0;
    }

    /// <summary>
    /// Restore roster + recruit slots to the state when this battle session began.
    /// Used by TRY AGAIN so mid-run recruits are undone while normal leave keeps them.
    /// </summary>
    public void RestoreBattleSessionSnapshot()
    {
        var d = PlayerData;
        if (d == null) return;

        if (d.BattleStartAgentIds != null && d.BattleStartAgentIds.Count > 0 && d.RecruitedAgents != null)
        {
            var keep = new HashSet<string>(d.BattleStartAgentIds);
            d.RecruitedAgents.RemoveAll(a => a == null || !keep.Contains(a.AgentId));
        }

        if (d.RecruitedAgents != null)
        {
            foreach (var a in d.RecruitedAgents)
            {
                if (a == null) continue;
                if (a.MaxHp <= 1f) a.MaxHp = 60f;
                a.FullHeal();
            }
        }

        d.BattleRecruitSlotsUsed = new int[3];
        // Keep the level the player was actually on (set before restore on Try Again).
        if (d.BattleStartLevel > 0)
            d.CurrentLevel = Mathf.Clamp(d.BattleStartLevel, 1, 5);
        d.PoliceHeat = Mathf.Clamp(d.BattleStartPoliceHeat > 0 ? d.BattleStartPoliceHeat : 7, 0, 10);
        SyncFanCountWithAgents();
        d.DeploymentSelectionCustomized = false;
        SyncAwaySelectionWithRoster();
        d.BattleSessionActive = true; // keep start IDs so Ensure won't re-snapshot mid Try Again
        SaveData();
    }

    /// <summary>
    /// Capture the roster at the start of a battle session (once per session).
    /// Leave/continue keeps recruits; Try Again restores this snapshot.
    /// </summary>
    public void EnsureBattleSessionSnapshot()
    {
        var d = PlayerData;
        if (d == null) return;

        // Always keep mode/level in sync — home→away must not keep a stale HQ flag.
        d.BattleStartLevel = Mathf.Clamp(d.CurrentLevel, 1, 5);
        d.BattleStartHomeMode = CityGameplay.HomeMode;

        if (d.BattleSessionActive && d.BattleStartAgentIds != null && d.BattleStartAgentIds.Count > 0)
        {
            SaveData();
            return;
        }

        d.BattleStartAgentIds = new List<string>();
        if (d.RecruitedAgents != null)
        {
            foreach (var a in d.RecruitedAgents)
                if (a != null && !string.IsNullOrEmpty(a.AgentId))
                    d.BattleStartAgentIds.Add(a.AgentId);
        }

        if (d.BattleRecruitSlotsUsed == null || d.BattleRecruitSlotsUsed.Length < 3)
            d.BattleRecruitSlotsUsed = new int[3];

        d.BattleSessionActive = true;
        d.BattleStartPoliceHeat = 7;
        SaveData();
    }

    /// <summary>Update home/away + level context without wiping the agent snapshot.</summary>
    public void RefreshBattleSessionContext(bool homeMode)
    {
        var d = PlayerData;
        if (d == null) return;
        d.BattleStartHomeMode = homeMode;
        d.BattleStartLevel = Mathf.Clamp(d.CurrentLevel, 1, 5);
        d.BattleStartPoliceHeat = 7;
        // Starting a fresh away trip from HQ should take a new roster snapshot.
        if (!homeMode)
        {
            d.BattleSessionActive = false;
            d.BattleStartAgentIds = new List<string>();
            d.BattleRecruitSlotsUsed = new int[3];
        }
        SaveData();
    }

    public void ClearBattleSessionSnapshot()
    {
        var d = PlayerData;
        if (d == null) return;
        d.BattleSessionActive = false;
        d.BattleStartAgentIds = new List<string>();
        d.BattleRecruitSlotsUsed = new int[3];
        d.BattleStartLevel = Mathf.Clamp(d.CurrentLevel, 1, 5);
        SaveData();
    }

    // â”€â”€ Battle Result â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public enum BattleResult { Victory, Defeat, TimerExpired }

    /// <summary>
    /// Called by BattleManager when a battle ends.
    /// Applies all stat changes, morale, agent deaths, fan adjustments, and saves.
    /// </summary>
    public void OnBattleComplete(BattleResult result, int moneyReward,
                                 int heatGain, int repGain,
                                 List<AgentData> survivingAgents,
                                 List<AgentData> deadAgents,
                                 int fansGained,
                                 string enemyFirmName = "")
    {
        var d = PlayerData;
        var events = new List<string>(d.RecentEvents ?? new string[0]);

        BotData opponentBot = null;
        if (!string.IsNullOrEmpty(enemyFirmName) && d.RivalBots != null)
        {
            opponentBot = d.RivalBots.Find(b => b.firmName.Equals(enemyFirmName, StringComparison.OrdinalIgnoreCase));
        }

        d.LastOpponentFought = enemyFirmName;

        // â”€â”€ 1. Sync surviving agent HP â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        foreach (var battleAgent in survivingAgents)
        {
            var stored = d.RecruitedAgents.Find(a => a.AgentId == battleAgent.AgentId);
            if (stored != null) stored.CurrentHp = battleAgent.CurrentHp;
        }

        // â”€â”€ 2. Mark dead agents â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        int agentsLost = 0;
        int maxDeathsAllowed = Mathf.Max(0, d.Fans - 1);
        foreach (var battleAgent in deadAgents)
        {
            var stored = d.RecruitedAgents.Find(a => a.AgentId == battleAgent.AgentId);
            if (stored != null)
            {
                if (agentsLost < maxDeathsAllowed)
                {
                    stored.CurrentHp = 0f;
                    agentsLost++;
                }
                else
                {
                    // Save the agent to ensure they stay synced with clamped Fans (at least 1)
                    stored.CurrentHp = 15f;
                }
            }
        }
        if (agentsLost > 0)
        {
            d.Fans = Mathf.Max(1, d.Fans - agentsLost);
            d.TotalAgentsLostAllTime += agentsLost;
            Debug.Log($"[GameData] {agentsLost} agent(s) lost â€” Fans now {d.Fans}.");
        }

        // â”€â”€ 3. Calculate morale delta â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        int totalAgents = survivingAgents.Count + deadAgents.Count;
        bool isVictory  = result == BattleResult.Victory;
        int moraleDelta = ReputationUnlockSystem.GetMoraleDeltaForBattle(isVictory, agentsLost, totalAgents);
        d.FanMorale = Mathf.Clamp(d.FanMorale + moraleDelta, 0, 100);

        // â”€â”€ 4. Apply result-dependent stat changes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        bool wasPoliceRaid = (GameManager.PendingBattleMode == BattleManager.BattleMode.PoliceRaid)
                           || (heatGain == 3);

        switch (result)
        {
            case BattleResult.Victory:
                d.Money      += moneyReward;
                d.BattleWins++;
                d.Wins++;

                if (wasPoliceRaid)
                {
                    d.PoliceHeat = Mathf.Max(5, d.PoliceHeat - 2);
                    Debug.Log("[GameData] Police Raid victory â€” PoliceHeat reduced, not cleared.");
                    events.Insert(0, agentsLost > 0
                        ? $"ðŸŸ¢ Police raid beaten! Heat cooled to {d.PoliceHeat}/10. {agentsLost} lad(s) down."
                        : $"ðŸŸ¢ Police raid beaten! Heat cooled to {d.PoliceHeat}/10. The firm escaped clean.");
                }
                else
                {
                    d.PoliceHeat = Mathf.Clamp(d.PoliceHeat + heatGain, 0, 10);
                    string moraleLine = moraleDelta > 0 ? $" Morale +{moraleDelta}." : "";
                    string lossNote   = agentsLost > 0 ? $" {agentsLost} lad(s) lost." : "";
                    events.Insert(0, $"ðŸŸ¢ Victory! +Â£{moneyReward:N0} Â· Rep {d.Reputation}.{lossNote}{moraleLine}");
                }

                RecalculatePlayerReputation();

                if (fansGained > 0)
                {
                    d.PendingFansGain += fansGained;
                    RivalGrowthSystem.NoteQueuedRecruits(fansGained);
                    d.Fans            += fansGained;
                }

                if (opponentBot != null)
                {
                    opponentBot.losses++;

                    // Bot loses half of what the player gained (they poached those lads)
                    int lostFans = Mathf.Clamp(fansGained / 2, 1, opponentBot.fans - 1);
                    opponentBot.fans = Mathf.Max(1, opponentBot.fans - lostFans);

                    // Bot rallies and recruits after the loss â€” grows stronger for next round.
                    // Recovery = fans the match was worth (fansGained) + 1 per 5 remaining fans.
                    // This means bigger bots bounce back harder, keeping them a real threat.
                    int botRecovery = fansGained + Mathf.Max(1, opponentBot.fans / 5);
                    opponentBot.fans = Mathf.Min(opponentBot.fans + botRecovery, 200);

                    Debug.Log($"[GameData] {opponentBot.firmName} lost {lostFans} fans but rallied +{botRecovery} â†’ now {opponentBot.fans} fans.");

                    RecalculateBotReputation(opponentBot);
                }
                break;

            case BattleResult.Defeat:
                d.Money       = Mathf.Max(0, d.Money - moneyReward / 2);
                d.BattleLosses++;
                d.Losses++;

                if (wasPoliceRaid)
                {
                    d.PoliceHeat = 10;
                    Debug.Log("[GameData] Police Raid defeat â€” PoliceHeat stays at 10.");
                    events.Insert(0, agentsLost > 0
                        ? $"ðŸ”´ Raided by the filth! {agentsLost} lad(s) nicked. Heat maxed."
                        : "ðŸ”´ Raided by the filth! The lads scattered. Heat maxed.");
                }
                else
                {
                    d.PoliceHeat = Mathf.Clamp(d.PoliceHeat + heatGain, 0, 10);
                    string moraleLine = $" Morale {moraleDelta}.";
                    events.Insert(0, agentsLost > 0
                        ? $"ðŸ”´ Defeat. {agentsLost} lad(s) didn't make it back.{moraleLine}"
                        : $"ðŸ”´ Defeat. The lads took a hammering.{moraleLine}");
                }

                RecalculatePlayerReputation();

                if (opponentBot != null)
                {
                    opponentBot.wins++;
                    // Bot gains only 1â€“2 fans from beating the player â€” keep growth slow
                    // so the player is motivated to recruit rather than being overwhelmed.
                    int gainedFans = UnityEngine.Random.Range(1, 3);
                    opponentBot.fans = Mathf.Min(opponentBot.fans + gainedFans, 200);
                    RecalculateBotReputation(opponentBot);
                }
                break;

            case BattleResult.TimerExpired:
                d.Money += moneyReward / 4;

                if (wasPoliceRaid)
                {
                    d.PoliceHeat = Mathf.Max(0, d.PoliceHeat - 3);
                    Debug.Log($"[GameData] Police Raid draw â€” PoliceHeat reduced to {d.PoliceHeat}.");
                    events.Insert(0, agentsLost > 0
                        ? $"ðŸŸ¡ Raid stalemate. Heat cooled. {agentsLost} lad(s) down."
                        : "ðŸŸ¡ Raid stalemate. Managed to slip away. Heat cooled.");
                }
                else
                {
                    d.PoliceHeat = Mathf.Clamp(d.PoliceHeat + heatGain, 0, 10);
                    events.Insert(0, agentsLost > 0
                        ? $"ðŸŸ¡ Time ran out â€” scrappy result. {agentsLost} lad(s) down."
                        : "ðŸŸ¡ Time ran out â€” scrappy result.");
                }
                break;
        }

        // â”€â”€ Morale critical warning â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (d.FanMorale < 30 && !events.Exists(e => e.Contains("LADS ARE SHAKEN")))
            events.Insert(0, "ðŸ”´ LADS ARE SHAKEN â€” morale is dangerously low.");

        if (events.Count > 8) events = events.GetRange(0, 8);
        d.RecentEvents = events.ToArray();

        NormalizeCampaignData(save: false);

        // Apply accumulated street reputation from the session (was previously unused).
        if (repGain != 0)
            AddReputation(repGain, save: false);
        else
            d.Ranking = RecalculateRanking(d);

        SaveData();
        OnBattleEnded?.Invoke(result);
    }

    // â”€â”€ "Lay Low" Action â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Called when the player uses the "Lay Low" action on the dashboard.
    /// Deducts money and reduces heat immediately.
    /// Returns false if the player can't afford it.
    /// </summary>
    public bool LayLow()
    {
        var d = PlayerData;
        int cost = PoliceHeatSystem.GetLayLowCost(d.PoliceHeat);
        if (d.Money < cost) return false;

        int reduction = PoliceHeatSystem.GetLayLowReduction(d.PoliceHeat);
        d.Money -= cost;
        d.PoliceHeat = Mathf.Max(0, d.PoliceHeat - reduction);

        var events = new List<string>(d.RecentEvents ?? new string[0]);
        events.Insert(0, $"ðŸŸ¢ Firm laid low â€” spent Â£{cost:N0}, heat -{reduction} (now {d.PoliceHeat}/10).");
        if (events.Count > 8) events = events.GetRange(0, 8);
        d.RecentEvents = events.ToArray();

        PoliceHeatSystem.EvaluateWatchlist(d);
        d.HeatCooldownCost = PoliceHeatSystem.GetLayLowCost(d.PoliceHeat);

        SaveData();
        OnSavingData?.Invoke();
        return true;
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void InitializeRivalBots()
    {
        if (PlayerData == null) return;
        PlayerData.RivalBots = new List<BotData>();

        BotRegistry reg = botRegistry;
        if (reg == null)
            reg = Resources.Load<BotRegistry>("BotRegistry");

        if (reg != null)
        {
            int offset = 0;
            foreach (var b in reg.bots)
            {
                int interval = UnityEngine.Random.Range(2, 6);
                int nextMD   = PlayerData.MatchDay + offset + interval;
                offset = (offset + 1) % 3;

                PlayerData.RivalBots.Add(new BotData
                {
                    clubName       = b.clubName,
                    clubShortName  = b.clubShortName,
                    firmName       = b.firmName,
                    primaryColor   = b.primaryColor,
                    secondaryColor = b.secondaryColor,
                    fans           = b.fans,
                    strength       = b.strength,
                    reputation     = b.reputation,
                    wins           = b.wins,
                    losses         = b.losses,
                    difficultyTier = b.difficultyTier,
                    archetype      = b.archetype,
                    motto          = b.motto,
                    policeResistance  = b.policeResistance,
                    recruitEfficiency = b.recruitEfficiency,
                    brawlBonus        = b.brawlBonus,
                    nextProgressionMatchDay = nextMD
                });
            }
        }
        else
        {
            // Hardcoded fallback matching default BotRegistry entries
            var fallbacks = new List<BotData>
            {
                new BotData { clubName="East End FC",    clubShortName="EEFC", firmName="East End Crew",    primaryColor="#1A3E8C", secondaryColor="#FFFFFF", fans=1, strength=24, reputation=73, wins=28, losses=10, difficultyTier=DifficultyTier.Hard,   archetype=FirmArchetype.Brawler,   motto="We hit first, we hit hardest.",                policeResistance=4, recruitEfficiency=5, brawlBonus=9, nextProgressionMatchDay=3 },
                new BotData { clubName="West Lions FC",  clubShortName="WLFC", firmName="West Lions",       primaryColor="#C8A800", secondaryColor="#1A1A1A", fans=1, strength=25, reputation=59, wins=22, losses=15, difficultyTier=DifficultyTier.Hard,   archetype=FirmArchetype.Veteran,   motto="Been doing this since before you were born.",  policeResistance=7, recruitEfficiency=4, brawlBonus=8, nextProgressionMatchDay=5 },
                new BotData { clubName="Green Street FC",clubShortName="GSFC", firmName="Green Street Boys",primaryColor="#2E8B3A", secondaryColor="#FFFFFF", fans=1, strength=19, reputation=68, wins=26, losses=12, difficultyTier=DifficultyTier.Medium, archetype=FirmArchetype.Tactical,  motto="We don't lose our heads. We take yours.",      policeResistance=6, recruitEfficiency=6, brawlBonus=5, nextProgressionMatchDay=2 },
                new BotData { clubName="Northside FC",   clubShortName="NSFC", firmName="Northside Boys",   primaryColor="#D71920", secondaryColor="#FFFFFF", fans=1, strength=20, reputation=51, wins=19, losses=18, difficultyTier=DifficultyTier.Easy,   archetype=FirmArchetype.Ambitious, motto="We're hungry. We're coming for everyone.",     policeResistance=3, recruitEfficiency=9, brawlBonus=4, nextProgressionMatchDay=4 },
                new BotData { clubName="South City FC",  clubShortName="SCFC", firmName="South City Firm",  primaryColor="#555555", secondaryColor="#CCCCCC", fans=1, strength=17, reputation=47, wins=18, losses=20, difficultyTier=DifficultyTier.Easy,   archetype=FirmArchetype.Slippery,  motto="You can't catch what you can't see.",          policeResistance=9, recruitEfficiency=5, brawlBonus=3, nextProgressionMatchDay=6 }
            };
            PlayerData.RivalBots.AddRange(fallbacks);
        }
    }

    private void SimulateRivalMatches(string opponentFought)
    {
        if (PlayerData == null || PlayerData.RivalBots == null) return;

        foreach (var bot in PlayerData.RivalBots)
        {
            if (!string.IsNullOrEmpty(opponentFought) && bot.firmName.Equals(opponentFought, StringComparison.OrdinalIgnoreCase))
                continue;

            bool botWins = UnityEngine.Random.value > 0.5f;
            if (botWins)
            {
                bot.wins++;
                // Simulated wins give a very small fan bump â€” max +1 per round
                // so rivals grow slowly and the player always has time to recruit.
                // Ambitious bots have a 50% chance of gaining 1 fan instead of 0.
                int fanGain = (bot.archetype == FirmArchetype.Ambitious)
                    ? UnityEngine.Random.Range(0, 2)   // 0 or 1
                    : (UnityEngine.Random.value > 0.7f ? 1 : 0);  // ~30% chance of +1
                if (fanGain > 0)
                    bot.fans = Mathf.Min(bot.fans + fanGain, 200);
            }
            else
            {
                bot.losses++;
                // Fan loss on defeat is rare â€” only Slippery bots truly bleed fans
                bool loseFan = bot.archetype == FirmArchetype.Slippery
                    ? UnityEngine.Random.value > 0.5f
                    : UnityEngine.Random.value > 0.8f;   // ~20% chance otherwise
                if (loseFan)
                    bot.fans = Mathf.Max(1, bot.fans - 1);
            }

            RecalculateBotReputation(bot);
        }
    }

    private void NewGamePlayerData()
    {
        PlayerData = new PlayerData();
        InitializeRivalBots();
        GenerateAgentsForFans(PlayerData.Fans);
        NormalizeCampaignData(save: false);
    }

    public void AddEventLog(string text)
    {
        if (PlayerData == null) return;
        var events = new List<string>(PlayerData.RecentEvents ?? new string[0]);
        events.Insert(0, text);
        if (events.Count > 8) events = events.GetRange(0, 8);
        PlayerData.RecentEvents = events.ToArray();
    }

    public void SaveData()
    {
        NormalizeCampaignData(save: false);
        SaveDataInLocal.DataSave(PlayerData);
        OnSavingData?.Invoke();
    }

    // â”€â”€ Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static Action OnSavingData;
    public static Action OnMatchDayEnded;
    public static Action<BattleResult> OnBattleEnded;
}
