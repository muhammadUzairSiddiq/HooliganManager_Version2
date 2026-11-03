using System.Collections.Generic;
using UnityEngine;

/// <summary>Show the living city by default; map annotations are a deliberate layer.</summary>
public sealed class FocusedWorldPresentation : MonoBehaviour
{
    readonly List<GameObject> rings=new List<GameObject>();
    float next;
    void Update()
    {
        if(Time.unscaledTime<next||!ModernCityHud.Instance)return;next=Time.unscaledTime+.4f;
        rings.RemoveAll(r=>!r);
        if(rings.Count==0)
        foreach(var zone in FindObjectsByType<GangArea>(FindObjectsSortMode.None))
        {var ring=zone.transform.Find("ZoneRing");if(ring)rings.Add(ring.gameObject);}
        foreach(var ring in rings)ring.SetActive(ModernCityHud.InspectWorld);
    }
    void OnDisable(){foreach(var ring in rings)if(ring)ring.SetActive(true);}
}
