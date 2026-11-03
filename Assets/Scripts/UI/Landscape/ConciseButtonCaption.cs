using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Retains meaningful action labels, costs and consequences.</summary>
public sealed class ConciseButtonCaption : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
{
    TextMeshProUGUI label;
    string detail, last;
    public static string Shorten(string text)
    {
        // Blind first-word truncation made both incident buttons say ASSIGN,
        // removed loot costs/heat and hid what a confirmation would do.
        return text;
    }
    void LateUpdate()
    {
        if(!label)label=GetComponentInChildren<TextMeshProUGUI>();if(!label||label.text==last)return;
        detail=label.text;last=Shorten(detail);label.text=last;
    }
    public void OnPointerDown(PointerEventData data)=>ShowDetail();
    public void OnPointerEnter(PointerEventData data)=>ShowDetail();
    void ShowDetail(){if(!string.IsNullOrEmpty(detail)&&detail!=last)BattleUIController.instance?.ShowAlert(detail.Replace('\n',' '),2f);}
}
public sealed class ConciseCityButtons : MonoBehaviour
{
    // Kept for saved scene compatibility. Each surface owns its captions;
    // the former global rescan/rewriter no longer runs every half second.
}
