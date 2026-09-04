using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Canvas))]
public class SafeArea : MonoBehaviour
{
    public static UnityEvent OnResolutionOrOrientationChanged = new UnityEvent();
    private static bool screenChangeVarsInitialized = false;
    private static ScreenOrientation lastOrientation = ScreenOrientation.Portrait;
    private static Vector2 lastResolution = Vector2.zero;
    private static Rect lastSafeArea = Rect.zero;
 
    private Canvas canvas;
    private RectTransform rectTransform;
    private RectTransform safeAreaTransform;

    // Boolean flags to enable/disable safe area from top or bottom
    public bool enableTopSafeArea = true;
    public bool enableBottomSafeArea = true;
 
    void Awake()
    {
        canvas = GetComponent<Canvas>();
        rectTransform = GetComponent<RectTransform>();
 
        safeAreaTransform = transform.GetChild(0) as RectTransform;

        if(!screenChangeVarsInitialized)
        {
            lastOrientation = Screen.orientation;
            lastResolution.x = Screen.width;
            lastResolution.y = Screen.height;
            lastSafeArea = Screen.safeArea;
 
            screenChangeVarsInitialized = true;
        }
  
        ApplySafeArea();
    }
 
    void FixedUpdate()
    {
        if(Application.isMobilePlatform && Screen.orientation != lastOrientation)
            OrientationChanged();
 
        if(Screen.safeArea != lastSafeArea)
            SafeAreaChanged();
 
        if(Screen.width != lastResolution.x || Screen.height != lastResolution.y)
            ResolutionChanged();
    }
 
    void ApplySafeArea()
    {
        if(safeAreaTransform == null)
            return;
 
        var safeArea = Screen.safeArea;
        var anchorMin = safeArea.position;
        var anchorMax = safeArea.position + safeArea.size;

        // Control top and bottom safe area
        if (!enableTopSafeArea)
        {
            // Don't consider the top part of the safe area
            anchorMax.y = canvas.pixelRect.height;  // Set it to the full screen height
        }
        
        if (!enableBottomSafeArea)
        {
            // Don't consider the bottom part of the safe area
            anchorMin.y = 0;  // Set it to the bottom of the screen
        }

        anchorMin.x /= canvas.pixelRect.width;
        anchorMin.y /= canvas.pixelRect.height;
        anchorMax.x /= canvas.pixelRect.width;
        anchorMax.y /= canvas.pixelRect.height;
 
        safeAreaTransform.anchorMin = anchorMin;
        safeAreaTransform.anchorMax = anchorMax;
    }
 
    private static void OrientationChanged()
    {
        lastOrientation = Screen.orientation;
        lastResolution.x = Screen.width;
        lastResolution.y = Screen.height;

        OnResolutionOrOrientationChanged.Invoke();
    }
 
    private static void ResolutionChanged()
    {
        lastResolution.x = Screen.width;
        lastResolution.y = Screen.height;

        OnResolutionOrOrientationChanged.Invoke();
    }
 
    private static void SafeAreaChanged()
    {
        lastSafeArea = Screen.safeArea;
    }
}
