using System.Collections.Generic;
using UnityEngine;

/// <summary>Reveal buildings on touch or while they intersect the camera body.</summary>
public sealed class RtsBuildingVisibility : MonoBehaviour
{
    public static RtsBuildingVisibility Instance { get; private set; }
    readonly List<Renderer> buildings=new List<Renderer>();
    readonly Dictionary<Renderer,bool> cameraHidden = new Dictionary<Renderer,bool>();
    readonly List<Renderer> restored = new List<Renderer>();
    static readonly string[] BuildingPrefixes = {
        "business_center_", "casino_", "airport_", "college_", "cosmodrome_", "fire_station_", "gas_station_",
        "government_bilding_", "hospital_", "police_department_", "railway_station_", "school_", "tram_depot_",
        "university_", "church_", "mosque_", "synagogue_", "elite_residental_building_", "residental_building_",
        "residential_building_", "bar_", "barbershop_", "burger_shop_", "cinema_", "coffee_shop_", "fastfood_",
        "gym_", "ice_cream_shop_", "library_", "mall_", "music_store_", "pizzeria_", "supermarket_",
        "tool_store_", "eiffel_tower_", "skyscraper_", "stadium_", "home_facility_building_"
    };
    Renderer revealed;
    bool previousHidden;
    float restoreAt;
    public Renderer RevealedBuilding=>revealed;
    public static bool IsBuildingName(string value)
    {
        string name=(value??"").ToLowerInvariant();
        if(IsStreetSurface(name))return false;
        foreach(var prefix in BuildingPrefixes)if(name.StartsWith(prefix))return true;
        return false;
    }
    static bool IsStreetSurface(string name) =>
        name.Contains("platform") || name.Contains("parking") || name.Contains("road") ||
        name.Contains("bridge") || name.Contains("park_") || name.Contains("ground") ||
        name.Contains("terrain") || name.Contains("sidewalk") || name.Contains("pavement") ||
        name.Contains("rail_track") || name.StartsWith("rail_") || name.Contains("crosswalk");

    static bool IsBuildingRenderer(Renderer renderer)
    {
        if(renderer.TryGetComponent<MeshFilter>(out var mesh)&&mesh.sharedMesh&&
            IsStreetSurface(mesh.sharedMesh.name.ToLowerInvariant()))return false;
        for(var node=renderer.transform;node;node=node.parent)
        {
            if(IsStreetSurface(node.name.ToLowerInvariant()))return false;
            if(IsBuildingName(node.name))return true;
        }
        return false;
    }
    void Awake(){Instance=this;}
    public static void RegisterFacility(GameObject model)
    {
        if(!model)return;
        foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            string name=renderer.name.ToLowerInvariant();
            if(IsStreetSurface(name))continue;
            if(!renderer.name.StartsWith("home_facility_building_"))renderer.name="home_facility_building_"+renderer.name;
            if(Instance&&!Instance.buildings.Contains(renderer))Instance.buildings.Add(renderer);
        }
        if(Instance)Instance.buildings.RemoveAll(r=>!r);
    }
    void Start()
    {
        foreach(var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if(IsBuildingRenderer(renderer)&&!buildings.Contains(renderer))buildings.Add(renderer);
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
        if(nearest==revealed){RestoreReveal();return true;}
        RestoreReveal();revealed=nearest;
        previousHidden=cameraHidden.TryGetValue(nearest,out var original)?original:nearest.forceRenderingOff;
        nearest.forceRenderingOff=true;restoreAt=Time.unscaledTime+12f;
        CityGameplay.Instance?.PostEvent("Building temporarily hidden. Tap it again to restore; streets stay visible.");
        return true;
    }
    void RestoreReveal()
    {
        if(revealed)revealed.forceRenderingOff=cameraHidden.ContainsKey(revealed)||previousHidden;
        revealed=null;
    }
    public void RestoreAll()
    {
        RestoreReveal();
        foreach(var entry in cameraHidden)if(entry.Key)entry.Key.forceRenderingOff=entry.Value;
        cameraHidden.Clear();
    }
    void LateUpdate()
    {
        if(revealed&&Time.unscaledTime>=restoreAt)RestoreReveal();
        var camera=Camera.main;
        if(!camera){RestoreAll();return;}
        float near=camera.nearClipPlane;
        float halfHeight=camera.orthographic?camera.orthographicSize:near*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
        float radius=Mathf.Clamp(halfHeight*Mathf.Max(1f,camera.aspect)+.35f,.65f,2.5f);
        Vector3 position=camera.transform.position;
        Vector3 nearCenter=position+camera.transform.forward*near;
        foreach(var renderer in buildings)
        {
            if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
            var bounds=renderer.bounds;
            if(bounds.SqrDistance(position)>radius*radius&&bounds.SqrDistance(nearCenter)>radius*radius)continue;
            if(!cameraHidden.ContainsKey(renderer))
                cameraHidden.Add(renderer,renderer==revealed?previousHidden:renderer.forceRenderingOff);
            renderer.forceRenderingOff=true;
        }
        restored.Clear();
        foreach(var entry in cameraHidden)
        {
            var renderer=entry.Key;
            if(renderer&&renderer.enabled&&renderer.gameObject.activeInHierarchy)
            {
                float exitRadius=radius+.2f;
                var bounds=renderer.bounds;
                if(bounds.SqrDistance(position)<=exitRadius*exitRadius||bounds.SqrDistance(nearCenter)<=exitRadius*exitRadius)continue;
            }
            if(renderer)renderer.forceRenderingOff=renderer==revealed||entry.Value;
            restored.Add(renderer);
        }
        foreach(var renderer in restored)cameraHidden.Remove(renderer);
    }
    void OnDisable(){RestoreAll();}
    void OnDestroy(){RestoreAll();if(Instance==this)Instance=null;}
}
