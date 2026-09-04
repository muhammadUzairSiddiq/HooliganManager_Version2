using UnityEngine;

/// <summary>
/// Drives the long-term stat evolution of rival bots.
///
/// ── Fan Growth (INDEPENDENT of the player) ─────────────────────────────
/// Bots grow on their own fixed schedule — they do NOT target a percentage
/// of the player's fans. Each progression tick adds a tiny number of fans
/// based on the bot's archetype:
///
///   Ambitious  → 0–2 fans per tick  (most aggressive recruiter)
///   Brawler    → 0–1 fans per tick  (strength-focused, fan growth slow)
///   Tactical   → 0–1 fans per tick  (balanced, steady)
///   Slippery   → 0–1 fans per tick  (ticks less often — slowest overall)
///   Veteran    → 0–1 fans per tick  (already established, very infrequent)
///
/// Growth is intentionally tiny so the player always has time to recruit
/// and stay ahead — if they don't, rivals will gradually close the gap.
///
/// ── Strength Growth ─────────────────────────────────────────────────────
/// Strength grows by a small fixed step each tick, scaled by archetype.
/// There is no player-relative target — bots evolve on their own curve.
///
/// ── Usage ──────────────────────────────────────────────────────────────
/// Attach this MonoBehaviour to the same persistent GameObject that holds
/// <see cref="GameData"/> (e.g. the "GameManager" prefab in the Main scene).
/// It subscribes to <see cref="GameData.OnMatchDayEnded"/> automatically.
/// </summary>
public class BotProgressionController : MonoBehaviour
{
    // ── Tick interval between each bot's progression step ──────────────────
    // Each bot ticks every 2–7 match days independently.
    private const int IntervalMin = 2;
    private const int IntervalMax = 7;

    // ── Absolute fan ceiling for bots (prevents infinite snowball) ──────────
    private const int BotFanHardCap = 200;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void OnEnable()  => GameData.OnMatchDayEnded += OnMatchDayEnded;
    private void OnDisable() => GameData.OnMatchDayEnded -= OnMatchDayEnded;

    // ── Core logic ─────────────────────────────────────────────────────────

    private void OnMatchDayEnded()
    {
        if (GameData.instance == null) return;

        var pd = GameData.instance.PlayerData;
        if (pd == null || pd.RivalBots == null) return;

        int currentMatchDay = pd.MatchDay;   // already incremented by EndMatchDay()

        foreach (var bot in pd.RivalBots)
        {
            // Skip the bot we just fought — battle result already adjusted them
            if (!string.IsNullOrEmpty(pd.LastOpponentFought) &&
                bot.firmName.Equals(pd.LastOpponentFought, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (currentMatchDay < bot.nextProgressionMatchDay) continue;

            // Resolve per-archetype growth amounts
            GetArchetypeModifiers(bot.archetype,
                out int maxFanGain, out int maxStrGain, out int intervalBias);

            // ── Fan growth: small fixed amount, fully independent of player ────
            int fansDelta = Random.Range(0, maxFanGain + 1);   // 0 to maxFanGain inclusive
            if (fansDelta > 0)
                bot.fans = Mathf.Min(bot.fans + fansDelta, BotFanHardCap);

            // ── Strength growth: small fixed step ──────────────────────────────
            int strDelta = Random.Range(0, maxStrGain + 1);
            if (strDelta > 0)
                bot.strength = Mathf.Clamp(bot.strength + strDelta, 1, 999);

            // Schedule next tick (Slippery/Veteran tick less often via intervalBias)
            bot.nextProgressionMatchDay = currentMatchDay
                + Random.Range(IntervalMin + intervalBias, IntervalMax + intervalBias);

            Debug.Log($"[BotProgression] {bot.firmName} ({bot.archetype}) " +
                      $"FANS +{fansDelta} → {bot.fans} | " +
                      $"STR +{strDelta} → {bot.strength} | " +
                      $"next MD {bot.nextProgressionMatchDay}");
        }

        GameData.instance.SaveData();
    }

    /// <summary>
    /// Returns the maximum fan and strength gain per tick for a given archetype,
    /// plus an interval bias (positive = ticks less often, i.e. slower overall growth).
    ///
    ///   maxFanGain   — upper bound of fans gained per tick (Random.Range 0..maxFanGain)
    ///   maxStrGain   — upper bound of strength gained per tick
    ///   intervalBias — added to both min and max tick interval
    /// </summary>
    private static void GetArchetypeModifiers(FirmArchetype archetype,
        out int maxFanGain, out int maxStrGain, out int intervalBias)
    {
        switch (archetype)
        {
            case FirmArchetype.Ambitious:
                // Fastest recruiter — can pull 1 or 2 new fans each tick
                maxFanGain   = 2;
                maxStrGain   = 1;
                intervalBias = 0;
                break;

            case FirmArchetype.Brawler:
                // Muscle over numbers — fans grow slowly, strength grows more
                maxFanGain   = 1;
                maxStrGain   = 2;
                intervalBias = 0;
                break;

            case FirmArchetype.Tactical:
                // Balanced — steady fan and strength growth
                maxFanGain   = 1;
                maxStrGain   = 1;
                intervalBias = 0;
                break;

            case FirmArchetype.Slippery:
                // Slow and deliberate — ticks less often overall
                maxFanGain   = 1;
                maxStrGain   = 1;
                intervalBias = 2;   // ticks 2 matchdays later on average
                break;

            case FirmArchetype.Veteran:
                // Already established — barely grows, very infrequent ticks
                maxFanGain   = 1;
                maxStrGain   = 1;
                intervalBias = 3;   // ticks much less often
                break;

            default:
                maxFanGain   = 1;
                maxStrGain   = 1;
                intervalBias = 0;
                break;
        }
    }
}
