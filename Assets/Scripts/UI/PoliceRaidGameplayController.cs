using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the PoliceRaidGameplay panel inside the GameScene (Dashboard).
///
/// This is a SINGLE-ROUND, UI-only police-raid encounter that runs entirely
/// within the Dashboard scene — no scene load required.  After it resolves
/// (win or lose), control is handed back to GameManager:
///   • Victory → GameManager.ChainRivalFightAfterRaid() loads BattleScene
///   • Defeat   → raid is cleared, player returns to the dashboard
///
/// ── Inspector Setup ──────────────────────────────────────────────────────
/// Attach this script to the PoliceRaidGameplay root GameObject.
/// Wire every field listed below in the Inspector.
/// The root should start INACTIVE (SetActive false); Show() activates it.
///
/// ── How the mini-battle works ────────────────────────────────────────────
/// A simple timed skirmish is simulated:
///   1. Player's alive agents fight against a police squad for one round
///      (roundDuration seconds).
///   2. Each side deals damage via per-tick calculation (no 3D scene needed).
///   3. When time runs out the side with more "health" remaining wins.
///   4. If police are fully wiped early → player wins.
///   5. If player is fully wiped early → player loses.
/// </summary>
public class PoliceRaidGameplayController : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
    public static PoliceRaidGameplayController instance;

    // ── Header / intro UI ────────────────────────────────────────────────
    [Header("Header")]
    [Tooltip("Title text e.g. '⚠ POLICE RAID ⚠'")]
    public TextMeshProUGUI titleText;

    [Tooltip("Sub-line e.g. 'Survive one round — then we hit the away ground'")]
    public TextMeshProUGUI subtitleText;

    // ── Heat bar ─────────────────────────────────────────────────────────
    [Header("Heat Bar")]
    public Slider   heatSlider;
    public TextMeshProUGUI heatLabel;   // "POLICE HEAT  10 / 10"

    // ── Round timer ───────────────────────────────────────────────────────
    [Header("Round Timer")]
    public TextMeshProUGUI timerText;   // "01:30"
    public Slider          timerSlider; // fill that drains left → right

    // ── Player side ───────────────────────────────────────────────────────
    [Header("Player Side")]
    public TextMeshProUGUI playerFirmNameText;
    public TextMeshProUGUI playerCountText;   // alive agent count
    public Slider          playerHealthBar;   // aggregate HP pool
    public Image           playerHealthFill;  // colour shifts green → red

    // ── Police side ───────────────────────────────────────────────────────
    [Header("Police Side")]
    public TextMeshProUGUI policeCountText;   // alive officer count
    public Slider          policeHealthBar;
    public Image           policeHealthFill;

    // ── Outcome panel (shown after the round) ─────────────────────────────
    [Header("Outcome Panel")]
    [Tooltip("A child panel that fades in when the round ends.")]
    public GameObject outcomePanel;
    public TextMeshProUGUI outcomeHeaderText;    // 'ESCAPED!' or 'NICKED!'
    public TextMeshProUGUI outcomeBodyText;      // flavour line
    public Button          continueButton;       // proceed after reading result

    // ── Command Buttons ───────────────────────────────────────────────────
    [Header("Command Buttons")]
    [Tooltip("ATTACK button — commands selected agents to attack nearest enemy.")]
    public Button attackButton;

    [Tooltip("MOVE button — tap then tap world to move selected agents.")]
    public Button moveButton;

    [Tooltip("RETREAT button — commands selected agents to fall back.")]
    public Button retreatButton;

    // ── Selected Agent Portrait Strip (mirrors BattleUIController) ──────────
    [Header("Selected Agent Portrait Strip")]
    [Tooltip("Label showing 'SELECTED: X UNITS'. Same pattern as BattleUIController.")]
    public TextMeshProUGUI selectedUnitsLabel;

    [Tooltip("Parent Transform that holds instantiated portrait cards (max 4 shown).")]
    public Transform portraitStrip;

    [Tooltip("Portrait card prefab — must have an AgentPortraitCard component. " +
             "Same prefab used by BattleUIController.portraitCardPrefab.")]
    public GameObject portraitCardPrefab;

    [Tooltip("Status label shown when awaiting a move-tap ('TAP FIELD TO MOVE').")]
    public TextMeshProUGUI movePromptText;

    // ── Visual feedback ───────────────────────────────────────────────────
    [Header("Flash / Pulse")]
    [Tooltip("Background Image that flashes red when police deal heavy damage.")]
    public Image backgroundFlashImage;

    // ── Settings ──────────────────────────────────────────────────────────
    [Header("Round Settings")]
    [Tooltip("Duration (seconds) of the single police-raid round.")]
    public float roundDuration = 60f;

    [Tooltip("How frequently (seconds) each side deals damage ticks.")]
    public float tickInterval = 1.0f;

    [Tooltip("Scales raw agent/officer strength values down so combat lasts the full round.\n" +
             "Agent Strength values are tuned for 1-on-1 3D hits, not for pool-wide simulation.\n" +
             "Default 0.12 = ~40-50 s to first side wipe with standard stat values.")]
    [Range(0.01f, 1f)]
    public float damageScaleFactor = 0.12f;

    // ── Internal state ────────────────────────────────────────────────────
    private float _playerHp;
    private float _playerMaxHp;
    private float _policeHp;
    private float _policeMaxHp;
    private int   _playerCount;
    private int   _policeCount;
    private float _timeRemaining;
    private bool  _roundActive;

    private float _tickTimer;

    // Damage per tick: derived from agent/officer stats
    private float _playerDmgPerTick;
    private float _policeDmgPerTick;

    // Track agents lost during the raid (applied to GameData on resolution)
    private int _agentsLostInRaid = 0;

    // Original police squad size — used as the base for count estimates each tick
    // (must not be mutated like _policeCount is for display purposes)
    private int _initialPoliceCount = 0;

    // Command mode state
    private bool _awaitingMoveTarget = false;
    private Camera _cam;

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Awake is called when the object is activated (SetActive(true) from Show()).
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        _cam = Camera.main;
    }

    void OnDestroy()
    {
        // Unsubscribe selection event if registered
        if (AgentSelectionManager.instance != null)
            AgentSelectionManager.instance.OnSelectionChanged -= OnSelectionChanged;
    }

    void Update()
    {
        if (!_awaitingMoveTarget || !_roundActive) return;

        // Detect tap / click on world for move command
        bool tapped = false;
        Vector2 screenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Touchscreen.current != null)
        {
            var touch = UnityEngine.InputSystem.Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            { tapped = true; screenPos = touch.position.ReadValue(); }
        }
        if (!tapped && UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        { tapped = true; screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue(); }
#else
        if (Input.GetMouseButtonDown(0))
        { tapped = true; screenPos = Input.mousePosition; }
#endif

        if (!tapped) return;

        // Skip if the tap was on UI
        if (IsPointerOverUI(screenPos))
            return;

        _awaitingMoveTarget = false;
        if (movePromptText) movePromptText.gameObject.SetActive(false);

        if (_cam == null) _cam = Camera.main;
        Ray ray = _cam.ScreenPointToRay(screenPos);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float dist))
        {
            Vector3 worldPt = ray.GetPoint(dist);
            AgentSelectionManager.instance?.CommandSelectedMoveTo(worldPt);
        }
    }

    // ── Static locator (called by BattleManager before the panel is active) ──

    /// <summary>
    /// Finds the PoliceRaidGameplayController even while the GameObject is inactive
    /// (Unity won't fire Awake on inactive objects, so we must use FindObjectOfType
    /// with includeInactive:true).
    /// </summary>
    public static PoliceRaidGameplayController FindInScene()
    {
        if (instance != null) return instance;
        // Unity 2021+: FindObjectOfType with includeInactive flag
        var found = FindObjectOfType<PoliceRaidGameplayController>(true);
        if (found != null) instance = found;
        return found;
    }

    // ── Public entry point ────────────────────────────────────────────────

    /// <summary>
    /// Activates the panel and starts the raid round.
    /// Called by BattleManager.Start() when Mode == PoliceRaid.
    /// All police stats are read directly from PoliceDataRegistry via PoliceManager.
    /// </summary>
    public void Show()
    {
        // SetActive registers instance via Awake; make sure singleton is set
        instance = this;
        gameObject.SetActive(true);

        if (outcomePanel) outcomePanel.SetActive(false);

        // ── Read police data from PoliceDataRegistry (via PoliceManager) ──
        int currentHeat = GameData.instance?.PlayerData?.PoliceHeat ?? 10;

        int    policeCount    = 10;
        float  officerBaseHp  = 50f;
        float  officerBaseDmg = 6f;

        if (PoliceManager.instance != null && PoliceManager.instance.policeRegistry != null)
        {
            var registry = PoliceManager.instance.policeRegistry;
            int policeStrength;
            (policeCount, policeStrength) = registry.GetPoliceParams(currentHeat);

            // Use the average archetype stats from the registry for HP and damage
            var officerType = registry.GetRandomOfficerType();
            if (officerType != null)
            {
                officerBaseHp  = 40f + registry.baseStrengthValue * 0.5f;
                officerBaseDmg = registry.baseStrengthValue;
                Debug.Log($"[PoliceRaidGameplay] Officer archetype: '{officerType.modelPrefab.name}' " +
                          $"HP={officerBaseHp:F1}  DMG={officerBaseDmg:F1}  Squad={policeCount}");
            }
        }
        else
        {
            // Fallback when PoliceManager or its registry is not configured
            Debug.LogWarning("[PoliceRaidGameplay] PoliceManager/PoliceDataRegistry not found — " +
                             "using hardcoded police stats.");
        }

        // ── Build player-side stats from real 3D models ────────────────────
        _playerMaxHp = 0f;
        if (BattleManager.instance != null && BattleManager.instance.PlayerAgents != null)
        {
            foreach (var a in BattleManager.instance.PlayerAgents)
                if (a != null && a.Data != null) _playerMaxHp += a.Data.MaxHp;
        }
        else
        {
            var roster = GameData.instance?.PlayerData?.RecruitedAgents;
            if (roster != null)
                foreach (var a in roster)
                    if (a.IsAlive) _playerMaxHp += a.MaxHp;
        }

        _playerHp         = _playerMaxHp;
        _agentsLostInRaid = 0;

        // ── Build police-side stats from real 3D models ─────────────────────
        _policeMaxHp = 0f;
        if (BattleManager.instance != null && BattleManager.instance.EnemyAgents != null)
        {
            foreach (var e in BattleManager.instance.EnemyAgents)
                if (e != null) _policeMaxHp += e.MaxHp;
        }
        else
        {
            _policeMaxHp = policeCount * officerBaseHp;
        }

        _policeCount        = policeCount;
        _initialPoliceCount = policeCount;            // fixed base, never mutated
        _policeHp           = _policeMaxHp;

        // ── Derive per-tick damage ─────────────────────────────────────────
        float playerStrengthTotal = 0f;
        var rosterAgents = GameData.instance?.PlayerData?.RecruitedAgents;
        if (rosterAgents != null)
            foreach (var a in rosterAgents)
                if (a.IsAlive) playerStrengthTotal += a.Strength;

        _playerDmgPerTick = playerStrengthTotal * damageScaleFactor;
        _policeDmgPerTick = (_policeCount * officerBaseDmg) * damageScaleFactor;

        // Guard: if the player has no alive agents give them a minimal HP pool
        // so the round doesn't end instantly on frame 1.
        if (_playerMaxHp <= 0f)
        {
            _playerMaxHp = 30f;
            _playerHp    = 30f;
            Debug.LogWarning("[PoliceRaidGameplay] No alive agents found — using minimal HP floor.");
        }
        else
        {
            UpdateRealModelStats();
        }

        // Full stats dump so balance issues are obvious in the console
        Debug.Log($"[PoliceRaidGameplay] === RAID START ===\n" +
                  $"  Heat: {currentHeat}/10\n" +
                  $"  Police: {_initialPoliceCount} officers | totalHP={_policeMaxHp:F0} | dmg/tick={_policeDmgPerTick:F1}\n" +
                  $"  Player: {_playerCount} agents  | totalHP={_playerMaxHp:F0} | dmg/tick={_playerDmgPerTick:F1}\n" +
                  $"  DamageScaleFactor={damageScaleFactor}  TickInterval={tickInterval}s  Duration={roundDuration}s\n" +
                  $"  Est. police die in: {(_playerDmgPerTick > 0 ? _policeMaxHp / _playerDmgPerTick : 9999):F0} ticks\n" +
                  $"  Est. player die in: {(_policeDmgPerTick > 0 ? _playerMaxHp / _policeDmgPerTick : 9999):F0} ticks");

        // ── UI ────────────────────────────────────────────────────────────
        _timeRemaining = roundDuration;
        _tickTimer     = 0f;
        _roundActive   = true;

        RefreshUI();
        SetupHeader();

        if (continueButton) continueButton.onClick.RemoveAllListeners();
        if (continueButton) continueButton.onClick.AddListener(OnContinuePressed);

        // ── Command buttons ───────────────────────────────────────────────
        // Delegate to AgentSelectionManager — same system as BattleUIController.
        if (attackButton)  { attackButton.onClick.RemoveAllListeners();  attackButton.onClick.AddListener(OnAttackPressed);  }
        if (moveButton)    { moveButton.onClick.RemoveAllListeners();    moveButton.onClick.AddListener(OnMovePressed);    }
        if (retreatButton) { retreatButton.onClick.RemoveAllListeners(); retreatButton.onClick.AddListener(OnRetreatPressed); }

        // ── Selected portrait: clear strip and subscribe to selection events ──
        if (selectedUnitsLabel) selectedUnitsLabel.text = "";
        if (portraitStrip != null)
            for (int i = portraitStrip.childCount - 1; i >= 0; i--)
                Destroy(portraitStrip.GetChild(i).gameObject);

        if (AgentSelectionManager.instance != null)
        {
            AgentSelectionManager.instance.OnSelectionChanged -= OnSelectionChanged; // avoid double-sub
            AgentSelectionManager.instance.OnSelectionChanged += OnSelectionChanged;
        }


        StartCoroutine(RunRound());
        // NOTE: agents are already unfrozen by BattleManager before Show() is called.
        // Do NOT call SetAllAgentsCinematicIdle(true) here — that was re-freezing them.
    }

    // ── Round coroutine ───────────────────────────────────────────────────

    private IEnumerator RunRound()
    {
        while (_timeRemaining > 0f && _roundActive)
        {
            _timeRemaining -= Time.deltaTime;

            float prevPlayerHp = _playerHp;
            UpdateRealModelStats();

            // Flash screen when player team takes damage
            if (_playerHp < prevPlayerHp && backgroundFlashImage != null)
            {
                StartCoroutine(FlashBackground(new Color(0.9f, 0.1f, 0.1f, 0.3f)));
            }

            RefreshTimerUI();
            RefreshBarsUI();

            // Early-exit: if either side's HP drops to 0 (or all models defeated)
            if (_playerHp <= 0f || _policeHp <= 0f) break;

            yield return null;
        }

        _roundActive = false;

        // Determine outcome — player wins only if they still have HP AND police are gone/below them
        bool playerWon = _playerHp > 0f && _policeHp <= 0f;

        // Timer expired with both sides still standing — whoever has more HP wins
        if (_playerHp > 0f && _policeHp > 0f)
            playerWon = _playerHp > _policeHp;

        EndRound(playerWon);
    }

    /// <summary>
    /// Sums up real-time HP and counts from actual 3D models spawned in the scene.
    /// </summary>
    private void UpdateRealModelStats()
    {
        if (BattleManager.instance == null) return;

        float currentAgentHpSum = 0f;
        int aliveAgentCount = 0;
        var playerAgents = BattleManager.instance.PlayerAgents;
        if (playerAgents != null)
        {
            foreach (var a in playerAgents)
            {
                if (a != null && a.IsAlive)
                {
                    currentAgentHpSum += a.CurrentHp;
                    aliveAgentCount++;
                }
            }
            _playerHp = currentAgentHpSum;
            _playerCount = aliveAgentCount;
        }

        float currentPoliceHpSum = 0f;
        int alivePoliceCount = 0;
        var enemyAgents = BattleManager.instance.EnemyAgents;
        if (enemyAgents != null)
        {
            foreach (var e in enemyAgents)
            {
                if (e != null && e.IsAlive)
                {
                    currentPoliceHpSum += e.CurrentHp;
                    alivePoliceCount++;
                }
            }
            _policeHp = currentPoliceHpSum;
            _policeCount = alivePoliceCount;
        }
    }

    // ── Round end ─────────────────────────────────────────────────────────

    private void EndRound(bool playerWon)
    {
        // Freeze the 3D fight — outcome is decided, wait for player to tap Continue
        BattleManager.instance?.SetAllAgentsCinematicIdle(true);

        // ── Reset all player agents to full HP after the raid ─────────────────
        // Raid damage is simulated in the UI only; we restore the roster so
        // agents enter the rival fight (or next session) at full strength.
        var roster = GameData.instance?.PlayerData?.RecruitedAgents;
        if (roster != null)
            foreach (var a in roster)
                a.FullHeal();

        // Write heat result into GameData
        if (playerWon)
        {
            GameData.instance.PlayerData.PoliceHeat = Mathf.Max(5, GameData.instance.PlayerData.PoliceHeat - 2);
            Debug.Log("[PoliceRaidGameplay] Victory — heat reduced but not cleared, all agents healed.");
        }
        else
        {
            GameData.instance.PlayerData.PoliceHeat = 10;
            Debug.Log("[PoliceRaidGameplay] Defeat — heat stays 10, all agents healed.");
        }
        GameData.instance.SaveData();

        ShowOutcome(playerWon);
    }

    /// <summary>
    /// Distributes the total HP loss from the raid proportionally across
    /// alive agents in PlayerData so they carry wounds into the rival fight.
    /// </summary>
    private void ApplyRaidDamageToAgents()
    {
        var roster = GameData.instance?.PlayerData?.RecruitedAgents;
        if (roster == null || _playerMaxHp <= 0f) return;

        float damageFraction = 1f - Mathf.Clamp01(_playerHp / _playerMaxHp);
        foreach (var a in roster)
        {
            if (!a.IsAlive) continue;
            float dmg = a.MaxHp * damageFraction;
            // Apply but keep at least 1 HP so agents survive into the rival fight
            // (only kill agents if player truly lost the entire HP pool)
            float minHp = _playerHp <= 0f ? 0f : 1f;
            a.CurrentHp = Mathf.Max(minHp, a.CurrentHp - dmg);
        }

        // Update fan count for any newly downed agents
        int deadCount = 0;
        if (roster != null)
            foreach (var a in roster) if (!a.IsAlive) deadCount++;
        // Fan reduction is handled in GameData.OnBattleComplete for the rival fight;
        // we just persist the HP here.
        GameData.instance.SaveData();
    }

    // ── UI refresh ────────────────────────────────────────────────────────

    private void SetupHeader()
    {
        if (titleText) titleText.text = "⚠  POLICE RAID  ⚠";
        if (subtitleText) subtitleText.text =
            "Survive one round to reach the away ground";

        var d = GameData.instance?.PlayerData;
        if (playerFirmNameText && d != null)
            playerFirmNameText.text = d.FirmName?.ToUpper() ?? "YOUR FIRM";

        int heat = d?.PoliceHeat ?? 10;
        if (heatSlider) heatSlider.value = heat / 10f;
        if (heatLabel)  heatLabel.text   = $"POLICE HEAT  {heat} / 10";
    }

    private void RefreshUI()
    {
        RefreshBarsUI();
        RefreshTimerUI();
    }

    private void RefreshBarsUI()
    {
        // Player
        float pFrac = _playerMaxHp > 0f ? _playerHp / _playerMaxHp : 0f;
        if (playerHealthBar)  playerHealthBar.value = pFrac;
        if (playerHealthFill) playerHealthFill.color = Color.Lerp(
                new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.85f, 0.3f), pFrac);

        if (playerCountText) playerCountText.text = $"{_playerCount}";

        // Police
        float cpFrac = _policeMaxHp > 0f ? _policeHp / _policeMaxHp : 0f;
        if (policeHealthBar)  policeHealthBar.value = cpFrac;
        if (policeHealthFill) policeHealthFill.color = Color.Lerp(
                new Color(0.2f, 0.85f, 0.3f), new Color(0.1f, 0.4f, 0.9f), 1f - cpFrac);
        if (policeCountText) policeCountText.text = $"{Mathf.Max(0, _policeCount)}";
    }

    private void RefreshTimerUI()
    {
        if (timerText)
        {
            int m = (int)(_timeRemaining / 60f);
            int s = (int)(_timeRemaining % 60f);
            timerText.text = $"{m:00}:{s:00}";
        }
        if (timerSlider) timerSlider.value = Mathf.Clamp01(_timeRemaining / roundDuration);
    }

    // ── Outcome screen ────────────────────────────────────────────────────

    private void ShowOutcome(bool playerWon)
    {
        if (outcomePanel) outcomePanel.SetActive(true);

        if (outcomeHeaderText)
            outcomeHeaderText.text = playerWon ? "ESCAPED!" : "NICKED!";

        if (outcomeBodyText)
        {
            if (playerWon)
                outcomeBodyText.text = "You broke through the police line.\nNow let's get to the away ground.";
            else
                outcomeBodyText.text = "The filth had us outnumbered.\nThe boys are scattered — trip is cancelled.";
        }
    }

    // ── Command button handlers ────────────────────────────────────────────

    private void OnAttackPressed()
    {
        _awaitingMoveTarget = false;
        if (movePromptText) movePromptText.gameObject.SetActive(false);
        AgentSelectionManager.instance?.CommandSelectedAttack();
    }

    private void OnMovePressed()
    {
        _awaitingMoveTarget = true;
        if (movePromptText)
        {
            movePromptText.text = "TAP FIELD TO MOVE";
            movePromptText.gameObject.SetActive(true);
        }
    }

    private void OnRetreatPressed()
    {
        _awaitingMoveTarget = false;
        if (movePromptText) movePromptText.gameObject.SetActive(false);
        AgentSelectionManager.instance?.CommandSelectedRetreat();
    }

    // ── Selected agent portrait ───────────────────────────────────────────

    // ── Selected portrait strip (mirrors BattleUIController.RefreshSelection) ──

    /// <summary>Called by AgentSelectionManager.OnSelectionChanged event.</summary>
    private void OnSelectionChanged(List<AgentController> selected)
    {
        // ── Count label ───────────────────────────────────────────────────
        if (selectedUnitsLabel)
            selectedUnitsLabel.text = (selected != null && selected.Count > 0)
                ? $"SELECTED: {selected.Count} UNITS"
                : "";

        // ── Clear portrait strip ──────────────────────────────────────────
        if (portraitStrip != null)
            for (int i = portraitStrip.childCount - 1; i >= 0; i--)
                Destroy(portraitStrip.GetChild(i).gameObject);

        if (portraitCardPrefab == null || portraitStrip == null) return;
        if (selected == null || selected.Count == 0) return;

        // ── Instantiate up to 4 portrait cards ───────────────────────────
        int max = Mathf.Min(selected.Count, 4);
        for (int i = 0; i < max; i++)
        {
            var agent  = selected[i];
            var cardGO = Instantiate(portraitCardPrefab, portraitStrip);
            var card   = cardGO.GetComponent<AgentPortraitCard>();

            if (card != null)
            {
                // AgentPortraitCard handles portrait sprite, HP bar, name label
                card.Initialise(agent, BattleManager.instance?.portraitRegistry, agent.modelPrefab);
            }
            else
            {
                // Fallback: manually set children if no AgentPortraitCard component
                var img = cardGO.transform.Find("Portrait")?.GetComponent<Image>();
                if (img != null && agent.modelPrefab != null &&
                    BattleManager.instance?.portraitRegistry != null)
                    img.sprite = BattleManager.instance.portraitRegistry.GetPortrait(agent.modelPrefab);

                var hpBar = cardGO.transform.Find("HpBar")?.GetComponent<Image>();
                if (hpBar != null && agent.Data != null && agent.Data.MaxHp > 0f)
                    hpBar.fillAmount = agent.CurrentHp / agent.Data.MaxHp;

                var nameLabel = cardGO.GetComponentInChildren<TextMeshProUGUI>();
                if (nameLabel != null && agent.Data != null)
                    nameLabel.text = agent.Data.AgentName.ToUpper();
            }
        }
    }

    // ── Continue / cleanup ────────────────────────────────────────────────

    private void OnContinuePressed()
    {
        gameObject.SetActive(false);

        // Tear down the police environment props
        PoliceManager.instance?.SetupRaid(false);

        bool playerWon = GameData.instance?.PlayerData?.PoliceHeat == 0;

        if (playerWon && GameManager.PendingRivalAfterRaid)
        {
            // Victory — chain directly into the rival fight (reloads GameScene as RivalFight)
            GameManager.instance?.ChainRivalFightAfterRaid();
        }
        else
        {
            // Defeat or no pending rival — cancel trip, go back to Dashboard
            GameManager.PendingRivalAfterRaid = false;
            GameManager.instance?.ReturnToDashboard();
        }
    }


    // ── Flash helper ──────────────────────────────────────────────────────

    private IEnumerator FlashBackground(Color flashColor)
    {
        if (backgroundFlashImage == null) yield break;
        Color orig = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        float t = 0f, dur = 0.25f;
        // Fade in
        while (t < dur) { t += Time.deltaTime;
            backgroundFlashImage.color = Color.Lerp(orig, flashColor, t / dur); yield return null; }
        // Fade out
        t = 0f;
        while (t < dur) { t += Time.deltaTime;
            backgroundFlashImage.color = Color.Lerp(flashColor, orig, t / dur); yield return null; }
        backgroundFlashImage.color = orig;
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
}
