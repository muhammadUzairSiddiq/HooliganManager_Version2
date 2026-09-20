using System;

/// <summary>
/// Persistent data for a single recruited agent.
/// Stored inside PlayerData.RecruitedAgents and saved via SaveDataInLocal.
/// </summary>
[Serializable]
public class AgentData
{
    // ── Identity ──────────────────────────────────────────────────────────
    public string AgentId;       // GUID — unique across saves
    public string AgentName;
    public int    PortraitIndex; // 0-3 → maps to profile sprite in Gameplay/

    // ── Stats ─────────────────────────────────────────────────────────────
    public float MaxHp;
    public float CurrentHp;
    public float Strength;       // damage dealt per hit
    public float Speed;          // world units per second
    public float AttackRange;    // how close before swinging

    // ── RTS management profile ──────────────────────────────────────────
    [System.Runtime.Serialization.OptionalField] public bool ManagementProfileInitialized;
    [System.Runtime.Serialization.OptionalField] public string ManagementRole;
    [System.Runtime.Serialization.OptionalField] public float Stamina;
    [System.Runtime.Serialization.OptionalField] public int OperationsExperience;

    // ── State ─────────────────────────────────────────────────────────────
    public bool  IsAlive => CurrentHp > 0;

    // ── Constructor with sensible defaults ───────────────────────────────
    public AgentData(string name, int portraitIndex = 0,
                     float hp = 60f, float strength = 10f,
                     float speed = 2.5f, float attackRange = 1.0f)
    {
        AgentId       = Guid.NewGuid().ToString();
        AgentName     = name;
        PortraitIndex = portraitIndex;
        MaxHp         = hp;
        CurrentHp     = hp;
        Strength      = strength;
        Speed         = speed;
        AttackRange   = attackRange;
        ManagementProfileInitialized = false;
        EnsureManagementProfile(portraitIndex);
    }

    /// <summary>Heal by a percentage of max HP (called on matchday end).</summary>
    public void HealPercent(float percent)
    {
        CurrentHp = UnityEngine.Mathf.Clamp(CurrentHp + MaxHp * percent, 0, MaxHp);
    }

    /// <summary>Fully restore HP (e.g. after a victory).</summary>
    public void FullHeal() => CurrentHp = MaxHp;

    public void EnsureManagementProfile(int rosterIndex = 0)
    {
        if (ManagementProfileInitialized) return;
        string[] roles = { "LEADER", "SCOUT", "ORGANIZER", "RUNNER" };
        ManagementRole = roles[Math.Abs(rosterIndex) % roles.Length];
        Stamina = 100f;
        OperationsExperience = 0;
        ManagementProfileInitialized = true;
    }
}
