using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Bottom dialogue box for recruitment — short copy, large readable buttons.
/// </summary>
public class RecruitDialogBox : MonoBehaviour
{
    public struct Choice
    {
        public string Label;
        public Color Color;
        public Action Action;
        public Choice(string label, Color color, Action action)
        {
            Label = label; Color = color; Action = action;
        }
    }

    private static RecruitDialogBox _instance;
    public static RecruitDialogBox Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("RecruitDialogBox");
                _instance = go.AddComponent<RecruitDialogBox>();
                _instance.Build();
            }
            return _instance;
        }
    }

    public static bool AnyOpen => _instance && _instance.IsOpen;
    public bool IsOpen => _root != null && _root.activeSelf;

    private GameObject _root;
    private TextMeshProUGUI _speaker;
    private TextMeshProUGUI _body;
    private RectTransform _buttonRow;
    private readonly List<GameObject> _buttons = new List<GameObject>();

    public void Show(string speaker, string line, params Choice[] choices)
    {
        if (_root == null) Build();
        _root.SetActive(true);
        _speaker.text = speaker;
        _body.text = line;

        foreach (var b in _buttons) Destroy(b);
        _buttons.Clear();

        foreach (var c in choices)
        {
            var captured = c;
            var btn = BuildButton(c.Label, c.Color);
            btn.GetComponent<Button>().onClick.AddListener(() =>
            {
                captured.Action?.Invoke();
            });
            _buttons.Add(btn);
        }
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void Build()
    {
        var canvasGo = new GameObject("RecruitDialogCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 28000;
        LandscapeUI.ConfigureLandscapeScaler(canvasGo.GetComponent<CanvasScaler>());

        _root = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(canvasGo.transform, false);
        var rt = _root.GetComponent<RectTransform>();
        // Short bottom box — taller than before for fat buttons.
        rt.anchorMin = new Vector2(0.20f, 0.13f);
        rt.anchorMax = new Vector2(0.80f, 0.48f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var bg = _root.GetComponent<Image>();
        bg.sprite = LandscapeTheme.Current ? LandscapeTheme.Current.panel : null;
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;
        bg.raycastTarget = true;
        _root.AddComponent<UiWorldTapBlocker>();

        var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(_root.transform, false);
        var art = accent.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 0f);
        art.anchorMax = new Vector2(0f, 1f);
        art.pivot = new Vector2(0f, 0.5f);
        art.sizeDelta = new Vector2(14f, 0f);
        art.anchoredPosition = Vector2.zero;
        accent.GetComponent<Image>().color = new Color(1f, 0.78f, 0.12f, 1f);

        _speaker = NewText("Speaker", _root.transform, "RECRUIT", 36f, FontStyles.Bold);
        var srt = _speaker.rectTransform;
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0f, 1f);
        srt.anchoredPosition = new Vector2(32f, -16f);
        srt.sizeDelta = new Vector2(-48f, 44f);
        _speaker.alignment = TextAlignmentOptions.Left;
        _speaker.color = new Color(1f, 0.82f, 0.25f);

        _body = NewText("Body", _root.transform, "", 30f, FontStyles.Normal);
        var brt = _body.rectTransform;
        brt.anchorMin = new Vector2(0f, 0.42f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.offsetMin = new Vector2(32f, 0f);
        brt.offsetMax = new Vector2(-28f, -64f);
        _body.alignment = TextAlignmentOptions.TopLeft;
        _body.color = new Color(0.92f, 0.94f, 0.97f);
        _body.enableWordWrapping = true;

        var rowGo = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(_root.transform, false);
        _buttonRow = rowGo.GetComponent<RectTransform>();
        _buttonRow.anchorMin = new Vector2(0f, 0f);
        _buttonRow.anchorMax = new Vector2(1f, 0f);
        _buttonRow.pivot = new Vector2(0.5f, 0f);
        _buttonRow.anchoredPosition = new Vector2(0f, 16f);
        _buttonRow.sizeDelta = new Vector2(-32f, 100f);
        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 18f;
        hlg.padding = new RectOffset(16, 16, 6, 6);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        _root.SetActive(false);
    }

    private GameObject BuildButton(string label, Color color)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(_buttonRow, false);
        go.GetComponent<LayoutElement>().flexibleWidth = 1f;
        go.GetComponent<LayoutElement>().minHeight = 88f;
        var img = go.GetComponent<Image>();
        img.sprite = null;
        img.type = Image.Type.Sliced;
        img.color = color;
        var txt = NewText("Label", go.transform, label, 30f, FontStyles.Bold);
        Stretch(txt.rectTransform);
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        return go;
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
}
