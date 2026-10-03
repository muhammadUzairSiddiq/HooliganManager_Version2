using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Bounded effects; corpses bake the final pose without retaining agents or animators.</summary>
public sealed class StreetCombatPresentation : MonoBehaviour
{
    static StreetCombatPresentation instance;
    readonly Queue<GameObject> bodies = new Queue<GameObject>();
    readonly Queue<GameObject> stains = new Queue<GameObject>();
    Material blood;
    Mesh splat;
    static StreetCombatPresentation Ensure()
    {
        if (!instance) instance = new GameObject("Street combat effects").AddComponent<StreetCombatPresentation>();
        return instance;
    }
    public static void Hit(Vector3 point)
    {
        var self = Ensure();
        if (!self.blood)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit"); if (!shader) return;
            self.blood = new Material(shader); self.blood.color = new Color(.30f,.015f,.025f);
            self.splat = new Mesh { name = "Blood splat" };
            var vertices = new Vector3[13]; var triangles = new int[36];
            for (int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6, r=(i%3==0?.62f:.4f);
                vertices[i+1]=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%12+1;triangles[i*3+2]=i+1;
            }
            self.splat.vertices=vertices;self.splat.triangles=triangles;self.splat.RecalculateNormals();
        }
        GameObject stain;
        if (self.stains.Count >= 32) stain = self.stains.Dequeue();
        else
        {
            stain = new GameObject("Blood",typeof(MeshFilter),typeof(MeshRenderer)); stain.transform.SetParent(self.transform);
            stain.GetComponent<MeshFilter>().sharedMesh=self.splat;
            var renderer=stain.GetComponent<MeshRenderer>();renderer.sharedMaterial=self.blood;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        stain.transform.position=point+Vector3.up*.06f;stain.transform.rotation=Quaternion.Euler(0,Random.Range(0,360),0);
        self.stains.Enqueue(stain);
    }
    public static void LeaveBody(Transform source)
    {
        var self = Ensure();
        while (self.bodies.Count >= 16) { var oldest=self.bodies.Dequeue(); if(oldest)Destroy(oldest); }
        var body=new GameObject("Fallen supporter");body.transform.SetParent(self.transform);
        foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.enabled || !skin.sharedMesh || skin.name.Contains("LOD1")) continue;
            var go=new GameObject("Resting pose",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(body.transform);
            go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            // Freeze world-space vertices once; FBX/root scale is not applied twice.
            go.transform.localScale=Vector3.one;
            // Skin once in world space from bind poses. BakeMesh(false) on the
            // imported Humanoid retains scale that TransformPoint applied again.
            var mesh=Instantiate(skin.sharedMesh);
            var vertices=mesh.vertices;var normals=mesh.normals;var weights=mesh.boneWeights;
            var binds=mesh.bindposes;var bones=skin.bones;var matrices=new Matrix4x4[bones.Length];
            for(int b=0;b<bones.Length;b++)matrices[b]=bones[b].localToWorldMatrix*binds[b];
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var v=vertices[i];var n=normals[i];
                float total=Mathf.Max(.0001f,w.weight0+w.weight1);
                vertices[i]=(matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1)/total;
                normals[i]=(matrices[w.boneIndex0].MultiplyVector(n)*w.weight0+matrices[w.boneIndex1].MultiplyVector(n)*w.weight1).normalized;
            }
            mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
            for(int slot=0;slot<skin.sharedMaterials.Length;slot++)
            {
                block.Clear();skin.GetPropertyBlock(block,slot);
                if(!block.isEmpty)renderer.SetPropertyBlock(block,slot);
            }
        }
        // A fallen pose must rest on the street, even if its clip ends above
        // the floor or navigation used a capsule/base offset.
        float lowest=float.PositiveInfinity;
        foreach(var renderer in body.GetComponentsInChildren<MeshRenderer>())lowest=Mathf.Min(lowest,renderer.bounds.min.y);
        float ground=source.position.y;
        if(UnityEngine.AI.NavMesh.SamplePosition(source.position,out var street,4f,UnityEngine.AI.NavMesh.AllAreas))ground=street.position.y;
        if(!float.IsInfinity(lowest))body.transform.position+=Vector3.up*(ground+.035f-lowest);
        body.AddComponent<BakedBodyLifetime>();self.bodies.Enqueue(body);Destroy(body,45f);
    }
    void OnDestroy(){if(instance==this)instance=null;if(blood)Destroy(blood);if(splat)Destroy(splat);}
}
public sealed class BakedBodyLifetime : MonoBehaviour
{
    void OnDestroy(){foreach(var filter in GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh)Destroy(filter.sharedMesh);}
}
