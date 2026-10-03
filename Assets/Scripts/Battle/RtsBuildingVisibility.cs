using System.Collections.Generic;
using UnityEngine;

/// <summary>Hides renderers only; collision, navigation and interaction stay authoritative.</summary>
public sealed class RtsBuildingVisibility : MonoBehaviour
{
    public static RtsBuildingVisibility Instance { get; private set; }
    readonly List<Renderer> buildings = new List<Renderer>();
    readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
    readonly HashSet<Renderer> required = new HashSet<Renderer>();
    readonly List<Renderer> restored = new List<Renderer>();
    readonly List<Vector3> units = new List<Vector3>();
    Camera view;
    float next;
    readonly List<Renderer> trafficVehicles=new List<Renderer>();
    void Awake() { Instance = this; }
    void Start()
    {
        view = Camera.main;
        var railSegments=new List<Bounds>();
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var name = renderer.name.ToLowerInvariant();
            bool city=HasAncestor(renderer.transform,"Demonstration")||name.StartsWith("railway_")||name=="road_column_001";
            bool traffic=HasAncestor(renderer.transform,"Traffic")||name.StartsWith("road_");
            if(traffic&&name.StartsWith("car"))trafficVehicles.Add(renderer);
            if(!city&&!traffic)continue;
            bool elevatedRoad=traffic&&name.StartsWith("road_")&&renderer.bounds.center.y>3f;
            if (name.Contains("bridge")||name=="road_column_001"||name.StartsWith("railway_")||elevatedRoad)
            {
                if(name.StartsWith("railway_")&&!name.Contains("column"))railSegments.Add(renderer.bounds);
                renderer.enabled = false;
                foreach (var collider in renderer.GetComponents<Collider>()) collider.enabled = false;
                continue;
            }
            if(!city)continue;
            if (renderer.bounds.size.y >= 4 && !name.Contains("tree") && !name.Contains("lamp") && !name.Contains("pole")) buildings.Add(renderer);
        }
        GroundRailTrack.Build(railSegments);
    }
    static bool HasAncestor(Transform node,string name){for(;node;node=node.parent)if(node.name.StartsWith(name))return true;return false;}
    void LateUpdate()
    {
        if (!view || Time.unscaledTime < next) return;
        next = Time.unscaledTime + .08f;
        units.Clear(); required.Clear();
        var bm = BattleManager.instance;
        if (bm)
        {
            foreach (var unit in bm.PlayerAgents) if (unit && unit.IsAlive && unit.gameObject.activeInHierarchy) Add(unit.transform.position);
            foreach (var unit in bm.EnemyAgents) if (unit && unit.IsAlive && unit.gameObject.activeInHierarchy) Add(unit.transform.position);
        }
        var cameraPosition = view.transform.position;
        foreach(var vehicle in trafficVehicles)if(vehicle)vehicle.forceRenderingOff=vehicle.bounds.center.y>5f;
        foreach (var building in buildings)
        {
            if (!building || !building.gameObject.activeInHierarchy) continue;
            var bounds = building.bounds; bounds.Expand(2f);
            bool obstructs = bounds.Contains(cameraPosition);
            for (int i = 0; !obstructs && i < units.Count; i++)
            {
                var delta = units[i] - cameraPosition;
                obstructs = bounds.IntersectRay(new Ray(cameraPosition, delta.normalized), out var distance) && distance < delta.magnitude;
            }
            if (!obstructs) continue;
            required.Add(building);
            if (!hidden.ContainsKey(building)) hidden.Add(building, building.forceRenderingOff);
            building.forceRenderingOff = true;
        }
        restored.Clear();
        foreach (var pair in hidden)
            if (!pair.Key || !required.Contains(pair.Key)) { if (pair.Key) pair.Key.forceRenderingOff = pair.Value; restored.Add(pair.Key); }
        foreach (var renderer in restored) hidden.Remove(renderer);
    }
    void Add(Vector3 point)
    {
        point += Vector3.up * 2f;
        var p = view.WorldToViewportPoint(point);
        if (p.z > 0 && p.x > -.02f && p.x < 1.02f && p.y > -.02f && p.y < 1.02f) units.Add(point);
    }
    void OnDisable() { foreach (var pair in hidden) if (pair.Key) pair.Key.forceRenderingOff = pair.Value; hidden.Clear(); }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
