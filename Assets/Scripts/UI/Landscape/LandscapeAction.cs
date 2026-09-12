using UnityEngine;
using UnityEngine.UI;

/// <summary>Serialized navigation binding; remains editable and survives scene/prefab saves.</summary>
[RequireComponent(typeof(Button))]
public sealed class LandscapeAction : MonoBehaviour
{
    public LandscapeFrontEnd target;
    public string action;
    void Awake() => GetComponent<Button>().onClick.AddListener(Invoke);
    public void Invoke() { if (target != null) target.Navigate(action); }
}
