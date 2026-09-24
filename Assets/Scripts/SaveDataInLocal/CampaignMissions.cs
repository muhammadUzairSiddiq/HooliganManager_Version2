using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The single source of truth for the five-part city campaign. The nine small
/// operations are mission steps, never a second competing progression system.
/// </summary>
public static class CampaignMissions
{
    public const int MissionCount = 5;

    public readonly struct Mission
    {
        public readonly int number, reward;
        public readonly string title, destination, briefing;
        public Mission(int number, string title, string destination, string briefing, int reward)
        { this.number=number;this.title=title;this.destination=destination;this.briefing=briefing;this.reward=reward; }
    }

    public readonly struct Step
    {
        public readonly string id, title, body;
        public readonly int progress, target;
        public readonly CityOperationType? operation;
        public bool Complete => progress >= target;
        public Step(string id,string title,string body,int progress,int target,CityOperationType? operation=null)
        { this.id=id;this.title=title;this.body=body;this.progress=progress;this.target=target;this.operation=operation; }
    }

    static readonly Mission[] Missions =
    {
        new Mission(1,"FORTIFY THE HOME END","HOME","Build the firm from the ground up, connect every local service, secure turf and survive the rival push.",4000),
        new Mission(2,"RIVERSIDE FIRST CONTACT","Riverside","Run a complete away-day plan, establish one foothold and break the local rival network.",5200),
        new Mission(3,"NORTH END PRESSURE","North End","Operate under heavier policing, stock deeper reserves and clear a tougher district.",6800),
        new Mission(4,"EAST DOCKS LOCKDOWN","East Docks","Coordinate multiple teams, control the docks and keep the supporter network together.",8500),
        new Mission(5,"OLD TOWN FINAL","Old Town","Use every system at full pressure, take three strategic areas and defeat the strongest firms.",12000)
    };

    static readonly CityOperationType[] Operations =
    {
        CityOperationType.ScoutDistrict,CityOperationType.SupporterRally,CityOperationType.GatherSupplies,
        CityOperationType.CollectTickets,CityOperationType.CommunityEvent,CityOperationType.ObservePolice,
        CityOperationType.PrepareTransport,CityOperationType.FirstAid,CityOperationType.PrepareStadium
    };

    public static void Ensure(PlayerData d)
    {
        if(d==null)return;
        d.CurrentLevel=Math.Max(1,Math.Min(MissionCount,d.CurrentLevel));
        d.CompletedCampaignMissions ??= new List<int>();
        d.CompletedCampaignSteps ??= new List<string>();
        d.CampaignRivalKeys ??= new List<string>();
        d.CampaignTerritoryKeys ??= new List<string>();
        d.CampaignActionKeys ??= new List<string>();

        // Milestone 4 migration: both fresh and existing firm campaigns begin
        // under visible police pressure. Apply once so later heat reduction is
        // never silently undone when loading a save.
        if(!d.CampaignStartingHeatApplied)
        {
            d.PoliceHeat=Math.Max(7,d.PoliceHeat);
            d.CampaignStartingHeatApplied=true;
        }

        // Preserve valid progress from Milestones 1-2 without leaving the player
        // with the old disconnected 0/9 presentation.
        for(int i=1;i<d.CurrentLevel;i++)
            if(!d.CompletedCampaignMissions.Contains(i))d.CompletedCampaignMissions.Add(i);
        if(d.CompletedCampaignSteps.Count==0 && d.CompletedCityOperations!=null)
            foreach(var old in d.CompletedCityOperations)
                if(Enum.TryParse(old,out CityOperationType type))d.CompletedCampaignSteps.Add(OperationKey(d.CurrentLevel,type));
        if(d.CampaignTerritoryKeys.Count==0 && d.CityCapturedZones!=null)
            foreach(var zone in d.CityCapturedZones.Take(3))d.CampaignTerritoryKeys.Add($"M{d.CurrentLevel}:{zone}");
    }

