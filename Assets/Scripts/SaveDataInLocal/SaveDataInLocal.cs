using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public static class SaveDataInLocal
{
    // Location of data C:\Users\samiz\AppData\LocalLow\DefaultCompany\SoftBall
    public static void DataSave(PlayerData Playerdata)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        string path = Application.persistentDataPath + "/playerdata.sz";
        Stream stream = new FileStream(path, FileMode.Create);

        // PlayerData playerdata = new PlayerData(Playerdata);

        formatter.Serialize(stream,Playerdata);
        Debug.Log("Save player data at "+path);
        stream.Close();
    }

    public static PlayerData DataLoad()
    {
        string path = Application.persistentDataPath + "/playerdata.sz";
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
