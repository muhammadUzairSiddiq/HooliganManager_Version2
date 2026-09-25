using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static LandscapeUI;

/// <summary>Landscape modal shared by gameplay and HQ. Long lists scroll inside the dialog.</summary>
public class GamePopup : MonoBehaviour
{
    public struct Option
    {
        public string Label; public Color Color; public Action Action;
        public Option(string label,Color color,Action action) {Label=label;Color=color;Action=action;}
    }
    static GamePopup instance;
    public static GamePopup Instance
    {
        get { if(!instance) {instance=new GameObject("GamePopup").AddComponent<GamePopup>();instance.Build();}return instance; }
    }
    GameObject root;
    Image dialog,accent,dimmer;
    TextMeshProUGUI title,body;
    RectTransform buttons;
    ScrollRect scroll;
    public static bool AnyOpen => instance && instance.IsOpen;
    public bool IsOpen => root && root.activeSelf;
    Action hideCallback;
    Coroutine autoHide;
    public void Show(string heading,string message,params Option[] options)=>Show(heading,message,null,options);
    public void ShowTimed(string heading,string message,float seconds=1.6f)
    {
        Show(heading,message,new Option("OK",PanelColor,null));
        autoHide=StartCoroutine(AutoHide(seconds));
    }
    IEnumerator AutoHide(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        autoHide=null;
        if(IsOpen) Hide();
    }
    public void Show(string heading,string message,Action onCancel,params Option[] options)
    {
        if(!root) Build();
        if(autoHide!=null){StopCoroutine(autoHide);autoHide=null;}
        hideCallback=onCancel;
        Color tone=ToneFor(heading);
        GameAudio.Play("popup");root.SetActive(true);title.text=heading;body.text=message;body.richText=true;body.overflowMode=TextOverflowModes.Overflow;Clear(buttons);
        if(accent)accent.color=tone;
        if(title)title.color=tone;
        if(dimmer)dimmer.color=new Color(tone.r*.20f,tone.g*.20f,tone.b*.20f,.44f);
        var outline=dialog?dialog.GetComponent<Outline>():null;
        if(outline)outline.effectColor=new Color(tone.r,tone.g,tone.b,.65f);
        if(options==null || options.Length==0) options=new[]{new Option("CLOSE",PanelColor,null)};
        bool longList=options.Length>3;
        var grid=buttons.GetComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount=longList?2:Mathf.Max(1,options.Length);
        float optionWidth=longList?681f:(1376f-14f*Mathf.Max(0,options.Length-1))/Mathf.Max(1,options.Length);
        grid.cellSize=new Vector2(optionWidth,72);
        foreach(var item in options)
        {
            var captured=item;var b=Button("Option_"+item.Label,buttons,item.Label,0,0,grid.cellSize.x,68);
            var image=b.GetComponent<Image>();
            if(image)
            {
                var theme=LandscapeTheme.Current;
                if(theme&&item.Color.r>item.Color.g*1.25f)image.sprite=theme.redButton;
                else if(theme&&item.Color.g>item.Color.r*1.15f)image.sprite=theme.greenButton;
                else if(theme&&item.Color.b>item.Color.r*1.15f)image.sprite=theme.outlineButton;
                if(image.sprite)image.type=UnityEngine.UI.Image.Type.Sliced;
                image.color=Color.white;
            }
            b.onClick.AddListener(()=>{var act=captured.Action;hideCallback=null;Hide();act?.Invoke();});
        }
        Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;
    }
    public void Hide()
    {
        if(autoHide!=null){StopCoroutine(autoHide);autoHide=null;}
        var cancel=hideCallback;hideCallback=null;
        if(root) root.SetActive(false);
        cancel?.Invoke();
    }
    void Build()
    {
        var canvas=new GameObject("GamePopupCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);
        var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=30000;
        LandscapeUI.ConfigureLandscapeScaler(canvas.GetComponent<CanvasScaler>());
        var safe=Rect("SafeArea",canvas.transform,0,0,1600,900);Stretch(safe);
        var frame=Rect("Frame",safe,0,0,1600,900);var viewport=safe.gameObject.AddComponent<LandscapeViewport>();viewport.frame=frame;viewport.Fit();
        root=Rect("Modal",frame,0,0,1600,900).gameObject;
        dimmer=Image("Dimmer",root.transform,0,0,1600,900,null,new Color(.04f,.08f,.12f,.44f));dimmer.raycastTarget=true;
        dimmer.gameObject.AddComponent<UiWorldTapBlocker>();
        dialog=Panel("Dialog",root.transform,48,36,1504,828,true);
        Image("TitleBand",dialog.transform,28,20,1448,96,null,new Color(.01f,.02f,.03f,.52f));
        accent=Image("Accent",dialog.transform,28,20,1448,8,null,Red);
        title=Text("Title",dialog.transform,"",48,36,1320,64,46,Red,true,TextAlignmentOptions.Center);
        var closeX=Button("CloseX",dialog.transform,"X",1416,28,64,60,"red");
        closeX.onClick.AddListener(Hide);
        body=Text("Body",dialog.transform,"",64,140,1376,400,30,White,false,TextAlignmentOptions.Center);
        body.richText=true;body.overflowMode=TextOverflowModes.Overflow;body.enableWordWrapping=true;body.fontSizeMin=20;
        scroll=Scroll("Options",dialog.transform,64,560,1376,230,grid:true);buttons=scroll.content;
        var grid=buttons.gameObject.AddComponent<GridLayoutGroup>();grid.spacing=new Vector2(14,14);grid.padding=new RectOffset(6,6,6,6);
        root.SetActive(false);
    }

    static Color ToneFor(string heading)
    {
        string value=(heading??string.Empty).ToUpperInvariant();
        if(value.Contains("DEFEAT")||value.Contains("FAILED")||value.Contains("POLICE")||value.Contains("RIVAL"))return Red;
        if(value.Contains("COMPLETE")||value.Contains("SECURED")||value.Contains("SUCCESS")||value.Contains("RECOVERY")||value.Contains("CONFIRMED")||value.Contains("JOINED"))return Green;
        if(value.Contains("WARNING")||value.Contains("HEAT")||value.Contains("ESCALATION")||value.Contains("POWER")||value.Contains("FUNDS"))return Gold;
        return new Color(.20f,.78f,1f);
    }
}