    public static Mission Get(int number)=>Missions[Math.Max(0,Math.Min(MissionCount-1,number-1))];
    /// <summary>Home tasks always save on mission 1. An away trip saves on that destination's mission.</summary>
    public static int ContextMission(PlayerData d)
    {
        Ensure(d);
        if(d==null)return 1;
        if(CityGameplay.HomeMode)return 1;
        int away=MissionForDestination(d.LastSelectedDestination);
        return away>1?away:Math.Max(1,d.CurrentLevel);
    }
    public static Mission Active(PlayerData d){Ensure(d);return Get(ContextMission(d));}
    public static bool Completed(PlayerData d,int mission){Ensure(d);return d?.CompletedCampaignMissions?.Contains(mission)==true;}
    public static int MissionForDestination(string destination)
    {
        for(int i=1;i<=MissionCount;i++)if(string.Equals(Get(i).destination,destination,StringComparison.OrdinalIgnoreCase))return i;
        return 0;
    }
    public static bool CanEnterDestination(PlayerData d,string destination,out string reason)
    {
        Ensure(d);if(d==null){reason="Campaign data unavailable.";return false;}int mission=MissionForDestination(destination);
        if(mission==0){reason="Unknown campaign destination.";return false;}
        if(d.CompletedCampaignMissions.Count>=MissionCount){reason="Campaign complete - replay available.";return true;}
        if(mission!=d.CurrentLevel)
        {
            reason=mission<d.CurrentLevel?"Mission complete. Continue with the active destination.":$"Locked - complete Mission {d.CurrentLevel:00} first.";
            return false;
        }
        reason=$"Mission {mission:00} ready.";return true;
    }

    public static string OperationKey(int mission,CityOperationType type)=>$"M{mission}:{type}";
    public static bool OperationComplete(PlayerData d,CityOperationType type)
    { Ensure(d);return d?.CompletedCampaignSteps?.Contains(OperationKey(ContextMission(d),type))==true; }
    public static void CompleteOperation(PlayerData d,CityOperationType type)
    { Ensure(d);string key=OperationKey(ContextMission(d),type);if(!d.CompletedCampaignSteps.Contains(key))d.CompletedCampaignSteps.Add(key); }

    public static void RecordRival(PlayerData d,string firm)
    {
        Ensure(d);if(d==null||string.IsNullOrWhiteSpace(firm))return;
        string key=$"M{ContextMission(d)}:{firm}";if(!d.CampaignRivalKeys.Contains(key))d.CampaignRivalKeys.Add(key);
    }
    public static int RivalCount(PlayerData d,int mission)
    { Ensure(d);string prefix=$"M{mission}:";return d?.CampaignRivalKeys?.Count(k=>k.StartsWith(prefix,StringComparison.Ordinal))??0; }
    public static void RecordTerritory(PlayerData d,string zone)
    {
        Ensure(d);if(d==null||string.IsNullOrWhiteSpace(zone))return;
        string key=$"M{ContextMission(d)}:{zone}";if(!d.CampaignTerritoryKeys.Contains(key))d.CampaignTerritoryKeys.Add(key);
    }
    public static int TerritoryCount(PlayerData d,int mission)
    { Ensure(d);string prefix=$"M{mission}:";return d?.CampaignTerritoryKeys?.Count(k=>k.StartsWith(prefix,StringComparison.Ordinal))??0; }

    public static void RecordAction(PlayerData d,string action,string token=null)
    {
        Ensure(d);if(d==null||string.IsNullOrWhiteSpace(action))return;
        string prefix=$"M{ContextMission(d)}:{action}:";
        if(string.IsNullOrWhiteSpace(token))token=(ActionCount(d,d.CurrentLevel,action)+1).ToString();
        string key=prefix+token;
        if(!d.CampaignActionKeys.Contains(key))d.CampaignActionKeys.Add(key);
    }
    public static int ActionCount(PlayerData d,int mission,string action)
    {
        Ensure(d);string prefix=$"M{mission}:{action}:";
        return d?.CampaignActionKeys?.Count(k=>k.StartsWith(prefix,StringComparison.Ordinal))??0;
    }

