using UnityEngine;
[CreateAssetMenu(menuName="Hooligan/Crowd poses")]
public sealed class CrowdPoseLibrary : ScriptableObject
{
    [System.Serializable] public class Variant { public string name; public Mesh[] poses; public float duration=2f; }
    public Variant[] variants;
}
