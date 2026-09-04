#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Editor utility that adds all Strategy Overhaul UI to DashboardScene and
/// wires every new field on MainDashboardController, RecruitFansController,
/// PlanAwayTripController, and RankingsController.
///
/// Usage:
///   1.  Open DashboardScene in the Unity Editor.
///   2.  Menu → Hooligan / Apply Strategy UI — Dashboard Scene
///   3.  Save the scene (Ctrl+S).
/// </summary>
public static class StrategyUIBuilder_Dashboard
{
    private const string MENU = "Hooligan/Apply Strategy UI — Dashboard Scene";

    [MenuItem(MENU)]
    static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.Contains("Dashboard"))
        {
            EditorUtility.DisplayDialog("Wrong Scene",
                "Please open DashboardScene first, then run this tool.", "OK");
            return;
        }

        // ── Locate root controllers ───────────────────────────────────────
        var dashGO  = FindGameObjectInScene(scene, "MainDashboard");
        var awayGO  = FindGameObjectInScene(scene, "PlanAwayTrip");
        var rankGO  = FindGameObjectInScene(scene, "Rankings");
        var recGO   = FindGameObjectInScene(scene, "RecruitFan");

        if (dashGO == null)  { Debug.LogError("[StrategyUI] Could not find 'MainDashboard' GameObject."); return; }
        if (awayGO == null)  { Debug.LogError("[StrategyUI] Could not find 'PlanAwayTrip' GameObject."); return; }
        if (rankGO == null)  { Debug.LogError("[StrategyUI] Could not find 'Rankings' GameObject."); return; }
        if (recGO  == null)  { Debug.LogError("[StrategyUI] Could not find 'RecruitFan' GameObject."); return; }

        // Load font + shared resources
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        int count = 0;

        // ─── 1. MainDashboard ─────────────────────────────────────────────
        count += PatchMainDashboard(dashGO, font);

        // ─── 2. PlanAwayTrip ──────────────────────────────────────────────
        count += PatchPlanAwayTrip(awayGO, font);

        // ─── 3. Rankings ──────────────────────────────────────────────────
        count += PatchRankings(rankGO, font);

        // ─── 4. RecruitFan ────────────────────────────────────────────────
        count += PatchRecruitFans(recGO, font);

        // ─── 5. BribePolice ───────────────────────────────────────────────
        var bribeGO = FindGameObjectInScene(scene, "BribePolice");
        if (bribeGO != null)
        {
            count += PatchBribePolice(bribeGO, font);
        }

        // ── Mark dirty and report ─────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Strategy UI Applied",
            $"Done! {count} UI element(s) created/wired.\n\nSave the scene with Ctrl+S.", "OK");
        Debug.Log($"[StrategyUI] Applied {count} changes to DashboardScene.");
    }

    public static void ApplyBatch()
    {
        var scenePath = "Assets/Scenes/DashboardScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var dashGO  = FindGameObjectInScene(scene, "MainDashboard");
        var awayGO  = FindGameObjectInScene(scene, "PlanAwayTrip");
        var rankGO  = FindGameObjectInScene(scene, "Rankings");
        var recGO   = FindGameObjectInScene(scene, "RecruitFan");

        if (dashGO == null)  { Debug.LogError("[StrategyUI] Could not find 'MainDashboard' GameObject."); return; }
        if (awayGO == null)  { Debug.LogError("[StrategyUI] Could not find 'PlanAwayTrip' GameObject."); return; }
        if (rankGO == null)  { Debug.LogError("[StrategyUI] Could not find 'Rankings' GameObject."); return; }
        if (recGO  == null)  { Debug.LogError("[StrategyUI] Could not find 'RecruitFan' GameObject."); return; }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        int count = 0;
        count += PatchMainDashboard(dashGO, font);
        count += PatchPlanAwayTrip(awayGO, font);
        count += PatchRankings(rankGO, font);
        count += PatchRecruitFans(recGO, font);

        var bribeGO = FindGameObjectInScene(scene, "BribePolice");
        if (bribeGO != null)
        {
            count += PatchBribePolice(bribeGO, font);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[StrategyUI] Applied {count} changes to DashboardScene in batch mode.");
    }

    // =========================================================================
    //  MAIN DASHBOARD
    // =========================================================================
    static int PatchMainDashboard(GameObject dashGO, TMP_FontAsset font)
    {
        int count = 0;
        var ctrl  = dashGO.GetComponent<MainDashboardController>();
        if (ctrl == null) { Debug.LogWarning("[StrategyUI] MainDashboardController missing on MainDashboard."); return 0; }

        // Find existing policeHeat bar if present
        var existingHeatBar = FindInHierarchy(dashGO.transform, "policeHeat")?.gameObject;
        RectTransform heatBarParent = existingHeatBar != null
            ? existingHeatBar.GetComponent<RectTransform>()
            : dashGO.GetComponent<RectTransform>();

        // ── 1a. Police Heat Description Text ─────────────────────────────
        if (ctrl.policeHeatDescriptionText == null)
        {
            var go = FindOrCreateTMPro(heatBarParent, "PoliceHeatDescription", font);
            go.GetComponent<TextMeshProUGUI>().text      = "Flying under the radar.";
            go.GetComponent<TextMeshProUGUI>().fontSize  = 24;
            go.GetComponent<TextMeshProUGUI>().color     = new Color(0.6f, 0.6f, 0.6f);
            PlaceBelow(go.GetComponent<RectTransform>(), existingHeatBar?.GetComponent<RectTransform>(), dashGO.GetComponent<RectTransform>(), 30);
            ctrl.policeHeatDescriptionText = go.GetComponent<TextMeshProUGUI>();
            Undo.RegisterCreatedObjectUndo(go, "Create PoliceHeatDescription");
            count++;
        }

        // ── 1b. Fan Morale Dot ────────────────────────────────────────────
        if (ctrl.moraleDotImage == null)
        {
            // Place near fans text — find "fans" text label
            var fansGO = FindInHierarchy(dashGO.transform, "fans")?.gameObject;
            Transform parent = fansGO != null ? fansGO.transform.parent : dashGO.transform;

            var dotGO = FindOrCreateImageGO(parent, "MoraleDot");
            var rt = dotGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(16, 16);
            rt.anchoredPosition = new Vector2(-90, 0);
            dotGO.GetComponent<Image>().color = new Color(0.2f, 0.85f, 0.2f);
            ctrl.moraleDotImage = dotGO.GetComponent<Image>();
            Undo.RegisterCreatedObjectUndo(dotGO, "Create MoraleDot");
            count++;
        }

        // ── 1c. Morale Text Label ─────────────────────────────────────────
        if (ctrl.moraleText == null)
        {
            var fansGO = FindInHierarchy(dashGO.transform, "fans")?.gameObject;
            Transform parent = fansGO != null ? fansGO.transform.parent : dashGO.transform;

            var go = FindOrCreateTMPro(parent, "MoraleText", font);
            go.GetComponent<TextMeshProUGUI>().text     = "MORALE: HIGH";
            go.GetComponent<TextMeshProUGUI>().fontSize = 20;
            go.GetComponent<TextMeshProUGUI>().color    = new Color(0.2f, 0.85f, 0.2f);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -28);
            ctrl.moraleText = go.GetComponent<TextMeshProUGUI>();
            Undo.RegisterCreatedObjectUndo(go, "Create MoraleText");
            count++;
        }

        // ── 1d. FirmCard Status Line ──────────────────────────────────────
        var firmCardGO = FindInHierarchy(dashGO.transform, "FirmCard")?.gameObject;
        if (firmCardGO != null)
        {
            if (ctrl.firmStatusText == null)
            {
                var go = FindOrCreateTMPro(firmCardGO.transform, "FirmStatusText", font);
                var tmp = go.GetComponent<TextMeshProUGUI>();
                tmp.text      = "UNKNOWN CREW — barely a rumour.";
                tmp.fontSize  = 22;
                tmp.color     = new Color(0.65f, 0.65f, 0.65f);
                tmp.fontStyle = FontStyles.Italic;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.sizeDelta  = new Vector2(0, 36);
                rt.anchoredPosition = new Vector2(0, 60);
                ctrl.firmStatusText = tmp;
                Undo.RegisterCreatedObjectUndo(go, "Create FirmStatusText");
                count++;
            }

            if (ctrl.firmRecordText == null)
            {
                var go = FindOrCreateTMPro(firmCardGO.transform, "FirmRecordText", font);
                var tmp = go.GetComponent<TextMeshProUGUI>();
                tmp.text     = "0W - 0L";
                tmp.fontSize = 22;
                tmp.color    = new Color(0.75f, 0.75f, 0.75f);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.sizeDelta  = new Vector2(0, 30);
                rt.anchoredPosition = new Vector2(0, 28);
                ctrl.firmRecordText = tmp;
                Undo.RegisterCreatedObjectUndo(go, "Create FirmRecordText");
                count++;
            }

            // ── 1e. Prestige Badge ────────────────────────────────────────
            if (ctrl.prestigeBadge == null)
            {
                var badge = FindOrCreateImageGO(firmCardGO.transform, "PrestigeBadge");
                badge.SetActive(false); // hidden by default
                var img = badge.GetComponent<Image>();
                img.color = new Color(1f, 0.84f, 0f, 0.9f); // gold
                var rt = badge.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(80, 30);
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-10, -15);
                // Add "PRESTIGE" label inside badge
                var labelGO = FindOrCreateTMPro(badge.transform, "PrestigeLabel", font);
                var lbl = labelGO.GetComponent<TextMeshProUGUI>();
                lbl.text      = "PRESTIGE";
                lbl.fontSize  = 14;
                lbl.fontStyle = FontStyles.Bold;
                lbl.color     = Color.black;
                lbl.alignment = TextAlignmentOptions.Center;
                FillRect(labelGO.GetComponent<RectTransform>());
                ctrl.prestigeBadge = badge;
                Undo.RegisterCreatedObjectUndo(badge, "Create PrestigeBadge");
                count++;
            }
        }

        // ── 1f. Lay Low Button ────────────────────────────────────────────
        if (ctrl.layLowBtn == null)
        {
            // Find existing action buttons to place it next to
            var endBtn = FindInHierarchy(dashGO.transform, "EndButton")?.gameObject;
            Transform btnParent = endBtn != null ? endBtn.transform.parent : dashGO.transform;

            var layLowGO = new GameObject("LayLowButton");
            Undo.RegisterCreatedObjectUndo(layLowGO, "Create LayLowButton");
            GameObjectUtility.SetParentAndAlign(layLowGO, btnParent.gameObject);
            layLowGO.SetActive(false); // hidden until heat ≥ 5

            // Background image
            var img = layLowGO.AddComponent<Image>();
            img.color = new Color(0.55f, 0.25f, 0.08f);
            var rt = layLowGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 55);
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 70);

            // Button component
            var btn = layLowGO.AddComponent<Button>();
            var btnUI = layLowGO.AddComponent<ButtonUI>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.7f, 0.35f, 0.1f);
            btn.colors = cb;

            // Label
            var lblGO  = FindOrCreateTMPro(layLowGO.transform, "LayLowLabel", font);
            var lbl    = lblGO.GetComponent<TextMeshProUGUI>();
            lbl.text      = "🕵 LAY LOW";
            lbl.fontSize  = 24;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color     = Color.white;
            lbl.alignment = TextAlignmentOptions.Center;
            FillRect(lblGO.GetComponent<RectTransform>());

            // Cost hint inside button
            var costGO  = FindOrCreateTMPro(layLowGO.transform, "LayLowCostHint", font);
            var cost    = costGO.GetComponent<TextMeshProUGUI>();
            cost.text      = "£1,500";
            cost.fontSize  = 18;
            cost.color     = new Color(1f, 0.85f, 0.5f);
            cost.alignment = TextAlignmentOptions.Center;
            var costRt = costGO.GetComponent<RectTransform>();
            costRt.anchorMin = new Vector2(0, 0);
            costRt.anchorMax = new Vector2(1, 0);
            costRt.anchoredPosition = new Vector2(0, 6);
            costRt.sizeDelta = new Vector2(0, 20);

            ctrl.layLowBtn     = btnUI;
            ctrl.layLowCostHint = cost;
            count++;
        }

        // ── 1g. Rank Change Popup ─────────────────────────────────────────
        if (ctrl.rankChangePopupText == null)
        {
            var popupGO = FindOrCreateTMPro(dashGO.transform, "RankChangePopup", font);
            var tmp     = popupGO.GetComponent<TextMeshProUGUI>();
            tmp.text      = "↑ CLIMBED TO #1";
            tmp.fontSize  = 38;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = new Color(0.2f, 0.85f, 0.2f);
            tmp.alignment = TextAlignmentOptions.Center;
            var rt = popupGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.sizeDelta = new Vector2(0, 60);
            rt.anchoredPosition = new Vector2(0, 80);
            popupGO.SetActive(false);
            // Add CanvasGroup for fade
            popupGO.AddComponent<CanvasGroup>();
            ctrl.rankChangePopupText = tmp;
            Undo.RegisterCreatedObjectUndo(popupGO, "Create RankChangePopup");
            count++;
        }

        // ── 1h. Police Heat Slider wiring ─────────────────────────────────
        if (ctrl.policeHeatFill == null)
        {
            var existingSlider = FindInHierarchy(dashGO.transform, "Slider")?.gameObject;
            if (existingSlider != null)
            {
                var slider = existingSlider.GetComponent<Slider>();
                if (slider != null)
                {
                    ctrl.policeHeatFill = slider;
                    EditorUtility.SetDirty(ctrl);
                    count++;
                }
            }
        }

        // ── 1i. Bribe Police Panel wiring ──────────────────────────────────
        if (ctrl.bribePolicePanel == null)
        {
            var bribeGO = FindGameObjectInScene(dashGO.scene, "BribePolice");
            if (bribeGO != null)
            {
                ctrl.bribePolicePanel = bribeGO;
                EditorUtility.SetDirty(ctrl);
                count++;
            }
        }

        EditorUtility.SetDirty(ctrl);
        return count;
    }

    // =========================================================================
    //  PLAN AWAY TRIP
    // =========================================================================
    static int PatchPlanAwayTrip(GameObject awayGO, TMP_FontAsset font)
    {
        int count = 0;
        var ctrl = awayGO.GetComponent<PlanAwayTripController>();
        if (ctrl == null) { Debug.LogWarning("[StrategyUI] PlanAwayTripController missing."); return 0; }

        // Find the detail panel — "SelectedInfo" or Result panel
        var detailRoot = FindInHierarchy(awayGO.transform, "SelectedInfo")
                      ?? FindInHierarchy(awayGO.transform, "Result")
                      ?? awayGO.transform;

        // ── 2a. Archetype text ────────────────────────────────────────────
        if (ctrl.detailArchetypeText == null)
        {
            var go  = FindOrCreateTMPro(detailRoot, "ArchetypeText", font);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text     = "BRAWLER — hits hard, fights dirty.";
            tmp.fontSize = 22;
            tmp.color    = new Color(0.9f, 0.55f, 0.1f);
            tmp.fontStyle = FontStyles.Italic;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 32);
            ctrl.detailArchetypeText = tmp;
            Undo.RegisterCreatedObjectUndo(go, "Create ArchetypeText");
            count++;
        }

        // ── 2b. Motto text ────────────────────────────────────────────────
        if (ctrl.detailMottoText == null)
        {
            var go  = FindOrCreateTMPro(detailRoot, "MottoText", font);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text      = "\"We hit first, we hit hardest.\"";
            tmp.fontSize  = 20;
            tmp.color     = new Color(0.65f, 0.65f, 0.65f);
            tmp.fontStyle = FontStyles.Italic;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 28);
            ctrl.detailMottoText = tmp;
            Undo.RegisterCreatedObjectUndo(go, "Create MottoText");
            count++;
        }

        // ── 2c. Net Result preview ────────────────────────────────────────
        if (ctrl.detailNetResultText == null)
        {
            var go  = FindOrCreateTMPro(detailRoot, "NetResultText", font);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text      = "EXPECTED NET: +£2,800";
            tmp.fontSize  = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color     = new Color(0.2f, 0.85f, 0.2f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 36);
            rt.anchoredPosition = new Vector2(0, -10);
            ctrl.detailNetResultText = tmp;
            Undo.RegisterCreatedObjectUndo(go, "Create NetResultText");
            count++;
        }

        // ── 2d. Infamy label ──────────────────────────────────────────────
        if (ctrl.detailInfamyText == null)
        {
            var go  = FindOrCreateTMPro(detailRoot, "InfamyText", font);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text     = "⚠ INFAMY x2 — Police presence elevated from repeat visits.";
            tmp.fontSize = 20;
            tmp.color    = new Color(0.9f, 0.6f, 0.1f);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 30);
            go.SetActive(false);
            ctrl.detailInfamyText = tmp;
            Undo.RegisterCreatedObjectUndo(go, "Create InfamyText");
            count++;
        }

        // ── 2e. Lock Overlay ──────────────────────────────────────────────
        if (ctrl.detailLockOverlay == null)
        {
            var overlayGO = new GameObject("LockOverlay");
            Undo.RegisterCreatedObjectUndo(overlayGO, "Create LockOverlay");
            GameObjectUtility.SetParentAndAlign(overlayGO, detailRoot.gameObject);
            var img = overlayGO.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.75f);
            FillRect(overlayGO.GetComponent<RectTransform>());
            overlayGO.SetActive(false);

            // Lock reason text inside overlay
            var reasonGO  = FindOrCreateTMPro(overlayGO.transform, "LockReasonText", font);
            var reasonTmp = reasonGO.GetComponent<TextMeshProUGUI>();
            reasonTmp.text      = "🔒 REQUIRES 50 REPUTATION\n(Current: 10)";
            reasonTmp.fontSize  = 28;
            reasonTmp.fontStyle = FontStyles.Bold;
            reasonTmp.color     = Color.white;
            reasonTmp.alignment = TextAlignmentOptions.Center;
            FillRect(reasonGO.GetComponent<RectTransform>());

            ctrl.detailLockOverlay   = overlayGO;
            ctrl.detailLockReasonText = reasonTmp;
            count++;
        }

        EditorUtility.SetDirty(ctrl);
        return count;
    }

    // =========================================================================
    //  RANKINGS
    // =========================================================================
    static int PatchRankings(GameObject rankGO, TMP_FontAsset font)
    {
        int count = 0;
        var ctrl = rankGO.GetComponent<RankingsController>();
        if (ctrl == null) { Debug.LogWarning("[StrategyUI] RankingsController missing."); return 0; }

        // Find "YourClubRank" panel
        var ycr = FindInHierarchy(rankGO.transform, "YourClubRank");
        Transform cardParent = ycr ?? rankGO.transform;

        // ── 3a. Unlock Status Text ────────────────────────────────────────
        if (ctrl.yourUnlockStatusText == null)
        {
            var go  = FindOrCreateTMPro(cardParent, "UnlockStatusText", font);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text     = "Reach Top 5 to unlock better recruits.";
            tmp.fontSize = 20;
            tmp.color    = new Color(0.7f, 0.7f, 0.3f);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 32);
            ctrl.yourUnlockStatusText = tmp;
            Undo.RegisterCreatedObjectUndo(go, "Create UnlockStatusText");
            count++;
        }

        // ── 3b. Morale text on player card ────────────────────────────────
        if (ctrl.yourMoraleText == null)
        {
            var go  = FindOrCreateTMPro(cardParent, "YourMoraleText", font);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text     = "MORALE: HIGH";
            tmp.fontSize = 22;
            tmp.color    = new Color(0.2f, 0.85f, 0.2f);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 30);
            ctrl.yourMoraleText = tmp;
            Undo.RegisterCreatedObjectUndo(go, "Create YourMoraleText");
            count++;
        }

        EditorUtility.SetDirty(ctrl);
        return count;
    }

    // =========================================================================
    //  RECRUIT FANS
    // =========================================================================
    static int PatchRecruitFans(GameObject recGO, TMP_FontAsset font)
    {
        int count = 0;
        var ctrl = recGO.GetComponent<RecruitFansController>();
        if (ctrl == null) { Debug.LogWarning("[StrategyUI] RecruitFansController missing."); return 0; }

        // Find top panel if present
        var topPanel = FindInHierarchy(recGO.transform, "TopPanel");
        Transform topParent = topPanel ?? recGO.transform;

        // ── 4a. Top Morale Text ───────────────────────────────────────────
        if (ctrl.topMoraleText == null)
        {
            var go  = FindOrCreateTMPro(topParent, "TopMoraleText", font);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text     = "MORALE: HIGH";
            tmp.fontSize = 22;
            tmp.color    = new Color(0.2f, 0.85f, 0.2f);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 30);
            ctrl.topMoraleText = tmp;
            Undo.RegisterCreatedObjectUndo(go, "Create TopMoraleText");
            count++;
        }

        // ── 4b. Watchlist Warning Banner ──────────────────────────────────
        if (ctrl.watchlistWarningBanner == null)
        {
            var bannerGO = new GameObject("WatchlistWarningBanner");
            Undo.RegisterCreatedObjectUndo(bannerGO, "Create WatchlistWarningBanner");
            GameObjectUtility.SetParentAndAlign(bannerGO, recGO);

            var bg = bannerGO.AddComponent<Image>();
            bg.color = new Color(0.5f, 0.05f, 0.05f, 0.92f);
            var rt = bannerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(0, 50);
            rt.anchoredPosition = new Vector2(0, -55);

            var warnGO  = FindOrCreateTMPro(bannerGO.transform, "WatchlistText", font);
            var warnTmp = warnGO.GetComponent<TextMeshProUGUI>();
            warnTmp.text      = "⚠ POLICE ARE WATCHING — recruiting reduced.";
            warnTmp.fontSize  = 22;
            warnTmp.fontStyle = FontStyles.Bold;
            warnTmp.color     = new Color(1f, 0.75f, 0.2f);
            warnTmp.alignment = TextAlignmentOptions.Center;
            FillRect(warnGO.GetComponent<RectTransform>());

            bannerGO.SetActive(false);
            ctrl.watchlistWarningBanner = bannerGO;
            ctrl.watchlistWarningText   = warnTmp;
            count++;
        }

        EditorUtility.SetDirty(ctrl);
        return count;
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================

    static GameObject FindOrCreateTMPro(Transform parent, string name, TMP_FontAsset font)
    {
        // Check if already exists
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.enableAutoSizing = false;
        tmp.fontSize = 24;
        tmp.color    = Color.white;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(0, 30);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static GameObject FindOrCreateImageGO(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go  = new GameObject(name);
        GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
        go.AddComponent<Image>();
        return go;
    }

    static void PlaceBelow(RectTransform rt, RectTransform above, RectTransform fallback, float offset)
    {
        if (rt == null) return;
        if (above != null)
            rt.anchoredPosition = new Vector2(0, above.anchoredPosition.y - above.sizeDelta.y * 0.5f - offset);
        else
            rt.anchoredPosition = new Vector2(0, -offset);
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

    static int PatchBribePolice(GameObject bribeGO, TMP_FontAsset font)
    {
        int count = 0;
        var ctrl = bribeGO.GetComponent<BribePoliceController>();
        if (ctrl == null)
        {
            ctrl = bribeGO.AddComponent<BribePoliceController>();
            Undo.RegisterCreatedObjectUndo(ctrl, "Add BribePoliceController");
            count++;
        }

        // Find child elements to wire
        if (ctrl.bribeAmountText == null)
        {
            var cashAmtTransform = FindInHierarchy(bribeGO.transform, "cash amount (TMP)");
            if (cashAmtTransform != null)
            {
                ctrl.bribeAmountText = cashAmtTransform.GetComponent<TextMeshProUGUI>();
                count++;
            }
        }

        if (ctrl.bribeBtn == null)
        {
            var bribeBtnTransform = FindInHierarchy(bribeGO.transform, "BribeBtn");
            if (bribeBtnTransform != null)
            {
                ctrl.bribeBtn = bribeBtnTransform.GetComponent<ButtonUI>();
                count++;
            }
        }

        if (ctrl.cancelBtn == null)
        {
            var cancelBtnTransform = FindInHierarchy(bribeGO.transform, "CancelBtn");
            if (cancelBtnTransform != null)
            {
                ctrl.cancelBtn = cancelBtnTransform.GetComponent<ButtonUI>();
                count++;
            }
        }

        EditorUtility.SetDirty(ctrl);
        return count;
    }
}
#endif
