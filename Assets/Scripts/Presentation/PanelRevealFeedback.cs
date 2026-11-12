using UnityEngine;

/// <summary>Short opacity reveal; panel position and interactive geometry never move.</summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class PanelRevealFeedback : MonoBehaviour
{
    CanvasGroup group;
    float age;
    bool EnsureGroup()
    {
        // Unity's missing/destroyed component wrappers are not CLR null. Use its
        // object check before reusing the cache or adding the required component.
        if(!group)group=GetComponent<CanvasGroup>();
        if(!group)group=gameObject.AddComponent<CanvasGroup>();
        return group;
    }
    void Awake() { EnsureGroup(); }
    void OnEnable() { age = 0; if (EnsureGroup()) group.alpha = GameFeedback.ReducedMotion ? 1 : .72f; }
    void Update()
    {
        if(!EnsureGroup())return;
        age += Time.unscaledDeltaTime;
        group.alpha = GameFeedback.ReducedMotion ? 1 : Mathf.Lerp(.72f, 1, Mathf.Clamp01(age / .13f));
    }
}
