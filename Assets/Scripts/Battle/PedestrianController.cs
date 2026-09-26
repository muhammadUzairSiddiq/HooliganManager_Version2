using Pastoral;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PedestrianController : MonoBehaviour
{
    public float walkSpeed = 1.0f;
    public float minWaitTime = 2.0f;
    public float maxWaitTime = 7.0f;
    
    private NavMeshAgent _navAgent;
    private Animator _animator;
    private MeshRenderer[] _pavementMeshes;
    private bool _hasActivityZone;
    private Vector3 _activityCenter;
    private float _activityRadius;
    
    private float _waitTimer = 0f;
    private bool _isWaiting = true;
    private bool _conversationPaused;

    // Optional: Reference to AgentAnimController if they use the same animation setup
    private AgentAnimController _animController;

    private void Awake()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _navAgent.speed = walkSpeed;
    }

    public void Initialize(MeshRenderer[] pavementMeshes, CharacterPortraitRegistry registry = null)
    {
        _pavementMeshes = pavementMeshes;
        _isWaiting = false; // Start walking immediately instead of idling
        _waitTimer = 0f;

        // --- Spawn the visual mesh ---
        if (registry != null && registry.entries != null && registry.entries.Count > 0)
        {
            int index = CityCharacterBudget.Pick(registry);
            var entry = registry.entries[index];
            GameObject characterPrefab = entry.optimizedModelPrefab ? entry.optimizedModelPrefab : entry.modelPrefab;
            if (characterPrefab != null)
            {
                GameObject spawnedCharacter = Instantiate(characterPrefab, transform.position, transform.rotation, transform);
                CityCharacterBudget.Apply(spawnedCharacter);
                
                GameplayTuning.ScaleModel(spawnedCharacter.transform);
                _animator = spawnedCharacter.GetComponent<Animator>();
                if (!_animator) _animator = spawnedCharacter.AddComponent<Animator>();
                _animator.runtimeAnimatorController = entry.animatorController != null ? entry.animatorController : BattleManager.instance.characterAnimator;
                StartCoroutine(General.InvokeMethod(()=>_animator.Play("Walking"), 1));
                
                // Add the anim controller wrapper
                _animController = new AgentAnimController(_animator);

                // Setup rendering layers
                SkinnedMeshRenderer[] smrs = spawnedCharacter.GetComponentsInChildren<SkinnedMeshRenderer>();
                var renderLayers = RenderingLayerMask.GetMask("Default", "Agent Light Layer");
                foreach (SkinnedMeshRenderer smr in smrs)
                {
                    smr.renderingLayerMask = renderLayers;
                }
            }
        }
        else
        {
            Debug.LogWarning("[PedestrianController] No CharacterPortraitRegistry provided or it is empty.");
        }

        PickRandomDestination();
    }

    /// <summary>Keeps special-event pedestrians close to an important location.</summary>
    public void SetActivityZone(Vector3 center, float radius)
    {
        _hasActivityZone = true;
        _activityCenter = center;
        _activityRadius = Mathf.Max(4f, radius);
        _isWaiting = false;
        PickRandomDestination();
    }

    private void Update()
    {
        // Animate
        float speed = _navAgent.velocity.magnitude;
        if (_animController != null)
        {
            // If we are not waiting, ALWAYS play the walking animation, regardless of NavMesh velocity.
            // This ensures they start walking immediately on spawn without waiting for acceleration.
            float normSpeed = Mathf.Clamp01(speed / Mathf.Max(.1f, walkSpeed)) * .5f;
            // We pass false for isFighting so it uses Idle instead of BattleIdle when standing still
            _animController.Tick(normSpeed, _streetFight || _conversationPaused);
        }
        else if (_animator != null)
        {
            // Fallback if they use a simple animator with a Speed parameter
            _animator.SetFloat("Speed", speed);
        }

        if (_conversationPaused) return;

        if (_isWaiting)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                _isWaiting = false;
                PickRandomDestination();
            }
        }
        else
        {
            // Check if reached destination
            if (!_navAgent.pathPending && _navAgent.remainingDistance <= _navAgent.stoppingDistance)
            {
                if (!_navAgent.hasPath || _navAgent.velocity.sqrMagnitude == 0f)
                {
                    _isWaiting = true;
                    _waitTimer = Random.Range(minWaitTime, maxWaitTime);
                }
            }
        }
    }

    private bool _streetFight;

    public void SetConversationPaused(bool paused, Vector3 speakerPosition)
    {
        _conversationPaused = paused;
        if (!paused) _streetFight = false;
        if (_navAgent != null && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = paused;
            if (paused) _navAgent.ResetPath();
            else PickRandomDestination();
        }
        if (paused) Face(speakerPosition);
    }

    public void BeginStreetFight()
    {
        _streetFight = true;
        _conversationPaused = true;
        if (_navAgent != null && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = true;
            _navAgent.ResetPath();
        }
    }

    public void EndStreetFight()
    {
        _streetFight = false;
        SetConversationPaused(false, transform.position);
    }

    public void Face(Vector3 worldPoint)
    {
        Vector3 look = worldPoint - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > .05f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
    }

    public void PlayAttack() => _animController?.PlayAttack();
    public void PlayDie()
    {
        _streetFight = false;
        _conversationPaused = true;
        if (_navAgent != null && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = true;
            _navAgent.ResetPath();
        }
        _animController?.PlayDie();
    }

    public bool IsConversationPaused => _conversationPaused;

    private void PickRandomDestination()
    {
        if (_hasActivityZone)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 offset = Random.insideUnitCircle * _activityRadius;
                Vector3 candidate = _activityCenter + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit localHit, 5f, NavMesh.AllAreas))
                {
                    _navAgent.SetDestination(localHit.position);
                    return;
                }
            }
        }

        if (_pavementMeshes == null || _pavementMeshes.Length == 0) return;

        // Try a few times to find a valid NavMesh point on a random pavement mesh
        for (int i = 0; i < 5; i++)
        {
            MeshRenderer randomMesh = _pavementMeshes[Random.Range(0, _pavementMeshes.Length)];
            Bounds bounds = randomMesh.bounds;

            Vector3 randomPos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                _navAgent.SetDestination(hit.position);
                return;
            }
        }
    }
}
