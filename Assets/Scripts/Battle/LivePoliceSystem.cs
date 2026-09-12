using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Live police for rival fights:
///   • Patrol vans stick to a coloured road spline.
///   • Gang fight → heat 0→10 in ~4s → world freezes → car cutscene →
///     officers spawn in front of the player → bribe OR fight.
///   • Bribe deactivates police for the rest of the current level.
/// </summary>
public class LivePoliceSystem : MonoBehaviour
{
    private static LivePoliceSystem _instance;
    public static LivePoliceSystem Instance => _instance;

    public static void EnsureExists()
    {
        if (_instance != null) return;
        if (BattleManager.instance == null) return;
        if (BattleManager.instance.Mode != BattleManager.BattleMode.RivalFight) return;
        var go = new GameObject("LivePoliceSystem");
        _instance = go.AddComponent<LivePoliceSystem>();
    }

    [Header("Heat")]
    [Tooltip("Starting police heat (0-10).")]
    public int startingHeat = 8;
    public int maxHeat = 10;

    [Header("Economy / combat")]
    public int bribeBaseCost = 500;
    public int bribeCostPerWave = 350;
    public int baseOfficers = 4;
    public int officersPerWave = 1;
    public float policeHpMul = 1.55f;
    public float policeDmgMul = 1.45f;

    [Header("Cutscene")]
    public float cutsceneHeight = 14f;
    public float cutsceneHoldSeconds = 2.4f;
    public int patrolCarCount = 2;

    private enum PoliceState { Idle, Cutscene, AwaitingChoice, Fighting, BribedOff }
    private PoliceState _state = PoliceState.Idle;
    private bool _pendingArrival;
    private int _heat;
    private int _wave;
    private readonly List<EnemyController> _waveOfficers = new List<EnemyController>();
    private GameObject _responseCar;
    private readonly List<PoliceCarChaser> _patrolCars = new List<PoliceCarChaser>();
    private readonly List<GameObject> _hiddenGangs = new List<GameObject>();

    private CanvasGroup _vignetteGroup;
    private Image _flashRed, _flashBlue;
    private Image[] _heatSegments;
    private GameObject _heatBarRoot;
    private TextMeshProUGUI _heatLabel;
    private TextMeshProUGUI _heatPlusLabel;
    private CanvasGroup _heatPlusGroup;
    private GameObject _popupRoot;
    private TextMeshProUGUI _popupTitle, _popupBody;
    private Button _fightBtn, _bribeBtn;
    private TextMeshProUGUI _fightLabel, _bribeLabel;

    public IReadOnlyList<PoliceCarChaser> ActivePatrolCars => _patrolCars;
    public GameObject ResponseCar => _responseCar;
    public bool IsBribedOff => _state == PoliceState.BribedOff;
    /// <summary>True while police cutscene / bribe-fight choice is up (blocks turf prompts).</summary>
    public bool BlocksWorldPrompts =>
        _state == PoliceState.Cutscene || _state == PoliceState.AwaitingChoice;
    public bool IsAwaitingPlayerChoice => BlocksWorldPrompts || _state == PoliceState.Fighting;
    public int CurrentHeat => _heat;

    void Start()
    {
        _heat = Mathf.Clamp(startingHeat, 0, maxHeat);
        BuildUI();
        PoliceRoadSpline.EnsureExists();
        SpawnPatrolCars();
        UpdateHeatBar();
    }

    /// <summary>
    /// Kill-only heat: +1 per rival taken down. Never auto-increments over time.
    /// Shows a "+1" ping next to the bar. At 10, triggers the police arrival
    /// only after the current gang fight has finished.
    /// </summary>
    public void NotifyKill()
    {
        if (_state == PoliceState.BribedOff) return;
        if (_state == PoliceState.Cutscene || _state == PoliceState.AwaitingChoice) return;
        if (_state == PoliceState.Fighting) return; // already dealing with police
        if (_heat >= maxHeat) return;

        _heat = Mathf.Min(maxHeat, _heat + 1);
        UpdateHeatBar();
        ShowHeatPlusOne();

        if (_heat >= maxHeat && _state == PoliceState.Idle)
            TryStartPoliceArrival();
    }

    /// <summary>Called when a rival firm wipe finishes — flush deferred police arrival.</summary>
    public void NotifyGangFightEnded()
    {
        if (_pendingArrival && _state == PoliceState.Idle && _heat >= maxHeat)
            TryStartPoliceArrival();
    }

