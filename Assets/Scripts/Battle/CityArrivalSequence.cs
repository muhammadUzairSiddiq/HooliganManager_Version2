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
        LandscapeUI.ConfigureLandscapeScaler(root.GetComponent<CanvasScaler>());
        var frame=LandscapeUI.Rect("Frame",root.transform,0,0,1600,900);
        frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,.5f);frame.anchoredPosition=Vector2.zero;
        LandscapeUI.Image("InputShield",frame,0,0,1600,900,null,Color.clear).raycastTarget=true;
        bool skip=false;
        LandscapeUI.Button("Skip",frame,"SKIP",1360,29,120,44).onClick.AddListener(()=>skip=true);
        bool cameraWasEnabled=controller && controller.enabled;
        var selection=AgentSelectionManager.instance;
        bool selectionWasEnabled=selection && selection.enabled;
        if(controller)controller.enabled=false;if(selection)selection.enabled=false;
        try
        {
            float pitch=PlayerPrefs.GetFloat("CityCameraPitch",GameplayTuning.Current.cameraPitch);
            float yaw=PlayerPrefs.GetFloat("CityCameraYaw",45);
            var rotation=Quaternion.Euler(pitch,yaw,0);
            var forward=rotation*Vector3.forward;
            for(int shot=0;shot<2 && !skip;shot++)
            {
                Vector3 focus=home;
                float startHeight=shot==0?90:76,endHeight=shot==0?76:65;
                for(float t=0;t<2.5f && !skip;t+=Time.unscaledDeltaTime)
                {
                    float h=Mathf.Lerp(startHeight,endHeight,Mathf.SmoothStep(0,1,t/2.5f));
                    Vector3 pos=focus-forward*(h/-forward.y);
                    pos=CameraPanTouchOnly.SafeCityPosition(focus,pos);
                    camera.transform.SetPositionAndRotation(pos,rotation);
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
                controller.ConfigureCity(PlayerPrefs.GetFloat("CityCameraFov",GameplayTuning.Current.cameraFov),PlayerPrefs.GetFloat("CityCameraHeight",GameplayTuning.Current.explorationHeight),PlayerPrefs.GetFloat("CityCameraPitch",GameplayTuning.Current.cameraPitch),PlayerPrefs.GetFloat("CityCameraYaw",45));
                controller.CenterOnSelection();
            }
        }
    }
}
