using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

/// <summary>Small action-driven coach. Opening a menu never completes a lesson.</summary>
public sealed class FirstSessionGuide:MonoBehaviour
{
    public static FirstSessionGuide Instance{get;private set;}
    public struct Lesson{public string title,text,button,target;public int step;}
    ModernCityHud hud;
    RectTransform frame;
    Outline currentHighlight;
    float next;
    int previousStep=-1,streetStep=-1;
    GameObject streetMarker;
    Material streetMaterial;
    Vector3 streetPoint;
    bool hasStreetPoint;
    public bool SuggestedStreetVisible=>streetMarker&&streetMarker.activeInHierarchy;
    public Vector3 SuggestedStreetPoint=>streetPoint;
    public static bool TryLesson(PlayerData d,out Lesson lesson)
    {
        lesson=default;if(GuidedCityTutorial.BlocksInput||d==null||!CityGameplay.HomeMode||!d.RecoveryGuidedStart||d.FirstSessionCoachHidden||PoliceCityMode.Active)return false;
        if(!d.LearnedSelection)lesson=new Lesson{step=1,title="1 / 7 · SELECT YOUR CREW",text="Press CREW, then SELECT one member. Their marker shows who will receive your next order.",button="OPEN CREW",target="CrewAccess"};
        else if(d.LearnedMoveOrders<2)lesson=new Lesson{step=2,title="2 / 7 · MOVE AND KEEP CONTROL",text="Tap the marked street, then another street. A coloured route shows the way. CREW / FIND locates or follows anyone.",button="SHOW STREET",target="SelectionCoach"};
        else if(!d.LearnedMultiSelection)lesson=new Lesson{step=3,title="3 / 7 · MAKE A GROUP",text="Press CREW and SELECT a second member. Both receive your next order.",button="ADD CREW",target="CrewAccess"};
        else if(!d.LearnedSplitSelection)lesson=new Lesson{step=4,title="4 / 7 · SPLIT YOUR GROUP",text="In CREW, DESELECT just one member. The others keep selection. Separate groups keep their own jobs.",button="SPLIT GROUP",target="CrewAccess"};
        else if(!HomeTerritorySystem.Built(d,0))lesson=new Lesson{step=5,title="5 / 7 · ESTABLISH HOME",text="Press HOME, then BUILD HEADQUARTERS. Selected crew walks to its plot and builds it. Watch progress in the world.",button="BUILD HQ",target="CityTop/Base"};
        else if(!HomeTerritorySystem.Built(d,1))lesson=new Lesson{step=6,title="6 / 7 · INVEST IN PRODUCTION",text="Press HOME and build SUPPLY YARD for £300. Its supplies fund preparation, shop improvements and matchday choices.",button="BUILD YARD",target="CityTop/Base"};
        else if(!HomeTerritorySystem.Built(d,2))lesson=new Lesson{step=7,title="7 / 7 · DEVELOP THE CREW",text="Build TRAINING GROUND for £450. It visibly develops Home and permanently improves your roster. Then follow the campaign goal.",button="BUILD TRAINING",target="CityTop/Base"};
        else return false;
        if(HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.IsBuilding){lesson.text=HomeTerritorySystem.Instance.Status+". Crew must arrive and stay on site. Another order cancels the job.";lesson.button="VIEW HOME";}
        return true;
    }
    public static void Ensure(ModernCityHud owner,RectTransform root)
    {var guide=owner.GetComponent<FirstSessionGuide>();if(!guide)guide=owner.gameObject.AddComponent<FirstSessionGuide>();guide.hud=owner;guide.frame=root;GuidedCityTutorial.Ensure();}
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;if(streetMarker)Destroy(streetMarker);if(streetMaterial)Destroy(streetMaterial);}
    void Update()
    {
        if(GuidedCityTutorial.BlocksInput){UpdateStreet(false);return;}
        if(Time.unscaledTime<next||!frame)return;next=Time.unscaledTime+.15f;
        Outline desired=null;
        if(TryLesson(GameManager.Data,out var lesson))
        {
            var target=frame.Find(lesson.target);if(target&&target.GetComponent<Graphic>())desired=target.GetComponent<Outline>()??target.gameObject.AddComponent<Outline>();
            if(previousStep>0&&previousStep<5&&lesson.step>previousStep)GameFeedback.Emit(GameFeedbackEvent.TaskComplete,AgentSelectionManager.LastPicked?AgentSelectionManager.LastPicked.transform.position:(Vector3?)null);
            previousStep=lesson.step;UpdateStreet(lesson.step==2);
        }
        else{previousStep=-1;UpdateStreet(false);}
        if(currentHighlight&&currentHighlight!=desired)currentHighlight.enabled=false;currentHighlight=desired;
        if(currentHighlight){currentHighlight.enabled=true;currentHighlight.effectDistance=new Vector2(3,-3);currentHighlight.effectColor=new Color(.3f,1,.65f,.5f+Mathf.PingPong(Time.unscaledTime*.5f,.4f));}
    }
    public void Guide()
    {
        if(!TryLesson(GameManager.Data,out var lesson))return;
        if(lesson.step==1||lesson.step==3||lesson.step==4)hud.OpenCrew();else if(lesson.step>=5)hud.OpenHome();else
        {UpdateStreet(true);if(hasStreetPoint)CameraPanTouchOnly.Instance?.FocusOn(streetPoint);else hud.FocusHome();}
    }
    void UpdateStreet(bool needed)
    {
        var selected=AgentSelectionManager.instance?.SelectedAgents;
        if(!needed||selected==null||selected.Count==0){if(streetMarker)streetMarker.SetActive(false);return;}
        var actor=selected.FirstOrDefault(a=>a&&a.IsAlive);if(!actor)return;
        int step=GameManager.Data.LearnedMoveOrders;
        if(streetStep!=step)
        {
            streetStep=step;hasStreetPoint=false;var nav=actor.GetComponent<NavMeshAgent>();
            if(nav&&nav.isOnNavMesh)
            {
                var filter=new NavMeshQueryFilter{agentTypeID=nav.agentTypeID,areaMask=nav.areaMask};var path=new NavMeshPath();
                for(int i=0;i<8&&!hasStreetPoint;i++)
                {
                    float angle=(i*45+step*90)*Mathf.Deg2Rad;var point=actor.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*13;
                    if(!NavMesh.SamplePosition(point,out var hit,4,filter)||!NavMesh.CalculatePath(actor.transform.position,hit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    if(CityBusinessSystem.HorizontalDistance(actor.transform.position,hit.position)<5)continue;streetPoint=hit.position;hasStreetPoint=true;
                }
            }
        }
        if(!hasStreetPoint)return;
        if(!streetMarker)
        {
            streetMarker=new GameObject("Tutorial suggested street");
            var line=streetMarker.AddComponent<LineRenderer>();var source=Resources.Load<Material>("Presentation/CrewRoute");streetMaterial=source?new Material(source):new Material(Shader.Find("Sprites/Default"));line.sharedMaterial=streetMaterial;
            line.useWorldSpace=false;line.positionCount=4;line.loop=true;line.widthMultiplier=.22f;line.alignment=LineAlignment.View;
            line.SetPositions(new[]{new Vector3(-2,0,-2),new Vector3(-2,0,2),new Vector3(2,0,2),new Vector3(2,0,-2)});
            line.startColor=line.endColor=LandscapeUI.Green;
            var label=ZoneLabelUtil.Create(streetMarker.transform,"TAP THIS STREET",3f,2.8f);label.GetComponent<ZoneLabelBillboard>().CommandPriority=true;
        }
        streetMarker.transform.position=streetPoint+Vector3.up*.25f;
        streetMarker.transform.localScale=Vector3.one*(GameFeedback.ReducedMotion?1:1+Mathf.Sin(Time.unscaledTime*3)*.05f);
        streetMarker.SetActive(!AgentSelectionManager.BlocksWorldTap());
    }
}
