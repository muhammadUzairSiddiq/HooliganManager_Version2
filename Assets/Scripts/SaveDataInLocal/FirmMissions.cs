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
        int activeCrew=d.RecruitedAgents?.Count(a=>a!=null&&a.IsAlive)??0;
        int captured=d.CityCapturedZones?.Count??0;
        int uniqueTrips=d.DestinationVisitCounts?.Count??0;
        if(tab==1)return new[] {
            new Mission("day"+d.MatchDay+"crew","BRING IN NEW BLOOD","Queue two fans at Recruitment for next matchday.",d.PendingFansGain,2,200,2),
            new Mission("day"+d.MatchDay+"morale","KEEP THE FAITH","Rally at the pub. Maintain at least 75 morale.",d.FanMorale,75,250,1),
            new Mission("day"+d.MatchDay+"funds","BUILD A WAR CHEST","Keep £6,000 available for your next trip.",d.Money,6000,300,1),
            new Mission("day"+d.MatchDay+"training","SHARPEN THE CREW","Use the training area at least once this matchday.",d.LastTrainingMatchday==d.MatchDay?1:0,1,300,3),
            new Mission("day"+d.MatchDay+"travel","KEEP THE CALENDAR MOVING","Complete at least one away-trip departure this matchday.",d.LastAwayTripMatchday==d.MatchDay?1:0,1,300,0) };
        return new[] {
            new Mission("crew5","BUILD YOUR CREW","Grow your active squad to five members.",activeCrew,5,1000,2),
            new Mission("win3","MAKE YOUR MARK","Win three battles against rival firms.",d.BattleWins,3,1500),
            new Mission("rep30","EARN YOUR REPUTATION","Reach 30 reputation across the campaign.",d.Reputation,30,1800),
            new Mission("level3","TAKE THE STREETS","Reach campaign level three.",d.CurrentLevel,3,2500),
            new Mission("home_defence","PROTECT HEADQUARTERS","Win the home territory defence scenario.",d.HomeDefenceCompleted?1:0,1,1750,0),
            new Mission("capture3","LOCK DOWN LOCAL TURF","Capture three home territory control points.",captured,3,2200,1),
            new Mission("travel4","WORK EVERY DESTINATION","Visit all four away districts.",uniqueTrips,4,2600,0),
            new Mission("level5","CITY DOMINATED","Clear the full five-level RTS campaign.",d.CurrentLevel,5,4000,0) };
    }
    public static bool Claimed(PlayerData d,string id)=>d.LandscapeClaimedMissions?.Contains(id)==true;
    public static bool Claim(PlayerData d,string id)
    {
        if(d==null || Claimed(d,id))return false;
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
