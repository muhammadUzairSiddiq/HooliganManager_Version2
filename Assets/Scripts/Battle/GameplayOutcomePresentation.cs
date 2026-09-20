using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One clear, colour-coded result screen for gameplay actions. It deliberately
/// shows only the action and the consequence so players never have to decode
/// the feed after completing a task.
/// </summary>
public sealed class GameplayOutcomePresentation : MonoBehaviour
{
    static GameplayOutcomePresentation instance;
    GameObject root;
    CanvasGroup group;
    Image accent;
    TextMeshProUGUI kicker,title,detail,heatValue;
    Image[] heatBars;
    Coroutine autoClose;

    public static void TaskStarted(string action,string destination)
        => Instance.Show("TASK STARTED",action,destination,new Color(.16f,.68f,1f),-1,-1,1.35f,false);

    public static void TaskComplete(string action,string reward,int heatBefore,int heatAfter)
        => Instance.Show("TASK COMPLETE",action,reward,LandscapeUI.Green,heatBefore,heatAfter,3.2f,true);

    public static void Message(string heading,string action,string detail,Color color,float seconds=2.4f)
        => Instance.Show(heading,action,detail,color,-1,-1,seconds,true);

    public static void DefeatCinematic()
        => Instance.Show("FIRM DEFEATED","ALL MEMBERS ARE DOWN","Regroup, recover, then try again.",new Color(.82f,.25f,.28f),-1,-1,2.3f,false);

    static GameplayOutcomePresentation Instance
    {
        get
        {
            if(instance)return instance;
            var go=new GameObject("Gameplay Outcome Presentation");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<GameplayOutcomePresentation>();
            instance.Build();
            return instance;
        }
    }

    void OnDestroy(){if(instance==this)instance=null;}

    void Build()
    {
        var canvasGo=new GameObject("Outcome Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform,false);
        var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=28500;
        LandscapeUI.ConfigureLandscapeScaler(canvasGo.GetComponent<CanvasScaler>());

        root=LandscapeUI.Rect("Outcome Root",canvasGo.transform,0,0,1600,900).gameObject;
        group=root.AddComponent<CanvasGroup>();
        var dim=LandscapeUI.Image("Dim",root.transform,0,0,1600,900,null,new Color(.015f,.025f,.04f,.66f));dim.raycastTarget=true;
        var panel=LandscapeUI.Panel("Outcome Panel",root.transform,390,242,820,410,true);
        LandscapeUI.Image("Header",panel.transform,24,24,772,70,null,new Color(.01f,.02f,.03f,.54f));
        accent=LandscapeUI.Image("Accent",panel.transform,24,24,772,7,null,LandscapeUI.Green);
        kicker=LandscapeUI.Text("Kicker",panel.transform,"TASK COMPLETE",45,43,730,24,18,LandscapeUI.Green,true,TextAlignmentOptions.Center);
        title=LandscapeUI.Text("Title",panel.transform,"",45,92,730,58,39,LandscapeUI.White,true,TextAlignmentOptions.Center);
        detail=LandscapeUI.Text("Detail",panel.transform,"",68,164,684,62,22,LandscapeUI.White,false,TextAlignmentOptions.Center);

        var heat=LandscapeUI.Panel("Heat Result",panel.transform,116,244,588,68,false);
        heat.color=new Color(.02f,.035f,.05f,.92f);
        heatValue=LandscapeUI.Text("HeatLabel",heat.transform,"",20,8,548,20,15,LandscapeUI.Gold,true,TextAlignmentOptions.Center);
        heatBars=new Image[10];
        for(int i=0;i<heatBars.Length;i++)
            heatBars[i]=LandscapeUI.Image("Heat"+i,heat.transform,20+i*55,35,45,17,null,new Color(.18f,.2f,.22f,.9f));
        heat.gameObject.SetActive(false);

        var continueButton=LandscapeUI.Button("Continue",panel.transform,"CONTINUE",262,330,296,54,"dark");
        continueButton.onClick.AddListener(Hide);
        root.SetActive(false);
    }

    void Show(string top,string action,string text,Color color,int before,int after,float seconds,bool sound)
    {
        if(!root)Build();
        if(autoClose!=null)StopCoroutine(autoClose);
        root.SetActive(true);group.alpha=1;group.blocksRaycasts=true;
        kicker.text=top;title.text=action;detail.text=text;accent.color=color;kicker.color=color;
        var heat=heatValue.transform.parent.gameObject;
        bool showHeat=before>=0&&after>=0;
        heat.SetActive(showHeat);
        if(showHeat)StartCoroutine(AnimateHeat(before,after,color));
        if(sound)GameAudio.PlayPresentationTheme(color==LandscapeUI.Red?"defeat":"success");
        autoClose=StartCoroutine(AutoClose(seconds));
    }

    IEnumerator AnimateHeat(int before,int after,Color tone)
    {
        before=Mathf.Clamp(before,0,10);after=Mathf.Clamp(after,0,10);
        float elapsed=0f;
        while(elapsed<.8f)
        {
            elapsed+=Time.unscaledDeltaTime;
            int shown=Mathf.RoundToInt(Mathf.Lerp(before,after,Mathf.SmoothStep(0,1,elapsed/.8f)));
            UpdateHeat(shown,after,tone);
            yield return null;
        }
        UpdateHeat(after,after,tone);
    }

    void UpdateHeat(int current,int final,Color tone)
    {
        heatValue.text=$"POLICE HEAT   {current}/10  →  {final}/10";
        for(int i=0;i<heatBars.Length;i++)
        {
            bool lit=i<current;
            float t=i/9f;
            heatBars[i].color=lit?Color.Lerp(new Color(1f,.72f,.16f),new Color(1f,.16f,.16f),t):new Color(.16f,.18f,.21f,.9f);
        }
    }

    IEnumerator AutoClose(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Hide();
    }

    void Hide()
    {
        if(autoClose!=null){StopCoroutine(autoClose);autoClose=null;}
        if(root)root.SetActive(false);
    }
}
