using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LandscapeScreenMotion : MonoBehaviour
{
    public LandscapeFrontEnd shell;
    public RectTransform background;
    public TextMeshProUGUI rotatingText;

    readonly string[] lines =
    {
        "One crew. Every street. A reputation to build.",
        "Recruit smart. Travel sharp. Come home louder.",
        "Win the block, lift morale, climb the table.",
        "Cash keeps the doors open. Loyalty keeps the firm alive.",
        "Every matchday is a chance to take more ground."
    };

    Coroutine pageRoutine;
    Coroutine textRoutine;
    Coroutine backgroundRoutine;
    Vector3 bgBaseScale = Vector3.one;
    Image backgroundImage;
    Image backgroundBlackout;
    Sprite[] backgroundCycle;

    void Awake()
    {
        if (!shell) shell = GetComponent<LandscapeFrontEnd>();
        if (!background)
        {
            var canvas = GetComponentInParent<Canvas>();
            background = canvas ? canvas.transform.Find("Backdrop") as RectTransform : null;
        }
        if (background) bgBaseScale = background.localScale;
        if (background) backgroundImage = background.GetComponent<Image>();
        var theme = LandscapeTheme.Current;
        if (theme && theme.presentationBackgrounds != null && theme.presentationBackgrounds.Length > 0)
            backgroundCycle = theme.presentationBackgrounds;
        else if (theme)
            backgroundCycle = new[] { theme.mainBackground, theme.townBackground, theme.tacticalBackground };
    }

    void OnEnable()
    {
        if (backgroundRoutine != null) StopCoroutine(backgroundRoutine);
        if (backgroundImage && backgroundCycle != null && backgroundCycle.Length > 0)
            backgroundRoutine = StartCoroutine(CycleBackground());
    }

    void OnDisable()
    {
        if (backgroundRoutine != null) StopCoroutine(backgroundRoutine);
        backgroundRoutine = null;
    }

    void Update() { }

    public void ShowPage(GameObject[] pages, string[] names, string action)
    {
        int index = System.Array.IndexOf(names, action);
        if (index < 0 || index >= pages.Length || !pages[index]) return;
        for (int i = 0; i < pages.Length; i++)
            if (pages[i]) pages[i].SetActive(i == index);
        HighlightButtons(names[index]);
    }

    IEnumerator SwitchPage(GameObject[] pages, string[] names, int target)
    {
        GameObject outgoing = null;
        for (int i = 0; i < pages.Length; i++)
            if (i != target && pages[i] && pages[i].activeSelf) outgoing = pages[i];

        GameObject incoming = pages[target];
        incoming.SetActive(true);
        var incomingRt = (RectTransform)incoming.transform;
        var incomingGroup = EnsureGroup(incoming);
        Vector2 baseIn = BasePosition(incomingRt);
        Vector2 start = baseIn + DirectionFor(target) * 58f;
        incomingRt.anchoredPosition = start;
        incomingRt.localScale = Vector3.one * .975f;
        incomingGroup.alpha = 0;

        float duration = .34f;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = Ease(t / duration);
            incomingRt.anchoredPosition = Vector2.LerpUnclamped(start, Vector2.zero, p);
            incomingRt.localScale = Vector3.LerpUnclamped(Vector3.one * .975f, Vector3.one, p);
            incomingGroup.alpha = p;
            if (outgoing)
            {
                var outRt = (RectTransform)outgoing.transform;
                var outGroup = EnsureGroup(outgoing);
                Vector2 baseOut = BasePosition(outRt);
                outRt.anchoredPosition = Vector2.LerpUnclamped(baseOut, baseOut - DirectionFor(target) * 20f, p);
                outRt.localScale = Vector3.LerpUnclamped(Vector3.one, Vector3.one * .99f, p);
                outGroup.alpha = 1f - p;
            }
            yield return null;
        }

        incomingRt.anchoredPosition = baseIn;
        incomingRt.localScale = Vector3.one;
        incomingGroup.alpha = 1;
        for (int i = 0; i < pages.Length; i++)
            if (i != target && pages[i])
            {
                pages[i].SetActive(false);
                var rt = (RectTransform)pages[i].transform;
                rt.anchoredPosition = BasePosition(rt);
                rt.localScale = Vector3.one;
                EnsureGroup(pages[i]).alpha = 1;
            }

        HighlightButtons(names[target]);
    }

    public void HighlightButtons(string page)
    {
        var actions = GetComponentsInChildren<LandscapeAction>(true);
        foreach (var action in actions)
        {
            var polish = action.GetComponent<LandscapeButtonPolish>();
            if (!polish) continue;
            bool selectableNav = action.gameObject.name.StartsWith("Nav_") || action.gameObject.name.StartsWith("Tab_");
            bool selected = selectableNav && IsSelected(action.action, page);
            polish.SetSelected(selected);
        }
    }

    static bool IsSelected(string action, string page)
    {
        if (action == page) return true;
        if (page == "home" && action == "home") return true;
        if (page == "missions" && (action == "campaign" || action == "matchday")) return true;
        if (page == "squad" && action.StartsWith("squad-")) return true;
        return false;
    }

    IEnumerator RotateText()
    {
        int index = 0;
        while (true)
        {
            yield return new WaitForSecondsRealtime(5.5f);
            index = (index + 1) % lines.Length;
            yield return FadeText(lines[index]);
        }
    }

    IEnumerator CycleBackground()
    {
        int index = Random.Range(0, backgroundCycle.Length);
        ShowBackground(backgroundImage, backgroundCycle[index]);
        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(3f, 6f));
            int next = (index + 1) % backgroundCycle.Length;
            Sprite sprite = backgroundCycle[next];
            if (!sprite) { yield return null; continue; }
            yield return BlackoutTo(sprite);
            index = next;
        }
    }

    IEnumerator BlackoutTo(Sprite next)
    {
        EnsureBlackout();
        if (!backgroundBlackout) yield break;
        const float outTime = 0.62f;
        const float hold = 0.14f;
        const float inTime = 0.78f;
        for (float t = 0f; t < outTime; t += Time.unscaledDeltaTime)
        {
            backgroundBlackout.color = new Color(0f, 0f, 0f, BlackoutEase(t / outTime));
            yield return null;
        }
        backgroundBlackout.color = Color.black;
        ShowBackground(backgroundImage, next);
        yield return new WaitForSecondsRealtime(hold);
        for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime)
        {
            backgroundBlackout.color = new Color(0f, 0f, 0f, 1f - BlackoutEase(t / inTime));
            yield return null;
        }
        backgroundBlackout.color = new Color(0f, 0f, 0f, 0f);
    }

    void EnsureBlackout()
    {
        if (backgroundBlackout || !background) return;
        var go = new GameObject("BackdropBlackout", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(background, false);
        rt.SetAsLastSibling();
        LandscapeUI.Stretch(rt);
        backgroundBlackout = go.GetComponent<Image>();
        backgroundBlackout.raycastTarget = false;
        backgroundBlackout.color = new Color(0f, 0f, 0f, 0f);
    }

    static float BlackoutEase(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    static void ShowBackground(Image image, Sprite sprite)
    {
        if (!image || !sprite) return;
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        var fitter = image.GetComponent<AspectRatioFitter>();
        if (fitter && sprite.rect.height > 1f)
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
    }

    IEnumerator FadeText(string next)
    {
        for (float t = 0; t < .22f; t += Time.unscaledDeltaTime)
        {
            rotatingText.alpha = 1f - t / .22f;
            yield return null;
        }
        rotatingText.text = next;
        for (float t = 0; t < .28f; t += Time.unscaledDeltaTime)
        {
            rotatingText.alpha = t / .28f;
            yield return null;
        }
        rotatingText.alpha = 1;
    }

    static CanvasGroup EnsureGroup(GameObject go)
    {
        var group = go.GetComponent<CanvasGroup>();
        if (!group) group = go.AddComponent<CanvasGroup>();
        return group;
    }

    static Vector2 DirectionFor(int index)
    {
        switch (index % 5)
        {
            case 0: return new Vector2(-1, 0);
            case 1: return new Vector2(1, 0);
            case 2: return new Vector2(0, -1);
            case 3: return new Vector2(0, 1);
            default: return new Vector2(.65f, -.65f);
        }
    }

    static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    static Vector2 BasePosition(RectTransform rt)
    {
        return new Vector2(0, rt.name == "Town" ? -96f : -96f);
    }
}
