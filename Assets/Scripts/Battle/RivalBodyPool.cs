using System.Collections.Generic;
using UnityEngine;

/// <summary>Reuses deactivated rival bodies so growing firms do not allocate without limit.</summary>
public static class RivalBodyPool
{
    static readonly Dictionary<string,Queue<GameObject>> pools = new Dictionary<string,Queue<GameObject>>();
    public static int PooledCount{get{int count=0;foreach(var pool in pools.Values)foreach(var body in pool)if(body)count++;return count;}}
    static Queue<GameObject> Pool(string key){if(!pools.TryGetValue(key,out var pool)){pool=new Queue<GameObject>();pools[key]=pool;}return pool;}

    public static GameObject Take(GameObject prefab, Vector3 position, Quaternion rotation,string role="gang")
    {
        var spare=Pool(role);
        while (spare.Count > 0)
        {
            var go = spare.Dequeue();
            if (!go) continue;
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            var behaviour = go.GetComponent<EnemyController>();
            if (behaviour) behaviour.StopAllCoroutines();
            return go;
        }
        return prefab ? Object.Instantiate(prefab, position, rotation) : null;
    }

    public static void Release(GameObject go)
    {
        if(!go)return;
        var unit=go.GetComponent<EnemyController>();
        var spare=Pool(unit&&unit.firmName=="POLICE"?"police":unit&&unit.IsAmbientMatchdayUnit?"supporter":"gang");
        if (!go || spare.Contains(go)) return;
        go.SetActive(false);
        if(spare.Count>=(Application.isMobilePlatform?12:16)){Object.Destroy(go);return;}
        spare.Enqueue(go);
    }
}
