using UnityEngine;

/// <summary>
/// Static helper — centralises all police heat logic and narrative.
///
/// Call <see cref="GetHeatDescription"/> for UI labels.
/// Call <see cref="EvaluateWatchlist"/> each matchday to update the PoliceWatchlisted flag.
/// Call <see cref="GetLayLowCost"/> to calculate how much "laying low" costs at the current heat.
/// </summary>
public static class PoliceHeatSystem
{
    // ── Narrative descriptions ─────────────────────────────────────────────

    /// <summary>
    /// Returns a short narrative string describing the current heat level.
    /// </summary>
    public static string GetHeatDescription(int heat)
    {
        return heat switch
        {
            0     => "Flying under the radar.",
            1     => "Nobody's watching. Stay sharp.",
            2     => "Local bobbies have heard your name.",
            3     => "Word's getting around. Be careful.",
            4     => "Local plod are keeping tabs on the firm.",
            5     => "CID has your firm flagged. Watch yourselves.",
            6     => "Undercover activity reported near the pub.",
            7     => "Surveillance active — informants possible.",
            8     => "The firm is being followed. A wrong move ends this.",
            9     => "One more incident and you're raided.",
            10    => "🚔  RAIDED — police are all over the firm.",
            _     => "Unknown heat level."
        };
    }

    /// <summary>
    /// Short single-word severity label for colour-coding.
    /// </summary>
    public static string GetHeatSeverityLabel(int heat)
    {
        return heat switch
        {
            <= 2  => "CLEAN",
            <= 4  => "LOW",
            <= 6  => "MEDIUM",
            <= 8  => "HIGH",
            _     => "CRITICAL"
        };
    }

    /// <summary>
    /// Returns a normalised 0–1 heat colour lerped from green → amber → red.
    /// </summary>
    public static UnityEngine.Color GetHeatColor(int heat)
    {
        float t = Mathf.Clamp01(heat / 10f);
        if (t < 0.5f)
            return Color.Lerp(new Color(0.2f, 0.85f, 0.2f), new Color(0.95f, 0.65f, 0.1f), t * 2f);
        else
            return Color.Lerp(new Color(0.95f, 0.65f, 0.1f), new Color(0.9f, 0.1f, 0.1f), (t - 0.5f) * 2f);
    }

    // ── Watchlist evaluation ───────────────────────────────────────────────

    /// <summary>
    /// Updates <see cref="PlayerData.PoliceWatchlisted"/> based on current heat.
    /// Called during EndMatchDay.
    /// Returns true if the watchlist state changed.
    /// </summary>
    public static bool EvaluateWatchlist(PlayerData d)
    {
        bool shouldBeWatchlisted = d.PoliceHeat >= 7;
        if (shouldBeWatchlisted == d.PoliceWatchlisted) return false;
        d.PoliceWatchlisted = shouldBeWatchlisted;
        return true;
    }

    // ── Lay Low cost ──────────────────────────────────────────────────────

    /// <summary>
    /// Money cost of the "Lay Low" action, scaling with heat level.
    /// Higher heat = more expensive to cool things down.
    /// </summary>
    public static int GetLayLowCost(int heat)
    {
        return heat switch
        {
            <= 4  => 800,
            <= 6  => 1500,
            <= 8  => 2500,
            _     => 4000    // near max or max heat — very expensive
        };
    }

    /// <summary>
    /// Amount of heat reduced by the "Lay Low" action at the given heat level.
    /// </summary>
    public static int GetLayLowReduction(int heat)
    {
        return heat switch
        {
            <= 4  => 2,
            <= 7  => 3,
            _     => 2   // near max is still manageable but not a magic fix
        };
    }

    // ── Recruiting modifier ────────────────────────────────────────────────

    /// <summary>
    /// Returns a 0–1 modifier applied to fan gains from recruiting.
    /// 1.0 = normal. Lower when watchlisted (police monitoring slows word of mouth).
    /// </summary>
    public static float GetRecruitModifier(PlayerData d)
    {
        if (!d.PoliceWatchlisted) return 1f;
        return d.PoliceHeat switch
        {
            >= 9  => 0.45f,
            >= 7  => 0.65f,
            _     => 0.8f
        };
    }

    /// <summary>
    /// Returns a money-cost multiplier for recruiting when watchlisted.
    /// 1.0 = normal. Higher = more expensive (informants demand a cut).
    /// </summary>
    public static float GetRecruitCostMultiplier(PlayerData d)
    {
        if (!d.PoliceWatchlisted) return 1f;
        return d.PoliceHeat switch
        {
            >= 9  => 1.6f,
            >= 7  => 1.35f,
            _     => 1.15f
        };
    }
}
