/// <summary>
/// All data collected during a battle, passed to BattleResultController.Show().
/// Populated by BattleManager at the end of a fight.
/// </summary>
public struct BattleResultData
{
    // ── Outcome ───────────────────────────────────────────────────────────
    public GameData.BattleResult Result;       // Victory / Defeat / TimerExpired

    // ── Match score card ─────────────────────────────────────────────────
    public string PlayerFirmName;             // e.g. "SOUTH END HOOLIGANS"
    public string PlayerFirmSubName;          // e.g. "HOOLIGANS"
    public string EnemyFirmName;              // e.g. "EAST SIDE"
    public string EnemyFirmSubName;           // e.g. "MOB"
    public int    PlayerAgentsAlive;          // final alive count (player side)
    public int    EnemyAgentsAlive;           // final alive count (enemy side)

    // ── Round scoreline (e.g. 2 - 1 out of 3 rounds) ───────────────────────
    public int    PlayerRoundsWon;            // rounds the player won
    public int    EnemyRoundsWon;             // rounds the enemy won
    public int    TotalRounds;               // total rounds played

    // ── Trip summary rows ─────────────────────────────────────────────────
    public int    EnemiesDefeated;            // how many enemies were killed
    public int    UnitsLost;                  // how many player agents died
    public int    MoneyEarned;                // net money reward (0 or negative on defeat)
    public int    ReputationGained;           // rep delta (can be negative)
    public int    FansGained;                 // fans earned from victory buzz
    public int    PoliceHeatChange;           // +N (always positive from fights)

    // ── Trip targets (objectives generated before battle, evaluated after) ──
    /// <summary>
    /// Up to 3 targets generated at battle-start, evaluated at EndBattle.
    /// Shown in a "TARGETS" section on the result screen with ✓ / ✗.
    /// </summary>
    public BattleTripTarget[] TripTargets;

    /// <summary>The destination name, e.g. "East Docks".</summary>
    public string DestinationName;

    // ── Club crests (assign in Inspector on BattleResultController) ──────
    // These are Sprites so they are NOT part of the struct — assign them separately.
}
