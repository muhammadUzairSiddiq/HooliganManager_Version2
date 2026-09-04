using UnityEngine;

/// <summary>
/// Drives ambient environmental atmosphere for the battle scene.
///
/// What it does
/// ────────────
///  • Keeps a Particle System (dust / debris / floating specks) alive for the
///    entire scene session — not just during the intro.
///  • Simulates wind gusts by oscillating the particle emission direction using
///    a sine-wave curve, so the environment never feels static.
///  • Optionally drives a second "heavy gust" burst on the enemy-advancing warning.
///
/// Setup
/// ─────
///  1. Create a Particle System GameObject in your Battle Scene (or drag the
///     prefab). Attach this script to the same GameObject, OR assign the
///     particle system via the Inspector reference below.
///  2. Recommended particle system settings for a subtle dust look:
///       Duration      : ∞ (looping)
///       Start Lifetime: 4 – 8 s
///       Start Speed   : 0.5 – 1.5
///       Start Size    : 0.01 – 0.06
///       Emission Rate : 15 – 30 / second
///       Shape          : Box  (wide, flat — covers the ground plane)
///       Color over Lifetime: white → transparent
///       Renderer       : Mesh or Billboard, additive blending
///  3. Assign the component in the Inspector. If left null, the script looks
///     for a ParticleSystem on the same GameObject.
/// </summary>
public class BattleEnvironmentFX : MonoBehaviour
{
    // ── Singleton (optional — used by PreBattleSequencer to trigger burst) ─
    public static BattleEnvironmentFX instance;

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Particle Systems")]
    [Tooltip("Main ambient dust / debris particle system. Leave null to auto-find on this GO.")]
    public ParticleSystem dustParticles;

    [Tooltip("Optional: a heavier gust burst PS triggered on the enemy warning.")]
    public ParticleSystem gustBurstParticles;

    [Header("Wind Simulation")]
    [Tooltip("Overall strength of the simulated wind force on particles.")]
    [Range(0f, 3f)]
    public float windStrength = 0.8f;

    [Tooltip("How fast the wind direction oscillates (Hz). Higher = gustier.")]
    [Range(0.05f, 1f)]
    public float gustFrequency = 0.18f;

    [Tooltip("Constant base wind direction (world space). The oscillation is added on top.")]
    public Vector3 baseWindDirection = new Vector3(1f, 0f, 0.3f);

    [Header("Intensity over battle phase")]
    [Tooltip("Emission rate multiplier during the pre-battle intro (more atmospheric).")]
    [Range(0.5f, 3f)]
    public float introEmissionMultiplier = 2.0f;

    [Tooltip("Emission rate multiplier once the battle is fully active.")]
    [Range(0.5f, 3f)]
    public float battleEmissionMultiplier = 1.0f;

    // ── Internal ──────────────────────────────────────────────────────────
    private ParticleSystem.VelocityOverLifetimeModule _velModule;
    private ParticleSystem.EmissionModule             _emissionModule;
    private float _baseEmissionRate;
    private bool  _modulesCached;

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        // Auto-find if not assigned
        if (dustParticles == null)
            dustParticles = GetComponent<ParticleSystem>();

        CacheModules();
    }

    void Start()
    {
        if (dustParticles != null && !dustParticles.isPlaying)
            dustParticles.Play();

        // Start at intro emission intensity
        SetEmissionMultiplier(introEmissionMultiplier);
    }

    void Update()
    {
        if (!_modulesCached) return;

        // ── Oscillating wind direction ────────────────────────────────────
        // Uses two layered sines at different frequencies for a natural, irregular feel
        float t         = Time.time;
        float gustSin   = Mathf.Sin(t * gustFrequency * Mathf.PI * 2f);
        float gustSin2  = Mathf.Sin(t * gustFrequency * 1.7f * Mathf.PI * 2f) * 0.4f; // harmonic

        // Perpendicular offset (cross product with up) creates lateral sway
        Vector3 perp = Vector3.Cross(baseWindDirection.normalized, Vector3.up).normalized;

        Vector3 windDir = (baseWindDirection + perp * (gustSin + gustSin2) * 0.5f).normalized;
        Vector3 windVel = windDir * windStrength * (0.7f + 0.3f * Mathf.Abs(gustSin));

        // Apply to velocity-over-lifetime so each particle drifts naturally
        _velModule.x = new ParticleSystem.MinMaxCurve(windVel.x - 0.1f, windVel.x + 0.1f);
        _velModule.y = new ParticleSystem.MinMaxCurve(windVel.y - 0.05f, windVel.y + 0.05f);
        _velModule.z = new ParticleSystem.MinMaxCurve(windVel.z - 0.1f, windVel.z + 0.1f);
    }

    // ── Public API (called by PreBattleSequencer / BattleManager) ─────────

    /// <summary>
    /// Call when the battle actually starts (after the intro sequence).
    /// Reduces emission back to the normal in-battle level.
    /// </summary>
    public void OnBattleStarted()
    {
        SetEmissionMultiplier(battleEmissionMultiplier);
    }

    /// <summary>
    /// Triggers the heavy gust burst particle system (if assigned) —
    /// called by PreBattleSequencer during the enemy-advancing warning.
    /// </summary>
    public void TriggerGustBurst()
    {
        if (gustBurstParticles != null)
        {
            gustBurstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gustBurstParticles.Play();
        }

        // Also temporarily spike the main dust emission for a dramatic moment
        SetEmissionMultiplier(introEmissionMultiplier * 2.5f);
        CancelInvoke(nameof(RestoreIntroEmission));
        Invoke(nameof(RestoreIntroEmission), 1.5f);
    }

    // ── Internal helpers ──────────────────────────────────────────────────

    private void CacheModules()
    {
        if (dustParticles == null) { _modulesCached = false; return; }

        _velModule      = dustParticles.velocityOverLifetime;
        _emissionModule = dustParticles.emission;
        _velModule.enabled = true;

        // Capture the designer's base emission rate from the particle system asset
        _baseEmissionRate = _emissionModule.rateOverTime.constant;
        _modulesCached    = true;
    }

    private void SetEmissionMultiplier(float multiplier)
    {
        if (!_modulesCached) return;
        _emissionModule.rateOverTime = _baseEmissionRate * multiplier;
    }

    private void RestoreIntroEmission()
    {
        SetEmissionMultiplier(introEmissionMultiplier);
    }
}
