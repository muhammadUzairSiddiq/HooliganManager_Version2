using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Persistent route endpoints and scout goals, owned by the assigned crew.</summary>
public sealed class RtsOrderVisual : MonoBehaviour
{
    List<AgentController> crew;
    Vector3 destination;
    Vector3 routeStart, routeEnd;
    EnemyController attackTarget;
    bool scouting, complete;
    public static RtsOrderVisual Patrol(Vector3 a,Vector3 b,IEnumerable<AgentController> members)
    {
        var visual=new GameObject("Patrol A-B").AddComponent<RtsOrderVisual>();visual.crew=members.ToList();visual.routeStart=a;visual.routeEnd=b;
        Pin(visual.transform,a,"A",Color.cyan);Pin(visual.transform,b,"B",Color.cyan);
        var route=RtsDutyBoundary.MakeLine("Patrol route");route.transform.SetParent(visual.transform);route.loop=false;route.positionCount=2;route.alignment=LineAlignment.View;
        route.startColor=route.endColor=Color.cyan;route.SetPositions(new[]{a+Vector3.up*.2f,b+Vector3.up*.2f});
        return visual;
    }
    public static void Attack(EnemyController target,IEnumerable<AgentController> members)
    {
        var visual=new GameObject("Attack target").AddComponent<RtsOrderVisual>();visual.crew=members.ToList();visual.attackTarget=target;
        visual.transform.position=target.transform.position;Pin(visual.transform,target.transform.position,"ATTACK",LandscapeUI.Gold);
    }
    public static void Scout(Vector3 point,IEnumerable<AgentController> members)
    {
        var accepted=members.Where(a=>a&&a.TryCommandMoveTo(point)).ToList();if(accepted.Count==0)return;
        var visual=new GameObject("Scout objective").AddComponent<RtsOrderVisual>();visual.crew=accepted;visual.scouting=true;visual.destination=point;
        Pin(visual.transform,point,"SCOUT",Color.cyan);
        CityGameplay.Instance?.PostEvent("SCOUT · reach the marker for local intel");
    }
    public static GameObject Pin(Transform parent,Vector3 point,string label,Color colour)
    {
        var pin=new GameObject(label+" marker");pin.transform.SetParent(parent);pin.transform.position=point;
        var ring=ZoneVolumeFactory.BuildFlatCircle(pin.transform,"Boundary",2.3f,.2f,colour,true,24);
        var pole=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pole.transform.SetParent(pin.transform,false);pole.transform.localPosition=Vector3.up*1.2f;pole.transform.localScale=new Vector3(.2f,1.2f,.2f);
        Destroy(pole.GetComponent<Collider>());
        var shader=Shader.Find("Universal Render Pipeline/Unlit");var material=new Material(shader){color=colour};pole.GetComponent<Renderer>().sharedMaterial=material;
        pole.AddComponent<OrderPinMaterial>();
        var caption=ZoneLabelUtil.Create(pin.transform,label,.5f,6f);
        caption.fontSize=12f;
        caption.color=colour;
        caption.GetComponent<ZoneLabelBillboard>().ForceWhite=false;
        caption.GetComponent<ZoneLabelBillboard>().CommandPriority=true;return pin;
    }
    void Update()
    {
        if(complete||crew==null)return;
        if(attackTarget)
        {
            if(!attackTarget.IsAlive||!crew.Any(a=>a&&a.IsAlive&&a.IsTargetingFirm(attackTarget.firmName))){Destroy(gameObject);return;}
            transform.position=attackTarget.transform.position;return;
        }
        crew.RemoveAll(a=>!a||!a.IsAlive||(!scouting&&!a.HasPatrolRoute(routeStart,routeEnd)));
        if(crew.Count==0){Destroy(gameObject);return;}
        if(!scouting)return;
        crew.RemoveAll(a=>(a.CommandDestination-destination).sqrMagnitude>25f);
        if(crew.Count==0){Destroy(gameObject);return;}
        if(!crew.Any(a=>(a.transform.position-destination).sqrMagnitude<36))return;
        int rivals=BattleManager.instance.EnemyAgents.Count(e=>e&&e.IsAlive&&!e.IsHomeMatchdaySupporter&&e.firmName!="POLICE"&&(e.transform.position-destination).sqrMagnitude<45*45);
        var data=GameManager.Data;
        if(data!=null)
        {
            string key="SCOUT:"+Mathf.RoundToInt(destination.x/20)+":"+Mathf.RoundToInt(destination.z/20);
            data.CampaignActionKeys??=new List<string>();
            if(!data.CampaignActionKeys.Contains(key)){data.CampaignActionKeys.Add(key);data.CityIntel=Mathf.Min(10,data.CityIntel+1);GameManager.Save();}
        }
        CityGameplay.Instance?.PostEvent("SCOUTED · "+rivals+" rivals nearby");complete=true;Destroy(gameObject,8f);
    }
}
public sealed class OrderPinMaterial : MonoBehaviour
{
    void OnDestroy(){var renderer=GetComponent<Renderer>();if(renderer&&renderer.sharedMaterial)Destroy(renderer.sharedMaterial);}
}
