/// <summary>
/// Shared Animator parameter and state name constants for all agent types.
///
/// SOURCE OF TRUTH — every string here must exactly match a parameter name
/// or state name in CharacterAnimatorController.controller.
///
/// Parameter types as declared in the controller:
///   Speed         → Float   (m_Type: 1)
///   Attack        → Trigger (m_Type: 9)
///   Hit           → Trigger (m_Type: 9)
///   Die           → Trigger (m_Type: 9)
///   Defend        → Bool    (m_Type: 4)
///   Retreat       → Bool    (m_Type: 4)
///   AttackVariant → Int     (m_Type: 3)
///   DieVariant    → Int     (m_Type: 3)
///   RunStyle      → Int     (m_Type: 3)
/// </summary>
public static class AgentAnimParams
{
    // ═════════════════════════════════════════════════════════════════════
    // PARAMETERS
    // ═════════════════════════════════════════════════════════════════════

    // ── Float ─────────────────────────────────────────────────────────────
    /// <summary>0 = idle, >0 = moving. Driven from NavMeshAgent.velocity.magnitude every frame.</summary>
    public const string Speed = "Speed";

    // ── Triggers ──────────────────────────────────────────────────────────
    /// <summary>
    /// Fire to play an attack animation. Always set AttackVariant first to
    /// choose which of the 4 punch clips plays.
    /// </summary>
    public const string Attack = "Attack";

    /// <summary>Fire when the agent receives damage (no dedicated state yet — trigger resets itself).</summary>
    public const string Hit = "Hit";

    /// <summary>
    /// Fire once when the agent dies. Always set DieVariant first to choose
    /// which of the 2 death-fall clips plays.
    /// </summary>
    public const string Die = "Die";

    // ── Bools ─────────────────────────────────────────────────────────────
    /// <summary>True while the agent is in a defensive/blocking stance.</summary>
    public const string Defend = "Defend";

    /// <summary>True while the agent is retreating to the spawn zone.</summary>
    public const string Retreat = "Retreat";

    // ── Ints (variation selectors) ─────────────────────────────────────────

    /// <summary>
    /// Set BEFORE firing the Attack trigger. Routes to the matching attack state:
    ///   0 → Right Punching          (AnyState, AttackVariant == 0)
    ///   1 → Left Punching           (AnyState, AttackVariant == 1)
    ///   2 → Right Cross Punch       (AnyState, AttackVariant == 2)
    ///   3 → Right Hook Punch        (AnyState, AttackVariant == 3)
    /// </summary>
    public const string AttackVariant = "AttackVariant";
    /// <summary>Number of valid AttackVariant values (0 … AttackVariantCount-1).</summary>
    public const int    AttackVariantCount = 4;

    /// <summary>
    /// Set BEFORE firing the Die trigger. Routes to the matching death state:
    ///   0 → Falling Back Death From Right Punch   (AnyState, DieVariant == 0)
    ///   1 → Falling Forward Death From Left Punch (AnyState, DieVariant == 1)
    /// </summary>
    public const string DieVariant = "DieVariant";
    /// <summary>Number of valid DieVariant values (0 … DieVariantCount-1).</summary>
    public const int    DieVariantCount = 2;

    /// <summary>
    /// Assigned ONCE at spawn. Determines which run clip this agent uses for the
    /// entire battle (prevents all agents switching style in sync mid-game):
    ///   0 → Running           (arms-out brawler sprint)
    ///   1 → Unarmed Run Forward (upright sprint)
    /// </summary>
    public const string RunStyle = "RunStyle";
    /// <summary>Number of valid RunStyle values (0 … RunStyleCount-1).</summary>
    public const int    RunStyleCount = 2;

    // ── Parameter hashes (cached for SetFloat / SetTrigger / SetBool / SetInteger) ──
    public static readonly int SpeedHash         = UnityEngine.Animator.StringToHash(Speed);
    public static readonly int AttackHash        = UnityEngine.Animator.StringToHash(Attack);
    public static readonly int HitHash           = UnityEngine.Animator.StringToHash(Hit);
    public static readonly int DieHash           = UnityEngine.Animator.StringToHash(Die);
    public static readonly int DefendHash        = UnityEngine.Animator.StringToHash(Defend);
    public static readonly int RetreatHash       = UnityEngine.Animator.StringToHash(Retreat);
    public static readonly int AttackVariantHash = UnityEngine.Animator.StringToHash(AttackVariant);
    public static readonly int DieVariantHash    = UnityEngine.Animator.StringToHash(DieVariant);
    public static readonly int RunStyleHash      = UnityEngine.Animator.StringToHash(RunStyle);

