using UnityEngine;
public sealed class CityLocationLabel : MonoBehaviour
{
    Transform label;
    RectTransform pin,frame;
    public int LocationIndex;
    void Start(){label=transform.Find("ZoneLabel");}
    void LateUpdate()
    {
        if(!Camera.main)return;
        if(!pin)
        {
            var hud=FindFirstObjectByType<LandscapeBattleHUD>();if(!hud)return;
            frame=hud.frame;
            var button=LandscapeUI.Button("LocationPin"+LocationIndex,frame,name,0,0,185,43);
            button.onClick.AddListener(()=>CityGameplay.Instance?.OpenLocation(LocationIndex));
            pin=button.transform as RectTransform;pin.SetAsFirstSibling();
            CityMissionHUD.Style(pin);
            if(label)label.gameObject.SetActive(false);
        }
        Vector3 projected=Camera.main.WorldToScreenPoint(transform.position+Vector3.up*4);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,projected,null,out var local);
        float x=local.x-frame.rect.xMin,y=frame.rect.yMax-local.y;
        bool visible=projected.z>0 && x>380 && x<1190 && y>190 && y<695 && Vector3.Distance(Camera.main.transform.position,transform.position)<260;
        pin.gameObject.SetActive(visible);
        if(visible)LandscapeUI.Place(pin,x-92,y-21,185,43);
    }
    void OnDestroy(){if(pin)Destroy(pin.gameObject);}
}
