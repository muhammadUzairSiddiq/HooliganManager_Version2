using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maps the authored 1600 x 900 coordinate system onto the complete device safe area.
/// The old fitter always kept a fixed 16:9 rectangle, which produced visible side
/// gutters on wide phones and ultrawide Game views.  The logical frame now grows on
/// the spare axis while preserving a uniform UI scale.
/// </summary>
[ExecuteAlways]
public sealed class LandscapeViewport : MonoBehaviour
{
    public RectTransform frame;
    Rect lastSafe;
    Vector2 lastScreen, lastAvailable;
    RectTransform lastFrame;
    bool fitted;
    void OnEnable()
    {
        fitted = false;
        LandscapeUI.ConfigureLandscapeScaler(GetComponentInParent<CanvasScaler>());
        Fit();
    }
    void LateUpdate() => Fit();
    public void Fit()
        => Fit(Screen.safeArea, new Vector2(Screen.width, Screen.height));

    // Explicit display metrics also allow repeatable notch/rotation regression checks.
    public void Fit(Rect safe, Vector2 screen)
    {
        var rt = transform as RectTransform;
        if (rt == null || frame == null || rt.parent == null) return;
        if (screen.x <= 0 || screen.y <= 0) return;
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, screen.x, screen.y);
        if (fitted && lastFrame == frame && lastSafe == safe && lastScreen == screen && lastAvailable == rt.rect.size) return;
        if (screen.x > 0 && screen.y > 0)
        {
            rt.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rt.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
        frame.anchoredPosition = Vector2.zero;
        float scale = Mathf.Min(rt.rect.width / 1600f, rt.rect.height / 900f);
        scale = Mathf.Max(.01f, scale);
        frame.sizeDelta = new Vector2(rt.rect.width / scale, rt.rect.height / scale);
        frame.localScale = Vector3.one * scale;
        lastSafe = safe;
        lastScreen = screen;
        lastAvailable = rt.rect.size;
        lastFrame = frame;
        fitted = true;
    }
}
