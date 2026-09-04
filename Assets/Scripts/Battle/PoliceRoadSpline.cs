using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds a closed police patrol route that stays on road surfaces.
/// Uses road mesh samples + NavMesh paths between hops (never straight chords
/// through buildings), then densifies along those paths.
/// </summary>
public class PoliceRoadSpline : MonoBehaviour
{
    public static PoliceRoadSpline Instance { get; private set; }

    [Header("Visual route")]
    public Color lineColorA = new Color(0.15f, 0.45f, 1f, 0.85f);
    public Color lineColorB = new Color(1f, 0.15f, 0.2f, 0.85f);
    public float lineWidth = 0.55f;
    public bool showRouteLine = true;

    private Vector3[] _points = System.Array.Empty<Vector3>();
    private float[] _cumLen = System.Array.Empty<float>();
    private float _totalLen;
    private LineRenderer _line;
    private static readonly NavMeshPath _sharedPath = new NavMeshPath();

    public float TotalLength => _totalLen;
    public bool IsReady => _points != null && _points.Length >= 4 && _totalLen > 1f;

    public static PoliceRoadSpline EnsureExists()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("PoliceRoadSpline");
        Instance = go.AddComponent<PoliceRoadSpline>();
        Instance.Build();
        return Instance;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Build()
    {
        var raw = SampleRoadPoints();
        if (raw.Count < 6)
            raw = SampleNavMeshRing(32);

        raw = BuildRoadLoop(raw);
        if (raw.Count < 4)
        {
            Debug.LogWarning("[PoliceRoadSpline] Not enough road points after loop build.");
            return;
        }

        // Follow NavMesh between control points so we never chord through buildings.
        raw = DensifyAlongNavMesh(raw);
        raw = SnapAllToRoad(raw);
        raw = RemoveOffRoadAndCollinear(raw);

        if (raw.Count < 4)
        {
            Debug.LogWarning("[PoliceRoadSpline] Route collapsed after road filtering.");
            return;
        }

        // Even spacing along the polyline (no Catmull corner-cutting through blocks).
        _points = ResamplePolyline(raw, spacing: 1.25f);
        if (_points.Length < 4)
        {
            Debug.LogWarning("[PoliceRoadSpline] Resample produced too few points.");
            return;
        }

        BuildCumulative();
        BuildLine();
        Debug.Log($"[PoliceRoadSpline] Route ready: {_points.Length} pts, {_totalLen:0.0}m");
    }

    public Vector3 GetPointAtDistance(float dist)
    {
        if (!IsReady) return Vector3.zero;
        dist = Mathf.Repeat(dist, _totalLen);
        int i = FindSegment(dist);
        int j = (i + 1) % _points.Length;
        float segStart = _cumLen[i];
        float segLen = (i + 1 < _cumLen.Length ? _cumLen[i + 1] : _totalLen) - segStart;
        float u = segLen > 0.0001f ? (dist - segStart) / segLen : 0f;
        return Vector3.Lerp(_points[i], _points[j], u);
    }

    public Vector3 GetTangentAtDistance(float dist)
    {
        if (!IsReady) return Vector3.forward;
        Vector3 a = GetPointAtDistance(dist);
        Vector3 b = GetPointAtDistance(dist + 0.75f);
        Vector3 t = b - a;
        t.y = 0f;
        return t.sqrMagnitude > 0.0001f ? t.normalized : Vector3.forward;
    }