    private void TryStartPoliceArrival()
    {
        if (_state != PoliceState.Idle || _heat < maxHeat) return;
        if (BattleManager.instance != null && BattleManager.instance.IsRivalGangFightActive())
        {
            _pendingArrival = true;
            Debug.Log("[LivePolice] Heat maxed — waiting for gang fight to end before arrival.");
            return;
        }

        _pendingArrival = false;
        StartCoroutine(ArrivalRoutine());
    }

    public void NotifyLevelChanged()
    {
        StopAllCoroutines();
        RestoreHiddenGangs();
        DespawnWave();
        UnfreezeWorld();
        _heat = Mathf.Clamp(startingHeat, 0, maxHeat);
        _wave = 0;
        _pendingArrival = false;
        _state = PoliceState.Idle;
        SSetVignetteOff();
        HidePopup();
        DespawnPatrolCars();
        PoliceRoadSpline.EnsureExists()?.Build();
        SpawnPatrolCars();
        UpdateHeatBar();
    }

    void Update()
    {
        if (_state == PoliceState.BribedOff ||
            _state == PoliceState.Cutscene ||
            _state == PoliceState.AwaitingChoice)
        {
            UpdateVignette(_state == PoliceState.AwaitingChoice || _state == PoliceState.Fighting);
            return;
        }

        if (_state == PoliceState.Fighting)
        {
            UpdateVignette(true);
            MonitorWave();
            return;
        }

        // Heat already maxed during a scrap — arrive once hostiles are cleared.
        if (_pendingArrival && _state == PoliceState.Idle && _heat >= maxHeat)
        {
            if (BattleManager.instance == null || !BattleManager.instance.IsRivalGangFightActive())
                TryStartPoliceArrival();
        }
    }

    // ── Patrol ───────────────────────────────────────────────────────────
    private void SpawnPatrolCars()
    {
        var prefab = PoliceManager.instance != null ? PoliceManager.instance.policeCarPrefab : null;
        if (prefab == null) return;

        _patrolCars.RemoveAll(c => c == null);
        var spline = PoliceRoadSpline.EnsureExists();
        if (spline == null || !spline.IsReady) return;

        int want = Mathf.Clamp(patrolCarCount, 1, 3);
        int need = want - _patrolCars.Count;
        if (need <= 0) return;

        for (int i = 0; i < need; i++)
        {
            float startDist = spline.TotalLength * ((_patrolCars.Count + i) / (float)want);
            Vector3 pos = spline.GetPointAtDistance(startDist);

            var car = Instantiate(prefab, pos, Quaternion.identity);
            car.name = "PolicePatrolCar_" + _patrolCars.Count;
            var chaser = car.GetComponent<PoliceCarChaser>() ?? car.AddComponent<PoliceCarChaser>();
            // Prefab may serialize an old offset; force nose-along-travel.
            chaser.yawOffsetDegrees = -90f;
            chaser.patrolSpeed = 3.5f;
            chaser.chaseSpeed = 5f;
            chaser.Init();
            chaser.StartSplinePatrol(spline, startDist);
            _patrolCars.Add(chaser);
        }
    }

    private void DespawnPatrolCars()
    {
        foreach (var c in _patrolCars)
            if (c != null) Destroy(c.gameObject);
        _patrolCars.Clear();
    }

