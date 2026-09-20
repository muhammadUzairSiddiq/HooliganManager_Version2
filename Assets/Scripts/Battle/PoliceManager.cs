using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton MonoBehaviour — manages the Police Raid environment, coordinates
/// police-specific assets, and handles ambient police spawning on the map.
/// Replaces the old PoliceRaidManager.
/// </summary>
public class PoliceManager : MonoBehaviour
{
    public static PoliceManager instance;

    [Header("Environment (Raid Settings)")]
    [Tooltip("Root GameObject containing all police-raid environment props. SetActive(true) when a raid begins.")]
    public GameObject policeRaidEnvRoot;

    [Tooltip("Additional environment GameObjects toggled alongside the main root.")]
    public List<GameObject> additionalEnvRoots = new List<GameObject>();

    [Header("Police Data")]
    [Tooltip("ScriptableObject that defines officer archetypes and squad parameters.")]
    public PoliceDataRegistry policeRegistry;

    [Header("Ambient Spawning Settings")]
    [Tooltip("The police prefab to spawn.")]
    public GameObject policePrefab;

    [Tooltip("The police car prefab to spawn alongside police members.")]
    public GameObject policeCarPrefab;

    [Tooltip("Offset for spawning police car relative to the active spawn point.")]
    public Vector3 policeCarOffset = new Vector3(2.5f, 0f, 0f);

    [Tooltip("Whether to spawn a police car alongside police members at each active spawn point.")]
    public bool spawnPoliceCar = true;

    [Tooltip("The parent transform containing all possible ambient spawn points as children.")]
    public Transform policeSpawnRoot;

    [Range(0f, 100f)]
    [Tooltip("Percentage of spawn points that will have police spawned at them.")]
    public float activeSpawnPercentage = 50f;

    [Tooltip("Minimum number of police to spawn at an active point.")]
    public int minPolicePerPoint = 1;

    [Tooltip("Maximum number of police to spawn at an active point.")]
    public int maxPolicePerPoint = 3;

    [Header("Police Roaming Settings")]
    [Tooltip("Patrol/roaming radius around spawn point for ambient police.")]
    public float policePatrolRadius = 6f;

    [Tooltip("Player detection radius for ambient police.")]
    public float policeDetectionRadius = 12f;

    private void Awake()
    {
        instance = this;

        if (policeRegistry == null)
            policeRegistry = Resources.Load<PoliceDataRegistry>("PoliceDataRegistry");

        if (policeRegistry == null)
            Debug.LogWarning("[PoliceManager] PoliceDataRegistry not found. Assign it or place in Resources/.");
    }

    private void Start()
    {
        BattleManager.instance.OnMapSpawnedIn += SpawnPolice;
    }

    /// <summary>
    /// Activates or deactivates the police raid environment root GameObject(s).
    /// </summary>
    public void SetupRaid(bool enable)
    {
        if (policeRaidEnvRoot != null)
        {
            policeRaidEnvRoot.SetActive(enable);
            Debug.Log($"[PoliceManager] policeRaidEnvRoot.SetActive({enable})");
        }
        else
        {
            Debug.LogWarning("[PoliceManager] policeRaidEnvRoot is not assigned.");
        }

        foreach (var obj in additionalEnvRoots)
        {
            if (obj != null)
                obj.SetActive(enable);
        }
    }

    /// <summary>
    /// Returns the officer count and strength value for the current police heat level.
    /// </summary>
    public (int count, int strength) GetPoliceParams(int currentHeat)
    {
        if (policeRegistry != null)
            return policeRegistry.GetPoliceParams(currentHeat);

        Debug.LogWarning("[PoliceManager] No PoliceDataRegistry — using fallback values.");
        return (10, 28);
    }

    /// <summary>
    /// Returns a random officer archetype from the registry.
    /// </summary>
    public CharacterPortraitRegistry.Entry GetRandomOfficerType()
    {
        return policeRegistry?.GetRandomOfficerType();
    }

    /// <summary>
    /// Ambient foot officers only — street cars are handled by LivePoliceSystem patrols.
    /// </summary>
    public void SpawnPolice()
    {
        Debug.LogWarning($"[PoliceManager] SpawnPolice started");
        if (GameManager.IsPolicePlayer) return;
        if (policePrefab == null)
        {
            Debug.LogWarning("PoliceManager: No police prefab assigned! Please assign the police character prefab.");
            return;
        }

        // Rival fights use LivePoliceSystem patrol cars — skip static parked cars.
        if (BattleManager.instance != null && BattleManager.instance.Mode == BattleManager.BattleMode.RivalFight)
        {
            Debug.Log("[PoliceManager] RivalFight — skipping ambient parked cars (LivePolice patrols instead).");
            return;
        }

        if (policeSpawnRoot == null)
        {
            GameObject foundRoot = GameObject.Find("PoliceSpawnPoints");
            if (foundRoot != null)
                policeSpawnRoot = foundRoot.transform;
        }

        if (policeSpawnRoot == null || policeSpawnRoot.childCount == 0)
        {
            Debug.LogWarning($"[PoliceManager] policeSpawnRoot missing or empty.");
            return;
        }

        List<Transform> spawnPoints = new List<Transform>();
        for (int i = 0; i < policeSpawnRoot.childCount; i++)
            spawnPoints.Add(policeSpawnRoot.GetChild(i));

        int activePointCount = Mathf.RoundToInt((activeSpawnPercentage / 100f) * spawnPoints.Count);
        activePointCount = Mathf.Clamp(activePointCount, 0, spawnPoints.Count);

        List<Transform> shuffledPoints = new List<Transform>(spawnPoints);
        ShuffleList(shuffledPoints);

        for (int i = 0; i < activePointCount; i++)
        {
            Transform activePoint = shuffledPoints[i];

            if (spawnPoliceCar && policeCarPrefab != null)
            {
                Vector3 carPosition = activePoint.TransformPoint(policeCarOffset);
                Instantiate(policeCarPrefab, carPosition, activePoint.rotation);
            }

            int policeCount = Random.Range(minPolicePerPoint, maxPolicePerPoint + 1);

            for (int j = 0; j < policeCount; j++)
            {
                Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                Vector3 spawnPosition = activePoint.position + randomOffset;
                var go = Instantiate(policePrefab, spawnPosition, activePoint.rotation);
                var ec = go.GetComponent<EnemyController>();
                if (ec != null)
                {
                    ec.patrolRadius = policePatrolRadius;
                    ec.detectionRadius = policeDetectionRadius;
                    ec.firmName = "POLICE";
                    ec.isHostile = false;

                    int heat = 10;
                    var (count, strength) = GetPoliceParams(heat);
                    float enemyHp = 40f + strength * 0.5f;
                    float enemyDmg = 6f + strength * 0.2f;
                    float enemySpeed = 2.2f;
                    ec.Initialise(enemyHp, enemyDmg, enemySpeed, 1.0f, policeRegistry != null ? policeRegistry.officerTypes : null);
                }
            }
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
