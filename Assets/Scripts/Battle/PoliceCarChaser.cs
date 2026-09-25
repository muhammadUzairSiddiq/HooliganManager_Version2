using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Police van that sticks to <see cref="PoliceRoadSpline"/> during patrol
/// (correct facing + smooth turns), or parks / idles after a response.
/// </summary>
public class PoliceCarChaser : MonoBehaviour
{
    [Header("Motion")]
    public float patrolSpeed = 3.5f;
    public float chaseSpeed = 5f;
    [Tooltip("If the van nose points the wrong way on the road, try 0, 90, -90, or 180.")]
    public float yawOffsetDegrees = -90f;
    public float groundProbeHeight = 4f;

    private Transform _target;
    private bool _chasing;
    private bool _patrolling;
    private bool _parked;
    private float _splineDist;
    private float _startOffset;
    private Light _redLight;
    private Light _blueLight;
    private float _blinkTimer;
    private PoliceRoadSpline _spline;
    private Vector3 _lastPos;
    private bool _hasLastPos;
    private NavMeshPath _responsePath;
    private int _responseCorner;
    private float _nextRepath;

    public bool IsPatrolCar => _patrolling;
    public bool IsActiveCar => _patrolling || _chasing || _parked;

    public void Init()
    {
        if (_redLight != null) return;
        _redLight = CreateLight(new Color(1f, 0.12f, 0.12f), new Vector3(-0.35f, 1.7f, 0.2f));
        _blueLight = CreateLight(new Color(0.15f, 0.4f, 1f), new Vector3(0.35f, 1.7f, 0.2f));
    }

    private Light CreateLight(Color color, Vector3 localPos)
    {
        var go = new GameObject("SirenLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = 16f;
        l.intensity = 4f;
        return l;
    }

    public void StartSplinePatrol(PoliceRoadSpline spline, float startDistance = 0f)
    {
        Init();
        _spline = spline;
        _startOffset = startDistance;
        _splineDist = startDistance;
        _patrolling = spline != null && spline.IsReady;
        _chasing = false;
        _parked = false;
        _target = null;

        if (_patrolling)
            SnapToSpline(_splineDist);
    }

    /// <summary>Legacy entry — converts loose points into a temporary open path via spline nearest.</summary>
    public void StartPatrol(Vector3[] waypoints)
    {
        var spline = PoliceRoadSpline.EnsureExists();
        float start = 0f;
        if (waypoints != null && waypoints.Length > 0)
            start = spline.FindNearestDistance(waypoints[0]);
        StartSplinePatrol(spline, start);
    }

    public void SetTarget(Transform target)
    {
        _target = target;
        _chasing = target != null;
        _patrolling = false;
        _parked = false;
    }

    public void StopAndIdle()
    {
        _chasing = false;
        _patrolling = false;
        _parked = true;
        _target = null;
    }

    public void ParkAt(Vector3 worldPos, Vector3 faceDir)
    {
        StopAndIdle();
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude < 0.001f) faceDir = transform.forward;
        Vector3 pos = worldPos;
        if (Physics.Raycast(worldPos + Vector3.up * groundProbeHeight, Vector3.down, out RaycastHit hit, 20f))
            pos = hit.point;
        transform.position = pos;
        ApplyFacing(faceDir.normalized);
    }

    void Update()
    {
        _blinkTimer += Time.unscaledDeltaTime;
        bool phase = Mathf.FloorToInt(_blinkTimer * 6f) % 2 == 0;
        if (_redLight != null) _redLight.enabled = phase;
        if (_blueLight != null) _blueLight.enabled = !phase;

        if (_parked) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (_chasing && _target != null)
        {
            if(gameObject.scene.name=="Gameplay") { DriveCityResponse(dt);return; }
            // Still prefer road when close; otherwise drive straight at target.
            Vector3 to = _target.position - transform.position;
            to.y = 0f;
            if (to.magnitude <= 5f)
            {
                StopAndIdle();
                return;
            }
            DriveFree(to.normalized, chaseSpeed * dt);
            return;
        }

        if (_patrolling)
        {
            if (_spline == null || !_spline.IsReady)
            {
                _spline = PoliceRoadSpline.Instance ?? PoliceRoadSpline.EnsureExists();
                if (_spline == null || !_spline.IsReady) return;
            }

            _splineDist += patrolSpeed * dt;
            if (_spline.TotalLength > 0.1f)
                _splineDist = Mathf.Repeat(_splineDist, _spline.TotalLength);

            SnapToSpline(_splineDist);
        }
    }

