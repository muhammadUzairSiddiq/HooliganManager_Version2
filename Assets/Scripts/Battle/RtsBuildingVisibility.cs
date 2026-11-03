using System.Collections.Generic;
using UnityEngine;

/// <summary>Touch a building to reveal behind it. Roads, terrain and colliders are never changed.</summary>
public sealed class RtsBuildingVisibility : MonoBehaviour
{
    public static RtsBuildingVisibility Instance { get; private set; }
    readonly List<Renderer> buildings=new List<Renderer>();
    Renderer revealed;
    bool previousHidden;
    float restoreAt;
    public Renderer RevealedBuilding=>revealed;
    public static bool IsBuildingName(string value)
    {
        string name=(value??"").ToLowerInvariant();
        if(name.Contains("platform")||name.Contains("parking")||name.Contains("road_")||name.Contains("bridge")||name.Contains("park_")||name.Contains("ground")||name.Contains("terrain"))return false;
        string[] prefixes={"business_center_","casino_","airport_","college_","cosmodrome_","fire_station_","gas_station_","government_bilding_","hospital_","police_department_","railway_station_","school_","tram_depot_","university_","church_","mosque_","synagogue_","elite_residental_building_","residental_building_","bar_","barbershop_","burger_shop_","cinema_","coffee_shop_","fastfood_","gym_","ice_cream_shop_","library_","mall_","music_store_","pizzeria_","supermarket_","tool_store_","eiffel_tower_","skyscraper_"};
        foreach(var prefix in prefixes)if(name.StartsWith(prefix))return true;
        return false;
    }
    void Awake(){Instance=this;}
    void Start()
    {
        foreach(var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if(IsBuildingName(renderer.name))buildings.Add(renderer);
    }
    public bool TryRevealAt(Vector2 screenPoint)
    {
        if(!Camera.main)return false;
        var ray=Camera.main.ScreenPointToRay(screenPoint);
        Renderer nearest=null;float best=float.MaxValue;
        foreach(var building in buildings)
        {
            if(!building||!building.enabled||!building.gameObject.activeInHierarchy)continue;
            if(building.bounds.IntersectRay(ray,out float distance)&&distance>0&&distance<best){nearest=building;best=distance;}
        }
        if(!nearest)return false;
        if(nearest==revealed){RestoreAll();return true;}
        RestoreAll();revealed=nearest;previousHidden=nearest.forceRenderingOff;
        nearest.forceRenderingOff=true;restoreAt=Time.unscaledTime+12f;
        CityGameplay.Instance?.PostEvent("Building temporarily hidden. Tap it again to restore; streets stay visible.");
        return true;
    }
    public void RestoreAll(){if(revealed)revealed.forceRenderingOff=previousHidden;revealed=null;}
    void Update(){if(revealed&&Time.unscaledTime>=restoreAt)RestoreAll();}
    void OnDisable(){RestoreAll();}
    void OnDestroy(){RestoreAll();if(Instance==this)Instance=null;}
}
