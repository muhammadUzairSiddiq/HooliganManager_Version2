using UnityEngine;
using UnityEngine.UI;

/// <summary>Stable tutorial identity, independent of translated captions and scroll order.</summary>
public sealed class TutorialAnchor : MonoBehaviour
{
    public string Id;
    public static Button Tag(Button button,string id)
    {
        var anchor=button.GetComponent<TutorialAnchor>();
        if(!anchor)anchor=button.gameObject.AddComponent<TutorialAnchor>();
        anchor.Id=id;return button;
    }
    public static Button Find(string id)
    {
        foreach(var anchor in Object.FindObjectsByType<TutorialAnchor>(FindObjectsSortMode.None))
            if(anchor.Id==id&&anchor.gameObject.activeInHierarchy)return anchor.GetComponent<Button>();
        return null;
    }
}
