using UnityEngine;

/// <summary>Insets a landscape canvas for notches and home indicators on Android and iOS.</summary>
public sealed class MobileSafeArea : MonoBehaviour
{
    Rect last;
    Vector2 lastScreen;
    RectTransform rect;

    void Awake()
    {
        rect = transform as RectTransform;
        Apply();
    }

    void Update()
    {
        if (Screen.safeArea != last || lastScreen != new Vector2(Screen.width, Screen.height)) Apply();
    }

    void Apply()
    {
        if (!rect) return;
        last = Screen.safeArea;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        lastScreen = new Vector2(Screen.width, Screen.height);
        var min = last.position;
        var max = last.position + last.size;
        rect.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
        rect.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
