#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ITHappy;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

[InitializeOnLoad]
public static class TrainRaidPlaytest
{
    const string Key="HM.TrainRaidPlaytest",Save="Artifacts/Recovery/train-raid-save.json",Report="Artifacts/Recovery/train-raid-playtest.txt";
    static readonly List<string> rows=new List<string>();static bool running,finished;static double deadline;
    static TrainRaidPlaytest(){if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;}
    public static void BeginBatch(){SessionState.SetBool(Key+".batch",true);Begin();}
    [MenuItem("Hooligan/Recovery/Run train arrivals and rival shop raid check")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Start in edit mode.");Directory.CreateDirectory("Artifacts/Recovery");
        SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));
        PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
        var d=new PlayerData{PlayerName="Train and Raid QA",Money=5000,Fans=3};RecoveryProgression.Begin(d);foreach(var name in new[]{"Responder","Home Guard","Runner"})d.RecruitedAgents.Add(new AgentData(name,hp:1000,speed:18));
        SaveDataInLocal.DataSave(d);SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING "+DateTime.UtcNow.ToString("O"));EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){rows.Clear();running=finished=false;deadline=EditorApplication.timeSinceStartup+150;CityGameplay.HomeMode=true;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("Gameplay");}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");int marker=SessionState.GetInt(Key+".marker",-1);if(marker<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,marker);
            if(SessionState.GetBool(Key+".batch",false)){SessionState.SetBool(Key+".batch",false);EditorApplication.Exit(rows.Any(r=>r.StartsWith("FAIL "))?1:0);}
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished)return;if(EditorApplication.timeSinceStartup>deadline){Finish("Train/raid watchdog expired.");return;}
        if(!running&&CityTrainSchedule.Instance)CityTrainSchedule.Instance.AutomaticArrivalsEnabled=false;
        if(running||!BattleManager.instance||!BattleManager.instance.BattleActive||!CityBusinessSystem.Instance||!LivingRivalDirector.Instance||!CityTrainSchedule.Instance||!CityTrainSchedule.Instance.HasService)return;
        running=true;deadline=EditorApplication.timeSinceStartup+700;var go=new GameObject("Train Raid Test Runner");UnityEngine.Object.DontDestroyOnLoad(go);go.AddComponent<TrainRaidTestRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator routine){while(true){bool more=false;object value=null;try{more=routine.MoveNext();if(more)value=routine.Current;}catch(Exception error){Finish(error.ToString());yield break;}if(!more){Finish(null);yield break;}yield return value;}}
    static void Check(bool ok,string label){rows.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add("Native disposable fixture: train spline phase positioned at its real station stop to isolate dispatch; durable fast response crew, accelerated time, automatic rival clashes disabled. Existing gang actors still walk actual complete routes and perform timed work. No natural service-frequency/device/performance claim.");rows.Add("UTC "+DateTime.UtcNow.ToString("O"));File.WriteAllLines(Report,rows);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static IEnumerator Until(Func<bool> condition,float seconds=100){float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;}
    static void TrainDetail(CityTrainSchedule train,string stage)
    {
        rows.Add($"DETAIL {stage}: wave={train.ArrivalWaveCount}, stopped={train.IsStopped}, finished={train.DispatchFinished}, groups={train.ArrivalGroups.Count}, live={train.LiveArrivalCount}, shared={CityActivityStreaming.Instance?.LiveAmbientBodies}");
        foreach(var reason in train.DispatchDiagnostics)rows.Add("DETAIL dispatch: "+reason);
        foreach(var group in train.ArrivalGroups)
        {
            rows.Add($"DETAIL group={group.Kind}; firm={group.Firm}; members={group.Members.Count}; exit={group.Exit}; venue={group.Destination}");
            foreach(var actor in group.Members)
            {
                var nav=actor?actor.GetComponent<NavMeshAgent>():null;
                rows.Add(actor&&nav?$"DETAIL actor={actor.GetInstanceID()}, alive={actor.IsAlive}, position={actor.transform.position}, type={nav.agentTypeID}, mask={nav.areaMask}, onMesh={nav.isOnNavMesh}, hasPath={nav.hasPath}, pending={nav.pathPending}, status={nav.pathStatus}, remaining={nav.remainingDistance}, escortFailure={actor.LastEscortFailure}":"DETAIL missing arrival actor or NavMeshAgent");
            }
        }
        File.WriteAllLines(Report,rows);
    }
    static void PositionTrain(CityTrainSchedule schedule)
    {
        var carriages=GameObject.Find("Train").GetComponentsInChildren<SplineSpeed>();var type=typeof(SplineSpeed);const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        for(int carriageIndex=0;carriageIndex<carriages.Length;carriageIndex++)
        {
            var carriage=carriages[carriageIndex];var container=(SplineContainer)type.GetField("m_Container",flags).GetValue(carriage);int spline=(int)type.GetField("m_Spline",flags).GetValue(carriage);float length=(float)type.GetField("m_Length",flags).GetValue(carriage);
            float best=0,distance=float.MaxValue;for(int step=0;step<=512;step++){container.Evaluate(spline,step/512f,out var p,out var tangent,out var up);float candidate=Vector3.Distance((Vector3)p,schedule.StationStopPoint);if(candidate<distance){distance=candidate;best=step/512f;}}
            type.GetField("m_Time",flags).SetValue(carriage,Mathf.Repeat(best-carriageIndex*14/Mathf.Max(1,length),1));carriage.RuntimeHeightOffset=0;type.GetMethod("EvaluateTransform",flags).Invoke(carriage,null);
        }
    }
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(1);var d=GameManager.Data;var battle=BattleManager.instance;var director=LivingRivalDirector.Instance;director.AutomaticDecisionsEnabled=false;
        if(LivePoliceSystem.Instance){LivePoliceSystem.Instance.StopAllCoroutines();LivePoliceSystem.Instance.enabled=false;}
        foreach(var actor in battle.EnemyAgents)if(actor){actor.SetCinematicIdle(true);actor.isHostile=false;}
        var train=CityTrainSchedule.Instance;int roster=d.RecruitedAgents.Count;var carriages=GameObject.Find("Train").GetComponentsInChildren<SplineSpeed>();
        // Remove only this disposable fixture's earlier natural arrival bodies.
        // The subsequent manual stop is still gated by the real train position.
        train.AutomaticArrivalsEnabled=false;train.enabled=false;yield return null;train.enabled=true;
        foreach(var carriage in carriages)carriage.RuntimeSpeedMultiplier=.45f;
        CameraPanTouchOnly.Instance.FocusOn(CityGameplay.Instance.Locations[7]);yield return new WaitForSecondsRealtime(.7f);
        TrainDetail(train,"Clean service before positioning");
        Check(train.HasService&&carriages.All(c=>c.RuntimeHeightOffset==0),"Actual train service retains the authored railway height");
        carriages[0].transform.position=train.StationStopPoint+Vector3.right*100;
        Check(!train.BeginArrivalStop(),"Passenger dispatch rejects a train that is not physically at its stop");
        PositionTrain(train);Time.timeScale=2;
        Check(train.BeginArrivalStop()&&train.IsStopped&&carriages.All(c=>c.StationStop),"The actual station-positioned train stops its carriages before dispatch");
        yield return Until(()=>train.DispatchFinished,30);yield return null;yield return null;TrainDetail(train,"Completed first stop");
        Check(train.ArrivalGroups.Count==3&&train.ArrivalGroups.Sum(g=>g.Members.Count)>=9&&train.ArrivalGroups.All(g=>g.Members.Count>=2),"One real stop dispatches multiple named home, visiting and neutral groups");
        var traveller=train.ArrivalGroups.FirstOrDefault(g=>g.Kind=="CITY TRAVELLERS");
        Check(train.ArrivalGroups.Select(g=>g.Kind).Distinct().Count()==3&&train.ArrivalGroups.Select(g=>g.Destination).Distinct().Count()==3&&traveller!=null&&CityBusinessSystem.Instance.Sites.Any(s=>s&&s.Definition.id!="pub"&&CityBusinessSystem.HorizontalDistance(s.transform.position,traveller.Destination)<=14),"Home, visiting and city-traveller groups head to distinct pub, stadium and real shop purposes");
        Check(train.ArrivalGroups.SelectMany((g,i)=>train.ArrivalGroups.Skip(i+1).Select(other=>CityBusinessSystem.HorizontalDistance(g.Exit,other.Exit)>=5)).All(v=>v),"Station groups emerge from distinct connected carriage-projected exits instead of one spawn pile");
        Check(train.ArrivalGroups.SelectMany(g=>g.SpawnPoints).All(p=>Mathf.Abs(p.y-CityGameplay.Instance.Locations[7].y)<2.6f),"Recorded actual arrival spawn points are on station-level curbs rather than rooftops");
        var people=train.ArrivalGroups.SelectMany(g=>g.Members).Where(a=>a&&a.IsAlive).ToArray();
        yield return Until(()=>people.All(a=>a.GetComponent<NavMeshAgent>()&&!a.GetComponent<NavMeshAgent>().pathPending),5);
        Check(people.Length>=9&&people.All(a=>
        {
            var nav=a.GetComponent<NavMeshAgent>();var path=new NavMeshPath();var group=train.ArrivalGroups.First(g=>g.Members.Contains(a));
            return nav.isOnNavMesh&&NavMesh.CalculatePath(a.transform.position,group.Destination,new NavMeshQueryFilter{agentTypeID=nav.agentTypeID,areaMask=nav.areaMask},path)&&path.status==NavMeshPathStatus.PathComplete;
        }),"Every actual arrival actor receives a complete route on its own navigation type");
        var moving=people.OrderByDescending(a=>a.GetComponent<NavMeshAgent>().remainingDistance).FirstOrDefault();Vector3 start=moving?moving.transform.position:Vector3.zero;yield return new WaitForSeconds(3);
        Check(moving&&Vector3.Distance(start,moving.transform.position)>1,"Arriving supporters physically leave their exits along the assigned routes");
        Check(d.RecruitedAgents.Count==roster&&train.LiveArrivalCount<=CityTrainSchedule.MaximumArrivalBodies,"Ambient arrivals stay bounded and never create free player crew");
        Time.timeScale=1;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/train-arrival-groups.png");yield return null;Time.timeScale=5;
        yield return Until(()=>!train.IsStopped,25);PositionTrain(train);Check(train.BeginArrivalStop(),"The same actual train can serve a later scheduled stop");yield return new WaitForSeconds(8);
        Check(train.LiveArrivalCount<=CityTrainSchedule.MaximumArrivalBodies&&CityActivityStreaming.Instance.LiveAmbientBodies<=CityActivityStreaming.BodyBudget,"Repeated arrival waves respect both local and shared city actor budgets");
        train.enabled=false;yield return null;
        Check(train.LiveArrivalCount==0&&carriages.All(c=>!c.StationStop&&c.RuntimeHeightOffset==0),"Disabling the service releases its pooled arrivals and the held train without hiding railway geometry");

        // Existing gang members, rather than a newly spawned scripted squad,
        // must walk to an actual business and finish exposed work there.
        var sites=CityBusinessSystem.Instance.Sites;var target=sites.FirstOrDefault(s=>director.TryStartShopRaid(s));
        Check(target&&director.ActiveRaids.Count==1,"An unguarded actual business accepts a real existing-gang raid assignment");if(!target)yield break;
        var raid=director.ActiveRaids[0];var crewIds=raid.Crew.Select(a=>a.GetInstanceID()).ToArray();var origins=raid.Crew.Select(a=>a.transform.position).ToArray();int cash=d.Money,heat=d.PoliceHeat;
        Check(raid.Crew.Length>=2&&raid.Crew.All(a=>!a.IsAmbientMatchdayUnit&&battle.EnemyAgents.Contains(a))&&raid.Crew.Select(a=>a.firmName).Distinct().Count()==1,"Raid uses multiple existing members of one real firm and does not invent an ambient army");
        CameraPanTouchOnly.Instance.FocusOn(target.transform.position);yield return new WaitForSeconds(4);
        Check(raid.Crew.Select((a,i)=>Vector3.Distance(a.transform.position,origins[i])>1).Any(value=>value),"Assigned raiders physically travel toward the shop");
        yield return Until(()=>director.ActiveRaids.Count==0,100);var state=CityBusinessLedger.Find(d,target.Definition.id);
        Check(state.rivalRaidCount==1&&state.rivalRaidLoot==target.Definition.loot&&state.disruptionSeconds>100&&state.rivalRaidCooldownSeconds>220,"Actual arrival and timed on-site work record NPC loot, disruption and cooldown once");
        Check(d.Money==cash&&d.PoliceHeat==heat&&!CityBusinessLedger.CompleteRivalRaid(d,target.Definition.id,raid.Firm),"NPC theft never rewards the player's wallet/heat and cannot replay within its saved cooldown");
        GameManager.Save();var loaded=SaveDataInLocal.DataLoad();var saved=CityBusinessLedger.Find(loaded,target.Definition.id);
        Check(saved.rivalRaidCount==1&&saved.rivalRaidCooldownSeconds>0&&saved.disruptionSeconds>0&&saved.lastRaidingFirm==raid.Firm,"Native save retains rival raid identity, disruption and replay protection");
        var effects=CityLivingPresentation.Instance;Check(effects&&effects.ActiveDamageEffects<=CityLivingPresentation.MaximumDamageEffects&&(GameFeedback.ReducedMotion?effects.ActiveDamageEffects==0:effects.ActiveDamageEffects>0),"A completed shop raid uses bounded local damage feedback and respects reduced-motion preference");
        effects.RaidDamage(target.transform.position);effects.RaidDamage(target.transform.position);Check(effects.ActiveDamageEffects<=2&&effects.transform.Cast<Transform>().Where(t=>t.name=="Pooled damaged shop effect").All(t=>t.GetComponentsInChildren<Collider>(true).Length==0),"Repeated damage feedback reuses at most two effects and adds no world collider");
        yield return new WaitForSeconds(7);Check(effects.ActiveDamageEffects==0&&(!effects.WaterEffectActive||effects.VerifiedWaterSurfaces>0),"Transient damage clears, and water foam exists only for a verified existing surface");

        var revised=sites.FirstOrDefault(s=>s!=target&&director.TryStartShopRaid(s));Check(revised,"A separate venue can receive a later independent raid");if(!revised)yield break;
        var revisedRaid=director.ActiveRaids[0];var reassigned=revisedRaid.Crew[0];var nav=reassigned.GetComponent<NavMeshAgent>();
        Vector3 elsewhere=CityGameplay.ReachableApproach(reassigned.transform.position,reassigned.transform.position+Vector3.right*24);bool newRoute=reassigned.TryBeginEscort(elsewhere,false);Vector3 newDestination=nav.destination;yield return null;yield return null;
        Check(newRoute&&director.ActiveRaids.Count==0&&CityBusinessLedger.Find(d,revised.Definition.id).rivalRaidCount==0&&CityBusinessSystem.HorizontalDistance(nav.destination,newDestination)<.5f,"A newer real route cancels the raid without theft and retains the replacement navigation order");

        var defender=battle.PlayerAgents.First(a=>a&&a.IsAlive);CityBusiness defenseSite=null;
        foreach(var site in sites.Where(s=>s!=target&&s!=revised))
        {
            Vector3 approach=CityGameplay.ReachableApproach(site.transform.position,site.transform.position+Vector3.right*32);
            if(CityBusinessSystem.HorizontalDistance(approach,site.transform.position)<20||!defender.TryCommandMoveTo(approach))continue;
            yield return Until(()=>CityBusinessSystem.HorizontalDistance(defender.transform.position,approach)<6,80);
            if(director.TryStartShopRaid(site)){defenseSite=site;break;}
        }
        Check(defenseSite,"A defender may pre-position outside a shop while rivals begin their real approach");if(!defenseSite)yield break;
        yield return Until(()=>director.ActiveRaids.Count==0||director.ActiveRaids[0].WorkSeconds>1,100);
        bool defenderInterruptedWork=director.ActiveRaids.Count>0&&director.ActiveRaids[0].WorkSeconds>1;
        defender.TryCommandMoveTo(defenseSite.transform.position);yield return Until(()=>director.ActiveRaids.Count==0,35);
        Check(defenderInterruptedWork&&CityBusinessSystem.HorizontalDistance(defender.transform.position,defenseSite.transform.position)<12&&CityBusinessLedger.Find(d,defenseSite.Definition.id).rivalRaidCount==0&&director.ActiveRaids.Count==0,"Actual defender arrival interrupts exposed rival work before any loot or disruption outcome");
        Time.timeScale=1;ScreenCapture.CaptureScreenshot("Artifacts/Recovery/rival-shop-raid-defense.png");yield return null;
        Time.timeScale=5;defender.TryCommandMoveTo(CityGameplay.Instance.Home);
        CityBusiness policeSite=null;EnemyController officer=null;
        foreach(var site in sites.Where(s=>s!=target&&s!=revised&&s!=defenseSite))
        {
            var approach=CityGameplay.ReachableApproach(site.transform.position,site.transform.position+Vector3.right*32);
            if(CityBusinessSystem.HorizontalDistance(approach,site.transform.position)<20)continue;
            CameraPanTouchOnly.Instance.FocusOn(site.transform.position);yield return new WaitForSecondsRealtime(.4f);
            officer=battle.SpawnMatchdayFighter(approach,"POLICE",new Color(.15f,.45f,.85f),500,0,MatchdayUnitRole.Police,"SHOP RAID TEST PATROL");
            if(!officer)continue;var march=officer.GetComponent<MatchdayMarch>();if(march)march.enabled=false;officer.SetCinematicIdle(true);
            if(director.TryStartShopRaid(site)){policeSite=site;break;}battle.DespawnMatchdayFighter(officer);officer=null;
        }
        Check(policeSite&&officer,"A real on-foot police patrol can wait outside an independently targeted shop");
        if(policeSite&&officer)
        {
            yield return Until(()=>director.ActiveRaids.Count==0||director.ActiveRaids[0].WorkSeconds>1,100);
            bool policeInterruptedWork=director.ActiveRaids.Count>0&&director.ActiveRaids[0].WorkSeconds>1;
            officer.SetCinematicIdle(false);bool policeRoute=officer.TryBeginEscort(policeSite.transform.position,true);yield return Until(()=>director.ActiveRaids.Count==0,35);
            Check(policeInterruptedWork&&policeRoute&&CityBusinessSystem.HorizontalDistance(officer.transform.position,policeSite.transform.position)<14&&director.ActiveRaids.Count==0&&CityBusinessLedger.Find(d,policeSite.Definition.id).rivalRaidCount==0,"Actual police arrival interrupts the exposed shop raid without crediting NPC loot");
            battle.DespawnMatchdayFighter(officer);
        }
    }
}
public sealed class TrainRaidTestRunner:MonoBehaviour{}
#endif
