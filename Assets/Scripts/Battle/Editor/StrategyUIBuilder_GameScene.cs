#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that adds all Strategy Overhaul UI to Gameplay's ResultsPanel
/// and wires the new fields on BattleResultController.
///
/// Usage:
///   1. Open Gameplay in the Unity Editor.
///   2. Menu → Hooligan / Apply Strategy UI — Game Scene (Battle Result)
///   3. Save the scene (Ctrl+S).
/// </summary>
public static class StrategyUIBuilder_GameScene
{
    private const string MENU = "Hooligan/Apply Strategy UI — Game Scene (Battle Result)";

    [MenuItem(MENU)]
    static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.Contains("Game") && !scene.name.Contains("Battle"))
        {
            EditorUtility.DisplayDialog("Wrong Scene",
                "Please Open Gameplay first, then run this tool.", "OK");
            return;
        }

        // ── Find ResultsPanel ─────────────────────────────────────────────
        var resultsPanel = FindGameObjectInScene(scene, "ResultsPanel");
        if (resultsPanel == null)
        {
            EditorUtility.DisplayDialog("Not Found",
                "Could not find 'ResultsPanel' GameObject in Gameplay.\n\nMake sure the scene is fully loaded.", "OK");
            return;
        }

        var ctrl = resultsPanel.GetComponent<BattleResultController>();
        if (ctrl == null)
        {
            EditorUtility.DisplayDialog("No Controller",
                "BattleResultController not found on 'ResultsPanel'.", "OK");
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        int count = 0;

        // Find trip summary container (the VerticalLayoutGroup that holds existing rows)
        var tripSummaryContainer = FindInHierarchy(resultsPanel.transform, "tripsummaryprefab (2)")?.parent
                                ?? FindInHierarchy(resultsPanel.transform, "tripsummaryprefab (1)")?.parent
                                ?? resultsPanel.transform;

        // ── A. Morale Change Row ──────────────────────────────────────────
        if (ctrl.moraleChangeValue == null)
        {
            var rowGO = CreateTripSummaryRow(tripSummaryContainer, "MoraleRow", "😤 MORALE CHANGE", font);
            var valueLabel = rowGO.transform.Find("Value")?.GetComponent<TextMeshProUGUI>();
            if (valueLabel != null)
            {
                ctrl.moraleChangeValue = valueLabel;
                count++;
            }
            Undo.RegisterCreatedObjectUndo(rowGO, "Create MoraleRow");
        }

        // ── B. "LADS ARE SHAKEN" Warning Banner ──────────────────────────
        if (ctrl.ladsAreShakenbanner == null)
        {
            var bannerGO = new GameObject("LadsAreShakenBanner");
            Undo.RegisterCreatedObjectUndo(bannerGO, "Create LadsAreShakenBanner");
            GameObjectUtility.SetParentAndAlign(bannerGO, resultsPanel);

            // Full-width warning bar at bottom of results panel
            var bg = bannerGO.AddComponent<Image>();
            bg.color = new Color(0.55f, 0.05f, 0.05f, 0.95f);
            var rt = bannerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(0, 60);
            rt.anchoredPosition = new Vector2(0, 70);  // just above bottom buttons

            // Warning text
            var textGO = new GameObject("LadsAreShakenText");
            GameObjectUtility.SetParentAndAlign(textGO, bannerGO);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text      = "⚠ LADS ARE SHAKEN — morale critical. Win urgently or lads will walk.";
            tmp.fontSize  = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = new Color(1f, 0.8f, 0.2f);
            tmp.alignment = TextAlignmentOptions.Center;
            FillRect(textGO.GetComponent<RectTransform>());

            bannerGO.SetActive(false);

            ctrl.ladsAreShakenbanner = bannerGO;
            ctrl.ladsAreShakenText   = tmp;
            count++;
        }

        // ── C. Subtitle text — ensure wired ───────────────────────────────
        // Try to find existing subtitle text if not already wired
        if (ctrl.subtitleText == null)
        {
            // Look for a TextMeshProUGUI that likely contains the subtitle
            var candidates = resultsPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var c in candidates)
            {
                if (c.text.Contains("FIGHT") || c.text.Contains("GAVE") || c.text.Contains("RAN OUT")
                 || c.text.Contains("TOOK"))
                {
                    ctrl.subtitleText = c;
                    count++;
                    Debug.Log($"[StrategyUI] Auto-wired subtitleText to: {c.gameObject.name}");
                    break;
                }
            }
        }

        // ── Mark dirty and report ─────────────────────────────────────────
        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(scene);

        EditorUtility.DisplayDialog("Strategy UI Applied",
            $"Done! {count} UI element(s) created/wired in Gameplay.\n\nSave the scene with Ctrl+S.", "OK");
        Debug.Log($"[StrategyUI] Applied {count} changes to Gameplay ResultsPanel.");
    }

    public static void ApplyBatch()
    {
        var scenePath = "Assets/Scenes/Gameplay.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var resultsPanel = FindGameObjectInScene(scene, "ResultsPanel");
        if (resultsPanel == null)
        {
            Debug.LogError("[StrategyUI] Could not find 'ResultsPanel' GameObject in Gameplay.");
            return;
        }

        var ctrl = resultsPanel.GetComponent<BattleResultController>();
        if (ctrl == null)
        {
            Debug.LogError("[StrategyUI] BattleResultController not found on 'ResultsPanel'.");
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        int count = 0;
        var tripSummaryContainer = FindInHierarchy(resultsPanel.transform, "tripsummaryprefab (2)")?.parent
                                ?? FindInHierarchy(resultsPanel.transform, "tripsummaryprefab (1)")?.parent
                                ?? resultsPanel.transform;

        if (ctrl.moraleChangeValue == null)
        {
            var rowGO = CreateTripSummaryRow(tripSummaryContainer, "MoraleRow", "😤 MORALE CHANGE", font);
            var valueLabel = rowGO.transform.Find("Value")?.GetComponent<TextMeshProUGUI>();
            if (valueLabel != null)
            {
                ctrl.moraleChangeValue = valueLabel;
                count++;
            }
        }

        if (ctrl.ladsAreShakenbanner == null)
        {
            var bannerGO = new GameObject("LadsAreShakenBanner");
            GameObjectUtility.SetParentAndAlign(bannerGO, resultsPanel);

            var bg = bannerGO.AddComponent<Image>();
            bg.color = new Color(0.55f, 0.05f, 0.05f, 0.95f);
            var rt = bannerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.sizeDelta = new Vector2(0, 60);
            rt.anchoredPosition = new Vector2(0, 70);

            var textGO = new GameObject("LadsAreShakenText");
            GameObjectUtility.SetParentAndAlign(textGO, bannerGO);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text      = "⚠ LADS ARE SHAKEN — morale critical. Win urgently or lads will walk.";
            tmp.fontSize  = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = new Color(1f, 0.8f, 0.2f);
            tmp.alignment = TextAlignmentOptions.Center;
            FillRect(textGO.GetComponent<RectTransform>());

            bannerGO.SetActive(false);

            ctrl.ladsAreShakenbanner = bannerGO;
            ctrl.ladsAreShakenText   = tmp;
            count++;
        }

        if (ctrl.subtitleText == null)
        {
            var candidates = resultsPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var c in candidates)
            {
                if (c.text.Contains("FIGHT") || c.text.Contains("GAVE") || c.text.Contains("RAN OUT")
                 || c.text.Contains("TOOK"))
                {
                    ctrl.subtitleText = c;
                    count++;
                    Debug.Log($"[StrategyUI] Auto-wired subtitleText to: {c.gameObject.name}");
                    break;
                }
            }
        }

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[StrategyUI] Applied {count} changes to Gameplay ResultsPanel in batch mode.");
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================

    /// <summary>
    /// Creates a two-column "label | value" row styled like the existing trip summary rows.
    /// </summary>
    static GameObject CreateTripSummaryRow(Transform parent, string name, string labelText, TMP_FontAsset font)
    {
        var rowGO = new GameObject(name);
        GameObjectUtility.SetParentAndAlign(rowGO, parent.gameObject);

        // Horizontal Layout Group to match existing prefab rows
        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 10;
        hlg.padding = new RectOffset(8, 8, 4, 4);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        var rowRt = rowGO.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(0, 60);

        // Background tint
        var bg = rowGO.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.04f);

        // Label (left)
        var lblGO = new GameObject("Label");
        GameObjectUtility.SetParentAndAlign(lblGO, rowGO);
        var lblTmp = lblGO.AddComponent<TextMeshProUGUI>();
        if (font != null) lblTmp.font = font;
        lblTmp.text      = labelText;
        lblTmp.fontSize  = 28;
        lblTmp.color     = new Color(0.75f, 0.75f, 0.75f);
        lblTmp.alignment = TextAlignmentOptions.Left;
        var lblLayout = lblGO.AddComponent<LayoutElement>();
        lblLayout.flexibleWidth = 1;

        // Value (right)
        var valGO = new GameObject("Value");
        GameObjectUtility.SetParentAndAlign(valGO, rowGO);
        var valTmp = valGO.AddComponent<TextMeshProUGUI>();
        if (font != null) valTmp.font = font;
        valTmp.text      = "+0";
        valTmp.fontSize  = 28;
        valTmp.fontStyle = FontStyles.Bold;
        valTmp.color     = new Color(0.2f, 0.85f, 0.2f);
        valTmp.alignment = TextAlignmentOptions.Right;
        var valLayout = valGO.AddComponent<LayoutElement>();
        valLayout.preferredWidth = 160;

        return rowGO;
    }

    static Transform FindInHierarchy(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindInHierarchy(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static GameObject FindGameObjectInScene(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindInHierarchy(root.transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    static void FillRect(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;
    }
}
#endif
