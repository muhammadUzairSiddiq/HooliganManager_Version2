using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class PedestrianSpawner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The pedestrian prefab to spawn (must have PedestrianController attached).")]
    public GameObject pedestrianPrefab;

    [Tooltip("The registry to pick pedestrian 3D models from. If null, falls back to BattleManager.portraitRegistry.")]
    public CharacterPortraitRegistry pedestrianRegistry;

    [Tooltip("The parent GameObject containing all the pavement mesh children.")]
    public Transform pavementParent;

    [Tooltip("Number of pedestrians to spawn.")]
    public int spawnCount = 10;

    [Tooltip("Radius around the camera to spawn pedestrians.")]
    public float spawnRadius = 40f;

    [Tooltip("Radius around the camera to despawn pedestrians.")]
    public float despawnRadius = 60f;

    private MeshRenderer[] _pavementMeshes;
    private List<GameObject> _activePedestrians = new List<GameObject>();
    private bool _isInitialized = false;
    public bool IsInitialized => _isInitialized;

    private int _ambientCount;

    private void Start()
    {
        int full = Mathf.Max(6, GameplayTuning.Current.maxPedestrians);
        _ambientCount = 3;
        spawnCount = _ambientCount;
        if (BattleManager.instance) BattleManager.instance.OnMapSpawnedIn += SetupPedestrianSpawner;
    }

    public void SetMatchdayIntensity(bool live)
    {
        int full = Mathf.Max(6, GameplayTuning.Current.maxPedestrians);
        if (_ambientCount <= 0) _ambientCount = 3;
        spawnCount = live ? Mathf.Min(5, full) : _ambientCount;
    }

    private void SetupPedestrianSpawner()
    {
        if (pavementParent == null)
        {
            // Try to find it dynamically if not assigned
            GameObject foundParent = GameObject.Find("PavementsParent");
            if (foundParent != null)
            {
                pavementParent = foundParent.transform;
            }
        }

        if (pedestrianPrefab == null)
        {
            Debug.LogWarning("[PedestrianSpawner] Pedestrian prefab is missing.");
            return;
        }

        // Get walkable presentation meshes. The production city is a flattened import and
        // does not always preserve a dedicated PavementsParent, so discover its path pieces.
        _pavementMeshes = pavementParent != null
            ? pavementParent.GetComponentsInChildren<MeshRenderer>()
            : FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r && r.transform.root.name == "Demonstration")
                .Where(r =>
                {
                    string n=r.name.ToLowerInvariant();
                    return n.StartsWith("park_road") || n.StartsWith("side_") ||
                           n.Contains("sidewalk") || n.Contains("pavement") || n.Contains("walkway");
                }).ToArray();

        if (_pavementMeshes.Length == 0)
        {
            Debug.LogWarning("[PedestrianSpawner] No MeshRenderers found in the children of the pavement parent!");
            return;
        }

        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 camPos = cam.transform.position;

        // Clean up out of bounds pedestrians
        for (int i = _activePedestrians.Count - 1; i >= 0; i--)
        {
            GameObject ped = _activePedestrians[i];
            if (ped == null)
            {
                _activePedestrians.RemoveAt(i);
                continue;
            }

            if (HorizontalDistance(ped.transform.position, camPos) > despawnRadius)
            {
                Destroy(ped);
                _activePedestrians.RemoveAt(i);
            }
        }

        // Spawn new ones if we are below the count
        int spawnTries = 0;
        // Limit spawns per frame to avoid lag spikes
        while (_activePedestrians.Count < spawnCount && spawnTries < 3)
        {
            spawnTries++;
            Vector3 spawnPos = GetRandomPavementPositionNearCamera(camPos);
            if (spawnPos != Vector3.zero)
            {
                GameObject go = Instantiate(pedestrianPrefab, spawnPos, Quaternion.identity, transform);
                
                PedestrianController controller = go.GetComponent<PedestrianController>();
                if (controller == null)
                {
                    controller = go.AddComponent<PedestrianController>();
                }
                
                var registryToUse = pedestrianRegistry != null ? pedestrianRegistry : BattleManager.instance.portraitRegistry;
                controller.Initialize(_pavementMeshes, registryToUse);
                (go.GetComponent<SocialNpc>() ?? go.AddComponent<SocialNpc>()).Configure("CITY STREET", false);
                
                _activePedestrians.Add(go);
            }
        }
    }

    private Vector3 GetRandomPavementPositionNearCamera(Vector3 camPos)
    {
        // Try up to 10 times to find a valid NavMesh spot on the pavement near the camera
        for (int i = 0; i < 10; i++)
        {
            MeshRenderer randomMesh = _pavementMeshes[Random.Range(0, _pavementMeshes.Length)];
            
            // Fast distance check to the mesh center before doing precise bounds logic
            if (HorizontalDistance(randomMesh.transform.position, camPos) > spawnRadius) continue;

            Bounds bounds = randomMesh.bounds;

            Vector3 randomPos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                // Ensure it's not too close to the camera to prevent popping in right in front of the player
                if (HorizontalDistance(hit.position, camPos) > 10f)
                {
                    return hit.position;
                }
            }
        }

        return Vector3.zero;
    }

    public GameObject SpawnActivityPedestrian(Vector3 center, float radius, Transform parent, string venue = "CITY STREET", bool showTalkPrompt = true)
    {
        if (!_isInitialized || pedestrianPrefab == null) return null;

        for (int i = 0; i < 16; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas)) continue;

            GameObject go = Instantiate(pedestrianPrefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
            go.name = "Matchday Supporter";
            PedestrianController controller = go.GetComponent<PedestrianController>();
            if (controller == null) controller = go.AddComponent<PedestrianController>();
            var registryToUse = pedestrianRegistry != null ? pedestrianRegistry : BattleManager.instance?.portraitRegistry;
            controller.Initialize(_pavementMeshes, registryToUse);
            controller.SetActivityZone(center, radius);
            (go.GetComponent<SocialNpc>() ?? go.AddComponent<SocialNpc>()).Configure(venue, showTalkPrompt);
            return go;
        }

        return null;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
