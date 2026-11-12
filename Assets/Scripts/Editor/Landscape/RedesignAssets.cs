#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Versioned resource wrappers built from this project's real low-poly city artwork.</summary>
public static class RedesignAssets
{
    const string Marker="HomeFacilityModelV2";
    [MenuItem("Hooligan/Recovery/Prepare visible Home facility models")]
    public static void Ensure()
    {
        CityIncidentAssets.Ensure();
        Directory.CreateDirectory("Assets/Resources/Presentation");
        if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Presentation/CrewRoute.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")){color=Color.white},"Assets/Resources/Presentation/CrewRoute.mat");
        string[] sources={
            "Assets/ithappy/Megacity/Prefabs/Buildings/Government/government_bilding_001.prefab",
            "Assets/ithappy/Megacity/Prefabs/Buildings/Shopping/tool_store_001.prefab",
            "Assets/ithappy/Megacity/Prefabs/Buildings/Shopping/gym_001.prefab"};
        string[] targets={"Headquarters","SupplyYard","TrainingGround"};
        Directory.CreateDirectory("Assets/Resources/HomeFacilities");
        for(int i=0;i<3;i++)
        {
            string path="Assets/Resources/HomeFacilities/"+targets[i]+".prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(existing&&existing.transform.Find(Marker))continue;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sources[i]);
            if(!source)throw new FileNotFoundException("Required existing Home building model is missing.",sources[i]);
            var preview=EditorSceneManager.NewPreviewScene();
            var root=new GameObject(targets[i]);SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                new GameObject(Marker).transform.SetParent(root.transform,false);
                var body=Object.Instantiate(source,root.transform,false);body.name="Facility building body";
                Normalize(body,i==1?9.5f:10.5f);
                if(i==0)
                {
                    AddProp(root,"bench_001","HQ meeting bench",new Vector3(-2.8f,0,-6),2.3f);
                    AddProp(root,"table_001","HQ planning table",new Vector3(2.4f,0,-6),2.3f);
                }
                else if(i==1)
                {
                    AddProp(root,"table_001","Supply sorting tables",new Vector3(-2.6f,0,-6),2.6f);
                    for(int crate=0;crate<3;crate++)Block(root,"Supply stock crate",new Vector3(1.3f+crate*.95f,.46f,-6),new Vector3(.85f,.9f,.85f),new Color(.52f,.35f,.18f));
                }
                else
                {
                    AddProp(root,"bench_001","Training rest bench",new Vector3(-2.5f,0,-6),2.5f);
                    AddProp(root,"bike_parking_001","Training equipment",new Vector3(2.5f,0,-6),2.5f);
                }
                Block(root,"Home identity mast",new Vector3(5.3f,2.3f,-4.8f),new Vector3(.12f,4.6f,.12f),new Color(.15f,.23f,.23f));
                Block(root,"Home identity flag",new Vector3(6,4,-4.8f),new Vector3(1.4f,.95f,.08f),new Color(.1f,.7f,.32f));
                foreach(var collider in root.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                foreach(var rigidbody in root.GetComponentsInChildren<Rigidbody>(true)){rigidbody.isKinematic=true;rigidbody.useGravity=false;}
                foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {renderer.enabled=true;renderer.forceRenderingOff=false;renderer.renderingLayerMask=1;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;}
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
        }
        EnsureTradeStall();AssetDatabase.SaveAssets();
    }
    static void EnsureTradeStall()
    {
        const string path="Assets/Resources/HomeFacilities/TradeStall.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path))return;
        var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject("TradeStall");SceneManager.MoveGameObjectToScene(root,preview);
        try
        {
            AddProp(root,"tent_001","Trade canopy",Vector3.zero,4.3f);
            AddProp(root,"table_001","Serving counter",new Vector3(0,0,-1.3f),2.6f);
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
    }
    static void AddProp(GameObject root,string key,string name,Vector3 position,float footprint)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Megacity/Prefabs/Props/"+key+".prefab");
        if(!source)return;var prop=Object.Instantiate(source,root.transform,false);prop.name=name;Normalize(prop,footprint);prop.transform.localPosition+=position;
    }
    static void Normalize(GameObject model,float footprint)
    {
        var renderers=model.GetComponentsInChildren<MeshRenderer>(true);if(renderers.Length==0)throw new InvalidDataException("Home model has no real mesh: "+model.name);
        var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        float span=Mathf.Max(bounds.size.x,bounds.size.z);if(span<.001f)throw new InvalidDataException("Home model has empty bounds: "+model.name);
        model.transform.localScale*=footprint/span;
        bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
    }
    static void Block(GameObject root,string name,Vector3 position,Vector3 scale,Color color)
    {
        var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name=name;block.transform.SetParent(root.transform,false);block.transform.localPosition=position;block.transform.localScale=scale;
        Object.DestroyImmediate(block.GetComponent<Collider>());
        var renderer=block.GetComponent<MeshRenderer>();
        string materialPath="Assets/Resources/HomeFacilities/"+(name=="Supply stock crate"?"StockCrate":name=="Home identity mast"?"FlagMast":"HomeFlag")+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){color=color};AssetDatabase.CreateAsset(material,materialPath);}
        renderer.sharedMaterial=material;
    }
}
#endif
