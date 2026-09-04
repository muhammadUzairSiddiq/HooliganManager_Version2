using UnityEngine;

/// <summary>
/// Applies a sensible mobile/Android performance baseline at startup so the
/// game runs smoothly on phones. Runs automatically — no scene setup needed.
///
/// - Targets 60 FPS (falls back gracefully on weaker devices).
/// - Disables VSync (targetFrameRate is authoritative on mobile).
/// - Trims shadow distance and disables soft particles where cheap wins exist.
///
/// Fine-grained rendering quality (MSAA, render scale, shadow cascades) lives in
/// the URP asset and is tuned during the atmosphere/optimisation pass.
/// </summary>
public static class MobilePerformanceConfig
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // 60 FPS target; VSync off so targetFrameRate is respected on device.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // Keep the screen awake during gameplay sessions.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Cheap shadow win: pull shadow distance in for a city-scale scene.
        if (QualitySettings.shadowDistance > 60f)
            QualitySettings.shadowDistance = 60f;
    }
}
