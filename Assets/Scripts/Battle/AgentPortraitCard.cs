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
    Image _background;
    Image _statusBar;
    Outline _outline;
    TextMeshProUGUI _statusLabel;
    TextMeshProUGUI _selectedBadge;
    bool _chromeReady;
    static AgentController _lastTapped;
    static float _lastTapTime;

    public static readonly Color FreeColor = new Color(0.24f, 0.86f, 0.43f, 1f);
    public static readonly Color BusyColor = new Color(0.94f, 0.78f, 0.22f, 1f);
    public static readonly Color HurtColor = new Color(0.95f, 0.52f, 0.12f, 1f);
    public static readonly Color DownColor = new Color(0.86f, 0.18f, 0.16f, 1f);
    public static readonly Color SelectedColor = new Color(1f, 0.88f, 0.28f, 1f);

    public static bool IsInjured(AgentController agent)
    {
        return agent && agent.IsAlive && agent.Data != null && agent.Data.MaxHp > 0f
            && agent.CurrentHp > 0f && agent.CurrentHp <= agent.Data.MaxHp * 0.30f;
    }

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

        EnsureChrome();
        ApplyPresentation();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Show or hide the selection highlight border.</summary>
    public void SetSelected(bool selected)
    {
        ApplyPresentation();
    }

    /// <summary>One tap selects. A second tap on the same card recenters the camera on that member.</summary>
    public void NotifyTap()
    {
        bool second = _agent && _lastTapped == _agent && Time.unscaledTime - _lastTapTime <= 0.35f;
        _lastTapped = _agent;
        _lastTapTime = Time.unscaledTime;
        if (second)
        {
            GoToMember();
            return;
        }
        if (_agent && _agent.IsAlive)
            AgentSelectionManager.instance?.ToggleSelect(_agent);
    }

    void GoToMember()
    {
        if (!_agent) return;
        CameraPanTouchOnly.Instance?.FocusOn(_agent.transform.position);
        string name = _agent.Data != null ? _agent.Data.AgentName.ToUpperInvariant() : "MEMBER";
        BattleUIController.instance?.ShowAlert(name + "  ·  " + Duty(_agent), 1.8f);
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

        if (_agent.Data != null)
            RefreshHP(_agent.IsAlive ? _agent.CurrentHp : 0f, _agent.Data.MaxHp);
        ApplyPresentation();

        // Low-HP pulse: card scales gently to draw attention
        if (_isPulsing && !_agent.IsSelected)
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

    void EnsureChrome()
    {
        if (_chromeReady) return;
        _chromeReady = true;
        _background = GetComponent<Image>();
        _outline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        _outline.useGraphicAlpha = false;
        _statusBar = LandscapeUI.Image("StatusBar", transform, 0, 0, 10, 92, null, FreeColor);
        _statusBar.raycastTarget = false;
        _statusBar.transform.SetAsFirstSibling();
        _statusLabel = LandscapeUI.Text("Duty", transform, "FREE", 96, 34, 130, 18, 12, FreeColor, true);
        _selectedBadge = LandscapeUI.Text("SelectedBadge", transform, "SELECTED", 96, 70, 130, 16, 11, SelectedColor, true);
        _selectedBadge.gameObject.SetActive(false);
        if (nameLabel) LandscapeUI.Place(nameLabel.rectTransform, 96, 6, 130, 26);
        if (hpLabel) LandscapeUI.Place(hpLabel.rectTransform, 96, 50, 130, 18);
        if (hpFill) LandscapeUI.Place(hpFill.rectTransform, 96, 74, 124, 6);
        var track = transform.Find("HealthTrack") as RectTransform;
        if (track) LandscapeUI.Place(track, 96, 74, 124, 6);
    }

    void ApplyPresentation()
    {
        if (_agent == null) return;
        EnsureChrome();
        bool alive = _agent.IsAlive;
        bool selected = alive && _agent.IsSelected;
        bool hurt = IsInjured(_agent);
        bool busy = alive && _agent.IsOnAssignment;
        Color tone = !alive ? DownColor : hurt ? HurtColor : busy ? BusyColor : FreeColor;

        if (_statusBar) _statusBar.color = tone;
        if (_outline)
        {
            _outline.enabled = selected;
            _outline.effectColor = SelectedColor;
            _outline.effectDistance = new Vector2(4f, -4f);
        }
        if (_background)
            _background.color = selected
                ? new Color(0.28f, 0.22f, 0.05f, 0.98f)
                : new Color(0.07f, 0.09f, 0.11f, 0.94f);
        if (portraitImage)
            portraitImage.color = alive ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.85f);
        if (_selectedBadge) _selectedBadge.gameObject.SetActive(selected);
        if (_statusLabel)
        {
            _statusLabel.color = selected ? SelectedColor : tone;
            _statusLabel.text = selected ? "SELECTED · " + Duty(_agent) : Duty(_agent);
        }
        if (selectionGlow)
        {
            selectionGlow.gameObject.SetActive(selected);
            selectionGlow.color = SelectedColor;
        }
    }

    static string Duty(AgentController agent)
    {
        if (!agent || !agent.IsAlive) return "DOWN";
        string place = CityOperationsSystem.AssignmentFor(agent);
        if (!string.IsNullOrEmpty(place) && place.Length > 16) place = place.Substring(0, 15) + "…";
        if (IsInjured(agent))
            return string.IsNullOrEmpty(place) ? "INJURED" : "HURT · " + place;
        if (!string.IsNullOrEmpty(place)) return place;
        if (agent.IsOnAssignment)
        {
            if (agent.CurrentState == AgentController.State.AutoAttacking) return "IN A FIGHT";
            if (agent.CurrentState == AgentController.State.Retreating) return "RETREATING";
            return "ON THE WAY";
        }
        return "FREE";
    }

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
