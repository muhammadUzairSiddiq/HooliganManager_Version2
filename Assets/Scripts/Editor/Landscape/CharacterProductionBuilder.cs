#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMeshSimplifier;
using Object=UnityEngine.Object;

/// <summary>Reproducible offline asset build. Never decimates at runtime or edits source FBX files.</summary>
public static class CharacterProductionBuilder
{
    const string Root="Assets/Generated/CharacterProduction";
    const string ControllerPath="Assets/Models/Character/animations/CharacterAnimatorController.controller";
    static readonly List<string> report=new List<string>();
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop gameplay before generating character assets.");
        Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Root+"/Animations");
        Directory.CreateDirectory("Assets/Resources/CharacterProduction");AssetDatabase.Refresh();
        report.Clear();
        var shader=Shader.Find("Universal Render Pipeline/Simple Lit");
        var shirt=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CharacterProduction/GangShirt.mat");
        if(!shirt){shirt=new Material(shader);AssetDatabase.CreateAsset(shirt,"Assets/Resources/CharacterProduction/GangShirt.mat");}
        shirt.SetColor("_BaseColor",Color.white);shirt.SetTexture("_BaseMap",null);shirt.SetFloat("_Smoothness",.12f);
        var built=new Dictionary<string,GameObject>();
        var textures=new HashSet<string>();
        foreach(var guid in AssetDatabase.FindAssets("t:CharacterPortraitRegistry"))
        {
            var registry=AssetDatabase.LoadAssetAtPath<CharacterPortraitRegistry>(AssetDatabase.GUIDToAssetPath(guid));
            foreach(var entry in registry.entries)
            {
                string source=string.IsNullOrEmpty(entry.sourceModelAssetPath)?AssetDatabase.GetAssetPath(entry.modelPrefab):entry.sourceModelAssetPath;
                if(string.IsNullOrEmpty(source))continue;
                if(!built.TryGetValue(source,out var prefab))
                {
                    prefab=BuildModel(source,shirt,textures);built[source]=prefab;
                }
                if(!prefab)continue;
                entry.sourceModelAssetPath=source;
                // Retain source path, not a runtime reference that forces high-poly FBX meshes into memory.
                entry.modelPrefab=prefab;entry.optimizedModelPrefab=prefab;
            }
            EditorUtility.SetDirty(registry);
        }
        foreach(string path in textures)
        {
            if(!(AssetImporter.GetAtPath(path) is TextureImporter importer))continue;
            importer.maxTextureSize=importer.textureType==TextureImporterType.NormalMap?512:1024;
            importer.isReadable=false;importer.mipmapEnabled=true;importer.streamingMipmaps=true;
            var android=importer.GetPlatformTextureSettings("Android");
            android.overridden=true;android.maxTextureSize=importer.textureType==TextureImporterType.NormalMap?256:512;
            android.format=TextureImporterFormat.ASTC_6x6;android.compressionQuality=50;
            importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
        }
        report.Add($"TEXTURES {textures.Count} unique textures: desktop <=1024, Android <=512 ASTC 6x6; mip streaming enabled.");
        BuildAnimations();
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Directory.CreateDirectory("Artifacts/CityQA");File.WriteAllLines("Artifacts/CityQA/character-production-build.txt",report);
    }

    static GameObject BuildModel(string path,Material shirt,HashSet<string> textures)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!source)return null;
        string id=Path.GetFileNameWithoutExtension(path);
        var root=Object.Instantiate(source);root.name=id+"_RTS";
        root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var temporaryMeshes=new List<Mesh>();
        try
        {
            SplitVerifiedClothing(root,id,temporaryMeshes);
            var originals=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var lod0=new List<Renderer>();var lod1=new List<Renderer>();
            int before=0,after=0,far=0,shirts=0;
            foreach(var renderer in originals)
            {
                var mesh=renderer.sharedMesh;if(!mesh)continue;
                int triangles=mesh.triangles.Length/3;before+=triangles;
                bool clothing=CrewKit.IsShirtRenderer(renderer.name);
                if(clothing){renderer.sharedMaterials=Enumerable.Repeat(shirt,renderer.sharedMaterials.Length).ToArray();shirts++;}
                else foreach(var material in renderer.sharedMaterials)
                {
                    if(!material)continue;
                    foreach(string property in material.GetTexturePropertyNames())
                    {
                        var texture=material.GetTexture(property);if(!texture)continue;
                        string texturePath=AssetDatabase.GetAssetPath(texture);if(!string.IsNullOrEmpty(texturePath))textures.Add(texturePath);
                    }
                }
                // Tiny alpha eyelash cards are not visible at RTS scale and waste draw calls.
                if(renderer.name.ToLowerInvariant().Contains("eyelash")){Object.DestroyImmediate(renderer.gameObject);continue;}
                var medium=Reduce(mesh,.16f,id+"_"+renderer.name+"_Near");
                var low=Reduce(mesh,.065f,id+"_"+renderer.name+"_Far");
                after+=medium.triangles.Length/3;far+=low.triangles.Length/3;
                renderer.sharedMesh=medium;renderer.quality=SkinQuality.Bone2;renderer.updateWhenOffscreen=false;
                lod0.Add(renderer);
                var child=new GameObject(renderer.name+"_LOD1");child.transform.SetParent(renderer.transform.parent,false);
                child.transform.localPosition=renderer.transform.localPosition;child.transform.localRotation=renderer.transform.localRotation;child.transform.localScale=renderer.transform.localScale;
                var distant=child.AddComponent<SkinnedMeshRenderer>();distant.sharedMesh=low;distant.sharedMaterials=renderer.sharedMaterials;
                distant.bones=renderer.bones;distant.rootBone=renderer.rootBone;distant.localBounds=renderer.localBounds;
                distant.quality=SkinQuality.Bone2;distant.updateWhenOffscreen=false;distant.shadowCastingMode=ShadowCastingMode.Off;
                lod1.Add(distant);
            }
            var group=root.GetComponent<LODGroup>();if(!group)group=root.AddComponent<LODGroup>();
            group.SetLODs(new[]{new LOD(.075f,lod0.ToArray()),new LOD(.012f,lod1.ToArray())});
            group.RecalculateBounds();group.fadeMode=LODFadeMode.None;
            string output=Root+"/Prefabs/"+id+"_RTS.prefab";
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,output);
            report.Add($"MODEL {id}: {before:N0} -> {after:N0} near / {far:N0} far triangles; {shirts} independent clothing renderer(s)."+
                (shirts==0?" SHIRT TINT SKIPPED: combined body has no verified clothing partition.":" Shirt texture removed; other materials preserved."));
            return prefab;
        }
        finally{Object.DestroyImmediate(root);foreach(var mesh in temporaryMeshes)Object.DestroyImmediate(mesh);}
    }

    static void SplitVerifiedClothing(GameObject root,string id,List<Mesh> temporary)
    {
        // Audited disconnected garment islands in the shipped combined meshes.
        // Exact topology signatures deliberately fail closed after an art reimport.
        int signature=id=="Ch06_nonPBR"?13986:id=="Ch12_nonPBR"?6070:id=="Ch18_nonPBR"?6587:0;
        if(signature==0)return;
        var renderer=root.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault();if(!renderer)return;
        var source=renderer.sharedMesh;
        var garment=ClothingTopologyAudit.Islands(source).SingleOrDefault(i=>i.indices.Count/3==signature);
        if(garment==null)throw new InvalidOperationException("Clothing topology changed for "+id+". Review the garment partition before building.");
        var vertices=new HashSet<int>(garment.indices);
        var body=Object.Instantiate(source);temporary.Add(body);
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            var indices=source.GetTriangles(sub);var keep=new List<int>(indices.Length);
            for(int i=0;i<indices.Length;i+=3)
                if(sub!=garment.submesh||!vertices.Contains(indices[i])){keep.Add(indices[i]);keep.Add(indices[i+1]);keep.Add(indices[i+2]);}
            body.SetTriangles(keep,sub);
        }
        renderer.sharedMesh=body;
        var shirt=Object.Instantiate(source);temporary.Add(shirt);shirt.subMeshCount=1;shirt.SetTriangles(garment.indices,0);
        var go=new GameObject(id+"_Shirt");go.transform.SetParent(renderer.transform.parent,false);
        go.transform.localPosition=renderer.transform.localPosition;go.transform.localRotation=renderer.transform.localRotation;go.transform.localScale=renderer.transform.localScale;
        var split=go.AddComponent<SkinnedMeshRenderer>();split.sharedMesh=shirt;split.sharedMaterial=renderer.sharedMaterials[garment.submesh];
        split.bones=renderer.bones;split.rootBone=renderer.rootBone;split.localBounds=renderer.localBounds;
        report.Add($"PARTITION {id}: isolated {signature:N0} garment triangles; body/face/hands/trousers remain textured.");
    }

    static Mesh Reduce(Mesh source,float quality,string name)
    {
        var simplifier=new MeshSimplifier();
        var options=SimplificationOptions.Default;
        // Smart linking keeps coincident seam vertices coherent while allowing
        // dense hair-card/UV borders to simplify down to an actual mobile budget.
        options.PreserveBorderEdges=false;options.PreserveUVSeamEdges=false;options.PreserveUVFoldoverEdges=false;
        simplifier.SimplificationOptions=options;simplifier.Initialize(source);simplifier.SimplifyMesh(quality);
        var mesh=simplifier.ToMesh();mesh.name=name;mesh.bindposes=source.bindposes;mesh.RecalculateBounds();
        string path=Root+"/Meshes/"+name+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
        else AssetDatabase.CreateAsset(mesh,path);
        return mesh;
    }

    public static void BuildAnimations()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Character/Ch01/Ch01_nonPBR.fbx");
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/Character/animations/Idle.anim");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if(!source||!idle||!controller)throw new InvalidOperationException("Animation source rig or controller missing.");
        var root=Object.Instantiate(source);
        try
        {
            // Neck is shared by all rigs; police uses a differently named head bone.
            var bones=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("mixamorig:")&&!t.name.Contains("Head")).ToArray();
            foreach(string task in new[]{"Chant","Watch","Treat","Pickup","Talk"})
            {
                var clip=new AnimationClip{name="Task "+task,frameRate=24,wrapMode=WrapMode.Loop};
                var curves=new Dictionary<Transform,AnimationCurve[]>();
                foreach(var bone in bones)curves[bone]=Enumerable.Range(0,10).Select(_=>new AnimationCurve()).ToArray();
                const float duration=2f;
                for(int frame=0;frame<=48;frame++)
                {
                    float time=frame/24f;
                    idle.SampleAnimation(root,time%duration);
                    PoseTask(root.transform,bones,task,time/duration);
                    foreach(var bone in bones)
                    {
                        var p=bone.localPosition;var q=bone.localRotation;var c=curves[bone];
                        var s=bone.localScale;
                        float[] v={p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};
                        for(int i=0;i<10;i++)c[i].AddKey(time,v[i]);
                    }
                }
                string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
                foreach(var pair in curves)
                {
                    string bonePath=AnimationUtility.CalculateTransformPath(pair.Key,root.transform);
                    for(int i=0;i<10;i++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(bonePath,typeof(Transform),properties[i]),pair.Value[i]);
                }
                clip.EnsureQuaternionContinuity();
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=true;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                string path=Root+"/Animations/Task "+task+".anim";
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(existing){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);clip=existing;}
                else AssetDatabase.CreateAsset(clip,path);
                var machine=controller.layers[0].stateMachine;
                var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==clip.name)??machine.AddState(clip.name);
                state.motion=clip;state.writeDefaultValues=true;
                report.Add("ANIMATION "+clip.name+" · 2 seconds / 24 fps / generic Mixamo bone paths / in-place loop");
            }
            EditorUtility.SetDirty(controller);
        }
        finally{Object.DestroyImmediate(root);}
    }

    static void PoseTask(Transform root,Transform[] bones,string task,float phase)
    {
        Transform Bone(string suffix)=>bones.FirstOrDefault(t=>t.name=="mixamorig:"+suffix);
        float wave=Mathf.Sin(phase*Mathf.PI*2f);
        var head=Bone("Neck");var spine=Bone("Spine1");
        if(task=="Watch")
        {
            if(head)head.rotation=Quaternion.AngleAxis(wave*35f,root.up)*head.rotation;
            return;
        }
        if(task=="Pickup"||task=="Treat")
            if(spine)spine.rotation=Quaternion.AngleAxis(task=="Pickup"?18f+12f*wave:12f,root.right)*spine.rotation;
        for(int side=-1;side<=1;side+=2)
        {
            string prefix=side<0?"Left":"Right";
            var arm=Bone(prefix+"Arm");var fore=Bone(prefix+"ForeArm");var hand=Bone(prefix+"Hand");
            if(!arm||!fore||!hand)continue;
            Vector3 direction;
            Vector3 wristDirection;
            if(task=="Chant")
            {direction=new Vector3(side*.6f,.6f,.3f);wristDirection=new Vector3(side*.15f,1f,.1f+wave*.25f);}
            else if(task=="Treat")
            {direction=new Vector3(side*.3f,-.65f,.65f);wristDirection=new Vector3(-side*.35f,-.2f,.8f+wave*.15f);}
            else if(task=="Pickup")
            {direction=new Vector3(side*.2f,-.85f,.4f);wristDirection=new Vector3(-side*.15f,-.5f,.7f);}
            else
            {direction=new Vector3(side*.35f,-.65f,.5f);wristDirection=new Vector3(side*.25f,.2f+wave*.2f,.75f);}
            arm.rotation=Quaternion.FromToRotation(fore.position-arm.position,root.TransformDirection(direction))*arm.rotation;
            fore.rotation=Quaternion.FromToRotation(hand.position-fore.position,root.TransformDirection(wristDirection))*fore.rotation;
        }
    }
}
#endif
