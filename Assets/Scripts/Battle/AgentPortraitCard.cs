using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Portrait Card for a single battle agent.
///
/// What it displays
/// ────────────────
///   • Pre-rendered portrait Sprite from CharacterPortraitRegistry
///   • HP fill bar that colour-shifts green → yellow → red as HP drops
///   • Numeric HP label  "45 / 80"
///   • Agent name label
///   • Highlight glow when the agent is selected
///
/// How to use
/// ──────────
///  1. Create the prefab hierarchy (see below), attach this script.
///  2. Assign all Inspector references.
///  3. After Instantiate, call  card.Initialise(agent, registry, modelIndex).
///  4. The card refreshes HP every LateUpdate automatically.
///
/// Suggested prefab hierarchy
/// ──────────────────────────
///   PortraitCard  ← this script
///   ├── Background       (Image — dark panel)
///   ├── PortraitFrame    (Image — border/frame graphic)
///   │   └── PortraitImage  (Image ← portrait sprite goes here)
///   ├── SelectionGlow    (Image — bright glow, hidden by default)
///   ├── HpBarBG          (Image — grey background)
///   │   └── HpBarFill    (Image — Fill Method: Horizontal)
///   ├── HpLabel          (TextMeshProUGUI — "45 / 80")
///   └── NameLabel        (TextMeshProUGUI — "BRICK")
/// </summary>
public class AgentPortraitCard : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Portrait Image")]
    [Tooltip("Image component that shows the pre-rendered portrait sprite.")]
    public Image portraitImage;

    [Tooltip("Sprite shown when no registry entry is found for this agent's model.")]
    public Sprite fallbackPortrait;

    [Header("HP Bar")]
    [Tooltip("Filled Image (Fill Method: Horizontal) — the coloured HP bar.")]
    public Image hpFill;

    [Tooltip("Full HP colour.")]
    public Color hpColorFull   = new Color(0.20f, 0.80f, 0.25f, 1f);   // green

    [Tooltip("Medium HP colour.")]
    public Color hpColorMedium = new Color(0.95f, 0.75f, 0.05f, 1f);   // yellow

    [Tooltip("Low HP colour — shown when HP ratio is below lowHpThreshold.")]
    public Color hpColorLow    = new Color(0.90f, 0.15f, 0.10f, 1f);   // red

    [Tooltip("HP ratio (0-1) below which the bar turns red and a pulse plays.")]
    [Range(0.05f, 0.40f)]
    public float lowHpThreshold    = 0.25f;

    [Tooltip("HP ratio (0-1) below which the bar turns yellow.")]
    [Range(0.30f, 0.70f)]
    public float mediumHpThreshold = 0.55f;

    [Header("Labels")]
    [Tooltip("Displays 'CURRENT / MAX' HP numbers.")]
    public TextMeshProUGUI hpLabel;

    [Tooltip("Displays the agent's name.")]
    public TextMeshProUGUI nameLabel;

    [Header("Selection Glow")]
    [Tooltip("Border/glow Image — shown when the agent is selected.")]
    public Image selectionGlow;

    // ── Internal ──────────────────────────────────────────────────────────

    private AgentController _agent;
    private float           _pulseTimer;
    private bool            _isPulsing;

    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bind this card to an agent. Call immediately after Instantiate.
    /// </summary>
    /// <param name="agent">The AgentController this card tracks.</param>
    /// <param name="registry">CharacterPortraitRegistry — used to look up the portrait sprite.</param>
    /// <param name="modelPrefab">
    /// The exact prefab reference used when spawning this agent's character model.
    /// Pass BattleManager.instance.characterList[index] for the spawned model.
    /// </param>
    public void Initialise(AgentController agent,
                           CharacterPortraitRegistry registry,
                           GameObject modelPrefab)
    {
        _agent = agent;

        // ── Portrait sprite ───────────────────────────────────────────────
        if (portraitImage != null)
        {
            Sprite portrait = registry != null
                ? registry.GetPortrait(modelPrefab)
                : null;

            portraitImage.sprite = portrait != null ? portrait : fallbackPortrait;
            portraitImage.gameObject.SetActive(portraitImage.sprite != null);
        }

        // ── Name label ────────────────────────────────────────────────────
        if (nameLabel != null && agent.Data != null)
        {
            agent.Data.EnsureManagementProfile();
            nameLabel.text = agent.Data.AgentName.ToUpper()+" · "+agent.Data.ManagementRole;
        }

        // ── Initial HP ────────────────────────────────────────────────────
        if (agent.Data != null)
            RefreshHP(agent.CurrentHp, agent.Data.MaxHp);

        // ── Selection glow ────────────────────────────────────────────────
        if (selectionGlow != null)
            selectionGlow.gameObject.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Show or hide the selection highlight border.</summary>
    public void SetSelected(bool selected)
    {
        if (selectionGlow != null)
            selectionGlow.gameObject.SetActive(selected);
    }

    /// <summary>Force an immediate HP refresh (call externally if needed).</summary>
    public void RefreshHP(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (hpFill != null)
        {
            hpFill.fillAmount = ratio;
            hpFill.color      = HPColour(ratio);
        }

        if (hpLabel != null)
            hpLabel.text = _agent?.Data!=null
                ? $"HP {Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)} · STA {Mathf.CeilToInt(_agent.Data.Stamina)}"
                : $"HP {Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";

        _isPulsing = ratio <= lowHpThreshold && ratio > 0f;
    }

    // ── Unity callbacks ───────────────────────────────────────────────────

    void LateUpdate()
    {
        if (_agent == null) return;

        // Hide when dead
        if (!_agent.IsAlive)
        {
            gameObject.SetActive(false);
            return;
        }

        // Keep HP in sync each frame
        if (_agent.Data != null)
            RefreshHP(_agent.CurrentHp, _agent.Data.MaxHp);

        // Low-HP pulse: card scales gently to draw attention
        if (_isPulsing)
        {
            _pulseTimer += Time.deltaTime * 4f;
            float pulse = 1f + 0.04f * Mathf.Sin(_pulseTimer * Mathf.PI * 2f);
            transform.localScale = Vector3.one * pulse;
        }
        else
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale, Vector3.one, Time.deltaTime * 12f);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private Color HPColour(float ratio)
    {
        if (ratio <= lowHpThreshold)
            return hpColorLow;

        if (ratio <= mediumHpThreshold)
        {
            float t = Mathf.InverseLerp(lowHpThreshold, mediumHpThreshold, ratio);
            return Color.Lerp(hpColorLow, hpColorMedium, t);
        }

        float t2 = Mathf.InverseLerp(mediumHpThreshold, 1f, ratio);
        return Color.Lerp(hpColorMedium, hpColorFull, t2);
    }
}
