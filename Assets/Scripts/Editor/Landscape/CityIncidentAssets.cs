#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Finished low-poly road-safety prefabs; runtime never substitutes UI rings or hidden roads.</summary>
public static class CityIncidentAssets
{
    const string Folder="Assets/Resources/CityIncidents";
    public static void Ensure()
    {
        Directory.CreateDirectory(Folder);
        var orange=Material("SafetyOrange",new Color(1,.36f,.045f));var white=Material("ReflectiveWhite",new Color(.96f,.96f,.88f));var rubber=Material("RubberBase",new Color(.075f,.085f,.09f));
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/TrafficCone.prefab"))
            {
                var root=new GameObject("Traffic cone - striped 3D safety mesh");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var body=new GameObject("Tapered striped cone",typeof(MeshFilter),typeof(MeshRenderer));body.transform.SetParent(root.transform,false);
                var vertices=new List<Vector3>();var orangeTriangles=new List<int>();var whiteTriangles=new List<int>();
                float[] heights={.09f,.29f,.47f,.65f,.83f,.96f};float[] radii={.42f,.34f,.27f,.2f,.12f,.055f};
                for(int band=0;band<heights.Length-1;band++)for(int side=0;side<12;side++)
                {
                    float a=side*Mathf.PI/6,b=(side+1)*Mathf.PI/6;int v=vertices.Count;
                    vertices.Add(new Vector3(Mathf.Cos(a)*radii[band],heights[band],Mathf.Sin(a)*radii[band]));vertices.Add(new Vector3(Mathf.Cos(b)*radii[band],heights[band],Mathf.Sin(b)*radii[band]));
                    vertices.Add(new Vector3(Mathf.Cos(b)*radii[band+1],heights[band+1],Mathf.Sin(b)*radii[band+1]));vertices.Add(new Vector3(Mathf.Cos(a)*radii[band+1],heights[band+1],Mathf.Sin(a)*radii[band+1]));
                    var triangles=band==1||band==3?whiteTriangles:orangeTriangles;triangles.AddRange(new[]{v,v+2,v+1,v,v+3,v+2});
                }
                var mesh=new Mesh{name="Striped twelve-sided road cone"};mesh.SetVertices(vertices);mesh.subMeshCount=2;mesh.SetTriangles(orangeTriangles,0);mesh.SetTriangles(whiteTriangles,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,Folder+"/TrafficCone.asset");body.GetComponent<MeshFilter>().sharedMesh=mesh;body.GetComponent<MeshRenderer>().sharedMaterials=new[]{orange,white};
                var foot=GameObject.CreatePrimitive(PrimitiveType.Cube);foot.name="Heavy square rubber foot";foot.transform.SetParent(root.transform,false);foot.transform.localPosition=Vector3.up*.055f;foot.transform.localScale=new Vector3(.98f,.11f,.98f);Object.DestroyImmediate(foot.GetComponent<Collider>());foot.GetComponent<Renderer>().sharedMaterial=rubber;
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/TrafficCone.prefab");Object.DestroyImmediate(root);
            }
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/RoadBarricade.prefab"))
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Megacity/Prefabs/Props/fencing_001.prefab");
                if(!source)throw new System.InvalidOperationException("Existing Megacity fencing_001 is required for road barricades.");
                var root=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);root.name="Road barricade - existing city fencing";
                foreach(var collider in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
                var renderers=root.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                root.transform.localScale*=3.1f/Mathf.Max(bounds.size.x,bounds.size.z);
                bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);root.transform.position+=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"/RoadBarricade.prefab");Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static Material Material(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;
        material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};AssetDatabase.CreateAsset(material,path);return material;
    }
}
#endif
