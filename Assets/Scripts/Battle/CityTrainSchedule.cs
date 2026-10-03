using UnityEngine;
using ITHappy;
public sealed class CityTrainSchedule : MonoBehaviour
{
    SplineSpeed[] carriages;
    Vector3 stop;
    float until, nextStop;
    bool stopped;
    void Start()
    {
        var train=GameObject.Find("Train");if(!train){enabled=false;return;}
        carriages=train.GetComponentsInChildren<SplineSpeed>();if(carriages.Length==0){enabled=false;return;}
        foreach(var carriage in carriages){carriage.RuntimeSpeedMultiplier=.18f;carriage.RuntimeHeightOffset=-8f;}
        var city=CityGameplay.Instance;
        Vector3 station=city&&city.Locations.Length>7?city.Locations[7]:train.transform.position;
        stop=carriages[0].NearestRoutePoint(station);
    }
    void Update()
    {
        if(stopped)
        {
            if(Time.time<until)return;
            foreach(var carriage in carriages)if(carriage)carriage.StationStop=false;
            stopped=false;nextStop=Time.time+90f;return;
        }
        if(Time.time<nextStop||!carriages[0]||(carriages[0].transform.position-stop).sqrMagnitude>144)return;
        stopped=true;until=Time.time+20f;
        foreach(var carriage in carriages)if(carriage)carriage.StationStop=true;
        CityGameplay.Instance?.PostEvent("STATION ARRIVAL · supporters are joining their groups");
        StartCoroutine(ArriveSupporters());
        BattleManager.instance?.ReinforceLivingRivals();
    }
    System.Collections.IEnumerator ArriveSupporters()
    {
        var city=CityGameplay.Instance;var battle=BattleManager.instance;
        if(!city||!battle||city.Locations.Length<8)yield break;
        int number=Random.Range(5,11);
        for(int i=0;i<number;i++)
        {
            Vector3 arrival=city.Locations[7]+new Vector3(i%3*2f,0,i/3*2f);
            bool home=i%2==0;
            string firm=home?(GameManager.Data?.FirmName??"HOME SUPPORTERS"):"VISITING SUPPORTERS";
            var body=battle.SpawnMatchdayFighter(arrival,firm,home?new Color(.15f,.65f,.3f):new Color(.8f,.2f,.1f),60,5,home?MatchdayUnitRole.HomeSupporter:MatchdayUnitRole.RivalSupporter,"TRAIN ARRIVAL");
            if(body){body.WalkToRally(city.Locations[5]);StartCoroutine(ReleaseArrival(body));}
            yield return new WaitForSeconds(.3f);
        }
    }
    System.Collections.IEnumerator ReleaseArrival(EnemyController body)
    {
        yield return new WaitForSeconds(180);
        if(body&&BattleManager.instance)BattleManager.instance.DespawnMatchdayFighter(body);
    }
    void OnDisable(){if(carriages!=null)foreach(var carriage in carriages)if(carriage){carriage.StationStop=false;carriage.RuntimeSpeedMultiplier=1f;carriage.RuntimeHeightOffset=0;}}
}
