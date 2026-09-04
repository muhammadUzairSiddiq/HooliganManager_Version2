using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any building GameObject in the battle scene.
/// Assign a <see cref="MapBuildingData"/> asset in the Inspector to fully configure
/// the building — no code changes needed for new building types.
///
/// Responsibilities:
///   • Detects nearby player / enemy units via periodic OverlapSphere
///   • Shows / hides the BuildingInteractionPanel in BattleUIController
///   • Executes actions (Heal, Boost, ReduceHeat, …) on targeted units
///   • Optionally auto-applies the first action to nearby enemy units
///
/// Gizmos draw the interaction radius in the Scene view for easy tuning.
/// </summary>
public class MapBuilding : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Building Configuration")]
    [Tooltip("ScriptableObject that defines this building's name, icon, and available actions.")]
    public MapBuildingData data;

    [Header("Detection Layer Mask")]
    [Tooltip("Layer(s) to include when scanning for units. Usually 'Default' or a dedicated 'Agent' layer.")]
    public LayerMask unitLayerMask = ~0;   // all layers by default

    [Header("Visual (optional)")]
    [Tooltip("Optional renderer/icon that gets tinted to buildingColor at runtime.")]
    public Renderer buildingRenderer;

    [Header("Recruitment (optional)")]
    [Tooltip("Specific position where newly recruited agents will spawn. If left null, they spawn outside the building at 80% of the interaction radius along the forward direction.")]
    public Transform recruitSpawnPoint;

    // ── Internal ──────────────────────────────────────────────────────────
    private List<AgentController>  _nearbyPlayers = new List<AgentController>();
    private bool                   _panelVisible   = false;

    // Scanning
    private float _scanTimer = 0f;
    private const float SCAN_INTERVAL = 0.35f;

    // Enemy auto-use
    private float _enemyAutoUseTimer = 0f;

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>Fired when player units first enter the building radius.</summary>
    public event System.Action<MapBuilding, List<AgentController>> OnPlayerUnitsEntered;
    /// <summary>Fired when all player units leave the building radius.</summary>
    public event System.Action<MapBuilding>                         OnPlayerUnitsExited;

    // ─────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Old desk-bribe police stations are retired — live patrols handle bribes.
        if (IsRetiredBribeBuilding())
        {
            enabled = false;
            var ring = transform.Find("InteractionRing");
            if (ring != null) ring.gameObject.SetActive(false);
            return;
        }

        // Tint the optional renderer to the building's accent colour.
        if (buildingRenderer != null && data != null)
            buildingRenderer.material.color = data.buildingColor;

        SetupVisualRing();
    }

    private bool IsRetiredBribeBuilding()
    {
        if (data == null || data.actions == null) return false;
        foreach (var a in data.actions)
        {
            if (a == null) continue;
            if (a.actionType == BuildingActionType.ReduceHeat) return true;
            if (!string.IsNullOrEmpty(a.actionLabel) &&
                a.actionLabel.IndexOf("BRIBE", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        if (!string.IsNullOrEmpty(data.buildingName) &&
            data.buildingName.IndexOf("POLICE", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
    }

    void Update()
    {
        if (data == null) return;

        _scanTimer += Time.deltaTime;
        if (_scanTimer >= SCAN_INTERVAL)
        {
            _scanTimer = 0f;
            ScanForUnits();
        }

        // Enemy auto-use (e.g. Infirmary auto-heal)
        if (data.enemyAutoUse && _nearbyPlayers.Count == 0)
        {
            _enemyAutoUseTimer += Time.deltaTime;
            if (_enemyAutoUseTimer >= data.enemyAutoUseInterval)
            {
                _enemyAutoUseTimer = 0f;
                AutoUseOnEnemies();
            }
        }
    }

    // ── Unit Detection ────────────────────────────────────────────────────

    private void ScanForUnits()
    {
        if (data == null) return;

        _nearbyPlayers.Clear();

        // Broad-phase sphere overlap
        Collider[] hits = Physics.OverlapSphere(transform.position, data.interactionRadius, unitLayerMask);
        foreach (Collider col in hits)
        {
            // Player agents
            var agent = col.GetComponent<AgentController>();
            if (agent != null && agent.IsAlive)
                _nearbyPlayers.Add(agent);
        }

        // ── Show or hide panel based on proximity ──
        bool shouldShow = _nearbyPlayers.Count > 0;

        if (shouldShow && !_panelVisible)
        {
            _panelVisible = true;
            OnPlayerUnitsEntered?.Invoke(this, _nearbyPlayers);
            BattleUIController.instance?.ShowBuildingUI(this, _nearbyPlayers);
        }
        else if (!shouldShow && _panelVisible)
        {
            _panelVisible = false;
            OnPlayerUnitsExited?.Invoke(this);
            BattleUIController.instance?.HideBuildingUI(this);
        }
        else if (shouldShow && _panelVisible)
        {
            // Refresh unit list in the open panel (units may have joined/left)
            BattleUIController.instance?.RefreshBuildingUnits(this, _nearbyPlayers);
        }
    }

    // ── Enemy Auto-Use ────────────────────────────────────────────────────

    private void AutoUseOnEnemies()
    {
        if (data == null || data.actions == null || data.actions.Count == 0) return;
        if (BattleManager.instance == null) return;

        // Use the first action on nearby enemies
        BuildingActionConfig firstAction = data.actions[0];
        var enemies = BattleManager.instance.EnemyAgents;
        if (enemies == null) return;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            if (Vector3.Distance(transform.position, enemy.transform.position) <= data.interactionRadius)
            {
                ExecuteActionOnEnemy(firstAction, enemy);
            }
        }
    }

    private void ExecuteActionOnEnemy(BuildingActionConfig action, EnemyController enemy)
    {
        switch (action.actionType)
        {
            case BuildingActionType.Heal:
                float healAmt = action.healToFull ? enemy.MaxHp : action.healAmount;
                enemy.HealAmount(healAmt);
                break;
            // Other action types don't have meaningful equivalents for enemies.
        }
    }

    // ── Public: Execute Action on Player Units ────────────────────────────

    /// <summary>
    /// Called by BuildingInteractionPanel when the player presses an action button.
    /// Applies the action to the supplied list of player agents and handles cost/cooldown.
    /// </summary>
    public bool ExecuteAction(BuildingActionConfig action, List<AgentController> targets)
    {
        if (action == null || !action.IsReady) return false;

        // Special check: Recruitment spawns a new agent at the building's position
        if (action.actionType == BuildingActionType.Recruit)
        {
            if (BattleManager.instance != null)
            {
                if (action.costMoney > 0 && BattleManager.instance.sessionMoneyEarned < action.costMoney)
                {
                    Debug.Log($"[MapBuilding] Not enough money to recruit '{action.recruitName}' (costs £{action.costMoney}).");
                    return false;
                }

                // Determine spawn position: use custom spawn point if assigned,
                // otherwise fallback to a distance of 80% of the interaction radius along the forward vector.
                Vector3 spawnPos;
                if (recruitSpawnPoint != null)
                {
                    spawnPos = recruitSpawnPoint.position;
                }
                else
                {
                    spawnPos = transform.position + transform.forward * (data.interactionRadius * 0.8f);
                }

                if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    spawnPos = hit.position;
                }

                var newAgent = BattleManager.instance.SpawnRecruitedAgentAt(spawnPos, action.recruitName);
                if (newAgent == null)
                {
                    return false; // Spawning failed (e.g. at max limits)
                }

                // Deduct cost after successful spawn
                if (action.costMoney > 0)
                {
                    BattleManager.instance.sessionMoneyEarned -= action.costMoney;
                }
            }

            action.LastUsedTime = Time.time;
            Debug.Log($"[MapBuilding] '{data.buildingName}' successfully recruited new unit '{action.recruitName}'.");
            return true;
        }

        // Cost check
        if (action.costMoney > 0 && BattleManager.instance != null)
        {
            if (BattleManager.instance.sessionMoneyEarned < action.costMoney)
            {
                Debug.Log($"[MapBuilding] Not enough money to use '{action.actionLabel}' (costs £{action.costMoney}).");
                return false;
            }
            BattleManager.instance.sessionMoneyEarned -= action.costMoney;
        }

        // Apply to each target
        foreach (var agent in targets)
        {
            if (agent == null || !agent.IsAlive) continue;
            ApplyActionToAgent(action, agent);
        }

        // Consume cooldown
        action.LastUsedTime = Time.time;

        Debug.Log($"[MapBuilding] '{data.buildingName}' executed '{action.actionLabel}' on {targets.Count} unit(s).");
        return true;
    }

    private void ApplyActionToAgent(BuildingActionConfig action, AgentController agent)
    {
        switch (action.actionType)
        {
            case BuildingActionType.Heal:
                float healAmt = action.healToFull ? agent.Data?.MaxHp ?? 100f : action.healAmount;
                agent.HealAmount(healAmt);
                break;

            case BuildingActionType.Boost:
                agent.ApplyTemporaryBoost(action.boostType, action.boostAmount, action.boostDuration);
                break;

            case BuildingActionType.ReduceHeat:
                // Retired — bribes only via live police arrival popup.
                Debug.Log("[MapBuilding] ReduceHeat / desk bribe disabled.");
                break;

            case BuildingActionType.Custom:
                // No-op — extend here for future custom action types.
                break;
        }
    }

    private void ReducePoliceHeat(int amount)
    {
        if (GameData.instance?.PlayerData == null) return;
        GameData.instance.PlayerData.PoliceHeat =
            Mathf.Max(0, GameData.instance.PlayerData.PoliceHeat - amount);

        if (BattleManager.instance != null)
            BattleManager.instance.sessionHeatGained =
                Mathf.Max(0, BattleManager.instance.sessionHeatGained - amount);

        Debug.Log($"[MapBuilding] Police heat reduced by {amount}.");
    }

    /// <summary>
    /// Dynamically configures a LineRenderer to draw a clean interaction ring
    /// flat on the ground matching data.interactionRadius and data.buildingColor.
    /// Uses raycasting to align segments with uneven terrain.
    /// </summary>
    private void SetupVisualRing()
    {
        if (data == null) return;

        LineRenderer line = GetComponent<LineRenderer>();
        if (line == null)
            line = gameObject.AddComponent<LineRenderer>();

        int segments = 60; // 60 segments for a smooth circle
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = 0.1f;
        line.endWidth = 0.1f;

        // Use built-in UI/2D sprite material to avoid light dependencies
        line.material = new Material(Shader.Find("Sprites/Default"));

        Color tintColor = data.buildingColor;
        tintColor.a = 0.5f; // Semi-transparent overlay
        line.startColor = tintColor;
        line.endColor = tintColor;

        Vector3 center = transform.position;
        float angle = 0f;

        for (int i = 0; i < segments; i++)
        {
            float x = Mathf.Sin(angle) * data.interactionRadius;
            float z = Mathf.Cos(angle) * data.interactionRadius;
            Vector3 worldPos = center + new Vector3(x, 2f, z); // Start above for Raycast

            float groundY = center.y;
            // Raycast down to find ground level
            if (Physics.Raycast(worldPos, Vector3.down, out RaycastHit hit, 20f))
            {
                // Verify we didn't hit an agent
                if (hit.collider.GetComponent<AgentController>() == null && 
                    hit.collider.GetComponent<EnemyController>() == null)
                {
                    groundY = hit.point.y;
                }
            }
            worldPos.y = groundY + 0.05f; // Draw slightly above ground to prevent Z-fighting

            line.SetPosition(i, worldPos);
            angle += 2f * Mathf.PI / segments;
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────

    void OnDisable()
    {
        // Ensure panel is hidden if the building object is deactivated mid-battle.
        if (_panelVisible)
        {
            _panelVisible = false;
            BattleUIController.instance?.HideBuildingUI(this);
        }
    }

    // ── Gizmos ────────────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        if (data == null) return;
        Gizmos.color = data.buildingColor;
        Gizmos.DrawWireSphere(transform.position, data.interactionRadius);

#if UNITY_EDITOR
        UnityEditor.Handles.color = data.buildingColor;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (data.interactionRadius + 0.5f),
            $"[{data.buildingName}]"
        );
#endif
    }
}
