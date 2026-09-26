#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterProductionAudit
{
    public static void Preview(bool gestures = false)
    {
        var registry=AssetDatabase.LoadAssetAtPath<CharacterPortraitRegistry>("Assets/Data/CharacterPortraitRegistry.asset");
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/Character/animations/Idle.anim");
        const int w=240,h=300;
        string[] tasks={"Chant","Watch","Treat","Pickup","Talk"};
        int columns=gestures?tasks.Length:registry.entries.Count;
        var sheet=new Texture2D(w*columns,h*2,TextureFormat.RGB24,false);
        var diagnostics=new List<string>();
        for(int i=0;i<columns;i++)for(int lod=0;lod<2;lod++)
        {
            var entry=registry.entries[gestures?0:i];var model=entry.optimizedModelPrefab?entry.optimizedModelPrefab:entry.modelPrefab;
            var preview=new PreviewRenderUtility();
            try
            {
                var instance=Object.Instantiate(model);preview.AddSingleGO(instance);
                instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                CrewKit.PaintShirt(instance.transform,new Color(.12f,.85f,.25f));
                var clip=gestures?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Generated/CharacterProduction/Animations/Task "+tasks[i]+".anim"):idle;
                clip.SampleAnimation(instance,gestures?(lod==0?.3f:1.2f):.2f);
                foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var baked=new Mesh();skin.BakeMesh(baked);
                    diagnostics.Add(clip.name+" "+skin.name+" baked="+baked.bounds+" root="+instance.transform.localScale+" world="+skin.bounds);
                    Object.DestroyImmediate(baked);
                }
                instance.GetComponent<LODGroup>()?.ForceLOD(gestures?0:lod);
                preview.camera.orthographic=true;preview.camera.orthographicSize=1.05f;
                preview.camera.transform.position=new Vector3(0,1,-3.5f);preview.camera.transform.LookAt(new Vector3(0,1,0));
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=20;
                preview.camera.backgroundColor=new Color(.12f,.15f,.18f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(40,180,0);
                preview.lights[1].intensity=.8f;preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.BeginStaticPreview(new Rect(0,0,w,h));preview.Render(true);var image=preview.EndStaticPreview();
                sheet.SetPixels(i*w,(1-lod)*h,w,h,image.GetPixels());Object.DestroyImmediate(image);
            }
            finally{preview.Cleanup();}
        }
        sheet.Apply();Directory.CreateDirectory("Artifacts/CityQA");File.WriteAllBytes("Artifacts/CityQA/"+(gestures?"character-gesture-contact-sheet.png":"character-lod-contact-sheet.png"),sheet.EncodeToPNG());Object.DestroyImmediate(sheet);
        File.WriteAllLines("Artifacts/CityQA/preview-bounds.txt",diagnostics);
    }

    public static void Run()
    {
        var lines=new List<string>();
        foreach(var guid in AssetDatabase.FindAssets("t:CharacterPortraitRegistry"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var registry=AssetDatabase.LoadAssetAtPath<CharacterPortraitRegistry>(path);
            lines.Add("REGISTRY "+path);
            foreach(var entry in registry.entries)
            {
                if(!entry.modelPrefab)continue;
                lines.Add("MODEL "+AssetDatabase.GetAssetPath(entry.modelPrefab));
                var animator=entry.modelPrefab.GetComponentInChildren<Animator>(true);
                lines.Add(" AVATAR "+(animator&&animator.avatar?animator.avatar.name+" human="+animator.avatar.isHuman+" valid="+animator.avatar.isValid:"none"));
                foreach(var r in entry.modelPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var m=r.sharedMesh;if(!m)continue;
                    lines.Add($" MESH {r.name} vertices={m.vertexCount} triangles={m.triangles.Length/3} submeshes={m.subMeshCount} bounds={m.bounds} readable={m.isReadable}");
                    for(int i=0;i<r.sharedMaterials.Length;i++)
                    {
                        var mat=r.sharedMaterials[i];if(!mat)continue;
                        lines.Add($"  MATERIAL {i} {mat.name} shader={mat.shader.name} path={AssetDatabase.GetAssetPath(mat)} texture={AssetDatabase.GetAssetPath(mat.mainTexture)}");
                    }
                }
                if(entry.animatorController)
                {
                    lines.Add(" CONTROLLER "+AssetDatabase.GetAssetPath(entry.animatorController));
                    foreach(var clip in entry.animatorController.animationClips.Distinct())lines.Add($"  CLIP {clip.name} {clip.length:F2}s humanoid={clip.humanMotion}");
                }
            }
        }
        Directory.CreateDirectory("Artifacts/CityQA");File.WriteAllLines("Artifacts/CityQA/character-production-audit.txt",lines);
    }
}
#endif