    public static Step[] Steps(PlayerData d,int mission=0)
    {
        Ensure(d);if(d==null)return Array.Empty<Step>();if(mission<=0)mission=ContextMission(d);
        var steps=new List<Step>(15);
        string destination=Get(mission).destination;
        foreach(var op in Operations)
        {
            string key=OperationKey(mission,op);
            bool done=d.CompletedCampaignSteps.Contains(key);
            JobCopy(destination,op,out string title,out string body);
            steps.Add(new Step(key,title,body,done?1:0,1,op));
        }
        int crew=d.RecruitedAgents?.Count(a=>a!=null&&a.IsAlive)??0;
        int rivals=RivalCount(d,mission),territory=TerritoryCount(d,mission);
        int extort=ActionCount(d,mission,"extort"),tax=ActionCount(d,mission,"tax");
        int taxi=ActionCount(d,mission,"taxi"),sabotage=ActionCount(d,mission,"sabotage");
        switch(mission)
        {
            case 1:
                steps.Add(new Step("crew","BUILD A FIVE-PERSON CORE","Recruit enough people to split scouting, logistics and security assignments.",crew,5));
                steps.Add(new Step("training","RUN A TRAINING SESSION","Train at least once so the home defence is not attempted with an unprepared crew.",d.HomeTrainingLevel,1));
                steps.Add(new Step("morale","LIFT SUPPORTER MORALE","Use the pub and community network to reach 75 morale.",d.FanMorale,75));
                steps.Add(new Step("territory","SECURE TWO HOME AREAS","Clear rivals, hold each ring and gain safe-route and supply benefits.",territory,2));
                steps.Add(new Step("extort","WORK THE STREETS","Demand money from two civilians. Refusal can become a damaging street fight and every attempt adds police heat.",extort,2));
                steps.Add(new Step("tax","COLLECT PROTECTION INCOME","Pay the occupation tax when turf is taken, then collect income from a secured area.",tax,1));
                steps.Add(new Step("taxi","USE THE TAXI NETWORK","Board the marked taxi and move the crew instantly to a distant district location.",taxi,1));
                steps.Add(new Step("sabotage","DESTROY A RIVAL SUPPLY CAR","Reach the marked vehicle, plant the charge and survive the police-heat increase.",sabotage,1));
                steps.Add(new Step("matchday","HANDLE MATCHDAY ESCALATION","Resolve the rival-fan arrival around the real stadium before the post-match window closes.",ActionCount(d,mission,"matchday"),1));
                steps.Add(new Step("defence","DEFEND HEADQUARTERS","Finish the home operation by defeating the organized rival push.",d.HomeDefenceCompleted?1:0,1));
                break;
            case 2:
                steps.Add(new Step("rivals","BREAK TWO RIVAL GROUPS","Use scouting before committing the full crew.",rivals,2));
                steps.Add(new Step("territory","ESTABLISH A RIVERSIDE FOOTHOLD","Clear and hold one control ring.",territory,1));
                steps.Add(new Step("tax","MAKE THE FOOTHOLD PAY","Collect one protection payment after paying the occupation tax.",tax,1));
                steps.Add(new Step("taxi","RUN A FAST EXTRACTION","Use the taxi once to reposition the away crew.",taxi,1));
                steps.Add(new Step("sabotage","BURN THE RIVERSIDE SUPPLY CAR","Destroy one marked rival vehicle.",sabotage,1));
                steps.Add(new Step("crew","DEPLOY A THREE-PERSON TEAM","Bring enough members to run support and combat roles in parallel.",crew,3));
                break;
            case 3:
                steps.Add(new Step("rivals","CLEAR THREE NORTH END FIRMS","Defeat the district's three organized rival groups.",rivals,3));
                steps.Add(new Step("territory","CONTROL TWO ROUTE NODES","Hold two areas so retreat and resupply routes stay open.",territory,2));
                steps.Add(new Step("intel","BUILD A DEEP INTEL PICTURE","Combine scouting and police observation to reach 5 intel.",d.CityIntel,5));
                steps.Add(new Step("supplies","HOLD TWO SUPPLY UNITS","Keep enough stock available for first aid after contact.",d.CitySupplies,2));
                steps.Add(new Step("extort","PRESS THREE LOCALS FOR CASH","Risk civilian resistance and police heat to finance the operation.",extort,3));
                steps.Add(new Step("tax","COLLECT FROM TWO ROUTES","Turn both secured areas into income.",tax,2));
                steps.Add(new Step("sabotage","DESTROY TWO SUPPORT VEHICLES","Cut the rival transport network before the final fight.",sabotage,2));
                break;
            case 4:
                steps.Add(new Step("rivals","DISMANTLE THREE DOCK FIRMS","Coordinate simultaneous assignments before the final confrontation.",rivals,3));
                steps.Add(new Step("territory","LOCK DOWN TWO DOCK AREAS","Capture two linked areas for route and resource bonuses.",territory,2));
                steps.Add(new Step("social","BUILD FOUR SOCIAL MOMENTUM","Connect pub, community and supporter activity.",d.SocialMomentum,4));
                steps.Add(new Step("crew","FIELD FIVE ACTIVE MEMBERS","Maintain enough depth for parallel work and recovery.",crew,5));
                steps.Add(new Step("rep","REACH 30 REPUTATION","Turn successful operations into city-wide standing.",d.Reputation,30));
                steps.Add(new Step("tax","COLLECT TWO DOCK PAYMENTS","Use controlled dock areas to fund the crew.",tax,2));
                steps.Add(new Step("taxi","COMPLETE TWO RAPID TRANSFERS","Split the operation by using the taxi network twice.",taxi,2));
                steps.Add(new Step("sabotage","DESTROY TWO DOCK VEHICLES","Blast both marked rival logistics vehicles.",sabotage,2));
                break;
            default:
                steps.Add(new Step("organizer","NEUTRALIZE THE OLD TOWN ORGANIZER","Identify and defeat the command group protecting the rival organizer before the district-wide push.",Math.Min(rivals,1),1));
                steps.Add(new Step("rivals","DEFEAT FOUR ELITE FIRMS","Clear Old Town's complete rival command structure.",rivals,4));
                steps.Add(new Step("territory","TAKE THREE STRATEGIC AREAS","Create a linked final route through Old Town.",territory,3));
                steps.Add(new Step("intel","REACH SEVEN INTEL","Map patrols, rival positions and extraction routes.",d.CityIntel,7));
                steps.Add(new Step("social","REACH FIVE SOCIAL MOMENTUM","Keep the supporter network organized under maximum pressure.",d.SocialMomentum,5));
                steps.Add(new Step("crew","FIELD SIX ACTIVE MEMBERS","Use specialists and reserves instead of one exhausted group.",crew,6));
                steps.Add(new Step("rep","REACH 45 REPUTATION","Enter the final action with proven city-wide influence.",d.Reputation,45));
                steps.Add(new Step("extort","RAISE STREET CASH FOUR TIMES","Use intimidation carefully; civilians can fight back and every incident raises heat.",extort,4));
                steps.Add(new Step("tax","COLLECT THREE AREA PAYMENTS","Fund the final operation from all three controlled areas.",tax,3));
                steps.Add(new Step("taxi","RUN THREE RAPID TRANSFERS","Move specialists around Old Town without exhausting them on long walks.",taxi,3));
                steps.Add(new Step("sabotage","DESTROY THREE ELITE VEHICLES","Remove the rival command group's full vehicle network.",sabotage,3));
                break;
        }
        return steps.ToArray();
    }

