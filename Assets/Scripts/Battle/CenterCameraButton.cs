using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A small HUD button that recentres the RTS camera on the currently selected
/// units (or all player units if none are selected).
///
/// Two ways to use it:
///   1. Attach to an existing UI Button — its onClick is wired automatically.
///   2. Call <see cref="EnsureExists"/> to auto-build a ready-made button at
///      runtime (used by CameraPanTouchOnly so the feature works out of the box).
///
/// In the Phase-8 UI pass this can be replaced by a designed HUD button that
/// simply calls <see cref="Recenter"/>.
/// </summary>
public class CenterCameraButton : MonoBehaviour
{
    private static CenterCameraButton _instance;

    [SerializeField] private Button targetButton;

    void Awake()
    {
        _instance = this;
        if (targetButton == null) targetButton = GetComponent<Button>();
        if (targetButton != null)
            targetButton.onClick.AddListener(Recenter);
    }

    /// <summary>Recentre the camera on the current selection.</summary>
    public void Recenter()
    {
        if (CameraPanTouchOnly.Instance != null)
            CameraPanTouchOnly.Instance.CenterOnSelection();
    }

    /// <summary>Create a default bottom-right "recenter" button if one doesn't already exist.</summary>
    public static void EnsureExists()
    {
        if (_instance != null) return;

        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) return;

        var go = new GameObject("CenterCameraButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-345f, 150f);
        rt.sizeDelta = new Vector2(104f, 104f);

        var img = go.GetComponent<Image>();
        img.sprite = null;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.09f, 0.10f, 0.13f, 0.82f);

        var labelGo = new GameObject("Icon", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "\u25C9"; // ◉ recenter glyph
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 48f;
        label.color = new Color(0.9f, 0.95f, 1f, 1f);
        label.raycastTarget = false;

        _instance = go.AddComponent<CenterCameraButton>();
        _instance.targetButton = go.GetComponent<Button>();
        BuildCompanionButton(canvas.transform, "CameraZoomInButton", "+", -465f, 150f, () => CameraPanTouchOnly.Instance?.ZoomIn());
        BuildCompanionButton(canvas.transform, "CameraZoomOutButton", "-", -585f, 150f, () => CameraPanTouchOnly.Instance?.ZoomOut());
        BuildCompanionButton(canvas.transform, "CameraRotateButton", "R", -705f, 150f, () => CameraPanTouchOnly.Instance?.RotateCamera(45f));
    }

    private static void BuildCompanionButton(Transform parent, string name, string label, float x, float y, System.Action action)
    {
        if (parent.Find(name) != null) return;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(104f, 104f);

        var img = go.GetComponent<Image>();
        img.sprite = null;
        img.color = new Color(0.09f, 0.10f, 0.13f, 0.82f);

        var labelGo = new GameObject("Icon", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var text = labelGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 58f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.9f, 0.95f, 1f, 1f);
        text.raycastTarget = false;

        go.GetComponent<Button>().onClick.AddListener(() => action?.Invoke());
    }

    private static Canvas FindOverlayCanvas()
    {
        Canvas best = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas) continue;
            if (c.renderMode == RenderMode.WorldSpace) continue;
            // Prefer the highest-sorting overlay canvas (the battle HUD).
            if (best == null || c.sortingOrder >= best.sortingOrder)
                best = c;
        }
        return best;
    }
}
