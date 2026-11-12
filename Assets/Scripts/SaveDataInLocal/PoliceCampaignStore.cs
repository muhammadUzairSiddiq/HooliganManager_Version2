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
    public int policingVersion,integrity=80,morale=75,acceptedBribes,disciplineDebt,lastDisciplineCost;
    public List<string> bribeReceipts=new List<string>(),reinforcementReceipts=new List<string>();
    public PoliceCrowdOperationSave crowdOperation;
    public PoliceRecoveryVehicleSave recoveryVehicle;
    public int PoliceTutorialVersion,PoliceTutorialStep;
    public bool PoliceTutorialRequired;
    public string PoliceTutorialRoadId;
    public List<string> recoveryDispatchReceipts=new List<string>();
}

[Serializable]
public sealed class PoliceVehicleSave
{
    public Vector3 position,destination;
    public float heading;
    public bool driving;
    public List<string> passengers=new List<string>();
    public List<string> suspectCallIds=new List<string>();
}

[Serializable]
public sealed class PoliceRecoveryVehicleSave
{
    public bool hired,driving;
    public Vector3 position,destination;
    public float heading;
    public string incidentId;
}

[Serializable]
public sealed class PoliceCrowdOperationSave
{
    public string id;
    public Vector3 point;
    public int state; // 0 gathering, 1 clash, 2 controlled, 3 resolved
    public float elapsed,controlProgress;
    public List<string> officerIds=new List<string>();
    public List<int> orderRevisions=new List<int>();
    public int casualties;
    public bool penaltyApplied;
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
    public bool bribeAccepted,bribeDecisionMade,inVehicle;
    public int bribeOffer=180;
    public string resolution;
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
        Ensure(career);
        if(GameCacheBootstrap.RefreshPoliceTutorial(career,GameCacheBootstrap.InstalledVersion))SaveCareer(career);
        return career;
    }
    public static PoliceCampaignData Create()
    {
        var d=new PlayerData{PlayerFaction="Police",ClubName="City Police",ClubShortName="CPD",FirmName="CITY POLICE",PlayerName="Police Command",PrimaryColor="#2878D0",SecondaryColor="#FFFFFF",PoliceHeat=0,Money=2500,Fans=3,CurrentLevel=1,RecoveryVersion=RecoveryProgression.Version,RecoveryGuidedStart=false,LastSessionMode="Police",AcceptedPrivacyPolicy=true};
        d.RecruitedAgents=new List<AgentData>{new AgentData("Officer Morgan",hp:120,speed:5),new AgentData("Officer Ellis",1,hp:120,speed:5),new AgentData("Officer Taylor",2,hp:120,speed:5)};
        d.TutorialAppVersion=GameCacheBootstrap.InstalledVersion;
        var career=new PoliceCampaignData{officers=d,PoliceTutorialRequired=true};Ensure(career);return career;
    }
    public static void Ensure(PoliceCampaignData career)
    {
        career.patrolChecks??=new List<int>();career.calls??=new List<PoliceCallData>();career.completedShiftKeys??=new List<string>();career.positions??=new List<PoliceOfficerSave>();
        career.bribeReceipts??=new List<string>();career.reinforcementReceipts??=new List<string>();
        career.recoveryDispatchReceipts??=new List<string>();
        if(career.policingVersion<1)
        {
            career.policingVersion=1;career.integrity=80;career.morale=75;
            // Preserve existing injury ratios while upgrading the specialist unit.
            foreach(var officer in career.officers.RecruitedAgents)if(officer!=null)
            {
                float ratio=officer.MaxHp>0?Mathf.Clamp01(officer.CurrentHp/officer.MaxHp):0;
                officer.MaxHp=Mathf.Max(160,officer.MaxHp);officer.CurrentHp=officer.MaxHp*ratio;
                officer.Strength=Mathf.Max(18,officer.Strength);
            }
        }
        if(career.calls.Count==0)
        {
            career.calls.Add(new PoliceCallData{id="S"+career.shift+":shop",title="REPORTED SHOPLIFTING",detail="A witness identified a suspect near the shopping street. Assess the report on foot, then detain and escort the identified suspect.",kind=1,location=2});
            career.calls.Add(new PoliceCallData{id="S"+career.shift+":route",title="KEEP THE STATION ROUTE OPEN",detail="Supporters need a clear arrival route. Send a second officer to hold the station approach while your first team handles the report.",kind=2,location=7});
        }
    }
    public static bool AcceptBribe(PoliceCampaignData career,PoliceCallData call)
    {
        if(career==null||call==null||string.IsNullOrEmpty(call.id)||call.state!=1||call.bribeAccepted||call.inVehicle||career.bribeReceipts.Contains(call.id))return false;
        career.bribeReceipts.Add(call.id);call.bribeDecisionMade=true;call.bribeAccepted=true;call.resolution="BRIBED";call.state=4;
        int amount=Mathf.Clamp(call.bribeOffer,80,600);career.officers.Money+=amount;career.acceptedBribes++;career.resolvedCalls++;
        career.integrity=Mathf.Max(0,career.integrity-18);career.trust=Mathf.Max(0,career.trust-8);career.morale=Mathf.Max(0,career.morale-6);
        career.disciplineDebt+=150;return true;
    }
    public static bool ReinforcementEligible(PoliceCampaignData career)=>career!=null&&career.integrity>=55&&career.trust>=55&&career.morale>=35&&
        career.officers.Money>=500&&!career.reinforcementReceipts.Contains("shift-"+career.shift)&&career.officers.RecruitedAgents.Count<6;
    public static bool FinishShift(PoliceCampaignData career)
    {
        if(career==null||career.patrolChecks.Count<3||career.calls.Count==0||career.calls.Exists(c=>c.state<4))return false;
        string key="shift-"+career.shift;
        if(career.completedShiftKeys.Contains(key))return false;
        career.completedShiftKeys.Add(key);career.completedShifts++;career.officers.Money+=750;
        career.lastDisciplineCost=Mathf.Min(career.officers.Money,career.disciplineDebt);career.officers.Money-=career.lastDisciplineCost;career.disciplineDebt-=career.lastDisciplineCost;
        career.trust=Mathf.Clamp(career.trust+(career.integrity<40?-6:5),0,100);career.morale=Mathf.Clamp(career.morale+(career.integrity<40?-8:2),0,100);
        if(!career.bribeReceipts.Exists(r=>r.StartsWith("S"+career.shift+":")))career.integrity=Mathf.Min(100,career.integrity+2);
        return true;
    }
    public static void NextShift(PoliceCampaignData career)
    {
        if(!career.completedShiftKeys.Contains("shift-"+career.shift))return;
        career.shift++;career.patrolChecks.Clear();career.calls.Clear();career.positions.Clear();career.vehicle=null;career.crowdOperation=null;
        career.officers.MatchDay=career.shift;
        if(career.recoveryVehicle!=null){career.recoveryVehicle.driving=false;career.recoveryVehicle.incidentId=null;}
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