    public static (int complete,int total) Progress(PlayerData d,int mission=0)
    { var s=Steps(d,mission);return(s.Count(x=>x.Complete),s.Length); }
    public static bool TryComplete(PlayerData d,out Mission completed)
    {
        completed=default;Ensure(d);if(d==null)return false;
        int finished=ContextMission(d);
        if(Completed(d,finished)||Steps(d,finished).Any(s=>!s.Complete))return false;
        completed=Get(finished);d.CompletedCampaignMissions.Add(finished);d.Money+=completed.reward;
        if(finished==d.CurrentLevel&&finished<MissionCount)d.CurrentLevel=finished+1;
        return true;
    }

    public static bool HomeCleared(PlayerData d)
    {
        Ensure(d);
        if(d==null)return false;
        return Completed(d,1)||d.CurrentLevel>1;
    }

    public static int JobFlavor(string destination)
    {
        if(string.Equals(destination,"Riverside",StringComparison.OrdinalIgnoreCase))return 1;
        if(string.Equals(destination,"North End",StringComparison.OrdinalIgnoreCase))return 2;
        if(string.Equals(destination,"East Docks",StringComparison.OrdinalIgnoreCase))return 3;
        if(string.Equals(destination,"Old Town",StringComparison.OrdinalIgnoreCase))return 4;
        return 0;
    }

