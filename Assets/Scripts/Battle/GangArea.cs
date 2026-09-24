using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Rival gang turf. Standing inside the circle opens HAVE IT / MOVE ON.
/// Only the crew in the circle are offered. People already on another job are left alone.
/// </summary>
public class GangArea : MonoBehaviour
{
    private string _gangName;
    private float _radius;
    private float _detectRadius;
    private Color _color;
    private TextMeshPro _label;
    private float _scanTimer;
    private bool _promptArmed = true;
    private bool _mustLeaveBeforeReprompt;

    public string GangName => _gangName;
    public Color ZoneColor => _color;
    public float Radius => _radius;
    public float DetectRadius => _detectRadius;

    public void Setup(string gangName, Vector3 center, float radius, Color color)
    {
        _gangName = gangName;
        _radius = radius;
        _detectRadius = Mathf.Max(radius * 2.2f, radius * 1.7f * 0.71f + 3.5f);
        _color = SanitizeGangColor(color);

        transform.position = GroundedCenter(center);
        ZoneVolumeFactory.Create(transform, _color, radius, height: 2.6f);
        _label = ZoneLabelUtil.Create(transform, _gangName.ToUpperInvariant()+"\n<size=68%>RIVAL TURF · ENTER TO CONFRONT</size>", 4.4f, 8.2f);
        MiniMapIconFactory.Register(transform, MiniMapIconFactory.Kind.Gang, _gangName);
    }

    private static Color SanitizeGangColor(Color c)
    {
        float lum = c.r * 0.3f + c.g * 0.6f + c.b * 0.1f;
        if (c.a < 0.01f || lum > 0.92f || lum < 0.08f)
            return new Color(0.9f, 0.15f, 0.15f, 1f);
        c.a = 1f;
        return c;
    }

    private Vector3 GroundedCenter(Vector3 center)
    {
        if (Physics.Raycast(center + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
            center.y = hit.point.y + 0.02f;
        return center;
    }

    void Update()
    {
        if (_label != null && Camera.main != null)
            _label.transform.rotation = Camera.main.transform.rotation;

        _scanTimer += Time.deltaTime;
        if (_scanTimer < 0.2f) return;
        _scanTimer = 0f;
        if (BattleManager.instance == null || string.IsNullOrEmpty(_gangName)) return;

        var inside = CrewInside();
        if (inside.Count == 0)
        {
            _promptArmed = true;
            _mustLeaveBeforeReprompt = false;
            return;
        }
        if (!_promptArmed || _mustLeaveBeforeReprompt || GamePopup.AnyOpen || AlreadyFighting()) return;

        _promptArmed = false;
        _mustLeaveBeforeReprompt = true;
        var crew = new List<AgentController>(inside);
        GamePopup.Instance.Show(
            _gangName.ToUpperInvariant() + " TURF",
            "Your crew is on their ground. Have it with the lads in the circle, or walk them back out.",
            new GamePopup.Option("HAVE IT!", new Color(0.7f, 0.15f, 0.15f), () =>
            {
                BattleManager.instance?.AttackGang(_gangName, crew);
                AgentSelectionManager.instance?.ReleaseOrdered(crew);
            }),
            new GamePopup.Option("MOVE ON", new Color(0.25f, 0.32f, 0.4f), () => WalkOut(crew)));
    }

    List<AgentController> CrewInside()
    {
        var found = new List<AgentController>();
        float limit = _radius + 1.25f;
        foreach (var agent in BattleManager.instance.PlayerAgents)
        {
            if (!agent || !agent.IsAlive || agent.IsActivityLocked) continue;
            if (agent.IsOnAssignment && !agent.IsSelected) continue;
            Vector3 delta = agent.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= limit * limit) found.Add(agent);
        }
        return found;
    }

    bool AlreadyFighting()
    {
        foreach (var enemy in BattleManager.instance.EnemyAgents)
        {
            if (!enemy || !enemy.IsAlive || enemy.firmName != _gangName || !enemy.isHostile) continue;
            return true;
        }
        return false;
    }

    void WalkOut(List<AgentController> crew)
    {
        foreach (var agent in crew)
        {
            if (!agent || !agent.IsAlive) continue;
            Vector3 delta = agent.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.04f) delta = Vector3.forward;
            Vector3 dest = transform.position + delta.normalized * (_radius + 4f);
            dest.y = agent.transform.position.y;
            if (UnityEngine.AI.NavMesh.SamplePosition(dest, out var hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
                dest = hit.position;
            agent.CommandMoveTo(dest);
        }
    }
}
