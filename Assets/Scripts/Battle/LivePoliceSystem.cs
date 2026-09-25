using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Live police for the living city:
///   • Patrol vans keep moving.
///   • Heat brings one skippable car shot to the group that was fighting.
///   • Bribe or fight applies only to that group. Everyone else keeps their orders.
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
        if (GameManager.IsPolicePlayer) return;
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
    private Button _fightBtn, _bribeBtn, _skipBtn;
    private TextMeshProUGUI _fightLabel, _bribeLabel;
    private bool _skipCutscene;
    private bool _arrestAnnounced;
    private readonly List<AgentController> _trouble = new List<AgentController>();

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
        if(gameObject.scene.name=="Gameplay") {cutsceneHeight=28;startingHeat=GameManager.Data?.PoliceHeat??0;}
        var pd = GameData.instance != null ? GameData.instance.PlayerData : null;
        _heat = Mathf.Clamp(pd != null ? pd.PoliceHeat : startingHeat, 0, maxHeat);
        BuildUI();
        PoliceRoadSpline.EnsureExists();
        SpawnPatrolCars();
        UpdateHeatBar();
    }

    /// <summary>
    /// Individual knockouts do not create noisy per-unit heat changes. A complete
    /// rival fight applies one predictable +4 spike in NotifyGangFightEnded.
    /// </summary>
    public void NotifyKill()
    {
        if (BattleManager.instance == null) return;
        AddHeat(1, "RIVAL DOWN - POLICE HEAT +1");
    }

    public void NotifyFightStarted()
    {
        AddHeat(1, "FIGHT STARTED - POLICE HEAT +1");
    }

    void AddHeat(int amount, string feed)
    {
        if (amount <= 0 || _state == PoliceState.BribedOff) return;
        int before = _heat;
        _heat = Mathf.Min(maxHeat, _heat + amount);
        if (_heat == before) return;
        var data = GameManager.Data;
        if (data != null) { data.PoliceHeat = _heat; GameManager.Save(); }
        UpdateHeatBar();
        CityGameplay.Instance?.PostEvent(feed);
    }

    /// <summary>Called when a rival firm wipe finishes — flush deferred police arrival.</summary>
    public void NotifyGangFightEnded()
    {
        if(_state!=PoliceState.BribedOff&&_state!=PoliceState.Fighting&&gameObject.scene.name=="Gameplay")
        {
            int before=_heat;_heat=Mathf.Min(maxHeat,_heat+BattleManager.RivalFightHeatGain);
            if(_heat!=before)
            {
                var data=GameManager.Data;if(data!=null){data.PoliceHeat=_heat;GameManager.Save();}
                UpdateHeatBar();ShowHeatPlusOne();
                CityGameplay.Instance?.PostEvent("RIVAL FIGHT COMPLETE - POLICE HEAT +4");
            }
        }
        if (_pendingArrival && _state == PoliceState.Idle && _heat >= maxHeat)
            TryStartPoliceArrival();
        else if(_state==PoliceState.Idle&&_heat>=maxHeat)TryStartPoliceArrival();
    }

    private void TryStartPoliceArrival()
    {
        if (_state != PoliceState.Idle || _heat < maxHeat) return;
        _pendingArrival = false;
        StartCoroutine(ArrivalRoutine());
    }

    public void NotifyLevelChanged()
    {
        StopAllCoroutines();
        RestoreHiddenGangs();
        DespawnWave();
        UnfreezeWorld();
        startingHeat = GameManager.Data?.PoliceHeat ?? startingHeat;
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

    private void SyncHeatFromCampaign()
    {
        if (_state == PoliceState.Fighting) return;
        var pd = GameData.instance != null ? GameData.instance.PlayerData : null;
        if (pd == null) return;
        int savedHeat = Mathf.Clamp(pd.PoliceHeat, 0, maxHeat);
        if (savedHeat == _heat) return;
        _heat = savedHeat;
        UpdateHeatBar();
    }

    void Update()
    {
        SyncHeatFromCampaign();

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
        else if(_state==PoliceState.Idle&&_heat>=maxHeat)
        {
            // City actions and matchday choices can reach 10/10 without a gang wipe.
            // A full bar must always produce a police response instead of silently persisting.
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
            car.transform.localScale *= 2f;
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
        _skipCutscene = false;
        _arrestAnnounced = false;
        UpdateHeatBar();

        _trouble.Clear();
        _trouble.AddRange(FindFightingGroup());

        Vector3 playerPos = TroubleCentroid();
        PoliceCarChaser nearest = FindNearestPatrolCar(playerPos);

        if (nearest != null)
        {
            _responseCar = nearest.gameObject;
            _patrolCars.Remove(nearest);
        }
        else
        {
            SpawnResponseCarNear(playerPos);
            nearest = _responseCar != null ? _responseCar.GetComponent<PoliceCarChaser>() : null;
        }

        Vector3 forward = TroubleForward();
        Vector3 carPark = playerPos - forward * 6f + Vector3.right * 2.5f;
        int roadMask = gameObject.scene.name == "Gameplay" ? 1 << 3 : NavMesh.AllAreas;
        if (NavMesh.SamplePosition(carPark, out var carHit, 60f, roadMask))
            carPark = carHit.position;

        if (nearest != null)
            StartCoroutine(DriveCarWithoutCamera(nearest, carPark));
        else if (_responseCar != null)
            _responseCar.transform.position = carPark;

        SpawnOfficersInFrontOfPlayer(playerPos, forward);
        SetVignette(true);
        _state = PoliceState.AwaitingChoice;
        ShowPopup();
        yield break;
    }

    IEnumerator DriveCarWithoutCamera(PoliceCarChaser car, Vector3 destination)
    {
        if (car == null) yield break;
        var drive = car.DriveArrival(destination);
        while (drive.MoveNext())
            yield return drive.Current;
    }

    private IEnumerator WaitOrSkip(float seconds)
    {
        float t = 0f;
        while (t < seconds && !_skipCutscene)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void RequestSkip() => _skipCutscene = true;

    private List<AgentController> FindFightingGroup()
    {
        var result = new List<AgentController>();
        var bm = BattleManager.instance;
        if (bm == null) return result;
        foreach (var a in bm.PlayerAgents)
            if (Eligible(a) && a.CurrentState == AgentController.State.AutoAttacking)
                result.Add(a);
        if (result.Count == 0)
        {
            foreach (var a in bm.PlayerAgents)
            {
                if (!Eligible(a)) continue;
                var enemy = bm.GetNearestEnemy(a.transform.position);
                if (enemy != null && enemy.IsAlive && enemy.firmName != "POLICE" &&
                    Vector3.Distance(a.transform.position, enemy.transform.position) <= 14f)
                    result.Add(a);
            }
        }
        if (result.Count == 0 && AgentSelectionManager.instance != null)
        {
            foreach (var a in AgentSelectionManager.instance.SelectedAgents)
                if (Eligible(a) && !result.Contains(a)) result.Add(a);
        }
        if (result.Count == 0)
        {
            AgentController nearest = null;
            float best = float.MaxValue;
            Vector3 origin = bm.GetPlayerCentroid();
            foreach (var a in bm.PlayerAgents)
            {
                if (!Eligible(a)) continue;
                float d = Vector3.SqrMagnitude(a.transform.position - origin);
                if (d < best) { best = d; nearest = a; }
            }
            if (nearest != null) result.Add(nearest);
        }
        if (result.Count > 0)
        {
            Vector3 center = Vector3.zero;
            foreach (var a in result) center += a.transform.position;
            center /= result.Count;
            foreach (var a in bm.PlayerAgents)
            {
                if (!Eligible(a) || result.Contains(a)) continue;
                if (Vector3.Distance(a.transform.position, center) <= 12f) result.Add(a);
            }
        }
        return result;
    }

    private static bool Eligible(AgentController a) => a != null && a.IsAlive && !a.IsActivityLocked;

    private void HoldTrouble(bool hold)
    {
        foreach (var a in _trouble)
            if (a != null) a.SetCinematicIdle(hold);
    }

    private Vector3 TroubleCentroid()
    {
        if (_trouble.Count == 0) return BattleManager.instance.GetPlayerCentroid();
        Vector3 c = Vector3.zero;
        int n = 0;
        foreach (var a in _trouble)
            if (a != null && a.IsAlive) { c += a.transform.position; n++; }
        return n == 0 ? BattleManager.instance.GetPlayerCentroid() : c / n;
    }

    private Vector3 TroubleForward()
    {
        foreach (var a in _trouble)
        {
            if (a == null) continue;
            Vector3 f = a.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.01f) return f.normalized;
        }
        return GetPlayerForward();
    }

    private AgentController NearestTrouble(Vector3 pos)
    {
        AgentController best = null;
        float bestD = float.MaxValue;
        foreach (var a in _trouble)
        {
            if (a == null || !a.IsAlive) continue;
            float d = Vector3.SqrMagnitude(a.transform.position - pos);
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    private bool AnyTroubleAlive()
    {
        foreach (var a in _trouble)
            if (a != null && a.IsAlive) return true;
        return false;
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
        _responseCar.transform.localScale *= 2f;
        var chaser = _responseCar.AddComponent<PoliceCarChaser>();
        chaser.yawOffsetDegrees = -90f;
        chaser.Init();
        chaser.StopAndIdle();
    }

    private IEnumerator PoliceCarCutscene(PoliceCarChaser car,Vector3 destination)
    {
        var camCtl = CameraPanTouchOnly.Instance;
        var cam = Camera.main;
        if (cam == null) yield break;

        bool wasEnabled = camCtl != null && camCtl.enabled;
        if (camCtl != null) camCtl.enabled = false;

        float frameHeight = Mathf.Max(18f, cam.transform.position.y);
        Vector3 focus=car?car.transform.position:destination;
        Vector3 focusPos=FrameFromRotation(cam.transform.rotation,focus,frameHeight);
        if(gameObject.scene.name=="Gameplay")focusPos=CameraPanTouchOnly.SafeCityPosition(focus,focusPos);
        yield return MoveCamRealtime(cam,cam.transform.position,focusPos,.45f);
        float height = Mathf.Max(18f, cam.transform.position.y);
        if(car!=null && !_skipCutscene)
        {
            var drive=car.DriveArrival(destination);
            while(drive.MoveNext() && !_skipCutscene)
            {
                Vector3 follow=FrameFromRotation(cam.transform.rotation,car.transform.position,height);
                if(gameObject.scene.name=="Gameplay")follow=CameraPanTouchOnly.SafeCityPosition(car.transform.position,follow);
                cam.transform.position=Vector3.Lerp(cam.transform.position,follow,Time.unscaledDeltaTime*6f);
                yield return drive.Current;
            }
        }
        if (_skipCutscene && car != null) car.ParkAt(destination, car.transform.forward);
        else if (!_skipCutscene) yield return WaitOrSkip(.35f);

        if (camCtl != null) camCtl.enabled = wasEnabled;
    }

    private IEnumerator PoliceOfficerCutscene(Vector3 playerPos,Vector3 forward)
    {
        var camCtl=CameraPanTouchOnly.Instance;var cam=Camera.main;if(!cam)yield break;
        bool wasEnabled=camCtl&&camCtl.enabled;if(camCtl)camCtl.enabled=false;
        var officer=_waveOfficers.FirstOrDefault(o=>o&&o.IsAlive);
        Vector3 focus=officer?officer.transform.position:playerPos+forward*3f;
        Vector3 close=FrameFromRotation(cam.transform.rotation,focus,Mathf.Max(18f,cutsceneHeight*.78f));
        if(gameObject.scene.name=="Gameplay")close=CameraPanTouchOnly.SafeCityPosition(focus,close);
        yield return MoveCamRealtime(cam,cam.transform.position,close,.35f);
        yield return new WaitForSecondsRealtime(.6f);
        if(camCtl)camCtl.enabled=wasEnabled;
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
        if(gameObject.scene.name=="Gameplay")dest=CameraPanTouchOnly.SafeCityPosition(player,dest);
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
        while (t < dur && !_skipCutscene)
        {
            t += Time.unscaledDeltaTime;
            cam.transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / dur));
            yield return null;
        }
        if (!_skipCutscene) cam.transform.position = b;
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
    private void FreezeWorld() => HoldTrouble(true);

    private void UnfreezeWorld()
    {
        HoldTrouble(false);
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
        HoldTrouble(false);
        foreach (var o in _waveOfficers)
        {
            if (o == null) continue;
            o.isHostile = true;
            var player = NearestTrouble(o.transform.position);
            if (player != null) o.AlertToTarget(player);
        }
        foreach (var a in _trouble)
        {
            if (a == null || !a.IsAlive) continue;
            EnemyController officer = null;
            float best = float.MaxValue;
            foreach (var o in _waveOfficers)
            {
                if (o == null || !o.IsAlive) continue;
                float d = Vector3.Distance(a.transform.position, o.transform.position);
                if (d < best) { best = d; officer = o; }
            }
            if (officer != null) a.CommandAttackTarget(officer);
        }

        if (_responseCar != null)
        {
            var chaser = _responseCar.GetComponent<PoliceCarChaser>();
            var target = NearestTrouble(_responseCar.transform.position);
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
        BattleUIController.instance?.ShowAlert("POLICE PAID OFF. THIS GROUP IS CLEAR.", 2.6f);
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

        if (anyAlive && !AnyTroubleAlive() && !_arrestAnnounced)
        {
            _arrestAnnounced = true;
            HoldTrouble(false);
            BattleUIController.instance?.ShowAlert("THAT GROUP WAS ARRESTED. THE REST OF THE CREW IS CLEAR.", 3.2f);
        }

        if (!anyAlive && _popupRoot != null && !_popupRoot.activeSelf)
        {
            DespawnWave();
            RestoreHiddenGangs();
            SSetVignetteOff();
            _heat = Mathf.Clamp(startingHeat, 0, maxHeat);
            _state = PoliceState.Idle;
            _trouble.Clear();
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
        _heatPlusLabel.text = "+4";
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
        HidePopup();
        int cost = BribeCost();
        var pd = GameData.instance != null ? GameData.instance.PlayerData : null;
        bool canPay = pd != null && pd.Money >= cost;
        GamePopup.Instance.Show(
            "POLICE",
            "Police are on the group that was fighting.\n\nStand and fight, or pay them to leave.\nThe rest of the city keeps moving.",
            () => { if (_state == PoliceState.AwaitingChoice) ShowPopup(); },
            new GamePopup.Option("STAND & FIGHT", new Color(0.7f, 0.15f, 0.15f), OnFight),
            new GamePopup.Option(canPay ? "BRIBE  £" + cost.ToString("N0") : "NEED £" + cost.ToString("N0"), new Color(0.15f, 0.4f, 0.7f), () =>
            {
                if (!canPay) ShowPopup();
                else OnBribe();
            }));
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
        LandscapeUI.ConfigureLandscapeScaler(canvasGo.GetComponent<CanvasScaler>());
        canvasGo.AddComponent<MobileSafeArea>();

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
        _heatPlusLabel = NewText("Plus", plusGo.transform, "+4", 14f, FontStyles.Bold);
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
        panelImg.sprite = null;
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

        TextMeshProUGUI skipLabel;
        _skipBtn = BuildButton(parent, new Vector2(0f, -280f), new Color(0.12f, 0.14f, 0.16f, 0.94f), "SKIP", out skipLabel);
        var skipRt = _skipBtn.GetComponent<RectTransform>();
        skipRt.anchorMin = skipRt.anchorMax = new Vector2(1f, 1f);
        skipRt.pivot = new Vector2(1f, 1f);
        skipRt.anchoredPosition = new Vector2(-36f, -36f);
        skipRt.sizeDelta = new Vector2(220f, 84f);
        _skipBtn.onClick.AddListener(RequestSkip);
        _skipBtn.gameObject.SetActive(false);

        _popupRoot.SetActive(false);
    }

    private void ShowSkip(bool visible)
    {
        if (_skipBtn != null) _skipBtn.gameObject.SetActive(visible);
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
        img.sprite = null;
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
