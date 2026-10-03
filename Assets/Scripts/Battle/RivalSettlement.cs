using System.Linq;
using UnityEngine;
public static class RivalSettlement
{
    public static bool IsSettled(string firm)=>GameManager.Data?.SettledRivalFirms?.Contains(firm)==true;
    public static void Break(string firm){GameManager.Data?.SettledRivalFirms?.Remove(firm);}
    public static void Offer(Transform anchor)
    {
        var selection=AgentSelectionManager.instance;var battle=BattleManager.instance;
        if(!battle||!selection||selection.SelectedAgents.Count==0)return;
        var member=selection.SelectedAgents.FirstOrDefault(a=>a&&a.IsAlive);if(!member)return;
        var rival=battle.EnemyAgents.Where(e=>e&&e.IsAlive&&e.isHostile&&e.firmName!="POLICE"&&!e.IsHomeMatchdaySupporter)
            .OrderBy(e=>(e.transform.position-member.transform.position).sqrMagnitude).FirstOrDefault();
        if(!rival||(rival.transform.position-member.transform.position).sqrMagnitude>80*80){CityGameplay.Instance?.PostEvent("No nearby rival dispute");return;}
        string firm=rival.firmName;int cost=Price(firm);
        WorldChoiceBar.Present(anchor,"TRUCE · £"+cost.ToString("N0"),
            ("PAY",LandscapeUI.Gold,()=>Pay(firm,cost)),("CANCEL",LandscapeUI.PanelColor,()=>{}));
    }
    public static int Price(string firm)=>Mathf.Max(150,100*(BattleManager.instance?.EnemyAgents.Count(e=>e&&e.IsAlive&&e.firmName==firm)??0));
    public static bool Pay(string firm,int quotedCost)
    {
        var data=GameManager.Data;var battle=BattleManager.instance;
        if(data==null||!battle||firm=="POLICE"||IsSettled(firm))return false;
        if(!battle.EnemyAgents.Any(e=>e&&e.IsAlive&&e.firmName==firm))return false;
        if(quotedCost!=Price(firm))return false;
        if(data.Money<quotedCost){CityGameplay.Instance?.PostEvent("Need £"+quotedCost.ToString("N0"));return false;}
        data.Money-=quotedCost;data.SettledRivalFirms??=new System.Collections.Generic.List<string>();data.SettledRivalFirms.Add(firm);
        foreach(var enemy in battle.EnemyAgents)if(enemy&&enemy.firmName==firm)enemy.StandDown();
        foreach(var agent in battle.PlayerAgents)if(agent&&agent.IsTargetingFirm(firm))agent.CommandRetreat();
        GameManager.Save();CityGameplay.Instance?.PostEvent("TRUCE · "+firm+" stood down");return true;
    }
}
