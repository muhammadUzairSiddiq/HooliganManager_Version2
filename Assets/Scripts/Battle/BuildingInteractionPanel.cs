using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Persistent UI panel that slides in from the bottom whenever a gang unit
/// enters a building's interaction radius.
///
/// The panel is data-driven: action buttons are generated from the
/// <see cref="MapBuildingData"/> asset at runtime, so no code changes are needed
/// for new building types.
///
/// Wire this component to the <see cref="BattleUIController"/> via
///   buildingInteractionPanel field in the Inspector.
///
/// Hierarchy expected under the assigned <c>panelRoot</c>:
///   panelRoot
///   ├── Header
///   │   ├── BuildingIcon   (Image)
///   │   ├── BuildingName   (TextMeshProUGUI)
///   │   └── BuildingDesc   (TextMeshProUGUI)
///   ├── ActionButtonContainer  (HorizontalLayoutGroup)
///   └── CloseButton        (Button)
/// </summary>
public class BuildingInteractionPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Panel Root")]
    [Tooltip("The RectTransform of the whole panel (used for slide animation).")]
    public RectTransform panelRoot;

    [Header("Header")]
    public Image            buildingIconImage;
    public TextMeshProUGUI  buildingNameText;
    public TextMeshProUGUI  buildingDescText;
    public Image            headerAccentBar;   // coloured divider line

    [Header("Action Buttons")]
    [Tooltip("Parent that holds the action buttons. Use a HorizontalLayoutGroup.")]
    public Transform        actionButtonContainer;

    [Tooltip("Prefab with: Button, Icon(Image), Label(TMP), CooldownOverlay(Image+TMP). See docs.")]
    public GameObject       actionButtonPrefab;

    [Header("Close Button")]
    public Button           closeButton;

    [Header("Animation")]
    [Tooltip("How many units the panel slides up / down when showing / hiding.")]
    public float            slideDistance = 220f;

    [Tooltip("Duration of the slide animation in seconds.")]
    public float            slideDuration = 0.25f;

    // ── Internal ──────────────────────────────────────────────────────────
    private MapBuilding              _currentBuilding;
    private List<AgentController>    _currentUnits = new List<AgentController>();
    private List<ActionButtonEntry>  _buttonEntries = new List<ActionButtonEntry>();
    private bool                     _isVisible;
    private Coroutine                _slideCoroutine;
    private Vector2                  _hiddenAnchoredPos;
    private Vector2                  _shownAnchoredPos;

    // Tracks which building is currently "locked" by the close button so that
    // a different building can still open the panel.
    private MapBuilding              _closedBuilding;

    // ─────────────────────────────────────────────────────────────────────

    private struct ActionButtonEntry
    {
        public BuildingActionConfig config;
        public Button               button;
        public TextMeshProUGUI      cooldownText;
        public Image                cooldownOverlay;
    }

    // ─────────────────────────────────────────────────────────────────────

    private bool _initialized;

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        if (panelRoot != null)
        {
            // Store shown and hidden positions (panel starts hidden off-screen).
            _shownAnchoredPos  = panelRoot.anchoredPosition;
            _hiddenAnchoredPos = _shownAnchoredPos + Vector2.down * slideDistance;
        }

        closeButton?.onClick.AddListener(OnClosePressed);
    }

    void Awake()
    {
        EnsureInitialized();
        if (!_isVisible)
        {
            if (panelRoot != null)
                panelRoot.anchoredPosition = _hiddenAnchoredPos;
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Tick cooldown overlays every frame while the panel is open.
        if (!_isVisible) return;
        RefreshCooldownDisplays();
    }

    // ── Public API (called by BattleUIController) ─────────────────────────

    /// <summary>Populates and shows the panel for the given building and nearby units.</summary>
    public void Show(MapBuilding building, List<AgentController> units)
    {
        // If the player dismissed this exact building, don't re-open it until
        // they leave and re-enter range.
        if (_closedBuilding == building) return;

        EnsureInitialized();

        _currentBuilding = building;
        _currentUnits    = units;

        PopulateHeader(building.data);
        PopulateActionButtons(building.data);

        SetPanelVisible(true, immediate: false);
    }

    /// <summary>Updates the unit list without rebuilding the panel (e.g. more units joined).</summary>
    public void RefreshUnits(MapBuilding building, List<AgentController> units)
    {
        if (_currentBuilding != building) return;
        _currentUnits = units;
    }

    /// <summary>Hides the panel. If <paramref name="building"/> doesn't match the current one, no-op.</summary>
    public void Hide(MapBuilding building)
    {
        if (_currentBuilding != building) return;

        // Reset the dismissed-building sentinel so the panel can re-open next time.
        _closedBuilding = null;
        SetPanelVisible(false, immediate: false);
        _currentBuilding = null;
        _currentUnits.Clear();
    }

    // ── Populate ──────────────────────────────────────────────────────────

    private void PopulateHeader(MapBuildingData data)
    {
        if (data == null) return;

        if (buildingNameText)  buildingNameText.text  = data.buildingName.ToUpper();
        if (buildingDescText)  buildingDescText.text  = data.buildingDescription;
        if (buildingIconImage) buildingIconImage.sprite = data.buildingIcon;
        if (buildingIconImage) buildingIconImage.enabled = data.buildingIcon != null;

        // Tint the accent bar to the building's colour
        if (headerAccentBar) headerAccentBar.color = data.buildingColor;
    }

    private void PopulateActionButtons(MapBuildingData data)
    {
        if (actionButtonContainer == null || actionButtonPrefab == null) return;

        // Clear previous buttons
        foreach (Transform child in actionButtonContainer)
            Destroy(child.gameObject);
        _buttonEntries.Clear();

        if (data?.actions == null) return;

        for (int i = 0; i < data.actions.Count; i++)
        {
            BuildingActionConfig cfg = data.actions[i];
            // Skip retired desk-bribe actions.
            if (cfg == null) continue;
            if (cfg.actionType == BuildingActionType.ReduceHeat) continue;
            if (!string.IsNullOrEmpty(cfg.actionLabel) &&
                cfg.actionLabel.IndexOf("BRIBE", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            GameObject btnGO = Instantiate(actionButtonPrefab, actionButtonContainer);

            // -- Wire button label
            var label = btnGO.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = cfg.actionLabel.ToUpper();

            // -- Wire button icon (optional child named "Icon")
            var iconImg = btnGO.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                iconImg.sprite  = cfg.actionIcon;
                iconImg.enabled = cfg.actionIcon != null;
            }

            // -- Wire cost label (optional child named "CostLabel")
            var costLabel = btnGO.transform.Find("CostLabel")?.GetComponent<TextMeshProUGUI>();
            if (costLabel != null)
                costLabel.text = cfg.costMoney > 0 ? $"£{cfg.costMoney:N0}" : "FREE";

            // -- Cooldown overlay (optional child named "CooldownOverlay")
            var cooldownOverlay = btnGO.transform.Find("CooldownOverlay")?.GetComponent<Image>();
            var cooldownText    = btnGO.transform.Find("CooldownOverlay/CooldownText")
                                       ?.GetComponent<TextMeshProUGUI>();

            // -- Wire the button click
            var btn = btnGO.GetComponent<Button>();
            int capturedIdx = i;   // capture for lambda
            btn?.onClick.AddListener(() => OnActionPressed(capturedIdx));

            // Tint the button accent to the building colour
            if (data != null)
            {
                var btnColors = btn?.colors ?? default;
                btnColors.normalColor      = data.buildingColor;
                btnColors.highlightedColor = data.buildingColor * 1.2f;
                btnColors.pressedColor     = data.buildingColor * 0.8f;
                if (btn != null) btn.colors = btnColors;
            }

            _buttonEntries.Add(new ActionButtonEntry
            {
                config          = cfg,
                button          = btn,
                cooldownText    = cooldownText,
                cooldownOverlay = cooldownOverlay
            });
        }

        RefreshCooldownDisplays();
    }

    // ── Action handling ───────────────────────────────────────────────────

    private void OnActionPressed(int index)
    {
        if (_currentBuilding == null || index >= _buttonEntries.Count) return;

        ActionButtonEntry entry = _buttonEntries[index];
        if (!entry.config.IsReady) return;

        bool success = _currentBuilding.ExecuteAction(entry.config, _currentUnits);
        if (success)
            RefreshCooldownDisplays();
    }

    private void OnClosePressed()
    {
        // Remember which building the player dismissed so we don't re-open it
        // immediately (they'd have to walk away and back in).
        _closedBuilding = _currentBuilding;

        SetPanelVisible(false, immediate: false);
        _currentBuilding = null;
        _currentUnits.Clear();
    }

    // ── Cooldown display ──────────────────────────────────────────────────

    private void RefreshCooldownDisplays()
    {
        foreach (var entry in _buttonEntries)
        {
            bool ready = entry.config.IsReady;

            if (entry.cooldownOverlay != null)
            {
                entry.cooldownOverlay.gameObject.SetActive(!ready);
                if (!ready && entry.cooldownOverlay != null)
                    entry.cooldownOverlay.fillAmount =
                        entry.config.CooldownRemaining / entry.config.cooldownSeconds;
            }

            if (entry.cooldownText != null && !ready)
                entry.cooldownText.text = $"{Mathf.CeilToInt(entry.config.CooldownRemaining)}s";

            if (entry.button != null)
                entry.button.interactable = ready;
        }
    }

    // ── Slide animation ───────────────────────────────────────────────────

    private void SetPanelVisible(bool visible, bool immediate)
    {
        _isVisible = visible;

        if (panelRoot == null)
        {
            gameObject.SetActive(visible);
            return;
        }

        if (_slideCoroutine != null)
            StopCoroutine(_slideCoroutine);

        Vector2 target = visible ? _shownAnchoredPos : _hiddenAnchoredPos;

        if (immediate)
        {
            panelRoot.anchoredPosition = target;
            gameObject.SetActive(visible);
        }
        else
        {
            if (visible) gameObject.SetActive(true);
            _slideCoroutine = StartCoroutine(SlideToPosition(target, visible));
        }
    }

    private IEnumerator SlideToPosition(Vector2 target, bool showingPanel)
    {
        Vector2 start = panelRoot.anchoredPosition;
        float   t     = 0f;

        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;
            float frac = Mathf.SmoothStep(0f, 1f, t / slideDuration);
            panelRoot.anchoredPosition = Vector2.Lerp(start, target, frac);
            yield return null;
        }

        panelRoot.anchoredPosition = target;

        // After hiding, deactivate so it doesn't block input.
        if (!showingPanel)
            gameObject.SetActive(false);
    }
}
