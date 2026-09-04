/// <summary>
/// A single trip target (objective) generated for a battle.
/// Shown on the ResultsPanel with a ✓ or ✗ next to each row.
/// </summary>
[System.Serializable]
public struct BattleTripTarget
{
    /// <summary>Human-readable label, e.g. "WIN THE MATCH".</summary>
    public string Label;

    /// <summary>Short sub-description, e.g. "Defeat more enemies than you lose".</summary>
    public string Description;

    /// <summary>Whether the player achieved this target.</summary>
    public bool   Achieved;
}