    public static void JobCopy(string destination,CityOperationType type,out string title,out string body)
    {
        int flavor=JobFlavor(destination);
        switch(type)
        {
            case CityOperationType.ScoutDistrict:
                title=flavor switch{1=>"PROMENADE SCOUT",2=>"ESTATE SCOUT",3=>"WAREHOUSE SCOUT",4=>"CATHEDRAL LANE",_=>"CHURCH LOOKOUT"};
                body=flavor switch{
                    1=>"Walk the river edge, then the far wall, and clock the boats.",
                    2=>"Cross the estate yard and mark who is watching the arches.",
                    3=>"Check the warehouse mouth, then the far wall, before the crew commits.",
                    4=>"Move through cathedral lane and read the square.",
                    _=>"Walk the church yard, then the school wall, and mark the streets."};
                return;
            case CityOperationType.SupporterRally:
                title=flavor switch{1=>"RIVER PUB CHANT",2=>"SOCIAL CLUB",3=>"DOCK PUB",4=>"CROWN PUB",_=>"PUB MEET"};
                body=flavor switch{
                    1=>"Take the riverside bar and run three chants with the crew.",
                    2=>"Hold the social club and pull the north-end crowd in.",
                    3=>"Use the dock bar to keep the firms from splitting.",
                    4=>"Fill the Crown and chant until the square answers.",
                    _=>"Stand in the pub and chant until the room joins in."};
                return;
            case CityOperationType.GatherSupplies:
                title=flavor switch{1=>"BOATYARD STOCK",2=>"MARKET CUT",3=>"CONTAINER PICK",4=>"BACK MARKET",_=>"BARBER RUN"};
                body=flavor switch{
                    1=>"Pick up stock along the boatyard, one stop at a time.",
                    2=>"Work the market cut and carry the bags back.",
                    3=>"Clear four pickup points around the containers.",
                    4=>"Buy through the back market without lingering.",
                    _=>"Use the barber shop as the drop and pick up three loads."};
                return;
            case CityOperationType.CollectTickets:
                title=flavor switch{1=>"FERRY TICKETS",2=>"RAIL ARCH TICKETS",3=>"FERRY ROAD WINDOW",4=>"TOWN GATE TICKETS",_=>"STATION WINDOW"};
                body="Queue at the window, then walk the platform with the crew.";
                return;
            case CityOperationType.CommunityEvent:
                title=flavor switch{1=>"WATERFRONT GATHERING",2=>"NORTH END MEET",3=>"LOCK-GATE MEET",4=>"SQUARE GATHERING",_=>"DOLPHINARIUM MEET"};
                body=flavor switch{
                    1=>"Gather on the waterfront and hold the crowd for four beats.",
                    2=>"Meet the north-end locals in the open and keep them there.",
                    3=>"Pull a crowd at the lock gate before the vans roll.",
                    4=>"Take the square and make the gathering visible.",
                    _=>"Gather by the dolphinarium and work the crowd in the open."};
                return;
            case CityOperationType.ObservePolice:
                title=flavor switch{1=>"BRIDGE WATCH",2=>"PATROL SHADOW",3=>"DOCK POLICE WATCH",4=>"OLD WATCH",_=>"YARD WATCH"};
                body=flavor switch{
                    2=>"Stand across from the yard and track five patrol turns.",
                    _=>"Hold the open ground opposite the police building and watch the yard."};
                return;
            case CityOperationType.PrepareTransport:
                title=flavor switch{1=>"BOAT CREW",2=>"BUS CREW",3=>"CLEAR THE LANE",4=>"ESCAPE VANS",_=>"FIREHOUSE RUN"};
                body=flavor switch{
                    1=>"Walk the crew from the mooring to the departure point.",
                    2=>"Stage at the bus bay, then move to the departure mark.",
                    3=>"Clear the lane outside the firehouse, then move to departure.",
                    4=>"Check the escape vans and walk the departure route.",
                    _=>"Check the firehouse bay, then walk the departure point."};
                return;
            case CityOperationType.FirstAid:
                title=flavor switch{1=>"RIVERSIDE PATCH",2=>"ESTATE CLINIC",3=>"DOCK INFIRMARY",4=>"LANE CLINIC",_=>"HOSPITAL WARD"};
                body="Walk the crew into the hospital approach and patch whoever is hurt.";
                return;
            default:
                title=flavor switch{1=>"AWAY-END WARMUP",2=>"HEAVY GYM",3=>"YARD CIRCUIT",4=>"OLD STAND DRILL",_=>"GYM CIRCUIT"};
                body=flavor switch{
                    2=>"Four gym stations. Punch through each one.",
                    3=>"Run the yard circuit and hit every station.",
                    _=>"Three gym stations. The crew punches through each one."};
                return;
        }
    }

    public static string OperationTitle(CityOperationType type)
    {
        JobCopy("HOME",type,out string title,out _);
        return title;
    }
}
