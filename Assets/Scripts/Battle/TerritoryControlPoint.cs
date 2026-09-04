using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Capturable pub/turf point. Does NOT silently start fights — that is owned by
/// <see cref="GangArea"/> (HAVE IT / MOVE ON). This only handles capture progress
/// when no rival fighters are present.
/// </summary>
public class TerritoryControlPoint : MonoBehaviour
{
    public string zoneName = "Local Pub";
    public float captureDuration = 12f;
    public int moneyReward = 1500;
    public int reputationReward = 10;
    public int heatGained = 2;

    public float detectionRadius = 8f;

    [Header("Faction Setup")]
    public string owningFaction = "";

    private float _captureProgress = 0f;
    private bool _isCaptured = false;

    private readonly List<AgentController> _playerUnits = new List<AgentController>();
    private readonly List<EnemyController> _enemyUnits = new List<EnemyController>();

    void Start()
    {
        MiniMapIconFactory.Register(transform, MiniMapIconFactory.Kind.Turf, zoneName);
    }

    void OnDisable()
    {
        if (Arikan.MiniMapView.Instance != null)
            Arikan.MiniMapView.Instance.UnfollowTarget(transform);
    }

    void Update()
    {
        if (_isCaptured) return;
        if (BattleManager.instance == null) return;

        _playerUnits.Clear();
        _enemyUnits.Clear();

        foreach (var a in BattleManager.instance.PlayerAgents)
        {
            if (a != null && a.IsAlive &&
                Vector3.Distance(transform.position, a.transform.position) <= detectionRadius)
                _playerUnits.Add(a);
        }

        foreach (var e in BattleManager.instance.EnemyAgents)
        {
            if (e != null && e.IsAlive && e.firmName != "POLICE" &&
                Vector3.Distance(transform.position, e.transform.position) <= detectionRadius)
                _enemyUnits.Add(e);
        }

        // Capture only when the pad is clear of rivals — never auto-aggro them.
        if (_playerUnits.Count > 0 && _enemyUnits.Count == 0)
        {
            _captureProgress += Time.deltaTime / captureDuration;
            _captureProgress = Mathf.Clamp01(_captureProgress);

            if (BattleUIController.instance != null && BattleUIController.instance.objectiveText != null)
                BattleUIController.instance.objectiveText.text =
                    $"SECURING {zoneName.ToUpper()}: {Mathf.RoundToInt(_captureProgress * 100f)}%";

            if (_captureProgress >= 1f)
                CaptureZone();
        }
        else if (_enemyUnits.Count > 0 && _playerUnits.Count == 0)
        {
            if (_captureProgress > 0f)
                _captureProgress = Mathf.Max(0f, _captureProgress - Time.deltaTime / captureDuration);
        }
    }

    private void CaptureZone()
    {
        _isCaptured = true;

        if (BattleManager.instance != null)
        {
            BattleManager.instance.sessionMoneyEarned += moneyReward;
            BattleManager.instance.sessionReputationGained += reputationReward;
            BattleManager.instance.sessionHeatGained += heatGained;
        }

        if (BattleUIController.instance != null)
        {
            if (BattleUIController.instance.objectiveText != null)
                BattleUIController.instance.objectiveText.text = $"SECURED {zoneName.ToUpper()}! (+£{moneyReward:N0})";
            BattleUIController.instance.ShowAlert(
                $"Secured {zoneName}! +£{moneyReward:N0}", 4f);
        }

        Debug.Log($"[TerritoryControlPoint] Secured {zoneName}!");
        gameObject.SetActive(false);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = _isCaptured ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
