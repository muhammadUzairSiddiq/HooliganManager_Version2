using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Selects the lightest existing rig on mobile; preserves authored art and animation.</summary>
public static class CityCharacterBudget
{
    static readonly Dictionary<CharacterPortraitRegistry,int> smallest=new Dictionary<CharacterPortraitRegistry,int>();
    public static int Pick(CharacterPortraitRegistry registry)
    {
        // Offline LOD variants retain variety without instantiating a high-poly rig.
        if(registry.entries.TrueForAll(e=>e.optimizedModelPrefab))return Random.Range(0,registry.entries.Count);
        if(!Application.isMobilePlatform)return Random.Range(0,registry.entries.Count);
        if(smallest.TryGetValue(registry,out int cached))return cached;
        int best=0,bestCount=int.MaxValue;
        for(int i=0;i<registry.entries.Count;i++)
        {
            var prefab=registry.entries[i].modelPrefab;if(!prefab)continue;
            int count=0;
            foreach(var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.sharedMesh)count+=r.sharedMesh.vertexCount;
            foreach(var m in prefab.GetComponentsInChildren<MeshFilter>(true))if(m.sharedMesh)count+=m.sharedMesh.vertexCount;
            if(count>0&&count<bestCount){bestCount=count;best=i;}
        }
        smallest[registry]=best;return best;
    }
    public static void Apply(GameObject model)
    {
        if(!model)return;
        foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            r.updateWhenOffscreen=false;r.quality=SkinQuality.Bone2;
            if(Application.isMobilePlatform){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
        }
    }
}
