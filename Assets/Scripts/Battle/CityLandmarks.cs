using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Finds the real city buildings already placed in the scene and picks an open
/// patch in front of them so the isometric camera is not staring into a wall.
/// </summary>
public static class CityLandmarks
{
    public struct Spot
    {
        public string PlaceName;
        public Vector3 Point;
        public Transform Building;
    }

    static readonly string[] PubHints = { "pub_bar_area", "pub_bar", "bar_001", "bar_" };
    static readonly string[] GymHints = { "GYM_Area", "gym_area", "gym_001", "gym" };
    static readonly string[] HospitalHints = { "hospital_AREA", "hospital_area", "hospital_001", "hospital" };
    static readonly string[] DolphinHints = { "dolphinariumAREA", "dolphinarium", "dolphin" };
    static readonly string[] ChurchHints = { "church_area", "church_001", "church_002", "church" };
    static readonly string[] StationHints = { "train_station", "railway_station", "station_area", "railway" };
    static readonly string[] FireHints = { "firestation", "fire_station", "fire_station_001" };
    static readonly string[] BarberHints = { "barber", "barbershop", "barber_shop" };
    static readonly string[] PoliceHints = { "police_department", "police_area", "police" };
    static readonly string[] MallHints = { "mall_001", "shopping", "market_area", "mall" };
    static readonly string[] SchoolHints = { "school_001", "school_area", "school" };

    public static Spot Pub(Vector3 fallback) => Locate("PUB", PubHints, fallback);
    public static Spot Gym(Vector3 fallback) => Locate("GYM", GymHints, fallback);
    public static Spot Hospital(Vector3 fallback) => Locate("HOSPITAL", HospitalHints, fallback);
    public static Spot Dolphinarium(Vector3 fallback) => Locate("DOLPHINARIUM", DolphinHints, fallback);
    public static Spot Church(Vector3 fallback) => Locate("CHURCH", ChurchHints, fallback);
    public static Spot Station(Vector3 fallback) => Locate("STATION", StationHints, fallback);
    public static Spot FireStation(Vector3 fallback) => Locate("FIRE STATION", FireHints, fallback);
    public static Spot Barber(Vector3 fallback) => Locate("BARBER", BarberHints, fallback);
    public static Spot Police(Vector3 fallback) => Locate("POLICE", PoliceHints, fallback);
    public static Spot Mall(Vector3 fallback) => Locate("SHOPS", MallHints, fallback);
    public static Spot School(Vector3 fallback) => Locate("SCHOOL", SchoolHints, fallback);

    public static Spot Locate(string label, string[] hints, Vector3 fallback)
    {
        var building = FindBuilding(hints);
        if (!building)
        {
            Debug.Log($"[CityLandmarks] {label} model was not in the scene. Using open ground.");
            return new Spot { PlaceName = label, Point = fallback, Building = null };
        }
        Vector3 point = OpenApproach(building, fallback);
        Debug.Log($"[CityLandmarks] {label} uses {building.name} at {point}.");
        return new Spot { PlaceName = label, Point = point, Building = building };
    }

