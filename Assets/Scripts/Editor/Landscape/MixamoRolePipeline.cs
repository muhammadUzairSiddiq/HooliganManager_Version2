#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Object=UnityEngine.Object;

public static class MixamoRolePipeline
{
    const string Root="Assets/Generated/RoleCharacters";
    public static void Polish()
    {
        var roles=RoleCharacterModels.Current;
        var controller=roles.gangs.entries[0].animatorController as AnimatorController;
        var machine=controller.layers[0].stateMachine;
        AnimationClip ImportMotion(string name)
        {
            string file="Assets/Floreswa/Models/Animation Clips/male03_2@"+name+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(file);
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            var settings=importer.defaultClipAnimations;
            foreach(var clip in settings){clip.loopTime=true;clip.lockRootPositionXZ=true;clip.lockRootRotation=true;}
            importer.clipAnimations=settings;importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        }
        var locomotion=ImportMotion("Dwarf Walk");
        var idle=ImportMotion("Bouncing Fight Idle");
        foreach(string name in new[]{"Walking","Running","Unarmed Run Forward","Dwarf Walk"})
        {
            var state=machine.states.FirstOrDefault(s=>s.state.name==name).state;
            if(!state)state=machine.AddState(name);state.motion=locomotion;state.speed=1;
        }
        foreach(string name in new[]{"Idle","Ready Idle","Police Watch"})
        {
            var state=machine.states.FirstOrDefault(s=>s.state.name==name).state;
            if(!state)state=machine.AddState(name);state.motion=idle;state.speed=1;
        }
        float death=(machine.states.First(s=>s.state.name=="Dying Backwards").state.motion as AnimationClip).length;
        foreach(var registry in new[]{roles.civilians,roles.gangs,roles.police})
        {
            string path=AssetDatabase.GetAssetPath(registry.entries[0].modelPrefab);var root=PrefabUtility.LoadPrefabContents(path);
            ConsolidateMaterials(root,registry.entries[0].sourceModelAssetPath);
            root.GetComponent<CharacterVisualProfile>().deathSeconds=death;
            root.GetComponent<CharacterVisualProfile>().attackSeconds=new[]{"Right Punching","Left Punching","Right Cross Punch","Right Hook Punch"}.Select(n=>(machine.states.First(s=>s.state.name==n).state.motion as AnimationClip).length).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
        }
        // Preserve the imported prefab GUIDs while repairing references to FBXs
        // the user replaced with the three new Mixamo role models.
        foreach(var file in Directory.GetFiles("Assets/Floreswa/Prefabs","male*.prefab"))
        {
            string id=Path.GetFileNameWithoutExtension(file);
            var replacement=id=="male03_2"?roles.police:id.StartsWith("male01")?roles.gangs:roles.civilians;
            var instance=Object.Instantiate(replacement.entries[0].modelPrefab);instance.name=id;
            PrefabUtility.SaveAsPrefabAsset(instance,file.Replace('\\','/'));Object.DestroyImmediate(instance);
        }
        foreach(string path in new[]{"Assets/Resources/Audio/stadium_ambience.mp3","Assets/Resources/Audio/stadium_cheer.mp3"})
        {
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;if(!importer)continue;
            importer.forceToMono=true;var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.5f;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        foreach(var unused in new[]{"NeutralIdle","NeutralIdleCorrected","RunningCorrected","DirectRunning","NeutralGrounded","RunningGrounded"})
            if(AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/"+unused+".anim"))AssetDatabase.DeleteAsset(Root+"/"+unused+".anim");
    }
    static void ConsolidateMaterials(GameObject root,string sourcePath)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var original=source.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name==skin.name);
            var mesh=original.sharedMesh;
            var vertices=mesh.vertices;var normals=mesh.normals;var weights=mesh.boneWeights;
            var outputVertices=new List<Vector3>();var outputNormals=new List<Vector3>();var outputWeights=new List<BoneWeight>();var colors=new List<Color>();var triangles=new List<int>();
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var material=original.sharedMaterials[sub];string name=material.name.ToLowerInvariant();
                bool cloth=name.Contains("shirt")||name.Contains("cardigan")||name.Contains("jacket");
                bool uniform=root.name.Contains("Police")&&(cloth||name.Contains("pant")||name.Contains("cap"));
                var color=uniform?new Color(.15f,.35f,.95f):material.color;color.a=cloth?0:1;
                var remap=new Dictionary<int,int>();
                foreach(int index in mesh.GetTriangles(sub))
                {
                    if(!remap.TryGetValue(index,out int mapped))
                    {mapped=outputVertices.Count;remap[index]=mapped;outputVertices.Add(vertices[index]);outputNormals.Add(normals[index]);outputWeights.Add(weights[index]);colors.Add(color);}
                    triangles.Add(mapped);
                }
            }
            var merged=new Mesh{name=root.name+" single draw"};merged.SetVertices(outputVertices);merged.SetNormals(outputNormals);merged.SetColors(colors);merged.SetTriangles(triangles,0);merged.boneWeights=outputWeights.ToArray();merged.bindposes=mesh.bindposes;merged.RecalculateBounds();
            string meshPath=Root+"/"+root.name+"_"+skin.name+"_Mesh.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(saved){EditorUtility.CopySerialized(merged,saved);Object.DestroyImmediate(merged);}else{AssetDatabase.CreateAsset(merged,meshPath);saved=merged;}
            string matPath=Root+"/Materials/"+root.name+"_Single.mat";var kit=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!kit){kit=new Material(Shader.Find("Hooligan/InstancedSupporter"));AssetDatabase.CreateAsset(kit,matPath);}
            kit.name="FactionCloth_"+root.name;kit.SetColor("_BaseColor",root.name.Contains("Police")?new Color(.15f,.35f,.95f):Color.white);EditorUtility.SetDirty(kit);
            skin.sharedMesh=saved;skin.sharedMaterials=new[]{kit};
        }
    }
    public static void Portraits()
    {
        var roles=RoleCharacterModels.Current;int index=0;
        foreach(var registry in new[]{roles.civilians,roles.gangs,roles.police})
        {
            var entry=registry.entries[0];var preview=new PreviewRenderUtility();
            string output=Root+"/"+registry.name+"_Portrait.png";
            try
            {
                var instance=Object.Instantiate(entry.modelPrefab);preview.AddSingleGO(instance);
                instance.transform.localScale=Vector3.one*instance.GetComponent<CharacterVisualProfile>().baseScale;
                CrewKit.PaintShirt(instance.transform,index==0?Color.white:index==1?new Color(.025f,.32f,.12f):new Color(.15f,.35f,.95f));
                var controller=(AnimatorController)entry.animatorController;
                ((AnimationClip)controller.layers[0].stateMachine.states.First(s=>s.state.name=="Walking").state.motion).SampleAnimation(instance,.1f);
                preview.camera.orthographic=true;preview.camera.orthographicSize=.44f;
                preview.camera.transform.position=new Vector3(0,1.45f,3f);preview.camera.transform.LookAt(new Vector3(0,1.45f,0));
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10;
                preview.camera.backgroundColor=new Color(.08f,.1f,.12f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(35,180,0);
                preview.lights[1].intensity=.8f;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,256,256));preview.Render(true);var texture=preview.EndStaticPreview();
                File.WriteAllBytes(output,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            finally{preview.Cleanup();}
            AssetDatabase.ImportAsset(output);var importer=(TextureImporter)AssetImporter.GetAtPath(output);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.SaveAndReimport();
            entry.portrait=AssetDatabase.LoadAssetAtPath<Sprite>(output);EditorUtility.SetDirty(registry);index++;
        }
        foreach(var guid in AssetDatabase.FindAssets("t:CharacterPortraitRegistry"))
        {
            var registry=AssetDatabase.LoadAssetAtPath<CharacterPortraitRegistry>(AssetDatabase.GUIDToAssetPath(guid));
            foreach(var entry in registry.entries)
            {
                if(entry.modelPrefab==roles.gangs.entries[0].modelPrefab)entry.portrait=roles.gangs.entries[0].portrait;
                else if(entry.modelPrefab==roles.police.entries[0].modelPrefab)entry.portrait=roles.police.entries[0].portrait;
            }
            EditorUtility.SetDirty(registry);
        }
        AssetDatabase.SaveAssets();
    }
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Character migration requires edit mode.");
        Directory.CreateDirectory(Root);Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
        var report=new List<string>();
        var clips=new Dictionary<string,AnimationClip>();
        foreach(var file in Directory.GetFiles("Assets/Floreswa/Models/Animation Clips","*.fbx"))
        {
            string path=file.Replace('\\','/');var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            var settings=importer.defaultClipAnimations;
            foreach(var c in settings){c.loopTime=!(path.Contains("Dying")||path.Contains("Hit")||path.Contains("Punch")||path.Contains("Kick"));c.lockRootPositionXZ=true;c.lockRootRotation=true;}
            importer.clipAnimations=settings;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            string name=Path.GetFileNameWithoutExtension(path).Split('@').Last();clips[name]=clip;
            report.Add("CLIP "+name+" humanoid="+clip.humanMotion);
            if(!clip.humanMotion)throw new InvalidOperationException(name+" did not import as humanoid.");
        }
        string controllerPath=Root+"/Shared.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine=controller.layers[0].stateMachine;foreach(var state in machine.states)machine.RemoveState(state.state);
        foreach(var pair in clips){var state=machine.AddState(pair.Key);state.motion=pair.Value;}
        var aliases=new Dictionary<string,string>{
            {"Idle","Bouncing Fight Idle"},{"Ready Idle","Bouncing Fight Idle"},{"Happy Idle","Happy Hand Gesture"},
            {"Unarmed Run Forward","Running"},{"Right Punching","Punching"},{"Left Punching","Fist Fight A"},
            {"Right Cross Punch","Cross Punch"},{"Right Hook Punch","Fist Fight B"},
            {"Falling Back Death From Right Punch","Dying Backwards"},{"Falling Forward Death From Left Punch","Dying Backwards"},
            {"Task Chant","Rallying"},{"Task Watch","Standing Fist Pump"},{"Task Treat","Clapping"},{"Task Pickup","Excited"},{"Task Talk","Happy Hand Gesture"}};
        foreach(var pair in aliases){var state=machine.AddState(pair.Key);state.motion=clips[pair.Value];if(pair.Key=="Idle")machine.defaultState=state;}
        EditorUtility.SetDirty(controller);
        var registries=new List<CharacterPortraitRegistry>();
        foreach(string role in new[]{"CivilianCharacters","GangCharacters","PoliceCharacters"})
        {
            string path="Assets/Floreswa/Models/"+role+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.isReadable=true;importer.SaveAndReimport();
            var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));instance.name=role;
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);instance.transform.localScale=Vector3.one;
                var animator=instance.GetComponent<Animator>();if(!animator)animator=instance.AddComponent<Animator>();
                if(!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)throw new InvalidOperationException(role+" invalid avatar");
                animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>();Bounds bounds=skins[0].bounds;
                foreach(var skin in skins)
                {
                    bounds.Encapsulate(skin.bounds);var mats=skin.sharedMaterials;
                    for(int i=0;i<mats.Length;i++)
                    {
                        var original=mats[i];string name=original.name.ToLowerInvariant();
                        bool cloth=name.Contains("shirt")||name.Contains("cardigan")||name.Contains("jacket")||name.Contains("tshirt");
                        bool uniform=role=="PoliceCharacters"&&(cloth||name.Contains("pant")||name.Contains("cap")||name.Contains("hat"));
                        string matName=(cloth?"FactionCloth_":"Surface_")+role+"_"+i;
                        string matPath=Root+"/Materials/"+skin.name+"_"+matName+".mat";
                        var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));AssetDatabase.CreateAsset(mat,matPath);}
                        mat.name=matName;mat.color=uniform?new Color(.15f,.35f,.95f):cloth?Color.white:original.color;
                        mat.SetFloat("_Smoothness",.05f);mats[i]=mat;EditorUtility.SetDirty(mat);
                        report.Add(role+" "+skin.name+" slot="+name+" uniform="+uniform+" cloth="+cloth);
                    }
                    skin.sharedMaterials=mats;skin.quality=SkinQuality.Bone2;
                }
                var profile=instance.AddComponent<CharacterVisualProfile>();profile.baseScale=1.8f/Mathf.Max(.1f,bounds.size.y);
                var prefab=PrefabUtility.SaveAsPrefabAsset(instance,Root+"/"+role+".prefab");
                string registryPath=Root+"/"+role+".asset";var registry=AssetDatabase.LoadAssetAtPath<CharacterPortraitRegistry>(registryPath);
                if(!registry){registry=ScriptableObject.CreateInstance<CharacterPortraitRegistry>();AssetDatabase.CreateAsset(registry,registryPath);}
                registry.entries=new List<CharacterPortraitRegistry.Entry>{new CharacterPortraitRegistry.Entry{modelPrefab=prefab,optimizedModelPrefab=prefab,animatorController=controller,sourceModelAssetPath=path}};
                EditorUtility.SetDirty(registry);registries.Add(registry);
                report.Add(role+" vertices="+skins.Sum(s=>s.sharedMesh.vertexCount)+" triangles="+skins.Sum(s=>s.sharedMesh.triangles.Length/3)+" height="+bounds.size.y);
            }
            finally{Object.DestroyImmediate(instance);}
        }
        var models=AssetDatabase.LoadAssetAtPath<RoleCharacterModels>("Assets/Resources/RoleCharacterModels.asset");
        if(!models){models=ScriptableObject.CreateInstance<RoleCharacterModels>();AssetDatabase.CreateAsset(models,"Assets/Resources/RoleCharacterModels.asset");}
        models.civilians=registries[0];models.gangs=registries[1];models.police=registries[2];EditorUtility.SetDirty(models);
        foreach(var guid in AssetDatabase.FindAssets("t:CharacterPortraitRegistry"))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);if(path.StartsWith(Root))continue;
            var registry=AssetDatabase.LoadAssetAtPath<CharacterPortraitRegistry>(path);
            var replacement=path.ToLowerInvariant().Contains("police")?registries[2].entries[0]:registries[1].entries[0];
            foreach(var entry in registry.entries){entry.modelPrefab=entry.optimizedModelPrefab=replacement.modelPrefab;entry.animatorController=controller;entry.sourceModelAssetPath=replacement.sourceModelAssetPath;}
            EditorUtility.SetDirty(registry);
        }
        AssetDatabase.SaveAssets();
        Polish();
        Portraits();
        File.WriteAllLines("Artifacts/CityQA/role-characters.txt",report);
    }
}
#endif
