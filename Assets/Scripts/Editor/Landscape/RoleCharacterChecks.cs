#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class RoleCharacterChecks
{
    public static void Retire()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Retire assets in edit mode.");
        Run();
        if(File.ReadAllLines("Artifacts/CityQA/retired-character-dependencies.txt").Length!=0)throw new InvalidOperationException("Retired models still have build dependencies; see audit.");
        var paths=Directory.GetDirectories("Assets/Models/Character").Select(p=>p.Replace('\\','/')).Where(p=>Path.GetFileName(p).StartsWith("Ch")||Path.GetFileName(p)=="Swat").ToList();
        paths.AddRange(new[]{"Assets/Generated/CharacterProduction/Meshes","Assets/Generated/CharacterProduction/Prefabs","Assets/Generated/Floreswa"});
        paths.AddRange(Directory.GetFiles("Assets/Resources/FloreswaCrowd","male*.asset").Select(p=>p.Replace('\\','/')));
        string workspace=Path.GetFullPath("Assets")+Path.DirectorySeparatorChar;
        foreach(var path in paths)
        {
            if(!Path.GetFullPath(path).StartsWith(workspace,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Asset retirement escaped Assets.");
            if(!AssetDatabase.DeleteAsset(path))throw new InvalidOperationException("Could not retire "+path);
        }
        File.WriteAllLines("Artifacts/CityQA/retired-character-assets.txt",paths);
        AssetDatabase.SaveAssets();
    }
    public static void Run()
    {
        var rows=new List<string>();void Check(bool ok,string text)=>rows.Add((ok?"PASS ":"FAIL ")+text);
        var roles=RoleCharacterModels.Current;
        foreach(var registry in new[]{roles.civilians,roles.gangs,roles.police})
        {
            var entry=registry.entries[0];var instance=Object.Instantiate(entry.modelPrefab);
            try
            {
                var animator=instance.GetComponent<Animator>();Check(animator&&animator.avatar&&animator.avatar.isHuman&&animator.avatar.isValid,"Valid humanoid: "+registry.name);
                Check(entry.portrait,"Updated portrait: "+registry.name);
                var controller=entry.animatorController as UnityEditor.Animations.AnimatorController;
                foreach(string state in new[]{"Idle","Walking","Running","Head Hit","Hit To Body","Rallying","Cheering","Clapping","Right Punching","Falling Back Death From Right Punch"})
                {
                    var clip=controller.layers[0].stateMachine.states.First(s=>s.state.name==state).state.motion as AnimationClip;
                    Check(clip&&clip.humanMotion,"Playable humanoid motion "+registry.name+" / "+state);
                    var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();
                    clip.SampleAnimation(instance,.05f);skin.BakeMesh(mesh,false);var first=mesh.vertices;
                    clip.SampleAnimation(instance,Mathf.Min(clip.length*.55f,.65f));skin.BakeMesh(mesh,false);var second=mesh.vertices;
                    Check(first.Where((v,i)=>(v-second[i]).sqrMagnitude>.00001f).Any(),"Deforming animation "+registry.name+" / "+state);
                    Object.DestroyImmediate(mesh);
                }
                int triangles=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(s=>s.sharedMesh.triangles.Length/3);
                Check(triangles<2500,"Authored low-poly mesh "+registry.name+" / "+triangles+" triangles");
                Check(instance.GetComponentsInChildren<SkinnedMeshRenderer>().All(s=>s.sharedMesh.subMeshCount==1&&s.sharedMaterials.Length==1),"Single material draw per skinned renderer: "+registry.name);
                Check(controller.layers[0].stateMachine.states.Where(s=>new[]{"Walking","Running","Unarmed Run Forward"}.Contains(s.state.name)).All(s=>AssetDatabase.GetAssetPath(s.state.motion).Contains("Dwarf Walk")),"All movement uses the requested Dwarf Walk: "+registry.name);
                Check(AssetDatabase.GetAssetPath(controller.layers[0].stateMachine.states.First(s=>s.state.name=="Idle").state.motion).Contains("Bouncing Fight Idle"),"Idle uses supplied fight idle: "+registry.name);
            }
            finally{Object.DestroyImmediate(instance);}
        }
        var scenePaths=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path);
        var resources=AssetDatabase.GetAllAssetPaths().Where(p=>p.Contains("/Resources/")&&!p.EndsWith(".cs")&&!AssetDatabase.IsValidFolder(p));
        var dependencies=AssetDatabase.GetDependencies(scenePaths.Concat(resources).ToArray(),true);
        var old=dependencies.Where(p=>p.StartsWith("Assets/Models/Character/Ch")||p.StartsWith("Assets/Models/Character/Swat")||p.StartsWith("Assets/Generated/CharacterProduction/Prefabs")||p.StartsWith("Assets/Generated/CharacterProduction/Meshes")||p.StartsWith("Assets/Generated/Floreswa/")).ToArray();
        File.WriteAllLines("Artifacts/CityQA/retired-character-dependencies.txt",old);
        Check(old.Length==0,"Build scene/resources have no dependency on retired character models");
        var crowd=Resources.Load<CrowdPoseLibrary>("FloreswaCrowd/Poses");
        Check(crowd&&crowd.variants.Length==10&&crowd.variants.All(v=>v.poses.Length==16),"Ten supporter actions each have sixteen baked poses");
        Check(crowd&&crowd.variants.SelectMany(v=>v.poses).All(m=>m&&m.vertexCount<1000),"Every spectator pose stays below 1000 vertices");
        rows.Add($"RESULT {rows.Count(s=>s.StartsWith("PASS"))} passed, {rows.Count(s=>s.StartsWith("FAIL"))} failed");
        File.WriteAllLines("Artifacts/CityQA/role-character-checks.txt",rows);
    }
}
#endif
