using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class PoliceCampaignData
{
    public int version=1;
    public PlayerData officers;
    public int shift=1, arrests, booked, resolvedCalls, completedShifts, trust=70;
    public List<int> patrolChecks=new List<int>();
    public List<PoliceCallData> calls=new List<PoliceCallData>();
    public List<string> completedShiftKeys=new List<string>();
    public bool dispatchIntroduced;
    public List<PoliceOfficerSave> positions=new List<PoliceOfficerSave>();
    public PoliceVehicleSave vehicle;
}

[Serializable]
public sealed class PoliceVehicleSave
{
    public Vector3 position,destination;
    public float heading;
    public bool driving;
    public List<string> passengers=new List<string>();
}

[Serializable]
public sealed class PoliceOfficerSave
{
    public string id,duty;
    public Vector3 position,destination;
    // Missing fields deserialize as version0 and keep the legacy fallback.
    public int dutyVersion;
    public Vector3 patrolA,patrolB,guardCenter,guardSize,guardPost;
    public bool patrolReturning,rectangularGuard;
}

[Serializable]
public sealed class PoliceCallData
{
    public string id,title,detail;
    // 0 public-order assessment, 1 shoplifting arrest, 2 route stewarding.
    public int kind,location,state;
    public float progress,deadlineSeconds=600;
    public string assignedOfficerId;
    public Vector3 suspectPosition;
    public bool positioned;
}

/// <summary>Police career and officer roster never share the firm campaign key.</summary>
public static class PoliceCampaignStore
{
    public const string Key="HM.PoliceCampaign.v1";
    static string EditorPath
    {
        get
        {
#if UNITY_EDITOR
            if(!string.IsNullOrEmpty(SaveDataInLocal.EditorSaveOverride))return SaveDataInLocal.EditorSaveOverride+".police.json";
#endif
            return null;
        }
    }
    public static bool Exists=>EditorPath!=null?File.Exists(EditorPath):PlayerPrefs.HasKey(Key);
    public static PoliceCampaignData LoadOrCreate()
    {
        string json=EditorPath!=null?(File.Exists(EditorPath)?File.ReadAllText(EditorPath):""):PlayerPrefs.GetString(Key,"");
        PoliceCampaignData career=null;
        try{if(!string.IsNullOrEmpty(json))career=JsonUtility.FromJson<PoliceCampaignData>(json);}catch(ArgumentException){}
        if(career==null||career.version!=1||career.officers==null)career=Create();
        Ensure(career);return career;
    }
    public static PoliceCampaignData Create()
    {
        var d=new PlayerData{PlayerFaction="Police",ClubName="City Police",ClubShortName="CPD",FirmName="CITY POLICE",PlayerName="Police Command",PrimaryColor="#2878D0",SecondaryColor="#FFFFFF",PoliceHeat=0,Money=2500,Fans=3,CurrentLevel=1,RecoveryVersion=RecoveryProgression.Version,RecoveryGuidedStart=false,LastSessionMode="Police",AcceptedPrivacyPolicy=true};
        d.RecruitedAgents=new List<AgentData>{new AgentData("Officer Morgan",hp:120,speed:5),new AgentData("Officer Ellis",1,hp:120,speed:5),new AgentData("Officer Taylor",2,hp:120,speed:5)};
        var career=new PoliceCampaignData{officers=d};Ensure(career);return career;
    }
    public static void Ensure(PoliceCampaignData career)
    {
        career.patrolChecks??=new List<int>();career.calls??=new List<PoliceCallData>();career.completedShiftKeys??=new List<string>();career.positions??=new List<PoliceOfficerSave>();
        if(career.calls.Count==0)
        {
            career.calls.Add(new PoliceCallData{id="S"+career.shift+":shop",title="REPORTED SHOPLIFTING",detail="A witness identified a suspect near the shopping street. Assess the report on foot, then detain and escort the identified suspect.",kind=1,location=2});
            career.calls.Add(new PoliceCallData{id="S"+career.shift+":route",title="KEEP THE STATION ROUTE OPEN",detail="Supporters need a clear arrival route. Send a second officer to hold the station approach while your first team handles the report.",kind=2,location=7});
        }
    }
    public static bool FinishShift(PoliceCampaignData career)
    {
        if(career==null||career.patrolChecks.Count<3||career.calls.Count==0||career.calls.Exists(c=>c.state<4))return false;
        string key="shift-"+career.shift;
        if(career.completedShiftKeys.Contains(key))return false;
        career.completedShiftKeys.Add(key);career.completedShifts++;career.officers.Money+=750;career.trust=Mathf.Min(100,career.trust+5);
        return true;
    }
    public static void NextShift(PoliceCampaignData career)
    {
        if(!career.completedShiftKeys.Contains("shift-"+career.shift))return;
        career.shift++;career.patrolChecks.Clear();career.calls.Clear();career.positions.Clear();career.vehicle=null;
        foreach(var officer in career.officers.RecruitedAgents)if(officer!=null)officer.FullHeal();
        Ensure(career);
    }
    public static void Save(PlayerData projected)
    {
        var career=PoliceCityMode.Career;
        if(career==null)return;
        if(projected!=null)career.officers=projected;
        PoliceCityMode.Instance?.CaptureState();SaveCareer(career);
    }
    public static void SaveCareer(PoliceCampaignData career)
    {
        if(career==null)return;Ensure(career);string json=JsonUtility.ToJson(career);
        if(EditorPath!=null){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(EditorPath)));File.WriteAllText(EditorPath,json);return;}
        PlayerPrefs.SetString(Key,json);PlayerPrefs.Save();
    }
}
