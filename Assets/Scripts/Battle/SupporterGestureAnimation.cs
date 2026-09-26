using UnityEngine;

/// <summary>Requests baked generic-rig task clips through the normal animation
/// controller. No LateUpdate bone offsets or Humanoid-avatar assumptions.</summary>
public sealed class SupporterGestureAnimation : MonoBehaviour
{
    EnemyController body;
    MatchdayMarch march;
    AgentController crew;
    string workGesture;
    float workUntil;
    void Awake()
    {
        body=GetComponent<EnemyController>();march=GetComponent<MatchdayMarch>();
        crew=GetComponent<AgentController>();
    }
    public static void Play(AgentController agent,string gesture,float seconds)
    {
        if(!agent)return;
        var animation=agent.GetComponent<SupporterGestureAnimation>();
        if(!animation)animation=agent.gameObject.AddComponent<SupporterGestureAnimation>();
        animation.workGesture=gesture;animation.workUntil=Time.time+seconds;
    }
    public string RequestedState
    {
        get
        {
            bool working=crew&&crew.IsAlive&&crew.IsOnLiveJob&&Time.time<workUntil;
            if(!working&&(!body||!body.IsAlive||body.isHostile||!march))return null;
            string task=working?workGesture:march.CurrentTask??"";
            if(task.Contains("CHANT")||task.Contains("RALLY"))return "Task Chant";
            if(task.Contains("WATCH")||task.Contains("PATROL")||task.Contains("SCOUT"))return "Task Watch";
            if(task=="TREAT")return "Task Treat";
            if(task=="PICKUP")return "Task Pickup";
            if(task.Contains("QUEUE")||task.Contains("REGROUP"))return "Task Talk";
            return null;
        }
    }
}
