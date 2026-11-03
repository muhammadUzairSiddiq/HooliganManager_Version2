using System.Linq;
using UnityEngine;

/// <summary>Extraction is a physical regroup, requested by the player.</summary>
public sealed class AwayExtractionSystem : MonoBehaviour
{
    public static AwayExtractionSystem Instance { get; private set; }
    public const float HoldSeconds=8f;
    public bool IsExtracting { get; private set; }
    public string Status { get; private set; }="Complete district goals, then extract at ARRIVAL.";
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public void RequestExtraction()
    {
        var city=CityGameplay.Instance;
        if(!city||CityGameplay.HomeMode)return;
        CameraPanTouchOnly.Instance?.FocusOn(city.Home);
        if(!CampaignMissions.ReadyForExtraction(GameManager.Data))
        {city.PostEvent("EXTRACTION LOCKED - complete the remaining district objectives in the mission journal.");return;}
        IsExtracting=true;
        Status="Bring every surviving traveller to ARRIVAL. Hold the clear zone for 8 seconds.";
        city.PostEvent(Status);
        // Only the chosen group moves. Other crews keep their orders until the
        // player deliberately regroups them; extraction never selects everyone.
        AgentSelectionManager.instance?.CommandSelectedMoveTo(city.Home);
    }
    void Update()
    {
        if(!IsExtracting||CityGameplay.HomeMode||!BattleManager.instance)return;
        var d=GameManager.Data;
        var alive=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();
        bool covered=alive.Length>0&&alive.All(a=>CityBusinessSystem.HorizontalDistance(a.transform.position,CityGameplay.Instance.Home)<13f)
            &&!CityBusinessSystem.HostilesNear(CityGameplay.Instance.Home,18f);
        d.AwayExtractionSeconds=covered?d.AwayExtractionSeconds+Time.deltaTime:0f;
        Status=covered?$"EXTRACTION {d.AwayExtractionSeconds:0}/{HoldSeconds:0}s":"Regroup all surviving travellers at ARRIVAL; clear nearby rivals.";
        if(d.AwayExtractionSeconds<HoldSeconds)return;
        IsExtracting=false;
        CampaignMissions.RecordAction(d,"extraction","departed");
        BattleManager.instance.PersistBattleProgress();
        CityGameplay.Instance.CheckCampaignCompletion();
    }
}
