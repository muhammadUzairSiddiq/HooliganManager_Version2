using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Orchestrates the entire battle: spawns agents, manages rounds + timer,
/// tracks alive counts, and reports results back to GameData.
///
/// Attach to the BattleManager GameObject in BattleScene.
/// Set spawner transforms and prefab references in Inspector.
/// </summary>
public class BattleManager : MonoBehaviour
{
    public const int RivalFightHeatGain = 4;
    // ── Battle Mode ───────────────────────────────────────────────────────
    public enum BattleMode { RivalFight, PoliceRaid }

    // ── Singleton ─────────────────────────────────────────────────────────
    public static BattleManager instance;

    // ── Inspector references ──────────────────────────────────────────────
    [Header("Prefabs")]
    public GameObject playerAgentPrefab;   // AgentController + NavMeshAgent + CapsuleCollider
    public GameObject enemyAgentPrefab;    // EnemyController + NavMeshAgent + CapsuleCollider

    [Header("Spawn Points")]
    public Transform playerSpawnRoot;      // child Transforms = individual spawn positions
    public Transform enemySpawnRoot;
    public Transform retreatPoint;         // where RETREAT sends player agents

    [Header("Portrait Registry")]
    [Tooltip("ScriptableObject that maps each character model prefab to its pre-rendered portrait sprite.")]
    public CharacterPortraitRegistry portraitRegistry;

    [Header("Round Settings")]
    public float roundDuration  = 90f;     // seconds per match

    [Header("Max agents in field at once")]
    public int maxPlayerAgents = 12;

    [Header("Models")]
    public RuntimeAnimatorController characterAnimator;   // shared animator controller for all spawned models

    [Header("Roaming & Patrol Settings")]
    [Tooltip("Patrol/roaming radius for gang members around spawn points.")]
    public float gangPatrolRadius = 6f;

    [Tooltip("Player detection radius for gang members.")]
    public float gangDetectionRadius = 12f;

    [Tooltip("Patrol/roaming radius for police officers around spawn points.")]
    public float policePatrolRadius = 6f;

    [Tooltip("Player detection radius for police officers.")]
    public float policeDetectionRadius = 25f;

    [Header("Map")]
    [SerializeField] private MapController mapController;



    // ── Runtime state ─────────────────────────────────────────────────────
    public BattleMode   Mode          { get; private set; }
    public int          CurrentRound  { get; private set; } = 1;
    public float        TimeRemaining { get; private set; }
    public bool         BattleActive  { get; private set; }
    public bool         IsPaused      { get; private set; }

    // ── Session accumulation variables (Strategic Free-Roam) ─────────────
    public int sessionMoneyEarned     = 0;
    public int sessionReputationGained = 0;
    public int sessionFansRecruited   = 0;
    public int sessionHeatGained       = 0;

    private List<AgentController>  _playerAgents = new List<AgentController>();
    private List<EnemyController>  _enemyAgents  = new List<EnemyController>();

    private int _enemyCount;
    private int _enemyStrength;
    private int _battleReward;
    private string _enemyFirmName;

    // ── Events ────────────────────────────────────────────────────────────
    public event System.Action<int, int>             OnCountsChanged;   // (alivePlayer, aliveEnemy)
    public event System.Action<int, float>           OnRoundChanged;    // (roundNum, timeRemaining)
    public event System.Action<BattleResultData>     OnBattleComplete;  // full result data

    // ── Kill counters (accumulated during the battle) ─────────────────────
    private int _enemiesKilled     = 0;
    private int _playerAgentsKilled = 0;

