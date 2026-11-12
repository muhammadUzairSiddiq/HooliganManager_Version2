using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Small, colour camera capture behind the teaching cutout; never captures overlay UI.</summary>
[DefaultExecutionOrder(200)]
public sealed class TutorialBackdropBlur : MonoBehaviour
{
    TutorialVeil veil;
    Material blur,soften;
    RenderTexture capture;
    Camera source;
    Matrix4x4 lastView,lastProjection;
    float nextCapture;
    bool ready;
    public bool Ready=>ready&&capture&&capture.IsCreated();
    public int CaptureCount {get;private set;}
    public void Initialize(TutorialVeil graphic)
    {
        veil=graphic;
        var shader=Resources.Load<Shader>("Presentation/TutorialBackdrop");
        var gaussian=Resources.Load<Shader>("Presentation/TutorialGaussian");
        if(!shader||!shader.isSupported||!gaussian||!gaussian.isSupported)return; // A translucent colour veil is still usable.
        blur=new Material(shader){name="Tutorial backdrop (runtime)",hideFlags=HideFlags.DontSave};
        soften=new Material(gaussian){name="Tutorial Gaussian (runtime)",hideFlags=HideFlags.DontSave};
        veil.material=blur;
    }
    void LateUpdate()
    {
        if(!blur||!veil||Time.unscaledTime<nextCapture)return;
        if(!source)source=Camera.main;
        if(!source||!source.isActiveAndEnabled)return;
        int width=Mathf.Min(512,Screen.width),height=Mathf.Max(1,Mathf.RoundToInt(width*(float)Screen.height/Mathf.Max(1,Screen.width)));
        bool resized=!capture||capture.width!=width||capture.height!=height;
        if(resized)
        {
            ReleaseCapture();
            capture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32)
            {name="Tutorial colour backdrop",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
            capture.Create();blur.SetTexture("_BackdropTex",capture);
        }
        // Static paused steps reuse the texture. Live work and camera gestures update at most 8 Hz.
        if(ready&&!resized&&Time.timeScale==0&&lastView==source.worldToCameraMatrix&&lastProjection==source.projectionMatrix)return;
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=capture};
        if(!RenderPipeline.SupportsRenderRequest(source,request))return;
        RenderPipeline.SubmitRenderRequest(source,request);
        var soft=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
        soft.filterMode=FilterMode.Bilinear;soft.wrapMode=TextureWrapMode.Clamp;
        var previousTarget=RenderTexture.active;
        try
        {
            Graphics.Blit(capture,soft,soften,0);
            Graphics.Blit(soft,capture,soften,1);
        }
        finally{RenderTexture.active=previousTarget;RenderTexture.ReleaseTemporary(soft);}
        lastView=source.worldToCameraMatrix;lastProjection=source.projectionMatrix;
        ready=true;CaptureCount++;blur.SetFloat("_Ready",1);nextCapture=Time.unscaledTime+.125f;
    }
    void ReleaseCapture()
    {
        ready=false;if(blur)blur.SetFloat("_Ready",0);
        if(!capture)return;capture.Release();Destroy(capture);capture=null;
    }
    void OnDisable()=>ReleaseCapture();
    void OnDestroy()
    {
        ReleaseCapture();if(veil)veil.material=null;if(blur)Destroy(blur);if(soften)Destroy(soften);
    }
}