    private void SnapToSpline(float dist)
    {
        Vector3 pos = _spline.GetPointAtDistance(dist);
        Vector3 tan = _spline.GetTangentAtDistance(dist);
        if (Physics.Raycast(pos + Vector3.up * groundProbeHeight, Vector3.down, out RaycastHit hit, 20f))
            pos.y = hit.point.y;

        // Prefer real travel direction so the nose never points reverse of motion.
        Vector3 face = tan;
        if (_hasLastPos)
        {
            Vector3 delta = pos - _lastPos;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.00005f)
                face = delta.normalized;
        }
        _lastPos = pos;
        _hasLastPos = true;

        transform.position = pos;
        ApplyFacing(face);
    }

    private void DriveFree(Vector3 dir, float step)
    {
        Vector3 next = transform.position + dir * step;
        if (Physics.Raycast(next + Vector3.up * groundProbeHeight, Vector3.down, out RaycastHit hit, 20f))
            next.y = hit.point.y;
        transform.position = next;
        ApplyFacing(dir);
    }

    private void DriveCityResponse(float dt)
    {
        if(Time.time>=_nextRepath)
        {
            _nextRepath=Time.time+1.5f;
            _responsePath=new NavMeshPath();_responseCorner=1;
            if(!NavMesh.SamplePosition(transform.position,out var start,15,1<<3) ||
               !NavMesh.SamplePosition(_target.position,out var end,60,1<<3) ||
               !NavMesh.CalculatePath(start.position,end.position,1<<3,_responsePath) ||
               _responsePath.status!=NavMeshPathStatus.PathComplete)
            { _responsePath=null;return; }
        }
        if(_responsePath==null || _responseCorner>=_responsePath.corners.Length)return;
        Vector3 point=_responsePath.corners[_responseCorner];
        Vector3 direction=point-transform.position;direction.y=0;
        if(direction.magnitude<.7f){_responseCorner++;return;}
        Vector3 next=Vector3.MoveTowards(transform.position,point,chaseSpeed*dt);
        if(NavMesh.Raycast(transform.position,next,out var obstruction,1<<3))return;
        transform.position=next;ApplyFacing(direction);
        if(Vector3.Distance(transform.position,_responsePath.corners[_responsePath.corners.Length-1])<2)StopAndIdle();
    }

    public System.Collections.IEnumerator DriveArrival(Vector3 destination)
    {
        StopAndIdle();
        var path=new NavMeshPath();
        if(!NavMesh.SamplePosition(transform.position,out var start,12,1<<3) ||
            !NavMesh.CalculatePath(start.position,destination,1<<3,path) || path.status!=NavMeshPathStatus.PathComplete)
            yield break;
        for(int i=1;i<path.corners.Length;i++)
        {
            while(Vector3.Distance(transform.position,path.corners[i])>.15f)
            {
                Vector3 next=Vector3.MoveTowards(transform.position,path.corners[i],12*Time.unscaledDeltaTime);
                ApplyFacing(next-transform.position);transform.position=next;
                yield return null;
            }
        }
    }

    private void ApplyFacing(Vector3 forwardXZ)
    {
        if (forwardXZ.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(forwardXZ.normalized, Vector3.up);
        // Model-space correction so the van nose points along the road.
        transform.rotation = look * Quaternion.Euler(0f, yawOffsetDegrees, 0f);
    }
}
