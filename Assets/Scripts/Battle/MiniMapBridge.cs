using UnityEngine;

/// <summary>
/// Replaces the old Arikan white-dot minimap bridge.
/// Disables any legacy MiniMapView and boots the live realtime minimap.
/// </summary>
public class MiniMapBridge : MonoBehaviour
{
    private void Start()
    {
        if (BattleManager.instance != null)
            BattleManager.instance.OnMapSpawnedIn += OnMapLoaded;
        else
            OnMapLoaded();
    }

    private void OnDestroy()
    {
        if (BattleManager.instance != null)
            BattleManager.instance.OnMapSpawnedIn -= OnMapLoaded;
    }

    private void OnMapLoaded()
    {
        LiveMiniMap.EnsureExists();
    }
}
