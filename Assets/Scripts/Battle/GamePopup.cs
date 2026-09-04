using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Short, large-touch modal for turf / defeat / choice prompts.
/// Sized for mobile portrait — big title, short body, fat buttons.
/// </summary>
public class GamePopup : MonoBehaviour
{
    public struct Option
    {
        public string Label;
        public Color Color;
        public Action Action;
        public Option(string label, Color color, Action action) { Label = label; Color = color; Action = action; }
    }

    private static GamePopup _instance;
    public static GamePopup Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("GamePopup");
                _instance = go.AddComponent<GamePopup>();
                _instance.Build();
            }
            return _instance;
        }
    }

    public bool IsOpen => _root != null && _root.activeSelf;

    private GameObject _root;
    private Image _panel;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _body;
    private RectTransform _buttonRow;
    private readonly List<GameObject> _buttons = new List<GameObject>();

    public void Show(string title, string body, params Option[] options)
    {
        if (_root == null) Build();
        _root.SetActive(true);
        _title.text = title;
        _body.text = body;

        foreach (var b in _buttons) Destroy(b);
        _buttons.Clear();

        foreach (var opt in options)
        {
            var captured = opt;
            var btn = BuildButton(opt.Label, opt.Color);
            btn.GetComponent<Button>().onClick.AddListener(() =>
            {
                Hide();
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
        var canvasGo = new GameObject("GamePopupCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Portrait-first reference so the box stays large on phones.
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;

        _root = new GameObject("Root", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(canvasGo.transform, false);
        Stretch(_root.GetComponent<RectTransform>());
        var dim = _root.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);

        _panel = NewImage("Panel", _root.transform);
        var prt = _panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        // Short dialog, wide enough for fat buttons.
        prt.sizeDelta = new Vector2(920f, 380f);
        _panel.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        _panel.type = Image.Type.Sliced;
        _panel.color = new Color(0.09f, 0.10f, 0.13f, 0.98f);

        var accent = NewImage("Accent", _panel.transform);
        var art = accent.rectTransform;
        art.anchorMin = new Vector2(0, 1); art.anchorMax = new Vector2(1, 1); art.pivot = new Vector2(0.5f, 1);
        art.anchoredPosition = Vector2.zero; art.sizeDelta = new Vector2(0, 12);
        accent.color = new Color(0.85f, 0.16f, 0.16f, 1f);

        _title = NewText("Title", _panel.transform, "", 56f, FontStyles.Bold);
        var tr = _title.rectTransform;
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1);
        tr.anchoredPosition = new Vector2(0, -28); tr.sizeDelta = new Vector2(-48, 72);
        _title.alignment = TextAlignmentOptions.Center; _title.color = Color.white;

        _body = NewText("Body", _panel.transform, "", 34f, FontStyles.Normal);
        var brt = _body.rectTransform;
        brt.anchorMin = new Vector2(0, 0.38f); brt.anchorMax = new Vector2(1, 0.72f);
        brt.offsetMin = new Vector2(40, 0); brt.offsetMax = new Vector2(-40, 0);
        _body.alignment = TextAlignmentOptions.Center; _body.color = new Color(0.88f, 0.90f, 0.95f);
        _body.enableWordWrapping = true;

        var rowGo = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(_panel.transform, false);
        _buttonRow = rowGo.GetComponent<RectTransform>();
        _buttonRow.anchorMin = new Vector2(0.5f, 0f); _buttonRow.anchorMax = new Vector2(0.5f, 0f); _buttonRow.pivot = new Vector2(0.5f, 0f);
        _buttonRow.anchoredPosition = new Vector2(0, 28); _buttonRow.sizeDelta = new Vector2(840f, 120f);
        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 28f; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        _root.SetActive(false);
    }

    private GameObject BuildButton(string text, Color color)
    {
        var go = new GameObject("Btn_" + text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(_buttonRow, false);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 112f; le.preferredHeight = 112f;
        var img = go.GetComponent<Image>();
        img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = color;
        var label = NewText("Label", go.transform, text, 36f, FontStyles.Bold);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return go;
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
}