    // ── Trip targets generated at battle start ────────────────────────────
    private BattleTripTarget[] _tripTargets;
    private bool _skipEndBattle; // level system owns defeat / campaign transitions

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        if(gameObject.scene.name=="Gameplay")
        {
            if(!GameData.instance)new GameObject("GameData").AddComponent<GameData>();
            if(!GameManager.instance)new GameObject("GameManager").AddComponent<GameManager>();
        }
    }

    void loadSpawnPoint()
    {
        Transform gangPos = GameObject.Find("GangPositionGameObject")?.transform;
        if (gangPos == null)
            gangPos = CreateGameplaySpawnAnchors();

        playerSpawnRoot = gangPos.GetChild(0);
        enemySpawnRoot = gangPos.GetChild(1);
        retreatPoint = gangPos.GetChild(0);
    }

    private Transform CreateGameplaySpawnAnchors()
    {
        Bounds bounds = FindSceneGameplayBounds();
        Vector3 center = bounds.center;
        float spread = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.22f, 18f, 60f);

        var root = new GameObject("GangPositionGameObject").transform;
        root.position = Vector3.zero;

        var player = new GameObject("PlayerSpawnRoot").transform;
        player.SetParent(root, false);
        player.position = FindPlayablePoint(center + new Vector3(-spread, 0f, -spread * 0.6f), bounds);
        player.LookAt(new Vector3(center.x, player.position.y, center.z));

        var enemies = new GameObject("EnemySpawnRoot").transform;
        enemies.SetParent(root, false);
        enemies.position = FindPlayablePoint(center + new Vector3(spread, 0f, spread * 0.6f), bounds);
        enemies.LookAt(new Vector3(center.x, enemies.position.y, center.z));

        Vector3[] offsets =
        {
            new Vector3(spread, 0f, spread * 0.6f),
            new Vector3(spread * 0.7f, 0f, -spread * 0.15f),
            new Vector3(spread * 0.1f, 0f, spread * 0.85f),
            new Vector3(-spread * 0.35f, 0f, spread * 0.55f),
            new Vector3(spread * 1.2f, 0f, -spread * 0.65f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            var point = new GameObject($"EnemySpawn_{i + 1:00}").transform;
            point.SetParent(enemies, false);
            point.position = FindPlayablePoint(center + offsets[i], bounds);
            point.rotation = enemies.rotation;
        }

        return root;
    }

    private static Bounds FindSceneGameplayBounds()
    {
        var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(r => r != null && r.gameObject.scene.IsValid() && r.bounds.size.sqrMagnitude > 1f);

        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(120f, 10f, 120f));
        foreach (var renderer in renderers)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            bounds = new Bounds(Vector3.zero, new Vector3(120f, 10f, 120f));

        return bounds;
    }

    private static Vector3 FindPlayablePoint(Vector3 preferred, Bounds bounds)
    {
        if (NavMesh.SamplePosition(preferred, out NavMeshHit navHit, 25f, NavMesh.AllAreas))
            return navHit.position;

        Vector3 rayStart = new Vector3(preferred.x, bounds.max.y + 25f, preferred.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, bounds.size.y + 75f))
            return hit.point;

        return new Vector3(preferred.x, bounds.min.y + 0.1f, preferred.z);
    }

    void Start()
    {
        _playerAgents.Clear();
        _enemyAgents.Clear();
        _enemiesKilled = 0;
        _playerAgentsKilled = 0;

        Mode           = GameManager.PendingBattleMode;
        _battleReward  = GameManager.PendingBattleReward;
        _enemyFirmName = GameManager.PendingEnemyFirmName;

        // Baseline rewards for the strategic free-roam session
        sessionMoneyEarned     = _battleReward;
        sessionReputationGained = 2; 
        // A resolved rival fight adds one clear, predictable four-bar heat spike.
        sessionHeatGained       = RivalFightHeatGain;
        sessionFansRecruited   = 0;

        StartCoroutine(StartGame());
    }

    public System.Action OnMapSpawnedIn;
    private IEnumerator StartGame()
    {
        yield return StartCoroutine(mapController.SpawnMap(GetMapIdForSelectedTrip()));
        OnMapSpawnedIn?.Invoke();
        _spawnGeometry = null;
        loadSpawnPoint();
        ApplyCityModeSpawnProfile();
        ClearTallBlockersNearGameplayAnchors();
        if(gameObject.scene.name=="Gameplay") CityGameplay.EnsureExists();

        // Subtle, clean lighting + solid city materials (no post FX).
        yield return StartCoroutine(CityAtmosphere.ApplyAfterMapReady());

        // Mobile readability: closer camera + larger HUD / minimap icons.
        GameplayReadability.ResetForNewBattle();
        GameplayReadability.Apply();
        BattleHudLayoutFix.EnsureExists();

        // Safety: destroy any leftover solid ZoneVolume meshes from older builds
        // that could fill the screen yellow/white when the camera pans into them.
        foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (mr == null) continue;
            string n = mr.gameObject.name;
            if (n == "ZoneVolume" || n == "LowReplacementBuilding" || n == "Vendor Counter" || n == "Striped Awning"
                || n == "Stadium Queue Rail" || n == "Matchday Approach Light" || n == "Banner Pole" || n == "Club Colour Flag"
                || n == "Market Counter" || n == "Market Awning" || n == "Market Crate" || n == "Park Seat"
                || n == "Community Table" || n == "Community Noticeboard" || n == "Transit Shelter" || n == "Queue Marker"
                || n == "Travel Bag" || n == "Food Stall" || n == "Stall Canopy" || n == "Street Performer Stage"
                || n == "Outdoor Pub Table" || n == "Pub Stool" || n == "Pub Activity Canopy"
                || n == "Arrival Bollard" || n == "Arrival Cargo" || n == "Arrival Shelter")
                Destroy(mr.gameObject);
        }

        SpawnAmbientControlPoints();

        // Map is loaded — let the loading screen finish and fade into the intro.
        SceneTransitionManager.Instance?.NotifyBattleReady();

        if (Mode == BattleMode.PoliceRaid)
        {
            StartCoroutine(StartPoliceRaidRoutine());
        }
        else
        {
            // ── Rival Fight: normal 3D battle ─────────────────────────────────
            _enemyCount    = GameManager.PendingEnemyCount;
            _enemyStrength = GameManager.PendingEnemyStrength;
            // Recruitment Center is spawned after gangs (non-overlapping) inside SpawnRivalAgentsInternal.
            StartCoroutine(RunBattle());
        }
    }

    /// <summary>
    /// Extra A/B/C recruitment pads are disabled. City recruitment stays on the
    /// single RECRUITMENT district location.
    /// </summary>
    private void SpawnRecruitAreas()
    {
        SpawnRecruitAreas(null);
    }

    private void SpawnRecruitAreas(System.Collections.Generic.List<Vector3> avoidCenters)
    {
        foreach (var old in FindObjectsByType<RecruitArea>(FindObjectsSortMode.None))
            if (old != null) Destroy(old.gameObject);
    }

    private void SpawnAmbientControlPoints()
    {
        var existing = FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None);
        if (existing != null && existing.Length > 0) return;

        if (enemySpawnRoot == null) return;
        
        int count = Mathf.Min(3, enemySpawnRoot.childCount);
        string[] names = GetControlPointNames();
        var bots = GameData.instance?.PlayerData?.RivalBots;
        
        for (int i = 0; i < count; i++)
        {
            Transform spawnPoint = enemySpawnRoot.GetChild(i);
            if (spawnPoint == null) continue;

            Vector3 pos = spawnPoint.position + spawnPoint.forward * 5f;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            GameObject go = new GameObject(names[i % names.Length]);
            go.transform.position = pos;
            var cp = go.AddComponent<TerritoryControlPoint>();
            cp.zoneName = names[i % names.Length];
            cp.detectionRadius = 8f;
            cp.moneyReward = 1500 + i * 500;
            cp.reputationReward = 10 + i * 2;
            cp.heatGained = 2;

            // Link the control point to the local gang patrolling this coordinate!
            if (bots != null && bots.Count > 0)
            {
                cp.owningFaction = bots[i % bots.Count].firmName;
            }
            else
            {
                cp.owningFaction = _enemyFirmName;
            }
            
            Debug.Log($"[BattleManager] Spawned ambient control point: {cp.zoneName} (Owned by {cp.owningFaction}) at {pos}");
        }
    }

    private MapController.MapId GetMapIdForSelectedTrip()
    {
        string selectedDest = GameData.instance?.PlayerData?.LastSelectedDestination;
        
        return selectedDest switch
        {
            "East Docks" => MapController.MapId.Map,
            "North End"  => MapController.MapId.Map1,
            "Riverside"  => MapController.MapId.Map2,
            "Old Town"   => MapController.MapId.Map3,
            _            => MapController.MapId.Map // Fallback default
        };
    }

    private string[] GetControlPointNames()
    {
        if (CityGameplay.HomeMode)
            return new[] { "Local Pub", "Training Yard", "Stadium Approach" };

        string dest = GameData.instance?.PlayerData?.LastSelectedDestination ?? "";
        return dest switch
        {
            "North End" => new[] { "North End Pub", "Market Cut", "Rail Arches" },
            "Riverside" => new[] { "Riverside Pub", "Boatyard", "Bridge Route" },
            "Old Town" => new[] { "Crown Pub", "Town Square", "Cathedral Lane" },
            _ => new[] { "East Docks Pub", "Warehouse Row", "Container Yard" },
        };
    }

    private void ApplyCityModeSpawnProfile()
    {
        if (playerSpawnRoot == null || enemySpawnRoot == null) return;

        string dest = GameData.instance?.PlayerData?.LastSelectedDestination ?? "";
        Vector3 playerTarget = CityGameplay.HomeMode ? new Vector3(120, 0, 90) : GetAwayPlayerSpawn(dest);
        Vector3 enemyTarget = CityGameplay.HomeMode ? new Vector3(195, 0, 60) : GetAwayEnemySpawn(dest);

        playerSpawnRoot.position = FindOpenReachableSpawn(GetAwaySpawnCandidates(dest, true, playerTarget), playerSpawnRoot.position);
        enemySpawnRoot.position = FindOpenReachableSpawn(GetAwaySpawnCandidates(dest, false, enemyTarget), enemySpawnRoot.position);

        Vector3 facing = enemySpawnRoot.position - playerSpawnRoot.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.01f)
        {
            playerSpawnRoot.forward = facing.normalized;
            enemySpawnRoot.forward = -facing.normalized;
        }

        Vector3[] enemyOffsets = GetAwayEnemyOffsets(dest);
        for (int i = 0; i < enemySpawnRoot.childCount; i++)
        {
            Transform child = enemySpawnRoot.GetChild(i);
            Vector3 target = enemySpawnRoot.position + enemyOffsets[i % enemyOffsets.Length];
            child.position = FindOpenReachableSpawn(target, enemySpawnRoot.position);
            child.rotation = enemySpawnRoot.rotation;
        }
    }

    private static Vector3 GetAwayPlayerSpawn(string destination)
    {
        return destination switch
        {
            "North End" => new Vector3(50, 0, 55),
            "Riverside" => new Vector3(118, 0, 218),
            "Old Town" => new Vector3(-130, 0, 88),
            "East Docks" => new Vector3(120, 0, -168),
            _ => new Vector3(120, 0, -168),
        };
    }

    private static Vector3 GetAwayEnemySpawn(string destination)
    {
        return destination switch
        {
            "North End" => new Vector3(88, 0, 116),
            "Riverside" => new Vector3(170, 0, 178),
            "Old Town" => new Vector3(-76, 0, 128),
            "East Docks" => new Vector3(238, 0, -156),
            _ => new Vector3(238, 0, -156),
        };
    }

    private static Vector3[] GetAwaySpawnCandidates(string destination, bool playerSide, Vector3 primary)
    {
        if (CityGameplay.HomeMode || destination != "East Docks")
            return new[] { primary };

        return playerSide
            ? new[]
            {
                primary,
                new Vector3(96, 0, -184),
                new Vector3(132, 0, -208),
                new Vector3(78, 0, -142),
                new Vector3(154, 0, -170),
                new Vector3(110, 0, -118)
            }
            : new[]
            {
                primary,
                new Vector3(260, 0, -176),
                new Vector3(224, 0, -204),
                new Vector3(286, 0, -124),
                new Vector3(212, 0, -122),
                new Vector3(248, 0, -84)
            };
    }

    private static Vector3[] GetAwayEnemyOffsets(string destination)
    {
        if (CityGameplay.HomeMode)
            return new[] { new Vector3(0, 0, 0), new Vector3(24, 0, 22), new Vector3(-18, 0, 24), new Vector3(42, 0, -10), new Vector3(-36, 0, -18) };
        return destination switch
        {
            "North End" => new[] { new Vector3(0, 0, 0), new Vector3(34, 0, -12), new Vector3(-28, 0, 20), new Vector3(10, 0, 46), new Vector3(48, 0, 38) },
            "Riverside" => new[] { new Vector3(0, 0, 0), new Vector3(30, 0, 18), new Vector3(-24, 0, 26), new Vector3(42, 0, -28), new Vector3(-38, 0, -16) },
            "Old Town" => new[] { new Vector3(0, 0, 0), new Vector3(26, 0, 30), new Vector3(-32, 0, 12), new Vector3(16, 0, -42), new Vector3(-46, 0, -24) },
            _ => new[] { new Vector3(0, 0, 0), new Vector3(34, 0, 22), new Vector3(-30, 0, 30), new Vector3(22, 0, -34), new Vector3(-42, 0, -20) },
        };
    }

    private static Vector3 FindOpenReachableSpawn(Vector3 preferred, Vector3 fallback)
    {
        if (TryFindOpenNavPoint(preferred, fallback, out Vector3 open))
            return open;
        return FindReachableSpawn(preferred, fallback);
    }

    private static Vector3 FindOpenReachableSpawn(Vector3[] preferredCandidates, Vector3 fallback)
    {
        if (preferredCandidates != null)
        {
            foreach (Vector3 candidate in preferredCandidates)
                if (TryFindOpenNavPoint(candidate, fallback, out Vector3 open))
                    return open;
        }
        return preferredCandidates != null && preferredCandidates.Length > 0
            ? FindOpenReachableSpawn(preferredCandidates[0], fallback)
            : FindReachableSpawn(fallback, fallback);
    }

    private static bool TryFindOpenNavPoint(Vector3 preferred, Vector3 origin, out Vector3 result)
    {
        result = preferred;
        var path = new NavMeshPath();

        bool Accept(Vector3 sample, out Vector3 accepted)
        {
            accepted = sample;
            if (!NavMesh.SamplePosition(sample, out var hit, 18f, NavMesh.AllAreas)) return false;
            if (!IsOpenGameplayPoint(hit.position, 8f)) return false;

            if (NavMesh.SamplePosition(origin, out var originHit, 24f, NavMesh.AllAreas) &&
                (!NavMesh.CalculatePath(originHit.position, hit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete))
                return false;

            accepted = hit.position;
            return true;
        }

        if (Accept(preferred, out result)) return true;
        for (int ring = 1; ring <= 18; ring++)
        {
            float radius = ring * 9f;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                Vector3 sample = preferred + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (Accept(sample, out result)) return true;
            }
        }
        return false;
    }

    private static Vector3 FindReachableSpawn(Vector3 preferred, Vector3 fallback)
    {
        if (NavMesh.SamplePosition(preferred, out var hit, 24f, NavMesh.AllAreas))
            return hit.position;
        if (NavMesh.SamplePosition(fallback, out hit, 24f, NavMesh.AllAreas))
            return hit.position;
        return preferred;
    }

    private void ClearTallBlockersNearGameplayAnchors()
    {
        var anchors = new List<Vector3>();
        if (playerSpawnRoot != null) anchors.Add(playerSpawnRoot.position);
        if (enemySpawnRoot != null)
        {
            anchors.Add(enemySpawnRoot.position);
            for (int i = 0; i < enemySpawnRoot.childCount; i++)
                anchors.Add(enemySpawnRoot.GetChild(i).position);
        }

        if (anchors.Count == 0) return;
        foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsGameplayBlocker(mr)) continue;
            foreach (Vector3 anchor in anchors)
            {
                if (HorizontalDistanceToBounds(mr.bounds, anchor) > 18f) continue;
                mr.enabled = false;
                foreach (var col in mr.GetComponentsInChildren<Collider>())
                    if (col != null) col.enabled = false;
                break;
            }
        }
    }

    private static Renderer[] _spawnGeometry;
    private static bool IsOpenGameplayPoint(Vector3 point, float clearRadius)
    {
        if (Physics.Raycast(point + Vector3.up * 1.2f, Vector3.up, out var overhead, 80f, ~0, QueryTriggerInteraction.Ignore))
        {
            string n = overhead.transform.name.ToLowerInvariant();
            if (!n.Contains("agent") && !n.Contains("zone"))
                return false;
        }

        if (_spawnGeometry == null) _spawnGeometry = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var renderer in _spawnGeometry)
        {
            if (!IsGameplayBlocker(renderer)) continue;
            if (HorizontalDistanceToBounds(renderer.bounds, point) <= clearRadius)
                return false;
        }
        return true;
    }

    private static bool IsGameplayBlocker(Renderer renderer)
    {
        if (renderer == null || renderer is SkinnedMeshRenderer) return false;
        var go = renderer.gameObject;
        if (go == null || !go.activeInHierarchy) return false;
        string n = go.name.ToLowerInvariant();
        if (n.Contains("road") || n.Contains("street") || n.Contains("ground") ||
            n.Contains("floor") || n.Contains("pavement") || n.Contains("sidewalk") ||
            n.Contains("grass") || n.Contains("water") || n.Contains("pitch") ||
            n.Contains("court") || n.Contains("zone") || n.Contains("agent"))
            return false;

        Bounds b = renderer.bounds;
        float horizontal = Mathf.Max(b.size.x, b.size.z);
        return b.size.y >= 7f && horizontal >= 5f;
    }

    private static float HorizontalDistanceToBounds(Bounds bounds, Vector3 point)
    {
        float x = Mathf.Max(bounds.min.x - point.x, 0f, point.x - bounds.max.x);
        float z = Mathf.Max(bounds.min.z - point.z, 0f, point.z - bounds.max.z);
        return Mathf.Sqrt(x * x + z * z);
    }

    // ── Police Raid setup coroutine ───────────────────────────────────────
    // Separated so we can yield on PreBattleSequencer before the panel shows.

    private IEnumerator StartPoliceRaidRoutine()
    {
        // 1. Pull squad params from PoliceDataRegistry
        int heat = GameData.instance?.PlayerData?.PoliceHeat ?? 10;
        (int policeCount, int policeStrength) = PoliceManager.instance != null
            ? PoliceManager.instance.GetPoliceParams(heat)
            : (10, 28);

        _enemyCount    = policeCount;
        _enemyStrength = policeStrength;
        _enemyFirmName = "POLICE";

        // 2. Enable police environment props (vans, barriers, lights…)
        PoliceManager.instance?.SetupRaid(true);

        // 3. Spawn 3D agents — locked in cinematic idle for the intro
        SpawnAllAgents();
        SetAllAgentsCinematicIdle(true);

        // 4. Pre-battle cinematic intro (reads PendingBattleMode == PoliceRaid)
        //    PreBattleSequencer already shows "POLICE RAID", police sub-text,
        //    "POLICE INBOUND" warning, and hype lines automatically.
        if (PreBattleSequencer.instance != null)
            yield return StartCoroutine(PreBattleSequencer.instance.PlaySequence());

        // 5. Release agents — they fight in 3D while the UI panel runs
        SetAllAgentsCinematicIdle(false);

        // 6. Activate the raid UI overlay (reads PoliceDataRegistry internally)
        var raidPanel = PoliceRaidGameplayController.FindInScene();
        if (raidPanel != null)
        {
            raidPanel.Show();
            Debug.Log($"[BattleManager] PoliceRaid — intro done, {policeCount} officers fighting, UI panel active.");
        }
        else
        {
            Debug.LogError("[BattleManager] PoliceRaidGameplayController not found in scene! Falling back to normal battle.");
            StartCoroutine(RunBattle());
        }
    }


    // ── Battle flow ───────────────────────────────────────────────────────

    private IEnumerator RunBattle()
    {
        // Generate targets before the battle begins
        _tripTargets = BuildTripTargets();

        // ── Spawn agents BEFORE the intro so both gangs are visible on field ──
        // They are immediately locked in cinematic idle: standing still,
        // playing the idle animation, no combat or NavMesh movement.
        SpawnAllAgents();
        SetAllAgentsCinematicIdle(true);

        // ── Pre-battle cinematic intro ──────────────────────────────────────
        // Camera pans across both teams, shows names/crests, hype prompts,
        // and the enemy-advancing warning. Battle timer does NOT run here.
        if (PreBattleSequencer.instance != null)
            yield return StartCoroutine(PreBattleSequencer.instance.PlaySequence());

        // ── Release agents — battle is about to start ───────────────────────
        SetAllAgentsCinematicIdle(false);

        // Re-apply readability after intro (camera is live, icons are registered).
        GameplayReadability.ResetForNewBattle();
        GameplayReadability.Apply();
        LevelSystem.Instance?.ApplyLargeHudScale();
        // Live realtime minimap (replaces the old white-dot map + legend).
        LiveMiniMap.EnsureExists();
        BattleHudLayoutFix.EnsureExists();

        // ── HUD ────────────────────────────────────────────────────────────
        BattleUIController.instance?.SetupHUD(Mode);
        BattleUIController.instance?.ShowHUDWithFade(0.5f);   // fades the HUD in
        BattleUIController.instance?.ShowTripTargets(_tripTargets);
        BroadcastCounts();

        // Tell the environment FX to drop back to normal in-battle emission
        BattleEnvironmentFX.instance?.OnBattleStarted();

        yield return new WaitForSeconds(1f); // brief "ready" pause

        BattleActive = true;
        TimeRemaining = 0f;

        // Aggressive live police pressure for rival fights (heat rises while fighting,
        // then a squad arrives with a vignette + cutscene + fight/bribe choice).
        LivePoliceSystem.EnsureExists();

        while (BattleActive)
        {
            if (!IsPaused)
            {
                TimeRemaining += Time.deltaTime;
                OnRoundChanged?.Invoke(1, TimeRemaining); // display elapsed time in HUD
                OnCountsChanged?.Invoke(AlivePlayerCount(), AliveEnemyCount());

                if (AlivePlayerCount() == 0)
                {
                    Debug.Log("[BattleManager] Player squad fully wiped — defeat popup.");
                    BattleActive = false;
                    _skipEndBattle = true;
                    LevelSystem.Instance?.OnPlayerDefeated();
                }
            }
            yield return null;
        }

        if (_skipEndBattle)
        {
            Debug.Log("[BattleManager] EndBattle skipped — level system handles the outcome.");
            yield break;
        }

        Debug.Log("[BattleManager] Free roam session complete — calculating results.");
        EndBattle();
    }




    private void EndBattle()
    {
        int aliveP = AlivePlayerCount();
        int aliveE = AliveEnemyCount();

        // ── Determine match result from survival ────────────────────────────
        GameData.BattleResult result = (aliveP == 0) ? GameData.BattleResult.Defeat : GameData.BattleResult.Victory;

        Debug.Log($"[BattleManager] Match result: {result} (Alive player units: {aliveP})");

        // ── Sync agent HP and split alive vs dead ───────────────────────────────
        // First pass: snapshot current HP into AgentData for every agent
        foreach (var a in _playerAgents)
            a.SyncHpToData();

        // Build two separate lists so GameData can handle them correctly
        var survivingAgents = new System.Collections.Generic.List<AgentData>();
        var deadAgents      = new System.Collections.Generic.List<AgentData>();
        foreach (var a in _playerAgents)
        {
            if (a.Data == null) continue;
            if (a.IsAlive) survivingAgents.Add(a.Data);
            else           deadAgents.Add(a.Data);
        }

        // ── Calculate rewards (from session gains) ──────────────────
        int heatGain = sessionHeatGained;

        // Reputation scales with rounds won: base ±2, +1 per extra round won
        int repGain = (result == GameData.BattleResult.Victory)
            ? sessionReputationGained
            : -5;

        int moneyNet = (result == GameData.BattleResult.Victory)
            ? sessionMoneyEarned
            : -(sessionMoneyEarned / 2);

        // Fans gained on victory — proportional to the opponent's actual fan count.
        // You earn roughly 20–40% of whatever fans the rival has, so small gangs
        // give tiny rewards and larger gangs give bigger (but still capped) rewards.
        // This encourages the player to fight bigger rivals for better fan gains.
        int fansGained = 0;
        if (result == GameData.BattleResult.Victory)
        {
            int opponentFans = 1;   // fallback if no bot data found
            if (!string.IsNullOrEmpty(_enemyFirmName) && GameData.instance?.PlayerData?.RivalBots != null)
            {
                var opBot = GameData.instance.PlayerData.RivalBots
                    .Find(b => b.firmName.Equals(_enemyFirmName, System.StringComparison.OrdinalIgnoreCase));
                if (opBot != null)
                    opponentFans = Mathf.Max(1, opBot.fans);
            }

            // 20–40% of opponent fans, always at least 1, never more than they have
            float pct = UnityEngine.Random.Range(0.20f, 0.40f);
            fansGained = Mathf.Clamp(Mathf.RoundToInt(opponentFans * pct), 1, opponentFans);
        }

        // ── Persist everything to GameData / SaveDataInLocal ─────────────────
        GameData.instance?.OnBattleComplete(
            result, _battleReward, heatGain, repGain,
            survivingAgents, deadAgents, fansGained, _enemyFirmName);

        // Street campaign session ends on a resolved match — next trip starts fresh snapshot.
        if (result == GameData.BattleResult.Victory)
            GameData.instance?.ClearBattleSessionSnapshot();


        // Build result data struct for the result screen
        var d = GameData.instance?.PlayerData;

        // Evaluate trip targets against actual battle outcomes
        EvaluateTripTargets(result, _enemiesKilled, _playerAgentsKilled, aliveP, aliveE);

        var resultData = new BattleResultData
        {
            Result              = result,
            PlayerFirmName      = d?.FirmName    ?? "YOUR FIRM",
            PlayerFirmSubName   = "HOOLIGANS",
            EnemyFirmName       = !string.IsNullOrEmpty(_enemyFirmName)
                                    ? _enemyFirmName.ToUpper()
                                    : (d?.RivalClubName != null ? d.RivalClubName.Replace(" FC", "").ToUpper() : "RIVAL FIRM"),
            EnemyFirmSubName    = Mode == BattleMode.PoliceRaid ? "POLICE" : "MOB",
            PlayerAgentsAlive   = aliveP,
            EnemyAgentsAlive    = aliveE,
            PlayerRoundsWon     = 1,
            EnemyRoundsWon      = 0,
            TotalRounds         = 1,
            EnemiesDefeated     = _enemiesKilled,
            UnitsLost           = _playerAgentsKilled,
            MoneyEarned         = moneyNet,
            ReputationGained    = repGain,
            FansGained          = fansGained,
            PoliceHeatChange    = heatGain,
            TripTargets         = _tripTargets,
            DestinationName     = d?.LastSelectedDestination ?? "",
        };


        // Notify subscribers (BattleUIController listens for HUD hide)
        OnBattleComplete?.Invoke(resultData);

        // Show dedicated result screen
        BattleUIController.instance?.resultPanel.Show(resultData);
        // NOTE: Police Raid chaining is now handled by PoliceRaidGameplayController
        // in the Gameplay scene. Battle flow only ever runs rival fights here.
    }

    // ── Spawning ──────────────────────────────────────────────────────────

    private void SpawnAllAgents()
    {
        SpawnPlayerAgents();
        SpawnEnemyAgents();
        BroadcastCounts();
    }

    /// <summary>
    /// Locks or unlocks all currently spawned agents in cinematic idle mode.
    /// Called before the intro sequence (lock) and again after it ends (unlock).
    /// </summary>
    public void SetAllAgentsCinematicIdle(bool locked)
    {
        foreach (var a in _playerAgents)
            if (a != null) a.SetCinematicIdle(locked);

        foreach (var e in _enemyAgents)
            if (e != null) e.SetCinematicIdle(locked);
    }

    private void SpawnPlayerAgents()
    {
        var roster = GameData.instance?.PlayerData?.RecruitedAgents;
        if (roster == null) return;

        int spawnIdx = 0;
        foreach (var data in roster)
        {
            if (!data.IsAlive) continue;
            if (!CityGameplay.HomeMode && !IsSelectedForAwayTrip(data)) continue;
            if (spawnIdx >= maxPlayerAgents) break;

            Vector3 pos = GetSpawnPosition(playerSpawnRoot, spawnIdx);
            pos = FindOpenReachableSpawn(pos, playerSpawnRoot.position);
            var go      = Instantiate(playerAgentPrefab, pos, Quaternion.identity);
            var ac      = go.GetComponent<AgentController>();

            Vector3 retreat = retreatPoint != null
                ? retreatPoint.position
                : pos - Vector3.right * 3f;

            CharacterPortraitRegistry playerVisuals = GameManager.IsPolicePlayer
                ? PoliceManager.instance?.policeRegistry?.officerTypes
                : null;
            ac?.Initialise(data, retreat, playerVisuals);

            ac.transform.forward = playerSpawnRoot.forward;

            _playerAgents.Add(ac);
            spawnIdx++;
        }

        // Select all spawned units by default so player can click/tap to move them immediately
        AgentSelectionManager.instance?.SelectAll();

        // Snapshot roster once per battle session (leave keeps recruits; Try Again restores).
        GameData.instance?.EnsureBattleSessionSnapshot();
    }

    private bool IsSelectedForAwayTrip(AgentData data)
    {
        var d = GameData.instance?.PlayerData;
        if (data == null || d == null) return false;
        if (d.SelectedAwayAgentIds == null)
            return true;
        if (d.SelectedAwayAgentIds.Count == 0)
            return false;
        return d.SelectedAwayAgentIds.Contains(data.AgentId);
    }

    private void SpawnEnemyAgents()
    {
        if (Mode == BattleMode.PoliceRaid)
        {
            SpawnPoliceAgents();
        }
        else
        {
            SpawnRivalAgents();
        }
    }

    // ── Police-specific spawning (uses PoliceDataRegistry archetypes) ─────

    private void SpawnPoliceAgents()
    {
        PoliceDataRegistry registry = PoliceManager.instance?.policeRegistry;
        float hpMultiplier = registry != null ? registry.hpMultiplier : 1f;

        float enemyHp    = 40f + registry.baseStrengthValue * 0.5f;
        float enemyDmg   = 6f  + registry.baseStrengthValue * 0.2f;
        float enemySpeed = 2.2f;
        float attackRange = 1.0f;

        int spawnPointsCount = enemySpawnRoot != null ? enemySpawnRoot.childCount : 0;

        // Spawn Police Car alongside police agents
        if (PoliceManager.instance != null && PoliceManager.instance.spawnPoliceCar && PoliceManager.instance.policeCarPrefab != null)
        {
            if (spawnPointsCount > 0)
            {
                for (int p = 0; p < spawnPointsCount; p++)
                {
                    Transform pt = enemySpawnRoot.GetChild(p);
                    Vector3 carPos = pt.TransformPoint(PoliceManager.instance.policeCarOffset);
                    Instantiate(PoliceManager.instance.policeCarPrefab, carPos, pt.rotation);
                }
            }
            else if (enemySpawnRoot != null)
            {
                Vector3 carPos = enemySpawnRoot.TransformPoint(PoliceManager.instance.policeCarOffset);
                Instantiate(PoliceManager.instance.policeCarPrefab, carPos, enemySpawnRoot.rotation);
            }
        }

        int spawnIdx = 0;
        for (int i = 0; i < _enemyCount; i++)
        {
            if (spawnIdx >= maxPlayerAgents) break;
            spawnIdx++;

            Transform spawnPoint = enemySpawnRoot;
            if (enemySpawnRoot != null && spawnPointsCount > 0)
            {
                int pointIndex = i % spawnPointsCount;
                spawnPoint = enemySpawnRoot.GetChild(pointIndex);
            }

            Vector3 pos = spawnPoint.position + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            GameObject prefabToUse = (PoliceManager.instance != null && PoliceManager.instance.policePrefab != null) 
                ? PoliceManager.instance.policePrefab 
                : enemyAgentPrefab;

            var go      = Instantiate(prefabToUse, pos, Quaternion.identity);
            var ec      = go.GetComponent<EnemyController>();
            if (ec != null)
            {
                ec.patrolRadius = policePatrolRadius;
                ec.detectionRadius = policeDetectionRadius;
                ec.Initialise(enemyHp, enemyDmg, enemySpeed, attackRange, registry.officerTypes);
                ec.firmName = "POLICE";
                ec.isHostile = false;
                ec.transform.forward = spawnPoint != null ? spawnPoint.forward : Vector3.forward;
                _enemyAgents.Add(ec);
            }
        }
    }


    // ── Rival fight spawning — one full gang group per patrol node ─────────

    private void SpawnRivalAgents()
    {
        LevelSystem.EnsureExists();
        int gangsWanted = LevelSystem.Instance != null ? LevelSystem.Instance.GangsRequired : 2;
        float hpMul = LevelSystem.Instance != null ? LevelSystem.Instance.EnemyHealthMultiplier : .85f;
        float dmgMul = LevelSystem.Instance != null ? LevelSystem.Instance.EnemyDamageMultiplier : .75f;
        SpawnRivalAgentsInternal(gangsWanted, hpMul, dmgMul, startNodeOffset: 0);
    }

    /// <summary>
    /// Clears existing rival gangs and spawns a fresh set for the given level,
    /// starting at a different node offset so they appear in new areas.
    /// </summary>
    public void RespawnLevelGangs(int level, float hpMul, float dmgMul, int gangsWanted)
    {
        // Remove existing non-police enemies + their turf markers.
        for (int i = _enemyAgents.Count - 1; i >= 0; i--)
        {
            var e = _enemyAgents[i];
            if (e == null) { _enemyAgents.RemoveAt(i); continue; }
            if (e.firmName == "POLICE") continue;
            Destroy(e.gameObject);
            _enemyAgents.RemoveAt(i);
        }
        foreach (var go in FindObjectsByType<GangArea>(FindObjectsSortMode.None))
            if (go != null) Destroy(go.gameObject);

        int offset = (level - 1) % Mathf.Max(1, enemySpawnRoot != null ? enemySpawnRoot.childCount : 1);
        SpawnRivalAgentsInternal(gangsWanted, hpMul, dmgMul, startNodeOffset: offset);
        // Refresh recruit centers so level unlocks more spots.
        var avoid = new System.Collections.Generic.List<Vector3>();
        foreach (var g in FindObjectsByType<GangArea>(FindObjectsSortMode.None))
            if (g != null) avoid.Add(g.transform.position);
        SpawnRecruitAreas(avoid);
        BroadcastCounts();
    }

    /// <summary>Buff all alive player agents (used by the level-up power-boost popup).</summary>
    public void ApplyPlayerPowerBoost(float hpMul, float strMul)
    {
        foreach (var a in _playerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            a.ApplyPowerBoost(hpMul, strMul);
        }
        Debug.Log($"[BattleManager] Player power boost applied (HP x{hpMul}, STR x{strMul}).");
    }

    private void SpawnRivalAgentsInternal(int gangsWanted, float hpMul, float dmgMul, int startNodeOffset)
    {
        if (enemySpawnRoot == null)
        {
            Debug.LogError("[BattleManager] enemySpawnRoot is NULL — no gangs will spawn.");
            return;
        }

        int nodeCount = enemySpawnRoot.childCount;
        if (nodeCount == 0)
        {
            Debug.LogError("[BattleManager] enemySpawnRoot has 0 children — no patrol nodes.");
            return;
        }

        var bots = GameData.instance?.PlayerData?.RivalBots;
        int gangsToSpawn = Mathf.Clamp(gangsWanted, 1, Mathf.Max(nodeCount, gangsWanted));
        int membersPerGang = 3; // every firm fields at least 3 lads


        // Keep turf pads from overlapping (zone size ≈ radius * 1.7).
        float minSeparation = Mathf.Max(26f, gangPatrolRadius * 3.8f);
        Vector3 playerPos = playerSpawnRoot != null ? playerSpawnRoot.position : Vector3.zero;
        var usedCenters = new System.Collections.Generic.List<Vector3>();

        Debug.Log($"[BattleManager] Level spawn: {gangsToSpawn} gang(s), minSep={minSeparation:0.0}m, HP×{hpMul:0.00}");

        for (int g = 0; g < gangsToSpawn; g++)
        {
            if (!TryPickSeparatedSpawn(usedCenters, playerPos, minSeparation, startNodeOffset + g, out Vector3 center, out Vector3 facing))
            {
                Debug.LogWarning($"[BattleManager] Could not find separated spawn for gang {g + 1} — skipping.");
                continue;
            }
            usedCenters.Add(center);

            BotData bot = (bots != null && bots.Count > 0)
                ? bots[g % bots.Count]
                : null;

            // Always unique firm ids so LevelSystem can count 1/2, 2/2 correctly.
            string baseName = bot != null && !string.IsNullOrEmpty(bot.firmName)
                ? bot.firmName
                : (!string.IsNullOrEmpty(_enemyFirmName) ? _enemyFirmName : "RIVAL FIRM");
            string gangName = $"{baseName} #{g + 1}";

            float gangHp  = (40f + (bot != null ? bot.strength : _enemyStrength) * 0.5f) * hpMul;
            float gangDmg = (6f  + (bot != null ? bot.strength : _enemyStrength) * 0.2f) * dmgMul;
            float gangSpeed = 2.2f;
            Color gangColor = Color.red;
            bool hasColor = bot != null && ColorUtility.TryParseHtmlString(bot.primaryColor, out gangColor);
            if (!hasColor)
                gangColor = GangPalette[g % GangPalette.Length];
            gangColor = ShiftHue(gangColor, g * 0.12f);

            var gangGroup = new System.Collections.Generic.List<EnemyController>();

            for (int m = 0; m < membersPerGang; m++)
            {
                float spreadAngle = m * (360f / membersPerGang) * Mathf.Deg2Rad;
                Vector3 spread = new Vector3(Mathf.Cos(spreadAngle) * 1.2f, 0, Mathf.Sin(spreadAngle) * 1.2f);
                Vector3 pos = center + spread;

                if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
                    pos = hit.position;

                var go = Instantiate(enemyAgentPrefab, pos, Quaternion.identity);
                var ec = go.GetComponent<EnemyController>();
                if (ec == null) continue;

                ec.patrolRadius = gangPatrolRadius;
                ec.detectionRadius = gangDetectionRadius;
                ec.Initialise(gangHp, gangDmg, gangSpeed, attackRange: 1.0f);
                ec.firmName = gangName;
                ec.isHostile = false;
                ec.transform.forward = facing.sqrMagnitude > 0.01f ? facing : Vector3.forward;

                ec.primaryColor = gangColor;
                if (ec.healthBar != null)
                {
                    ec.healthBar.enemyColor = gangColor;
                    ec.healthBar.SetHealth(ec.CurrentHp, ec.MaxHp);
                }

                _enemyAgents.Add(ec);
                gangGroup.Add(ec);
            }

            foreach (var member in gangGroup)
                member.SetGangCenter(center);

            GameObject areaGo = new GameObject("GangRing_" + gangName);
            var gangArea = areaGo.AddComponent<GangArea>();
            gangArea.Setup(gangName, center, gangPatrolRadius, gangColor);
        }

        CityZoneClearance.Resolve();
        // Recruitment Center after gangs so it can avoid their turf.
        SpawnRecruitAreas(usedCenters);
    }

    /// <summary>
    /// Picks a NavMesh point far from the player and already-placed gangs.
    /// Prefers enemy spawn nodes, then random NavMesh rings.
    /// </summary>
    private bool TryPickSeparatedSpawn(
        System.Collections.Generic.List<Vector3> used,
        Vector3 playerPos,
        float minSep,
        int preferNode,
        out Vector3 center,
        out Vector3 facing)
    {
        center = Vector3.zero;
        facing = Vector3.forward;
        float minSepSqr = minSep * minSep;
        float playerSepSqr = (minSep * 0.85f) * (minSep * 0.85f);

        bool FarEnough(Vector3 p)
        {
            if ((p - playerPos).sqrMagnitude < playerSepSqr) return false;
            foreach (var u in used)
                if ((p - u).sqrMagnitude < minSepSqr) return false;
            if (CityZoneClearance.OverlapsExisting(p, gangPatrolRadius)) return false;
            return true;
        }

        // Pass 1: authored enemy nodes, starting at preferNode, then shuffled.
        if (enemySpawnRoot != null && enemySpawnRoot.childCount > 0)
        {
            int n = enemySpawnRoot.childCount;
            for (int i = 0; i < n; i++)
            {
                Transform node = enemySpawnRoot.GetChild((preferNode + i) % n);
                if (node == null) continue;
                Vector3 p = node.position;
                if (UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                    p = hit.position;
                if (!FarEnough(p)) continue;
                if (!IsOpenGameplayPoint(p, 9f)) continue;
                center = p;
                facing = node.forward;
                return true;
            }
        }

        // Pass 2: random NavMesh samples on expanding rings.
        for (int attempt = 0; attempt < 48; attempt++)
        {
            float radius = minSep + attempt * 4f;
            Vector2 ring = Random.insideUnitCircle.normalized * radius;
            Vector3 guess = playerPos + new Vector3(ring.x, 0f, ring.y);
            if (!UnityEngine.AI.NavMesh.SamplePosition(guess, out var hit, 14f, UnityEngine.AI.NavMesh.AllAreas))
                continue;
            if (!FarEnough(hit.position)) continue;
            if (!IsOpenGameplayPoint(hit.position, 9f)) continue;
            center = hit.position;
            facing = (playerPos - center).normalized;
            facing.y = 0f;
            return true;
        }

        return false;
    }

    private static readonly Color[] GangPalette =
    {
        new Color(0.90f, 0.15f, 0.18f), // red
        new Color(0.15f, 0.45f, 0.95f), // blue
        new Color(0.95f, 0.55f, 0.10f), // orange
        new Color(0.55f, 0.20f, 0.85f), // purple
        new Color(0.10f, 0.75f, 0.55f), // teal
        new Color(0.90f, 0.20f, 0.55f), // magenta
    };

    private static Color ShiftHue(Color c, float amount)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h = (h + amount) % 1f;
        if (h < 0f) h += 1f;
        s = Mathf.Clamp01(Mathf.Max(s, 0.65f));
        v = Mathf.Clamp01(Mathf.Max(v, 0.75f));
        return Color.HSVToRGB(h, s, v);
    }

    // ── Safe Zone / HQ Escape triggers ───────────────────────────────────

    public bool CheckEscapeCondition()
    {
        if (retreatPoint == null) return false;
        
        int aliveCount = 0;
        int nearHqCount = 0;
        foreach (var a in _playerAgents)
        {
            if (a != null && a.IsAlive)
            {
                aliveCount++;
                if (Vector3.Distance(a.transform.position, retreatPoint.position) < 8f)
                {
                    nearHqCount++;
                }
            }
        }
        return aliveCount > 0 && nearHqCount == aliveCount;
    }

    public void TriggerEscape()
    {
        if (!BattleActive) return;
        Debug.Log("[BattleManager] Escape triggered inside Safe Zone.");
        BattleActive = false;
    }

    /// <summary>Ends the free-roam battle loop (used when restarting after defeat).</summary>
    public void StopBattleLoop()
    {
        BattleActive = false;
    }



    // ── Trip Target helpers ────────────────────────────────────────────────

    /// <summary>
    /// Builds up to 3 contextual trip targets for the current battle.
    /// Targets vary by mode and enemy count so they feel fresh each trip.
    /// </summary>
    private BattleTripTarget[] BuildTripTargets()
    {
        var targets = new System.Collections.Generic.List<BattleTripTarget>();

        // Target 1: Win the match (always present)
        targets.Add(new BattleTripTarget
        {
            Label       = "WIN THE MATCH",
            Description = "Wipe out more enemies than you lose",
            Achieved    = false
        });

        if (Mode == BattleMode.PoliceRaid)
        {
            // Target 2 (Police Raid): Keep losses below 40% of squad
            int rosterAlive = 0;
            var roster = GameData.instance?.PlayerData?.RecruitedAgents;
            if (roster != null)
                foreach (var a in roster) if (a.IsAlive) rosterAlive++;
            int maxLossAllowed = Mathf.Max(1, Mathf.RoundToInt(rosterAlive * 0.4f));

            targets.Add(new BattleTripTarget
            {
                Label       = $"KEEP LOSSES BELOW {maxLossAllowed}",
                Description = "Don't let too many lads go down",
                Achieved    = false
            });

            // Target 3 (Police Raid): Defeat at least half the police
            int halfPolice = Mathf.Max(1, _enemyCount / 2);
            targets.Add(new BattleTripTarget
            {
                Label       = $"DEFEAT {halfPolice}+ POLICE",
                Description = "Put at least half the filth on the ground",
                Achieved    = false
            });
        }
        else
        {
            // Target 2 (Rival Fight): Defeat at least half the rivals
            int killTarget = Mathf.Max(1, _enemyCount / 2);
            targets.Add(new BattleTripTarget
            {
                Label       = $"DEFEAT {killTarget}+ RIVALS",
                Description = "Take out at least half their firm",
                Achieved    = false
            });

            // Target 3 (Rival Fight): No more than 2 units lost (bonus challenge)
            targets.Add(new BattleTripTarget
            {
                Label       = "TAKE NO MORE THAN 2 LOSSES",
                Description = "Keep your lads standing throughout",
                Achieved    = false
            });
        }

        return targets.ToArray();
    }

    /// <summary>
    /// Evaluates each target in-place after the battle stats are known.
    /// </summary>
    private void EvaluateTripTargets(GameData.BattleResult result,
                                     int enemiesKilled, int playerLost,
                                     int aliveP, int aliveE)
    {
        if (_tripTargets == null) return;

        for (int i = 0; i < _tripTargets.Length; i++)
        {
            var t = _tripTargets[i];
            if (t.Label == "WIN THE MATCH")
            {
                t.Achieved = result == GameData.BattleResult.Victory;
            }
            else if (t.Label.StartsWith("DEFEAT ") && t.Label.EndsWith(" RIVALS"))
            {
                int required = ParseFirstInt(t.Label);
                t.Achieved = enemiesKilled >= required;
            }
            else if (t.Label.StartsWith("DEFEAT ") && t.Label.EndsWith(" POLICE"))
            {
                int required = ParseFirstInt(t.Label);
                t.Achieved = enemiesKilled >= required;
            }
            else if (t.Label.StartsWith("KEEP LOSSES BELOW "))
            {
                int threshold = ParseFirstInt(t.Label);
                t.Achieved = playerLost < threshold;
            }
            else if (t.Label == "TAKE NO MORE THAN 2 LOSSES")
            {
                t.Achieved = playerLost <= 2;
            }
            _tripTargets[i] = t;
        }
    }

    /// <summary>Extracts the first integer found in a string label.</summary>
    private static int ParseFirstInt(string s)
    {
        int result = 0;
        bool found = false;
        foreach (char c in s)
        {
            if (char.IsDigit(c)) { result = result * 10 + (c - '0'); found = true; }
            else if (found) break;
        }
        return result;
    }

    private Vector3 GetSpawnPosition(Transform root, int index)
    {
        // Use a consistent roomy grid instead of legacy authored points, which
        // were close enough for unit rings and tap targets to overlap.
        const float spacing = 3.2f;
        float x = ((index % 4) - 1.5f) * spacing;
        float z = (index / 4) * spacing;
        Vector3 basePos = root != null ? root.position : Vector3.zero;
        Vector3 right = root != null ? root.right : Vector3.right;
        Vector3 forward = root != null ? root.forward : Vector3.forward;
        right.y = 0f; forward.y = 0f;
        return basePos + right.normalized * x + forward.normalized * z;
    }

    // ── Public helpers (called by AgentController / EnemyController) ──────

    public void OnAgentDied(AgentController agent)
    {
        _playerAgentsKilled++;
        GameManager.Save();
        GameAudio.Play("injury");
        InjuryNotifications.Report(agent.Data);
        BroadcastCounts();
        // Wipe detection is handled inside RunRound's per-frame check.
        // Do NOT set BattleActive = false here; doing so would bypass _roundEndedByWipe.
    }

    public void OnEnemyDied(EnemyController enemy)
    {
        _enemiesKilled++;
        BroadcastCounts();

        // Per-kill brawl scraps.
        sessionMoneyEarned += 200;
        sessionReputationGained += 1;

        // Heat only rises on rival kills (not police).
        if (enemy != null && enemy.firmName != "POLICE")
            LivePoliceSystem.Instance?.NotifyKill();

        if (enemy != null && !string.IsNullOrEmpty(enemy.firmName) && enemy.firmName != "POLICE")
        {
            var remaining = _enemyAgents.Count(e =>
                e != null && e.IsAlive && e.firmName == enemy.firmName);

            if (remaining == 0)
            {
                foreach (var go in FindObjectsByType<GangArea>(FindObjectsSortMode.None))
                {
                    if (go != null && go.name == "GangRing_" + enemy.firmName)
                        Destroy(go.gameObject);
                }

                GrantGangWipeReward(enemy.firmName);
                LevelSystem.Instance?.OnGangEliminated(enemy.firmName);
                UnstickPlayerAgents();

                // Police may have been waiting for this scrap to finish.
                if (!IsRivalGangFightActive())
                    LivePoliceSystem.Instance?.NotifyGangFightEnded();
            }
        }
    }

    /// <summary>Congrats popup + cash + reputation for wiping a firm.</summary>
    private void GrantGangWipeReward(string firmName)
    {
        int level = LevelSystem.Instance != null ? LevelSystem.Instance.CurrentLevel : 1;
        int reward = 1500 + level * 250;
        int repGain = 8 + level * 2;
        sessionMoneyEarned += reward;
        sessionReputationGained += repGain;

        if (GameData.instance?.PlayerData != null)
        {
            GameData.instance.PlayerData.Money += reward;
            if(gameObject.scene.name=="Gameplay")GameData.instance.PlayerData.BattleWins++;
            GameData.instance.AddReputation(repGain);
            PersistBattleProgress();
        }

        int rank = GameData.instance?.PlayerData?.Ranking ?? 0;
        GamePopup.Instance.Show(
            "CONGRATS!",
            $"You wiped out {firmName}!\n\n+£{reward:N0}  ·  +{repGain} REP\nRanking: #{rank}",
            new GamePopup.Option("SORTED", new Color(0.2f, 0.65f, 0.3f), null)
        );
    }

    /// <summary>Stop player agents mid-path (used before gang encounter popup).</summary>
    public void HaltPlayerAgents()
    {
        foreach (var a in _playerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            // Clears AutoAttacking and freezes in place so the scrap can't start
            // before the HAVE IT / MOVE ON choice.
            a.CommandMoveTo(a.transform.position);
        }
    }

    /// <summary>Nudge living player agents out of a world radius (MOVE ON from turf).</summary>
    public void PullPlayerAgentsFrom(Vector3 center, float radius)
    {
        foreach (var a in _playerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            a.SetCinematicIdle(false);
            Vector3 delta = a.transform.position - center;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f)
                delta = a.transform.forward.sqrMagnitude > 0.01f ? a.transform.forward : Vector3.forward;
            // Always push clear of the zone so OnTriggerExit-style rearm can fire.
            Vector3 dest = center + delta.normalized * (radius + 2.5f);
            if (UnityEngine.AI.NavMesh.SamplePosition(dest, out var hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                dest = hit.position;
            a.CommandMoveTo(dest);
        }
    }

    /// <summary>Clears stuck AutoAttacking / path states after a scrap ends.</summary>
    public void UnstickPlayerAgents()
    {
        foreach (var a in _playerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            a.SetCinematicIdle(false);
            a.CommandMoveTo(a.transform.position);
        }
    }

    /// <summary>Returns nearest alive hostile EnemyController to a world position.</summary>
    public EnemyController GetNearestEnemy(Vector3 from)
    {
        EnemyController nearest = null;
        float minDist = float.MaxValue;
        foreach (var e in _enemyAgents)
        {
            if (e == null || !e.IsAlive || !e.isHostile) continue;
            float d = Vector3.Distance(from, e.transform.position);
            if (d < minDist) { minDist = d; nearest = e; }
        }
        return nearest;
    }

    /// <summary>Returns nearest alive AgentController to a world position.</summary>
    public AgentController GetNearestAgent(Vector3 from)
    {
        AgentController nearest = null;
        float minDist = float.MaxValue;
        foreach (var a in _playerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            float d = Vector3.Distance(from, a.transform.position);
            if (d < minDist) { minDist = d; nearest = a; }
        }
        return nearest;
    }

    public bool IsGangFightActive()
    {
        foreach (var e in _enemyAgents)
        {
            if (e == null || !e.IsAlive) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            if (e.firmName == "POLICE") continue;
            if (e.isHostile) return true;
        }
        return false;
    }

    /// <summary>Alias used by LivePoliceSystem — same as <see cref="IsGangFightActive"/>.</summary>
    public bool IsRivalGangFightActive() => IsGangFightActive();

    /// <summary>All alive player agents — used by AgentSelectionManager.</summary>
    public IReadOnlyList<AgentController> PlayerAgents => _playerAgents;

    /// <summary>All spawned enemy/police agents.</summary>
    public IReadOnlyList<EnemyController> EnemyAgents => _enemyAgents;

    /// <summary>
    /// Registers an enemy/police unit spawned at runtime (e.g. by LivePoliceSystem)
    /// so it counts toward alive totals and is picked up by targeting logic.
    /// </summary>
    public void RegisterEnemy(EnemyController e)
    {
        if (e != null && !_enemyAgents.Contains(e))
        {
            _enemyAgents.Add(e);
            BroadcastCounts();
        }
    }

    /// <summary>Centroid of alive player units (falls back to Vector3.zero if none).</summary>
    public Vector3 GetPlayerCentroid()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var a in _playerAgents)
            if (a != null && a.IsAlive) { sum += a.transform.position; n++; }
        return n > 0 ? sum / n : Vector3.zero;
    }

    /// <summary>
    /// Spawns a new player agent at runtime and adds them to the active player list.
    /// Also saves the new agent to the PlayerData roster so they stay in the player's gang.
    /// </summary>
    public void RestoreRecoveredAgent(AgentData data)
    {
        var existing = _playerAgents.Find(a => a && a.Data != null && data != null &&
            (a.Data == data || a.Data.AgentId == data.AgentId));
        if (existing) { existing.RestoreFromData(data); BroadcastCounts(); return; }
        if (_playerAgents.Count(a => a && a.IsAlive) >= maxPlayerAgents || !playerAgentPrefab) return;
        Vector3 center = playerSpawnRoot ? playerSpawnRoot.position : Vector3.zero;
        Vector3 position = FindOpenReachableSpawn(center + Vector3.right * _playerAgents.Count, center);
        position = FindOpenReachableSpawn(position, playerSpawnRoot ? playerSpawnRoot.position : position);
        var go = Instantiate(playerAgentPrefab, position, Quaternion.identity);
        var agent = go.GetComponent<AgentController>();
        if (!agent) { Destroy(go); return; }
        agent.Initialise(data, retreatPoint ? retreatPoint.position : center);
        agent.SetCinematicIdle(false); _playerAgents.Add(agent); BroadcastCounts();
    }

    public AgentController SpawnRecruitedAgentAt(Vector3 position, string name)
    {
        if (_playerAgents.Count(a=>a && a.IsAlive) >= maxPlayerAgents)
        {
            Debug.LogWarning("[BattleManager] Cannot spawn recruit — max active player agents reached!");
            return null;
        }

        // 1. Create agent data structure with default stats
        int portIdx = Random.Range(0, 4);
        var data = new AgentData(name, portIdx, hp: 60f, strength: 10f, speed: 2.5f, attackRange: 1.0f);

        // Persist to roster so they stay in the player's gang after battle
        var roster = GameData.instance?.PlayerData?.RecruitedAgents;
        roster?.Add(data);
        if (GameData.instance?.PlayerData != null)
        {
            if (!GameData.instance.PlayerData.DeploymentSelectionCustomized)
                GameData.instance.AddAgentToAwaySelection(data, maxPlayerAgents);
            GameData.instance.SyncFanCountWithAgents();
            GameData.instance.SaveData();
        }

        // 2. Instantiate 3D agent GameObject
        position = FindOpenReachableSpawn(position, playerSpawnRoot ? playerSpawnRoot.position : position);
        var go = Instantiate(playerAgentPrefab, position, Quaternion.identity);
        var ac = go.GetComponent<AgentController>();

        Vector3 retreat = retreatPoint != null ? retreatPoint.position : position;
        ac?.Initialise(data, retreat);

        if (ac != null)
        {
            ac.SetCinematicIdle(false);
            _playerAgents.Add(ac);
            BroadcastCounts();
            sessionFansRecruited++; // increment dynamic statistic
            AgentSelectionManager.instance?.Select(ac);

            Debug.Log($"[BattleManager] Recruited new unit: {name} at {position}. Added to active agents and saved roster.");
        }

        return ac;
    }

    /// <summary>Write live agent HP into the save roster and flush to disk.</summary>
    public void PersistBattleProgress()
    {
        foreach (var ac in _playerAgents)
        {
            if (ac == null) continue;
            ac.SyncHpToData();
        }
        GameData.instance?.SaveData();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PersistBattleProgress();
    }

    private void OnApplicationQuit()
    {
        PersistBattleProgress();
    }

    private void RemoveGangRing(string gangName)
    {
        var rings = GameObject.FindObjectsOfType<GameObject>();
        foreach (var go in rings)
        {
            if (go.name == "GangRing_" + gangName)
            {
                Destroy(go);
            }
        }
    }

    /// <summary>Orders every living player into AutoAttack against nearest hostile.</summary>
    public void OrderAllPlayersAttackNearestHostile()
    {
        SetAllAgentsCinematicIdle(false);
        AgentSelectionManager.instance?.SelectAll();
        foreach (var a in _playerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            a.SetCinematicIdle(false);
            var e = GetNearestEnemy(a.transform.position);
            if (e != null) a.CommandAttackTarget(e);
            else a.CommandAttack();
        }
    }

    public void AttackGang(string gangName)
    {
        var gangMembers = _enemyAgents.Where(e => e != null && e.IsAlive && e.firmName == gangName).ToList();
        if (gangMembers.Count == 0)
        {
            Debug.LogWarning($"[BattleManager] AttackGang: no living members for '{gangName}'.");
            return;
        }

        // Unlock cinematic lock (e.g. leftover from recruit) so units can fight.
        SetAllAgentsCinematicIdle(false);

        foreach (var enemy in gangMembers)
        {
            enemy.isHostile = true;
            enemy.SetCinematicIdle(false);
            var nearestPlayer = GetNearestAgent(enemy.transform.position);
            if (nearestPlayer != null)
                enemy.AlertToTarget(nearestPlayer);
        }

        AgentSelectionManager.instance?.SelectAll();

        // Assign each player a concrete target — CommandAttack alone can Idle out
        // if the hostile scan races a frame behind.
        foreach (var a in _playerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            a.SetCinematicIdle(false);

            EnemyController best = null;
            float bestD = float.MaxValue;
            foreach (var e in gangMembers)
            {
                if (e == null || !e.IsAlive) continue;
                float d = Vector3.Distance(a.transform.position, e.transform.position);
                if (d < bestD) { bestD = d; best = e; }
            }

            if (best != null)
                a.CommandAttackTarget(best);
            else
                a.CommandAttack();
        }

        Debug.Log($"[BattleManager] AttackGang '{gangName}': {gangMembers.Count} hostiles, {_playerAgents.Count} players ordered in.");
        BattleUIController.instance?.ShowAlert($"FIGHTING <color=#FF6B4A>{gangName.ToUpper()}</color>", 2.4f);
    }

    public void RecruitGang(string gangName, bool success)
    {
        RemoveGangRing(gangName);
        var gangMembers = _enemyAgents.Where(e => e != null && e.IsAlive && e.firmName == gangName).ToList();

        if (success)
        {
            Debug.Log($"[BattleManager] Successfully recruited rival gang: {gangName}");
            foreach (var enemy in gangMembers)
            {
                Vector3 pos = enemy.transform.position;
                if (_enemyAgents.Contains(enemy))
                {
                    _enemyAgents.Remove(enemy);
                }
                Destroy(enemy.gameObject);

                SpawnRecruitedAgentAt(pos, gangName + " Recruit");
            }

            BroadcastCounts();
        }
        else
        {
            Debug.Log($"[BattleManager] Recruitment rejected by gang: {gangName}! They are now hostile.");
            foreach (var enemy in gangMembers)
            {
                enemy.isHostile = true;
                var nearestPlayer = GetNearestAgent(enemy.transform.position);
                if (nearestPlayer != null)
                {
                    enemy.AlertToTarget(nearestPlayer);
                    enemy.AlertAlliesOfSameFaction(nearestPlayer);
                }
            }
        }
    }

    // ── Pause ─────────────────────────────────────────────────────────────
    public void SetPaused(bool paused)
    {
        IsPaused    = paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    // ── Counts ────────────────────────────────────────────────────────────
    public int AlivePlayerCount()
    {
        int c = 0;
        foreach (var a in _playerAgents) if (a != null && a.IsAlive) c++;
        return c;
    }

    public int AliveEnemyCount()
    {
        int c = 0;
        foreach (var e in _enemyAgents) if (e != null && e.IsAlive) c++;
        return c;
    }

    private bool IsAnyPlayerAlive() => AlivePlayerCount() > 0;
    private bool IsAnyEnemyAlive()  => AliveEnemyCount()  > 0;

    private void BroadcastCounts() =>
        OnCountsChanged?.Invoke(AlivePlayerCount(), AliveEnemyCount());

    // ── Police Raid chain ─────────────────────────────────────────────────

    /// <summary>
    /// Waits briefly (so the result panel is visible), then asks GameManager
    /// to launch the rival fight that was stashed before the police intercepted
    /// the away trip.
    /// </summary>
    private System.Collections.IEnumerator ChainRivalAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameManager.instance?.ChainRivalFightAfterRaid();
    }
}
