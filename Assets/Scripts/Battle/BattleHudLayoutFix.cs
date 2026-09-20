using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Professional battle HUD layout:
///   1) Full-width top bar — MEMBERS | TIMER/MATCH | CASH/REP
///   2) Police heat — directly under the top bar
///   3) Level / objective — below the heat bar (no overlap)
/// Disables the legacy scene TopPanel (red/blue sliders, logos).
/// </summary>
public class BattleHudLayoutFix : MonoBehaviour
{
    private const float TopBarHeight = 88f;
    private const float HeatBarHeight = 30f;
    private const float HeatGap = 8f;
    private const float SidePad = 24f;

    private static BattleHudLayoutFix _instance;
    private RectTransform _topBar;
    private TextMeshProUGUI _cashLabel;
    private TextMeshProUGUI _repLabel;
    private TextMeshProUGUI _hintLabel;
    private Image _membersIcon;
    private bool _applied;
    private bool _legacyChromeHidden;

    public static void EnsureExists()
    {
        if (FindFirstObjectByType<LandscapeBattleHUD>() != null) return;
        if (_instance == null)
        {
            var go = new GameObject("BattleHudLayoutFix");
            _instance = go.AddComponent<BattleHudLayoutFix>();
        }
        _instance.TryApply();
    }

    private void TryApply()
    {
        if (_applied) return;
        if (BattleUIController.instance == null) return;
        Apply();
        _applied = true;
    }

    private void LateUpdate()
    {
        if (!_applied) TryApply();
        if (_applied)
        {
            if (!_legacyChromeHidden) HideLegacyTopPanel();
            LayoutFrame();
        }
    }

    private void Apply()
    {
        var ui = BattleUIController.instance;
        if (ui == null) return;

        // Rescue HUD texts out of TopPanel before we disable it.
        RescueHudTexts(ui);
        HideLegacyTopPanel();
        HideLegacyHeatWidgets(ui);

        if (ui.enemyCountText != null)
        {
            ui.enemyCountText.text = "";
            ui.enemyCountText.gameObject.SetActive(false);
        }

        if (ui.objectiveText != null)
            ui.objectiveText.gameObject.SetActive(false);

        BuildTopBar(ui);
        BuildCashAndRep();
        StyleMembers(ui);
        StyleTimer(ui);
        BuildControlsHint();
        StartCoroutine(DeferredLayout());
    }

    private System.Collections.IEnumerator DeferredLayout()
    {
        yield return null;
        yield return null;
        HideLegacyTopPanel();
        LayoutFrame();
        yield return new WaitForSecondsRealtime(0.2f);
        HideLegacyTopPanel();
        LayoutFrame();
    }

    /// <summary>
    /// Reparent members / timer / MATCH onto the canvas root so disabling
    /// TopPanel does not destroy the live HUD texts.
    /// </summary>
    private static void RescueHudTexts(BattleUIController ui)
    {
        Transform canvas = ui.transform;
        var c = ui.GetComponentInParent<Canvas>();
        if (c != null) canvas = c.transform;

        void Rescue(TextMeshProUGUI tmp)
        {
            if (tmp == null) return;
            // Only lift out of TopPanel / Gameplay chrome — keep under canvas.
            var p = tmp.transform.parent;
            if (p == null) return;
            string pn = p.name;
            if (pn == "TopPanel" || pn == "Gameplay" || pn.Contains("Slider") ||
                pn.Contains("logo") || pn == "image")
                tmp.transform.SetParent(canvas, true);
        }

        Rescue(ui.playerCountText);
        Rescue(ui.roundTimerText);
        Rescue(ui.roundLabel);
        Rescue(ui.enemyCountText);
    }

    /// <summary>Disable every scene TopPanel (red/blue logos + sliders).</summary>
    private void HideLegacyTopPanel()
    {
        var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool found = false;
        foreach (var t in all)
        {
            if (t == null) continue;
            if (t.name == "TopPanel")
            {
                if (t.gameObject.activeSelf)
                    t.gameObject.SetActive(false);
                found = true;
            }
            else if (t.name == "Slider(red)" || t.name == "Slider(blue)" ||
                     t.name == "redlogo" || t.name == "bluelogo" || t.name == "blueNumber0")
            {
                if (t.gameObject.activeSelf)
                    t.gameObject.SetActive(false);
            }
        }
        if (found) _legacyChromeHidden = true;
    }

    private static void HideLegacyHeatWidgets(BattleUIController ui)
    {
        if (ui.policeHeatBarLabel != null)
            ui.policeHeatBarLabel.gameObject.SetActive(false);
        if (ui.policeHeatFill != null)
        {
            ui.policeHeatFill.gameObject.SetActive(false);
            var parent = ui.policeHeatFill.transform.parent;
            if (parent != null && parent != ui.transform && parent.name != "TopHudBar")
                parent.gameObject.SetActive(false);
        }
    }

