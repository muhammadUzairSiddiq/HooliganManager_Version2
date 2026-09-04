public enum DifficultyTier { Easy, Medium, Hard }

/// <summary>
/// The personality archetype of a rival firm.
/// Drives how the bot grows and how they are described in the UI.
/// </summary>
public enum FirmArchetype
{
    Brawler,    // Raw physical power — gains strength fast, less focus on recruiting
    Tactical,   // Disciplined, efficient — balanced growth, resist police pressure
    Slippery,   // Elusive — resist heat build-up, grow slowly but survive longer
    Ambitious,  // Hungry to rise — recruits aggressively, rep grows fast
    Veteran     // Seasoned firm — high baseline, very hard to push off their perch
}

[System.Serializable]
public class BotData
{
    public string clubName;
    public string clubShortName;
    public string firmName;
    public string primaryColor;
    public string secondaryColor;
    public int fans;
    public int strength;
    public int reputation;
    public int wins;
    public int losses;

    // ── Personality ───────────────────────────────────────────────────────
    /// <summary>Defines the firm's growth and combat personality.</summary>
    public FirmArchetype archetype = FirmArchetype.Tactical;

    /// <summary>Short flavour line shown in the trip detail panel.</summary>
    public string motto = "";

    /// <summary>
    /// 1–10. How well this firm handles police pressure.
    /// Slippery/Tactical bots have higher resistance (heat builds slower for them in sim).
    /// </summary>
    public int policeResistance = 5;

    /// <summary>
    /// 1–10. How efficiently this firm recruits new fans.
    /// Ambitious bots have higher efficiency (fan growth is faster).
    /// </summary>
    public int recruitEfficiency = 5;

    /// <summary>
    /// 1–10. Combat strength modifier applied during battle difficulty scaling.
    /// Brawler bots have higher values.
    /// </summary>
    public int brawlBonus = 5;

    /// <summary>
    /// True when this rival's reputation has surpassed the player's.
    /// Triggers a "Challenge" event in the event log.
    /// </summary>
    public bool isChallenger = false;

    // ── Progression ───────────────────────────────────────────────────────
    /// <summary>
    /// How challenging this bot is relative to the player.
    /// Easy  → targets ~80% of player strength/fans
    /// Medium → targets ~105% of player strength/fans
    /// Hard   → targets ~130% of player strength/fans
    /// </summary>
    public DifficultyTier difficultyTier = DifficultyTier.Medium;

    /// <summary>
    /// The matchday number on which this bot's next gradual stat shift fires.
    /// Assigned a random offset on first init so all bots don't shift simultaneously.
    /// </summary>
    public int nextProgressionMatchDay = 0;
}
