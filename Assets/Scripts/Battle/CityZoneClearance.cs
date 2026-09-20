using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Keeps world rings from sitting on top of each other. After landmarks, hold
/// areas, operations and gangs are placed, overlapping circles are pushed apart
/// on the NavMesh so a clear gap remains between them.
/// </summary>
public static class CityZoneClearance
{
    public const float Gap = 5f;

    sealed class Zone
    {
        public Transform Transform;
        public float Radius;
        public int Mobility;
        public GangArea Gang;
    }

    public static bool OverlapsExisting(Vector3 point, float radius, Transform ignore = null)
    {
        foreach (var zone in Collect())
        {
            if (!zone.Transform || zone.Transform == ignore) continue;
            if (TooClose(point, radius, zone.Transform.position, zone.Radius)) return true;
        }
        return false;
    }

    public static void Resolve()
    {
        var zones = Collect();
        if (zones.Count < 2) return;
        for (int iter = 0; iter < 12; iter++)
        {
            bool moved = false;
            for (int i = 0; i < zones.Count; i++)
            {
                for (int j = i + 1; j < zones.Count; j++)
                {
                    if (Separate(zones[i], zones[j])) moved = true;
                }
            }
            if (!moved) break;
        }
    }

    static List<Zone> Collect()
    {
        var zones = new List<Zone>();
        foreach (var point in Object.FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None))
            if (point) Add(zones, point.transform, point.detectionRadius + .2f, 1);
        foreach (var gang in Object.FindObjectsByType<GangArea>(FindObjectsSortMode.None))
            if (gang) Add(zones, gang.transform, gang.Radius, 3, gang);
        foreach (var node in Object.FindObjectsByType<CityOperationNode>(FindObjectsSortMode.None))
            if (node) Add(zones, node.transform, 3.2f, 2);
        foreach (var label in Object.FindObjectsByType<CityLocationLabel>(FindObjectsSortMode.None))
        {
            if (!label) continue;
            float radius = label.LocationIndex == 0 ? 4.2f : 3.1f;
            Add(zones, label.transform, radius, 0);
        }
        var taxi = CityActionSystem.Instance ? CityActionSystem.Instance.TaxiTransform : null;
        if (taxi) Add(zones, taxi, 5.2f, 1);
        foreach (var sabotage in Object.FindObjectsByType<CitySabotageTarget>(FindObjectsSortMode.None))
            if (sabotage && !sabotage.Complete) Add(zones, sabotage.transform, 4.5f, 2);
        return zones;
    }

    static void Add(List<Zone> zones, Transform transform, float radius, int mobility, GangArea gang = null)
    {
        if (!transform) return;
        zones.Add(new Zone { Transform = transform, Radius = radius, Mobility = mobility, Gang = gang });
    }

    static bool Separate(Zone a, Zone b)
    {
        if (!a.Transform || !b.Transform) return false;
        float need = a.Radius + b.Radius + Gap;
        Vector3 delta = b.Transform.position - a.Transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.0001f) delta = Vector3.right;
        float dist = delta.magnitude;
        if (dist >= need) return false;

        Vector3 dir = delta / dist;
        float push = need - dist + .35f;
        if (a.Mobility > b.Mobility) return Relocate(a, a.Transform.position - dir * push);
        if (b.Mobility > a.Mobility) return Relocate(b, b.Transform.position + dir * push);
        bool moved = Relocate(a, a.Transform.position - dir * (push * .5f));
        moved |= Relocate(b, b.Transform.position + dir * (push * .5f));
        return moved;
    }

    static bool Relocate(Zone zone, Vector3 desired)
    {
        if (zone.Mobility <= 0) return false;
        if (!NavMesh.SamplePosition(desired, out var hit, 14f, NavMesh.AllAreas)) return false;
        Vector3 next = hit.position;
        if ((next - zone.Transform.position).sqrMagnitude < .04f) return false;
        Vector3 old = zone.Transform.position;
        zone.Transform.position = next;
        if (zone.Gang) ShiftGangMembers(zone.Gang, old, next);
        return true;
    }

    static void ShiftGangMembers(GangArea gang, Vector3 from, Vector3 to)
    {
        Vector3 shift = to - from;
        shift.y = 0f;
        var bm = BattleManager.instance;
        if (!gang || bm == null) return;
        foreach (var enemy in bm.EnemyAgents)
        {
            if (!enemy || !enemy.IsAlive || enemy.firmName != gang.GangName) continue;
            Vector3 pos = enemy.transform.position + shift;
            if (NavMesh.SamplePosition(pos, out var hit, 8f, NavMesh.AllAreas)) pos = hit.position;
            var nav = enemy.GetComponent<NavMeshAgent>();
            if (nav && nav.enabled && nav.isOnNavMesh) nav.Warp(pos);
            else enemy.transform.position = pos;
            enemy.SetGangCenter(to);
        }
    }

    static bool TooClose(Vector3 a, float ra, Vector3 b, float rb)
    {
        Vector3 d = a - b;
        d.y = 0f;
        float need = ra + rb + Gap;
        return d.sqrMagnitude < need * need;
    }
}