    public static Transform FindBuilding(string[] hints)
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var hint in hints)
        {
            Transform exact = null;
            float exactSize = -1f;
            foreach (var t in all)
            {
                if (!t || t.GetComponentInParent<Canvas>()) continue;
                if (!string.Equals(t.name, hint, System.StringComparison.OrdinalIgnoreCase)) continue;
                float size = BoundsOf(t).size.sqrMagnitude;
                if (size > exactSize) { exactSize = size; exact = t; }
            }
            if (exact) return exact;
        }
        Transform best = null;
        float bestSize = 0f;
        foreach (var hint in hints)
        {
            foreach (var t in all)
            {
                if (!t || t.GetComponentInParent<Canvas>()) continue;
                if (t.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                float size = BoundsOf(t).size.sqrMagnitude;
                if (size > bestSize) { bestSize = size; best = t; }
            }
            if (best) return best;
        }
        return null;
    }

    /// <summary>Stand in the open ground the camera can see, not inside the mesh.</summary>
    public static Vector3 OpenApproach(Transform building, Vector3 fallback)
    {
        if (!HasMesh(building))
        {
            Vector3 marker = building.position;
            if (NavMesh.SamplePosition(marker, out var markerHit, 8f, NavMesh.AllAreas)) return markerHit.position;
            return marker;
        }
        Bounds bounds = BoundsOf(building);
        Vector3 look = Camera.main ? Camera.main.transform.forward : Quaternion.Euler(54f, 45f, 0f) * Vector3.forward;
        look.y = 0f;
        if (look.sqrMagnitude < 0.01f) look = new Vector3(0.7f, 0f, 0.7f);
        look.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, look);
        float reach = Mathf.Max(bounds.extents.x, bounds.extents.z) + 9f;
        Vector3 origin = new Vector3(bounds.center.x, 0f, bounds.center.z);
        Vector3[] candidates =
        {
            origin - look * reach,
            origin - look * (reach + 8f),
            origin + right * reach,
            origin - right * reach,
            origin + look * (reach + 6f)
        };
        Vector3 best = fallback;
        float bestScore = float.MinValue;
        bool found = false;
        foreach (var guess in candidates)
        {
            if (!NavMesh.SamplePosition(guess, out var hit, 14f, NavMesh.AllAreas)) continue;
            Vector3 point = hit.position;
            if (ContainsFlat(bounds, point)||!HasTacticalClearance(point)) continue;
            float clearance = Horizontal(point, origin);
            float towardCamera = Vector3.Dot(origin - point, look);
            float score = clearance + towardCamera * 0.35f;
            if (score > bestScore) { bestScore = score; best = point; found = true; }
        }
        return found ? best : (NavMesh.SamplePosition(fallback, out var fb, 8f, NavMesh.AllAreas) ? fb.position : fallback);
    }

    public static bool HasTacticalClearance(Vector3 point)
    {
        if(!CityActivityStreaming.TryStreetPoint(point,out _,2f))return false;
        // Upward rays miss one-sided mountain meshes when the point starts inside them.
        if(Physics.Raycast(point+Vector3.up*250f,Vector3.down,out var roof,260f,~0,QueryTriggerInteraction.Ignore)&&roof.point.y>point.y+3f)return false;
        // Test the viewing corridor too: walkable ground can still be hidden behind a mountain.
        // Location binding can run before the camera's Start initializes its isometric pose.
        Vector3 forward=Quaternion.Euler(PlayerPrefs.GetFloat("CityCameraPitch",65),PlayerPrefs.GetFloat("CityCameraYaw",45),0)*Vector3.forward;
        Vector3 eye=point-forward*45f;
        return !Physics.Linecast(point+Vector3.up*2.5f,eye,~0,QueryTriggerInteraction.Ignore);
    }

    public static Vector3 ClearNearby(Vector3 point)
    {
        if(HasTacticalClearance(point))return point;
        var route=new NavMeshPath();
        for(int ring=1;ring<=48;ring++)for(int i=0;i<16;i++)
        {
            float angle=i*Mathf.PI/8;
            var guess=point+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*8;
            if(NavMesh.SamplePosition(guess,out var hit,12,NavMesh.AllAreas)&&HasTacticalClearance(hit.position))
            {
                if(CityGameplay.Instance&&(!NavMesh.CalculatePath(CityGameplay.Instance.Home,hit.position,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete))continue;
                return hit.position;
            }
        }
        return point;
    }

    static bool HasMesh(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            if (renderer && !(renderer is ParticleSystemRenderer)) return true;
        return false;
    }

    static Bounds BoundsOf(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds bounds = new Bounds(root.position, Vector3.one * 4f);
        foreach (var renderer in renderers)
        {
            if (!renderer || renderer is ParticleSystemRenderer) continue;
            if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    static bool ContainsFlat(Bounds bounds, Vector3 point)
    {
        point.y = bounds.center.y;
        bounds.Expand(1.5f);
        return bounds.Contains(point);
    }

    static float Horizontal(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
