using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Flat rails replace the viaduct without retaining bridge decks or columns.</summary>
public sealed class GroundRailTrack : MonoBehaviour
{
    Mesh mesh;
    Material material;
    public static void Build(IEnumerable<Bounds> segments)
    {
        var source=GameObject.CreatePrimitive(PrimitiveType.Cube);var cube=source.GetComponent<MeshFilter>().sharedMesh;
        var boxes=new List<CombineInstance>();
        foreach(var segment in segments)
        {
            bool alongX=segment.size.x>segment.size.z;float length=alongX?segment.size.x:segment.size.z;
            if(length<12||length>180)continue;
            Vector3 center=new Vector3(segment.center.x,.10f,segment.center.z);
            foreach(float offset in new[]{-3f,-1.5f,1.5f,3f})
                Add(center+(alongX?Vector3.forward:Vector3.right)*offset,alongX?new Vector3(length,.12f,.12f):new Vector3(.12f,.12f,length));
            for(float distance=-length*.5f;distance<length*.5f;distance+=2.5f)
                Add(center+(alongX?Vector3.right:Vector3.forward)*distance-Vector3.up*.035f,alongX?new Vector3(.25f,.08f,7.5f):new Vector3(7.5f,.08f,.25f));
        }
        Destroy(source);if(boxes.Count==0)return;
        var root=new GameObject("Ground railway",typeof(MeshFilter),typeof(MeshRenderer));var owner=root.AddComponent<GroundRailTrack>();
        owner.mesh=new Mesh{name="Combined ground rails",indexFormat=IndexFormat.UInt32};owner.mesh.CombineMeshes(boxes.ToArray(),true,true);
        root.GetComponent<MeshFilter>().sharedMesh=owner.mesh;
        owner.material=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){color=new Color(.24f,.25f,.27f)};
        var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=owner.material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        void Add(Vector3 position,Vector3 size)=>boxes.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(position,Quaternion.identity,size)});
    }
    void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
}
