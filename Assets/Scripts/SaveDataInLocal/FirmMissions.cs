using System.Collections.Generic;
using System.Linq;

/// <summary>One mission ledger shared by management and the live city. Claims are idempotent.</summary>
public static class FirmMissions
{
    public struct Mission
    {
        public string id, title, body;
        public int progress, target, reward, location;
        public Mission(string key,string name,string description,int value,int goal,int cash,int district=0)
        { id=key;title=name;body=description;progress=value;target=goal;reward=cash;location=district; }
    }
    public static Mission[] Get(PlayerData d,int tab)
    {
        if(d==null)return new Mission[0];
        if(tab==1)return new[] {
            new Mission("day"+d.MatchDay+"crew","BRING IN NEW BLOOD","Queue two fans at Recruitment for next matchday.",d.PendingFansGain,2,200,2),
            new Mission("day"+d.MatchDay+"morale","KEEP THE FAITH","Rally at the pub. Maintain at least 75 morale.",d.FanMorale,75,250,1),
            new Mission("day"+d.MatchDay+"funds","BUILD A WAR CHEST","Keep £6,000 available for your next trip.",d.Money,6000,300,1),
            new Mission("day"+d.MatchDay+"training","SHARPEN THE CREW","Use the training area at least once this matchday.",d.LastTrainingMatchday==d.MatchDay?1:0,1,300,3),
            new Mission("day"+d.MatchDay+"travel","KEEP THE CALENDAR MOVING","Complete at least one away-trip departure this matchday.",d.LastAwayTripMatchday==d.MatchDay?1:0,1,300,0) };
        CampaignMissions.Ensure(d);
        return Enumerable.Range(1,CampaignMissions.MissionCount).Select(number=>
        {
            var campaign=CampaignMissions.Get(number);var p=CampaignMissions.Progress(d,number);
            string state=CampaignMissions.Completed(d,number)?"COMPLETE":number==d.CurrentLevel?"ACTIVE":number>d.CurrentLevel?"LOCKED":"COMPLETE";
            return new Mission("campaign_"+number,$"MISSION {number:00} · {campaign.title}",campaign.briefing+"  ["+state+"]",p.complete,p.total,campaign.reward,0);
        }).ToArray();
    }
    public static bool Claimed(PlayerData d,string id)
    {
        if(id!=null&&id.StartsWith("campaign_")&&int.TryParse(id.Substring(9),out int mission))return CampaignMissions.Completed(d,mission);
        return d.LandscapeClaimedMissions?.Contains(id)==true;
    }
    public static bool Claim(PlayerData d,string id)
    {
        if(d==null || Claimed(d,id)||id.StartsWith("campaign_"))return false;
        foreach(var m in Get(d,0).Concat(Get(d,1)))
        {
            if(m.id!=id || m.progress<m.target)continue;
            if(d.LandscapeClaimedMissions==null)d.LandscapeClaimedMissions=new List<string>();
            d.LandscapeClaimedMissions.Add(id);d.Money+=m.reward;return true;
        }
        return false;
    }
    public static bool BonusReady(PlayerData d)=>d!=null && d.LandscapeBonusMatchday!=d.MatchDay && Get(d,1).All(m=>Claimed(d,m.id));
    public static bool ClaimBonus(PlayerData d)
    { if(!BonusReady(d))return false;d.LandscapeBonusMatchday=d.MatchDay;d.Money+=750;return true; }
}
