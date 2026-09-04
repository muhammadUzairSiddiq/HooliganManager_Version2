using UnityEngine;

/// <summary>
/// Retired — no G/R/P text legend. LiveMiniMap uses icon markers only.
/// </summary>
public class MiniMapLegend : MonoBehaviour
{
    public static void EnsureExists()
    {
        // Destroy any leftover legend from previous sessions.
        var existing = GameObject.Find("MiniMapLegend");
        if (existing != null) Object.Destroy(existing);
        var canvas = GameObject.Find("MiniMapLegendCanvas");
        if (canvas != null) Object.Destroy(canvas);
    }
}
