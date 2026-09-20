using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls a single enemy agent (rival hooligan or riot police) in 3D.
/// Fully autonomous — always hunts the nearest alive player agent.
///
/// Required components:
///   - NavMeshAgent    — pathfinding
///   - CapsuleCollider — hit detection
///   - Animator        — 3D character animator (CharacterAnimatorController)
///   - AgentHealthBar  — world-space HP bar child (set isEnemy = true)
///
/// Animation is fully code-driven via AgentAnimController.
/// The Animator Controller asset needs only states — no parameters, no transitions.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CapsuleCollider))]
public class EnemyController : MonoBehaviour
{
    // ── Stats (set by BattleManager.Initialise) ───────────────────────────
    public float MaxHp       = 60f;
    public float CurrentHp   { get; private set; }
    public float Strength    = 10f;
    public float Speed       = 2.0f;
    public float AttackRange = 1.0f;

    public bool IsAlive => CurrentHp > 0;

    // ── Inspector references ──────────────────────────────────────────────
    [Header("References")]
    public AgentHealthBar healthBar;

    [Header("Animator")]
    [Tooltip("Leave null to auto-find in children")]
    public Animator animator;

    [Header("Attack Settings")]
    public float attackInterval = 1.4f;

    // ── Internal ──────────────────────────────────────────────────────────
    private NavMeshAgent       _nav;
    private AgentAnimController _anim;      // ← code-driven animation helper
    private AgentController _targetPlayer;
    private EnemyController _targetEnemy;

    private Transform TargetTransform => _targetPlayer != null ? _targetPlayer.transform : (_targetEnemy != null ? _targetEnemy.transform : null);
    private bool IsTargetAlive => (_targetPlayer != null && _targetPlayer.IsAlive) || (_targetEnemy != null && _targetEnemy.IsAlive);

    private void SetTarget(MonoBehaviour target)
    {
        if (target is AgentController ac)
        {
            _targetPlayer = ac;
            _targetEnemy = null;
            _hasAggro = true;
        }
        else if (target is EnemyController ec)
        {
            _targetEnemy = ec;
            _targetPlayer = null;
            _hasAggro = true;
        }
        else
        {
            ClearTarget();
        }
    }

    private void ClearTarget()
    {
        _targetPlayer = null;
        _targetEnemy = null;
        _hasAggro = false;
    }

    private void DealDamageToTarget(float amount)
    {
        if (_targetPlayer != null) _targetPlayer.TakeDamage(amount);
        else if (_targetEnemy != null) _targetEnemy.TakeDamage(amount, this);
    }
    private float              _attackTimer;
    private float              _scanTimer;

    private const float SCAN_INTERVAL = 0.6f;

    // When true the enemy stands still in idle — used during the pre-battle
    // cinematic intro so the camera can show the gang without them charging.
    private bool _cinematicIdle;

    [Header("RTS Roaming")]
    public float detectionRadius = 12f;
    public float patrolRadius = 6f;        // Roaming radius around spawn position
    public float patrolSpeed = 0.8f;       // casual walking speed while patrolling
    public Vector3[] patrolWaypoints;
    private int _currentWaypointIndex;
    private Vector3 _spawnPosition;
    private Vector3 _gangCenter;           // centre of the gang group — used to face teammates while passive
    private bool _hasAggro;
    private float _chaseSpeed;         // full combat speed — stored on Initialise

    [Header("Faction / Gang Settings")]
    public string firmName = "";
    public Color primaryColor = Color.blue;
    public bool isHostile = false;
    public Transform defenceObjective;

    // ─────────────────────────────────────────────────────────────────────

