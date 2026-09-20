using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Landscape uGUI primitives. Coordinates are measured from the top left of a 1600 x 900 frame.</summary>
public static class LandscapeUI
{
    public static readonly Color Ink = new Color32(9, 15, 20, 255);
    public static readonly Color PanelColor = new Color32(18, 27, 34, 255);
    public static readonly Color Muted = new Color32(155, 173, 181, 255);
    public static readonly Color White = new Color32(238, 241, 234, 255);
    public static readonly Color Red = new Color32(197, 43, 49, 255);
    public static readonly Color Green = new Color32(97, 193, 116, 255);
    public static readonly Color Gold = new Color32(232, 186, 90, 255);
    public static readonly Vector2 Resolution = new Vector2(1600, 900);

    public static void ConfigureLandscapeScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Resolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
    }

    public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Place(rt, x, y, w, h);
        return rt;
    }
    public static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;
    }
    public static void Stretch(RectTransform rt, float inset = 0)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }
    public static Image Image(string name, Transform p, float x, float y, float w, float h, Sprite sprite = null, Color? color = null, bool aspect = false)
    {
        var i = Rect(name, p, x, y, w, h).gameObject.AddComponent<Image>();
        i.sprite = sprite; i.color = color ?? Color.white; i.raycastTarget = false;
        i.preserveAspect = aspect;
        if (sprite != null && sprite.border != Vector4.zero) i.type = UnityEngine.UI.Image.Type.Sliced;
        return i;
    }
    public static Image Panel(string name, Transform p, float x, float y, float w, float h, bool block = false)
    {
        var i = Image(name, p, x, y, w, h, LandscapeTheme.Current?.panel, Color.white);
        if (!i.sprite) i.color = PanelColor;
        i.raycastTarget = block;
        var outline=i.gameObject.AddComponent<Outline>();
        outline.effectColor=new Color(.12f,.78f,.76f,.34f);outline.effectDistance=new Vector2(1,-1);
        return i;
    }
    public static TextMeshProUGUI Text(string name, Transform p, string value, float x, float y, float w, float h, float size = 24, Color? color = null, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var t = Rect(name, p, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        if (LandscapeTheme.Current?.font) t.font = LandscapeTheme.Current.font;
        t.text = value; t.fontSize = size; t.fontSizeMax = size; t.fontSizeMin = size * .82f;
        t.enableAutoSizing = true; t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.alignment = align; t.color = color ?? White; t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.enableWordWrapping = true;
        return t;
    }
    public static Button Button(string name, Transform p, string label, float x, float y, float w, float h, string style = "dark")
    {
        var i = ButtonImage(name, p, label, x, y, w, h, style);
        var b = i.gameObject.AddComponent<Button>(); b.targetGraphic = i; Colors(b);
        AttachPolish(i);
        return b;
    }
    public static ButtonUI LegacyButton(string name, Transform p, string label, float x, float y, float w, float h, string style = "dark")
    {
        var i = ButtonImage(name, p, label, x, y, w, h, style);
        i.gameObject.SetActive(false);
        var b = i.gameObject.AddComponent<ButtonUI>(); b.targetGraphic = i; Colors(b);
        AttachPolish(i);
        i.gameObject.SetActive(true);
        return b;
    }
    static Image ButtonImage(string name, Transform p, string label, float x, float y, float w, float h, string style)
    {
        var theme = LandscapeTheme.Current;
        var sprite = theme == null ? null : style == "red" ? theme.redButton : style == "green" ? theme.greenButton : style == "outline" ? theme.outlineButton : theme.darkButton;
        var i = Image(name, p, x, y, w, h, sprite, sprite ? Color.white : PanelColor);
        i.raycastTarget = true;
        i.gameObject.AddComponent<RectMask2D>();
        var shine = Image("Shine", i.transform, -w * .35f, 9, Mathf.Max(30, w * .12f), h - 18, null, new Color(1, 1, 1, .06f));
        shine.rectTransform.localEulerAngles = new Vector3(0, 0, -12);
        var glow = Image("SelectedGlow", i.transform, 18, h - 7, w - 36, 3, null, new Color(.95f, .15f, .18f, 0));
        var labelText = Text("Label", i.transform, label, 16, 4, w - 32, h - 8, Mathf.Clamp(h * .36f, 16, 22), null, true, TextAlignmentOptions.Center);
        labelText.enableWordWrapping = false;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.fontSizeMin = 8;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        return i;
    }
    static void AttachPolish(Image i)
    {
        var polish = i.gameObject.AddComponent<LandscapeButtonPolish>();
        var shine = i.transform.Find("Shine") as RectTransform;
        var glow = i.transform.Find("SelectedGlow")?.GetComponent<Graphic>();
        polish.shine = shine;
        polish.selectedGlow = glow;
        polish.label = i.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
    }
    public static void Colors(Selectable b)
    {
        var c = b.colors; c.normalColor = Color.white; c.highlightedColor = new Color(1.23f, 1.23f, 1.23f);
        c.pressedColor = new Color(.7f, .7f, .7f); c.selectedColor = new Color(1.15f, 1.15f, 1.15f);
        c.disabledColor = new Color(.42f, .46f, .49f, .65f); c.fadeDuration = .12f; b.colors = c;
    }
    public static ScrollRect Scroll(string name, Transform p, float x, float y, float w, float h, bool horizontal = false, bool grid = false)
    {
        var rt = Rect(name, p, x, y, w, h);
        var bg = rt.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, .01f);
        var scroll = rt.gameObject.AddComponent<ScrollRect>();
        var viewport = Rect("Viewport", rt, 0, 0, w, h); Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport, 0, 0, w, h);
        if (horizontal) { content.anchorMax = new Vector2(0, 1); content.sizeDelta = new Vector2(0, h); }
        else { content.anchorMax = new Vector2(1, 1); content.sizeDelta = Vector2.zero; }
        if (!grid)
        {
            var layout = horizontal ? (HorizontalOrVerticalLayoutGroup)content.gameObject.AddComponent<HorizontalLayoutGroup>() : content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14; layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = !horizontal; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        }
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = horizontal ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = horizontal; scroll.vertical = !horizontal;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32; scroll.decelerationRate = .12f;
        return scroll;
    }
    public static void LayoutSize(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = w; le.preferredHeight = le.minHeight = h;
    }
    public static Image Progress(Transform p, float x, float y, float w, float value, Color color)
    {
        Image("Track", p, x, y, w, 6, null, new Color32(41, 55, 61, 255));
        return Image("Progress", p, x, y, w * Mathf.Clamp01(value), 6, null, color);
    }
    public static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var go = parent.GetChild(i).gameObject;
            if (Application.isPlaying) { go.SetActive(false); Object.Destroy(go); }
            else Object.DestroyImmediate(go);
        }
    }
}
