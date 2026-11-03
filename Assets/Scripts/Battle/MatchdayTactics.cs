using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MatchdayTacticsState
{
    public int day,plan;
    public bool planCommitted,escortComplete,stallBuilt;
    public float escortSeconds,stallIncomeSeconds,stallWorkSeconds;
    public string escortOfficerId,stallBuilderId;
    public bool stallBuildRequested;
    public Vector3 escortPosition;
    public bool escortPositioned;
    public List<int> supplySupport=new List<int>();
}

/// <summary>Saved, explicit matchday tradeoffs. No passive chapter completion.</summary>
public static class MatchdayTactics
{
    public static readonly string[] Names={"BALANCED COVERAGE","PROTECT THE ROUTES","MATCHDAY TRADE"};
    public static MatchdayTacticsState Ensure(PlayerData d)
    {
        if(d==null)return null;
        if(d.MatchdayTactics==null||d.MatchdayTactics.day!=d.MatchDay)d.MatchdayTactics=new MatchdayTacticsState{day=d.MatchDay};
        d.MatchdayTactics.supplySupport??=new List<int>();return d.MatchdayTactics;
    }
    public static int CashCost(int plan)=>plan==0?0:plan==1?200:150;
    public static int SupplyCost(int plan)=>plan==0?0:2;
    public static string Description(int plan)=>plan==1?"£200 + 2 supplies. Steward equipment cuts each hold to 15s. Missed coverage adds no heat.":plan==2?"£150 + 2 supplies. Pub needs TWO crew, pays £225 on coverage. Staff a built stall for extra trade.":"No upfront cost. One crew per location, 20s coverage, £100 each. Keep a spare member for escort or trade.";
    public static bool Choose(PlayerData d,int plan)
    {
        var s=Ensure(d);if(s==null||plan<0||plan>2||s.planCommitted)return false;s.plan=plan;return true;
    }
    public static bool CanCommit(PlayerData d,out string reason)
    {
        reason="";var s=Ensure(d);if(s==null){reason="Campaign unavailable.";return false;}
        if(s.planCommitted)return true;
        if(d.Money<CashCost(s.plan)||d.CitySupplies<SupplyCost(s.plan)){reason=$"{Names[s.plan]} needs £{CashCost(s.plan)} and {SupplyCost(s.plan)} supplies. Choose Balanced for no cost.";return false;}return true;
    }
    public static bool Commit(PlayerData d)
    {
        if(!CanCommit(d,out _))return false;var s=Ensure(d);if(s.planCommitted)return true;
        d.Money-=CashCost(s.plan);d.CitySupplies-=SupplyCost(s.plan);s.planCommitted=true;return true;
    }
    public static int CrewRequired(PlayerData d,int response)=>Ensure(d)?.plan==2&&response==1?2:1;
    public static float HoldSeconds(PlayerData d,int response)=>Mathf.Max(10,(Ensure(d)?.plan==1?15:20)-(Ensure(d)?.supplySupport.Contains(response)==true?5:0));
    public static int Reward(PlayerData d,int response)=>Ensure(d)?.plan==2&&response==1?225:100;
    public static bool Support(PlayerData d,int response)
    {
        var s=Ensure(d);if(s==null||response<0||response>1||!s.planCommitted||s.supplySupport.Contains(response)||d.CitySupplies<2)return false;
        d.CitySupplies-=2;s.supplySupport.Add(response);return true;
    }
}
