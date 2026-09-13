using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls a single player-side 3D agent in the battle scene.
///
/// Required components on the same GameObject (or child for Animator):
///   - NavMeshAgent          — handles pathfinding and movement
///   - CapsuleCollider       — used for selection raycasting
///   - Animator              — 3D character animator (CharacterAnimatorController)
///   - AgentHealthBar        — world-space HP bar (child GO)
///   - SelectionCircle       — dashed circle projector (child GO)
///
/// Animation is fully code-driven via AgentAnimController.
/// The Animator Controller asset needs only states — no parameters, no transitions.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
public class AgentController : MonoBehaviour
{
    // ── State machine ─────────────────────────────────────────────────────
    public enum State { Idle, MovingToPoint, AutoAttacking, Retreating, Dead }
    public State CurrentState { get; private set; } = State.Idle;

    // ── Data (set by BattleManager on spawn) ──────────────────────────────
    public AgentData Data { get; private set; }

    // ── Runtime ───────────────────────────────────────────────────────────
    public float CurrentHp  { get; private set; }
    public bool  IsAlive    => CurrentHp > 0;
    public bool  IsSelected { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("References")]
    public AgentHealthBar healthBar;
    public GameObject     selectionCircle;

    [Header("Animator")]
    [Tooltip("Leave null to auto-find in children")]
    public Animator animator;

    [Header("Attack Settings")]
    [Tooltip("Seconds between attacks")]
    public float attackInterval = 1.2f;

    [Tooltip("Extra range auto-engagement kicks in (multiplier of AttackRange)")]
    public float engageRangeMultiplier = 3f;

    // ── Internal ──────────────────────────────────────────────────────────
    private NavMeshAgent      _nav;
    private AgentAnimController _anim;      // ← code-driven animation helper
    private EnemyController   _target;
    private Vector3           _moveTarget;
    private Vector3           _retreatPoint;
    private float             _attackTimer;
    private float             _scanTimer;

    private const float SCAN_INTERVAL = 0.5f;

    // When true the agent stands still in idle — used during the pre-battle
    // cinematic intro so the camera can reveal each team without them fighting.
    private bool _cinematicIdle;

    public GameObject modelPrefab { get; private set; }

    // ─────────────────────────────────────────────────────────────────────

    public void Initialise(AgentData data, Vector3 retreatPoint)
    {
        setup();
        
        Data          = data;
        // Player firm starts stronger than early-level rival gangs (who are at ~30% HP).
        if (data.MaxHp < 90f)
        {
            data.MaxHp *= 1.35f;
            data.CurrentHp = data.MaxHp;
            data.Strength *= 1.25f;
        }
        CurrentHp     = data.CurrentHp;
        _retreatPoint = retreatPoint;

        // Nav agent
        _nav                  = GetComponent<NavMeshAgent>();
        _nav.speed            = data.Speed;
        _nav.stoppingDistance = data.AttackRange * 0.9f;
        _nav.angularSpeed     = 360f;
        _nav.acceleration     = 12f;

        // Animator — auto-find if not assigned in Inspector
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Build the code-driven animation controller.
        // It randomises run style internally so each agent looks different.
        _anim = new AgentAnimController(animator);

        // Health bar
        healthBar?.Initialise(transform, false);
        RefreshHealthBar();

        // Selection indicator
        selectionCircle?.SetActive(false);

        SetState(State.Idle);
    }

