/// <summary>
/// Static helper — defines reputation thresholds that gate in-game content,
/// and fan morale mechanics.
///
/// Reputation Unlock Table
/// ───────────────────────
///   Rep  25 → "Pub Recruitment" recruit option available
///   Rep  40 → "Online Campaign" recruit option available
///   Rep  60 → "Local Youth Contacts" recruit option available (top tier)
///   Rep  50 → "Old Town" destination accessible
///   Rep  35 → "East Docks" destination accessible
///   Rep  70 → Hard rivals start issuing challenger events
///   Rep  80 → Prestige badge shown on firm card
///
/// Fan Morale Thresholds
/// ─────────────────────
///   ≥ 70 : Normal — no penalty
///   50–69 : Slightly low — recruit efficiency –10%
///   30–49 : Low morale — recruit efficiency –25%, some lads restless
///   15–29 : Critical — fans may desert each matchday
///    < 15 : Rock bottom — guaranteed desertion + strong recruiter penalty
/// </summary>
public static class ReputationUnlockSystem
{
    // ── Reputation thresholds ──────────────────────────────────────────────

    public const int RepForPubRecruitment    = 25;
    public const int RepForOnlineCampaign    = 40;
    public const int RepForYouthContacts     = 60;
    public const int RepForOldTown           = 50;
    public const int RepForEastDocks         = 35;
    public const int RepForHardChallenges    = 70;
    public const int RepForPrestigeBadge     = 80;

    // ── Feature unlock queries ─────────────────────────────────────────────

    public static bool IsRecruitOptionUnlocked(int optionIndex, int reputation)
    {
        // Option 0: Posters & Flyers — always available
        // Option 1: Pub Recruitment  — rep 25+
        // Option 2: Online Campaign  — rep 40+
        // Option 3: Youth Contacts   — rep 60+
        return optionIndex switch
        {
            0 => true,
            1 => reputation >= RepForPubRecruitment,
            2 => reputation >= RepForOnlineCampaign,
            3 => reputation >= RepForYouthContacts,
            _ => false
        };
    }

    public static int GetRecruitOptionRequiredRep(int optionIndex)
    {
        return optionIndex switch
        {
            1 => RepForPubRecruitment,
            2 => RepForOnlineCampaign,
            3 => RepForYouthContacts,
            _ => 0
        };
    }

    public static bool IsDestinationUnlocked(string destinationName, int reputation)
    {
        return destinationName switch
        {
            "Riverside"  => true,         // always available — safe starter
            "North End"  => true,         // always available — medium difficulty
            "East Docks" => reputation >= RepForEastDocks,
            "Old Town"   => reputation >= RepForOldTown,
            _            => true           // any unnamed destination is unlocked by default
        };
    }

    public static int GetDestinationRequiredRep(string destinationName)
    {
        return destinationName switch
        {
            "East Docks" => RepForEastDocks,
            "Old Town"   => RepForOldTown,
            _            => 0
        };
    }

    public static bool HasPrestigeBadge(int reputation) => reputation >= RepForPrestigeBadge;

    // ── Firm status text ───────────────────────────────────────────────────

    /// <summary>
    /// Returns a narrative status line based on ranking + reputation.
    /// Shown on the firm card as flavour text.
    /// </summary>
    public static string GetFirmStatusText(int ranking, int reputation)
    {
        if (ranking == 1 && reputation >= 85)
            return "TOP FIRM — nobody touches you right now.";
        if (ranking <= 4 && reputation >= 70)
            return "FEARED OUTFIT — the top firms are watching.";
        if (ranking <= 9 && reputation >= 50)
            return "RESPECTED FIRM — rivals know the name.";
        if (ranking <= 14 && reputation >= 30)
            return "RISING OUTFIT — word's getting out.";
        return "UNKNOWN CREW — barely a rumour.";
    }

    // ── Fan morale mechanics ───────────────────────────────────────────────

    /// <summary>
    /// Returns how many fans may desert this matchday based on morale.
    /// Called during EndMatchDay if morale is dangerously low.
    /// </summary>
    public static int GetMoraleDeserterCount(int fanMorale)
    {
        if (fanMorale < 15) return UnityEngine.Random.Range(2, 5);
        if (fanMorale < 30) return UnityEngine.Random.Range(0, 3);
        return 0;
    }

    /// <summary>
    /// Recruit fan-gain modifier based on morale (0–1).
    /// </summary>
    public static float GetMoraleRecruitModifier(int fanMorale)
    {
        if (fanMorale >= 70) return 1.0f;
        if (fanMorale >= 50) return 0.90f;
        if (fanMorale >= 30) return 0.75f;
        if (fanMorale >= 15) return 0.55f;
        return 0.35f;
    }

    /// <summary>
    /// Morale delta applied after a battle based on result and losses.
    /// </summary>
    public static int GetMoraleDeltaForBattle(bool isVictory, int agentsLost, int totalAgents)
    {
        if (isVictory)
        {
            int bonus = agentsLost == 0 ? 18 : agentsLost <= 2 ? 10 : 5;
            return bonus;
        }
        else
        {
            // Defeat — morale drops harder if many were lost
            float lossRatio = totalAgents > 0 ? (float)agentsLost / totalAgents : 0f;
            if (lossRatio > 0.5f) return -25;
            if (lossRatio > 0.25f) return -15;
            return -8;
        }
    }

    /// <summary>Short label for morale shown in UI.</summary>
    public static string GetMoraleLabel(int morale)
    {
        if (morale >= 70) return "HIGH";
        if (morale >= 50) return "OK";
        if (morale >= 30) return "LOW";
        if (morale >= 15) return "CRITICAL";
        return "BROKEN";
    }

    public static UnityEngine.Color GetMoraleColor(int morale)
    {
        if (morale >= 70) return new UnityEngine.Color(0.2f, 0.85f, 0.2f);
        if (morale >= 50) return new UnityEngine.Color(0.95f, 0.75f, 0.1f);
        if (morale >= 30) return new UnityEngine.Color(0.9f, 0.45f, 0.1f);
        return new UnityEngine.Color(0.9f, 0.1f, 0.1f);
    }
}
