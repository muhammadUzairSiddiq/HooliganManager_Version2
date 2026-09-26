using UnityEngine;

/// <summary>
/// Applies a sensible mobile/Android performance baseline at startup so the
/// game runs smoothly on phones. Runs automatically — no scene setup needed.
///
/// - Targets 30 FPS on mobile; device profiling is still required.
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
        // Mobile 30 FPS target; VSync off so targetFrameRate is respected on device.
        QualitySettings.vSyncCount = 0;
        bool lowMemory=Application.isMobilePlatform&&(SystemInfo.systemMemorySize<=4096||SystemInfo.graphicsMemorySize<=1024);
        Application.targetFrameRate = Application.isMobilePlatform?30:PlayerPrefs.GetInt("HM.FrameRate", GameplayTuning.Current.targetFps);

        // Keep the screen awake during gameplay sessions.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Cheap shadow win: pull shadow distance in for a city-scale scene.
        QualitySettings.shadowDistance = GameplayTuning.Current.shadowDistance;
        QualitySettings.skinWeights = SkinWeights.TwoBones;
        QualitySettings.softParticles = false;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.streamingMipmapsActive=true;
        QualitySettings.streamingMipmapsMemoryBudget=Application.isMobilePlatform?96f:256f;
        if (Application.isMobilePlatform)
        {
            QualitySettings.lodBias = .8f;
            var source = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (source)
            {
                var mobile = Object.Instantiate(source);
                mobile.renderScale = lowMemory?.7f:GameplayTuning.Current.renderScale;
                mobile.msaaSampleCount = lowMemory?1:2;
                mobile.shadowDistance = GameplayTuning.Current.shadowDistance;
                mobile.shadowCascadeCount = 1;
                QualitySettings.renderPipeline = mobile;
            }
        }
    }
}