    private void setup()
    {
        var registry = BattleManager.instance.portraitRegistry;

        // Pick a random model prefab from the portrait registry.
        // Falls back to no visual model if the registry is not configured.
        if (registry == null || registry.entries == null || registry.entries.Count == 0)
        {
            Debug.LogWarning("[AgentController] portraitRegistry is empty — no character model will be spawned.");
            return;
        }

        int index = Mathf.RoundToInt(Random.Range(0, registry.entries.Count - 1));
        var entry = registry.entries[index];
        GameObject characterPrefab = entry.modelPrefab;
        if (characterPrefab == null) return;

        modelPrefab = characterPrefab;

        GameObject spawnedCharacter = Instantiate(characterPrefab, transform.position, transform.rotation, transform);

        // Add the Animator component dynamically
        animator = spawnedCharacter.GetComponent<Animator>();
        if (!animator) animator=spawnedCharacter.AddComponent<Animator>();
        animator.runtimeAnimatorController = entry.animatorController != null ? entry.animatorController : BattleManager.instance.characterAnimator;

        // Set rendering layer to match the agent so they receive the same lighting.
        SkinnedMeshRenderer[] skinnedMeshRenderers = spawnedCharacter.transform
            .Cast<Transform>()
            .Select(child => child.GetComponent<SkinnedMeshRenderer>())
            .Where(renderer => renderer != null)
            .ToArray();
        var renderLayers = RenderingLayerMask.GetMask("Default", "Agent Light Layer");
        foreach (SkinnedMeshRenderer smr in skinnedMeshRenderers)
        {
            Debug.Log($"Setting rendering layer for {smr.gameObject.name}");
            smr.renderingLayerMask = renderLayers;
        }
    }

