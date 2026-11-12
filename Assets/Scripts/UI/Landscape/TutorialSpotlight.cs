using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public enum TutorialGesture { Tap, DoubleTap, Pan, PinchOut, PinchIn }

/// <summary>Live-gameplay shield with rectangular UI highlights, world cutouts and safe-area teaching controls.</summary>
public sealed class TutorialSpotlight : MonoBehaviour
{
    RectTransform frame,hand,otherHand,ripple;
    TutorialVeil veil;
    TutorialGestureSurface hit;
    Button previous,next,skip;
    TextMeshProUGUI title,copy,progress;
    Rect target;
    float pinchDistance;
    int lesson=-1;
    bool navigationPlaced,navigationAtTop;
    public TutorialGesture Gesture {get;private set;}
    public Action Accepted,Skipped,Previous,Next;
    public Action<Vector2,Vector2> Panned;
    public Action<float> Zoomed;
    public Rect TargetScreenRect {get;private set;}
    public Rect CircleScreenRect {get;private set;}
    public bool Waiting {get;private set;}
    public static TutorialSpotlight Create()
    {
        var host=new GameObject("FirstRunTutorialCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31000;
        LandscapeUI.ConfigureLandscapeScaler(host.GetComponent<CanvasScaler>());
        var self=host.AddComponent<TutorialSpotlight>();
        var block=LandscapeUI.Rect("InputShield",host.transform,0,0,1600,900);LandscapeUI.Stretch(block);
        self.veil=block.gameObject.AddComponent<TutorialVeil>();self.veil.raycastTarget=true;
        self.veil.color=new Color(0,0,0,.58f);
        var safe=LandscapeUI.Rect("Safe",host.transform,0,0,1600,900);LandscapeUI.Stretch(safe);
        self.frame=LandscapeUI.Rect("Frame",safe,0,0,1600,900);
        var viewport=safe.gameObject.AddComponent<LandscapeViewport>();viewport.frame=self.frame;viewport.Fit();
        self.hit=LandscapeUI.Rect("OnlyAllowedTarget",self.frame,0,0,1,1).gameObject.AddComponent<TutorialGestureSurface>();
        self.hit.owner=self;self.hit.color=new Color(1,1,1,.001f);
        self.hit.gameObject.SetActive(false);
        self.ripple=LandscapeUI.Rect("TapRipple",self.frame,0,0,1,1);
        self.ripple.gameObject.AddComponent<TutorialVeil>().RingOnly=true;
        self.ripple.GetComponent<TutorialVeil>().raycastTarget=false;
        self.hand=MakeHand(self.frame,"AnimatedTapHand");self.otherHand=MakeHand(self.frame,"SecondPinchHand");
        self.hand.gameObject.SetActive(false);self.otherHand.gameObject.SetActive(false);self.ripple.gameObject.SetActive(false);
        self.title=LandscapeUI.Text("Title",self.frame,"",200,24,1200,48,36,LandscapeUI.White,true,TextAlignmentOptions.Center);
        self.copy=LandscapeUI.Text("OneAction",self.frame,"",200,78,1200,38,25,LandscapeUI.White,false,TextAlignmentOptions.Center);
        self.progress=LandscapeUI.Text("Progress",self.frame,"",600,120,400,24,16,LandscapeUI.Gold,true,TextAlignmentOptions.Center);
        self.previous=LandscapeUI.Button("Previous",self.frame,"BACK",28,800,190,64);
        self.previous.onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();self.Previous?.Invoke();});
        self.skip=LandscapeUI.Button("SkipPart",self.frame,"SKIP PART",650,800,300,64);
        self.skip.onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();self.Skipped?.Invoke();});
        self.next=LandscapeUI.Button("Continue",self.frame,"NEXT",1382,800,190,64);
        self.next.onClick.AddListener(()=>{AgentSelectionManager.ConsumeUiPointer();self.Next?.Invoke();});
        ColourButton(self.previous,new Color(.10f,.40f,.72f));
        ColourButton(self.skip,new Color(.88f,.49f,.10f));
        ColourButton(self.next,new Color(.08f,.58f,.36f));
        return self;
    }
    static void ColourButton(Button button,Color colour)
    {
        // Retain the game's metal rim, with a readable coloured centre and feedback.
        var fill=LandscapeUI.Image("Colour",button.transform,10,9,1,1,null,colour);
        var rt=fill.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;
        rt.offsetMin=new Vector2(10,9);rt.offsetMax=new Vector2(-10,-9);
        rt.SetAsFirstSibling();button.targetGraphic=fill;
        var colours=button.colors;colours.disabledColor=new Color(.45f,.52f,.60f,.65f);button.colors=colours;
    }
    static RectTransform MakeHand(Transform parent,string name)
    {
        var rt=LandscapeUI.Rect(name,parent,0,0,68,92);rt.gameObject.AddComponent<TutorialHand>().raycastTarget=false;
        var outline=rt.gameObject.AddComponent<Outline>();outline.effectColor=new Color(0,0,0,.85f);outline.effectDistance=new Vector2(2,-2);return rt;
    }
    public void SetLesson(int step,TutorialGesture gesture)
    {
        if(lesson==step&&Gesture==gesture)return;
        lesson=step;Gesture=gesture;pinchDistance=0;hit.ResetGesture();navigationPlaced=false;
        hit.gameObject.SetActive(false);hand.gameObject.SetActive(false);otherHand.gameObject.SetActive(false);ripple.gameObject.SetActive(false);TargetScreenRect=default;
    }
    public void Present(string part,string heading,string text,Rect screenRect,bool waiting,bool hasTarget,string caption="NEXT",bool rectangular=false,bool passThrough=false,bool canContinue=true)
    {
        Waiting=waiting;title.text=heading;copy.text=text;progress.text=part;
        previous.interactable=lesson>0&&(!waiting||canContinue);
        next.interactable=canContinue;
        next.GetComponentInChildren<TextMeshProUGUI>().text=!hasTarget&&!waiting?caption:"NEXT";
        var a=ToFrame(screenRect.min);var b=ToFrame(screenRect.max);
        target=Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        bool visible=hasTarget&&screenRect.width>1;
        // Keep navigation still within a lesson and clear of low HUD targets.
        if(!navigationPlaced&&visible){navigationAtTop=target.yMin<160;navigationPlaced=true;}
        float navY=navigationPlaced&&navigationAtTop?176:frame.rect.height-190;
        LandscapeUI.Place((RectTransform)previous.transform,28,navY,190,64);
        LandscapeUI.Place((RectTransform)skip.transform,(frame.rect.width-260)*.5f,navY,260,64);
        LandscapeUI.Place((RectTransform)next.transform,frame.rect.width-218,navY,190,64);
        TargetScreenRect=visible&&!waiting?screenRect:default;
        hit.gameObject.SetActive(visible&&!waiting&&!passThrough);hand.gameObject.SetActive(visible&&!waiting);
        veil.PassThroughScreenRect=visible&&!waiting&&passThrough?screenRect:default;
        otherHand.gameObject.SetActive(visible&&!waiting&&(Gesture==TutorialGesture.PinchIn||Gesture==TutorialGesture.PinchOut));
        ripple.gameObject.SetActive(visible&&!waiting);
        if(visible)
        {
            // The exclusive proxy gives short existing buttons a comfortable touch area.
            float width=Mathf.Max(64,target.width),height=Mathf.Max(64,target.height);
            LandscapeUI.Place(hit.rectTransform,target.center.x-width*.5f,frame.rect.height-target.center.y-height*.5f,width,height);
        }
        var vr=veil.rectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(vr,screenRect.center,null,out var center);
        Vector2 size=screenRect.size/Mathf.Max(.01f,vr.lossyScale.x);
        float radius=Mathf.Min(Mathf.Min(vr.rect.width,vr.rect.height)*.45f,Mathf.Max(64,Mathf.Sqrt(size.x*size.x+size.y*size.y)*.5f+12));
        center.x=Mathf.Clamp(center.x,vr.rect.xMin+radius+4,vr.rect.xMax-radius-4);
        center.y=Mathf.Clamp(center.y,vr.rect.yMin+radius+4,vr.rect.yMax-radius-4);
        veil.Rectangular=rectangular;
        if(rectangular)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(vr,screenRect.min,null,out var lo);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(vr,screenRect.max,null,out var hi);
            veil.Hole=visible?Rect.MinMaxRect(lo.x-6,lo.y-6,hi.x+6,hi.y+6):default;
        }
        else veil.Hole=visible?new Rect(center-Vector2.one*radius,Vector2.one*radius*2):default;
        veil.color=new Color(0,0,0,waiting?.18f:.58f);veil.SetVerticesDirty();
        var circleCenter=RectTransformUtility.WorldToScreenPoint(null,vr.TransformPoint(center));
        CircleScreenRect=visible?new Rect(circleCenter-Vector2.one*radius*vr.lossyScale.x,Vector2.one*radius*2*vr.lossyScale.x):default;
        float headingWidth=Mathf.Min(1100,frame.rect.width-80),headingX=(frame.rect.width-headingWidth)*.5f;
        if(visible&&target.yMax>frame.rect.height-180)
        {
            headingWidth=Mathf.Min(660,frame.rect.width*.45f);
            headingX=target.center.x<frame.rect.width*.5f?frame.rect.width-headingWidth-30:30;
        }
        LandscapeUI.Place(title.rectTransform,headingX,24,headingWidth,48);
        LandscapeUI.Place(copy.rectTransform,headingX,78,headingWidth,38);
        LandscapeUI.Place(progress.rectTransform,headingX,120,headingWidth,24);
    }
    Vector2 ToFrame(Vector2 point){RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,point,null,out var local);return local-frame.rect.min;}
    public void ClickTarget(){if(!Waiting)Accepted?.Invoke();}
    public void BlockInteraction()
    {veil.PassThroughScreenRect=default;hit.gameObject.SetActive(false);TargetScreenRect=default;}
    public void PanTarget(Vector2 previousPosition,Vector2 position)
    {if(!Waiting&&Gesture==TutorialGesture.Pan)Panned?.Invoke(previousPosition,position);}
    public void ZoomTarget(float amount)
    {
        if(Waiting||!hit.gameObject.activeInHierarchy)return;
        if(Gesture==TutorialGesture.PinchOut&&amount>0||Gesture==TutorialGesture.PinchIn&&amount<0)Zoomed?.Invoke(amount);
    }
    void Update()
    {
        if(!hand||!hand.gameObject.activeSelf)return;
        var touches=Touch.activeTouches;
        if(touches.Count==2&&(Gesture==TutorialGesture.PinchIn||Gesture==TutorialGesture.PinchOut))
        {
            var p=touches[0].screenPosition;var q=touches[1].screenPosition;
            if(TargetScreenRect.Contains(p)&&TargetScreenRect.Contains(q))
            {float distance=Vector2.Distance(p,q);if(pinchDistance>0)ZoomTarget((distance-pinchDistance)*.05f);pinchDistance=distance;}
            else pinchDistance=0;
        }
        else pinchDistance=0;
        float phase=GameFeedback.ReducedMotion?.5f:Mathf.Repeat(Time.unscaledTime*.85f,1);
        float pulse=GameFeedback.ReducedMotion?0:Mathf.Sin(phase*Mathf.PI*2)*6;
        Vector2 focus=target.center;
        if(Gesture==TutorialGesture.Pan)focus.x+=Mathf.Lerp(-65,65,phase);
        if(Gesture==TutorialGesture.PinchIn||Gesture==TutorialGesture.PinchOut)
        {
            float gap=Mathf.Lerp(30,110,Gesture==TutorialGesture.PinchOut?phase:1-phase);
            PlaceHand(otherHand,focus+new Vector2(-gap,0),pulse,18);focus.x+=gap;
        }
        PlaceHand(hand,focus,pulse,-18);
        float rippleSize=GameFeedback.ReducedMotion?32:Mathf.Lerp(22,60,Gesture==TutorialGesture.DoubleTap?Mathf.Repeat(phase*2,1):phase);
        LandscapeUI.Place(ripple,focus.x-rippleSize*.5f,frame.rect.height-focus.y-rippleSize*.5f,rippleSize,rippleSize);
        var ring=ripple.GetComponent<TutorialVeil>();ring.Hole=ring.rectTransform.rect;ring.color=LandscapeUI.Gold;ring.SetVerticesDirty();
    }
    void PlaceHand(RectTransform rt,Vector2 fingertip,float bob,float rotation)
    {
        // Rotate around the actual index fingertip. Low targets use a hand from
        // above, keeping the cue on its button instead of clamping it elsewhere.
        bool fromAbove=fingertip.y<130;
        rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(.34f,1);
        rt.sizeDelta=new Vector2(68,92);rt.localScale=Vector3.one;
        rt.anchoredPosition=new Vector2(fingertip.x,fingertip.y-frame.rect.height+(fromAbove?bob:-bob));
        rt.localRotation=Quaternion.Euler(0,0,rotation+(fromAbove?180:0));
    }
}

