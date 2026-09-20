using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 5-level campaign progression inside the gameplay scene.
///
/// Level 01 starts with 2 weak gangs (30% HP). Clearing them shows a level-complete
/// popup → short loading → next level with more/harder gangs. Progress is saved on
/// PlayerData.CurrentLevel. Defeat shows a Try Again / Back to Menu popup.
/// On difficulty jumps a power-boost offer appears for the player.
/// </summary>
public class LevelSystem : MonoBehaviour
{
    public static LevelSystem Instance { get; private set; }

    public const int MaxLevels = 5;

    // Enemy HP multiplier per level (level 1 = 30%, then ramps up).
    private static readonly float[] EnemyHpMul = { 0.30f, 0.45f, 0.60f, 0.80f, 1.00f };
    private static readonly float[] EnemyDmgMul = { 0.35f, 0.50f, 0.65f, 0.85f, 1.00f };
    private static readonly int[] GangsPerLevel = { 2, 2, 3, 3, 4 };
    private static readonly string[] LevelTitles =
    {
        "LEVEL 01", "LEVEL 02", "LEVEL 03", "LEVEL 04", "LEVEL 05"
    };
    private static readonly string[] LevelObjectives =
    {
        "Eliminate 2 rival gangs",
        "Clear 2 tougher firms",
        "Wipe out 3 rival firms",
        "Crush 3 hardened gangs",
        "Dominate 4 final firms"
    };

    private int _level = 1;
    private int _gangsCleared;
    private int _gangsRequired;
    private readonly HashSet<string> _clearedFirms = new HashSet<string>();
    private bool _transitioning;
    private bool _combatPhaseAnnounced;
    private bool _campaignCheckQueued;

    // HUD
    private TextMeshProUGUI _levelTitle;
    private TextMeshProUGUI _objectiveText;
    private Image _objectiveFill;

