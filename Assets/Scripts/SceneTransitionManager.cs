using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Professional faded scene transitions with a loading bar.
///
/// Every scene change routes through <see cref="Transition"/>: the screen fades
/// to a themed loading overlay, the target scene loads asynchronously with a real
/// progress bar, then the overlay fades away. Built entirely at runtime and kept
/// alive across scenes (DontDestroyOnLoad), so no scene wiring is required.
///
/// For the battle scene the overlay is held until <see cref="NotifyBattleReady"/>
/// is called (after the additive map finishes loading), so players never see a
/// half-loaded map — it hands straight off to the pre-battle cutscene.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    private static SceneTransitionManager _instance;
    public static SceneTransitionManager Instance
    {
        get
        {
            if (_instance == null) Bootstrap();
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("SceneTransitionManager");
        _instance = go.AddComponent<SceneTransitionManager>();
        DontDestroyOnLoad(go);
        _instance.BuildOverlay();
    }

    // ── Tunables ───────────────────────────────────────────────────────────
    private const float FadeDuration   = 0.35f;
    private const float MinShowSeconds = 0.8f;

    // ── UI ─────────────────────────────────────────────────────────────────
    private CanvasGroup _group;
    private Image _barFill;
    private TextMeshProUGUI _percentLabel;
    private TextMeshProUGUI _titleLabel;
    private TextMeshProUGUI _subtitleLabel;

    private bool _battleHold;
    private bool _battleReady;

    private string _pendingTitle;
    private string _pendingSubtitle;

    private static readonly string[] LoadingTips =
    {
        "Keep your firm tight — lone lads get picked off.",
        "The longer you scrap, the hotter the police get.",
        "Bung the coppers a few quid to make them walk away.",
        "Take the turf, take the cash. Hold the streets.",
        "Recruit on the road to keep your numbers up.",
        "Bigger rivals, bigger reputation. Pick your fights.",
    };

    // ── Public API ───────────────────────────────────────────────────────
    /// <summary>Fade out, load the scene async with a progress bar, fade back in.</summary>
    public void Transition(string sceneName) => Transition(sceneName, null, null);

    /// <summary>Themed transition with an optional title and subtitle/tip line.</summary>
    public void Transition(string sceneName, string title, string subtitle)
    {
        bool isBattle = sceneName == "GameScene";
        _pendingTitle = string.IsNullOrEmpty(title) ? "LOADING" : title;
        _pendingSubtitle = string.IsNullOrEmpty(subtitle)
            ? LoadingTips[Random.Range(0, LoadingTips.Length)]
            : subtitle;
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(sceneName, isBattle));
    }

    /// <summary>Called by BattleManager once the map is loaded and the battle is ready.</summary>
    public void NotifyBattleReady() => _battleReady = true;

    // ── Transition flow ──────────────────────────────────────────────────
    private IEnumerator TransitionRoutine(string sceneName, bool isBattle)
    {
        _battleHold  = isBattle;
        _battleReady = false;

        if (_titleLabel != null) _titleLabel.text = _pendingTitle ?? "LOADING";
        if (_subtitleLabel != null) _subtitleLabel.text = _pendingSubtitle ?? "";

        yield return Fade(1f);            // fade to loading overlay
        SetProgress(0f);

        float startTime = Time.unscaledTime;

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            SetProgress(Mathf.Clamp01(op.progress / 0.9f) * (isBattle ? 0.7f : 1f));
            yield return null;
        }

        // Respect a minimum display time so the bar never just flashes.
        while (Time.unscaledTime - startTime < MinShowSeconds)
            yield return null;

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        if (isBattle)
        {
            // Keep the overlay up until the additive map is ready; animate the
            // last stretch of the bar so it reads as "finishing loading the map".
            float t = 0.7f;
            while (_battleHold && !_battleReady)
            {
                t = Mathf.Min(0.97f, t + Time.unscaledDeltaTime * 0.15f);
                SetProgress(t);
                yield return null;
            }
        }

        SetProgress(1f);
        yield return Fade(0f);            // reveal the new scene
    }

    private IEnumerator Fade(float targetAlpha)
    {
        _group.blocksRaycasts = targetAlpha > 0.01f;
        float start = _group.alpha;
        float t = 0f;
        while (t < FadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(start, targetAlpha, t / FadeDuration);
            yield return null;
        }
        _group.alpha = targetAlpha;
        _group.blocksRaycasts = targetAlpha > 0.01f;
    }

    private void SetProgress(float value01)
    {
        if (_barFill != null) _barFill.fillAmount = Mathf.Clamp01(value01);
        if (_percentLabel != null) _percentLabel.text = Mathf.RoundToInt(Mathf.Clamp01(value01) * 100f) + "%";
    }

    // ── Overlay construction (runtime) ─────────────────────────────────────
    private void BuildOverlay()
    {
        var canvasGo = new GameObject("TransitionCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // above everything

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        // Full-screen background
        var bg = NewImage("Background", canvasGo.transform);
        Stretch(bg.rectTransform);
        bg.color = new Color(0.055f, 0.06f, 0.08f, 1f);

        // Title
        _titleLabel = NewText("Title", canvasGo.transform, "LOADING", 64f, FontStyles.Bold);
        var tr = _titleLabel.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0f, 70f);
        tr.sizeDelta = new Vector2(1200f, 100f);
        _titleLabel.color = new Color(0.92f, 0.94f, 1f, 1f);
        _titleLabel.alignment = TextAlignmentOptions.Center;

        // Progress bar background
        var barBg = NewImage("BarBg", canvasGo.transform);
        var bbr = barBg.rectTransform;
        bbr.anchorMin = bbr.anchorMax = new Vector2(0.5f, 0.5f);
        bbr.pivot = new Vector2(0.5f, 0.5f);
        bbr.anchoredPosition = new Vector2(0f, -20f);
        bbr.sizeDelta = new Vector2(760f, 26f);
        barBg.color = new Color(1f, 1f, 1f, 0.10f);

        // Progress bar fill
        _barFill = NewImage("BarFill", barBg.transform);
        Stretch(_barFill.rectTransform);
        _barFill.type = Image.Type.Filled;
        _barFill.fillMethod = Image.FillMethod.Horizontal;
        _barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _barFill.fillAmount = 0f;
        _barFill.color = new Color(0.85f, 0.16f, 0.16f, 1f); // firm red

        // Percent label
        _percentLabel = NewText("Percent", canvasGo.transform, "0%", 26f, FontStyles.Normal);
        var pr = _percentLabel.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = new Vector2(0f, -62f);
        pr.sizeDelta = new Vector2(400f, 40f);
        _percentLabel.color = new Color(0.75f, 0.78f, 0.85f, 1f);
        _percentLabel.alignment = TextAlignmentOptions.Center;

        // Subtitle / rotating tip line, sits below the bar.
        _subtitleLabel = NewText("Subtitle", canvasGo.transform, "", 24f, FontStyles.Italic);
        var sr = _subtitleLabel.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.anchoredPosition = new Vector2(0f, -120f);
        sr.sizeDelta = new Vector2(1300f, 40f);
        _subtitleLabel.color = new Color(0.62f, 0.66f, 0.74f, 1f);
        _subtitleLabel.alignment = TextAlignmentOptions.Center;
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
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
