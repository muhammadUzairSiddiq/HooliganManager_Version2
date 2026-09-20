using UnityEngine;

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
    void OnEnable() => Fit();
    void LateUpdate() => Fit();
    public void Fit()
    {
        var rt = transform as RectTransform;
        if (rt == null || frame == null || rt.parent == null) return;
        var safe = Screen.safeArea;
        if (Screen.width > 0 && Screen.height > 0)
        {
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
        frame.anchoredPosition = Vector2.zero;
        float scale = Mathf.Min(rt.rect.width / 1600f, rt.rect.height / 900f);
        scale = Mathf.Max(.01f, scale);
        frame.sizeDelta = new Vector2(rt.rect.width / scale, rt.rect.height / scale);
        frame.localScale = Vector3.one * scale;
    }
}
