using System;
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
    TextMeshProUGUI title,body;
    RectTransform buttons;
    ScrollRect scroll;
    public bool IsOpen => root && root.activeSelf;
    public void Show(string heading,string message,params Option[] options)
    {
        if(!root) Build();root.SetActive(true);title.text=heading;body.text=message;Clear(buttons);
        if(options==null || options.Length==0) options=new[]{new Option("CLOSE",PanelColor,null)};
        bool longList=options.Length>3;
        var grid=buttons.GetComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount=longList?2:Mathf.Max(1,options.Length);
        grid.cellSize=new Vector2(longList?438:((892-14*(options.Length-1))/options.Length),68);
        foreach(var item in options)
        {
            var captured=item;var b=Button("Option_"+item.Label,buttons,item.Label,0,0,grid.cellSize.x,68);
            b.GetComponent<Image>().color=Color.Lerp(Color.white,item.Color,.35f);
            b.onClick.AddListener(()=>{Hide();captured.Action?.Invoke();});
        }
        Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;
    }
    public void Hide() {if(root) root.SetActive(false);}
    void Build()
    {
        var canvas=new GameObject("GamePopupCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);
        var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=30000;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=LandscapeUI.Resolution;scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var safe=Rect("SafeArea",canvas.transform,0,0,1600,900);Stretch(safe);
        var frame=Rect("Frame",safe,0,0,1600,900);var viewport=safe.gameObject.AddComponent<LandscapeViewport>();viewport.frame=frame;viewport.Fit();
        root=Rect("Modal",frame,0,0,1600,900).gameObject;
        Image("Dimmer",root.transform,0,0,1600,900,null,new Color(0,0,0,.76f)).raycastTarget=true;
        var panel=Panel("Dialog",root.transform,306,180,988,540,true);
        Image("Accent",panel.transform,38,0,912,4,null,Red);
        title=Text("Title",panel.transform,"",43,27,902,65,39,null,true,TextAlignmentOptions.Center);
        body=Text("Body",panel.transform,"",48,115,892,145,25,Muted,false,TextAlignmentOptions.Center);
        scroll=Scroll("Options",panel.transform,42,285,904,218,grid:true);buttons=scroll.content;
        var grid=buttons.gameObject.AddComponent<GridLayoutGroup>();grid.spacing=new Vector2(14,14);grid.padding=new RectOffset(6,6,6,6);
        root.SetActive(false);
    }
}
