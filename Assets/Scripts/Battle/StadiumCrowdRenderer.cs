using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>Persistent group records, shared baked poses and bounded instanced rendering.</summary>
public sealed class StadiumCrowdRenderer : MonoBehaviour
{
    public const int Capacity=500;
    sealed class Group
    {
        public Vector3 center,home;
        public Vector3[] route;
        public int corner,count,faction,gesture,formation;
        public float yaw,nextRoute;
        public bool cheering;
        public readonly Matrix4x4[] matrices=new Matrix4x4[15];
        public readonly Matrix4x4[] batch=new Matrix4x4[15];
    }
    readonly List<Group> groups=new List<Group>(64);
    readonly Material[] kits=new Material[4];
    CrowdPoseLibrary library;
    Camera view;
    float moraleClock,nextSimulation,lastSimulation;
    int attendance=Capacity,routeCursor;
    public int Population{get;private set;}
    public int GroupCount=>groups.Count;
    public void AppendHeat(List<CityHeatSample> target)
    {
        foreach(var group in groups)target.Add(new CityHeatSample(group.center,group.count));
    }
    public int MovingGroupCount{get{int n=0;foreach(var g in groups)if(!g.cheering)n++;return n;}}
    public bool GroupSizesValid=>groups.TrueForAll(g=>g.count>=3&&g.count<=15);
    public int VerticesPerSupporter=>library&&library.variants.Length>0?library.variants[0].poses[0].vertexCount:0;
    IEnumerator Start()
    {
        library=Resources.Load<CrowdPoseLibrary>("FloreswaCrowd/Poses");view=Camera.main;
        if(!library||library.variants.Length<10||!SystemInfo.supportsInstancing){enabled=false;yield break;}
        var shader=Shader.Find("Hooligan/InstancedSupporter");if(!shader){enabled=false;yield break;}
        var colours=new[]{new Color(.38f,.88f,.14f),new Color(.8f,.16f,.1f),new Color(.7f,.25f,.85f),new Color(.85f,.65f,.12f)};
        for(int i=0;i<4;i++){kits[i]=new Material(shader){enableInstancing=true};kits[i].SetColor("_BaseColor",colours[i]);}
        var random=new System.Random(8217);
        for(int attempt=0;attempt<1600&&Population<Capacity;attempt++)
        {
            bool cheering=groups.Count%8==0;
            var home=transform.position;
            if(!cheering&&CityGameplay.Instance&&CityGameplay.Instance.Locations.Length>0)
                home=CityGameplay.Instance.Locations[groups.Count%CityGameplay.Instance.Locations.Length];
            float angle=(float)random.NextDouble()*Mathf.PI*2;
            float radius=cheering?12+(float)random.NextDouble()*35:25+(float)random.NextDouble()*85;
            var guess=home+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
            if(!CityActivityStreaming.TryStreetPoint(guess,out var point,10))continue;
            bool occupied=false;foreach(var other in groups)if((other.center-point).sqrMagnitude<12*12){occupied=true;break;}
            if(occupied)continue;
            int count=Mathf.Min(random.Next(3,16),Capacity-Population);
            if(Capacity-Population-count>0&&Capacity-Population-count<3)count-=3-(Capacity-Population-count);
            if(count<3)break;
            groups.Add(new Group{center=point,home=point,count=count,faction=groups.Count%4,gesture=groups.Count%9,formation=groups.Count%3,cheering=cheering,yaw=(float)random.NextDouble()*360});
            Population+=count;
        }
        lastSimulation=Time.time;
        yield return null;
    }
    void Update()
    {
        if(!library||!view||groups.Count==0)return;
        moraleClock+=Time.deltaTime;
        if(moraleClock>=120)
        {
            moraleClock=0;bool supported=false;
            if(BattleManager.instance)foreach(var a in BattleManager.instance.PlayerAgents)
                if(a&&a.IsAlive&&(a.transform.position-transform.position).sqrMagnitude<80*80){supported=true;break;}
            var data=GameManager.Data;
            if(data!=null){data.FanMorale=Mathf.Clamp(data.FanMorale+(supported?2:-2),0,100);attendance=data.FanMorale<35?Mathf.Max(250,attendance-15):Mathf.Min(Capacity,attendance+10);}
        }
        // One path request per frame; distant groups have no Animator or agent.
        var routed=groups[routeCursor++%groups.Count];
        if(!routed.cheering&&Time.time>=routed.nextRoute&&(routed.route==null||routed.corner>=routed.route.Length))PlanRoute(routed);
        if(Time.time>=nextSimulation)
        {
            float dt=Mathf.Min(.5f,Time.time-lastSimulation);lastSimulation=Time.time;nextSimulation=Time.time+.1f;
            foreach(var group in groups)
            {
                if(!group.cheering&&group.route!=null&&group.corner<group.route.Length)
                {
                    var target=group.route[group.corner];var delta=target-group.center;
                    if(delta.sqrMagnitude<.5f){group.corner++;if(group.corner>=group.route.Length)group.nextRoute=0;}
                    else{group.yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;group.center=Vector3.MoveTowards(group.center,target,dt*2.2f);}
                }
                var rotation=Quaternion.Euler(0,group.yaw,0);
                for(int i=0;i<group.count;i++)
                {
                    int width=group.formation==0?1:group.formation==1?Mathf.Min(5,group.count):3;
                    var offset=new Vector3((i%width-(width-1)*.5f)*1.7f+Mathf.Sin(i*2.3f)*.3f,0,i/width*1.8f+Mathf.Cos(i*1.7f)*.3f);
                    group.matrices[i]=Matrix4x4.TRS(group.center+rotation*offset,rotation,Vector3.one*GameplayTuning.Current.characterScale);
                }
            }
        }
        int remaining=attendance;
        foreach(var group in groups)
        {
            int count=Mathf.Min(group.count,remaining);remaining-=count;if(count<=0)break;
            if(group.faction==0&&GameManager.Data!=null)count=Mathf.Min(count,Mathf.Clamp(3+GameManager.Data.StrategySupporters/12,3,15));
            if(!CityActivityStreaming.Interested(group.center,true))continue;
            int action=group.cheering?group.gesture:9;
            var variant=library.variants[action];
            int pose=(Mathf.FloorToInt(Time.time*variant.poses.Length/Mathf.Max(.2f,variant.duration))+group.gesture)%variant.poses.Length;
            // Three phase cohorts prevent synchronized marching without per-person Animators.
            for(int phase=0;phase<3;phase++)
            {
                int n=0;for(int i=phase;i<count;i+=3)group.batch[n++]=group.matrices[i];
                if(n>0)Graphics.DrawMeshInstanced(variant.poses[(pose+phase*5)%variant.poses.Length],0,kits[group.faction],group.batch,n,null,ShadowCastingMode.Off,false,0,view,LightProbeUsage.Off);
            }
        }
    }
    void PlanRoute(Group group)
    {
        group.nextRoute=Time.time+3;
        var guess=group.home+new Vector3(Random.Range(-65f,65f),0,Random.Range(-65f,65f));
        if(!CityActivityStreaming.TryStreetPoint(guess,out var target,8))return;
        var path=new NavMeshPath();
        if(!NavMesh.CalculatePath(group.center,target,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)return;
        group.route=path.corners;group.corner=1;
    }
    void OnDestroy(){foreach(var material in kits)if(material)Destroy(material);}
}
