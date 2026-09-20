using System;
using System.IO;
using UnityEngine;

// PlayerPrefs is the campaign authority. The legacy binary is retained but never silently restored.
public static class SaveDataInLocal
{
    public const string SaveKey = "HM.Campaign.v2";
    const string BackupKey = "HM.Campaign.v2.backup";
#if UNITY_EDITOR
    public static string EditorSaveOverride;
#endif
    [Serializable] class Envelope { public int version = 2; public PlayerData player; }
    public static void DataSave(PlayerData player)
    {
        if (player == null) return;
        string json = JsonUtility.ToJson(new Envelope { player = player });
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(EditorSaveOverride))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(EditorSaveOverride)));
            File.WriteAllText(EditorSaveOverride, json); return;
        }
#endif
        var previous = PlayerPrefs.GetString(SaveKey, "");
        if (Decode(previous) != null) PlayerPrefs.SetString(BackupKey, previous);
        PlayerPrefs.SetString(SaveKey, json); PlayerPrefs.Save();
    }
    public static PlayerData DataLoad()
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(EditorSaveOverride))
            return File.Exists(EditorSaveOverride) ? Decode(File.ReadAllText(EditorSaveOverride)) : null;
#endif
        return Decode(PlayerPrefs.GetString(SaveKey, "")) ?? Decode(PlayerPrefs.GetString(BackupKey, ""));
    }
    public static bool HasSavedCampaign()
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(EditorSaveOverride))
            return File.Exists(EditorSaveOverride) && Decode(File.ReadAllText(EditorSaveOverride)) != null;
#endif
        return Decode(PlayerPrefs.GetString(SaveKey, "")) != null
            || Decode(PlayerPrefs.GetString(BackupKey, "")) != null;
    }
    static PlayerData Decode(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var save = JsonUtility.FromJson<Envelope>(json);
            return save != null && save.version == 2 && save.player?.RecruitedAgents != null ? save.player : null;
        }
        catch (ArgumentException) { return null; }
    }
}
