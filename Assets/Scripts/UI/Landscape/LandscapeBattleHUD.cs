using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>Hosts late-created gameplay widgets in reserved landscape slots without changing their gameplay logic.</summary>
public sealed class LandscapeBattleHUD : MonoBehaviour
{
    public RectTransform frame, objectiveSlot, heatSlot;
    public TextMeshProUGUI cash, reputation;
    public GameObject result, pause;
    bool objectivePlaced, heatPlaced;
    float nextScan;
    void LateUpdate()
    {
        var d=GameData.instance?.PlayerData;
        if(cash) cash.text="£"+((d?.Money??0)+(BattleManager.instance?.sessionMoneyEarned??0)).ToString("N0");
        if(reputation) reputation.text="REP "+(d?.Reputation??0)+"   /   LEVEL "+(d?.CurrentLevel??1);
        if(Time.unscaledTime>nextScan)
        {
            nextScan=Time.unscaledTime+.35f;
            if(!objectivePlaced) objectivePlaced=Dock("LevelPanel",objectiveSlot,270,152);
            if(!heatPlaced) heatPlaced=Dock("HeatBar",heatSlot,270,44);
        }
        if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            var controller=BattlePauseMenuController.instance;
            if(controller) { if(controller.IsVisible) controller.Hide(); else controller.Show(); }
        }
    }
    static bool Dock(string name,RectTransform slot,float width,float height)
    {
        var go=GameObject.Find(name); if(!go || !slot) return false;
        var rt=go.transform as RectTransform; if(!rt) return false;
        rt.SetParent(slot,false); LandscapeUI.Place(rt,0,0,width,height);
        foreach(var graphic in go.GetComponentsInChildren<Graphic>()) graphic.raycastTarget=false;
        if(name=="LevelPanel")
        {
            var title=go.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if(title) {title.fontSize=23;title.enableAutoSizing=true;title.fontSizeMax=23;title.fontSizeMin=19;}
            var obj=go.transform.Find("Obj")?.GetComponent<TextMeshProUGUI>();
            if(obj) {obj.fontSize=20;obj.enableAutoSizing=true;obj.fontSizeMax=20;obj.fontSizeMin=17;obj.overflowMode=TextOverflowModes.Ellipsis;
                var le=obj.GetComponent<LayoutElement>();if(le) le.preferredHeight=74;}
        }
        return true;
    }
}
