using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using JetBrains.Annotations;

public class MapController : MonoBehaviour
{
    public enum MapId
    {
        Map,
        Map1,
        Map2,
        Map3,
        Map4
    }

    public IEnumerator SpawnMap(MapId mapId)
    {
        yield return SceneManager.LoadSceneAsync(mapId.ToString(), LoadSceneMode.Additive);
    }
}
