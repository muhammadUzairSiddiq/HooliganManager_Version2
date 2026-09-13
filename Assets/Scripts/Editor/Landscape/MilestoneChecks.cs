#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MilestoneChecks
{
    public static void Run()
    {
        var rows=new List<string>();
        void Check(bool value,string label){rows.Add((value?"PASS ":"FAIL ")+label);}
        var d=new PlayerData {Money=0,FanMorale=0,Reputation=0,CurrentLevel=1,LandscapeClaimedMissions=null,SelectedAwayAgentIds=new List<string>()};
        Check(!FirmMissions.Claim(d,"crew5") && d.Money==0,"Incomplete mission cannot pay");
        Check(!FirmMissions.Claim(d,"unknown"),"Unknown mission cannot pay");
        for(int i=0;i<5;i++)d.RecruitedAgents.Add(new AgentData("Test "+i));
        d.SelectedAwayAgentIds.Add(d.RecruitedAgents[0].AgentId);
        Check(d.SelectedAwayAgentIds.Count==1,"Away trip deployment selection saves member ids");
        d.RecruitedAgents[0].CurrentHp=0;
        Check(FirmMissions.Get(d,0)[0].progress==4,"Crew objective excludes fallen members");
        d.RecruitedAgents[0].FullHeal();
        Check(FirmMissions.Claim(d,"crew5") && d.Money==1000,"Crew goal pays exactly the configured reward");
        Check(!FirmMissions.Claim(d,"crew5") && d.Money==1000,"Duplicate claim blocked across screens");
        d.BattleWins=3;d.Reputation=30;d.CurrentLevel=5;d.HomeDefenceCompleted=true;d.HomeTrainingLevel=1;d.LastTrainingMatchday=d.MatchDay;d.LastAwayTripMatchday=d.MatchDay;
        d.CityCapturedZones=new List<string>{"Local Pub","Training Yard","Stadium Approach"};
        d.DestinationVisitCounts.Set("East Docks",1);d.DestinationVisitCounts.Set("North End",1);d.DestinationVisitCounts.Set("Riverside",1);d.DestinationVisitCounts.Set("Old Town",1);
        foreach(var mission in FirmMissions.Get(d,0).Skip(1))Check(FirmMissions.Claim(d,mission.id),"Campaign claim "+mission.id);
        Check(!FirmMissions.ClaimBonus(d),"Bonus locked until daily claims complete");
        d.PendingFansGain=2;d.FanMorale=75;d.Money=6000;
        foreach(var mission in FirmMissions.Get(d,1))Check(FirmMissions.Claim(d,mission.id),"Matchday claim "+mission.id);
        Check(FirmMissions.Get(d,1).Length==5,"Five matchday missions are available");
        Check(FirmMissions.ClaimBonus(d) && d.Money==8100,"Daily rewards and bonus total £2,100");
        Check(!FirmMissions.ClaimBonus(d) && d.Money==8100,"Duplicate bonus blocked");
        var saved=JsonUtility.ToJson(d);var restored=JsonUtility.FromJson<PlayerData>(saved);
        Check(!FirmMissions.Claim(restored,"crew5") && !FirmMissions.ClaimBonus(restored),"Save roundtrip preserves claims and bonus");
        restored.MatchDay++;
        Check(FirmMissions.Get(restored,1).All(m=>!FirmMissions.Claimed(restored,m.id)),"New matchday has distinct objectives");
        Check(Resources.Load<Texture2D>("CityPresentation/CityLoading"),"Loading artwork packaged in Resources");
        Check(Resources.Load<Texture2D>("CityPresentation/CityLoading_Riverside"),"Riverside loading artwork packaged in Resources");
        Check(Resources.Load<Texture2D>("CityPresentation/CityLoading_OldTown"),"Old Town loading artwork packaged in Resources");
        if(Application.isPlaying)
        {
            var city=CityGameplay.Instance;Check(city && city.Locations!=null,"Live city initialized");
            if(city && city.Locations!=null)
            {
                foreach(var pair in city.Locations.Select((p,i)=>(p,i)))
                {
                    var path=new NavMeshPath();
                    Check(NavMesh.CalculatePath(city.Home,pair.p,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Reachable "+city.LocationNames[pair.i]);
                }
            }
            Check(BattleManager.instance,"Battle manager live");
            var agents=BattleManager.instance ? BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray() : Array.Empty<AgentController>();
            Check(agents.Length>0 && agents.All(a=>a.GetComponent<NavMeshAgent>().isOnNavMesh),"Living squad on NavMesh");
            Check(agents.All(a=>a.GetComponentsInChildren<Animator>().Any(x=>x.runtimeAnimatorController)),"Squad has animated character models");
            Check(UnityEngine.Object.FindFirstObjectByType<CityMissionHUD>(),"Gameplay mission HUD exists");
            var camera=CameraPanTouchOnly.Instance;
            Check(camera && camera.enabled,"Gameplay tactical camera is active");
            AgentSelectionManager.instance?.SelectAll();
            camera?.CenterOnSelection();
            Check(camera && camera.IsFollowingSelection,"Recenter enables camera follow");
            if(city && city.Locations!=null && city.Locations.Length>1)
            {
                AgentSelectionManager.instance?.CommandSelectedMoveTo(city.Locations[1]);
                Check(camera && camera.IsFollowingSelection,"Move command keeps camera follow active");
            }
            var es=EventSystem.current;var hits=new List<RaycastResult>();
            if(es)es.RaycastAll(new PointerEventData(es){position=new Vector2(Screen.width*.55f,Screen.height*.48f)},hits);
            Check(es && hits.Count==0,"Open world gesture area is not blocked by HUD "+string.Join(",",hits.Select(h=>h.gameObject.name)));
            Check(GameObject.Find("Capture")?.GetComponent<Button>()?.interactable==true,"Capture command is enabled");
        }
        else rows.Add("NOT RUN live checks: enter Gameplay play mode first");
        Directory.CreateDirectory("Artifacts/CityQA");
        File.WriteAllLines("Artifacts/CityQA/milestone-checks.txt",rows);
        if(rows.Any(r=>r.StartsWith("FAIL")))throw new Exception("Milestone check failures; see Artifacts/CityQA/milestone-checks.txt");
    }
}
#endif
