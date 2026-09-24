using UnityEngine;

/// <summary>
/// Code-driven animation helper for battle agents (PlayerAgent and EnemyAgent).
///
/// DESIGN:
///   All state transitions are issued from code via CrossFadeInFixedTime / Play.
///   The Animator Controller is used purely as a clip library (no parameters,
///   no transitions needed in the asset itself).
///
/// HOW TO USE:
///   1. Construct once in Initialise():
///        _anim = new AgentAnimController(animator);
///
///   2. Call Tick() every Update frame with the agent's movement speed:
///        _anim.Tick(navMeshAgent.velocity.magnitude);
///      → Automatically crossfades Idle ↔ Run based on threshold.
///
///   3. Fire one-shot events as game logic dictates:
///        _anim.PlayAttack();  // random punch variant
///        _anim.PlayHit();     // hit-react pause
///        _anim.PlayDie();     // random death fall (terminal)
///
///   4. Optional – force an immediate state (e.g. victory screen):
///        _anim.PlayImmediate(AgentAnimParams.States.HappyIdle);
///
/// LOCK SYSTEM:
///   One-shot animations (attack, hit, die) set a time-based lock that
///   prevents Tick() from overriding them before the clip finishes.
///   Attack and hit locks expire automatically. Die is permanent (terminal).
/// </summary>
public class AgentAnimController
{
    // ── Animator reference ────────────────────────────────────────────────
    private readonly Animator _anim;

    // ── Run style — randomised once at construction ───────────────────────
    private readonly int _runVariant; // index into s_RunStates

    // ── One-shot lock — Tick() does nothing while locked ─────────────────
    private float _lockUntil;
    private bool  IsLocked => Time.time < _lockUntil;

    // ── Current state tracking — avoids redundant CrossFade calls ─────────
    private string _currentState = string.Empty;

    // ── Crossfade durations (seconds) ─────────────────────────────────────
    private const float FadeToIdle   = 0.20f;
    private const float FadeToRun    = 0.12f;
    private const float FadeToAttack = 0.05f;
    private const float FadeToDie    = 0.10f;

    // ── One-shot lock durations (seconds) ─────────────────────────────────
    /// <summary>
    /// How long Tick() is suppressed after an attack starts.
    /// Should be slightly shorter than the shortest punch clip so the
    /// transition back to idle feels responsive.
    /// </summary>
    public float AttackLockDuration = 0.9f;

    /// <summary>
    /// How long Tick() is suppressed after a hit reaction.
    /// Gives a brief visible hitch without disrupting the flow.
    /// </summary>
    public float HitLockDuration = 0.25f;

    // ── Speed thresholds for idle ↔ walk ↔ run decision ────────────────
    // Speed is expected as a 0–1 normalised value (actual / chaseSpeed).
    // Below IdleThreshold  → BattleIdle (standing still)
    // Below WalkThreshold  → Run clip at 0.45x speed (looks like casual walk)
    // Above WalkThreshold  → Run clip at 1.0x speed (full sprint)
    private const float IdleThreshold = 0.05f;
    private const float WalkThreshold = 0.55f;
    private const float WalkAnimSpeed = 0.45f;   // slowed run = casual patrol walk

    // ── State name tables (indices match AgentAnimParams.States docs) ──────
    private static readonly string[] s_AttackStates =
    {
        AgentAnimParams.States.RightPunching,   // 0
        AgentAnimParams.States.LeftPunching,    // 1
        AgentAnimParams.States.RightCross,      // 2
        AgentAnimParams.States.RightHook,       // 3
    };

    private static readonly string[] s_DieStates =
    {
        AgentAnimParams.States.DeathFallBack,    // 0
        AgentAnimParams.States.DeathFallForward, // 1
    };

    private static readonly string[] s_RunStates =
    {
        AgentAnimParams.States.Running,    // 0 — arms-out brawler sprint
        AgentAnimParams.States.UnarmedRun, // 1 — upright sprint
    };

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Create a controller for the given Animator.
    /// Run style is randomised here so each spawned agent uses a different
    /// run clip for the entire battle, avoiding a crowd that looks identical.
    /// </summary>
    public AgentAnimController(Animator animator)
    {
        _anim       = animator;
        _runVariant = Random.Range(0, s_RunStates.Length);
        if (_anim) { _anim.applyRootMotion = false; _anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms; _anim.speed = 1; }

        // Snap to idle immediately — no blend on first frame.
        PlayImmediate(AgentAnimParams.States.Idle);
    }

    // ═════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═════════════════════════════════════════════════════════════════════

    // ── Per-frame driver ──────────────────────────────────────────────────

