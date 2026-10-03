using UnityEngine;
using UnityEngine.UI;
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MiniMapCameraFootprint : MaskableGraphic
{
    public Camera MapCamera;
    readonly Vector2[] corners=new Vector2[4];
    protected override void Awake(){base.Awake();raycastTarget=false;color=new Color(1,1,1,.85f);}
    void LateUpdate(){SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var main=Camera.main;if(!main||!MapCamera)return;
        var plane=new Plane(Vector3.up,Vector3.zero);var rect=rectTransform.rect;
        for(int i=0;i<4;i++)
        {
            var ray=main.ViewportPointToRay(new Vector3(i==1||i==2?1:0,i>=2?1:0,0));
            if(!plane.Raycast(ray,out var d))return;
            var p=MapCamera.WorldToViewportPoint(ray.GetPoint(d));
            corners[i]=new Vector2(rect.xMin+p.x*rect.width,rect.yMin+p.y*rect.height);
        }
        for(int i=0;i<4;i++)
        {
            var a=corners[i];var b=corners[(i+1)%4];var v=(b-a).normalized;var n=new Vector2(-v.y,v.x);
            int k=vh.currentVertCount;
            vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddVert(a-n,color,Vector2.zero);
            vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
        }
    }
}
