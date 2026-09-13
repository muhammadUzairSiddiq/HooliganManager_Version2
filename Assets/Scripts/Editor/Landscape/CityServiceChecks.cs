#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class CityServiceChecks
{
    public static void Run()
    {
        if(!Application.isPlaying || !BattleManager.instance.BattleActive)throw new InvalidOperationException("Wait for the city intro to finish before service checks.");
        var city=CityGameplay.Instance;var bm=BattleManager.instance;var original=GameManager.Data;
        var originalPath=SaveDataInLocal.EditorSaveOverride;
        var originalAgents=bm.PlayerAgents.Where(a=>a).ToArray();
        var positions=originalAgents.Select(a=>a.transform.position).ToArray();
        var health=originalAgents.Select(a=>a.CurrentHp).ToArray();
        var strength=originalAgents.Select(a=>a.Data.Strength).ToArray();
        var max=bm.maxPlayerAgents;
        var rows=new List<string>();
        void Check(bool value,string label){rows.Add((value?"PASS ":"FAIL ")+label);}
        var service=typeof(CityGameplay).GetMethod("Service",BindingFlags.Instance|BindingFlags.NonPublic);
        void RunService(int index,int cost)=>service.Invoke(city,new object[]{index,cost});
        void Warp(int index){foreach(var a in originalAgents.Where(a=>a.IsAlive))a.GetComponent<NavMeshAgent>().Warp(city.Locations[index]);}
        Directory.CreateDirectory("Artifacts/CityQA");
        try
        {
            SaveDataInLocal.EditorSaveOverride=Path.GetFullPath("Artifacts/CityQA/service-test-save.sz");
            var d=JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(original));GameData.instance.PlayerData=d;
            d.Money=10000;d.FanMorale=60;d.HomeTrainingLevel=0;
            Warp(8);RunService(1,100);
            Check(d.Money==10000 && d.FanMorale==60,"Remote pub service rejects without charging");
            Warp(1);RunService(1,100);
            Check(d.Money==9900 && d.FanMorale==70,"Pub rally charges £100 and raises morale");
            d.Money=0;RunService(1,100);
            Check(d.Money==0 && d.FanMorale==70,"Insufficient funds reject service");
            d.Money=10000;Warp(3);RunService(3,300);
            Check(d.HomeTrainingLevel==1 && originalAgents[0].Data.Strength==strength[0]+2,"Training applies strength upgrade");
            d.HomeTrainingLevel=5;int cash=d.Money;RunService(3,300);
            Check(d.Money==cash,"Training level cap does not charge");
            Warp(4);
            var hp=typeof(AgentController).GetProperty("CurrentHp");hp.SetValue(originalAgents[0],Mathf.Max(1,originalAgents[0].Data.MaxHp-10));
            RunService(4,200);Check(originalAgents[0].CurrentHp==originalAgents[0].Data.MaxHp && d.Money==cash-200,"Recovery heals runtime squad and charges once");
            cash=d.Money;RunService(4,200);Check(d.Money==cash,"Healthy squad recovery does not charge");
            Warp(2);cash=d.Money;int pending=d.PendingFansGain;
            typeof(CityGameplay).GetMethod("QueueFans",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(city,null);
            Check(d.PendingFansGain==pending+2 && d.Money==cash-300,"City recruitment queue advances matchday objective");
            int count=bm.PlayerAgents.Count(a=>a&&a.IsAlive);bm.maxPlayerAgents=count;cash=d.Money;
            RunService(2,500);Check(d.Money==cash,"Full squad rejects recruitment without charging");
            bm.maxPlayerAgents=count+1;RunService(2,500);
            Check(bm.PlayerAgents.Count(a=>a&&a.IsAlive)==count+1 && d.Money==cash-500,"Recruitment creates a live animated squad member");
            var disk=SaveDataInLocal.DataLoad();
            Check(disk!=null && disk.PendingFansGain==d.PendingFansGain && disk.RecruitedAgents.Count==d.RecruitedAgents.Count,"Real save serialization preserves recruitment");
        }
        finally
        {
            File.WriteAllLines("Artifacts/CityQA/service-checks.txt",rows);
            AgentSelectionManager.instance.DeselectAll();
            var list=(List<AgentController>)typeof(BattleManager).GetField("_playerAgents",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bm);
            foreach(var a in list.Where(a=>a&&!originalAgents.Contains(a)).ToArray()){list.Remove(a);UnityEngine.Object.DestroyImmediate(a.gameObject);}
            GameData.instance.PlayerData=original;SaveDataInLocal.EditorSaveOverride=originalPath;bm.maxPlayerAgents=max;
            for(int i=0;i<originalAgents.Length;i++)
            {
                var a=originalAgents[i];a.Data.Strength=strength[i];typeof(AgentController).GetProperty("CurrentHp").SetValue(a,health[i]);a.SyncHpToData();
                a.GetComponent<NavMeshAgent>().Warp(positions[i]);a.CommandMoveTo(positions[i]);
            }
            AgentSelectionManager.instance.SelectAll();CameraPanTouchOnly.Instance.CenterOnSelection();
            File.WriteAllLines("Artifacts/CityQA/service-checks.txt",rows);
        }
        if(rows.Any(r=>r.StartsWith("FAIL")))throw new Exception("City service failures; see service-checks.txt");
    }
}
#endif
