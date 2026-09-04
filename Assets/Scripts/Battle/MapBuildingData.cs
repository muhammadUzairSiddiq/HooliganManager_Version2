using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that fully describes a map building's identity and capabilities.
///
/// Create via: Assets → Create → Hooligan → Map Building Data
///
/// Each distinct building type (Infirmary, Police Station, Armoury, etc.) gets its
/// own asset. Drag the asset onto a MapBuilding component in the scene to configure
/// that building — no code changes required for new building types.
/// </summary>
[CreateAssetMenu(menuName = "Hooligan/Map Building Data", fileName = "NewBuildingData")]
public class MapBuildingData : ScriptableObject
{
    // ── Identity ──────────────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip("Displayed in the interaction panel header, e.g. 'INFIRMARY' or 'POLICE STATION'")]
    public string buildingName = "Building";

    [TextArea(2, 4)]
    [Tooltip("Short flavour description shown in the panel subtitle.")]
    public string buildingDescription = "Interact with this location.";

    [Tooltip("Icon shown in the panel header and on any map overlay.")]
    public Sprite buildingIcon;

    [Tooltip("Accent colour used for the panel header and proximity indicator ring.")]
    public Color buildingColor = new Color(0.2f, 0.8f, 0.4f, 1f);

    // ── Proximity ─────────────────────────────────────────────────────────
    [Header("Proximity")]
    [Tooltip("Units within this radius trigger the interaction UI.")]
    public float interactionRadius = 6f;

    [Tooltip("If true, enemy units near this building also auto-use the first action (e.g. Infirmary auto-heal for enemies).")]
    public bool enemyAutoUse = false;

    [Tooltip("Interval (seconds) at which enemy auto-use fires. Ignored if enemyAutoUse is false.")]
    public float enemyAutoUseInterval = 5f;

    // ── Actions ───────────────────────────────────────────────────────────
    [Header("Actions")]
    [Tooltip("List of actions available in the interaction panel. Each entry creates one button.")]
    public List<BuildingActionConfig> actions = new List<BuildingActionConfig>();
}
