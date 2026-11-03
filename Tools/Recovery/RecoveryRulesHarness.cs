using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

public static class RecoveryRulesBootstrap
{
    public static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(_,args)=>{
            string name=new AssemblyName(args.Name).Name+".dll";
            foreach(var variable in new[]{"HM_RECOVERY_ENGINE_MANAGED","HM_RECOVERY_SCRIPT_ASSEMBLIES"})
            {
                string directory=Environment.GetEnvironmentVariable(variable);
                if(string.IsNullOrEmpty(directory))continue;
                string path=Path.Combine(directory,name);
                if(File.Exists(path))return Assembly.LoadFrom(path);
            }
            return null;
        };
        return RecoveryRulesHarness.Run();
    }
}

public static class RecoveryRulesHarness
{
    static int passed,failed;
    static void Check(bool ok,string label) { Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(ok)passed++;else failed++; }
    static PlayerData Fresh()
    {
        var d=new PlayerData { Money=5000,RecruitedAgents=new List<AgentData>{new AgentData("Brick"),new AgentData("Dex"),new AgentData("Jay")} };
        RecoveryProgression.Begin(d);return d;
    }
    public static int Run()
    {
        try
        {
            var legacy=Fresh();legacy.RecoveryVersion=0;legacy.PoliceHeat=9;legacy.Money=17;legacy.CurrentLevel=4;
            RecoveryProgression.Ensure(legacy);RecoveryProgression.Ensure(legacy);
            Check(legacy.PoliceHeat==9&&legacy.Money==17&&legacy.CurrentLevel==4&&!legacy.RecoveryGuidedStart,"Legacy migration preserves heat, cash and campaign level without forcing tutorial");
            var d=Fresh();CampaignMissions.Ensure(d);CampaignMissions.Ensure(d);
            Check(d.PoliceHeat==1&&d.RecoveryGuidedStart,"New campaign remains quiet after repeated campaign normalization");
            Check(!RecoveryProgression.OrientationComplete(d),"New tutorial cannot complete from opening the UI");
            RecoveryProgression.RecordSelection(d,1);RecoveryProgression.RecordOrder(d,"MOVE");RecoveryProgression.RecordOrder(d,"MOVE");RecoveryProgression.RecordOrder(d,"MOVE");
            Check(d.LearnedMoveOrders==2&&!RecoveryProgression.OrientationComplete(d),"Repeated movement is recorded once per teaching step; split lesson still required");
            RecoveryProgression.RecordSelection(d,2);RecoveryProgression.RecordSelection(d,0,true);
            Check(!d.LearnedSplitSelection,"Clearing the entire selection does not fake individual split control");
            RecoveryProgression.RecordSelection(d,1,true);
            Check(RecoveryProgression.OrientationComplete(d),"Additive selection followed by removing one member completes the control lesson");
            RecoveryProgression.RecordOrder(d,"GUARD");RecoveryProgression.RecordOrder(d,"PATROL");
            Check(RecoveryProgression.DutiesComplete(d),"Guard and patrol learning persists independently of selection");
            var steps=CampaignMissions.Steps(d,1);
            Check(steps.Length==11&&!steps.Any(s=>s.id=="extort"||s.id=="taxi"||s.id=="sabotage"),"Home has eleven connected goals including base construction; extortion, taxi and sabotage are optional");
            CityBusinessLedger.Ensure(d);int records=d.CityBusinesses.Count;CityBusinessLedger.Ensure(d);
            Check(records==6&&d.CityBusinesses.Count==6,"Business initialization is idempotent and supplies six distinct archetypes");
            Check(CityBusinessLedger.Control(d,"burger")&&!CityBusinessLedger.Control(d,"burger"),"Control changes ownership once");
            int before=d.Money;
            Check(CityBusinessLedger.Tick(d,89,false)==0&&d.Money==before,"Income is not paid before its simulation interval");
            Check(CityBusinessLedger.Tick(d,1,false)==90&&d.Money==before+90,"A full interval pays the configured burger income once");
            CityBusinessLedger.Ensure(d);
            Check(CityBusinessLedger.Tick(d,0,false)==0&&d.Money==before+90,"Pause and reopening/normalizing the ledger do not pay again");
            Check(CityBusinessLedger.Loot(d,"burger")&&d.Money==before+340&&d.PoliceHeat==3,"Loot pays once and adds the declared police heat");
            Check(!CityBusinessLedger.Loot(d,"burger")&&d.Money==before+340,"Immediate repeated loot is rejected without a second payout");
            Check(CityBusinessLedger.Tick(d,180,false)==0,"A disrupted business earns nothing during its full disruption");
            Check(CityBusinessLedger.Tick(d,90,false)==90,"Trading income resumes after disruption");
            Check(!CityBusinessLedger.CanLoot(d,"burger",out _),"Loot cooldown persists after trading resumes");
            CityBusinessLedger.Tick(d,30,false);
            Check(CityBusinessLedger.CanLoot(d,"burger",out _),"Loot becomes available only when the full cooldown elapses");
            var partial=Fresh();CityBusinessLedger.Tick(partial,60,false);CityBusinessLedger.Control(partial,"burger");
            Check(CityBusinessLedger.Tick(partial,30,false)==30,"Capturing just before a payout earns only the controlled portion of the interval");
            var match=Fresh();CityBusinessLedger.Control(match,"pizza");CityBusinessLedger.Control(match,"pub");
            Check(CityBusinessLedger.Tick(match,90,true)==540,"Pizza and pub have explicit matchday demand bonuses");
            var split=Fresh();CityBusinessLedger.Control(split,"bakery");
            int chunks=CityBusinessLedger.Tick(split,45,false)+CityBusinessLedger.Tick(split,45,false);
            Check(chunks==100,"Income is consistent across different update chunk sizes");
            var pressured=Fresh();CityBusinessLedger.Control(pressured,"burger");CityBusinessLedger.Find(pressured,"burger").rivalPressureSeconds=5;
            Check(CityBusinessLedger.Tick(pressured,90,false)==0,"Contested business does not generate new income");
            int unchanged=d.Money;
            Check(!CityBusinessLedger.Control(d,"missing")&&!CityBusinessLedger.Loot(d,"missing")&&d.Money==unchanged,"Unknown business IDs cannot create rewards");
            Check(!MatchdayStrategySystem.CanStart(Fresh(),out _),"Matchday cannot auto-start before teaching and preparation");
            d.TransportPrepared=true;d.StadiumAccessPrepared=true;
            Check(MatchdayStrategySystem.CanStart(d,out _),"Prepared viable crew can choose to start matchday");
            var home=Fresh();home.CurrentLevel=2;home.CampaignResourceMission=1;home.CityIntel=4;home.CitySupplies=3;home.TransportPrepared=true;home.StadiumAccessPrepared=true;
            CityGameplay.HomeMode=true;CityOperationsLedger.Ensure(home);
            Check(home.CityIntel==4&&home.CitySupplies==3&&home.TransportPrepared,"Unlocking an Away chapter does not reset Home resources while still at Home");
            CityGameplay.HomeMode=false;home.LastSelectedDestination="Riverside";CityOperationsLedger.Ensure(home);
            Check(home.CampaignResourceMission==2&&home.CityIntel==0,"Away starts with its own operation resource context");
            home.CityIntel=2;CityGameplay.HomeMode=true;CityOperationsLedger.Ensure(home);
            Check(home.CampaignResourceMission==1&&home.CityIntel==4&&home.CitySupplies==3&&home.TransportPrepared&&home.StadiumAccessPrepared,"Returning Home restores permanent preparation and resources without repeating tasks");
            Check(ConciseButtonCaption.Shorten("ASSIGN STADIUM GUARD")=="ASSIGN STADIUM GUARD"&&ConciseButtonCaption.Shorten("ASSIGN PUB GUARD")=="ASSIGN PUB GUARD","Different strategic actions retain distinct descriptive captions");
            Check(ConciseButtonCaption.Shorten("LOOT +£250\nHEAT +2")=="LOOT +£250\nHEAT +2","Button captions preserve visible loot rewards and heat consequences");
            var away=Fresh();CityGameplay.HomeMode=false;away.CurrentLevel=2;away.LastSelectedDestination="Riverside";
            away.ActiveAwayAgentIds=away.RecruitedAgents.Take(2).Select(a=>a.AgentId).ToList();
            Check(CampaignMissions.LivingTravellers(away)==2&&CampaignMissions.HomeReserves(away)==1,"Away active crew excludes a nondeployed Home reserve");
            Check(CampaignMissions.MinimumAwayCrew(2)==3&&CampaignMissions.MinimumAwayCrew(4)==4,"Launch requirements grow from three to four actual travellers");
            foreach(int mission in new[]{2,3,4,5})
            {
                var chapter=CampaignMissions.Steps(away,mission);
                Check(chapter.Length>=6&&chapter.Length<=7&&chapter.Last().id=="extraction"&&!chapter.Any(s=>new[]{"extort","taxi","sabotage","rep","intel"}.Contains(s.id)),"Chapter "+mission+" has a reachable connected core and an extraction goal");
            }
            Check(!CampaignMissions.ReadyForExtraction(away),"A district cannot extract before its objectives are ready");
            foreach(var step in CampaignMissions.Steps(away))if(step.operation.HasValue)CampaignMissions.CompleteOperation(away,step.operation.Value);
            CampaignMissions.RecordRival(away,"First Firm");CampaignMissions.RecordRival(away,"Second Firm");CampaignMissions.RecordTerritory(away,"River Route");
            Check(CampaignMissions.ReadyForExtraction(away)&&!CampaignMissions.TryComplete(away,out _),"District readiness does not award success until physical extraction is recorded");
            int rewardCash=away.Money;CampaignMissions.RecordAction(away,"extraction","departed");
            Check(CampaignMissions.TryComplete(away,out var missionDone)&&missionDone.number==2&&away.CurrentLevel==3&&away.Money==rewardCash+5200,"Riverside extraction advances once and pays the chapter reward");
            rewardCash=away.Money;Check(!CampaignMissions.TryComplete(away,out _)&&away.Money==rewardCash,"Rechecking a finished Away chapter cannot replay its reward");
            Check(MatchdayStrategySystem.ResponseDeadline(0)<MatchdayStrategySystem.ResponseDeadline(1),"Stadium and pub responses have distinct visible opportunity windows");
            CityGameplay.HomeMode=true;
            Console.WriteLine($"RESULT {passed} passed, {failed} failed. Rules only; no Unity scene, save serializer or device execution.");
        }
        catch(Exception error){Console.WriteLine("FAIL HARNESS "+error);failed++;}
        return failed==0?0:1;
    }
}
