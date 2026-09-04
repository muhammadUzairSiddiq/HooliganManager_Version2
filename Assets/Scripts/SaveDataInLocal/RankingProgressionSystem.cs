using System.Collections.Generic;

/// <summary>
/// Static helper — evaluates ranking changes each matchday and generates
/// consequences and event log entries for rank movements.
///
/// Milestones
/// ──────────
///   Top 5  → Tier-2 recruit options unlocked
///   Top 3  → Hard-tier rivals notice the player; sends challenger events
///   #1     → "Defend Your Title" mode — all hard bots target the player
///
/// Usage: call <see cref="Evaluate"/> from GameData.EndMatchDay().
/// </summary>
public static class RankingProgressionSystem
{
    // ── Milestone thresholds ───────────────────────────────────────────────
    public const int MilestoneTop5  = 5;
    public const int MilestoneTop3  = 3;
    public const int MilestoneTop1  = 1;

    /// <summary>
    /// Run at the end of each matchday after SimulateRivalMatches.
    /// Returns a list of event-log entries to prepend, and applies ranking consequences.
    /// </summary>
    /// <param name="d">Current PlayerData (already has updated Ranking from RankingsController).</param>
    /// <param name="newRanking">The ranking freshly calculated this matchday.</param>
    public static List<string> Evaluate(PlayerData d, int newRanking)
    {
        var entries = new List<string>();
        int prev = d.PreviousRanking;

        // ── Update highest ranking reached ────────────────────────────────
        if (newRanking < d.HighestRankingReached)
            d.HighestRankingReached = newRanking;

        // ── Detect movement ───────────────────────────────────────────────
        if (newRanking < prev)
        {
            // Climbed — reward
            int moneyBonus = (prev - newRanking) * 200;
            d.Money += moneyBonus;
            entries.Add($"🔵 Firm climbed to #{newRanking} — £{moneyBonus:N0} buzz money earned.");
        }
        else if (newRanking > prev)
        {
            // Dropped — consequence: small morale hit
            int moralePenalty = (newRanking - prev) * 4;
            d.FanMorale = UnityEngine.Mathf.Max(0, d.FanMorale - moralePenalty);
            entries.Add($"🔵 Firm dropped to #{newRanking}. Lads aren't happy — morale -{moralePenalty}.");
        }

        // ── Milestone events ──────────────────────────────────────────────
        if (newRanking <= MilestoneTop1 && prev > MilestoneTop1)
            entries.Add("🏆 TOP FIRM. You're #1. Every crew in the city knows your name.");
        else if (newRanking <= MilestoneTop3 && prev > MilestoneTop3)
            entries.Add("⚡ TOP 3. The big firms are watching. Expect challengers.");
        else if (newRanking <= MilestoneTop5 && prev > MilestoneTop5)
            entries.Add("📈 TOP 5 REACHED — better recruits now available.");

        // ── Challenger events: rival bot surpasses player ─────────────────
        if (d.RivalBots != null)
        {
            foreach (var bot in d.RivalBots)
            {
                bool wasChallenger = bot.isChallenger;
                bot.isChallenger = bot.reputation > d.Reputation;

                if (!wasChallenger && bot.isChallenger)
                    entries.Add($"⚡ {bot.firmName.ToUpper()} have overtaken your rep. They're coming for you.");
                else if (wasChallenger && !bot.isChallenger)
                    entries.Add($"✅ {bot.firmName.ToUpper()} knocked back down. Your rep is ahead again.");
            }
        }

        // ── Save the new previous ranking for next matchday ───────────────
        d.PreviousRanking = newRanking;

        return entries;
    }

    // ── Unlock query helpers ───────────────────────────────────────────────

    /// <summary>True if the player has ever reached Top 5.</summary>
    public static bool HasReachedTop5(PlayerData d)  => d.HighestRankingReached <= MilestoneTop5;
    /// <summary>True if the player has ever reached Top 3.</summary>
    public static bool HasReachedTop3(PlayerData d)  => d.HighestRankingReached <= MilestoneTop3;
    /// <summary>True if the player has ever been #1.</summary>
    public static bool HasReachedRank1(PlayerData d) => d.HighestRankingReached <= MilestoneTop1;
}
