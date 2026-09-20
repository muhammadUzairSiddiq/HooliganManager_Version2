using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Mobile readability pass for gameplay: pulls the camera closer and enlarges
/// HUD / minimap / world labels so nothing reads as "too small to see".
/// Called once when the battle map is ready.
/// </summary>
public static class GameplayReadability
{
    private static bool _applied;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        PullCameraCloser();
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Gameplay")
        { EnlargeMinimapIcons();return; }
        EnlargeBattleHudTexts();
        EnlargePortraits();
        EnlargeMinimapIcons();
    }

    /// <summary>Reset so a new battle scene can re-apply.</summary>
    public static void ResetForNewBattle() => _applied = false;

    private static void PullCameraCloser()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (cam.gameObject.scene.name == "Gameplay")
        {
            CameraPanTouchOnly.Instance?.ConfigureCity(PlayerPrefs.GetFloat("CityCameraFov",GameplayTuning.Current.cameraFov),
                PlayerPrefs.GetFloat("CityCameraHeight",GameplayTuning.Current.explorationHeight),PlayerPrefs.GetFloat("CityCameraPitch",GameplayTuning.Current.cameraPitch));
            CameraPanTouchOnly.Instance?.CenterOnSelection();
            return;
        }

        var pan = CameraPanTouchOnly.Instance;
        if (cam.orthographic)
        {
            // Closer orthographic framing for mobile portrait.
            float target = Mathf.Clamp(cam.orthographicSize * 0.72f, 5.5f, 10f);
            cam.orthographicSize = target;
            if (pan != null) pan.ForceOrthoSize(target);
        }
        else
        {
            // Drop camera height so characters fill more of the screen.
            Vector3 p = cam.transform.position;
            float targetY = Mathf.Clamp(p.y * 0.75f, 16f, 24f);
            p.y = targetY;
            cam.transform.position = p;
            if (pan != null) pan.ForceHeight(targetY);
        }
    }

    private static void EnlargeBattleHudTexts()
    {
        var ui = BattleUIController.instance;
        if (ui == null) return;

        BumpText(ui.roundTimerText, 1.45f, minSize: 42f);
        BumpText(ui.roundLabel, 1.35f, minSize: 22f);
        BumpText(ui.playerCountText, 1.35f, minSize: 26f);
        BumpText(ui.enemyCountText, 1.35f, minSize: 26f);
        BumpText(ui.objectiveText, 1.4f, minSize: 24f);
        BumpText(ui.selectedUnitsLabel, 2.0f, minSize: 44f);
        BumpText(ui.policeHeatBarLabel, 1.3f, minSize: 20f);

        // Also bump any other TMP labels under the battle HUD canvas that are still tiny.
        var root = ui.transform;
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp == null) continue;
            if (tmp.fontSize < 20f) tmp.fontSize = Mathf.Max(tmp.fontSize * 1.5f, 22f);
            else if (tmp.fontSize < 28f) tmp.fontSize *= 1.25f;
        }
    }

    private static void EnlargePortraits()
    {
        var ui = BattleUIController.instance;
        if (ui == null || ui.portraitStrip == null) return;

        foreach (Transform child in ui.portraitStrip)
        {
            var rt = child as RectTransform;
            if (rt == null) continue;
            rt.sizeDelta = new Vector2(
                Mathf.Max(rt.sizeDelta.x, 120f),
                Mathf.Max(rt.sizeDelta.y, 140f));

            var le = child.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredWidth = Mathf.Max(le.preferredWidth, 120f);
                le.preferredHeight = Mathf.Max(le.preferredHeight, 140f);
            }
        }
    }

    private static void EnlargeMinimapIcons()
    {
        // Live minimap icons are sized by LiveMiniMap itself.
        LiveMiniMap.EnsureExists();
    }

    private static void BumpText(TextMeshProUGUI tmp, float mul, float minSize)
    {
        if (tmp == null) return;
        tmp.fontSize = Mathf.Max(tmp.fontSize * mul, minSize);
    }
}