    public void Initialise(float maxHp, float strength, float speed, float attackRange = 1.0f, CharacterPortraitRegistry characterPortraitRegistry = null)
    {
        // Passive until AttackGang / police fight / damage response flips this on.
        isHostile = false;
        setup(characterPortraitRegistry);

        MaxHp       = maxHp;
        CurrentHp   = maxHp;
        Strength    = strength;
        Speed       = speed;
        AttackRange = attackRange;

        _nav                   = GetComponent<NavMeshAgent>();
        _chaseSpeed            = speed;           // remember full combat speed
        _nav.speed             = patrolSpeed;     // start slow (patrol walk)
        _nav.stoppingDistance  = attackRange * 0.9f;
        _nav.angularSpeed      = 360f;
        _nav.acceleration      = 8f;

        // Animator — auto-find if not assigned in Inspector
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Build the code-driven animation controller.
        // Run style is randomised internally so each enemy looks different.
        _anim = new AgentAnimController(animator);
        attackInterval = GameplayTuning.Current.attackInterval;
        _attackTimer = Random.Range(0f, attackInterval * .4f);

        healthBar?.Initialise(transform, true);
        healthBar?.SetHealth(CurrentHp, MaxHp);

        GenerateAmbientPatrolPoints();
    }

    // ── Cinematic idle lock ───────────────────────────────────────────────
    /// <summary>
    /// When locked = true the enemy freezes in idle (NavMesh stopped, no combat).
    /// Call with false after the intro sequence finishes to release normal AI.
    /// </summary>
    public void SetCinematicIdle(bool locked)
    {
        _cinematicIdle = locked;
        if (locked)
            _nav.ResetPath();
    }

    /// <summary>
    /// Tell this enemy where the centre of its gang group is so passive members
    /// can face each other while standing still at spawn.
    /// </summary>
    public void SetGangCenter(Vector3 center)
    {
        _gangCenter = center;
    }

