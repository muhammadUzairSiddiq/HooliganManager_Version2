using System.Collections.Generic;
using UnityEngine;

/// <summary>Staggered strategic decisions, with real walking and local rival clashes.</summary>
public sealed class LivingRivalDirector : MonoBehaviour
{
    float nextDecision, nextClash;
    int groupIndex;
    GangArea[] groups;
    TerritoryControlPoint[] businesses;
    void Start()
    {
        groups=FindObjectsByType<GangArea>(FindObjectsSortMode.None);
        businesses=FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None);
        nextDecision=Time.time+35;
    }
    void Update()
    {
        var battle=BattleManager.instance;if(!battle)return;
        if(Time.time>=nextClash)
        {
            nextClash=Time.time+2f;int incidents=0;
            var used=new HashSet<string>();
            foreach(var a in battle.EnemyAgents)
            {
                if(!Eligible(a)||a.isHostile||used.Contains(a.firmName))continue;
                foreach(var b in battle.EnemyAgents)
                {
                    if(!Eligible(b)||b.isHostile||a.firmName==b.firmName||used.Contains(b.firmName))continue;
                    if((a.transform.position-b.transform.position).sqrMagnitude>100)continue;
                    a.isHostile=b.isHostile=true;a.AlertToTarget(b);b.AlertToTarget(a);
                    used.Add(a.firmName);used.Add(b.firmName);incidents++;break;
                }
                if(incidents>=3)break;
            }
        }
        if(Time.time<nextDecision||groups==null||groups.Length<2)return;
        nextDecision=Time.time+18;
        int index=groupIndex++%groups.Length;var area=groups[index];if(!area)return;
        var other=groups[(index+1)%groups.Length];if(!other)return;
        Vector3 destination=(area.transform.position+other.transform.position)*.5f;
        foreach(var business in businesses)
            if(business&&business.IsCaptured&&(business.transform.position-area.transform.position).sqrMagnitude<140*140){destination=business.transform.position;break;}
        int count=0;
        foreach(var member in battle.EnemyAgents)
        {
            if(!Eligible(member)||member.firmName!=area.GangName||member.isHostile)continue;
            if(member.WalkToRally(destination))count++;
            if(count>=3)break;
        }
    }
    static bool Eligible(EnemyController member)=>member&&member.IsAlive&&member.gameObject.activeInHierarchy&&!member.IsAmbientMatchdayUnit&&member.firmName!="POLICE"&&!RivalSettlement.IsSettled(member.firmName);
}
