using TMPro;
using UnityEngine;

/// <summary>Bounded, non-interactive feedback anchored to its actual world event.</summary>
public sealed class CityFeedback : MonoBehaviour
{
    static int count;
    RectTransform bubble, frame;
    Vector3 position;
    float expires;
    public static void At(Vector3 point,string message,Color color)
    {
        CityGameplay.Instance?.PostEvent(message.Replace('\n',' '));
        if(count>=3||!Camera.main)return;
        var hud=FindFirstObjectByType<LandscapeBattleHUD>();
        if(!hud||!hud.frame)return;
        var go=new GameObject("Local task feedback");
        var feedback=go.AddComponent<CityFeedback>();
        feedback.position=point;feedback.frame=hud.frame;feedback.expires=Time.unscaledTime+3.5f;count++;
        var panel=LandscapeUI.Panel("WorldEventBubble",hud.frame,0,0,260,60,false);
        panel.color=new Color(.025f,.05f,.065f,.9f);panel.raycastTarget=false;
        feedback.bubble=panel.rectTransform;
        var text=LandscapeUI.Text("Text",panel.transform,message,10,6,240,48,17,color,true,TextAlignmentOptions.Center);
        text.raycastTarget=false;text.overflowMode=TextOverflowModes.Ellipsis;
    }
    void LateUpdate()
    {
        if(Time.unscaledTime>=expires||!frame||!Camera.main){Destroy(gameObject);return;}
        var p=Camera.main.WorldToScreenPoint(position+Vector3.up*4);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,p,null,out var local);
        float x=local.x-frame.rect.xMin,y=frame.rect.yMax-local.y;
        bool visible=!AgentSelectionManager.BlocksWorldTap()&&p.z>0&&x>450&&x<frame.rect.width-456&&y>180&&y<frame.rect.height-70;
        visible=visible&&WorldAnnotationBudget.Reserve(Camera.main,position+Vector3.up*4,270,68);
        bubble.gameObject.SetActive(visible);
        if(visible)LandscapeUI.Place(bubble,x-130,y-30,260,60);
    }
    void OnDestroy(){count=Mathf.Max(0,count-1);if(bubble)Destroy(bubble.gameObject);}
}

/// <summary>One reusable pulse per HUD value; rapid changes coalesce, never stack windows.</summary>
public sealed class HudValuePulse : MonoBehaviour
{
    TextMeshProUGUI value, delta;
    string previous;
    Color original;
    float until;
    public static void Watch(TextMeshProUGUI text)
    {
        if(text&&!text.GetComponent<HudValuePulse>())text.gameObject.AddComponent<HudValuePulse>();
    }
    void Awake(){value=GetComponent<TextMeshProUGUI>();original=value.color;previous=value.text;}
    void LateUpdate()
    {
        if(value.text!=previous)
        {
            if(!string.IsNullOrEmpty(previous))
            {
                if(!delta)
                {
                    delta=LandscapeUI.Text("Change",transform,"",0,0,100,22,16,LandscapeUI.Gold,true,TextAlignmentOptions.Right);
                    delta.raycastTarget=false;
                    delta.rectTransform.anchorMin=delta.rectTransform.anchorMax=new Vector2(1,1);
                    delta.rectTransform.pivot=new Vector2(1,0);
                }
                int before,after=0;
                bool parsed=Number(previous,out before)&&Number(value.text,out after);
                int difference=parsed?after-before:0;
                delta.text=parsed&&difference!=0?(difference>0?"+":"")+difference.ToString("N0"):"UPDATED";
                until=Time.unscaledTime+1.8f;
            }
            previous=value.text;
        }
        float left=Mathf.Max(0,until-Time.unscaledTime);
        value.color=left>0?Color.Lerp(original,LandscapeUI.Gold,.45f+.45f*Mathf.Sin(left*15)):original;
        if(delta){delta.gameObject.SetActive(left>0);delta.color=new Color(1,.8f,.3f,Mathf.Clamp01(left));delta.rectTransform.anchoredPosition=new Vector2(0,3+(1.8f-left)*6);}
    }
    static bool Number(string text,out int number)
    {
        text=text.Replace("£","").Replace(",","");int slash=text.IndexOf('/');if(slash>=0)text=text.Substring(0,slash);
        return int.TryParse(text.Trim(),out number);
    }
    void OnDisable(){if(value)value.color=original;if(delta)delta.gameObject.SetActive(false);}
}
