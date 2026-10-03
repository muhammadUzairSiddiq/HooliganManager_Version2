using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>One-word actions; details go to the selection/status line on focus.</summary>
public sealed class ConciseButtonCaption : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
{
    TextMeshProUGUI label;
    string detail, last;
    public static string Shorten(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return text;
        string clean=System.Text.RegularExpressions.Regex.Replace(text,"<.*?>","").Trim();
        if(clean=="SELECT ALL"||clean=="DESELECT ALL")return clean;
        if(clean.StartsWith("MOVE ON"))return "LEAVE";
        if(clean.StartsWith("START PATROL"))return "START";
        if(clean.StartsWith("SHOW NEXT"))return "OBJECTIVE";
        if(clean.StartsWith("PLAN &"))return "DEVELOP";
        if(clean.StartsWith("▢"))return "SELECT";
        if(clean.StartsWith("BRIBE"))return "PAY";
        if(clean.StartsWith("STAND &"))return "FIGHT";
        var words=clean.Split(new[]{' ','\n','\r','\t'},System.StringSplitOptions.RemoveEmptyEntries);
        return words.Length>0?words[0]:clean;
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
    float next;
    void Update()
    {
        if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;
        foreach(var button in FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(!button.GetComponent<ConciseButtonCaption>())button.gameObject.AddComponent<ConciseButtonCaption>();
    }
}
