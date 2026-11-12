using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ITHappy;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Authored train stops dispatch staggered, connected pedestrian groups within the city actor budget.</summary>
public sealed class CityTrainSchedule:MonoBehaviour
{
    public sealed class ArrivalGroup
    {
        public string Kind,Firm,Token;
        public Vector3 Exit,Destination;
        public readonly List<EnemyController> Members=new List<EnemyController>();
        public readonly List<Vector3> SpawnPoints=new List<Vector3>();
    }
    sealed class ArrivalBody{public EnemyController Actor;public string Token;public float Expires,ReachedAt;public Vector3 Destination;public bool Returning;}
    public const int MaximumArrivalBodies=18;
    public const int MaximumGroupsPerStop=3;
    public static CityTrainSchedule Instance{get;private set;}
    public bool HasService=>carriages!=null&&carriages.Length>0;
    public bool IsStopped=>stopped;
    public Vector3 StationStopPoint=>stop;
    public int ArrivalWaveCount{get;private set;}
    public bool AutomaticArrivalsEnabled{get;set;}=true;
    public bool DispatchFinished=>!dispatching;
    public IReadOnlyList<string> DispatchDiagnostics=>diagnostics;
    public int LiveArrivalCount=>arrivals.Count(a=>a.Actor&&a.Actor.IsAlive&&a.Actor.MatchdayGroupName==a.Token);
    public IReadOnlyList<ArrivalGroup> ArrivalGroups=>groups;
    readonly List<ArrivalGroup> groups=new List<ArrivalGroup>();
    readonly List<ArrivalBody> arrivals=new List<ArrivalBody>();
    readonly List<string> diagnostics=new List<string>();
    SplineSpeed[] carriages;
    Vector3 stop;
    float until,nextStop,nextCleanup;
    bool stopped,dispatching;
    void Awake(){Instance=this;}
    IEnumerator Start()
    {
        while(!CityGameplay.Instance||CityGameplay.Instance.Locations==null||CityGameplay.Instance.Locations.Length<8)yield return null;
        CityRoadIncidentSystem.Ensure();
        var train=GameObject.Find("Train");if(!train){enabled=false;yield break;}
        carriages=train.GetComponentsInChildren<SplineSpeed>();if(carriages.Length==0){enabled=false;yield break;}
        // Keep the original railway height. The old -8m correction belonged to
        // the superseded viaduct-removal presentation and buried visible trains.
        foreach(var carriage in carriages){carriage.RuntimeSpeedMultiplier=.45f;carriage.RuntimeHeightOffset=0;}
        stop=carriages[0].NearestRoutePoint(CityGameplay.Instance.Locations[7]);
    }
    void Update()
    {
        if(!HasService)return;
        if(Time.time>=nextCleanup){nextCleanup=Time.time+1;CleanupArrivals();}
        if(stopped)
        {
            if(Time.time<until||dispatching)return;
            foreach(var carriage in carriages)if(carriage)carriage.StationStop=false;
            stopped=false;nextStop=Time.time+90;return;
        }
        if(AutomaticArrivalsEnabled&&Time.time>=nextStop)BeginArrivalStop();
    }
    public bool BeginArrivalStop()
    {
        if(!HasService||stopped||!carriages[0]||CityBusinessSystem.HorizontalDistance(carriages[0].transform.position,stop)>12)return false;
        CleanupArrivals();stopped=true;until=Time.time+20;ArrivalWaveCount++;diagnostics.Clear();
        diagnostics.Add($"Wave {ArrivalWaveCount}: live={LiveArrivalCount}/{MaximumArrivalBodies}, shared={CityActivityStreaming.Instance?.LiveAmbientBodies}, station={CityGameplay.Instance.Locations[7]}, carriage={carriages[0].transform.position}");
        foreach(var carriage in carriages)if(carriage)carriage.StationStop=true;
        CityGameplay.Instance?.PostEvent("TRAIN ARRIVAL · home fans, visiting supporters and city travellers leave through separate station exits.");
        GameFeedback.Emit(GameFeedbackEvent.TrainArrival,CityGameplay.Instance.Locations[7]);
        StartCoroutine(ArriveSupporters());return true;
    }
    IEnumerator ArriveSupporters()
    {
        dispatching=true;var city=CityGameplay.Instance;var battle=BattleManager.instance;
        if(!city||!battle||!battle.enemyAgentPrefab){dispatching=false;yield break;}
        groups.Clear();var source=battle.enemyAgentPrefab.GetComponent<NavMeshAgent>();
        var filter=new NavMeshQueryFilter{agentTypeID=source?source.agentTypeID:0,areaMask=source?source.areaMask:NavMesh.AllAreas};
        string[] kinds={"HOME FANS","VISITING SUPPORTERS","CITY TRAVELLERS"};
        var shop=CityBusinessSystem.Instance?CityBusinessSystem.Instance.Sites.Where(s=>s&&s.Definition.id!="pub").OrderBy(s=>CityBusinessSystem.HorizontalDistance(s.transform.position,city.Locations[7])).FirstOrDefault():null;
        Vector3[] targets={city.Locations[1],city.Locations[5],shop?shop.transform.position:Vector3.zero};int[] sizes={4,4,3};
        // A later train shares space with people already visiting venues. Split
        // remaining room across purposes instead of starving the third group.
        int room=MaximumArrivalBodies-LiveArrivalCount;
        if(room<11){sizes=new[]{0,0,0};for(int slot=0;slot<room;slot++)sizes[(slot+ArrivalWaveCount)%3]++;}
        for(int groupIndex=0;groupIndex<MaximumGroupsPerStop;groupIndex++)
        {
            if(LiveArrivalCount>=MaximumArrivalBodies){diagnostics.Add(kinds[groupIndex]+": local actor cap reached");break;}
            if(groupIndex==2&&!shop){diagnostics.Add(kinds[groupIndex]+": no actual city shop available");continue;}
            if(sizes[groupIndex]==0){diagnostics.Add(kinds[groupIndex]+": existing visitors retain the bounded actor allocation");continue;}
            Vector3 destination=targets[groupIndex];
            var carriage=carriages[Mathf.Min(groupIndex,carriages.Length-1)];
            Vector3 projected=carriage?carriage.transform.position:city.Locations[7];projected.y=city.Locations[7].y;
            if(!TryExitAndDestination(projected,city.Locations[7],destination,filter,groups.Select(g=>g.Exit),out var exit,out destination)){diagnostics.Add(kinds[groupIndex]+$": no connected station exit/venue approach; requested={targets[groupIndex]}, type={filter.agentTypeID}, mask={filter.areaMask}");continue;}
            string homeFirm=PoliceCityMode.Active?"HOME SUPPORTERS":GameManager.Data?.FirmName??"HOME FANS";
            var group=new ArrivalGroup{Kind=kinds[groupIndex],Firm=groupIndex==0?homeFirm:groupIndex==1?"VISITING SUPPORTERS":"NEUTRAL CITY TRAVELLERS",Token=$"TRAIN {ArrivalWaveCount}: {kinds[groupIndex]}",Exit=exit,Destination=destination};groups.Add(group);
            int invalidSpawn=0,blockedRoute=0,rejectedBody=0;
            for(int i=0;i<sizes[groupIndex]&&LiveArrivalCount<MaximumArrivalBodies;i++)
            {
                Vector3 candidate=exit+new Vector3((i%2)*1.8f,0,(i/2)*1.8f);
                if(!NavMesh.SamplePosition(candidate,out var spawn,3,filter)||Mathf.Abs(spawn.position.y-city.Locations[7].y)>2.5f){invalidSpawn++;continue;}
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(spawn.position,destination,filter,path)||path.status!=NavMeshPathStatus.PathComplete){blockedRoute++;continue;}
                Color color=groupIndex==0?new Color(.15f,.65f,.3f):groupIndex==1?new Color(.8f,.22f,.14f):new Color(.5f,.62f,.65f);
                // Neutral travellers share the non-hostile supporter movement
                // adapter, but are named separately and never become player crew.
                var role=groupIndex==1?MatchdayUnitRole.RivalSupporter:MatchdayUnitRole.HomeSupporter;
                var body=battle.SpawnMatchdayFighter(spawn.position,group.Firm,color,60,0,role,group.Token);
                if(body)
                {
                    var nav=body.GetComponent<NavMeshAgent>();
                    var actualFilter=new NavMeshQueryFilter{agentTypeID=nav?nav.agentTypeID:0,areaMask=nav?nav.areaMask:0};
                    bool actualRoute=nav&&nav.enabled&&nav.isOnNavMesh&&Mathf.Abs(body.transform.position.y-city.Locations[7].y)<=2.5f&&NavMesh.CalculatePath(body.transform.position,destination,actualFilter,path)&&path.status==NavMeshPathStatus.PathComplete;
                    if(!actualRoute){blockedRoute++;battle.DespawnMatchdayFighter(body);continue;}
                    var march=body.GetComponent<MatchdayMarch>();if(march)march.enabled=false;
                    body.SetCinematicIdle(false);
                    if(body.TryBeginEscort(destination,false))
                    {group.Members.Add(body);group.SpawnPoints.Add(body.transform.position);arrivals.Add(new ArrivalBody{Actor=body,Token=group.Token,Destination=destination,Expires=Time.time+600});CityRoadIncidentSystem.Instance?.TryQueueArrival(body,destination);}
                    else{blockedRoute++;battle.DespawnMatchdayFighter(body);}
                }
                else rejectedBody++;
                yield return new WaitForSeconds(.3f);
            }
            diagnostics.Add(group.Kind+$": members={group.Members.Count}/{sizes[groupIndex]}, exit={exit}, venue={destination}, invalidSpawn={invalidSpawn}, rejectedRoute={blockedRoute}, rejectedBodyOrBudget={rejectedBody}, live={LiveArrivalCount}, shared={CityActivityStreaming.Instance?.LiveAmbientBodies}");
            yield return new WaitForSeconds(.9f);
        }
        dispatching=false;
    }
    public static bool TryExit(Vector3 projected,Vector3 station,Vector3 destination,NavMeshQueryFilter filter,IEnumerable<Vector3> used,out Vector3 exit)
    {return TryExitAndDestination(projected,station,destination,filter,used,out exit,out _);}
    static bool TryExitAndDestination(Vector3 projected,Vector3 station,Vector3 requested,NavMeshQueryFilter filter,IEnumerable<Vector3> used,out Vector3 exit,out Vector3 destination)
    {
        exit=destination=default;var path=new NavMeshPath();var previous=used.ToArray();
        for(int attempt=0;attempt<32;attempt++)
        {
            Vector3 origin=attempt<16?projected:station;int index=attempt%16;float angle=index*Mathf.PI/4;
            var candidate=origin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(3+index/8*7);
            if(!NavMesh.SamplePosition(candidate,out var hit,8,filter)||Mathf.Abs(hit.position.y-station.y)>2.5f||CityBusinessSystem.HorizontalDistance(hit.position,station)>180)continue;
            if(previous.Any(p=>CityBusinessSystem.HorizontalDistance(p,hit.position)<5))continue;
            for(int venueAttempt=0;venueAttempt<25;venueAttempt++)
            {
                float venueAngle=(venueAttempt-1)*Mathf.PI/4;
                Vector3 candidateVenue=requested+(venueAttempt==0?Vector3.zero:new Vector3(Mathf.Cos(venueAngle),0,Mathf.Sin(venueAngle))*(3+((venueAttempt-1)/8)*4));
                if(!NavMesh.SamplePosition(candidateVenue,out var venue,3,filter)||Mathf.Abs(venue.position.y-requested.y)>2.5f||CityBusinessSystem.HorizontalDistance(venue.position,requested)>14)continue;
                if(!NavMesh.CalculatePath(hit.position,venue.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                if(CityBusinessSystem.HorizontalDistance(hit.position,venue.position)<12)continue;
                exit=hit.position;destination=venue.position;return true;
            }
        }
        return false;
    }
    void CleanupArrivals()
    {
        for(int i=arrivals.Count-1;i>=0;i--)
        {
            var entry=arrivals[i];if(!entry.Actor||!entry.Actor.IsAlive||entry.Actor.MatchdayGroupName!=entry.Token){arrivals.RemoveAt(i);continue;}
            if(CityRoadIncidentSystem.IsQueued(entry.Actor))continue;
            if(!entry.Returning&&CityBusinessSystem.HorizontalDistance(entry.Actor.transform.position,entry.Destination)<8)
            {
                if(entry.ReachedAt<=0)entry.ReachedAt=Time.time;
                if(Time.time-entry.ReachedAt>55&&entry.Actor.TryBeginEscort(CityGameplay.Instance.Locations[7],false))entry.Returning=true;
            }
            bool boarded=entry.Returning&&stopped&&CityBusinessSystem.HorizontalDistance(entry.Actor.transform.position,CityGameplay.Instance.Locations[7])<10;
            // Visible visitors never evaporate on a global five-minute timer.
            // Depart at the station, or retire an old body outside the view.
            if(!boarded&&(Time.time<entry.Expires||CityActivityStreaming.InView(entry.Actor.transform.position,.1f)))continue;
            if(BattleManager.instance)BattleManager.instance.DespawnMatchdayFighter(entry.Actor);arrivals.RemoveAt(i);
        }
    }
    void OnDisable()
    {
        StopAllCoroutines();dispatching=false;stopped=false;
        if(carriages!=null)foreach(var carriage in carriages)if(carriage){carriage.StationStop=false;carriage.RuntimeSpeedMultiplier=1;carriage.RuntimeHeightOffset=0;}
        foreach(var entry in arrivals)if(entry.Actor&&entry.Actor.MatchdayGroupName==entry.Token&&BattleManager.instance)BattleManager.instance.DespawnMatchdayFighter(entry.Actor);
        arrivals.Clear();groups.Clear();
    }
    void OnDestroy(){if(Instance==this)Instance=null;}
}
