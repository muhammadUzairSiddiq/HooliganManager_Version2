#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ClothingTopologyAudit
{
    public sealed class Island
    {
        public Bounds bounds;
        public readonly List<int> indices=new List<int>();
        public int submesh;
    }
    public static List<Island> Islands(Mesh mesh)
    {
        var vertices=mesh.vertices;var parents=Enumerable.Range(0,vertices.Length).ToArray();
        int Root(int v){while(parents[v]!=v){parents[v]=parents[parents[v]];v=parents[v];}return v;}
        void Union(int a,int b){a=Root(a);b=Root(b);if(a!=b)parents[b]=a;}
        var weld=new Dictionary<Vector3Int,int>();
        for(int i=0;i<vertices.Length;i++)
        {
            var p=vertices[i]*100000f;var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
            if(weld.TryGetValue(key,out int other))Union(i,other);else weld[key]=i;
        }
        for(int s=0;s<mesh.subMeshCount;s++)
        {
            var t=mesh.GetTriangles(s);for(int i=0;i<t.Length;i+=3){Union(t[i],t[i+1]);Union(t[i],t[i+2]);}
        }
        var islands=new Dictionary<(int,int),Island>();
        for(int s=0;s<mesh.subMeshCount;s++)
        {
            var t=mesh.GetTriangles(s);
            for(int i=0;i<t.Length;i+=3)
            {
                var key=(Root(t[i]),s);
                if(!islands.TryGetValue(key,out var island))islands[key]=island=new Island{bounds=new Bounds(vertices[t[i]],Vector3.zero),submesh=s};
                for(int j=0;j<3;j++){island.indices.Add(t[i+j]);island.bounds.Encapsulate(vertices[t[i+j]]);}
            }
        }
        return islands.Values.OrderByDescending(i=>i.indices.Count).ToList();
    }
    public static void Run()
    {
        var rows=new List<string>();
        foreach(string id in new[]{"Ch06","Ch12","Ch18"})
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Character/{id}/{id}_nonPBR.fbx");
            foreach(var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                rows.Add("MODEL "+renderer.name);
                foreach(var island in Islands(renderer.sharedMesh).Where(i=>i.indices.Count>150).Take(30))rows.Add($"SUB {island.submesh} TRI {island.indices.Count/3} MIN {island.bounds.min} MAX {island.bounds.max}");
            }
        }
        Directory.CreateDirectory("Artifacts/CityQA");File.WriteAllLines("Artifacts/CityQA/clothing-topology.txt",rows);
    }
}
#endif
