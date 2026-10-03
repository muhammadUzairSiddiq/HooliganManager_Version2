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
    public enum StandingOrder { None, Guard, Patrol }
    public StandingOrder CurrentOrder { get; private set; }
    Vector3 _orderStart, _orderEnd;
    bool _patrolReturning;
    const float DefenceRadius = 12f;
    NavMeshPath _commandPath;
    public Bounds GuardRegion { get; private set; }
    bool rectangularGuard;

    // ── Data (set by BattleManager on spawn) ──────────────────────────────
    public AgentData Data { get; private set; }

    // ── Runtime ───────────────────────────────────────────────────────────
    public float CurrentHp  { get; private set; }
    public bool  IsAlive    => CurrentHp > 0;
    public bool  IsSelected { get; private set; }
    /// <summary>True while carrying out an order; selection and manual commands remain available.</summary>
    public bool  IsOnAssignment { get; private set; }
    /// <summary>Scales hits during a chosen gang fight so larger firms last longer.</summary>
    public float FightPace = 1f;
    static readonly Color SelectedRing = new Color(.12f, 1f, .36f, 1f);
    static readonly Color BusyRing = new Color(1f, .78f, .12f, 1f);
    public Vector3 CommandDestination => _moveTarget;
    public bool IsTargetingFirm(string firm)=>_target&&_target.firmName==firm;

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
    int _hitsTaken;
    private float             _scanTimer;
    private UnitAffiliationMarker _affiliationMarker;

    private const float SCAN_INTERVAL = 0.5f;

    // When true the agent stands still in idle — used during the pre-battle
    // cinematic intro so the camera can reveal each team without them fighting.
    private bool _cinematicIdle;
    private bool _activityLocked;
    bool _jobHold;
    public bool IsActivityLocked => _activityLocked;

    public GameObject modelPrefab { get; private set; }

    // ─────────────────────────────────────────────────────────────────────

    public void Initialise(AgentData data, Vector3 retreatPoint, CharacterPortraitRegistry visualRegistry = null)
    {
        Data = data;
        setup(visualRegistry);
        
        Data          = data;
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
        attackInterval = GameplayTuning.Current.attackInterval;
        _attackTimer = Random.Range(0f, attackInterval * .4f);

        // Health bar
        healthBar?.Initialise(transform, false);
        RefreshHealthBar();

        // Selection indicator — hollow ring only while selected (no always-on rings).
        if (selectionCircle != null)
        {
            selectionCircle.transform.localScale *= 2f;
            selectionCircle.SetActive(false);
        }
        _affiliationMarker = UnitAffiliationMarker.Attach(transform, new Color(.12f, 1f, .36f, 1f), 1.28f, true);
        _affiliationMarker?.SetVisible(false);

        // Make controlled people and their tap targets clearly readable on phones.
        // NavMesh spacing stays unchanged so larger visuals do not block streets.
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null) { capsule.radius = .58f; capsule.height = 3.2f; capsule.center = Vector3.up * 1.6f; }

        SetState(State.Idle);
    }

    private void setup(CharacterPortraitRegistry visualRegistry = null)
    {
        var registry = visualRegistry != null ? visualRegistry : BattleManager.instance.portraitRegistry;

        // Pick a random model prefab from the portrait registry.
        // Falls back to no visual model if the registry is not configured.
        if (registry == null || registry.entries == null || registry.entries.Count == 0)
        {
            Debug.LogWarning("[AgentController] portraitRegistry is empty — no character model will be spawned.");
            return;
        }

        int index = Mathf.Clamp(Data != null ? Data.PortraitIndex : Random.Range(0, registry.entries.Count), 0, registry.entries.Count - 1);
        var entry = registry.entries[index];
        GameObject characterPrefab = entry.optimizedModelPrefab ? entry.optimizedModelPrefab : entry.modelPrefab;
        if (characterPrefab == null) return;

        modelPrefab = characterPrefab;

        GameObject spawnedCharacter = Instantiate(characterPrefab, transform.position, transform.rotation, transform);
        GameplayTuning.ScaleModel(spawnedCharacter.transform);
        // Friendly tactical identity is green, matching the squad markers (not the club-brand save color).
        CrewKit.PaintShirt(spawnedCharacter.transform, new Color(.12f,.85f,.25f));
        CityCharacterBudget.Apply(spawnedCharacter);

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
        if (selectionCircle) selectionCircle.SetActive(false);
        RefreshOrderRing();
    }

    public void BeginAssignment()
    {
        if (!IsAlive) return;
        IsOnAssignment = true;
        RefreshOrderRing();
    }

    public void EndAssignment()
    {
        IsOnAssignment = false;
        FightPace = 1f;
        RefreshOrderRing();
    }

    void RefreshOrderRing()
    {
        if (_affiliationMarker == null) return;
        if (IsSelected)
        {
            _affiliationMarker.KeepVisibleWhenIdle = false;
            _affiliationMarker.ApplyColor(SelectedRing);
            _affiliationMarker.SetVisible(true);
            _affiliationMarker.SetSelected(true);
        }
        else
        {
            _affiliationMarker.KeepVisibleWhenIdle = false;
            _affiliationMarker.SetVisible(false);
        }
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

    /// <summary>Temporarily assigns this member to a non-combat city operation.</summary>
    public void SetActivityLocked(bool locked, Vector3 facePoint)
    {
        _activityLocked = locked;
        if (locked) BeginAssignment();
        else if (CurrentState == State.Idle) EndAssignment();
        if (_nav != null && _nav.enabled && _nav.isOnNavMesh)
        {
            _nav.isStopped = locked;
            if (locked) _nav.ResetPath();
        }
        if (locked)
        {
            CurrentOrder = StandingOrder.None;
            _target = null;
            SetState(State.Idle);
            Vector3 direction = facePoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .05f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Stay on the yellow ring while a live city job walks this member around a building.</summary>
    public void BeginJob()
    {
        if (!IsAlive) return;
        _jobHold = true;
        BeginAssignment();
    }

    public bool IsOnLiveJob => _jobHold;

    public void EndJob()
    {
        _jobHold = false;
        if (CurrentState == State.Idle) EndAssignment();
    }

    /// <summary>Move during a live job without freezing on the activity lock.</summary>
    public void JobMoveTo(Vector3 point)
    {
        if (!IsAlive || _cinematicIdle || _nav == null || !_nav.isOnNavMesh) return;
        CurrentOrder = StandingOrder.None;
        _jobHold = true;
        _activityLocked = false;
        _nav.isStopped = false;
        if (!NavMesh.SamplePosition(point, out var destination, 4f, _nav.areaMask)) return;
        var path = new NavMeshPath();
        if (!_nav.CalculatePath(destination.position, path) || path.status != NavMeshPathStatus.PathComplete) return;
        _moveTarget = destination.position;
        _target = null;
        BeginAssignment();
        SetState(State.MovingToPoint);
        _nav.SetDestination(destination.position);
    }

    /// <summary>Move to a world-space point, then return to Idle.</summary>
    public void CommandMoveTo(Vector3 point)
    {
        TryCommandMoveTo(point);
    }

    public bool TryCommandMoveTo(Vector3 point)
    {
        if (!TryNavigate(point)) return false;
        CurrentOrder = StandingOrder.None;
        if (_jobHold) EndJob();
        return true;
    }

    bool TryNavigate(Vector3 point)
    {
        if (!IsAlive || _cinematicIdle || _activityLocked || !_nav || !_nav.enabled || !_nav.isOnNavMesh) return false;
        if (!NavMesh.SamplePosition(point, out var destination, 3f, _nav.areaMask)) return false;
        if (_commandPath == null) _commandPath = new NavMeshPath();
        if (!_nav.CalculatePath(destination.position, _commandPath) || _commandPath.status != NavMeshPathStatus.PathComplete) return false;
        _nav.isStopped = false;
        _nav.stoppingDistance = .15f;
        if (!_nav.SetPath(_commandPath)) return false;
        _moveTarget = destination.position;
        _target = null;
        BeginAssignment();
        SetState(State.MovingToPoint);
        return true;
    }

    /// <summary>Guard the destination, or patrol from the current position to it and back.</summary>
    public bool CommandArea(Vector3 point, bool patrol)
    {
        rectangularGuard = false;
        Vector3 start = transform.position;
        if (!TryNavigate(point)) return false;
        if (_jobHold) EndJob();
        CurrentOrder = patrol ? StandingOrder.Patrol : StandingOrder.Guard;
        _orderStart = patrol ? start : _moveTarget;
        _orderEnd = _moveTarget;
        _patrolReturning = false;
        return true;
    }

    public bool HasPatrolRoute(Vector3 a,Vector3 b) => CurrentOrder==StandingOrder.Patrol && (_orderStart-a).sqrMagnitude<1 && (_orderEnd-b).sqrMagnitude<1;
    public bool CommandGuard(Bounds area)
    { return CommandGuard(area,area.center); }

    public bool CommandGuard(Bounds area, Vector3 post)
    {
        if (!CommandArea(post, false)) return false;
        GuardRegion = area;
        rectangularGuard = true;
        return true;
    }

    public bool CommandPatrol(Vector3 a, Vector3 b)
    {
        if (!_nav || !_nav.enabled || !_nav.isOnNavMesh) return false;
        if (!NavMesh.SamplePosition(a, out var start, 3f, _nav.areaMask) || !NavMesh.SamplePosition(b, out var end, 3f, _nav.areaMask)) return false;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, end.position, _nav.areaMask, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        if (!TryNavigate(start.position)) return false;
        if (_jobHold) EndJob();
        rectangularGuard = false;
        CurrentOrder = StandingOrder.Patrol;
        _orderStart = start.position; _orderEnd = end.position; _patrolReturning = true;
        return true;
    }

    float DistanceFromDuty(Vector3 position)
    {
        if (CurrentOrder == StandingOrder.Guard && rectangularGuard)
        {
            position.y = GuardRegion.center.y;
            return Vector3.Distance(position, GuardRegion.ClosestPoint(position));
        }
        Vector3 route = _orderEnd - _orderStart;
        float t = route.sqrMagnitude > .01f ? Mathf.Clamp01(Vector3.Dot(position - _orderStart, route) / route.sqrMagnitude) : 0f;
        return Vector3.Distance(position, _orderStart + route * t);
    }

    bool IsDutyThreat(EnemyController enemy)
    {
        return enemy && enemy.IsAlive && enemy.gameObject.activeInHierarchy && !enemy.IsHomeMatchdaySupporter && !RivalSettlement.IsSettled(enemy.firmName)
            && (enemy.firmName != "POLICE" || enemy.isHostile)
            && DistanceFromDuty(enemy.transform.position) <= (rectangularGuard && CurrentOrder == StandingOrder.Guard ? 3f : DefenceRadius)
            && Vector3.Distance(transform.position, enemy.transform.position) <= (rectangularGuard && CurrentOrder == StandingOrder.Guard ? GuardRegion.size.magnitude + 3f : DefenceRadius);
    }

    void ScanDutyThreats()
    {
        if (!BattleManager.instance) return;
        EnemyController nearest = null;
        float best = float.MaxValue;
        foreach (var enemy in BattleManager.instance.EnemyAgents)
        {
            if (!IsDutyThreat(enemy)) continue;
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distance < best) { best = distance; nearest = enemy; }
        }
        if (nearest) EngageForDefence(nearest);
    }

    void EngageForDefence(EnemyController enemy)
    {
        _target = enemy;
        BeginAssignment();
        SetState(State.AutoAttacking);
    }

    void ResumeDuty()
    {
        _target = null;
        if (!TryNavigate(_patrolReturning ? _orderStart : _orderEnd))
        {
            if (_nav && _nav.enabled && _nav.isOnNavMesh) _nav.ResetPath();
            SetState(State.Idle);
        }
    }

    /// <summary>Switch to aggressive auto-attack mode.</summary>
    public void CommandAttack()
    {
        if (!IsAlive) return;
        if (_jobHold) EndJob();
        if (_activityLocked) return;
        CurrentOrder = StandingOrder.None;
        BeginAssignment();
        SetState(State.AutoAttacking);
    }

    /// <summary>Target a specific enemy to attack.</summary>
    public void CommandAttackTarget(EnemyController target)
    {
        if (!IsAlive) return;
        if (_jobHold) EndJob();
        if (_activityLocked) return;
        CurrentOrder = StandingOrder.None;
        _target = target;
        BeginAssignment();
        SetState(State.AutoAttacking);
    }

    /// <summary>Move back to spawn / retreat zone.</summary>
    public void CommandRetreat()
    {
        if (!IsAlive || _cinematicIdle || !_nav || !_nav.enabled || !_nav.isOnNavMesh) return;
        if (_jobHold) EndJob();
        if (_activityLocked) return;
        CurrentOrder = StandingOrder.None;
        _target = null;
        _nav.isStopped = false;
        _nav.stoppingDistance = .15f;
        BeginAssignment();
        SetState(State.Retreating);
        _nav.SetDestination(_retreatPoint);
    }

    // ── Damage ────────────────────────────────────────────────────────────
    public void PlayStreetPunch()
    {
        if (!IsAlive) return;
        _anim?.PlayAttack();
    }

    public void TakeDamage(float amount, EnemyController attacker = null)
    {
        if (!IsAlive) return;
        float guard = Data != null ? Mathf.Clamp(Data.Intelligence, 0, 8) * 0.03f : 0f;
        if (amount > 0) StreetCombatPresentation.Hit(transform.position);
        CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(0, amount) * (1f - guard) * Mathf.Clamp(FightPace, 0.2f, 1f));
        SyncHpToData();
        RefreshHealthBar();
        if (CurrentHp <= 0) GameManager.Save();

        if (CurrentHp <= 0) { Die(); return; }

        _hitsTaken++;
        if (_hitsTaken % 4 == 0) _anim?.PlayKnockdown();
        else _anim?.PlayHit();
        if (!_injuryReported && CurrentHp <= Data.MaxHp * .3f) { _injuryReported = true; InjuryNotifications.Report(Data); }

        // Defend idle members and standing duties, but let an explicit MOVE or
        // RETREAT pull a member out of danger even while blows are landing.
        bool manualMovement = CurrentOrder == StandingOrder.None &&
            (CurrentState == State.MovingToPoint || CurrentState == State.Retreating);
        if (!manualMovement && !_activityLocked && !_cinematicIdle && CurrentState != State.AutoAttacking && CurrentState != State.Dead)
        {
            var nearest = attacker && attacker.IsAlive ? attacker : FindNearestEnemy();
            if (nearest != null)
                EngageForDefence(nearest);
            else
                CommandAttack();
            BattleManager.instance?.AlertNearbyCrew(this);
        }
    }

    private void Die()
    {
        CurrentOrder = StandingOrder.None;
        AgentSelectionManager.instance?.Deselect(this);
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
        yield return new WaitForSeconds(_anim!=null?_anim.DeathSeconds:2f);
        StreetCombatPresentation.LeaveBody(transform);
        gameObject.SetActive(false);
        BattleManager.instance?.OnAgentDied(this);
    }

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        if (!IsAlive) return;

        // During the pre-battle cinematic intro agents stand still in idle.
        // Drive animation only — skip all combat and navigation logic.
        if (_cinematicIdle || _activityLocked)
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
            if (CurrentOrder != StandingOrder.None && (!_target || !_target.IsAlive))
                ScanDutyThreats();
            else if (CurrentState == State.AutoAttacking && (_target == null || !_target.IsAlive))
                _target = FindNearestEnemy();
        }

        // Drive animation — AgentAnimController handles idle vs run crossfade.
        if(_nav&&CurrentState==State.MovingToPoint)_nav.speed=Data.Speed*(CrewMovementPreference.RunOnTap?1f:.52f);
        else if(_nav&&CurrentState==State.AutoAttacking)_nav.speed=Data.Speed;
        float speed = _nav.enabled ? _nav.velocity.magnitude : 0f;
        _anim?.Tick(speed / Mathf.Max(.1f, Data.Speed), CurrentState == State.AutoAttacking);

        switch (CurrentState)
        {
            case State.Idle:
                // Don't spam ResetPath every frame — it freezes agents in place.
                if (_nav.hasPath || _nav.pathPending)
                    _nav.ResetPath();
                break;

            case State.MovingToPoint:
                if (!_nav.pathPending && _nav.remainingDistance <= _nav.stoppingDistance + .1f)
                {
                    SetState(State.Idle);
                    if (CurrentOrder == StandingOrder.Patrol && (_orderEnd - _orderStart).sqrMagnitude > 1f)
                    {
                        _patrolReturning = !_patrolReturning;
                        ResumeDuty();
                    }
                    else if (CurrentOrder == StandingOrder.None && !_activityLocked && !_jobHold) EndAssignment();
                }
                break;

            case State.Retreating:
                if (!_nav.pathPending && _nav.remainingDistance <= _nav.stoppingDistance + .1f)
                {
                    SetState(State.Idle);
                    EndAssignment();
                }
                break;

            case State.AutoAttacking:
                if (CurrentOrder != StandingOrder.None && !IsDutyThreat(_target))
                {
                    ResumeDuty();
                    break;
                }
                if (_target == null || !_target.IsAlive)
                {
                    _target = FindNearestEnemy();
                    if (_target == null) { SetState(State.Idle); EndAssignment(); break; }
                }
                float dist = DistanceTo(_target.transform);
                if (dist > Data.AttackRange)
                {
                    _nav.stoppingDistance = Data.AttackRange * .9f;
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

        _pendingTarget = _target;
        _impactPending = true;
        StartCoroutine(ImpactAfterDelay());
    }
    EnemyController _pendingTarget;
    bool _impactPending;
    IEnumerator ImpactAfterDelay()
    {
        yield return new WaitForSeconds(GameplayTuning.Current.impactDelay);
        AnimEvent_DealDamage();
    }
    public void AnimEvent_DealDamage()
    {
        if (!_impactPending) return;
        _impactPending = false;
        if (!IsAlive || _cinematicIdle || CurrentState != State.AutoAttacking || !_pendingTarget || !_pendingTarget.IsAlive) return;
        if (DistanceTo(_pendingTarget.transform) > Data.AttackRange + .35f) return;
        float staminaCap = Data != null && Data.MaxStamina > 1f ? Data.MaxStamina : 100f;
        float tired = Data != null && Data.Stamina / staminaCap < 0.35f ? 0.75f : 1f;
        _pendingTarget.TakeDamage(Mathf.Max(1, (Data.Strength + Random.Range(-1f, 1f)) * GameplayTuning.Current.playerDamageMultiplier * _strengthMultiplier * tired), this);
        GameAudio.Play("impact");
    }
    public void RestoreFromData()
    {
        RestoreFromData(Data);
    }

    public void RestoreFromData(AgentData data)
    {
        if (data != null) Data = data;
        StopAllCoroutines(); _injuryReported = false; _activeBoosts.Clear(); _strengthMultiplier = 1; _impactPending = false;
        gameObject.SetActive(true); CurrentHp = Data.CurrentHp;
        _nav.enabled = true; if (_nav.isOnNavMesh) _nav.ResetPath();
        ApplyRosterStats(); _nav.isStopped = false; _target = null; _cinematicIdle = false; _activityLocked = false;
        CurrentOrder = StandingOrder.None;
        _anim = new AgentAnimController(animator); SetState(State.Idle); RefreshHealthBar();
    }

    // ── Joystick (called by VirtualJoystick) ─────────────────────────────
    /// <summary>Direct joystick override — moves agent in direction at walk speed.</summary>
    public void ApplyJoystickVelocity(Vector2 dir)
    {
        if (!IsAlive || CurrentState == State.Dead || !_nav.enabled) return;

        if (dir.sqrMagnitude > 0.01f)
        {
            CurrentOrder = StandingOrder.None;
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
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), GameplayTuning.Current.turnSpeed * Time.deltaTime);
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

    /// <summary>Push saved health, power and speed onto the live crew member.</summary>
    public void ApplyRosterStats()
    {
        if (Data == null) return;
        if (Data.MaxStamina < 1f) Data.MaxStamina = 100f;
        CurrentHp = Mathf.Clamp(Data.CurrentHp, 0f, Data.MaxHp);
        if (_nav) _nav.speed = Data.Speed;
        RefreshHealthBar();
    }

    public void ApplyGrowth()
    {
        if (Data == null) return;
        CurrentHp = Mathf.Min(Data.MaxHp, CurrentHp + 8f);
        SyncHpToData();
        if (_nav) _nav.speed = Data.Speed;
        RefreshHealthBar();
    }

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
    private bool _injuryReported;
    private float _strengthMultiplier = 1;
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
                if (_nav != null) _nav.speed = Data.Speed * (1 + amount);
                Debug.Log($"[AgentController] {Data.AgentName} speed boosted +{amount * 100f:F0}% for {duration}s");
                break;
            case BoostType.Strength:
                _strengthMultiplier = 1 + amount;
                Debug.Log($"[AgentController] {Data.AgentName} strength boosted +{amount * 100f:F0}% for {duration}s");
                break;
        }

        yield return new WaitForSeconds(duration);

        // Revert — only if still alive
        if (!IsAlive || Data == null) yield break;
        switch (type)
        {
            case BoostType.Speed:
                if (_nav != null) _nav.speed = Data.Speed;
                break;
            case BoostType.Strength:
                _strengthMultiplier = 1;
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

public static class CrewKit
{
    public static Color RivalColor(Color color,string firm)
    {
        Color.RGBToHSV(color,out var hue,out var saturation,out var value);
        if((hue>.20f&&hue<.47f)||(hue>.53f&&hue<.70f)||saturation<.25f)
        {
            uint hash=2166136261;foreach(char c in firm??"Rival")hash=(hash^c)*16777619;
            float[] hues={.0f,.075f,.13f,.78f,.9f};color=Color.HSVToRGB(hues[hash%(uint)hues.Length],.78f,.85f);
        }
        color.a=1;return color;
    }
    static readonly MaterialPropertyBlock kitBlock=new MaterialPropertyBlock();
    static Material plainShirt;
    public static bool IsShirtRenderer(string value)
    {
        string n=value.ToLowerInvariant();
        return n.Contains("shirt")||n.Contains("hoodie")||n.Contains("hoody")||n.Contains("sweater")||n.Contains("jacket")||n.Contains("collar")||n.Contains("jersey");
    }
    public static void PaintShirt(Transform model, Color color)
    {
        if (!model) return;
        var enemy=model.GetComponentInParent<EnemyController>();
        if(model.GetComponentInParent<AgentController>())color=new Color(.025f,.32f,.12f);
        else if(enemy&&enemy.firmName!="POLICE")color=enemy.IsHomeMatchdaySupporter?new Color(.38f,.88f,.14f):RivalColor(color,enemy.firmName);
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            bool importedClothing=false;
            var slots=renderer.sharedMaterials;
            for(int slot=0;slot<slots.Length;slot++)
            {
                if(!slots[slot]||!slots[slot].name.StartsWith("FactionCloth"))continue;
                kitBlock.Clear();kitBlock.SetColor("_BaseColor",color);kitBlock.SetColor("_Color",color);
                renderer.SetPropertyBlock(kitBlock,slot);importedClothing=true;
            }
            if(importedClothing)continue;
            // Never infer "body" means shirt: many atlases include exposed skin.
            // Only an explicitly separated clothing renderer can lose its texture.
            if(!IsShirtRenderer(renderer.name))continue;
            if(!plainShirt)
            {
                plainShirt=Resources.Load<Material>("CharacterProduction/GangShirt");
                if(!plainShirt)
                {
                    var shader=Shader.Find("Universal Render Pipeline/Simple Lit")??Shader.Find("Universal Render Pipeline/Lit");
                    if(!shader)return;
                    plainShirt=new Material(shader){name="Gang Shirt · Plain"};
                    plainShirt.SetColor("_BaseColor",Color.white);plainShirt.SetFloat("_Smoothness",.12f);
                }
            }
            var mats=renderer.sharedMaterials;
            for(int i=0;i<mats.Length;i++)mats[i]=plainShirt;
            renderer.sharedMaterials=mats;
            kitBlock.Clear();kitBlock.SetColor("_BaseColor",color);kitBlock.SetColor("_Color",color);
            renderer.SetPropertyBlock(kitBlock);
        }
    }

    static bool IsShirt(string name)
    {
        name = name.ToLowerInvariant();
        return name.Contains("shirt") || name.Contains("top") || name.Contains("torso") || name.Contains("jacket") || name.Contains("jersey") || name.Contains("body") || name.Contains("cloth");
    }

    static bool IsPantsOrSkin(string name)
    {
        name = name.ToLowerInvariant();
        return name.Contains("pant") || name.Contains("trouser") || name.Contains("leg") || name.Contains("shoe") || name.Contains("boot") || name.Contains("hair") || name.Contains("head") || name.Contains("face") || name.Contains("skin") || name.Contains("eye");
    }

    public static void KeepGap(Transform self, float gap)
    {
        if (!self || BattleManager.instance == null) return;
        Vector3 pos = self.position;
        Vector3 push = Vector3.zero;
        void Consider(Vector3 other)
        {
            Vector3 delta = pos - other;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist < 0.05f) delta = new Vector3(0.4f, 0f, 0.2f);
            else if (dist >= gap) return;
            push += delta.normalized * (gap - dist);
        }
        foreach (var agent in BattleManager.instance.PlayerAgents)
            if (agent && agent.IsAlive && agent.transform != self) Consider(agent.transform.position);
        foreach (var enemy in BattleManager.instance.EnemyAgents)
            if (enemy && enemy.IsAlive && enemy.transform != self) Consider(enemy.transform.position);
        if (push.sqrMagnitude < 0.0001f) return;
        pos += push;
        var nav = self.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav && nav.enabled && nav.isOnNavMesh) nav.Warp(pos);
        else self.position = pos;
    }
}