    /// <summary>
    /// Call every Update frame with the agent's current movement speed
    /// (e.g. <c>NavMeshAgent.velocity.magnitude</c>).
    /// Crossfades between BattleIdle and the agent's assigned run clip.
    /// Does nothing while a one-shot animation is locked.
    /// </summary>
    public void Tick(float speed, bool isFighting = false)
    {
        if (IsLocked) return;
        speed = Mathf.Clamp01(speed);

        if (speed > WalkThreshold)
        {
            // Full combat sprint
            if (_anim != null) _anim.speed = Mathf.Lerp(_anim.speed, Mathf.Clamp(speed, .7f, 1.15f), 1 - Mathf.Exp(-12 * Time.deltaTime));
            SmoothCrossFade(s_RunStates[_runVariant], GameplayTuning.Current.animationBlend);
        }
        else if (speed > IdleThreshold)
        {
            // Casual patrol — use the dedicated walk clip
            if (_anim != null) _anim.speed = 1f;
            SmoothCrossFade(AgentAnimParams.States.Walking, GameplayTuning.Current.animationBlend);
        }
        else
        {
            if (_anim != null) _anim.speed = 1f;
            string idleState = isFighting ? AgentAnimParams.States.BattleIdle : AgentAnimParams.States.Idle;
            SmoothCrossFade(idleState, FadeToIdle);
        }
    }

    // ── One-shot events ───────────────────────────────────────────────────

    /// <summary>
    /// Crossfades to a random punch animation and locks Tick() for
    /// <see cref="AttackLockDuration"/> seconds so the clip plays fully
    /// before idle/run resumes.
    /// </summary>
    public void PlayAttack()
    {
        if (_anim) _anim.speed = 1;
        Lock(AttackLockDuration);
        ForceCrossFade(s_AttackStates[Random.Range(0, s_AttackStates.Length)], FadeToAttack);
    }

    /// <summary>
    /// Briefly suppresses Tick() to give a visible hit hitch.
    /// Replace the body with a dedicated hit-react CrossFade if you add
    /// a hit-reaction clip to the controller in the future.
    /// </summary>
    public void PlayHit()
    {
        Lock(0.4f);
        ForceCrossFade(AgentAnimParams.States.ReadyIdle, 0.06f);
    }

    /// <summary>
    /// Crossfades to a random death animation and permanently locks all
    /// future Tick() calls (death is terminal — the agent will never move again).
    /// </summary>
    public void PlayDie()
    {
        _lockUntil = float.MaxValue; // terminal lock — never released
        ForceCrossFade(s_DieStates[Random.Range(0, s_DieStates.Length)], FadeToDie);
    }

    // ── Direct play (bypasses lock and current-state guard) ───────────────

    /// <summary>
    /// Immediately jumps to <paramref name="stateName"/> with no crossfade blend.
    /// Use on spawn or for cut-scene states (e.g. <c>AgentAnimParams.States.HappyIdle</c>).
    /// Does NOT set a lock — Tick() will resume on the next frame.
    /// </summary>
    public void PlayImmediate(string stateName)
    {
        if (_anim == null) return;
        _currentState = stateName;
        _anim.Play(stateName, 0, 0f);
    }

    /// <summary>
    /// Crossfade to any state by name, with a custom blend duration.
    /// Bypasses the one-shot lock and the current-state guard.
    /// </summary>
    public void CrossFadeTo(string stateName, float fadeDuration = 0.15f)
    {
        if (_anim == null) return;
        _currentState = stateName;
        _anim.CrossFadeInFixedTime(stateName, fadeDuration, 0);
    }

    // ═════════════════════════════════════════════════════════════════════
    // INTERNALS
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>Lock Tick() for <paramref name="duration"/> seconds.</summary>
    private void Lock(float duration)
        => _lockUntil = Mathf.Max(_lockUntil, Time.time + duration);

    /// <summary>
    /// Crossfade only when the target state differs from the current one.
    /// Used by Tick() to avoid spamming CrossFadeInFixedTime every frame.
    /// </summary>
    private void SmoothCrossFade(string stateName, float duration)
    {
        if (_anim == null || _currentState == stateName) return;
        _currentState = stateName;
        _anim.CrossFadeInFixedTime(stateName, duration, 0);
    }

    /// <summary>
    /// Crossfade unconditionally — used by one-shot events so they always
    /// interrupt whatever is currently playing.
    /// </summary>
    private void ForceCrossFade(string stateName, float duration)
    {
        if (_anim == null) return;
        _currentState = stateName;
        _anim.CrossFadeInFixedTime(stateName, duration, 0);
    }
}
