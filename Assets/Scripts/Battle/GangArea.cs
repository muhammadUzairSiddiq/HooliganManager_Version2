using UnityEngine;
using TMPro;

/// <summary>
/// Rival gang turf. Enter → HAVE IT / MOVE ON.
/// MOVE ON hides the prompt and requires a full leave + re-enter before it returns.
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
    private bool _wasInside;
    private bool _mustLeaveBeforeReprompt;

    public string GangName => _gangName;
    public Color ZoneColor => _color;
    public float DetectRadius => _detectRadius;

    public void Setup(string gangName, Vector3 center, float radius, Color color)
    {
        _gangName = gangName;
        _radius = radius;
        _detectRadius = Mathf.Max(radius * 2.2f, radius * 1.7f * 0.71f + 3.5f);
        _color = SanitizeGangColor(color);

        transform.position = GroundedCenter(center);
        ZoneVolumeFactory.Create(transform, _color, radius, height: 2.6f);
        _label = ZoneLabelUtil.Create(transform, _gangName.ToUpperInvariant(), 3.4f, 12f);
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
        if (_scanTimer < 0.12f) return;
        _scanTimer = 0f;
        if (BattleManager.instance == null) return;

        if (GangIsHostile())
        {
            _promptArmed = false;
            _wasInside = true;
            return;
        }

        bool playerInside = AnyPlayerInside();

        // OnTriggerExit equivalent — must fully leave before the prompt can arm again.
        if (!playerInside)
        {
            if (_wasInside || _mustLeaveBeforeReprompt)
            {
                _mustLeaveBeforeReprompt = false;
                _promptArmed = true;
            }
            _wasInside = false;
            return;
        }

        // OnTriggerEnter equivalent — rising edge only.
        if (!_wasInside && _promptArmed && !_mustLeaveBeforeReprompt && !IsPromptBlocked())
        {
            _promptArmed = false;
            ShowEncounterPopup();
        }

        _wasInside = true;
    }

    private bool AnyPlayerInside()
    {
        float r2 = _detectRadius * _detectRadius;
        foreach (var a in BattleManager.instance.PlayerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            Vector3 d = a.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) return true;
        }
        return false;
    }

    private static bool IsPromptBlocked()
    {
        if (GamePopup.Instance != null && GamePopup.Instance.IsOpen) return true;
        if (RecruitDialogBox.Instance != null && RecruitDialogBox.Instance.IsOpen) return true;
        if (LivePoliceSystem.Instance != null && LivePoliceSystem.Instance.BlocksWorldPrompts) return true;
        return false;
    }

    private bool GangIsHostile()
    {
        foreach (var e in BattleManager.instance.EnemyAgents)
            if (e != null && e.IsAlive && e.firmName == _gangName && e.isHostile)
                return true;
        return false;
    }

    private void ShowEncounterPopup()
    {
        BattleManager.instance?.HaltPlayerAgents();

        GamePopup.Instance.Show(
            _gangName.ToUpper() + " TURF",
            $"You've stepped onto {_gangName}'s patch. Do you want it?",
            new GamePopup.Option("HAVE IT!", new Color(0.7f, 0.15f, 0.15f), () =>
            {
                BattleManager.instance?.AttackGang(_gangName);
            }),
            new GamePopup.Option("MOVE ON", new Color(0.25f, 0.32f, 0.4f), OnMoveOn)
        );
    }

    private void OnMoveOn()
    {
        // Hide is already done by GamePopup button. Do NOT fight.
        // Require a full exit before the prompt can appear again.
        _mustLeaveBeforeReprompt = true;
        _promptArmed = false;
        _wasInside = true;
        BattleManager.instance?.PullPlayerAgentsFrom(transform.position, _detectRadius + 3.5f);
    }
}
