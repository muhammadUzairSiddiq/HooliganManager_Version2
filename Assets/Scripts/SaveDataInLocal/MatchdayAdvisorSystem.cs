using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static helper — analyses the player's current state and generates up to 3
/// contextual advisor tips for the event log each matchday.
///
/// Tips are prefixed with "💡" so the dashboard can style them differently.
/// Call <see cref="GenerateTips"/> from GameData.EndMatchDay().
/// </summary>
public static class MatchdayAdvisorSystem
{
    public static List<string> GenerateTips(PlayerData d, int currentMatchDay)
    {
        var tips = new List<string>();
        if (d == null) return tips;

        // ── 1. Police heat warnings ───────────────────────────────────────
        if (d.PoliceHeat >= 9)
            tips.Add("💡 ADVISOR: Heat is at max. A raid is imminent — Lay Low before your next trip.");
        else if (d.PoliceHeat >= 7)
            tips.Add("💡 ADVISOR: Heat is critical. Police are watching every move. Consider laying low.");
        else if (d.PoliceHeat >= 5)
            tips.Add("💡 ADVISOR: Police heat climbing — avoid back-to-back trips without cooling off.");

        if (tips.Count >= 3) return Trim(tips);

        // ── 2. Fan morale warnings ────────────────────────────────────────
        if (d.FanMorale < 20)
            tips.Add("💡 ADVISOR: Morale is rock bottom — lads are walking. Win a fight urgently.");
        else if (d.FanMorale < 40)
            tips.Add("💡 ADVISOR: Low morale is hurting recruitment. A solid win will turn it around.");

        if (tips.Count >= 3) return Trim(tips);

        // ── 3. Money pressure ─────────────────────────────────────────────
        if (d.Money < 800)
            tips.Add("💡 ADVISOR: Funds critically low — you can't afford a trip or any recruitment.");
        else if (d.Money < 2000)
            tips.Add("💡 ADVISOR: Cash is tight. Choose between recruiting or a trip, not both.");

        if (tips.Count >= 3) return Trim(tips);

        // ── 4. Rival growth warnings ──────────────────────────────────────
        if (d.RivalBots != null)
        {
            BotData fastestGrower = null;
            foreach (var bot in d.RivalBots)
            {
                if (bot.archetype == FirmArchetype.Ambitious && bot.fans > d.Fans)
                {
                    fastestGrower = bot;
                    break;
                }
            }
            if (fastestGrower != null)
                tips.Add($"💡 ADVISOR: {fastestGrower.firmName} are growing fast — fight them soon or lose ground.");
        }

        if (tips.Count >= 3) return Trim(tips);

        // ── 4b. Recruit guidance — warn player when rivals are closing the fan gap ──
        if (d.RivalBots != null)
        {
            int closestGap = int.MaxValue;
            BotData closestBot = null;
            foreach (var bot in d.RivalBots)
            {
                int gap = d.Fans - bot.fans;
                if (gap >= 0 && gap < closestGap)
                {
                    closestGap = gap;
                    closestBot = bot;
                }
            }

            if (closestBot != null)
            {
                if (closestGap <= 2)
                    tips.Add($"💡 ADVISOR: {closestBot.firmName} are almost level with your lads ({closestBot.fans} fans). RECRUIT NOW or they'll overtake you.");
                else if (closestGap <= 5)
                    tips.Add($"💡 ADVISOR: {closestBot.firmName} are only {closestGap} fans behind. Recruit to keep your advantage.");
                else if (closestGap <= 10 && d.Money >= 1000)
                    tips.Add($"💡 ADVISOR: Rivals are creeping up. Use your cash to recruit fans before the gap closes.");
            }
        }

        if (tips.Count >= 3) return Trim(tips);

        // ── 5. Challenger warnings ────────────────────────────────────────
        if (d.RivalBots != null)
        {
            foreach (var bot in d.RivalBots)
            {
                if (bot.isChallenger)
                {
                    tips.Add($"💡 ADVISOR: {bot.firmName} has surpassed your rep. Take them on to reclaim it.");
                    break;
                }
            }
        }

        if (tips.Count >= 3) return Trim(tips);

        // ── 6. Recruitment opportunity ────────────────────────────────────
        int aliveAgents = 0;
        if (d.RecruitedAgents != null)
            foreach (var a in d.RecruitedAgents)
                if (a.IsAlive) aliveAgents++;

        if (aliveAgents < 5 && d.Money >= 1200)
            tips.Add("💡 ADVISOR: Firm is thin on the ground — recruit before your next fight.");

        if (tips.Count >= 3) return Trim(tips);

        // ── 7. Ranking opportunity ────────────────────────────────────────
        if (d.Ranking > 5 && d.Reputation > 0)
            tips.Add("💡 ADVISOR: Keep winning — the top 5 unlocks better recruits.");
        else if (d.Ranking <= 3)
            tips.Add("💡 ADVISOR: You're in the top 3. The #1 spot is within reach.");

        return Trim(tips);
    }

    private static List<string> Trim(List<string> tips)
    {
        if (tips.Count > 3) tips.RemoveRange(3, tips.Count - 3);
        return tips;
    }
}