    private void setup(CharacterPortraitRegistry characterPortraitRegistry = null)
    {
        var registry = characterPortraitRegistry ?? BattleManager.instance.portraitRegistry;

        // Pick a random model prefab from the portrait registry.
        // Falls back to no visual model if the registry is not configured.
        if (registry == null || registry.entries == null || registry.entries.Count == 0)
        {
            Debug.LogWarning("[EnemyController] portraitRegistry is empty — no character model will be spawned.");
            return;
        }

        int index = Random.Range(0, registry.entries.Count);
        var entry = registry.entries[index];
        GameObject characterPrefab = entry.modelPrefab;
        if (characterPrefab == null) return;

        GameObject spawnedCharacter = Instantiate(characterPrefab, transform.position, transform.rotation, transform);
        GameplayTuning.ScaleModel(spawnedCharacter.transform);

        // Add the Animator component dynamically
        animator = spawnedCharacter.GetComponent<Animator>();
        if (!animator) animator=spawnedCharacter.AddComponent<Animator>();
        animator.runtimeAnimatorController = entry.animatorController != null ? entry.animatorController : BattleManager.instance.characterAnimator;

        // Set rendering layer to match the enemy's so they receive the same lighting.
        SkinnedMeshRenderer[] skinnedMeshRenderers = spawnedCharacter.GetComponentsInChildren<SkinnedMeshRenderer>().ToArray();
        var renderLayers = RenderingLayerMask.GetMask("Default", "Agent Light Layer");
        foreach (SkinnedMeshRenderer smr in skinnedMeshRenderers)
        {
            smr.renderingLayerMask = renderLayers;
        }

        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null) { capsule.radius *= 1.6f; capsule.height *= 2f; }
    }

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        if (!IsAlive) return;

        // During the pre-battle cinematic intro enemies stand still in idle.
        // Drive animation only — skip all combat and navigation logic.
        if (_cinematicIdle)
        {
            _anim?.Tick(0f);
            return;
        }

        if(defenceObjective && _nav && _nav.enabled && _nav.isOnNavMesh)
        {
            var nearby=BattleManager.instance?.GetNearestAgent(transform.position);
            if(!nearby || Vector3.Distance(nearby.transform.position,transform.position)>12)
            {
                ClearTarget();_nav.speed=_chaseSpeed;
                if(!_nav.pathPending && (!_nav.hasPath || Vector3.Distance(_nav.destination,defenceObjective.position)>1))
                    _nav.SetDestination(defenceObjective.position);
                _anim?.Tick(_nav.velocity.magnitude/Mathf.Max(.1f,_chaseSpeed));
                return;
            }
        }
        _attackTimer += Time.deltaTime;
        _scanTimer   += Time.deltaTime;

        // Periodic target scan
        if (_scanTimer >= SCAN_INTERVAL)
        {
            _scanTimer = 0;

            // Cops see gang fights
            if (firmName == "POLICE" && !isHostile)
            {
                if (BattleManager.instance != null && BattleManager.instance.IsGangFightActive())
                {
                    bool fightNearby = false;
                    foreach (var a in BattleManager.instance.PlayerAgents)
                    {
                        if (a != null && a.IsAlive && Vector3.Distance(transform.position, a.transform.position) <= detectionRadius)
                        {
                            fightNearby = true;
                            break;
                        }
                    }
                    if (!fightNearby)
                    {
                        foreach (var e in BattleManager.instance.EnemyAgents)
                        {
                            if (e != null && e != this && e.IsAlive && e.firmName != "POLICE" && e.isHostile &&
                                Vector3.Distance(transform.position, e.transform.position) <= detectionRadius)
                            {
                                fightNearby = true;
                                break;
                            }
                        }
                    }

                    if (fightNearby)
                    {
                        isHostile = true;
                        Debug.Log("[Police] Cop spotted a gang fight! Intervening.");
                    }
                }
            }
            
            if (isHostile)
            {
                if (firmName == "POLICE")
                {
                    // Cop targeting: find the nearest player or hostile rival gang member
                    MonoBehaviour nearestTarget = null;
                    float minDist = float.MaxValue;

                    foreach (var player in BattleManager.instance.PlayerAgents)
                    {
                        if (player == null || !player.IsAlive) continue;
                        float d = Vector3.Distance(transform.position, player.transform.position);
                        if (d < minDist) { minDist = d; nearestTarget = player; }
                    }

                    foreach (var enemy in BattleManager.instance.EnemyAgents)
                    {
                        if (enemy == null || enemy == this || !enemy.IsAlive || enemy.firmName == "POLICE") continue;
                        float d = Vector3.Distance(transform.position, enemy.transform.position);
                        if (d < minDist) { minDist = d; nearestTarget = enemy; }
                    }

                    if (nearestTarget != null)
                    {
                        float distToTarget = Vector3.Distance(transform.position, nearestTarget.transform.position);
                        if (distToTarget <= detectionRadius)
                        {
                            SetTarget(nearestTarget);
                        }
                        else if (_hasAggro && distToTarget > detectionRadius * 1.5f)
                        {
                            ClearTarget();
                            _nav.ResetPath();
                        }
                    }
                    else
                    {
                        ClearTarget();
                    }
                }
                else
                {
                    // Rival gang targeting: target the cop who hit them first, otherwise target the player
                    if (_targetEnemy != null && _targetEnemy.IsAlive && _targetEnemy.firmName == "POLICE")
                    {
                        float distToCop = Vector3.Distance(transform.position, _targetEnemy.transform.position);
                        if (distToCop > detectionRadius * 1.5f)
                        {
                            ClearTarget();
                        }
                    }
                    else
                    {
                        var nearestPlayer = BattleManager.instance?.GetNearestAgent(transform.position);
                        if (nearestPlayer != null && nearestPlayer.IsAlive)
                        {
                            float distToPlayer = Vector3.Distance(transform.position, nearestPlayer.transform.position);
                            if (distToPlayer <= detectionRadius)
                            {
                                SetTarget(nearestPlayer);
                            }
                            else if (_hasAggro && distToPlayer > detectionRadius * 1.5f)
                            {
                                ClearTarget();
                                _nav.ResetPath();
                            }
                        }
                        else
                        {
                            ClearTarget();
                        }
                    }
                }
            }
            else
            {
                ClearTarget();
            }
        }

        // ── Speed mode: walk when patrolling, sprint when hostile ────────
        if (_hasAggro)
        {
            if (_nav.speed != _chaseSpeed)
                _nav.speed = _chaseSpeed;
        }
        else
        {
            if (_nav.speed != patrolSpeed)
                _nav.speed = patrolSpeed;
        }

        // Drive animation — pass normalised 0-1 speed so anim controller
        // can distinguish walk (< 0.6) from run (>= 0.6) cleanly.
        float rawSpeed    = _nav.enabled ? _nav.velocity.magnitude : 0f;
        float normSpeed   = _chaseSpeed > 0f ? rawSpeed / _chaseSpeed : 0f;
        _anim?.Tick(normSpeed);

        if (TargetTransform == null || !IsTargetAlive)
        {
            if (!isHostile)
            {
                // Passive idle wander around the turf so gangs don't look frozen.
                if (_nav.enabled)
                {
                    if (!_nav.pathPending && (!_nav.hasPath || _nav.remainingDistance < 0.4f))
                    {
                        Vector3 center = _gangCenter != Vector3.zero ? _gangCenter : _spawnPosition;
                        Vector2 r = Random.insideUnitCircle * Mathf.Max(1.5f, patrolRadius * 0.55f);
                        Vector3 guess = center + new Vector3(r.x, 0f, r.y);
                        if (UnityEngine.AI.NavMesh.SamplePosition(guess, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                            _nav.SetDestination(hit.position);
                    }
                }
            }
            else
            {
                // Hostile but no target yet — patrol waypoints
                if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                {
                    if (_nav.enabled && !_nav.pathPending && (_nav.remainingDistance < 0.5f || !_nav.hasPath))
                    {
                        _currentWaypointIndex = (_currentWaypointIndex + 1) % patrolWaypoints.Length;
                        _nav.SetDestination(patrolWaypoints[_currentWaypointIndex]);
                    }
                }
                else if (_nav.enabled && !_nav.hasPath)
                {
                    _nav.SetDestination(_spawnPosition);
                }
            }
            return;
        }

        float dist = Vector3.Distance(transform.position, TargetTransform.position);

        if (dist > AttackRange)
        {
            // Chase target
            if (_nav.enabled)
            {
                _nav.SetDestination(TargetTransform.position);
            }
        }
        else
        {
            // In range — stop, face, attack
            if (_nav.enabled)
            {
                _nav.ResetPath();
            }
            FaceTarget(TargetTransform.position);
            TryAttack();
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

        _pendingPlayer = _targetPlayer; _pendingEnemy = _targetEnemy;
        _impactPending = true;
        StartCoroutine(ImpactAfterDelay());
    }
    AgentController _pendingPlayer;
    EnemyController _pendingEnemy;
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
        if (!IsAlive || _cinematicIdle) return;
        var target = _pendingPlayer ? _pendingPlayer.transform : _pendingEnemy ? _pendingEnemy.transform : null;
        if (!target || Vector3.Distance(transform.position, target.position) > AttackRange + .35f) return;
        float damage = Mathf.Max(1, Strength + Random.Range(-1f, 1f));
        if (_pendingPlayer && _pendingPlayer.IsAlive) _pendingPlayer.TakeDamage(damage);
        else if (_pendingEnemy && _pendingEnemy.IsAlive) _pendingEnemy.TakeDamage(damage, this);
        GameAudio.Play("impact");
    }

    // ── Damage ────────────────────────────────────────────────────────────
    public void TakeDamage(float amount, MonoBehaviour attacker = null)
    {
        if (!IsAlive) return;
        CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(0, amount));
        healthBar?.SetHealth(CurrentHp, MaxHp);

        if (CurrentHp <= 0) { Die(); return; }

        _anim?.PlayHit();

        // Target the attacker!
        if (attacker != null)
        {
            SetTarget(attacker);

            if (!isHostile)
            {
                isHostile = true;
                AlertAlliesOfSameFaction(attacker);
            }
            else
            {
                AlertAlliesNearby(attacker);
            }
        }
    }

    public void AlertAlliesOfSameFaction(MonoBehaviour attacker)
    {
        if (BattleManager.instance == null) return;
        foreach (var enemy in BattleManager.instance.EnemyAgents)
        {
            if (enemy != null && enemy.IsAlive && enemy.firmName == this.firmName)
            {
                enemy.isHostile = true;
                enemy.AlertToTarget(attacker);
            }
        }
    }

    public void AlertToTarget(MonoBehaviour attacker)
    {
        if (!IsAlive || _hasAggro) return;
        SetTarget(attacker);
    }

    private void AlertAlliesNearby(MonoBehaviour attacker)
    {
        if (BattleManager.instance == null) return;
        foreach (var enemy in BattleManager.instance.EnemyAgents)
        {
            if (enemy != null && enemy != this && enemy.IsAlive)
            {
                if (Vector3.Distance(transform.position, enemy.transform.position) < 20f)
                {
                    enemy.AlertToTarget(attacker);
                }
            }
        }
    }

    public void GenerateAmbientPatrolPoints()
    {
        _spawnPosition  = transform.position;
        patrolWaypoints = new Vector3[4];
        patrolWaypoints[0] = _spawnPosition;

        float navSampleRadius = patrolRadius + 2f;

        for (int i = 1; i < 4; i++)
        {
            // Use cardinal-ish offsets so the patrol circuit is predictable
            // rather than fully random (avoids zig-zagging across the street).
            float angle = (i - 1) * 120f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * patrolRadius,
                0f,
                Mathf.Sin(angle) * patrolRadius
            );
            Vector3 candidate = _spawnPosition + offset;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navSampleRadius, NavMesh.AllAreas))
                patrolWaypoints[i] = navHit.position;
            else
                patrolWaypoints[i] = _spawnPosition;
        }
        _currentWaypointIndex = 0;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? _spawnPosition : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, patrolRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    private void Die()
    {
        _nav.ResetPath();
        _nav.enabled = false;

        _anim?.PlayDie();
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(1.8f);
        gameObject.SetActive(false);
        BattleManager.instance?.OnEnemyDied(this);
    }


    // ── Utility ───────────────────────────────────────────────────────────
    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), GameplayTuning.Current.turnSpeed * Time.deltaTime);
    }

    // ── Building Interactions ──────────────────────────────────────────────

    /// <summary>
    /// Restores <paramref name="amount"/> HP, clamped to MaxHp.
    /// Called by <see cref="MapBuilding"/> when enemyAutoUse is enabled
    /// (e.g. an Infirmary automatically heals nearby enemy units).
    /// </summary>
    public void HealAmount(float amount)
    {
        if (!IsAlive) return;
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        // Refresh the world-space health bar if one is attached.
        if (healthBar != null)
            healthBar.SetHealth(CurrentHp, MaxHp);
        Debug.Log($"[EnemyController] {gameObject.name} auto-healed +{amount:F1} HP (now {CurrentHp:F1}/{MaxHp:F1})");
    }

    private void Start()
    {
        var kind = firmName == "POLICE" ? MiniMapIconFactory.Kind.Police : MiniMapIconFactory.Kind.Gang;
        MiniMapIconFactory.Register(transform, kind, firmName);
        UnitAffiliationMarker.Attach(transform,
            firmName == "POLICE" ? new Color(.20f, .55f, 1f, 1f) : new Color(1f, .10f, .08f, 1f), 1.02f, false);
    }

    private void OnDestroy()
    {
        if (Arikan.MiniMapView.Instance != null)
        {
            Arikan.MiniMapView.Instance.UnfollowTarget(transform);
        }
    }
}

