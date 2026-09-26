using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Distinct, lightweight social scenes that make important districts feel inhabited.</summary>
public sealed class CityLifeActivity:MonoBehaviour
{
    readonly List<GameObject> people=new List<GameObject>();
    string activityType;
    Vector3 center;
    int targetCount;
    public string ActivityType=>activityType;
    public bool VisualsReady{get;private set;}
    StreamedSocialCrowd streamed;
    public int ActiveNpcCount=>streamed?streamed.People.Count:0;

    public static CityLifeActivity Ensure(string type,Vector3 position,int npcCount=1)
    {
        var go=new GameObject(type+" Social Activity");go.transform.position=position;
        var activity=go.AddComponent<CityLifeActivity>();activity.activityType=type.ToUpperInvariant();activity.center=position;activity.targetCount=Mathf.Clamp(npcCount,1,4);
        return activity;
    }

    bool matchdayIntensity;
    bool crowdSpawned;
    public void IncreaseForMatchday(int extra)
    {
        if(matchdayIntensity)return;
        matchdayIntensity=true;
        extra=Mathf.Clamp(extra,1,6);
        targetCount+=extra;
        if(streamed)streamed.Count=Mathf.Min(4,targetCount);
    }

    IEnumerator SpawnAdditional(int count)
    {
        PedestrianSpawner spawner=null;float timeout=10f;
        while(timeout>0)
        {
            spawner=FindFirstObjectByType<PedestrianSpawner>();
            if(spawner&&spawner.IsInitialized)break;
            timeout-=Time.unscaledDeltaTime;yield return null;
        }
        if(!spawner||!spawner.IsInitialized)yield break;
        for(int i=0;i<count;i++)
        {
            var npc=spawner.SpawnActivityPedestrian(center,12f,transform,activityType,true);
            if(npc){npc.name=activityType+" Matchday Visitor";people.Add(npc);}yield return null;
        }
    }

    IEnumerator Start()
    {
        BuildVisuals();
        streamed=gameObject.AddComponent<StreamedSocialCrowd>();
        streamed.Venue=activityType;streamed.Count=Mathf.Min(4,targetCount);
        yield break;
    }

    void BuildVisuals()
    {
        // The destination already has a pin. Avoid a second stacked venue title.
        // Use the authored city environment plus real NPC activity. Procedural
        // cube stalls, shelters and seats were placeholder geometry and have
        // deliberately been retired for the production presentation.
        VisualsReady=true;
    }

    void BuildMarket(Color accent)
    {
        for(int i=-2;i<=2;i++)
        {
            Primitive(PrimitiveType.Cube,"Market Counter",new Vector3(i*3.5f,1f,3f),new Vector3(2.8f,2f,1.5f),new Color(.35f,.22f,.12f));
            Primitive(PrimitiveType.Cube,"Market Awning",new Vector3(i*3.5f,2.35f,3f),new Vector3(3.1f,.22f,1.9f),i%2==0?accent:Color.white);
            Primitive(PrimitiveType.Cube,"Market Crate",new Vector3(i*3.5f,.35f,.8f),new Vector3(1.1f,.7f,1.1f),accent);
        }
    }

    void BuildPark(Color accent)
    {
        for(int i=-1;i<=1;i++)
        {
            Primitive(PrimitiveType.Cylinder,"Community Table",new Vector3(i*5f,.65f,2f),new Vector3(1.4f,.12f,1.4f),new Color(.38f,.24f,.12f));
            Primitive(PrimitiveType.Cube,"Park Seat",new Vector3(i*5f,.45f,-.3f),new Vector3(3f,.45f,.7f),accent);
        }
        Primitive(PrimitiveType.Cube,"Community Noticeboard",new Vector3(0,1.7f,6f),new Vector3(5f,3.4f,.2f),Color.Lerp(accent,Color.white,.35f));
    }

    void BuildTransit(Color accent)
    {
        Primitive(PrimitiveType.Cube,"Transit Shelter",new Vector3(0,2.6f,4f),new Vector3(15f,.25f,4.2f),Color.Lerp(accent,Color.white,.35f));
        for(int i=-3;i<=3;i++)
        {
            Primitive(PrimitiveType.Cylinder,"Queue Marker",new Vector3(i*2.2f,.45f,0),new Vector3(.2f,.45f,.2f),accent);
            if(i%2==0)Primitive(PrimitiveType.Cube,"Travel Bag",new Vector3(i*2.2f,.35f,2.5f),new Vector3(.8f,.7f,.5f),new Color(.22f,.25f,.29f));
        }
    }

    void BuildFoodAndMusic(Color accent)
    {
        for(int i=-1;i<=1;i++)
        {
            Primitive(PrimitiveType.Cube,"Food Stall",new Vector3(i*5f,1.2f,3f),new Vector3(4f,2.4f,2.2f),i%2==0?accent:new Color(.94f,.68f,.18f));
            Primitive(PrimitiveType.Cube,"Stall Canopy",new Vector3(i*5f,2.75f,3f),new Vector3(4.6f,.22f,2.8f),Color.white);
        }
        Primitive(PrimitiveType.Cylinder,"Street Performer Stage",new Vector3(0,.12f,-4f),new Vector3(3f,.12f,3f),accent);
    }

    void Primitive(PrimitiveType type,string objectName,Vector3 localPosition,Vector3 scale,Color color)
    {
        var go=GameObject.CreatePrimitive(type);go.name=objectName;go.transform.SetParent(transform,false);go.transform.localPosition=localPosition;go.transform.localScale=scale;
        var collider=go.GetComponent<Collider>();if(collider)Destroy(collider);
        var renderer=go.GetComponent<Renderer>();if(!renderer)return;
        var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");if(shader)renderer.material=new Material(shader){color=color};
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
}
