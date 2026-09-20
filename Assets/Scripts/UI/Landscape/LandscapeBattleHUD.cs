using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

/// <summary>Hosts late-created gameplay widgets in reserved landscape slots without changing their gameplay logic.</summary>
public sealed class LandscapeBattleHUD : MonoBehaviour
{
    public RectTransform frame, objectiveSlot, heatSlot;
    public TextMeshProUGUI cash, reputation;
    public GameObject result, pause;
    bool objectivePlaced, heatPlaced;
    float nextScan;
    RectTransform battleRoot;
    readonly Dictionary<RectTransform, RectState> authored = new Dictionary<RectTransform, RectState>();

    struct RectState
    {
        public Vector2 position, size;
        public RectState(RectTransform value) { position=value.anchoredPosition; size=value.sizeDelta; }
    }

    void LateUpdate()
    {
        FitResponsiveEdges();
        var d=GameData.instance?.PlayerData;
        if(cash) cash.text="£"+((d?.Money??0)+(BattleManager.instance?.sessionMoneyEarned??0)).ToString("N0");
        if(reputation)
        {
            reputation.richText=true;
            reputation.text="<color=#9BADB5>Reputation</color>  <color=#E8BA5A>"+(d?.Reputation??0)+"</color>"
                +"     <color=#9BADB5>Level</color>  <color=#E8F0F4>"+(d?.CurrentLevel??1)+"</color>";
        }
        if(Time.unscaledTime>nextScan)
        {
            nextScan=Time.unscaledTime+.35f;
            if(!objectivePlaced) objectivePlaced=Dock("LevelPanel",objectiveSlot,260,118);
            if(!heatPlaced) heatPlaced=Dock("HeatBar",heatSlot,260,40);
        }
        if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            var controller=BattlePauseMenuController.instance;
            if(controller) { if(controller.IsVisible) controller.Hide(); else controller.Show(); }
        }
    }

    /// <summary>Re-capture authored positions after runtime HUD builders reposition widgets.</summary>
    public void RefreshAuthoredLayout()
    {
        authored.Clear();
        battleRoot = frame ? frame.Find("BattleHUD") as RectTransform : null;
        if (!battleRoot) return;
        foreach (Transform child in battleRoot)
            if (child is RectTransform rt) authored[rt] = new RectState(rt);
    }

    /// <summary>Preserves the authored layout while using all spare safe-area width/height.</summary>
    void FitResponsiveEdges()
    {
        if (!frame) return;
        if (!battleRoot)
        {
            battleRoot = frame.Find("BattleHUD") as RectTransform;
            if (!battleRoot) return;
            foreach (Transform child in battleRoot)
                if (child is RectTransform rt) authored[rt] = new RectState(rt);
        }

        float width = Mathf.Max(1600f, frame.rect.width);
        float height = Mathf.Max(900f, frame.rect.height);
        battleRoot.anchorMin = battleRoot.anchorMax = battleRoot.pivot = new Vector2(0, 1);
        battleRoot.anchoredPosition = Vector2.zero;
        battleRoot.sizeDelta = new Vector2(width, height);
        float dx = width - 1600f, dy = height - 900f;

        foreach (var pair in authored)
        {
            var rt = pair.Key;
            if (!rt) continue;
            // Runtime command strip is positioned below after responsive pinning.
            if (rt.name == "Move" || rt.name == "Attack") continue;
            var state = pair.Value;
            float horizontal = HorizontalPin(rt.name);
            float vertical = BottomPinned(rt.name) ? 1f : 0f;
            rt.anchoredPosition = state.position + new Vector2(dx * horizontal, -dy * vertical);
            rt.sizeDelta = state.size;
            if (rt.name == "TopBar")
            {
                rt.anchoredPosition = new Vector2(0, state.position.y);
                rt.sizeDelta = new Vector2(width, state.size.y);
            }
        }

        PositionCommand("Move",365,dx,dy);
        PositionCommand("Attack",505,dx,dy);
        PositionCommand("Talk",645,dx,dy);
        PositionCommand("Capture",785,dx,dy);
        PositionCommand("Actions",925,dx,dy);
        PositionCommand("Retreat",1065,dx,dy);
    }

    void PositionCommand(string name, float x, float dx, float dy)
    {
        var rt = battleRoot.Find(name) as RectTransform;
        if (!rt || !rt.gameObject.activeSelf) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x + dx * .5f, -(812f + dy));
        rt.sizeDelta = new Vector2(name=="Retreat"?140f:130f, 54f);
        rt.localScale = Vector3.one;
    }

    static float HorizontalPin(string name)
    {
        switch (name)
        {
            case "Pause": case "EnemyCount": case "ObjectiveSlot": case "HeatSlot":
            case "CenterCamera": case "CameraSetup": case "RecenterCamera": return 1f;
            case "Cash": case "Reputation": return .58f;
            case "Timer": case "Round": return .78f;
            case "SelectionStatus": case "Talk": case "Capture": case "Actions": case "Retreat": case "Controls": return .5f;
            default: return 0f;
        }
    }

    static bool BottomPinned(string name)
    {
        switch (name)
        {
            case "SelectionStatus": case "Talk": case "Capture": case "Actions": case "Retreat": case "Controls":
            case "Joystick": case "CenterCamera": case "CameraSetup": case "RecenterCamera": return true;
            default: return false;
        }
    }
    static bool Dock(string name, RectTransform slot, float width, float height)
    {
        var go = GameObject.Find(name);
        if (!go || !slot) return false;
        var rt = go.transform as RectTransform;
        if (!rt) return false;
        rt.SetParent(slot, false);
        LandscapeUI.Place(rt, 0, 0, width, height);
        foreach (var graphic in go.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        if (name == "LevelPanel")
        {
            var title = go.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (title)
            {
                title.fontSize = 20;
                title.enableAutoSizing = true;
                title.fontSizeMax = 20;
                title.fontSizeMin = 16;
            }
            var obj = go.transform.Find("Obj")?.GetComponent<TextMeshProUGUI>();
            if (obj)
            {
                obj.fontSize = 15;
                obj.enableAutoSizing = true;
                obj.fontSizeMax = 15;
                obj.fontSizeMin = 13;
                obj.overflowMode = TextOverflowModes.Ellipsis;
                var le = obj.GetComponent<LayoutElement>();
                if (le) le.preferredHeight = 40;
            }
        }
        return true;
    }
}
