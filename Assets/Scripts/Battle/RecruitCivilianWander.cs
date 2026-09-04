using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Civilian at a recruitment pad — walks between points with proper Idle/Walk clips.
/// </summary>
public class RecruitCivilianWander : MonoBehaviour
{
    private Vector3 _center;
    private float _radius;
    private NavMeshAgent _agent;
    private AgentAnimController _anim;
    private float _wait;
    private bool _paused;
    private float _baseSpeed = 1.2f;

    public void Init(Vector3 center, float radius, Animator animator, NavMeshAgent agent)
    {
        _center = center;
        _radius = Mathf.Max(1.5f, radius);
        _agent = agent;
        if (_agent != null) _baseSpeed = Mathf.Max(0.8f, _agent.speed);

        if (animator != null)
        {
            animator.applyRootMotion = false;
            _anim = new AgentAnimController(animator);
        }

        _wait = Random.Range(0.2f, 1.0f);
        PickNewDestination();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (_agent == null) return;
        if (paused)
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.isStopped = true;
            _anim?.Tick(0f);
        }
        else
        {
            _agent.isStopped = false;
            _wait = 0.2f;
            PickNewDestination();
        }
    }

    void Update()
    {
        if (_paused || _agent == null || !_agent.enabled) return;
        if (!_agent.isOnNavMesh)
        {
            _anim?.Tick(0f);
            return;
        }

        float speed = _agent.velocity.magnitude;
        // AgentAnimController expects roughly 0–1 normalised speed.
        float norm = _baseSpeed > 0.01f ? Mathf.Clamp01(speed / _baseSpeed) : 0f;
        // Bias toward walk band so civilians never sprint.
        if (norm > 0.05f && norm < 0.55f)
            norm = Mathf.Lerp(0.15f, 0.5f, norm);
        else if (norm >= 0.55f)
            norm = 0.45f; // force walk clip, never sprint for civilians
        _anim?.Tick(norm);

        if (_agent.pathPending) return;

        bool arrived = !_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 0.2f;
        if (!arrived) return;

        _wait -= Time.deltaTime;
        if (_wait > 0f)
        {
            _anim?.Tick(0f); // idle while waiting
            return;
        }

        PickNewDestination();
        _wait = Random.Range(1.5f, 3.5f);
    }

    private void PickNewDestination()
    {
        if (_agent == null || !_agent.enabled) return;
        for (int i = 0; i < 10; i++)
        {
            Vector2 r = Random.insideUnitCircle * _radius;
            Vector3 guess = _center + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(guess, out var hit, 3.5f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
                return;
            }
        }
    }
}