    // ── Arrival sequence ─────────────────────────────────────────────────
    private IEnumerator ArrivalRoutine()
    {
        _state = PoliceState.Cutscene;
        _wave++;
        _heat = maxHeat;
        UpdateHeatBar();

        // 1) Everything stops.
        FreezeWorld();

        Vector3 playerPos = BattleManager.instance.GetPlayerCentroid();
        PoliceCarChaser nearest = FindNearestPatrolCar(playerPos);

        if (nearest != null)
        {
            _responseCar = nearest.gameObject;
            _patrolCars.Remove(nearest);
            nearest.StopAndIdle();
        }
        else
        {
            SpawnResponseCarNear(playerPos);
            nearest = _responseCar != null ? _responseCar.GetComponent<PoliceCarChaser>() : null;
        }

        Vector3 carFocus = _responseCar != null ? _responseCar.transform.position : playerPos;

        // 2) Cutscene camera on the blinking patrol car.
        yield return StartCoroutine(PoliceCarCutscene(carFocus));

        // 3) Hide rival gangs — only player + police remain.
        HideRivalGangs();

        // 4) Park car beside the player and spawn stronger officers in front.
        Vector3 forward = GetPlayerForward();
        Vector3 carPark = playerPos - forward * 6f + Vector3.right * 2.5f;
        if (NavMesh.SamplePosition(carPark, out var carHit, 8f, NavMesh.AllAreas))
            carPark = carHit.position;

        if (nearest != null)
            nearest.ParkAt(carPark, forward);
        else if (_responseCar != null)
            _responseCar.transform.position = carPark;

        SpawnOfficersInFrontOfPlayer(playerPos, forward);

        // 5) Camera back to the player.
        yield return StartCoroutine(ReturnCameraToPlayer());

        // 6) Red/blue vignette + choice popup.
        SetVignette(true);
        _state = PoliceState.AwaitingChoice;
        ShowPopup();
        // Keep world frozen until the player chooses.
    }

    private PoliceCarChaser FindNearestPatrolCar(Vector3 playerPos)
    {
        PoliceCarChaser nearest = null;
        float best = float.MaxValue;
        foreach (var c in _patrolCars)
        {
            if (c == null) continue;
            float d = Vector3.SqrMagnitude(c.transform.position - playerPos);
            if (d < best) { best = d; nearest = c; }
        }
        return nearest;
    }

    private void SpawnResponseCarNear(Vector3 playerPos)
    {
        var prefab = PoliceManager.instance != null ? PoliceManager.instance.policeCarPrefab : null;
        if (prefab == null) return;
        var spline = PoliceRoadSpline.Instance;
        Vector3 origin = playerPos + Vector3.forward * 12f;
        if (spline != null && spline.IsReady)
            origin = spline.GetPointAtDistance(spline.FindNearestDistance(playerPos));
        else if (NavMesh.SamplePosition(origin, out var hit, 12f, NavMesh.AllAreas))
            origin = hit.position;

        _responseCar = Instantiate(prefab, origin, Quaternion.identity);
        var chaser = _responseCar.AddComponent<PoliceCarChaser>();
        chaser.yawOffsetDegrees = -90f;
        chaser.Init();
        chaser.StopAndIdle();
    }

    private IEnumerator PoliceCarCutscene(Vector3 focus)
    {
        var camCtl = CameraPanTouchOnly.Instance;
        var cam = Camera.main;
        if (cam == null) yield break;

        bool wasEnabled = camCtl != null && camCtl.enabled;
        if (camCtl != null) camCtl.enabled = false;

        Vector3 startPos = cam.transform.position;
        Vector3 focusPos = FrameFromRotation(cam.transform.rotation, focus, cutsceneHeight);

        yield return MoveCamRealtime(cam, startPos, focusPos, 0.7f);
        yield return new WaitForSecondsRealtime(cutsceneHoldSeconds);
        // Hold briefly — sirens keep blinking via unscaled time.

        if (camCtl != null) camCtl.enabled = wasEnabled;
    }

    private IEnumerator ReturnCameraToPlayer()
    {
        var camCtl = CameraPanTouchOnly.Instance;
        var cam = Camera.main;
        if (cam == null) yield break;

        bool wasEnabled = camCtl != null && camCtl.enabled;
        if (camCtl != null) camCtl.enabled = false;

        Vector3 player = BattleManager.instance.GetPlayerCentroid();
        Vector3 dest = FrameFromRotation(cam.transform.rotation, player, cutsceneHeight);
        yield return MoveCamRealtime(cam, cam.transform.position, dest, 0.7f);

        if (camCtl != null)
        {
            camCtl.enabled = wasEnabled;
            camCtl.CenterOnSelection();
        }
    }

