using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public static class SaveDataInLocal
{
#if UNITY_EDITOR
    // The UI smoke harness uses a separate save path and restores the in-memory campaign.
    public static string EditorSaveOverride;
#endif
    private static string SavePath
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorSaveOverride)) return EditorSaveOverride;
#endif
            return Application.persistentDataPath + "/playerdata.sz";
        }
    }
    // Location of data C:\Users\samiz\AppData\LocalLow\DefaultCompany\SoftBall
    public static void DataSave(PlayerData Playerdata)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        string path = SavePath;
        Stream stream = new FileStream(path, FileMode.Create);

        // PlayerData playerdata = new PlayerData(Playerdata);

        formatter.Serialize(stream,Playerdata);
        Debug.Log("Save player data at "+path);
        stream.Close();
    }

    public static PlayerData DataLoad()
    {
        string path = SavePath;
        Debug.Log("Load player data from "+path);
        if (File.Exists(path))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            Stream stream = new FileStream(path, FileMode.Open);
            try
            {
                PlayerData Playerdata = formatter.Deserialize(stream) as PlayerData;
                stream.Close();
                return Playerdata;
            }
            catch { Debug.Log("Data was corrupted or tempered with.");}
            stream.Close();

            return null;
        }
        else
        {
            Debug.Log("No Data found.");
            return null;
        }
    }
}
