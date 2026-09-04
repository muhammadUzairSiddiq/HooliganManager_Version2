using UnityEngine;

/// <summary>
/// Legacy stub — the old Arikan dot minimap + G/R/P legend are retired.
/// LiveMiniMap handles icons itself. Kept so existing call sites compile.
/// </summary>
public static class MiniMapIconFactory
{
    public enum Kind { Player, Gang, Recruit, Police, Turf }

    public static void Register(Transform target, Kind kind, string tooltip = null)
    {
        // No-op: LiveMiniMap scans the world every frame.
        LiveMiniMap.EnsureExists();
    }

    public static Sprite GetSprite(Kind kind) => null;
    public static Color ColorFor(Kind kind) => Color.white;
}
