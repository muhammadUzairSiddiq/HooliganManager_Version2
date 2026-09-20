using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Drives the complete battle HUD for both Rival Fight and Police Raid modes.
///
/// Inspector wiring required (match to your BattleScene Canvas):
///   — Top bar, round bar, count labels, portrait strip, action buttons.
///   — resultPanel and its children for the end-of-battle screen.
/// </summary>
public class BattleUIController : MonoBehaviour
{
    public static BattleUIController instance;
    [Header("Landscape layout")]
    public bool landscapeLayout;
    [Header("Panel References")]
    public BattleResultController resultPanel; // panel prefab for showing end-of-battle results

    // ── Top Bar ───────────────────────────────────────────────────────────
    [Header("Top Bar")]
    public TextMeshProUGUI playerCountText;   // "24" with red people icon
    public TextMeshProUGUI enemyCountText;    // "18" with blue people icon
    public TextMeshProUGUI roundTimerText;    // "01:24"
    public TextMeshProUGUI roundLabel;        // "ROUND 1/3"
    public TextMeshProUGUI objectiveText;     // "OBJECTIVE: BEAT THE RIVALS"
    public Button          pauseButton;

    // ── Police Raid extras ────────────────────────────────────────────────
    [Header("Police Raid Top Bar (activate for PoliceRaid mode)")]
    public GameObject policeRaidHeader;        // "POLICE RAID! Break through..." panel
    public TextMeshProUGUI policeHeatBarLabel;
    public Image           policeHeatFill;

    // ── Bottom HUD ────────────────────────────────────────────────────────
    [Header("Bottom HUD")]
    public TextMeshProUGUI selectedUnitsLabel; // "SELECTED: 6 UNITS"
    public Transform       portraitStrip;      // parent for agent portrait cards
    public GameObject      portraitCardPrefab; // card with portrait + HP bar

    private Coroutine _alertRoutine;
    private float _alertBaseFontSize = -1f;

    // ── Action Buttons ────────────────────────────────────────────────────
    [Header("Action Buttons")]
    public Button attackBtn;   // Red fist — ATTACK
    public Button retreatBtn;  // Flag     — RETREAT
    public Button moveBtn;     // Pin      — MOVE (tap then tap world to place)

    // ── Portrait card pool ────────────────────────────────────────────────
    [Header("Portrait Sprites (Gameplay/profile 1-4)")]
    public Sprite[] portraitSprites;   // 4 sprites

    // ── Building Interaction ─────────────────────────────────────────
    [Header("Building Interaction")]
    [Tooltip("Panel that slides up when a gang unit enters a building's radius. Wire BuildingInteractionPanel component here.")]
    public BuildingInteractionPanel buildingInteractionPanel;

    // ── Internal ──────────────────────────────────────────────────────────
    private BattleManager.BattleMode _mode;
    private bool _awaitingMoveTarget;
    private Camera _cam;
    private ScrollRect _portraitScroll;
    private bool _portraitScrollReady;
    private Button _selectAllSquadToggle;
    private TextMeshProUGUI _selectAllSquadLabel;
    private const float PortraitCardW = 120f;
    private const float PortraitCardH = 140f;
    private const float PortraitSpacing = 12f;

    // CanvasGroup on this GameObject — used to fade the HUD in after the intro.
    // Add a CanvasGroup component to your BattleUIController GameObject in the
    // Inspector; if one doesn’t exist this script adds it automatically.
    private CanvasGroup _canvasGroup;

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        _cam = Camera.main;

