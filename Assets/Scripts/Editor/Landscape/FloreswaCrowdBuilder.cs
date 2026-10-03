#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class FloreswaCrowdBuilder
{
    public static void Build()
    {
        const string folder="Assets/Resources/FloreswaCrowd";
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var library=AssetDatabase.LoadAssetAtPath<CrowdPoseLibrary>(folder+"/Poses.asset");
        if(!library){library=ScriptableObject.CreateInstance<CrowdPoseLibrary>();AssetDatabase.CreateAsset(library,folder+"/Poses.asset");}
        var variants=new List<CrowdPoseLibrary.Variant>();
        foreach(var action in new[]{"Cheering","Rallying","Clapping","Standing Clap","Victory","Excited","Happy Hand Gesture","Cheering (1)","Standing Fist Pump","Dwarf Walk"})
        {
            var instance=Object.Instantiate(RoleCharacterModels.Current.civilians.entries[0].modelPrefab);
            instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);instance.transform.localScale=Vector3.one;
            try
            {
                string id="Supporter_"+variants.Count;var variant=new CrowdPoseLibrary.Variant{name=action,poses=new Mesh[16]};
                var controller=instance.GetComponent<Animator>().runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                var clip=controller.layers[0].stateMachine.states.First(s=>s.state.name==action).state.motion as AnimationClip;
                variant.duration=clip.length;
                for(int pose=0;pose<variant.poses.Length;pose++)
                {
                    clip.SampleAnimation(instance,clip.length*pose/variant.poses.Length);
                    var vertices=new List<Vector3>();var normals=new List<Vector3>();var colours=new List<Color>();var indices=new List<int>();
                    foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var baked=new Mesh();skin.BakeMesh(baked);var v=baked.vertices;var n=baked.normals;var bakedColors=baked.colors;var mats=skin.sharedMaterials;
                        for(int sub=0;sub<baked.subMeshCount;sub++)
                        {
                            var material=mats[Mathf.Min(sub,mats.Length-1)];Color color=material.color;
                            color.a=material.name.StartsWith("FactionCloth")?0:1;
                            var remap=new Dictionary<int,int>();
                            foreach(int index in baked.GetTriangles(sub))
                            {
                                if(!remap.TryGetValue(index,out var mapped))
                                {
                                    mapped=vertices.Count;remap[index]=mapped;
                                    vertices.Add(skin.transform.TransformPoint(v[index])*instance.GetComponent<CharacterVisualProfile>().baseScale);
                                    normals.Add(skin.transform.TransformDirection(n[index]).normalized);colours.Add(bakedColors.Length==v.Length?bakedColors[index]:color);
                                }
                                indices.Add(mapped);
                            }
                        }
                        Object.DestroyImmediate(baked);
                    }
                    var mesh=new Mesh{name=id+"_"+pose};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colours);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
                    var full=mesh;
                    // Simplify the frozen spectator pose, never the live rig or bones.
                    // Nearby interactive characters retain the user's authored model.
                    for(float quality=.3f;quality>=.08f;quality-=.04f)
                    {
                        var reducer=new UnityMeshSimplifier.MeshSimplifier();
                        var settings=UnityMeshSimplifier.SimplificationOptions.Default;
                        settings.PreserveBorderEdges=false;settings.PreserveUVSeamEdges=false;settings.PreserveUVFoldoverEdges=false;
                        reducer.SimplificationOptions=settings;reducer.Initialize(full);reducer.SimplifyMesh(quality);
                        var reduced=reducer.ToMesh();reduced.name=full.name;reduced.RecalculateBounds();
                        if(mesh!=full)Object.DestroyImmediate(mesh);mesh=reduced;
                        if(mesh.vertexCount<1000)break;
                    }
                    Object.DestroyImmediate(full);
                    string output=folder+"/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(output);
                    if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,output);
                    variant.poses[pose]=mesh;
                }
                variants.Add(variant);
            }
            finally{Object.DestroyImmediate(instance);}
        }
        library.variants=variants.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
        File.WriteAllText("Artifacts/CityQA/floreswa-crowd.txt",$"{variants.Count} supporter animations, 16 poses each, using the supplied CivilianCharacters model.");
    }
}
#endif