    private void BuildTopBar(BattleUIController ui)
    {
        Transform parent = ui.transform;
        var canvas = ui.GetComponentInParent<Canvas>();
        if (canvas != null) parent = canvas.transform;

        var go = new GameObject("TopHudBar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();

        _topBar = go.GetComponent<RectTransform>();
        _topBar.anchorMin = new Vector2(0f, 1f);
        _topBar.anchorMax = new Vector2(1f, 1f);
        _topBar.pivot = new Vector2(0.5f, 1f);
        _topBar.anchoredPosition = Vector2.zero;
        _topBar.sizeDelta = new Vector2(0f, TopBarHeight);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.04f, 0.045f, 0.055f, 0.94f);
        img.raycastTarget = false;

        var line = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(go.transform, false);
        var lrt = line.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(0f, 3f);
        line.GetComponent<Image>().color = new Color(0.85f, 0.18f, 0.16f, 0.95f);
        line.GetComponent<Image>().raycastTarget = false;
    }

    private void StyleMembers(BattleUIController ui)
    {
        if (ui.playerCountText == null) return;
        var tmp = ui.playerCountText;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.fontSize = 28f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        if (_membersIcon == null)
        {
            var iconGo = new GameObject("MembersIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_topBar != null ? _topBar : tmp.transform.parent, false);
            _membersIcon = iconGo.GetComponent<Image>();
            _membersIcon.color = new Color(0.92f, 0.22f, 0.2f, 1f);
            _membersIcon.raycastTarget = false;
        }
    }

    private void StyleTimer(BattleUIController ui)
    {
        if (ui.roundTimerText != null)
        {
            ui.roundTimerText.alignment = TextAlignmentOptions.Center;
            ui.roundTimerText.fontSize = 36f;
            ui.roundTimerText.fontStyle = FontStyles.Bold;
            ui.roundTimerText.color = Color.white;
            ui.roundTimerText.enableAutoSizing = false;
        }
        if (ui.roundLabel != null)
        {
            ui.roundLabel.alignment = TextAlignmentOptions.Center;
            ui.roundLabel.fontSize = 16f;
            ui.roundLabel.fontStyle = FontStyles.Bold;
            ui.roundLabel.color = new Color(0.75f, 0.78f, 0.85f, 1f);
            ui.roundLabel.text = "MATCH";
            ui.roundLabel.enableAutoSizing = false;
        }
    }

    private void BuildCashAndRep()
    {
        Transform parent = _topBar != null ? (Transform)_topBar : transform;

        var cashGo = new GameObject("CashLabel", typeof(RectTransform));
        cashGo.transform.SetParent(parent, false);
        _cashLabel = cashGo.AddComponent<TextMeshProUGUI>();
        _cashLabel.fontSize = 26f;
        _cashLabel.fontStyle = FontStyles.Bold;
        _cashLabel.alignment = TextAlignmentOptions.Right;
        _cashLabel.color = new Color(0.45f, 0.92f, 0.55f, 1f);
        _cashLabel.raycastTarget = false;
        _cashLabel.text = "£0";

        var repGo = new GameObject("RepLabel", typeof(RectTransform));
        repGo.transform.SetParent(parent, false);
        _repLabel = repGo.AddComponent<TextMeshProUGUI>();
        _repLabel.fontSize = 16f;
        _repLabel.fontStyle = FontStyles.Bold;
        _repLabel.alignment = TextAlignmentOptions.Right;
        _repLabel.color = new Color(0.85f, 0.88f, 0.95f, 0.9f);
        _repLabel.raycastTarget = false;
        _repLabel.text = "REP 0  ·  #—";
    }

    private void LayoutFrame()
    {
        var ui = BattleUIController.instance;
        if (ui == null || _topBar == null) return;

        // ── Full-bleed top bar ───────────────────────────────────────────
        _topBar.anchorMin = new Vector2(0f, 1f);
        _topBar.anchorMax = new Vector2(1f, 1f);
        _topBar.pivot = new Vector2(0.5f, 1f);
        _topBar.anchoredPosition = Vector2.zero;
        _topBar.offsetMin = new Vector2(0f, -TopBarHeight);
        _topBar.offsetMax = new Vector2(0f, 0f);

        // Columns (normalized) — no shared horizontal space:
        //   Members: left
        //   Timer:   dead centre
        //   Cash:    right (inset for minimap)

        // ── Members (left only) ──────────────────────────────────────────
        if (_membersIcon != null)
        {
            var ir = _membersIcon.rectTransform;
            ir.SetParent(_topBar, false);
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = new Vector2(SidePad + 14f, 0f);
            ir.sizeDelta = new Vector2(26f, 26f);
        }

        if (ui.playerCountText != null)
        {
            var rt = ui.playerCountText.rectTransform;
            rt.SetParent(_topBar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(SidePad + 36f, 0f);
            rt.sizeDelta = new Vector2(240f, 40f);
            ui.playerCountText.fontSize = 28f;
            ui.playerCountText.alignment = TextAlignmentOptions.Left;
        }

        // ── Timer + MATCH (centre of top bar only) ───────────────────────
        if (ui.roundTimerText != null)
        {
            var rt = ui.roundTimerText.rectTransform;
            rt.SetParent(_topBar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 10f);
            rt.sizeDelta = new Vector2(220f, 40f);
            ui.roundTimerText.fontSize = 36f;
            ui.roundTimerText.alignment = TextAlignmentOptions.Center;
        }

        if (ui.roundLabel != null)
        {
            var rt = ui.roundLabel.rectTransform;
            rt.SetParent(_topBar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -20f);
            rt.sizeDelta = new Vector2(140f, 22f);
            ui.roundLabel.fontSize = 16f;
            ui.roundLabel.alignment = TextAlignmentOptions.Center;
        }

        // ── Cash + Rep (right, clear of minimap) ─────────────────────────
        float rightInset = 200f;
        if (_cashLabel != null)
        {
            var rt = _cashLabel.rectTransform;
            rt.SetParent(_topBar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-rightInset, 10f);
            rt.sizeDelta = new Vector2(260f, 34f);
        }

        if (_repLabel != null)
        {
            var rt = _repLabel.rectTransform;
            rt.SetParent(_topBar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-rightInset, -18f);
            rt.sizeDelta = new Vector2(260f, 22f);
        }

        // ── Police heat: immediately under top bar ───────────────────────
        float heatTop = TopBarHeight + HeatGap;
        var heat = GameObject.Find("HeatBar");
        if (heat != null)
        {
            var rt = heat.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Keep on its own canvas but pin under the top bar.
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -heatTop);
                rt.sizeDelta = new Vector2(310f, HeatBarHeight);

                var bg = heat.GetComponent<Image>();
                if (bg != null) bg.color = new Color(0.05f, 0.055f, 0.07f, 0.9f);

                var label = heat.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    label.fontSize = 12f;
                    label.fontStyle = FontStyles.Bold;
                }
            }
        }