// Without this dependency, GraphicRaycaster throws before any control receives input.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialVeil : MaskableGraphic,ICanvasRaycastFilter
{
    public Rect Hole;
    public bool RingOnly,Rectangular;
    public Rect PassThroughScreenRect;
    public bool IsRaycastLocationValid(Vector2 screenPoint,Camera eventCamera)=>!PassThroughScreenRect.Contains(screenPoint);
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        if(Hole.width<=0||Hole.height<=0){if(!RingOnly)Quad(vh,r,color);return;}
        if(Rectangular)
        {
            var h=Rect.MinMaxRect(Mathf.Max(r.xMin,Hole.xMin),Mathf.Max(r.yMin,Hole.yMin),Mathf.Min(r.xMax,Hole.xMax),Mathf.Min(r.yMax,Hole.yMax));
            Quad(vh,Rect.MinMaxRect(r.xMin,r.yMin,r.xMax,h.yMin),color);
            Quad(vh,Rect.MinMaxRect(r.xMin,h.yMax,r.xMax,r.yMax),color);
            Quad(vh,Rect.MinMaxRect(r.xMin,h.yMin,h.xMin,h.yMax),color);
            Quad(vh,Rect.MinMaxRect(h.xMax,h.yMin,r.xMax,h.yMax),color);
            var gold=new Color(1,.76f,.32f,1);
            Quad(vh,new Rect(h.xMin,h.yMin,h.width,3),gold);Quad(vh,new Rect(h.xMin,h.yMax-3,h.width,3),gold);
            Quad(vh,new Rect(h.xMin,h.yMin,3,h.height),gold);Quad(vh,new Rect(h.xMax-3,h.yMin,3,h.height),gold);return;
        }
        Vector2 c=Hole.center;float radius=Hole.width*.5f;
        if(!RingOnly)
        {
            float bottom=Mathf.Clamp(c.y-radius,r.yMin,r.yMax),top=Mathf.Clamp(c.y+radius,r.yMin,r.yMax);
            Quad(vh,Rect.MinMaxRect(r.xMin,r.yMin,r.xMax,bottom),color);
            Quad(vh,Rect.MinMaxRect(r.xMin,top,r.xMax,r.yMax),color);
            const int strips=128;
            for(int i=0;i<strips;i++)
            {
                float y0=Mathf.Lerp(bottom,top,i/(float)strips),y1=Mathf.Lerp(bottom,top,(i+1f)/strips);
                float x0=Mathf.Sqrt(Mathf.Max(0,radius*radius-(y0-c.y)*(y0-c.y)));
                float x1=Mathf.Sqrt(Mathf.Max(0,radius*radius-(y1-c.y)*(y1-c.y)));
                Polygon(vh,new Vector2(r.xMin,y0),new Vector2(r.xMin,y1),new Vector2(Mathf.Clamp(c.x-x1,r.xMin,r.xMax),y1),new Vector2(Mathf.Clamp(c.x-x0,r.xMin,r.xMax),y0),color);
                Polygon(vh,new Vector2(Mathf.Clamp(c.x+x0,r.xMin,r.xMax),y0),new Vector2(Mathf.Clamp(c.x+x1,r.xMin,r.xMax),y1),new Vector2(r.xMax,y1),new Vector2(r.xMax,y0),color);
            }
        }
        Ring(vh,c,radius,RingOnly?2.5f:3,new Color(1,.76f,.32f,RingOnly?.65f:1));
    }
    static void Ring(VertexHelper vh,Vector2 center,float radius,float width,Color tint)
    {
        for(int i=0;i<128;i++)
        {
            float a=i*Mathf.PI*2/128,b=(i+1)*Mathf.PI*2/128;
            var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
            Polygon(vh,center+p*(radius-width),center+q*(radius-width),center+q*radius,center+p*radius,tint);
        }
    }
    public static void Quad(VertexHelper vh,Rect r,Color c)
    {if(r.width>0&&r.height>0)Polygon(vh,new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin),c);}
    static void Polygon(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
    {int i=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddVert(d,tint,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialGestureSurface : Image,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IScrollHandler
{
    public TutorialSpotlight owner;
    float lastTap=-10;
    bool dragged;
    public void ResetGesture(){lastTap=-10;dragged=false;}
    public void OnPointerClick(PointerEventData e)
    {
        if(dragged)return;
        if(owner.Gesture==TutorialGesture.Tap)owner.ClickTarget();
        else if(owner.Gesture==TutorialGesture.DoubleTap)
        {if(Time.unscaledTime-lastTap<=.35f){lastTap=-10;owner.ClickTarget();}else lastTap=Time.unscaledTime;}
    }
    public void OnBeginDrag(PointerEventData e){dragged=true;}
    public void OnDrag(PointerEventData e)=>owner.PanTarget(e.position-e.delta,e.position);
    public void OnEndDrag(PointerEventData e){}
    public void OnScroll(PointerEventData e)=>owner.ZoomTarget(e.scrollDelta.y*.08f);
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialHand : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        Vector2[] p={new Vector2(.25f,.08f),new Vector2(.74f,.08f),new Vector2(.85f,.38f),new Vector2(.86f,.57f),new Vector2(.76f,.62f),new Vector2(.69f,.57f),new Vector2(.59f,.66f),new Vector2(.49f,.59f),new Vector2(.43f,.94f),new Vector2(.34f,1),new Vector2(.25f,.94f),new Vector2(.28f,.48f),new Vector2(.12f,.60f),new Vector2(.03f,.54f),new Vector2(.08f,.43f),new Vector2(.25f,.27f)};
        vh.AddVert(new Vector3(r.xMin+r.width*.47f,r.yMin+r.height*.40f),Color.white,Vector2.zero);
        foreach(var v in p)vh.AddVert(new Vector3(r.xMin+v.x*r.width,r.yMin+v.y*r.height),Color.white,Vector2.zero);
        for(int i=0;i<p.Length;i++)vh.AddTriangle(0,i+1,(i+1)%p.Length+1);
    }
}
