using UnityEngine;
public static class CrewMovementPreference
{
    public const string RunKey="Crew.RunOnTap", PaceKey="Crew.TravelPace.v1";
    static bool loaded,run;
    static float pace;
    static void Load()
    {
        if(loaded)return;loaded=true;
        // The earlier walk-only implementation wrote 0 even when RUN was chosen.
        // Migrate that non-functional setting once; subsequent real choices persist.
        if(!PlayerPrefs.HasKey(PaceKey)){PlayerPrefs.SetFloat(PaceKey,1.65f);PlayerPrefs.SetInt(RunKey,1);PlayerPrefs.Save();}
        run=PlayerPrefs.GetInt(RunKey,1)!=0;pace=Mathf.Clamp(PlayerPrefs.GetFloat(PaceKey,1.65f),1f,2.2f);
    }
    public static bool RunOnTap { get{Load();return run;} set{Load();run=value;PlayerPrefs.SetInt(RunKey,value?1:0);PlayerPrefs.Save();} }
    public static float SpeedMultiplier { get{Load();return pace;} set{Load();pace=Mathf.Clamp(value,1f,2.2f);PlayerPrefs.SetFloat(PaceKey,pace);PlayerPrefs.Save();} }
    public static string PaceLabel=>SpeedMultiplier<1.3f?"STANDARD":SpeedMultiplier<1.9f?"BRISK":"FAST";
    public static void ReloadPreferences(){loaded=false;}
}
