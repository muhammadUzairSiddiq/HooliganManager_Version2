using TMPro;
using UnityEngine;

/// <summary>
/// Short, non-blocking feedback rendered inside the 3D city. It deliberately
/// owns no input and never pauses pedestrians or gameplay.
/// </summary>
public sealed class WorldActionFeedback : MonoBehaviour
{
    TextMeshPro label;
    Color baseColor;
    float shownAt;
    float duration;

    public void Setup(TextMeshPro target,float seconds)
    {
        label=target;
        baseColor=label?label.color:Color.white;
        duration=Mathf.Max(.4f,seconds);
        shownAt=Time.unscaledTime;
    }

    void LateUpdate()
    {
        var cam=Camera.main;
        if(cam)transform.rotation=cam.transform.rotation;

        float elapsed=Time.unscaledTime-shownAt;
        transform.position+=Vector3.up*(Time.unscaledDeltaTime*.22f);
        if(label)
        {
            float fade=Mathf.Clamp01((duration-elapsed)/.45f);
            label.color=new Color(baseColor.r,baseColor.g,baseColor.b,fade);
        }
        if(elapsed>=duration)Destroy(gameObject);
    }
}
