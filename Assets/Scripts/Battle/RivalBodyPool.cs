using System.Collections.Generic;
using UnityEngine;

/// <summary>Reuses deactivated rival bodies so growing firms do not allocate without limit.</summary>
public static class RivalBodyPool
{
    static readonly Queue<GameObject> spare = new Queue<GameObject>();

    public static GameObject Take(GameObject prefab, Vector3 position, Quaternion rotation)
    {
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
        if (!go || spare.Contains(go)) return;
        go.SetActive(false);
        if(spare.Count>=4){Object.Destroy(go);return;}
        spare.Enqueue(go);
    }
}