        // Ensure a CanvasGroup exists and start the HUD invisible so it can
        // fade in cleanly after the pre-battle intro sequence finishes.
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;   // hidden until ShowHUDWithFade() is called
    }

    void Start()
    {
        // Wire buttons
        attackBtn?.onClick.AddListener(OnAttack);
        retreatBtn?.onClick.AddListener(OnRetreat);
        moveBtn?.onClick.AddListener(OnMovePressed);
        pauseButton?.onClick.AddListener(OnPause);
        SyncPauseButtonIcon(BattleManager.instance?.IsPaused ?? false);

        // Deactivate attack, retreat, and move — city HUD shows Capture/Retreat contextually.
        if (attackBtn != null) attackBtn.gameObject.SetActive(false);
        if (retreatBtn != null) retreatBtn.gameObject.SetActive(false);
        if (moveBtn != null) moveBtn.gameObject.SetActive(false);

        // Subscribe to BattleManager events
        if (BattleManager.instance != null)
        {
            BattleManager.instance.OnCountsChanged  += UpdateCounts;
            BattleManager.instance.OnRoundChanged   += UpdateRound;
            BattleManager.instance.OnBattleComplete += OnBattleEnded;
        }

        // Dynamically add off-screen indicator system
        gameObject.AddComponent<OffscreenIndicatorManager>();
    }

    void OnDestroy()
    {
        if (BattleManager.instance != null)
        {
            BattleManager.instance.OnCountsChanged  -= UpdateCounts;
            BattleManager.instance.OnRoundChanged   -= UpdateRound;
            BattleManager.instance.OnBattleComplete -= OnBattleEnded;
        }
    }

    // ── Setup ─────────────────────────────────────────────────────────────

    public void SetupHUD(BattleManager.BattleMode mode)
    {
        _mode = mode;

        if (GameManager.IsPolicePlayer)
        {
            foreach (var label in transform.root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label != null && label.name == "SquadTitle") label.text = "YOUR UNIT";
        }

        bool isRaid = mode == BattleManager.BattleMode.PoliceRaid;

        if (policeRaidHeader) policeRaidHeader.SetActive(isRaid);

        if (objectiveText)
        {
            // LevelSystem owns the objective line in rival fights — hide the
            // long banner so it doesn't stack under the timer.
            bool levelOwnsObjective = LevelSystem.Instance != null
                && mode == BattleManager.BattleMode.RivalFight;
            objectiveText.gameObject.SetActive(!levelOwnsObjective);
            if (!levelOwnsObjective)
            {
                objectiveText.text = isRaid
                    ? "OBJECTIVE: SURVIVE THE RAID AND RUN FOR THE HQ"
                    : "OBJECTIVE: SECURE TURF & RETREAT TO HQ TO ESCAPE";
            }
        }

        EnsurePortraitScrollView();

        // Populate police heat if needed
        if (isRaid)
        {
            int heat = GameData.instance?.PlayerData?.PoliceHeat ?? 0;
            if (policeHeatFill)  policeHeatFill.fillAmount = heat / 10f;
            if (policeHeatBarLabel) policeHeatBarLabel.text = $"HEAT {heat}/10";
        }
    }

    /// <summary>
    /// Fades the battle HUD in over <paramref name="duration"/> seconds.
    /// Called by BattleManager after the pre-battle intro sequence completes.
    /// </summary>
    public void ShowHUDWithFade(float duration)
    {
        if (_canvasGroup == null) return;
        StartCoroutine(FadeCanvasGroup(_canvasGroup, 0f, 1f, duration));
    }

    private System.Collections.IEnumerator FadeCanvasGroup(
        CanvasGroup cg, float from, float to, float duration)
    {
        cg.alpha = from;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    /// <summary>
    /// Receives trip targets built by BattleManager before the battle begins.
    /// Briefly flashes the objectives in the HUD objective bar (4 s),
    /// then restores the normal objective text. Full results appear on the
    /// post-match BattleResultController panel.
    /// </summary>
    public void ShowTripTargets(BattleTripTarget[] targets)
    {
        if (targets == null || targets.Length == 0) return;
        if (objectiveText)
            StartCoroutine(FlashTargetsBriefly(targets));
    }

    private System.Collections.IEnumerator FlashTargetsBriefly(BattleTripTarget[] targets)
    {
        string original = objectiveText ? objectiveText.text : "";
        if (objectiveText)
        {
            var sb = new System.Text.StringBuilder("TARGETS: ");
            for (int i = 0; i < targets.Length; i++)
            {
                if (i > 0) sb.Append("  |  ");
                sb.Append(targets[i].Label);
            }
            objectiveText.text = sb.ToString();
        }
        yield return new WaitForSeconds(4f);
        if (objectiveText) objectiveText.text = original;
    }

    // ── Round / Timer ─────────────────────────────────────────────────────

    private void UpdateRound(int round, float timeLeft)
    {
        if (roundLabel)
            roundLabel.text = "MATCH";

        if (roundTimerText)
        {
            int minutes = (int)(timeLeft / 60);
            int seconds = (int)(timeLeft % 60);
            roundTimerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    // ── Counts / Cash / Heat ──────────────────────────────────────────────

    void Update()
    {
        // Cash/heat are drawn by BattleHudLayoutFix in a non-overlapping slot.
        // Do NOT write into enemyCountText — that overlaps the MATCH timer.
    }

    private void UpdateCounts(int aliveP, int aliveE)
    {
        if (playerCountText) playerCountText.text = GameManager.IsPolicePlayer
            ? $"OFFICERS: {aliveP}"
            : $"MEMBERS: {aliveP}";
        if (landscapeLayout && enemyCountText) enemyCountText.text = $"RIVALS: {aliveE}";
        EnsureSquadSelectToggle();
    }

    // ── Selection HUD ─────────────────────────────────────────────────────

    public void RefreshSelection(List<AgentController> selected)
    {
        if (selectedUnitsLabel)
            selectedUnitsLabel.text = selected.Count == 0
                ? ""
                : selected.Count == 1 ? "1 SELECTED" : $"{selected.Count} SELECTED";

        EnsurePortraitScrollView();
        EnsureSquadSelectToggle();
        RefreshSquadSelectToggle(selected);

        // Clear portrait strip
        if (portraitStrip != null)
            for (int i = portraitStrip.childCount - 1; i >= 0; i--)
                Destroy(portraitStrip.GetChild(i).gameObject);

        if (portraitCardPrefab == null || portraitStrip == null) return;

        var allAgents = BattleManager.instance != null
            ? BattleManager.instance.PlayerAgents
            : new List<AgentController>();

        for (int i = 0; i < allAgents.Count; i++)
        {
            var agent = allAgents[i];
            if (agent == null || !agent.IsAlive) continue;
            bool isSelected = selected.Contains(agent);

            var cardGO = Instantiate(portraitCardPrefab, portraitStrip);
            SizePortraitCard(cardGO);

            var card = cardGO.GetComponent<AgentPortraitCard>();
            if (card != null)
            {
                card.Initialise(agent, BattleManager.instance.portraitRegistry, agent.modelPrefab);
                card.SetSelected(isSelected);
            }
            else
            {
                // Fallback direct setup if no AgentPortraitCard script is attached
                var img = cardGO.transform.Find("Portrait")?.GetComponent<Image>();
                if (img != null && agent.modelPrefab != null && BattleManager.instance.portraitRegistry != null)
                {
                    img.sprite = BattleManager.instance.portraitRegistry.GetPortrait(agent.modelPrefab);
                }
                var hpBar = cardGO.transform.Find("HpBar")?.GetComponent<Image>();
                if (hpBar != null && agent.Data != null)
                    hpBar.fillAmount = agent.CurrentHp / agent.Data.MaxHp;
                var nameLabel = cardGO.GetComponentInChildren<TextMeshProUGUI>();
                if (nameLabel != null && agent.Data != null)
                    nameLabel.text = agent.Data.AgentName.ToUpper();
            }

            var button = cardGO.GetComponent<Button>() ?? cardGO.AddComponent<Button>();
            button.targetGraphic = cardGO.GetComponent<Graphic>();
            LandscapeUI.Colors(button);
            button.onClick.AddListener(() => AgentSelectionManager.instance?.ToggleSelect(agent));
        }

        // Rebuild content width and keep scroll usable on touch.
        Canvas.ForceUpdateCanvases();
        if (_portraitScroll != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(portraitStrip as RectTransform);
            _portraitScroll.horizontalNormalizedPosition = 0f;
        }
    }

    private void EnsureSquadSelectToggle()
    {
        if (!landscapeLayout || _selectAllSquadToggle != null) return;
        Transform parent = playerCountText != null ? playerCountText.transform.parent : transform;
        _selectAllSquadToggle = parent.Find("SquadSelectToggle")?.GetComponent<Button>();
        if (_selectAllSquadToggle == null)
            _selectAllSquadToggle = LandscapeUI.Button("SquadSelectToggle", parent, "", 245, 171, 32, 28, "outline");
        _selectAllSquadLabel = _selectAllSquadToggle.GetComponentInChildren<TextMeshProUGUI>();
        if (_selectAllSquadLabel != null)
        {
            _selectAllSquadLabel.fontSizeMin = 10;
            _selectAllSquadLabel.fontSizeMax = 18;
        }
        _selectAllSquadToggle.onClick.AddListener(ToggleAllSquadSelection);
    }

    private void RefreshSquadSelectToggle(List<AgentController> selected)
    {
        if (_selectAllSquadLabel == null) return;
        int alive = BattleManager.instance != null ? BattleManager.instance.PlayerAgents.Count(a => a != null && a.IsAlive) : 0;
        bool allSelected = alive > 0 && selected != null && selected.Count(a => a != null && a.IsAlive) >= alive;
        _selectAllSquadLabel.text = allSelected ? "ON" : "";
    }

    private void ToggleAllSquadSelection()
    {
        int alive = BattleManager.instance != null ? BattleManager.instance.PlayerAgents.Count(a => a != null && a.IsAlive) : 0;
        int selected = AgentSelectionManager.instance != null ? AgentSelectionManager.instance.SelectedAgents.Count(a => a != null && a.IsAlive) : 0;
        if (alive > 0 && selected >= alive)
            AgentSelectionManager.instance?.DeselectAll();
        else
            AgentSelectionManager.instance?.SelectAll();
    }

    /// <summary>
    /// Wraps <see cref="portraitStrip"/> in a horizontal ScrollRect so recruiting
    /// more members adds cards you can drag through.
    /// </summary>
    private void EnsurePortraitScrollView()
    {
        if (_portraitScrollReady || portraitStrip == null) return;

        var stripRt = portraitStrip as RectTransform;
        if (stripRt == null) return;

        // Already scrolled?
        var existing = portraitStrip.GetComponentInParent<ScrollRect>();
        if (existing != null && existing.content == stripRt)
        {
            _portraitScroll = existing;
            ConfigurePortraitContent(stripRt);
            _portraitScrollReady = true;
            return;
        }

        Transform oldParent = stripRt.parent;
        int sibling = stripRt.GetSiblingIndex();
        Vector2 stripAnchorMin = stripRt.anchorMin;
        Vector2 stripAnchorMax = stripRt.anchorMax;
        Vector2 stripPivot = stripRt.pivot;
        Vector2 stripPos = stripRt.anchoredPosition;
        Vector2 stripSize = stripRt.sizeDelta;

        // Scroll root — same slot the strip occupied.
        var scrollGo = new GameObject("PortraitScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(oldParent, false);
        scrollGo.transform.SetSiblingIndex(sibling);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = stripAnchorMin;
        scrollRt.anchorMax = stripAnchorMax;
        scrollRt.pivot = stripPivot;
        scrollRt.anchoredPosition = stripPos;
        // Wide bottom strip for dragging portraits.
        scrollRt.sizeDelta = new Vector2(Mathf.Max(stripSize.x, 720f), Mathf.Max(stripSize.y, PortraitCardH + 16f));
        if (scrollRt.anchorMin.x != scrollRt.anchorMax.x)
        {
            // Stretch horizontally if the strip was full-width.
            scrollRt.offsetMin = new Vector2(40f, scrollRt.offsetMin.y);
            scrollRt.offsetMax = new Vector2(-40f, scrollRt.offsetMax.y);
            scrollRt.sizeDelta = new Vector2(scrollRt.sizeDelta.x, Mathf.Max(stripSize.y, PortraitCardH + 16f));
        }

        var scrollImg = scrollGo.GetComponent<Image>();
        scrollImg.color = new Color(0f, 0f, 0f, 0.35f);
        scrollImg.raycastTarget = true;

        // Viewport with mask
        var viewGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewGo.transform.SetParent(scrollGo.transform, false);
        var viewRt = viewGo.GetComponent<RectTransform>();
        viewRt.anchorMin = Vector2.zero;
        viewRt.anchorMax = Vector2.one;
        viewRt.offsetMin = new Vector2(8f, 6f);
        viewRt.offsetMax = new Vector2(-8f, -6f);
        var viewImg = viewGo.GetComponent<Image>();
        viewImg.color = Color.white;
        viewImg.raycastTarget = true;
        viewGo.GetComponent<Mask>().showMaskGraphic = false;

        // Content = existing portraitStrip
        stripRt.SetParent(viewGo.transform, false);
        stripRt.anchorMin = new Vector2(0f, 0.5f);
        stripRt.anchorMax = new Vector2(0f, 0.5f);
        stripRt.pivot = new Vector2(0f, 0.5f);
        stripRt.anchoredPosition = Vector2.zero;
        stripRt.sizeDelta = new Vector2(0f, PortraitCardH);

        ConfigurePortraitContent(stripRt);

        _portraitScroll = scrollGo.GetComponent<ScrollRect>();
        _portraitScroll.content = stripRt;
        _portraitScroll.viewport = viewRt;
        _portraitScroll.horizontal = true;
        _portraitScroll.vertical = false;
        _portraitScroll.movementType = ScrollRect.MovementType.Clamped;
        _portraitScroll.inertia = true;
        _portraitScroll.decelerationRate = 0.135f;
        _portraitScroll.scrollSensitivity = 24f;
        _portraitScroll.horizontalScrollbar = null;
        _portraitScroll.verticalScrollbar = null;

        _portraitScrollReady = true;
    }

    private void ConfigurePortraitContent(RectTransform stripRt)
    {
        if (landscapeLayout)
        {
            var vertical = stripRt.GetComponent<VerticalLayoutGroup>() ?? stripRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing=10; vertical.childControlWidth=true; vertical.childControlHeight=false;
            vertical.childForceExpandHeight=false; vertical.childForceExpandWidth=false;
            return;
        }
        var hlg = stripRt.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = stripRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = PortraitSpacing;
        hlg.padding = new RectOffset(8, 8, 4, 4);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var fitter = stripRt.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = stripRt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void SizePortraitCard(GameObject cardGO)
    {
        if (landscapeLayout)
        {
            var rect=cardGO.transform as RectTransform;
            if(rect) rect.sizeDelta=new Vector2(234,92);
            LandscapeUI.LayoutSize(cardGO,234,92);return;
        }
        var rt = cardGO.transform as RectTransform;
        if (rt != null)
            rt.sizeDelta = new Vector2(PortraitCardW, PortraitCardH);

        var le = cardGO.GetComponent<LayoutElement>();
        if (le == null) le = cardGO.AddComponent<LayoutElement>();
        le.preferredWidth = PortraitCardW;
        le.preferredHeight = PortraitCardH;
        le.minWidth = PortraitCardW;
        le.minHeight = PortraitCardH;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
    }

    /// <summary>Big readable mid-screen alert (turf / secure / etc).</summary>
    public void ShowAlert(string message, float seconds = 3.5f)
    {
        if (landscapeLayout)
        {
            if (!selectedUnitsLabel) return;
            selectedUnitsLabel.richText = true;
            selectedUnitsLabel.text=message; selectedUnitsLabel.fontSize=22;
            selectedUnitsLabel.color = LandscapeUI.White;
            if (_alertRoutine!=null) StopCoroutine(_alertRoutine);
            _alertRoutine=StartCoroutine(AlertTimeout(seconds));return;
        }
        if (selectedUnitsLabel == null) return;
        if (_alertBaseFontSize < 0f)
            _alertBaseFontSize = Mathf.Max(selectedUnitsLabel.fontSize, 22f);

        // Twice the normal size so ALERT lines are readable on phone.
        selectedUnitsLabel.fontSize = Mathf.Max(_alertBaseFontSize * 2f, 44f);
        selectedUnitsLabel.text = message;
        selectedUnitsLabel.enableWordWrapping = true;

        var rt = selectedUnitsLabel.rectTransform;
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 900f), Mathf.Max(rt.sizeDelta.y, 80f));
        }

        if (_alertRoutine != null) StopCoroutine(_alertRoutine);
        _alertRoutine = StartCoroutine(AlertTimeout(seconds));
    }

    private IEnumerator AlertTimeout(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (selectedUnitsLabel != null)
        {
            if (_alertBaseFontSize > 0f)
                selectedUnitsLabel.fontSize = _alertBaseFontSize;
            var sel = AgentSelectionManager.instance?.SelectedAgents;
            selectedUnitsLabel.text = sel != null && sel.Count > 0
                ? (sel.Count == 1 ? "1 SELECTED" : $"{sel.Count} SELECTED")
                : "";
        }
        _alertRoutine = null;
    }

    // ── Action Buttons ────────────────────────────────────────────────────

    private void OnAttack()
    {
        _awaitingMoveTarget = false;
        AgentSelectionManager.instance?.CommandSelectedAttack();
        ShowAlert("ATTACK ORDER CONFIRMED", 1.4f);
    }

    private void OnRetreat()
    {
        _awaitingMoveTarget = false;
        AgentSelectionManager.instance?.CommandSelectedRetreat();
        ShowAlert(CityGameplay.HomeMode ? "REGROUPING AT HEADQUARTERS" : "RETREAT ORDER CONFIRMED", 1.4f);
        if(CityGameplay.Instance && CityGameplay.HomeMode)
        {CityGameplay.Instance.PostEvent("REGROUPING AT HEADQUARTERS");return;}

        // If the units are already at the HQ, trigger escape immediately!
        if (BattleManager.instance != null && BattleManager.instance.CheckEscapeCondition())
        {
            BattleManager.instance.TriggerEscape();
        }
        else
        {
            if (selectedUnitsLabel)
                selectedUnitsLabel.text = "RETREATING TO HQ SAFE ZONE";
        }
    }

    private void OnMovePressed()
    {
        if (CityGameplay.Instance)
        {
            _awaitingMoveTarget = false;
            AgentSelectionManager.instance?.ArmMoveCommand();
            return;
        }
        // Next tap on the world will set move target
        _awaitingMoveTarget = true;
        if (selectedUnitsLabel)
            selectedUnitsLabel.text = "TAP WORLD TO MOVE";
    }

    private void OnPause()
    {
        // Toggle the pause menu panel.
        // The panel itself calls BattleManager.SetPaused(true/false).
        if (BattlePauseMenuController.instance != null)
        {
            if (BattlePauseMenuController.instance.IsVisible)
                BattlePauseMenuController.instance.Hide();
            else
                BattlePauseMenuController.instance.Show();
        }
        else
        {
            // Fallback if pause menu controller is not in scene
            bool paused = !(BattleManager.instance?.IsPaused ?? false);
            BattleManager.instance?.SetPaused(paused);
        }

        SyncPauseButtonIcon(BattleManager.instance?.IsPaused ?? false);
    }

    /// <summary>
    /// Keeps the HUD pause button (icon + label) in sync with the current pause state.
    /// Icons are generated sprites — the project fonts have no ⏸ / ▶ glyphs.
    /// Called both from OnPause and from BattlePauseMenuController when the player resumes.
    /// </summary>
    public void SyncPauseButtonIcon(bool isPaused)
    {
        if(!isPaused) gameObject.SetActive(true); // Ensure HUD is visible when unpausing
        if (!pauseButton) return;
        pauseButton.gameObject.SetActive(true);
        pauseButton.interactable = true;
        var mask = pauseButton.GetComponent<RectMask2D>();
        if (mask) mask.enabled = false;
        HudIconFactory.EnsureButtonIcon(pauseButton, isPaused ? HudIconFactory.Play() : HudIconFactory.Pause(), 26f);
        var label = pauseButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>() ?? pauseButton.GetComponentInChildren<TextMeshProUGUI>();
        if (!label)
        {
            label = LandscapeUI.Text("Label", pauseButton.transform, "", 44, 4, 100, 48, 18, Color.white, true, TextAlignmentOptions.Center);
        }
        label.text = isPaused ? "RESUME" : "PAUSE";
        label.color = Color.white;
        label.alpha = 1f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 13;
        label.fontSizeMax = 20;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.gameObject.SetActive(true);
        var icon = pauseButton.transform.Find("Icon")?.GetComponent<UnityEngine.UI.Image>();
        if (icon) { icon.color = Color.white; icon.gameObject.SetActive(true); }
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        TouchSimulation.Enable();
        Touch.onFingerDown += OnFingerDown;
    }

    void OnDisable()
    {
        Touch.onFingerDown -= OnFingerDown;
    }

    /// <summary>
    /// Called by the new Input System whenever any finger (or simulated mouse finger) touches the screen.
    /// </summary>
    private void OnFingerDown(Finger finger)
    {
        // City selection resolves release after gesture classification; never issue orders on press.
        if(CityGameplay.Instance)return;
        HandleTap(finger.screenPosition);
    }

    void HandleTap(Vector2 screenPosition)
    {
        // Handle MOVE tap on world space
        if (_awaitingMoveTarget)
        {
            if (AgentSelectionManager.BlocksWorldTap() || IsPointerOverUI(screenPosition))
                return;

            // Raycast onto the ground plane (Y = 0) for a 3D destination
            Ray ray = _cam.ScreenPointToRay(screenPosition);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                AgentSelectionManager.instance?.CommandSelectedMoveTo(worldPoint);
            }

            _awaitingMoveTarget = false;

            // Restore label
            var sel = AgentSelectionManager.instance?.SelectedAgents;
            if (selectedUnitsLabel && sel != null)
                selectedUnitsLabel.text = sel != null && sel.Count > 0
                    ? (sel.Count == 1 ? "1 SELECTED" : $"{sel.Count} SELECTED")
                    : "";
        }
    }

    // ── Battle ended — hide HUD so result screen can show cleanly ────────────

    private void OnBattleEnded(BattleResultData _)
    {
        // Hide the live HUD canvas so the result panel renders on top unobstructed
        gameObject.SetActive(false);
    }

    // ── Building Interaction API (called by MapBuilding) ──────────────────

    /// <summary>
    /// Shows the building interaction panel for the given building and nearby units.
    /// Safe to call even if buildingInteractionPanel is not wired — no-op in that case.
    /// </summary>
    public void ShowBuildingUI(MapBuilding building, System.Collections.Generic.List<AgentController> nearbyUnits)
    {
        // Never surface retired police-desk bribe UI.
        if (building != null && building.data != null)
        {
            var actions = building.data.actions;
            if (actions != null)
            {
                foreach (var a in actions)
                {
                    if (a != null && a.actionType == BuildingActionType.ReduceHeat)
                        return;
                    if (a != null && !string.IsNullOrEmpty(a.actionLabel) &&
                        a.actionLabel.IndexOf("BRIBE", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return;
                }
            }
        }
        buildingInteractionPanel?.Show(building, nearbyUnits);
    }

    /// <summary>
    /// Hides the building interaction panel. Only hides if it is currently
    /// showing for the specified building (prevents cross-building interference).
    /// </summary>
    public void HideBuildingUI(MapBuilding building)
    {
        buildingInteractionPanel?.Hide(building);
    }

    /// <summary>
    /// Refreshes the unit list in an already-open panel without rebuilding the buttons.
    /// Called each scan tick while units are in range, so the panel always acts on
    /// the up-to-date set of nearby agents.
    /// </summary>
    public void RefreshBuildingUnits(MapBuilding building, System.Collections.Generic.List<AgentController> units)
    {
        buildingInteractionPanel?.RefreshUnits(building, units);
    }

    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;
        var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            position = screenPos
        };
        var results = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    private GameObject _dynamicGangPanel;

    public void ShowGangInteractionPanel(string gangName, int playerUnits, int enemyUnits, float chance, System.Action onAttack, System.Action onRecruit, System.Action onClose)
    {
        if (_dynamicGangPanel != null)
        {
            Destroy(_dynamicGangPanel);
        }

        // Create Panel GameObject
        _dynamicGangPanel = new GameObject("GangInteractionPanel");
        _dynamicGangPanel.transform.SetParent(this.transform, false);

        // Add RectTransform and size
        RectTransform rect = _dynamicGangPanel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(650f, 450f);
        rect.anchoredPosition = Vector2.zero;

        // Background Image
        Image bg = _dynamicGangPanel.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f); // Sleek dark mode

        // Title Text
        GameObject titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(_dynamicGangPanel.transform, false);
        TextMeshProUGUI titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = $"RIVAL GANG: {gangName.ToUpper()}";
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 70f);
        titleRect.anchoredPosition = new Vector2(0f, -25f);

        // Info Text
        GameObject infoGo = new GameObject("InfoText");
        infoGo.transform.SetParent(_dynamicGangPanel.transform, false);
        TextMeshProUGUI infoText = infoGo.AddComponent<TextMeshProUGUI>();
        int chancePercent = Mathf.RoundToInt(chance * 100f);
        infoText.text = $"Your Active Crew: {playerUnits}\nTheir Gang Members: {enemyUnits}\n\nRecruit Success Chance: <b>{chancePercent}%</b>";
        infoText.fontSize = 24;
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        RectTransform infoRect = infoGo.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 0.5f);
        infoRect.anchorMax = new Vector2(1f, 0.5f);
        infoRect.pivot = new Vector2(0.5f, 0.5f);
        infoRect.sizeDelta = new Vector2(-80f, 180f);
        infoRect.anchoredPosition = new Vector2(0f, -10f);

        // Buttons Container
        GameObject btnContainer = new GameObject("Buttons");
        btnContainer.transform.SetParent(_dynamicGangPanel.transform, false);
        RectTransform containerRect = btnContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.sizeDelta = new Vector2(0f, 80f);
        containerRect.anchoredPosition = new Vector2(0f, 25f);

        // Helper to create buttons
        CreateDynamicButton(btnContainer.transform, "Recruit", new Vector2(-200f, 0f), new Vector2(180f, 60f), new Color(0.1f, 0.6f, 0.1f, 1f), () => {
            onRecruit?.Invoke();
        });

        CreateDynamicButton(btnContainer.transform, "Attack", new Vector2(0f, 0f), new Vector2(180f, 60f), new Color(0.7f, 0.1f, 0.1f, 1f), () => {
            onAttack?.Invoke();
        });

        CreateDynamicButton(btnContainer.transform, "Close", new Vector2(200f, 0f), new Vector2(180f, 60f), new Color(0.4f, 0.4f, 0.4f, 1f), () => {
            Destroy(_dynamicGangPanel);
            onClose?.Invoke();
        });
    }

    public void ShowGangResponsePanel(string title, string responseText, string buttonText, System.Action onProceed)
    {
        if (_dynamicGangPanel != null)
        {
            Destroy(_dynamicGangPanel);
        }

        // Create Panel GameObject
        _dynamicGangPanel = new GameObject("GangInteractionPanel");
        _dynamicGangPanel.transform.SetParent(this.transform, false);

        // Add RectTransform and size
        RectTransform rect = _dynamicGangPanel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(650f, 450f);
        rect.anchoredPosition = Vector2.zero;

        // Background Image
        Image bg = _dynamicGangPanel.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f); // Sleek dark mode

        // Title Text
        GameObject titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(_dynamicGangPanel.transform, false);
        TextMeshProUGUI titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = title.ToUpper();
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 70f);
        titleRect.anchoredPosition = new Vector2(0f, -25f);

        // Dialogue/Response Text
        GameObject infoGo = new GameObject("InfoText");
        infoGo.transform.SetParent(_dynamicGangPanel.transform, false);
        TextMeshProUGUI infoText = infoGo.AddComponent<TextMeshProUGUI>();
        infoText.text = $"<i>\"{responseText}\"</i>";
        infoText.fontSize = 26;
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.color = new Color(0.9f, 0.9f, 0.1f, 1f); // Vibrant dialogue yellow
        RectTransform infoRect = infoGo.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 0.5f);
        infoRect.anchorMax = new Vector2(1f, 0.5f);
        infoRect.pivot = new Vector2(0.5f, 0.5f);
        infoRect.sizeDelta = new Vector2(-80f, 180f);
        infoRect.anchoredPosition = new Vector2(0f, -10f);

        // Buttons Container
        GameObject btnContainer = new GameObject("Buttons");
        btnContainer.transform.SetParent(_dynamicGangPanel.transform, false);
        RectTransform containerRect = btnContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.sizeDelta = new Vector2(0f, 80f);
        containerRect.anchoredPosition = new Vector2(0f, 25f);

        // Single Proceed button
        CreateDynamicButton(btnContainer.transform, buttonText, new Vector2(0f, 0f), new Vector2(220f, 60f), new Color(0.2f, 0.5f, 0.9f, 1f), () => {
            Destroy(_dynamicGangPanel);
            onProceed?.Invoke();
        });
    }

    private void CreateDynamicButton(Transform parent, string textStr, Vector2 pos, Vector2 size, Color bgColor, System.Action onClickAction)
    {
        GameObject btnGo = new GameObject(textStr + "Button");
        btnGo.transform.SetParent(parent, false);
        RectTransform btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.sizeDelta = size;
        btnRect.anchoredPosition = pos;

        Image img = btnGo.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(() => onClickAction?.Invoke());

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        TextMeshProUGUI btnText = textGo.AddComponent<TextMeshProUGUI>();
        btnText.text = textStr;
        btnText.fontSize = 22;
        btnText.fontStyle = FontStyles.Bold;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;

        RectTransform txtRect = textGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
    }
}