    public float FindNearestDistance(Vector3 world)
    {
        if (!IsReady) return 0f;
        float best = 0f;
        float bestSqr = float.MaxValue;
        int steps = Mathf.Clamp(Mathf.RoundToInt(_totalLen / 2f), 16, 256);
        for (int i = 0; i < steps; i++)
        {
            float d = (i / (float)steps) * _totalLen;
            float sqr = (GetPointAtDistance(d) - world).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = d; }
        }
        return best;
    }

    // ── Sampling ─────────────────────────────────────────────────────────
    private List<Vector3> SampleRoadPoints()
    {
        var pts = new List<Vector3>();
        Transform roadsRoot = FindRoadsRoot();
        if (roadsRoot == null) return pts;

        var filters = roadsRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            string n = mf.gameObject.name.ToLowerInvariant();
            if (n.Contains("build") || n.Contains("tree") || n.Contains("car") ||
                n.Contains("prop") || n.Contains("sign") || n.Contains("light"))
                continue;

            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            int stride = Mathf.Max(1, verts.Length / 140);
            var xf = mf.transform;
            for (int i = 0; i < verts.Length; i += stride)
            {
                Vector3 w = xf.TransformPoint(verts[i]);
                if (TrySnapToRoad(w, roadsRoot, out Vector3 onRoad))
                    pts.Add(onRoad);
            }
        }

        // Dense grid over road bounds — catches long straight asphalt strips.
        var rends = roadsRoot.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float step = 3.5f;
            for (float x = b.min.x; x <= b.max.x; x += step)
            for (float z = b.min.z; z <= b.max.z; z += step)
            {
                Vector3 p = new Vector3(x, b.max.y + 8f, z);
                if (!Physics.Raycast(p, Vector3.down, out RaycastHit rh, 80f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (!IsRoadHit(rh.transform, roadsRoot)) continue;
                if (TrySnapToRoad(rh.point, roadsRoot, out Vector3 onRoad))
                    pts.Add(onRoad);
            }
        }

        return Dedup(pts, 3.0f);
    }

    private static Transform FindRoadsRoot()
    {
        var roads = GameObject.Find("Roads");
        if (roads != null) return roads.transform;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t != null && t.name.Equals("Roads", System.StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    private static bool TrySnapToRoad(Vector3 world, Transform roadsRoot, out Vector3 onRoad)
    {
        onRoad = world;
        // Tight NavMesh snap — large radii pull samples into sidewalks / yards.
        if (!NavMesh.SamplePosition(world, out var hit, 1.0f, NavMesh.AllAreas))
            return false;

        if (!Physics.Raycast(hit.position + Vector3.up * 4f, Vector3.down, out RaycastHit rh, 12f,
                ~0, QueryTriggerInteraction.Ignore))
            return false;

        if (!IsRoadHit(rh.transform, roadsRoot))
            return false;

        // Reject if a building sits immediately above the sample (drive-through gap).
        if (Physics.Raycast(hit.position + Vector3.up * 0.3f, Vector3.up, out RaycastHit upHit, 6f,
                ~0, QueryTriggerInteraction.Ignore))
        {
            if (IsBuildingName(upHit.transform.name))
                return false;
        }

        onRoad = hit.position;
        return true;
    }

    private static bool IsRoadHit(Transform t, Transform roadsRoot)
    {
        while (t != null)
        {
            if (roadsRoot != null && t == roadsRoot) return true;
            string n = t.name.ToLowerInvariant();
            if (IsBuildingName(n)) return false;
            if (n.Contains("road") || n.Contains("asphalt") || n.Contains("street") ||
                n.Contains("highway") || n.Contains("pavement") || n == "roads")
                return true;
            t = t.parent;
        }
        return false;
    }

    private static bool IsBuildingName(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        n = n.ToLowerInvariant();
        return n.Contains("build") || n.Contains("wall") || n.Contains("roof") ||
               n.Contains("house") || n.Contains("shop") || n.Contains("store") ||
               n.Contains("apartment") || n.Contains("collider") || n.Contains("fence") ||
               n.Contains("prop_") || n.Contains("trash") || n.Contains("dumpster");
    }

    private List<Vector3> SampleNavMeshRing(int count)
    {
        var pts = new List<Vector3>();
        Transform roadsRoot = FindRoadsRoot();
        Vector3 center = BattleManager.instance != null
            ? BattleManager.instance.GetPlayerCentroid()
            : Vector3.zero;
        for (int ring = 0; ring < 3; ring++)
        {
            float radius = 25f + ring * 18f;
            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f;
                Vector3 guess = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
                if (TrySnapToRoad(guess, roadsRoot, out Vector3 onRoad))
                    pts.Add(onRoad);
                else if (NavMesh.SamplePosition(guess, out var hit, 12f, NavMesh.AllAreas))
                    pts.Add(hit.position);
            }
        }
        return Dedup(pts, 4f);
    }

    private static List<Vector3> Dedup(List<Vector3> src, float minDist)
    {
        var outPts = new List<Vector3>();
        float minSqr = minDist * minDist;
        foreach (var p in src)
        {
            bool ok = true;
            for (int i = 0; i < outPts.Count; i++)
                if ((outPts[i] - p).sqrMagnitude < minSqr) { ok = false; break; }
            if (ok) outPts.Add(p);
        }
        return outPts;
    }

    /// <summary>
    /// Nearest-neighbour tour that ONLY accepts road-valid NavMesh hops.
    /// Never falls back to a chord that cuts through buildings.
    /// </summary>
    private static List<Vector3> BuildRoadLoop(List<Vector3> pts)
    {
        if (pts.Count < 2) return pts;

        var ordered = new List<Vector3>(pts.Count);
        var used = new bool[pts.Count];
        int cur = 0;
        for (int i = 1; i < pts.Count; i++)
            if (pts[i].x < pts[cur].x) cur = i;

        // Grow max hop gradually so we stay on roads before taking longer arcs.
        float[] hopCaps = { 10f, 14f, 18f, 24f, 32f };

        for (int n = 0; n < pts.Count; n++)
        {
            ordered.Add(pts[cur]);
            used[cur] = true;

            int next = -1;
            float best = float.MaxValue;

            foreach (float maxJump in hopCaps)
            {
                float maxJumpSqr = maxJump * maxJump;
                best = float.MaxValue;
                next = -1;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (used[i]) continue;
                    float dSqr = (pts[i] - pts[cur]).sqrMagnitude;
                    if (dSqr > maxJumpSqr) continue;
                    if (!IsValidRoadHop(pts[cur], pts[i], maxJump * 1.35f)) continue;
                    if (dSqr < best) { best = dSqr; next = i; }
                }
                if (next >= 0) break;
            }

            // Last resort: any unused point reachable by a complete NavMesh path on road,
            // preferring the shortest *path length* (not Euclidean chord).
            if (next < 0)
            {
                best = float.MaxValue;
                next = -1;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (used[i]) continue;
                    if (!TryNavMeshPathLength(pts[cur], pts[i], out float pathLen)) continue;
                    if (pathLen > 80f) continue;
                    if (!PathStaysNearRoad(pts[cur], pts[i])) continue;
                    if (pathLen < best) { best = pathLen; next = i; }
                }
            }

            if (next < 0) break; // leave unreachable islands out of the loop
            cur = next;
        }

        // Close the loop only if the return hop is road-valid.
        if (ordered.Count >= 4)
        {
            Vector3 first = ordered[0];
            Vector3 last = ordered[ordered.Count - 1];
            if (!IsValidRoadHop(last, first, 40f) && !PathStaysNearRoad(last, first))
            {
                // Drop trailing points until we can close, or leave open-ish loop densified later.
                for (int trim = ordered.Count - 1; trim >= 4; trim--)
                {
                    if (IsValidRoadHop(ordered[trim], first, 40f) || PathStaysNearRoad(ordered[trim], first))
                    {
                        if (trim < ordered.Count - 1)
                            ordered.RemoveRange(trim + 1, ordered.Count - trim - 1);
                        break;
                    }
                }
            }
        }

        return ordered;
    }

    private static bool IsValidRoadHop(Vector3 a, Vector3 b, float maxPathLen)
    {
        float euclid = Vector3.Distance(a, b);
        if (euclid < 0.5f) return true;
        if (!TryNavMeshPathLength(a, b, out float pathLen)) return false;
        if (pathLen > maxPathLen) return false;
        // Chord much shorter than NavMesh path ⇒ straight line would cut a block.
        if (euclid > 4f && pathLen > euclid * 1.55f) return false;
        return SegmentClearsBuildings(a, b) && PathStaysNearRoad(a, b);
    }

    private static bool TryNavMeshPathLength(Vector3 a, Vector3 b, out float length)
    {
        length = 0f;
        if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, _sharedPath))
            return false;
        if (_sharedPath.status != NavMeshPathStatus.PathComplete)
            return false;
        var corners = _sharedPath.corners;
        if (corners == null || corners.Length < 2) return false;
        for (int i = 1; i < corners.Length; i++)
            length += Vector3.Distance(corners[i - 1], corners[i]);
        return length > 0.01f;
    }

    /// <summary>True when every NavMesh corner / mid-sample sits on (or next to) road asphalt.</summary>
    private static bool PathStaysNearRoad(Vector3 a, Vector3 b)
    {
        if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, _sharedPath))
            return false;
        if (_sharedPath.status != NavMeshPathStatus.PathComplete)
            return false;

        Transform roadsRoot = FindRoadsRoot();
        var corners = _sharedPath.corners;
        for (int i = 0; i < corners.Length; i++)
        {
            if (!PointOnOrNearRoad(corners[i], roadsRoot, 2.0f))
                return false;
        }

        // Also probe between corners — NavMesh can still skim yard gaps.
        for (int i = 1; i < corners.Length; i++)
        {
            float dist = Vector3.Distance(corners[i - 1], corners[i]);
            int samples = Mathf.Clamp(Mathf.CeilToInt(dist / 2.0f), 1, 10);
            for (int s = 1; s <= samples; s++)
            {
                Vector3 p = Vector3.Lerp(corners[i - 1], corners[i], s / (float)(samples + 1));
                if (!PointOnOrNearRoad(p, roadsRoot, 2.25f))
                    return false;
                if (!SegmentClearsBuildings(corners[i - 1], corners[i]))
                    return false;
            }
        }
        return true;
    }

    private static bool PointOnOrNearRoad(Vector3 p, Transform roadsRoot, float radius)
    {
        if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit rh, 20f,
                ~0, QueryTriggerInteraction.Ignore))
        {
            if (IsRoadHit(rh.transform, roadsRoot)) return true;
            if (IsBuildingName(rh.transform.name)) return false;
        }

        // Soft fallback: NavMesh sample must still raycast onto something road-like nearby.
        if (NavMesh.SamplePosition(p, out var hit, radius, NavMesh.AllAreas))
        {
            if (Physics.Raycast(hit.position + Vector3.up * 4f, Vector3.down, out RaycastHit rh2, 12f,
                    ~0, QueryTriggerInteraction.Ignore))
                return IsRoadHit(rh2.transform, roadsRoot);
        }
        return roadsRoot == null; // if no Roads object, don't block entirely
    }

    private static bool SegmentClearsBuildings(Vector3 a, Vector3 b)
    {
        Vector3 flat = b - a;
        flat.y = 0f;
        float dist = flat.magnitude;
        if (dist < 0.15f) return true;
        Vector3 dir = flat / dist;

        // Bumper-height ray along the chord — buildings between roads block this.
        if (Physics.SphereCast(a + Vector3.up * 1.15f, 0.35f, dir, out RaycastHit hit, dist,
                ~0, QueryTriggerInteraction.Ignore))
        {
            if (IsBuildingName(hit.transform.name))
                return false;
        }

        int samples = Mathf.Clamp(Mathf.CeilToInt(dist / 1.5f), 2, 16);
        for (int i = 1; i < samples; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / (float)samples);
            if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out RaycastHit down, 12f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsBuildingName(down.transform.name))
                    return false;
            }
        }
        return true;
    }

    /// <summary>Replace each hop with the NavMesh corner chain so the route hugs roads.</summary>
    private static List<Vector3> DensifyAlongNavMesh(List<Vector3> loop)
    {
        if (loop.Count < 2) return loop;
        var dense = new List<Vector3>(loop.Count * 8);
        Transform roadsRoot = FindRoadsRoot();

        for (int i = 0; i < loop.Count; i++)
        {
            Vector3 a = loop[i];
            Vector3 b = loop[(i + 1) % loop.Count];

            if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, _sharedPath) ||
                _sharedPath.status != NavMeshPathStatus.PathComplete ||
                _sharedPath.corners == null || _sharedPath.corners.Length < 2)
            {
                // Skip illegal closing chord rather than cutting through a block.
                if (i == loop.Count - 1) break;
                dense.Add(a);
                continue;
            }

            var corners = _sharedPath.corners;
            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 p = corners[c];
                if (roadsRoot != null && TrySnapToRoad(p, roadsRoot, out Vector3 snapped))
                    p = snapped;
                else if (NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas))
                    p = hit.position;

                if (dense.Count == 0 || (dense[dense.Count - 1] - p).sqrMagnitude > 0.6f * 0.6f)
                    dense.Add(p);
            }
        }

        return dense.Count >= 4 ? dense : loop;
    }

    private static List<Vector3> SnapAllToRoad(List<Vector3> pts)
    {
        Transform roadsRoot = FindRoadsRoot();
        var outPts = new List<Vector3>(pts.Count);
        foreach (var p in pts)
        {
            if (TrySnapToRoad(p, roadsRoot, out Vector3 onRoad))
                outPts.Add(onRoad);
            else if (PointOnOrNearRoad(p, roadsRoot, 2.5f) &&
                     NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas))
                outPts.Add(hit.position);
            // else drop — point was in a building gap
        }
        return Dedup(outPts, 1.0f);
    }

    private static List<Vector3> RemoveOffRoadAndCollinear(List<Vector3> pts)
    {
        if (pts.Count < 3) return pts;
        var cleaned = new List<Vector3> { pts[0] };
        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 prev = cleaned[cleaned.Count - 1];
            Vector3 cur = pts[i];
            if ((prev - cur).sqrMagnitude < 0.4f * 0.4f) continue;

            // Drop points that would form a building-cutting chord with previous.
            if (!SegmentClearsBuildings(prev, cur))
                continue;

            cleaned.Add(cur);
        }

        // Ensure closing edge is safe; trim tail if needed.
        if (cleaned.Count >= 4)
        {
            while (cleaned.Count >= 4 && !SegmentClearsBuildings(cleaned[cleaned.Count - 1], cleaned[0]))
                cleaned.RemoveAt(cleaned.Count - 1);
        }
        return cleaned;
    }

    private static Vector3[] ResamplePolyline(List<Vector3> loop, float spacing)
    {
        if (loop.Count < 2) return loop.ToArray();

        // Measure closed length.
        float total = 0f;
        for (int i = 0; i < loop.Count; i++)
            total += Vector3.Distance(loop[i], loop[(i + 1) % loop.Count]);

        int count = Mathf.Max(8, Mathf.RoundToInt(total / Mathf.Max(0.5f, spacing)));
        var result = new Vector3[count];
        float step = total / count;
        float acc = 0f;
        int seg = 0;

        for (int i = 0; i < count; i++)
        {
            float target = i * step;
            while (acc + Vector3.Distance(loop[seg], loop[(seg + 1) % loop.Count]) < target - 0.0001f)
            {
                acc += Vector3.Distance(loop[seg], loop[(seg + 1) % loop.Count]);
                seg = (seg + 1) % loop.Count;
                if (seg == 0 && i > 0) break;
            }

            Vector3 a = loop[seg];
            Vector3 b = loop[(seg + 1) % loop.Count];
            float segLen = Vector3.Distance(a, b);
            float u = segLen > 0.0001f ? Mathf.Clamp01((target - acc) / segLen) : 0f;
            Vector3 p = Vector3.Lerp(a, b, u);

            if (NavMesh.SamplePosition(p, out var hit, 1.25f, NavMesh.AllAreas))
                p = hit.position;
            result[i] = p;
        }
        return result;
    }

    private void BuildCumulative()
    {
        _cumLen = new float[_points.Length];
        _totalLen = 0f;
        _cumLen[0] = 0f;
        for (int i = 1; i < _points.Length; i++)
        {
            _totalLen += Vector3.Distance(_points[i - 1], _points[i]);
            _cumLen[i] = _totalLen;
        }
        _totalLen += Vector3.Distance(_points[_points.Length - 1], _points[0]);
    }

    private int FindSegment(float dist)
    {
        int lo = 0, hi = _cumLen.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_cumLen[mid] <= dist) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    private void BuildLine()
    {
        if (!showRouteLine || !IsReady) return;
        if (_line == null)
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            _line.widthMultiplier = lineWidth;
            _line.numCornerVertices = 4;
            _line.numCapVertices = 2;
            _line.useWorldSpace = true;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
        }

        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(lineColorA, 0f),
                new GradientColorKey(lineColorB, 0.5f),
                new GradientColorKey(lineColorA, 1f)
            },
            new[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.8f, 1f)
            });
        _line.colorGradient = grad;

        _line.positionCount = _points.Length + 1;
        for (int i = 0; i < _points.Length; i++)
        {
            Vector3 p = _points[i];
            p.y += 0.12f;
            _line.SetPosition(i, p);
        }
        Vector3 close = _points[0];
        close.y += 0.12f;
        _line.SetPosition(_points.Length, close);
    }
}
