using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Camera/crew interest policy. Only disposable presentation is streamed;
/// persistent crew, combat near crew, navigation and mission state remain authoritative.</summary>
public sealed class CityActivityStreaming : MonoBehaviour
{
    public static CityActivityStreaming Instance { get; private set; }
    public static int BodyBudget => Application.isMobilePlatform ? 64 : 96;
    public int LiveAmbientBodies { get; private set; }
    Camera view;
    Vector3 lastFocus, predictedFocus;
    float footprint=65f, nextAudit;
    public float PreloadRadius=>footprint+55f;
    int spawnFrame=-1, spawnedThisFrame;
    public static void Ensure()
    {
        if(!Instance)new GameObject("City Activity Streaming").AddComponent<CityActivityStreaming>();
    }
    void Awake(){Instance=this;view=Camera.main;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    void Update()
    {
        if(!view)view=Camera.main;
        if(!view)return;
        Vector3 focus=GroundFocus(view);
        var velocity=(focus-lastFocus)/Mathf.Max(.01f,Time.unscaledDeltaTime);
        predictedFocus=focus+Vector3.ClampMagnitude(velocity*.8f,65f);
        lastFocus=focus;
        footprint=45f;
        var ground=new Plane(Vector3.up,Vector3.zero);
        for(int i=0;i<4;i++)
        {
            var ray=view.ViewportPointToRay(new Vector3(i%2,i/2,0));
            if(ground.Raycast(ray,out var distance))footprint=Mathf.Max(footprint,Vector3.Distance(focus,ray.GetPoint(distance)));
        }
        footprint=Mathf.Min(footprint,300f);
        if(Time.unscaledTime<nextAudit)return;
        nextAudit=Time.unscaledTime+.5f;
        LiveAmbientBodies=FindObjectsByType<PedestrianController>(FindObjectsSortMode.None).Length;
        var bm=BattleManager.instance;
        if(bm)foreach(var e in bm.EnemyAgents)if(e&&e.IsAlive&&e.IsAmbientMatchdayUnit)LiveAmbientBodies++;
    }
    public static Vector3 GroundFocus(Camera cam)
    {
        if(!cam)return Vector3.zero;
        var ray=cam.ViewportPointToRay(new Vector3(.5f,.5f));
        return new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float enter)?ray.GetPoint(enter):cam.transform.position;
    }
    public static bool Interested(Vector3 position,bool retained=false)
    {
        var self=Instance;
        if(!self)return false;
        float margin=retained?85f:55f;
        Vector3 delta=position-self.predictedFocus;delta.y=0;
        if(delta.sqrMagnitude<(self.footprint+margin)*(self.footprint+margin)||(position-self.lastFocus).sqrMagnitude<(self.footprint+margin)*(self.footprint+margin))return true;
        // Looking away never makes a player's ongoing encounter disappear.
        var bm=BattleManager.instance;
        if(bm)foreach(var a in bm.PlayerAgents)
            if(a&&a.IsAlive&&(a.transform.position-position).sqrMagnitude<32f*32f)return true;
        return false;
    }
    public static bool TryReserveBody(Vector3 position)
    {
        if(!Interested(position))return false;
        var self=Instance;
        if(self.spawnFrame!=Time.frameCount){self.spawnFrame=Time.frameCount;self.spawnedThisFrame=0;}
        if(self.LiveAmbientBodies>=BodyBudget||self.spawnedThisFrame>=(Application.isMobilePlatform?8:16))return false;
        self.spawnedThisFrame++;self.LiveAmbientBodies++;
        return true;
    }
    public static void ReleaseBody(){if(Instance)Instance.LiveAmbientBodies=Mathf.Max(0,Instance.LiveAmbientBodies-1);}
    public static bool InView(Vector3 point,float padding=.1f)
    {
        var camera=Instance?Instance.view:Camera.main;if(!camera)return false;
        var p=camera.WorldToViewportPoint(point);
        return p.z>0&&p.x>-padding&&p.x<1+padding&&p.y>-padding&&p.y<1+padding;
    }
    public static bool TryStreetPoint(Vector3 candidate,out Vector3 point,float radius=6f)
    {
        point=candidate;
        if(!NavMesh.SamplePosition(candidate,out var hit,radius,NavMesh.AllAreas))return false;
        if(Mathf.Abs(hit.position.y-candidate.y)>4f)return false;
        // Exclude covered bridge/rail approaches instead of putting task prompts under them.
        if(Physics.Raycast(hit.position+Vector3.up*2f,Vector3.up,12f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return false;
        point=hit.position;return true;
    }
}

/// <summary>Data-light social venues create visitors only in an interest region.</summary>
public sealed class StreamedSocialCrowd : MonoBehaviour
{
    public readonly List<GameObject> People=new List<GameObject>();
    public string Venue;
    public int Count=2;
    public float Radius=12f;
    PedestrianSpawner spawner;
    IEnumerator Start()
    {
        CityActivityStreaming.Ensure();
        while(true)
        {
            People.RemoveAll(p=>!p);
            if(!CityActivityStreaming.Interested(transform.position,true))
            {
                foreach(var person in People)if(person)Destroy(person);
                People.Clear();
            }
            else if(People.Count<Count)
            {
                if(!spawner)spawner=FindFirstObjectByType<PedestrianSpawner>();
                if(spawner&&spawner.IsInitialized)
                {
                    var npc=spawner.SpawnActivityPedestrian(transform.position,Radius,transform,Venue,true);
                    if(npc)People.Add(npc);
                }
            }
            yield return new WaitForSeconds(.6f);
        }
    }
    void OnDestroy(){foreach(var p in People)if(p)Destroy(p);}
}
