using UnityEngine;
public static class CrewMovementPreference
{
    // Retained for save/API compatibility; movement now has one authored gait.
    public static bool RunOnTap { get=>false; set {PlayerPrefs.SetInt("Crew.RunOnTap",0);PlayerPrefs.Save();} }
}
