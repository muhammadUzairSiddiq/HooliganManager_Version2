using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CityArrivalSequence
{
    public static IEnumerator Play(Camera camera, CameraPanTouchOnly controller)
    {
        if(!camera || !BattleManager.instance.playerSpawnRoot)yield break;
        var home=BattleManager.instance.playerSpawnRoot.position;
        var root=new GameObject("CityArrival",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=25000;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        var frame=LandscapeUI.Rect("Frame",root.transform,0,0,1600,900);
        frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,.5f);frame.anchoredPosition=Vector2.zero;
        LandscapeUI.Image("InputShield",frame,0,0,1600,900,null,Color.clear).raycastTarget=true;
        LandscapeUI.Image("TopLetterbox",frame,0,0,1600,105,null,LandscapeUI.Ink);
        LandscapeUI.Image("BottomLetterbox",frame,0,695,1600,205,null,LandscapeUI.Ink);
        LandscapeUI.Text("Chapter",frame,CityGameplay.HomeMode?"HOME TERRITORY / ARRIVAL":"AWAY OPERATION / ARRIVAL",55,32,1100,43,24,LandscapeUI.Gold,true);
        var title=LandscapeUI.Text("Title",frame,"THE CITY IS YOURS TO EXPLORE",55,723,1450,56,39,null,true);
        var subtitle=LandscapeUI.Text("Subtitle",frame,"Build your crew. Prepare at headquarters. Choose your next move.",55,795,1440,48,24,LandscapeUI.Muted);
        bool skip=false;
        LandscapeUI.Button("Skip",frame,"SKIP INTRO",1360,29,190,50).onClick.AddListener(()=>skip=true);
        bool cameraWasEnabled=controller && controller.enabled;
        var selection=AgentSelectionManager.instance;
        bool selectionWasEnabled=selection && selection.enabled;
        if(controller)controller.enabled=false;if(selection)selection.enabled=false;
        try
        {
            var rotation=Quaternion.Euler(65,45,0);
            var forward=rotation*Vector3.forward;
            for(int shot=0;shot<2 && !skip;shot++)
            {
                Vector3 focus=shot==0?home+new Vector3(20,0,15):home;
                float startHeight=shot==0?130:85,endHeight=shot==0?100:65;
                if(shot==1)
                {
                    title.text=GameManager.Data?.FirmName.ToUpperInvariant()??"YOUR FIRM";
                    subtitle.text="Hold and drag to explore • Tap a destination to move • Camera follows your squad";
                }
                for(float t=0;t<2.5f && !skip;t+=Time.unscaledDeltaTime)
                {
                    float h=Mathf.Lerp(startHeight,endHeight,Mathf.SmoothStep(0,1,t/2.5f));
                    camera.transform.SetPositionAndRotation(focus-forward*(h/-forward.y),rotation);
                    yield return null;
                }
            }
        }
        finally
        {
            Object.Destroy(root);
            if(selection)selection.enabled=selectionWasEnabled;
            if(controller)
            {
                controller.enabled=true;
                controller.ConfigureCity(PlayerPrefs.GetFloat("CityCameraFov",65),PlayerPrefs.GetFloat("CityCameraHeight",65),PlayerPrefs.GetFloat("CityCameraPitch",65));
                controller.CenterOnSelection();
            }
        }
    }
}
