using System;
using UnityEngine;

/// <summary>Only one deliberate management sheet owns the city at a time.</summary>
public static class CityUiSheets
{
    static UnityEngine.Object owner;
    static Action close;
    public static void Open(UnityEngine.Object next,Action dismiss)
    {
        if(owner&&owner!=next){var previous=close;owner=null;close=null;previous?.Invoke();}
        WorldChoiceBar.DismissCurrent();
        owner=next;close=dismiss;
    }
    public static void CloseCurrent()
    {var previous=close;owner=null;close=null;previous?.Invoke();}
}