    // ── Selection ─────────────────────────────────────────────────────────
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if(selectionCircle)selectionCircle.SetActive(selected);
    }

    // ── Cinematic idle lock ───────────────────────────────────────────────
    /// <summary>
    /// When locked = true the agent freezes in idle (NavMesh stopped, no combat).
    /// Call with false after the intro sequence finishes to release normal AI.
    /// </summary>
    public void SetCinematicIdle(bool locked)
    {
        _cinematicIdle = locked;
        if (locked)
        {
            _nav.ResetPath();
            SetState(State.Idle);
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Move to a world-space point, then return to Idle.</summary>
    public void CommandMoveTo(Vector3 point)
    {
        if (!IsAlive || _cinematicIdle || _nav == null || !_nav.isOnNavMesh) return;
        if (!NavMesh.SamplePosition(point,out var destination,3f,_nav.areaMask)) return;
        var path=new NavMeshPath();
        if (!_nav.CalculatePath(destination.position,path) || path.status!=NavMeshPathStatus.PathComplete) return;
        point=destination.position;
        _moveTarget = point;
        _target     = null;
        SetState(State.MovingToPoint);
        _nav.SetDestination(point);
    }

    /// <summary>Switch to aggressive auto-attack mode.</summary>
    public void CommandAttack()
    {
        if (!IsAlive) return;
        SetState(State.AutoAttacking);
    }

    /// <summary>Target a specific enemy to attack.</summary>
    public void CommandAttackTarget(EnemyController target)
    {
        if (!IsAlive) return;
        _target = target;
        SetState(State.AutoAttacking);
    }

    /// <summary>Move back to spawn / retreat zone.</summary>
    public void CommandRetreat()
    {
        if (!IsAlive) return;
        _target = null;
        SetState(State.Retreating);
        _nav.SetDestination(_retreatPoint);
    }

    // ── Damage ────────────────────────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;
        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        RefreshHealthBar();

        if (CurrentHp <= 0) { Die(); return; }

        _anim?.PlayHit();

        // Always fight back when punched — even if the turf popup was skipped.
        if (!_cinematicIdle && CurrentState != State.AutoAttacking && CurrentState != State.Dead)
        {
            var nearest = FindNearestEnemy();
            if (nearest != null)
                CommandAttackTarget(nearest);
            else
                CommandAttack();
            BattleManager.instance?.OrderAllPlayersAttackNearestHostile();
        }
    }

    private void Die()
    {
        SetState(State.Dead);
        if (Data != null) Data.CurrentHp = 0;

        _nav.ResetPath();
        _nav.enabled = false;

        _anim?.PlayDie();
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // Wait for the death animation to finish before disabling the GO.
        yield return new WaitForSeconds(1.8f);
        gameObject.SetActive(false);
        BattleManager.instance?.OnAgentDied(this);
    }

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        if (!IsAlive) return;

        // During the pre-battle cinematic intro agents stand still in idle.
        // Drive animation only — skip all combat and navigation logic.
        if (_cinematicIdle)
        {
            _anim?.Tick(0f);
            return;
        }

        _attackTimer += Time.deltaTime;
        _scanTimer   += Time.deltaTime;

        // Periodic enemy scan — only refresh target when the current one is gone.
        // Overwriting every tick cleared CommandAttackTarget and dropped agents to Idle.
        if (_scanTimer >= SCAN_INTERVAL)
        {
            _scanTimer = 0;
            if (CurrentState == State.AutoAttacking && (_target == null || !_target.IsAlive))
                _target = FindNearestEnemy();
        }

        // Drive animation — AgentAnimController handles idle vs run crossfade.
        float speed = _nav.enabled ? _nav.velocity.magnitude : 0f;
        _anim?.Tick(speed);

        switch (CurrentState)
        {
            case State.Idle:
                // Don't spam ResetPath every frame — it freezes agents in place.
                if (_nav.hasPath || _nav.pathPending)
                    _nav.ResetPath();
                break;

            case State.MovingToPoint:
                if (!_nav.pathPending && _nav.remainingDistance < 0.2f)
                    SetState(State.Idle);
                break;

            case State.Retreating:
                if (!_nav.pathPending && _nav.remainingDistance < 0.2f)
                    SetState(State.Idle);
                break;

            case State.AutoAttacking:
                if (_target == null || !_target.IsAlive)
                {
                    _target = FindNearestEnemy();
                    if (_target == null) { SetState(State.Idle); break; }
                }
                float dist = DistanceTo(_target.transform);
                if (dist > Data.AttackRange)
                {
                    _nav.SetDestination(_target.transform.position);
                }
                else
                {
                    _nav.ResetPath();
                    FaceTarget(_target.transform.position);
                    TryAttack();
                }
                break;
        }
    }

    // ── Attack ────────────────────────────────────────────────────────────
    private void TryAttack()
    {
        if (_attackTimer < attackInterval) return;
        _attackTimer = 0;

        // AgentAnimController picks a random punch variant and locks Tick()
        // for AttackLockDuration seconds so the clip plays fully.
        _anim?.PlayAttack();

        float damage = Data.Strength + Random.Range(-2f, 2f);
        Debug.Log($"{Data.AgentName} attacks {_target.gameObject.name} for {damage:F1} dmg");
        // Pass attacker so enemies flip hostile and fight back.
        _target?.TakeDamage(damage, this);
    }

    // ── Called by Animation Event on the Attack clip (optional) ──────────
    /// <summary>
    /// Wire to the impact frame of any attack clip as an Animation Event
    /// to apply damage in sync with the animation instead of immediately.
    /// </summary>
    public void AnimEvent_DealDamage()
    {
        if (_target != null && _target.IsAlive)
        {
            float damage = Data.Strength + Random.Range(-2f, 2f);
            _target.TakeDamage(damage, this);
        }
    }

    // ── Joystick (called by VirtualJoystick) ─────────────────────────────
    /// <summary>Direct joystick override — moves agent in direction at walk speed.</summary>
    public void ApplyJoystickVelocity(Vector2 dir)
    {
        if (!IsAlive || CurrentState == State.Dead || !_nav.enabled) return;

        if (dir.sqrMagnitude > 0.01f)
        {
            Vector3 worldDir = new Vector3(dir.x, 0, dir.y).normalized;
            Vector3 target   = transform.position + worldDir * 2f;
            _nav.SetDestination(target);
            SetState(State.MovingToPoint);
        }
        else
        {
            _nav.ResetPath();
            SetState(State.Idle);
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────
    private EnemyController FindNearestEnemy() =>
        BattleManager.instance?.GetNearestEnemy(transform.position);

    private float DistanceTo(Transform other) =>
        Vector3.Distance(transform.position, other.position);

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    private void SetState(State s)
    {
        CurrentState = s;
        // Animation is driven entirely by Tick() / one-shot calls above.
        // No animator parameters needed.
    }

    private void RefreshHealthBar()
    {
        if (Data != null) healthBar?.SetHealth(CurrentHp, Data.MaxHp);
    }

    /// <summary>Snapshot runtime HP back into AgentData before saving.</summary>
    public void SyncHpToData() { if (Data != null) Data.CurrentHp = CurrentHp; }

    // ── Building Interactions ──────────────────────────────────────────────

    /// <summary>
    /// Restores <paramref name="amount"/> HP, clamped to MaxHp.
    /// Called by <see cref="MapBuilding"/> when the player uses a Heal action.
    /// </summary>
    public void HealAmount(float amount)
    {
        if (!IsAlive || Data == null) return;
        CurrentHp = Mathf.Min(Data.MaxHp, CurrentHp + amount);
        RefreshHealthBar();
        Debug.Log($"[AgentController] {Data.AgentName} healed +{amount:F1} HP (now {CurrentHp:F1}/{Data.MaxHp:F1})");
    }

    /// <summary>Permanent session buff used by the level-up power-boost offer.</summary>
    public void ApplyPowerBoost(float hpMul, float strMul)
    {
        if (!IsAlive || Data == null) return;
        Data.MaxHp *= hpMul;
        CurrentHp = Mathf.Min(Data.MaxHp, CurrentHp * hpMul);
        Data.Strength *= strMul;
        RefreshHealthBar();
    }

    // Tracks active boost coroutines by boost type so they can be cancelled if
    // a new boost of the same type is applied before the old one expires.
    private Dictionary<BoostType, Coroutine> _activeBoosts = new Dictionary<BoostType, Coroutine>();

    /// <summary>
    /// Temporarily multiplies Speed or Strength by (1 + <paramref name="amount"/>)
    /// for <paramref name="duration"/> seconds, then reverts.
    /// Stacks are replaced, not summed — re-applying the same boost type
    /// restarts the timer at the full duration.
    /// </summary>
    public void ApplyTemporaryBoost(BoostType type, float amount, float duration)
    {
        if (!IsAlive || Data == null) return;

        // Cancel existing boost of the same type so we don't double-revert.
        if (_activeBoosts.TryGetValue(type, out Coroutine existing) && existing != null)
            StopCoroutine(existing);

        Coroutine c = StartCoroutine(RunTemporaryBoost(type, amount, duration));
        _activeBoosts[type] = c;
    }

    private IEnumerator RunTemporaryBoost(BoostType type, float amount, float duration)
    {
        // Apply
        switch (type)
        {
            case BoostType.Speed:
                if (_nav != null) _nav.speed += Data.Speed * amount;
                Debug.Log($"[AgentController] {Data.AgentName} speed boosted +{amount * 100f:F0}% for {duration}s");
                break;
            case BoostType.Strength:
                Data.Strength += Data.Strength * amount;
                Debug.Log($"[AgentController] {Data.AgentName} strength boosted +{amount * 100f:F0}% for {duration}s");
                break;
        }

        yield return new WaitForSeconds(duration);

        // Revert — only if still alive
        if (!IsAlive || Data == null) yield break;
        switch (type)
        {
            case BoostType.Speed:
                if (_nav != null) _nav.speed -= Data.Speed * amount;
                break;
            case BoostType.Strength:
                Data.Strength -= Data.Strength * amount;
                break;
        }

        _activeBoosts.Remove(type);
        Debug.Log($"[AgentController] {Data.AgentName} {type} boost expired.");
    }

    private void Start()
    {
        MiniMapIconFactory.Register(transform, MiniMapIconFactory.Kind.Player);
    }

    private void OnDestroy()
    {
        if (Arikan.MiniMapView.Instance != null)
        {
            Arikan.MiniMapView.Instance.UnfollowTarget(transform);
        }
    }
}
