using UnityEngine;

/// <summary>Keeps the complete authored layout inside the device safe area, including tablets and ultrawide screens.</summary>
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
        frame.anchoredPosition = Vector2.zero; frame.sizeDelta = LandscapeUI.Resolution;
        float scale = Mathf.Min(rt.rect.width / 1600f, rt.rect.height / 900f);
        frame.localScale = Vector3.one * Mathf.Max(.01f, scale);
    }
}
