using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Mobile-bounded social life at the pub or an away arrival point.</summary>
public sealed class CitySocialActivity : MonoBehaviour
{
    readonly List<GameObject> npcs = new List<GameObject>();
    Vector3 center;
    string venue;
    int targetCount;
    bool matchdayIntensity;
    bool crowdSpawned;

    public int ActiveNpcCount
    {
        get { npcs.RemoveAll(x => !x); return npcs.Count; }
    }
    public bool VenueVisualsReady { get; private set; }
    public string Venue => venue;

    public void IncreaseForMatchday(int extra)
    {
        if(matchdayIntensity||!venue.Contains("PUB"))return;
        extra=Mathf.Clamp(extra,1,8);
        matchdayIntensity=true;
        targetCount+=extra;
        if(crowdSpawned) StartCoroutine(SpawnAdditional(extra));
    }

    IEnumerator SpawnAdditional(int count)
    {
        float timeout=10f;PedestrianSpawner spawner=null;
        while(timeout>0f){spawner=FindFirstObjectByType<PedestrianSpawner>();if(spawner&&spawner.IsInitialized)break;timeout-=Time.unscaledDeltaTime;yield return null;}
        if(!spawner||!spawner.IsInitialized)yield break;
        for(int i=0;i<count;i++){var npc=spawner.SpawnActivityPedestrian(center,18f,transform,"MATCHDAY PUB",true);if(npc)npcs.Add(npc);yield return null;}
    }

    public static CitySocialActivity EnsurePub(Vector3 pubCenter)
    {
        return Create("Pub Surrounding Social Activity", pubCenter, "LOCAL PUB", 2);
    }

    public static CitySocialActivity EnsureArrival(Vector3 arrivalCenter, string arrivalName)
    {
        return Create("Arrival Area Social Activity", arrivalCenter, arrivalName + " ARRIVAL", 2);
    }

    static CitySocialActivity Create(string objectName, Vector3 position, string venueName, int count)
    {
        var go = new GameObject(objectName);
        go.transform.position = position;
        var activity = go.AddComponent<CitySocialActivity>();
        activity.center = position;
        activity.venue = venueName;
        activity.targetCount = count;
        return activity;
    }

    IEnumerator Start()
    {
        BuildVenueVisuals();
        PedestrianSpawner spawner = null;
        float timeout = 12f;
        while (timeout > 0f)
        {
            spawner = FindFirstObjectByType<PedestrianSpawner>();
            if (spawner && spawner.IsInitialized) break;
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (!spawner || !spawner.IsInitialized) yield break;

        for (int i = 0; i < targetCount; i++)
        {
            var npc = spawner.SpawnActivityPedestrian(center, venue.Contains("PUB") ? 15f : 19f, transform, venue, true);
            if (npc) npcs.Add(npc);
            yield return null;
        }
        crowdSpawned = true;
    }

    void BuildVenueVisuals()
    {
        string text = venue.Contains("PUB") ? "PUB" : "ARRIVAL";
        var label = ZoneLabelUtil.Create(transform, text, 4.4f, 6.6f);
        label.name = "SocialActivityLabel";
        label.alignment = TextAlignmentOptions.Center;

        // The city scenes already contain authored street furniture. The old
        // procedural cargo/canopy cubes looked like debug geometry and are not
        // suitable for a production build; the crowd and interaction remain.
        VenueVisualsReady = true;
    }

    void BuildPub(Color accent)
    {
        for (int i = -1; i <= 1; i++)
        {
            Vector3 p = new Vector3(i * 5f, 0f, 7f);
            Primitive(PrimitiveType.Cylinder, "Outdoor Pub Table", p + Vector3.up * .75f, new Vector3(1.5f, .12f, 1.5f), new Color(.30f, .18f, .10f));
            for (int s = 0; s < 4; s++)
            {
                float a = s * Mathf.PI * .5f;
                Primitive(PrimitiveType.Cylinder, "Pub Stool", p + new Vector3(Mathf.Cos(a) * 1.55f, .42f, Mathf.Sin(a) * 1.55f), new Vector3(.48f, .42f, .48f), accent);
            }
        }
        Primitive(PrimitiveType.Cube, "Pub Activity Canopy", new Vector3(0f, 3.1f, 7f), new Vector3(17f, .25f, 5.2f), Color.Lerp(accent, Color.white, .35f));
    }

    void BuildArrival(Color accent)
    {
        for (int i = -3; i <= 3; i++)
            Primitive(PrimitiveType.Cylinder, "Arrival Bollard", new Vector3(i * 3f, .6f, 5f), new Vector3(.30f, .60f, .30f), accent);
        for (int i = 0; i < 5; i++)
            Primitive(PrimitiveType.Cube, "Arrival Cargo", new Vector3(-8f + i * 4f, .7f, -5f + (i % 2) * 2f), new Vector3(2.6f, 1.4f, 2f), i % 2 == 0 ? accent : new Color(.88f, .58f, .12f));
        Primitive(PrimitiveType.Cube, "Arrival Shelter", new Vector3(0f, 3f, 9f), new Vector3(20f, .28f, 5f), Color.Lerp(accent, Color.white, .4f));
    }

    void Primitive(PrimitiveType type, string objectName, Vector3 localPosition, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = objectName;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>(); if (collider) Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        if (renderer)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader) renderer.material = new Material(shader) { color = color };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
