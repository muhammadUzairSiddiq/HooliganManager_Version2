using UnityEngine;
using UnityEngine.SceneManagement;
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

    [Tooltip("Gameplay now owns the upgraded environment directly. Leave this on so old additive maps are not loaded over it.")]
    public bool useActiveGameplayEnvironment = true;

    public IEnumerator SpawnMap(MapId mapId)
    {
        if (useActiveGameplayEnvironment && SceneManager.GetActiveScene().name == GameManager.SCENE_BATTLE)
            yield break;

        yield return SceneManager.LoadSceneAsync(mapId.ToString(), LoadSceneMode.Additive);
    }
}
