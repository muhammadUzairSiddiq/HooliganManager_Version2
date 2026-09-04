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
            SaveData();
        }

        // Ensure Fans count always reflects alive agents on load
        SyncFanCountWithAgents();
    }

    // ── New Game ──────────────────────────────────────────────────────────

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

        PlayerData.Fans       = fans;
        PlayerData.Strength   = strength;
        PlayerData.Reputation = reputation;
        PlayerData.PoliceHeat = policeHeat;
        PlayerData.Ranking    = ranking;
        PlayerData.Money      = money;

        PlayerData.MatchDay     = 1;
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

        // Initialize rival bots
        InitializeRivalBots();

        // Generate starting agent roster from initial fan count
        PlayerData.RecruitedAgents = new List<AgentData>();
        GenerateAgentsForFans(fans);

        SaveData();
    }

    // ── Agent Generation ──────────────────────────────────────────────────

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

            PlayerData.RecruitedAgents.Add(
                new AgentData(name, portrait, hp, strength, speed));
        }
    }

    // ── Matchday End ──────────────────────────────────────────────────────

    /// <summary>
    /// Advances matchday: applies all pending effects, runs all strategy systems, saves.
    /// Orchestrates: fans, money, heat, morale, ranking, advisor tips, rival simulation, bot progression.
    /// </summary>
    public void EndMatchDay()
    {
        var d = PlayerData;
        var events = new List<string>(d.RecentEvents ?? new string[0]);

        // ── 1. Apply pending fan gains → generate AgentData ───────────────
        if (d.PendingFansGain > 0)
        {
            GenerateAgentsForFans(d.PendingFansGain);
            d.Fans += d.PendingFansGain;
            events.Insert(0, $"🟢 +{d.PendingFansGain} new lads joined the firm.");
            d.PendingFansGain = 0;
        }

        // ── 2. Apply pending money ────────────────────────────────────────
        if (d.PendingMoneyGain != 0)
        {
            d.Money += d.PendingMoneyGain;
            events.Insert(0, d.PendingMoneyGain > 0
                ? $"🟢 +£{d.PendingMoneyGain:N0} earned from away trip."
                : $"🔴 -£{Mathf.Abs(d.PendingMoneyGain):N0} lost.");
            d.PendingMoneyGain = 0;
        }

        // ── 3. Apply pending heat ─────────────────────────────────────────
        if (d.PendingHeatGain != 0)
        {
            d.PoliceHeat = Mathf.Clamp(d.PoliceHeat + d.PendingHeatGain, 0, 10);
            events.Insert(0, d.PendingHeatGain > 0
                ? $"🔴 Police spotted near the pub. Heat is now {d.PoliceHeat}/10."
                : $"🟢 Heat cooled down to {d.PoliceHeat}/10.");
            d.PendingHeatGain = 0;
        }

        // ── 4. Gradual heat cool-down ─────────────────────────────────────
        if (d.PoliceHeat > 0)
            d.PoliceHeat = Mathf.Max(0, d.PoliceHeat - 1);

        // ── 5. Update police watchlist + heat cooldown cost ───────────────
        bool watchlistChanged = PoliceHeatSystem.EvaluateWatchlist(d);
        d.HeatCooldownCost    = PoliceHeatSystem.GetLayLowCost(d.PoliceHeat);
        if (watchlistChanged)
        {
            events.Insert(0, d.PoliceWatchlisted
                ? "🔴 POLICE WATCHLIST — firm is under surveillance. Recruiting is harder."
                : "🟢 Off the watchlist. Police pressure easing.");
        }

        // ── 6. Fan morale decay / recovery passively ──────────────────────
        // Morale drifts toward 50 naturally each matchday (+1 if below, -1 if well above)
        if (d.FanMorale < 50)      d.FanMorale = Mathf.Min(50, d.FanMorale + 1);
        else if (d.FanMorale > 75) d.FanMorale = Mathf.Max(75, d.FanMorale - 1);

        // Check for deserters when morale is critically low
        int deserters = ReputationUnlockSystem.GetMoraleDeserterCount(d.FanMorale);
        if (deserters > 0)
        {
            d.Fans = Mathf.Max(1, d.Fans - deserters);
            events.Insert(0, $"🔴 LADS WALKING OUT — {deserters} fan(s) left the firm. Morale is rock bottom.");
        }

        // ── 7. Heal agents ────────────────────────────────────────────────
        HealAgentsOnMatchDay(0.30f);

        // ── 8. Simulate rival matches for non-fought bots ─────────────────
        SimulateRivalMatches(d.LastOpponentFought);

        // ── 9. Recalculate ranking from live leaderboard data ─────────────
        int newRanking = RecalculateRanking(d);
        d.Ranking = newRanking;

        // ── 10. Ranking progression system ────────────────────────────────
        var rankEvents = RankingProgressionSystem.Evaluate(d, newRanking);
        foreach (var e in rankEvents)
            events.Insert(0, e);

        // ── 11. Advisor tips ──────────────────────────────────────────────
        var tips = MatchdayAdvisorSystem.GenerateTips(d, d.MatchDay);
        foreach (var tip in tips)
            events.Insert(0, tip);

        // ── 12. Advance matchday ──────────────────────────────────────────
        d.MatchDay++;

        // Keep fans in sync with alive agents before saving
        SyncFanCountWithAgents();

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

    // ── Fan / Agent sync ───────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds Fans from the live agent roster — Fans == number of alive agents.
    /// Call this whenever agents can die or be revived to keep data consistent.
    /// </summary>
    public void SyncFanCountWithAgents()
    {
        if (PlayerData == null || PlayerData.RecruitedAgents == null) return;
        int alive = 0;
        foreach (var a in PlayerData.RecruitedAgents)
            if (a.IsAlive) alive++;
        // Clamp to at least 1 so the player is never fully zeroed out
        PlayerData.Fans = Mathf.Max(1, alive);
    }

    /// <summary>
    /// Revives a dead agent for the given cash cost, restoring them to full HP.
    /// Syncs Fans immediately. Returns false if the player can't afford it or
    /// the agent is already alive.
    /// </summary>
    public bool ReviveFan(AgentData agent, int cost)
    {
        var d = PlayerData;
        if (d == null || agent == null) return false;
        if (agent.IsAlive) return false;       // already alive
        if (d.Money < cost) return false;      // can't afford

        d.Money     -= cost;
        agent.CurrentHp = agent.MaxHp * 0.5f; // revive at 50 % HP
        SyncFanCountWithAgents();

        var events = new System.Collections.Generic.List<string>(d.RecentEvents ?? new string[0]);
        events.Insert(0, $"🟢 {agent.AgentName} patched up and back in the firm. (£{cost:N0} spent)");
        if (events.Count > 8) events = events.GetRange(0, 8);
        d.RecentEvents = events.ToArray();

        SaveData();
        OnSavingData?.Invoke();
        Debug.Log($"[GameData] Revived {agent.AgentName} for £{cost:N0}. Fans now {d.Fans}.");
        return true;
    }

    // ── Ranking calculation ───────────────────────────────────────────────

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

    // ── Reputation Helpers ────────────────────────────────────────────────

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
        // Keep additive street reputation — do not overwrite with win-rate %.
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
        d.CurrentLevel = 1;
        d.Fans = Mathf.Max(1, d.RecruitedAgents?.Count ?? 1);
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

        if (d.BattleSessionActive && d.BattleStartAgentIds != null && d.BattleStartAgentIds.Count > 0)
            return;

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
        SaveData();
    }

    public void ClearBattleSessionSnapshot()
    {
        var d = PlayerData;
        if (d == null) return;
        d.BattleSessionActive = false;
        d.BattleStartAgentIds = new List<string>();
        d.BattleRecruitSlotsUsed = new int[3];
        SaveData();
    }

    // ── Battle Result ─────────────────────────────────────────────────────

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

        // ── 1. Sync surviving agent HP ────────────────────────────────────
        foreach (var battleAgent in survivingAgents)
        {
            var stored = d.RecruitedAgents.Find(a => a.AgentId == battleAgent.AgentId);
            if (stored != null) stored.CurrentHp = battleAgent.CurrentHp;
        }

        // ── 2. Mark dead agents ───────────────────────────────────────────
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
            Debug.Log($"[GameData] {agentsLost} agent(s) lost — Fans now {d.Fans}.");
        }

        // ── 3. Calculate morale delta ─────────────────────────────────────
        int totalAgents = survivingAgents.Count + deadAgents.Count;
        bool isVictory  = result == BattleResult.Victory;
        int moraleDelta = ReputationUnlockSystem.GetMoraleDeltaForBattle(isVictory, agentsLost, totalAgents);
        d.FanMorale = Mathf.Clamp(d.FanMorale + moraleDelta, 0, 100);

        // ── 4. Apply result-dependent stat changes ────────────────────────
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
                    d.PoliceHeat = 0;
                    Debug.Log("[GameData] Police Raid victory — PoliceHeat reset to 0.");
                    events.Insert(0, agentsLost > 0
                        ? $"🟢 Police raid beaten! Heat cleared. {agentsLost} lad(s) down."
                        : "🟢 Police raid beaten! Heat cleared. The firm escaped clean.");
                }
                else
                {
                    d.PoliceHeat = Mathf.Clamp(d.PoliceHeat + heatGain, 0, 10);
                    string moraleLine = moraleDelta > 0 ? $" Morale +{moraleDelta}." : "";
                    string lossNote   = agentsLost > 0 ? $" {agentsLost} lad(s) lost." : "";
                    events.Insert(0, $"🟢 Victory! +£{moneyReward:N0} · Rep {d.Reputation}.{lossNote}{moraleLine}");
                }

                RecalculatePlayerReputation();

                if (fansGained > 0)
                {
                    d.PendingFansGain += fansGained;
                    d.Fans            += fansGained;
                }

                if (opponentBot != null)
                {
                    opponentBot.losses++;

                    // Bot loses half of what the player gained (they poached those lads)
                    int lostFans = Mathf.Clamp(fansGained / 2, 1, opponentBot.fans - 1);
                    opponentBot.fans = Mathf.Max(1, opponentBot.fans - lostFans);

                    // Bot rallies and recruits after the loss — grows stronger for next round.
                    // Recovery = fans the match was worth (fansGained) + 1 per 5 remaining fans.
                    // This means bigger bots bounce back harder, keeping them a real threat.
                    int botRecovery = fansGained + Mathf.Max(1, opponentBot.fans / 5);
                    opponentBot.fans = Mathf.Min(opponentBot.fans + botRecovery, 200);

                    Debug.Log($"[GameData] {opponentBot.firmName} lost {lostFans} fans but rallied +{botRecovery} → now {opponentBot.fans} fans.");

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
                    Debug.Log("[GameData] Police Raid defeat — PoliceHeat stays at 10.");
                    events.Insert(0, agentsLost > 0
                        ? $"🔴 Raided by the filth! {agentsLost} lad(s) nicked. Heat maxed."
                        : "🔴 Raided by the filth! The lads scattered. Heat maxed.");
                }
                else
                {
                    d.PoliceHeat = Mathf.Clamp(d.PoliceHeat + heatGain, 0, 10);
                    string moraleLine = $" Morale {moraleDelta}.";
                    events.Insert(0, agentsLost > 0
                        ? $"🔴 Defeat. {agentsLost} lad(s) didn't make it back.{moraleLine}"
                        : $"🔴 Defeat. The lads took a hammering.{moraleLine}");
                }

                RecalculatePlayerReputation();

                if (opponentBot != null)
                {
                    opponentBot.wins++;
                    // Bot gains only 1–2 fans from beating the player — keep growth slow
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
                    Debug.Log($"[GameData] Police Raid draw — PoliceHeat reduced to {d.PoliceHeat}.");
                    events.Insert(0, agentsLost > 0
                        ? $"🟡 Raid stalemate. Heat cooled. {agentsLost} lad(s) down."
                        : "🟡 Raid stalemate. Managed to slip away. Heat cooled.");
                }
                else
                {
                    d.PoliceHeat = Mathf.Clamp(d.PoliceHeat + heatGain, 0, 10);
                    events.Insert(0, agentsLost > 0
                        ? $"🟡 Time ran out — scrappy result. {agentsLost} lad(s) down."
                        : "🟡 Time ran out — scrappy result.");
                }
                break;
        }

        // ── Morale critical warning ───────────────────────────────────────
        if (d.FanMorale < 30 && !events.Exists(e => e.Contains("LADS ARE SHAKEN")))
            events.Insert(0, "🔴 LADS ARE SHAKEN — morale is dangerously low.");

        if (events.Count > 8) events = events.GetRange(0, 8);
        d.RecentEvents = events.ToArray();

        // Sync fan count with alive agents after all HP changes
        SyncFanCountWithAgents();

        // Apply accumulated street reputation from the session (was previously unused).
        if (repGain != 0)
            AddReputation(repGain, save: false);
        else
            d.Ranking = RecalculateRanking(d);

        SaveData();
        OnBattleEnded?.Invoke(result);
    }

    // ── "Lay Low" Action ─────────────────────────────────────────────────

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
        events.Insert(0, $"🟢 Firm laid low — spent £{cost:N0}, heat -{reduction} (now {d.PoliceHeat}/10).");
        if (events.Count > 8) events = events.GetRange(0, 8);
        d.RecentEvents = events.ToArray();

        PoliceHeatSystem.EvaluateWatchlist(d);
        d.HeatCooldownCost = PoliceHeatSystem.GetLayLowCost(d.PoliceHeat);

        SaveData();
        OnSavingData?.Invoke();
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

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
                // Simulated wins give a very small fan bump — max +1 per round
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
                // Fan loss on defeat is rare — only Slippery bots truly bleed fans
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
        SaveDataInLocal.DataSave(PlayerData);
        OnSavingData?.Invoke();
    }

    // ── Events ────────────────────────────────────────────────────────────
    public static Action OnSavingData;
    public static Action OnMatchDayEnded;
    public static Action<BattleResult> OnBattleEnded;
}