        // ── Level / objective: BELOW the heat bar (no overlap with members) ─
        float levelTop = heatTop + HeatBarHeight + 10f;
        var level = GameObject.Find("LevelPanel");
        if (level != null)
        {
            var rt = level.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Stay on LevelHudCanvas — position from top of screen.
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(SidePad, -levelTop);
                rt.sizeDelta = new Vector2(420f, rt.sizeDelta.y);

                var bg = level.GetComponent<Image>();
                if (bg != null) bg.color = new Color(0.04f, 0.045f, 0.055f, 0.82f);

                var vlg = level.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.padding = new RectOffset(14, 14, 10, 10);
                    vlg.spacing = 4f;
                }

                var title = level.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
                if (title != null) { title.fontSize = 24f; title.fontStyle = FontStyles.Bold; }
                var obj = level.transform.Find("Obj")?.GetComponent<TextMeshProUGUI>();
                if (obj != null) { obj.fontSize = 17f; }
            }
        }
    }

    private void BuildControlsHint()
    {
        var canvasGo = new GameObject("ControlsHintCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 13000;
        LandscapeUI.ConfigureLandscapeScaler(canvasGo.GetComponent<CanvasScaler>());

        var go = new GameObject("Hint", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasGo.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 12f);
        rt.sizeDelta = new Vector2(760f, 52f);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        _hintLabel = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        _hintLabel.transform.SetParent(go.transform, false);
        var tr = _hintLabel.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(10, 4); tr.offsetMax = new Vector2(-10, -4);
        _hintLabel.text = "DRAG to pan  ·  PINCH to zoom  ·  DOUBLE-TAP to recenter";
        _hintLabel.fontSize = 26f;
        _hintLabel.alignment = TextAlignmentOptions.Center;
        _hintLabel.color = new Color(0.92f, 0.94f, 0.98f);
        _hintLabel.raycastTarget = false;
    }

    void Update()
    {
        int cash = 0;
        int rep = 0;
        int rank = 0;
        if (GameData.instance?.PlayerData != null)
        {
            cash = GameData.instance.PlayerData.Money;
            rep = GameData.instance.PlayerData.Reputation;
            rank = GameData.instance.PlayerData.Ranking;
        }
        if (BattleManager.instance != null)
            cash += BattleManager.instance.sessionMoneyEarned;

        if (_cashLabel != null)
            _cashLabel.text = $"£{cash:N0}";
        if (_repLabel != null)
            _repLabel.text = $"REP {rep}  ·  #{rank}";
    }
}
