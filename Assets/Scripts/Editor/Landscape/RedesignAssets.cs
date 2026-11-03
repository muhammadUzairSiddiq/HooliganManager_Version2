#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RedesignAssets
{
    public static void Ensure()
    {
        string[] sources={"tent_001","table_001","bench_001"};string[] targets={"Headquarters","SupplyYard","TrainingGround"};
        Directory.CreateDirectory("Assets/Resources/HomeFacilities");
        for(int i=0;i<3;i++)
        {
            string path="Assets/Resources/HomeFacilities/"+targets[i]+".prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path))continue;
            string source="Assets/ithappy/Megacity/Prefabs/Props/"+sources[i]+".prefab";
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(source))continue;
            var content=PrefabUtility.LoadPrefabContents(source);content.name=targets[i];
            foreach(var collider in content.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var body in content.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
            PrefabUtility.SaveAsPrefabAsset(content,path);PrefabUtility.UnloadPrefabContents(content);
        }
        AssetDatabase.SaveAssets();
    }
}
#endif
