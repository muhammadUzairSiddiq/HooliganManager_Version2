using System;
using UnityEngine;

/// <summary>
/// Defines a single interactive action that a MapBuilding exposes to nearby units.
/// Add multiple entries to MapBuildingData.actions to populate multiple buttons in
/// the BuildingInteractionPanel.
///
/// All fields are serializable so they can be authored directly in a
/// MapBuildingData ScriptableObject asset — no code changes needed for new actions.
/// </summary>
[Serializable]
public class BuildingActionConfig
{
    // ── Identity ──────────────────────────────────────────────────────────
    [Tooltip("Text shown on the action button, e.g. 'HEAL UP' or 'BRIBE OFFICER'")]
    public string actionLabel = "USE";

    [Tooltip("Icon shown next to the button label. Optional.")]
    public Sprite actionIcon;

    // ── Action Type ───────────────────────────────────────────────────────
    public BuildingActionType actionType = BuildingActionType.Heal;

    // ── Heal parameters ───────────────────────────────────────────────────
    [Header("Heal Settings")]
    [Tooltip("HP restored to each nearby unit when actionType = Heal")]
    public float healAmount = 30f;

    [Tooltip("If true, heals to full HP instead of using healAmount")]
    public bool healToFull = false;

    // ── Boost parameters ──────────────────────────────────────────────────
    [Header("Boost Settings")]
    public BoostType boostType = BoostType.Speed;

    [Tooltip("Multiplier added on top of base stat (e.g. 0.5 = +50% speed)")]
    public float boostAmount = 0.5f;

    [Tooltip("How long the boost lasts in seconds")]
    public float boostDuration = 20f;

    // ── Heat/Police parameters ────────────────────────────────────────────
    [Header("Heat Settings (PoliceStation)")]
    [Tooltip("Amount of police heat removed when actionType = ReduceHeat")]
    public int heatReduction = 2;

    // ── Economy ───────────────────────────────────────────────────────────
    [Header("Cost")]
    [Tooltip("Money deducted from sessionMoneyEarned. 0 = free.")]
    public int costMoney = 0;

    // ── Recruit parameters ────────────────────────────────────────────────
    [Header("Recruit Settings")]
    [Tooltip("Name of the newly recruited agent.")]
    public string recruitName = "New Recruit";

    // ── Cooldown ──────────────────────────────────────────────────────────
    [Header("Cooldown")]
    [Tooltip("Seconds before this action can be used again. 0 = no cooldown.")]
    public float cooldownSeconds = 15f;

    // ── Runtime cooldown (not serialised) ────────────────────────────────
    [NonSerialized] public float LastUsedTime = -999f;

    /// <summary>True when the action is ready to be used.</summary>
    public bool IsReady => Time.time - LastUsedTime >= cooldownSeconds;

    /// <summary>Remaining cooldown in seconds (clamped to 0).</summary>
    public float CooldownRemaining => Mathf.Max(0f, cooldownSeconds - (Time.time - LastUsedTime));
}

// ── Enums ─────────────────────────────────────────────────────────────────

public enum BuildingActionType
{
    Heal,           // Restore HP to nearby player units
    Boost,          // Temporarily raise Speed or Strength
    ReduceHeat,     // Lower police heat (Police Station bribe)
    Recruit,        // Recruit a new gang member to join the battle
    Custom          // Reserved for future extension
}

public enum BoostType
{
    Speed,
    Strength
}