    private IEnumerator MoveCamRealtime(Camera cam, Vector3 a, Vector3 b, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cam.transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / dur));
            yield return null;
        }
        cam.transform.position = b;
    }

    private static Vector3 FrameFromRotation(Quaternion rot, Vector3 target, float height)
    {
        Vector3 fwd = rot * Vector3.forward;
        Vector3 fwdXZ = new Vector3(fwd.x, 0f, fwd.z);
        float back = (fwdXZ.magnitude > 0.001f && Mathf.Abs(fwd.y) > 0.001f)
            ? height / Mathf.Abs(fwd.y) * fwdXZ.magnitude
            : 8f;
        Vector3 pos = target - fwdXZ.normalized * back;
        pos.y = height;
        return pos;
    }

    private Vector3 GetPlayerForward()
    {
        var nearest = BattleManager.instance.GetNearestAgent(BattleManager.instance.GetPlayerCentroid());
        if (nearest != null)
        {
            Vector3 f = nearest.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.01f) return f.normalized;
        }
        return Vector3.forward;
    }

    // ── Freeze / hide ────────────────────────────────────────────────────
    private void FreezeWorld()
    {
        BattleManager.instance?.SetAllAgentsCinematicIdle(true);
        foreach (var c in _patrolCars)
            if (c != null) c.StopAndIdle();
        if (CameraPanTouchOnly.Instance != null)
            CameraPanTouchOnly.Instance.enabled = false;
    }

    private void UnfreezeWorld()
    {
        BattleManager.instance?.SetAllAgentsCinematicIdle(false);
        if (CameraPanTouchOnly.Instance != null)
            CameraPanTouchOnly.Instance.enabled = true;
    }

    private void HideRivalGangs()
    {
        _hiddenGangs.Clear();
        if (BattleManager.instance == null) return;

        foreach (var e in BattleManager.instance.EnemyAgents)
        {
            if (e == null || e.firmName == "POLICE") continue;
            // Soften: make non-hostile and hide visuals.
            e.isHostile = false;
            _hiddenGangs.Add(e.gameObject);
            e.gameObject.SetActive(false);
        }

        foreach (var g in FindObjectsByType<GangArea>(FindObjectsSortMode.None))
        {
            if (g == null) continue;
            _hiddenGangs.Add(g.gameObject);
            g.gameObject.SetActive(false);
        }
    }

    private void RestoreHiddenGangs()
    {
        foreach (var go in _hiddenGangs)
            if (go != null) go.SetActive(true);
        _hiddenGangs.Clear();
    }

    // ── Choices ──────────────────────────────────────────────────────────
    private void OnFight()
    {
        HidePopup();
        UnfreezeWorld();
        // Keep gangs hidden during the police fight.
        foreach (var o in _waveOfficers)
        {
            if (o == null) continue;
            o.isHostile = true;
            var player = BattleManager.instance.GetNearestAgent(o.transform.position);
            if (player != null) o.AlertToTarget(player);
        }

        if (_responseCar != null)
        {
            var chaser = _responseCar.GetComponent<PoliceCarChaser>();
            var target = BattleManager.instance.GetNearestAgent(_responseCar.transform.position);
            if (chaser != null && target != null) chaser.SetTarget(target.transform);
        }

        BattleManager.instance.sessionHeatGained += 1;
        _state = PoliceState.Fighting;
        SetVignette(true);
    }

    private void OnBribe()
    {
        int cost = BribeCost();
        var pd = GameData.instance != null ? GameData.instance.PlayerData : null;
        if (pd == null || pd.Money < cost)
        {
            if (_bribeLabel != null) _bribeLabel.text = "NOT ENOUGH CASH";
            if (_bribeBtn != null) _bribeBtn.interactable = false;
            return;
        }

        pd.Money -= cost;
        GameData.instance.SaveData();

        HidePopup();
        DespawnWave();
        RestoreHiddenGangs();
        UnfreezeWorld();
        SSetVignetteOff();
        // Bribe cools heat to halfway (5/10) — not cleared to zero.
        _heat = Mathf.Clamp(5, 0, maxHeat);
        _state = PoliceState.BribedOff;

        foreach (var c in _patrolCars)
            if (c != null) c.StopAndIdle();

        if (pd != null)
        {
            pd.PoliceHeat = _heat;
            GameData.instance.SaveData();
        }

        UpdateHeatBar();

        GamePopup.Instance?.Show(
            "POLICE PAID OFF",
            "Heat dropped to 5. They're looking the other way for the rest of this level.",
            new GamePopup.Option("SORTED", new Color(0.15f, 0.4f, 0.7f), null)
        );
    }

    private int BribeCost() => bribeBaseCost + bribeCostPerWave * Mathf.Max(0, _wave - 1);

    private void SpawnOfficersInFrontOfPlayer(Vector3 playerPos, Vector3 forward)
    {
        int count = baseOfficers + officersPerWave * Mathf.Max(0, _wave - 1);
        var registry = PoliceManager.instance != null ? PoliceManager.instance.policeRegistry : null;
        CharacterPortraitRegistry portraits = registry != null ? registry.officerTypes : null;

        int strength = registry != null
            ? registry.baseStrengthValue + (_wave - 1) * registry.strengthPerHeatTier
            : 32 + (_wave - 1) * 5;
        float hp = (48f + strength * 0.55f) * policeHpMul;
        float dmg = (8f + strength * 0.25f) * policeDmgMul;

        GameObject prefab = (PoliceManager.instance != null && PoliceManager.instance.policePrefab != null)
            ? PoliceManager.instance.policePrefab
            : BattleManager.instance.enemyAgentPrefab;
        if (prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            float side = (i - (count - 1) * 0.5f) * 1.4f;
            Vector3 pos = playerPos + forward * (3.2f + (i % 2) * 0.6f) + Vector3.Cross(Vector3.up, forward) * side;
            if (NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas))
                pos = hit.position;

            var go = Instantiate(prefab, pos, Quaternion.LookRotation(-forward));
            var ec = go.GetComponent<EnemyController>();
            if (ec == null) continue;

            ec.patrolRadius = 5f;
            ec.detectionRadius = 35f;
            ec.Initialise(hp, dmg, 2.5f, 1.0f, portraits);
            ec.firmName = "POLICE";
            ec.isHostile = false; // armed on Fight choice
            BattleManager.instance.RegisterEnemy(ec);
            _waveOfficers.Add(ec);
        }
    }

    private void MonitorWave()
    {
        bool anyAlive = false;
        foreach (var o in _waveOfficers)
            if (o != null && o.IsAlive) { anyAlive = true; break; }

        if (!anyAlive && _popupRoot != null && !_popupRoot.activeSelf)
        {
            DespawnWave();
            RestoreHiddenGangs();
            SSetVignetteOff();
            _heat = Mathf.Clamp(startingHeat, 0, maxHeat);
            _state = PoliceState.Idle;
            UpdateHeatBar();
            if (_patrolCars.Count < patrolCarCount)
                SpawnPatrolCars();
        }
    }

    private void DespawnWave()
    {
        foreach (var o in _waveOfficers)
            if (o != null) Destroy(o.gameObject);
        _waveOfficers.Clear();
        if (_responseCar != null)
        {
            Destroy(_responseCar);
            _responseCar = null;
        }
    }

    // ── UI ───────────────────────────────────────────────────────────────
    private bool _vignetteOn;
    private void SSetVignetteOff() => _vignetteOn = false;
    private void SetVignette(bool on) => _vignetteOn = on;

    private void UpdateVignette(bool forceOn)
    {
        if (_vignetteGroup == null) return;
        bool on = _vignetteOn || forceOn;
        float heatT = maxHeat > 0 ? _heat / (float)maxHeat : 0f;
        float target = on ? 1f : (heatT > 0.7f ? (heatT - 0.7f) * 0.5f : 0f);
        _vignetteGroup.alpha = Mathf.MoveTowards(_vignetteGroup.alpha, target, Time.unscaledDeltaTime * 3f);

        if (_vignetteGroup.alpha > 0.001f)
        {
            float phase = (Mathf.Sin(Time.unscaledTime * 9f) + 1f) * 0.5f;
            if (_flashRed != null) _flashRed.color = new Color(1f, 0.08f, 0.08f, 0.22f * phase);
            if (_flashBlue != null) _flashBlue.color = new Color(0.1f, 0.3f, 1f, 0.22f * (1f - phase));
        }
    }

    private void UpdateHeatBar()
    {
        // Keep the bar visible after a bribe so the player sees the cooled 5/10 heat.
        if (_heatBarRoot != null) _heatBarRoot.SetActive(true);
        if (_heatLabel != null)
            _heatLabel.text = $"POLICE HEAT  {_heat}/{maxHeat}";

        if (_heatSegments == null) return;
        for (int i = 0; i < _heatSegments.Length; i++)
        {
            if (_heatSegments[i] == null) continue;
            bool lit = i < _heat;
            float t = i / Mathf.Max(1f, maxHeat - 1f);
            Color onCol = Color.Lerp(new Color(1f, 0.75f, 0.15f), new Color(1f, 0.12f, 0.12f), t);
            _heatSegments[i].color = lit ? onCol : new Color(0.18f, 0.18f, 0.2f, 0.85f);
        }
    }

    private void ShowHeatPlusOne()
    {
        if (_heatPlusLabel == null || _heatPlusGroup == null) return;
        StopCoroutine(nameof(HeatPlusRoutine));
        StartCoroutine(HeatPlusRoutine());
    }

    private IEnumerator HeatPlusRoutine()
    {
        _heatPlusLabel.text = "+1";
        _heatPlusGroup.alpha = 1f;
        var rt = _heatPlusLabel.rectTransform;
        Vector2 start = new Vector2(18f, 0f);
        Vector2 end = new Vector2(18f, 40f);
        float t = 0f;
        while (t < 0.9f)
        {
            t += Time.unscaledDeltaTime;
            float u = t / 0.9f;
            rt.anchoredPosition = Vector2.Lerp(start, end, u);
            _heatPlusGroup.alpha = 1f - u;
            yield return null;
        }
        _heatPlusGroup.alpha = 0f;
    }

    private void ShowPopup()
    {
        if (_popupRoot == null) return;
        _popupRoot.SetActive(true);
        if (_popupTitle != null) _popupTitle.text = "POLICE!";
        if (_popupBody != null)
            _popupBody.text = "The patrol is on you. Bribe them off for this level, or stand and fight.";
        if (_fightLabel != null) _fightLabel.text = "STAND & FIGHT";
        if (_bribeBtn != null) _bribeBtn.interactable = true;
        if (_bribeLabel != null) _bribeLabel.text = $"BRIBE (\u00A3{BribeCost():n0})";

        var pd = GameData.instance != null ? GameData.instance.PlayerData : null;
        if (pd != null && pd.Money < BribeCost())
        {
            if (_bribeBtn != null) _bribeBtn.interactable = false;
            if (_bribeLabel != null) _bribeLabel.text = "NOT ENOUGH CASH";
        }
    }

    private void HidePopup()
    {
        if (_popupRoot != null) _popupRoot.SetActive(false);
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("LivePoliceCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = LandscapeUI.Resolution;
        scaler.matchWidthOrHeight = 0.5f;

        var vig = new GameObject("Vignette", typeof(RectTransform), typeof(CanvasGroup));
        vig.transform.SetParent(canvasGo.transform, false);
        Stretch(vig.GetComponent<RectTransform>());
        _vignetteGroup = vig.GetComponent<CanvasGroup>();
        _vignetteGroup.alpha = 0f;
        _vignetteGroup.blocksRaycasts = false;
        _vignetteGroup.interactable = false;
        _flashRed = NewImage("Red", vig.transform); Stretch(_flashRed.rectTransform); _flashRed.color = new Color(1f, 0.1f, 0.1f, 0f); _flashRed.raycastTarget = false;
        _flashBlue = NewImage("Blue", vig.transform); Stretch(_flashBlue.rectTransform); _flashBlue.color = new Color(0.15f, 0.35f, 1f, 0f); _flashBlue.raycastTarget = false;

        // Compact segmented heat bar (half size) — under the full-width top HUD.
        _heatBarRoot = new GameObject("HeatBar", typeof(RectTransform));
        _heatBarRoot.transform.SetParent(canvasGo.transform, false);
        var hbr = _heatBarRoot.GetComponent<RectTransform>();
        hbr.anchorMin = hbr.anchorMax = new Vector2(0.5f, 1f);
        hbr.pivot = new Vector2(0.5f, 1f);
        hbr.anchoredPosition = new Vector2(0f, -102f);
        hbr.sizeDelta = new Vector2(320f, 32f);
        _heatBarRoot.AddComponent<Image>().color = new Color(0.05f, 0.055f, 0.07f, 0.88f);

        // Label lives INSIDE the bar (not above it) so it can't overlap MATCH/timer.
        _heatLabel = NewText("Label", _heatBarRoot.transform, $"POLICE HEAT  {_heat}/{maxHeat}", 12f, FontStyles.Bold);
        var lrt = _heatLabel.rectTransform;
        lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1);
        lrt.pivot = new Vector2(0.5f, 1); lrt.anchoredPosition = new Vector2(0, -2); lrt.sizeDelta = new Vector2(0, 12);
        _heatLabel.color = new Color(1f, 0.92f, 0.92f, 0.95f);
        _heatLabel.alignment = TextAlignmentOptions.Center;

        var row = new GameObject("Segments", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(_heatBarRoot.transform, false);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 0f);
        rowRt.anchorMax = new Vector2(1f, 0f);
        rowRt.pivot = new Vector2(0.5f, 0f);
        rowRt.anchoredPosition = new Vector2(0f, 4f);
        rowRt.sizeDelta = new Vector2(-10f, 14f);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 2f;
        hlg.padding = new RectOffset(3, 3, 1, 1);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        _heatSegments = new Image[maxHeat];
        for (int i = 0; i < maxHeat; i++)
        {
            var seg = NewImage("Seg" + i, row.transform);
            seg.color = new Color(0.18f, 0.18f, 0.2f, 0.85f);
            var le = seg.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 18f;
            le.flexibleWidth = 1f;
            le.minHeight = 12f;
            _heatSegments[i] = seg;
        }

        // "+1" ping to the right of the bar.
        var plusGo = new GameObject("HeatPlus", typeof(RectTransform), typeof(CanvasGroup));
        plusGo.transform.SetParent(_heatBarRoot.transform, false);
        _heatPlusGroup = plusGo.GetComponent<CanvasGroup>();
        _heatPlusGroup.alpha = 0f;
        _heatPlusGroup.blocksRaycasts = false;
        var plusRt = plusGo.GetComponent<RectTransform>();
        plusRt.anchorMin = plusRt.anchorMax = new Vector2(1f, 0.5f);
        plusRt.pivot = new Vector2(0f, 0.5f);
        plusRt.anchoredPosition = new Vector2(8f, 0f);
        plusRt.sizeDelta = new Vector2(40f, 20f);
        _heatPlusLabel = NewText("Plus", plusGo.transform, "+1", 14f, FontStyles.Bold);
        Stretch(_heatPlusLabel.rectTransform);
        _heatPlusLabel.alignment = TextAlignmentOptions.Left;
        _heatPlusLabel.color = new Color(1f, 0.35f, 0.2f, 1f);

        BuildPopup(canvasGo.transform);
        UpdateHeatBar();
    }

    private void BuildPopup(Transform parent)
    {
        _popupRoot = new GameObject("PolicePopup", typeof(RectTransform), typeof(Image));
        _popupRoot.transform.SetParent(parent, false);
        var pr = _popupRoot.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(720f, 360f);
        var panelImg = _popupRoot.GetComponent<Image>();
        panelImg.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(0.08f, 0.09f, 0.12f, 0.97f);

        _popupTitle = NewText("Title", _popupRoot.transform, "POLICE!", 46f, FontStyles.Bold);
        var tr = _popupTitle.rectTransform;
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = new Vector2(0, -24); tr.sizeDelta = new Vector2(-40, 70);
        _popupTitle.alignment = TextAlignmentOptions.Center;
        _popupTitle.color = new Color(1f, 0.35f, 0.35f);

        _popupBody = NewText("Body", _popupRoot.transform, "", 24f, FontStyles.Normal);
        var br = _popupBody.rectTransform;
        br.anchorMin = new Vector2(0, 0.4f); br.anchorMax = new Vector2(1, 0.75f);
        br.offsetMin = new Vector2(40, 0); br.offsetMax = new Vector2(-40, 0);
        _popupBody.alignment = TextAlignmentOptions.Center;
        _popupBody.color = new Color(0.9f, 0.92f, 0.96f);

        _fightBtn = BuildButton(_popupRoot.transform, new Vector2(-165f, -120f), new Color(0.7f, 0.15f, 0.15f, 1f), "STAND & FIGHT", out _fightLabel);
        _fightBtn.onClick.AddListener(OnFight);
        _bribeBtn = BuildButton(_popupRoot.transform, new Vector2(165f, -120f), new Color(0.15f, 0.4f, 0.7f, 1f), "BRIBE", out _bribeLabel);
        _bribeBtn.onClick.AddListener(OnBribe);

        _popupRoot.SetActive(false);
    }

    private Button BuildButton(Transform parent, Vector2 anchoredPos, Color color, string text, out TextMeshProUGUI label)
    {
        var go = new GameObject("Btn_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(300f, 90f);
        var img = go.GetComponent<Image>();
        img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = color;
        label = NewText("Label", go.transform, text, 26f, FontStyles.Bold);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return go.GetComponent<Button>();
    }

    private static Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style; t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