    // ═════════════════════════════════════════════════════════════════════
    // STATE NAMES  (exact m_Name values from CharacterAnimatorController)
    // ═════════════════════════════════════════════════════════════════════
    /// <summary>
    /// State name string constants — use with Animator.Play(), CrossFade(),
    /// or AnimatorStateInfo.IsName() checks. Each name matches m_Name in the
    /// controller file exactly (case-sensitive).
    /// </summary>
    public static class States
    {
        // ── Idle states ───────────────────────────────────────────────────
        /// <summary>Entry state. Plays once then auto-transitions to BattleIdle.</summary>
        public const string Idle            = "Idle";

        /// <summary>Main in-battle idle loop (bouncing fighter stance). Default looping state.</summary>
        public const string BattleIdle      = "Bouncing Fight Idle";

        /// <summary>Pre-battle ready stance (unused in runtime flow; kept for cutscenes).</summary>
        public const string ReadyIdle       = "Ready Idle";

        /// <summary>Victory celebration idle (unused in runtime flow; play via Animator.Play after battle).</summary>
        public const string HappyIdle       = "Happy Idle";

        // ── Walk states ───────────────────────────────────────────────────
        /// <summary>Walking animation for patrol and ambient roaming.</summary>
        public const string Walking         = "Walking";

        // ── Run states ────────────────────────────────────────────────────
        /// <summary>Arms-out brawler run. Played when RunStyle == 0 and Speed > 0.1.</summary>
        public const string Running         = "Running";

        /// <summary>Upright unarmed sprint. Played when RunStyle == 1 and Speed > 0.1.</summary>
        public const string UnarmedRun      = "Unarmed Run Forward";

        // ── Attack states (AttackVariant 0-3) ─────────────────────────────
        /// <summary>AttackVariant 0. Standard right-hand jab.</summary>
        public const string RightPunching   = "Right Punching";

        /// <summary>AttackVariant 1. Left-hand jab.</summary>
        public const string LeftPunching    = "Left Punching";

        /// <summary>AttackVariant 2. Straight right cross.</summary>
        public const string RightCross      = "Right Cross Punch";

        /// <summary>AttackVariant 3. Looping right hook.</summary>
        public const string RightHook       = "Right Hook Punch";

        // ── Death states (DieVariant 0-1) ─────────────────────────────────
        /// <summary>DieVariant 0. Agent staggers and falls backwards.</summary>
        public const string DeathFallBack   = "Falling Back Death From Right Punch";

        /// <summary>DieVariant 1. Agent collapses forward.</summary>
        public const string DeathFallForward = "Falling Forward Death From Left Punch";

        // ── State hashes (use with AnimatorStateInfo.shortNameHash) ───────
        public static readonly int IdleHash            = UnityEngine.Animator.StringToHash(Idle);
        public static readonly int BattleIdleHash      = UnityEngine.Animator.StringToHash(BattleIdle);
        public static readonly int ReadyIdleHash       = UnityEngine.Animator.StringToHash(ReadyIdle);
        public static readonly int HappyIdleHash       = UnityEngine.Animator.StringToHash(HappyIdle);
        public static readonly int UnarmedRunHash      = UnityEngine.Animator.StringToHash(UnarmedRun);
        public static readonly int WalkingHash         = UnityEngine.Animator.StringToHash(Walking);
        public static readonly int RightPunchingHash   = UnityEngine.Animator.StringToHash(RightPunching);
        public static readonly int LeftPunchingHash    = UnityEngine.Animator.StringToHash(LeftPunching);
        public static readonly int RightCrossHash      = UnityEngine.Animator.StringToHash(RightCross);
        public static readonly int RightHookHash       = UnityEngine.Animator.StringToHash(RightHook);
        public static readonly int DeathFallBackHash   = UnityEngine.Animator.StringToHash(DeathFallBack);
        public static readonly int DeathFallForwardHash = UnityEngine.Animator.StringToHash(DeathFallForward);
    }
}
