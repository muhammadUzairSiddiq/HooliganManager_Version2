#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// ScriptableObject — the single source of truth for Police Raid parameters.
///
/// Set up via:  Assets ▶ Create ▶ Hooligan ▶ Police Data Registry
///
/// Key properties
/// ──────────────
///  • officerTypes        — list of officer archetypes (Constable, Riot Officer…)
///  • baseSquadSize       — how many officers spawn when PoliceHeat first maxes (10)
///  • strengthPerHeatTier — bonus strength added per heat tier above 8
///  • hpMultiplier        — HP scalar applied to every officer
///
/// At runtime BattleManager calls GetPoliceParams() via PoliceManager to
/// retrieve the final (count, strength) pair.
/// </summary>
[CreateAssetMenu(menuName = "Hooligan/Police Data Registry", fileName = "PoliceDataRegistry")]
public class PoliceDataRegistry : ScriptableObject
{
    // ── Officer Archetypes ────────────────────────────────────────────────
    [Header("Officer Archetypes")]
    [Tooltip("Each entry defines one category of police officer. " +
             "BattleManager randomly picks from this list when spawning enemies.")]
    public CharacterPortraitRegistry officerTypes;

    // ── Squad Parameters ──────────────────────────────────────────────────
    [Header("Squad Parameters")]
    [Tooltip("Number of officers spawned when PoliceHeat = 10 (baseline).")]
    public int baseSquadSize = 10;

    [Tooltip("Extra officers added per completed heat tier above 8 " +
             "(heat 10 = tier 1, heat 12 would be tier 2 if the cap were raised, etc.).")]
    public int extraOfficersPerTier = 2;

    [Tooltip("Base numeric strength fed into enemy stat calculations.")]
    public int baseStrengthValue = 28;

    [Tooltip("Bonus strength added per heat tier above 8.")]
    public int strengthPerHeatTier = 4;

    [Tooltip("Global HP multiplier applied on top of each officer archetype's baseHp.")]
    [Range(0.5f, 3f)]
    public float hpMultiplier = 1.0f;

    public Sprite crestSprite;
    // ── Computed helpers (called by PoliceManager) ────────────────────

    /// <summary>
    /// Returns the number of officers and their numeric strength for the given
    /// current police heat level (expected range 0–10).
    /// Heat tiers above 8 increase both count and strength.
    /// </summary>
    public (int count, int strength) GetPoliceParams(int currentHeat)
    {
        int tier  = Mathf.Max(0, currentHeat - 8); // 0 at heat 8, 1 at heat 9-10, etc.
        int count = baseSquadSize + tier * extraOfficersPerTier;
        int str   = baseStrengthValue + tier * strengthPerHeatTier;
        return (count, str);
    }

    /// <summary>
    /// Returns a random PoliceCharacterEntry from the configured archetypes.
    /// Falls back gracefully if the list is empty.
    /// </summary>
    public CharacterPortraitRegistry.Entry GetRandomOfficerType()
    {
        if (officerTypes == null || officerTypes.entries.Count == 0) return null;
        return officerTypes.entries[Random.Range(0, officerTypes.entries.Count)];
    }
}

// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
/// <summary>
/// Auto-creates the PoliceDataRegistry asset under Assets/Data/ on first compile,
/// matching the pattern used by BotRegistry.
/// </summary>
public static class PoliceDataRegistryAssetCreator
{
    [InitializeOnLoadMethod]
    private static void CreateRegistryAsset()
    {
        const string directory = "Assets/Data";
        const string path      = directory + "/PoliceDataRegistry.asset";

        if (!AssetDatabase.IsValidFolder(directory))
            AssetDatabase.CreateFolder("Assets", "Data");

        if (AssetDatabase.LoadAssetAtPath<PoliceDataRegistry>(path) == null)
        {
            var asset = ScriptableObject.CreateInstance<PoliceDataRegistry>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PoliceDataRegistry] Created asset at {path}");
        }
    }
}
#endif
