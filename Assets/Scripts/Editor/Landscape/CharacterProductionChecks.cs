#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CharacterProductionChecks
{
    public static void Run()
    {
        var rows=new List<string>();
        void Check(bool ok,string text)=>rows.Add((ok?"PASS ":"FAIL ")+text);
        var tested=new HashSet<GameObject>();
        foreach(var guid in AssetDatabase.FindAssets("t:CharacterPortraitRegistry"))
        {
            var registry=AssetDatabase.LoadAssetAtPath<CharacterPortraitRegistry>(AssetDatabase.GUIDToAssetPath(guid));
            foreach(var entry in registry.entries)
            {
                var model=entry.optimizedModelPrefab;
                Check(model&&entry.modelPrefab==model,"Registry references optimized asset without loading high-poly source");
                if(!model||!tested.Add(model))continue;
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(entry.sourceModelAssetPath);
                Check(source,"Original source retained: "+model.name);
                var group=model.GetComponent<LODGroup>();
                Check(group&&group.lodCount==2,"Two LOD tiers: "+model.name);
                if(!group)continue;
                var lods=group.GetLODs();
                int original=source.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(r=>r.sharedMesh.triangles.Length/3);
                int near=lods[0].renderers.OfType<SkinnedMeshRenderer>().Sum(r=>r.sharedMesh.triangles.Length/3);
                int far=lods[1].renderers.OfType<SkinnedMeshRenderer>().Sum(r=>r.sharedMesh.triangles.Length/3);
                Check(near<original*.5f&&far<near,$"Mesh reduction {model.name}: {original} -> {near} / {far}");
                foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=renderer.sharedMesh;
                    Check(mesh&&mesh.boneWeights.Length==mesh.vertexCount&&mesh.bindposes.Length==renderer.bones.Length,"Skinning preserved: "+renderer.name);
                    if(CrewKit.IsShirtRenderer(renderer.name))Check(renderer.sharedMaterials.All(m=>m&&!m.mainTexture),"Clothing has plain texture-free material: "+renderer.name);
                    else Check(renderer.sharedMaterials.All(m=>m&&m.name!="GangShirt"),"Non-clothing materials preserved: "+renderer.name);
                }
            }
        }
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Models/Character/animations/CharacterAnimatorController.controller");
        foreach(string name in new[]{"Task Chant","Task Watch","Task Treat","Task Pickup","Task Talk"})
        {
            var state=controller.layers[0].stateMachine.states.Select(x=>x.state).FirstOrDefault(x=>x.name==name);
            var clip=state?state.motion as AnimationClip:null;
            Check(clip&&clip.length>=1.9f&&AnimationUtility.GetCurveBindings(clip).Length>100,"Baked controller clip: "+name);
            if(!clip)continue;
            foreach(var model in tested)
            {
                var missing=AnimationUtility.GetCurveBindings(clip).Where(b=>!model.transform.Find(b.path)).Select(b=>b.path).Distinct().ToArray();
                Check(missing.Length==0,"Clip paths bind on "+model.name+": "+name+" "+string.Join(", ",missing));
            }
        }
        rows.Add($"RESULT {rows.Count(x=>x.StartsWith("PASS"))} passed, {rows.Count(x=>x.StartsWith("FAIL"))} failed");
        Directory.CreateDirectory("Artifacts/CityQA");File.WriteAllLines("Artifacts/CityQA/character-production-checks.txt",rows);
        Debug.Log(rows[rows.Count-1]);
    }
}
#endif
