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
    Image backgroundOverlay;
    Sprite[] backgroundCycle;

    void Awake()
    {
        if (!shell) shell = GetComponent<LandscapeFrontEnd>();
        if (background) bgBaseScale = background.localScale;
        if (background) backgroundImage = background.GetComponent<Image>();
        var theme = LandscapeTheme.Current;
        if (theme) backgroundCycle = new[] { theme.mainBackground, theme.townBackground, theme.tacticalBackground };
    }

    void OnEnable()
    {
        if (rotatingText && textRoutine == null) textRoutine = StartCoroutine(RotateText());
        if (backgroundImage && backgroundCycle != null && backgroundCycle.Length > 1 && backgroundRoutine == null)
            backgroundRoutine = StartCoroutine(CycleBackground());
    }

    void Update()
    {
        if (!background) return;
        float t = Time.unscaledTime;
        float zoom = 1f + Mathf.Sin(t * .09f) * .018f;
        background.localScale = bgBaseScale * zoom;
        background.anchoredPosition = new Vector2(Mathf.Sin(t * .07f) * 10f, Mathf.Cos(t * .06f) * 6f);
    }

    public void ShowPage(GameObject[] pages, string[] names, string action)
    {
        int index = System.Array.IndexOf(names, action);
        if (index < 0 || index >= pages.Length || !pages[index]) return;
        if (pageRoutine != null) StopCoroutine(pageRoutine);
        pageRoutine = StartCoroutine(SwitchPage(pages, names, index));
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
        int index = 0;
        for (int i = 0; i < backgroundCycle.Length; i++)
            if (backgroundCycle[i] == backgroundImage.sprite) index = i;

        while (true)
        {
            yield return new WaitForSecondsRealtime(13.5f);
            int next = (index + 1) % backgroundCycle.Length;
            if (!backgroundCycle[next]) continue;
            if (!backgroundOverlay)
            {
                var go = new GameObject("BackdropCrossfade", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(background, false);
                LandscapeUI.Stretch(rt);
                backgroundOverlay = go.GetComponent<Image>();
                backgroundOverlay.raycastTarget = false;
                backgroundOverlay.preserveAspect = backgroundImage.preserveAspect;
                backgroundOverlay.type = backgroundImage.type;
            }
            backgroundOverlay.sprite = backgroundCycle[next];
            backgroundOverlay.color = new Color(1, 1, 1, 0);
            for (float t = 0; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                backgroundOverlay.color = new Color(1, 1, 1, Mathf.SmoothStep(0, .45f, t / 1.1f));
                yield return null;
            }
            backgroundImage.sprite = backgroundCycle[next];
            backgroundOverlay.color = new Color(1, 1, 1, 0);
            index = next;
        }
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