    public int CurrentLevel => _level;
    public float EnemyHealthMultiplier => GameplayTuning.Current.LevelValue(GameplayTuning.Current.enemyHealth, _level);
    public float EnemyDamageMultiplier => GameplayTuning.Current.LevelValue(GameplayTuning.Current.enemyDamage, _level);
    public int GangsRequired => GangsPerLevel[Mathf.Clamp(_level - 1, 0, MaxLevels - 1)];

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("LevelSystem");
        Instance = go.AddComponent<LevelSystem>();
    }

    void Awake()
    {
        Instance = this;
        var d = GameData.instance?.PlayerData;
        _level = d != null ? Mathf.Clamp(d.CurrentLevel, 1, MaxLevels) : 1;
        _gangsRequired = GangsPerLevel[_level - 1];
        _gangsCleared = 0;
    }

    void Start()
    {
        BuildHud();
        RefreshHud();
    }

    /// <summary>Called by BattleManager after a rival firm is fully wiped.</summary>
    public void OnGangEliminated(string firmName)
    {
        if(CityGameplay.HomeMode && gameObject.scene.name=="Gameplay")return;
        if (_transitioning) return;
        if (string.IsNullOrEmpty(firmName) || firmName == "POLICE") return;
        if (!_clearedFirms.Add(firmName))
        {
            Debug.Log($"[LevelSystem] Firm '{firmName}' already counted.");
            return;
        }

        _gangsCleared++;
        if(gameObject.scene.name=="Gameplay")CampaignMissions.RecordRival(GameManager.Data,firmName);
        RefreshHud();
        Debug.Log($"[LevelSystem] Gang cleared '{firmName}' → {_gangsCleared}/{_gangsRequired}");

        if(gameObject.scene.name=="Gameplay")
        {
            GameManager.Save();
            if(!_campaignCheckQueued)StartCoroutine(CampaignProgressRoutine());
        }
        else if (_gangsCleared >= _gangsRequired)StartCoroutine(LevelCompleteRoutine());
    }

    IEnumerator CampaignProgressRoutine()
    {
        _campaignCheckQueued=true;
        while(GamePopup.Instance!=null&&GamePopup.Instance.IsOpen)yield return null;
        yield return null;
        bool complete=CityGameplay.Instance?.CheckCampaignCompletion()??false;
        if(!complete&&_gangsCleared>=_gangsRequired&&!_combatPhaseAnnounced)
        {
            _combatPhaseAnnounced=true;
            var progress=CampaignMissions.Progress(GameManager.Data);
            GamePopup.Instance?.Show("RIVAL PHASE COMPLETE",
                $"The rival network is down. Mission progress: {progress.complete}/{progress.total}.\n\nFinish the remaining scouting, logistics, supporter and territory steps before returning home.",
                new GamePopup.Option("OPEN MISSION PLAN",LandscapeUI.Green,()=>CityOperationsSystem.Instance?.OpenBoard()),
                new GamePopup.Option("KEEP MOVING",LandscapeUI.PanelColor,null));
        }
        _campaignCheckQueued=false;
    }

    /// <summary>Called when the player's entire squad is wiped.</summary>
    public void OnPlayerDefeated()
    {
        if (_transitioning) return;
        _transitioning = true;
        StartCoroutine(DefeatSequence());
    }

    IEnumerator DefeatSequence()
    {
        BattleManager.instance?.SetAllAgentsCinematicIdle(true);
        GameAudio.PlayPresentationTheme("defeat");
        GameplayOutcomePresentation.DefeatCinematic();
        yield return StartCoroutine(DefeatCameraSequence());
        yield return new WaitForSecondsRealtime(.35f);
        ShowDefeatPopup();
    }

    IEnumerator DefeatCameraSequence()
    {
        var cam=Camera.main;
        if(!cam)yield break;
        var control=CameraPanTouchOnly.Instance;
        bool wasEnabled=control&&control.enabled;
        if(control)control.enabled=false;
        Vector3 target=BattleManager.instance!=null?BattleManager.instance.GetPlayerCentroid():Vector3.zero;
        Vector3 radial=cam.transform.position-target;
        if(radial.sqrMagnitude<2f)radial=new Vector3(-18f,25f,-18f);
        float elapsed=0f;
        while(elapsed<2.1f)
        {
            elapsed+=Time.unscaledDeltaTime;
            float t=Mathf.SmoothStep(0f,1f,elapsed/2.1f);
            Vector3 offset=Quaternion.Euler(0f,Mathf.Lerp(0f,54f,t),0f)*radial;
            Vector3 pos=target+offset;
            if(gameObject.scene.name=="Gameplay")pos=CameraPanTouchOnly.SafeCityPosition(target,pos);
            cam.transform.position=pos;
            cam.transform.rotation=Quaternion.LookRotation((target+Vector3.up*1.2f-pos).normalized,Vector3.up);
            yield return null;
        }
        if(control)control.enabled=wasEnabled;
    }

    // ── Level complete → next ──────────────────────────────────────────
    private IEnumerator LevelCompleteRoutine()
    {
        _transitioning = true;

        // Let the gang-wipe CONGRATS popup finish first so progress feels correct.
        while (GamePopup.Instance != null && GamePopup.Instance.IsOpen)
            yield return null;
        yield return null;

        int next = Mathf.Min(_level + 1, MaxLevels);
        CommitLevelProgress(next);

        string title = LevelTitles[Mathf.Clamp(_level - 1, 0, MaxLevels - 1)] + " COMPLETE";
        string body = LevelObjectives[Mathf.Clamp(_level - 1, 0, MaxLevels - 1)]
                      + $" — {_gangsCleared}/{_gangsRequired} sorted. Ready for the next patch?";

        GamePopup.Instance.Show(
            title,
            body,
            new GamePopup.Option("CONTINUE", new Color(0.2f, 0.6f, 0.3f), null)
        );

        // Wait until the popup is closed.
        while (GamePopup.Instance.IsOpen) yield return null;

        if (_level >= MaxLevels)
        {
            GamePopup.Instance.Show(
                "CITY DOMINATED",
                "All five levels cleared. Your firm runs these streets.",
                new GamePopup.Option("BACK TO HQ", new Color(0.25f, 0.32f, 0.4f), () =>
                {
                    GameManager.instance?.ReturnToDashboard();
                })
            );
            yield break;
        }

        // Short loading bar between levels.
        yield return StartCoroutine(InterLevelLoadBar());

        AdvanceToLevel(next);

        // Offer a power boost when difficulty jumps.
        OfferPowerBoost();
        while (GamePopup.Instance.IsOpen) yield return null;

        _transitioning = false;
    }

    private void AdvanceToLevel(int next)
    {
        int heatBefore = GameData.instance?.PlayerData?.PoliceHeat ?? 0;
        _level = next;
        _gangsCleared = 0;
        _clearedFirms.Clear();
        _gangsRequired = GangsPerLevel[_level - 1];

        var d = GameData.instance?.PlayerData;
        if (d != null)
        {
            d.CurrentLevel = _level;
            d.PoliceHeat = heatBefore;
            GameData.instance.SaveData();
        }

        // Respawn rival gangs for the new level at different nodes.
        BattleManager.instance?.RespawnLevelGangs(_level, EnemyHealthMultiplier, EnemyDamageMultiplier, _gangsRequired);
        LivePoliceSystem.Instance?.NotifyLevelChanged();
        if (d != null)
        {
            d.PoliceHeat = heatBefore;
            GameData.instance.SaveData();
        }
        RefreshHud();
    }

    private void CommitLevelProgress(int levelToSave)
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;
        d.CurrentLevel = Mathf.Clamp(levelToSave, 1, MaxLevels);
        BattleManager.instance?.PersistBattleProgress();
        GameData.instance.SaveData();
    }

    private void OfferPowerBoost()
    {
        GamePopup.Instance.Show(
            "POWER BOOST",
            "The streets get harder. Toughen up your firm — free HP & strength bump.",
            new GamePopup.Option("BOOST THE LADS", new Color(0.75f, 0.55f, 0.1f), () =>
            {
                BattleManager.instance?.ApplyPlayerPowerBoost(hpMul: 1.25f, strMul: 1.20f);
            }),
            new GamePopup.Option("SKIP", new Color(0.25f, 0.32f, 0.4f), null)
        );
    }

    private void ShowDefeatPopup()
    {
        GamePopup.Instance.Show(
            "DEFEATED",
            "Your firm got smashed. Retry from your last save, or return to HQ.",
            new GamePopup.Option("TRY AGAIN", new Color(0.7f, 0.15f, 0.15f), RetryFromLastSave),
            new GamePopup.Option("HOME HQ", new Color(0.25f, 0.32f, 0.4f), () =>
            {
                Time.timeScale = 1f;
                GamePopup.Instance.Hide();
                GameAudio.Play("popup");
                GameManager.instance?.EnterHomeTerritory();
            })
        );
    }

    /// <summary>
    /// Restore the battle-start snapshot and reload the SAME mode the player died in
    /// (away trip stays away; home stays home), keeping level + city ops progress.
    /// </summary>
    private void RetryFromLastSave()
    {
        Time.timeScale = 1f;
        GamePopup.Instance.Hide();
        GameAudio.Play("loading");

        // Capture live mode NOW — BattleStartHomeMode can be stale from an earlier HQ visit.
        bool wasHome = CityGameplay.HomeMode;
        var d = GameData.instance?.PlayerData;
        int restoreLevel = d != null
            ? Mathf.Clamp(d.CurrentLevel > 0 ? d.CurrentLevel : d.BattleStartLevel, 1, MaxLevels)
            : 1;
        if (d != null)
        {
            d.BattleStartHomeMode = wasHome;
            d.BattleStartLevel = restoreLevel;
        }

        GameData.instance?.RestoreBattleSessionSnapshot();

        _transitioning = false;
        _level = restoreLevel;
        _gangsCleared = 0;
        _clearedFirms.Clear();
        _gangsRequired = GangsPerLevel[Mathf.Clamp(_level - 1, 0, MaxLevels - 1)];

        BattleManager.instance?.StopBattleLoop();
        GameManager.instance?.RetryLastBattleSession(wasHome);
    }

    private IEnumerator InterLevelLoadBar()
    {
        var canvasGo = new GameObject("LevelLoadCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        LandscapeUI.ConfigureLandscapeScaler(canvasGo.GetComponent<CanvasScaler>());
        var group = canvasGo.GetComponent<CanvasGroup>();

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgr = bg.GetComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
        bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.92f);

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(canvasGo.transform, false);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        title.text = "LOADING " + LevelTitles[Mathf.Min(_level, MaxLevels - 1)];
        title.fontSize = 48f; title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center; title.color = Color.white;
        var tr = title.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0, 60); tr.sizeDelta = new Vector2(900, 80);

        var barBg = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
        barBg.transform.SetParent(canvasGo.transform, false);
        var barBgR = barBg.GetComponent<RectTransform>();
        barBgR.anchorMin = barBgR.anchorMax = new Vector2(0.5f, 0.5f);
        barBgR.anchoredPosition = new Vector2(0, -20); barBgR.sizeDelta = new Vector2(640, 22);
        barBg.GetComponent<Image>().color = new Color(1, 1, 1, 0.12f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(barBg.transform, false);
        var fillR = fill.GetComponent<RectTransform>();
        fillR.anchorMin = Vector2.zero; fillR.anchorMax = Vector2.one;
        fillR.offsetMin = Vector2.zero; fillR.offsetMax = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;
        fillImg.color = new Color(0.85f, 0.16f, 0.16f);

        float t = 0f;
        while (t < 1.4f)
        {
            t += Time.unscaledDeltaTime;
            fillImg.fillAmount = Mathf.Clamp01(t / 1.4f);
            yield return null;
        }

        Destroy(canvasGo);
    }

    // ── HUD ────────────────────────────────────────────────────────────
    private void BuildHud()
    {
        // Hide the scene's long OBJECTIVE banner — LevelSystem owns the objective line now.
        if (BattleUIController.instance != null && BattleUIController.instance.objectiveText != null)
            BattleUIController.instance.objectiveText.gameObject.SetActive(false);

        var canvasGo = new GameObject("LevelHudCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12000;
        LandscapeUI.ConfigureLandscapeScaler(canvasGo.GetComponent<CanvasScaler>());

        // Level / objective strip — docked top-right, clear of minimap.
        var panel = new GameObject("LevelPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(canvasGo.transform, false);
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(1f, 1f);
        pr.anchorMax = new Vector2(1f, 1f);
        pr.pivot = new Vector2(1f, 1f);
        pr.anchoredPosition = new Vector2(-24f, -128f);
        pr.sizeDelta = new Vector2(260f, 0f);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.045f, 0.055f, 0.88f);

        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _levelTitle = NewText("Title", panel.transform, "LEVEL 01", 20f, FontStyles.Bold);
        _levelTitle.alignment = TextAlignmentOptions.Left;
        _levelTitle.color = Color.white;
        var titleLe = _levelTitle.gameObject.AddComponent<LayoutElement>();
        titleLe.preferredHeight = 24f;

        _objectiveText = NewText("Obj", panel.transform, "", 17f, FontStyles.Normal);
        _objectiveText.alignment = TextAlignmentOptions.Left;
        _objectiveText.color = Color.white;
        var objLe = _objectiveText.gameObject.AddComponent<LayoutElement>();
        objLe.preferredHeight = 36f;

        var barBg = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        barBg.transform.SetParent(panel.transform, false);
        barBg.GetComponent<Image>().color = new Color(1, 1, 1, 0.14f);
        var barLe = barBg.GetComponent<LayoutElement>();
        barLe.preferredHeight = 8f;
        barLe.minHeight = 8f;

        _objectiveFill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        _objectiveFill.transform.SetParent(barBg.transform, false);
        var fr = _objectiveFill.rectTransform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
        _objectiveFill.type = Image.Type.Filled;
        _objectiveFill.fillMethod = Image.FillMethod.Horizontal;
        _objectiveFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _objectiveFill.color = new Color(0.85f, 0.16f, 0.16f);
    }

    /// <summary>Bump level HUD sizes further after the battle HUD is live.</summary>
    public void ApplyLargeHudScale()
    {
        if (_levelTitle != null) _levelTitle.fontSize = Mathf.Max(_levelTitle.fontSize, 20f);
        if (_objectiveText != null) _objectiveText.fontSize = Mathf.Max(_objectiveText.fontSize, 17f);
    }

    private void RefreshHud()
    {
        if (CityGameplay.HomeMode && gameObject.scene.name == "Gameplay")
        {
            if (_levelTitle) _levelTitle.text = "HOME";
            if (_objectiveText) _objectiveText.text = "Hold the district";
            if (_objectiveFill) _objectiveFill.fillAmount = 1;
            return;
        }
        if(gameObject.scene.name=="Gameplay")
        {
            var data=GameManager.Data;var mission=CampaignMissions.Active(data);var progress=CampaignMissions.Progress(data);
            if(_levelTitle)_levelTitle.text=$"MISSION {mission.number:00}";
            if(_objectiveText)_objectiveText.text=$"{mission.title}  {progress.complete}/{progress.total}";
            if(_objectiveFill)_objectiveFill.fillAmount=progress.total>0?(float)progress.complete/progress.total:0;
            return;
        }
        int idx = Mathf.Clamp(_level - 1, 0, MaxLevels - 1);
        if (_levelTitle != null) _levelTitle.text = LevelTitles[idx];
        if (_objectiveText != null)
            _objectiveText.text = $"{LevelObjectives[idx]}  {_gangsCleared}/{_gangsRequired}";
        if (_objectiveFill != null)
            _objectiveFill.fillAmount = _gangsRequired > 0 ? (float)_gangsCleared / _gangsRequired : 0f;

        if (BattleUIController.instance != null && BattleUIController.instance.objectiveText != null)
            BattleUIController.instance.objectiveText.gameObject.SetActive(false);
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style; t.raycastTarget = false;
        return t;
    }
}
