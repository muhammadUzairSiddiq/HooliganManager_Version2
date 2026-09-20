using UnityEngine;

/// <summary>
/// Single source of truth for the four recruitment campaigns shown at headquarters
/// and inside the city. Pricing, gain ranges, reputation gates and modifiers all
/// live here so both screens behave identically.
/// </summary>
public static class RecruitPackages
{
    public struct Package
    {
        public string Title, Description;
        public int Cost, MinFans, MaxFans;
        public Package(string title, string description, int cost, int minFans, int maxFans)
        { Title = title; Description = description; Cost = cost; MinFans = minFans; MaxFans = maxFans; }
    }

    public static readonly Package[] All =
    {
        new Package("POSTERS & FLYERS",    "Put up posters around town.",                 1200, 2, 4),
        new Package("PUB RECRUITMENT",     "Buy rounds and talk football.",               2500, 3, 5),
        new Package("ONLINE CAMPAIGN",     "Spread the word. Build hype.",                3800, 4, 7),
        new Package("LOCAL YOUTH CONTACTS","Connect with young lads looking for a crew.", 5000, 5, 8),
    };

    public static bool IsUnlocked(int index, PlayerData d) =>
        d != null && ReputationUnlockSystem.IsRecruitOptionUnlocked(index, d.Reputation);

    public static int RequiredReputation(int index) => ReputationUnlockSystem.GetRecruitOptionRequiredRep(index);

    /// <summary>Cost after the police-watchlist markup.</summary>
    public static int EffectiveCost(int index, PlayerData d)
    {
        if (d == null || index < 0 || index >= All.Length) return int.MaxValue;
        return Mathf.CeilToInt(All[index].Cost * PoliceHeatSystem.GetRecruitCostMultiplier(d));
    }

    /// <summary>Combined watchlist + morale efficiency (1 = full effect).</summary>
    public static float GainModifier(PlayerData d)
    {
        if (d == null) return 1f;
        return PoliceHeatSystem.GetRecruitModifier(d) * ReputationUnlockSystem.GetMoraleRecruitModifier(d.FanMorale);
    }

    /// <summary>Roll the number of fans a purchase produces.</summary>
    public static int RollGain(int index, PlayerData d)
    {
        if (index < 0 || index >= All.Length) return 0;
        var p = All[index];
        int baseGain = Random.Range(p.MinFans, p.MaxFans + 1);
        return Mathf.Max(1, Mathf.FloorToInt(baseGain * GainModifier(d)));
    }

    public static string GainLabel(int index) => $"+{All[index].MinFans} - {All[index].MaxFans} FANS";
}
