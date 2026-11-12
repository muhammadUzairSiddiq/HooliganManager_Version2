using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Standard world button anchored at the stadium entrance. Matchday actions run in the existing city.</summary>
public sealed class StadiumMatchdayControl : MonoBehaviour
{
    public static StadiumMatchdayControl Instance {get;private set;}
    StadiumMatchdayActivity owner;
    Button pin;
    RectTransform frame;
    string[] choiceIds=Array.Empty<string>();
    public Vector3 TapPoint=>transform.position;
    public bool Visible=>pin&&pin.gameObject.activeInHierarchy;

    public static StadiumMatchdayControl Create(StadiumMatchdayActivity stadium,Vector3 ground)
    {
        var host=new GameObject("Stadium Matchday access");
        host.transform.SetParent(stadium.transform,true);
        host.transform.position=ground;
        var control=host.AddComponent<StadiumMatchdayControl>();
        control.owner=stadium;
        control.frame=WorldButtonLayer.Frame();
        control.pin=TutorialAnchor.Tag(LandscapeUI.Button("MatchdayPin",control.frame,
            stadium.AutomaticMatchdaysEnabled?"STADIUM":"MATCHDAY",0,0,210,48),"world.matchday");
        CityMissionHUD.Style((RectTransform)control.pin.transform);
        control.pin.onClick.AddListener(control.Activate);
        return control;
    }

    void Awake(){Instance=this;}

    void OnDestroy()
    {
        if(pin)Destroy(pin.gameObject);
        if(Instance==this)Instance=null;
    }

    void LateUpdate()
    {
        if(!pin||!frame)return;
        var camera=Camera.main;
        bool show=camera&&CityGameplay.HomeMode&&!PoliceCityMode.Active&&GuidedCityTutorial.AllowsFeature("matchday")
            &&(!AgentSelectionManager.BlocksWorldTap()||GuidedCityTutorial.AllowsWorldChoice);
        if(show)
        {
            Vector3 projected=camera.WorldToScreenPoint(TapPoint+Vector3.up*4);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,projected,null,out var local);
            float x=local.x-frame.rect.xMin,y=frame.rect.yMax-local.y;
            var area=new Rect(x-105,y-24,210,48);
            show=projected.z>0&&area.xMin>16&&area.xMax<frame.rect.width-16&&area.yMin>100&&area.yMax<frame.rect.height-75
                &&!WorldButtonLayer.ChoiceCovers(area)&&!WorldButtonLayer.CoversCrew(area);
            if(show)LandscapeUI.Place((RectTransform)pin.transform,area.x,area.y,area.width,area.height);
        }
        if(pin.gameObject.activeSelf!=show)pin.gameObject.SetActive(show);
    }

    public void Focus()=>FocusTutorial();
    public Bounds TutorialBounds
    {
        get
        {
            var model=CityLandmarks.FindBuilding(new[]{"stadium_001"});
            var bounds=new Bounds(TapPoint,Vector3.one*12);
            if(model)foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
    public void FocusTutorial()=>CameraPanTouchOnly.Instance?.TutorialFrame(TutorialBounds);
    public void FocusAndOpen()
    {
        if(!owner.AutomaticMatchdaysEnabled)Focus();
        Activate();
    }
    public void TagChoice(Button button,int index)
    {if(index<choiceIds.Length)TutorialAnchor.Tag(button,choiceIds[index]);}
    void Choices(string title,string[] ids,params (string label,Color color,Action action)[] options)
    {choiceIds=ids;WorldChoiceBar.Present(transform,title,options);}
    public void Activate()
    {
        if(!owner||!CityGameplay.HomeMode||PoliceCityMode.Active)return;
        AgentSelectionManager.ConsumeUiPointer();ModernCityHud.Instance?.CloseSheet();MatchdayStrategySystem.Instance?.CloseBoard();
        var street=owner.StreetOperations;
        if(owner.AutomaticMatchdaysEnabled)
        {
            if(street&&street.Automatic)street.ViewLiveMatch();
            else Focus();
            return;
        }
        if(owner.IsAftermath)
        {
            var data=GameManager.Data;
            bool resolved=data.MatchdayIncidentResolved;
            if(!resolved&&(!street||!street.IsCurrent))
            {
                Choices("MATCHDAY AFTERMATH",new[]{"world.matchday.finish","world.matchday.response0","world.matchday.response1"},
                    ("RETRY COVERAGE",LandscapeUI.Green,owner.RetryMissedResponses),
                    ("STADIUM TEAM",LandscapeUI.PanelColor,()=>MatchdayStrategySystem.Instance?.Assign(0)),
                    ("PUB TEAM",LandscapeUI.PanelColor,()=>MatchdayStrategySystem.Instance?.Assign(1)));
                return;
            }
            if(resolved&&CampaignMissions.HomeCleared(data)&&data.CurrentLevel>1&&!CampaignMissions.Completed(data,CampaignMissions.MissionCount))
            {
                string credit=data.MatchdayTravelCredit>0?$" · £{data.MatchdayTravelCredit} AWAY CREDIT":"";
                Choices("MATCHDAY COMPLETE"+credit,new[]{"world.matchday.away","world.matchday.finish"},
                    ("PLAN AWAY",LandscapeUI.Green,()=>owner.CloseMatchday(true)),
                    ("NEXT DAY",LandscapeUI.PanelColor,owner.CloseMatchday));
                return;
            }
            Choices(street&&street.IsCurrent?street.Summary:"MATCHDAY · AFTERMATH",new[]{"world.matchday.finish"},
                (resolved?"NEXT DAY":"RETRY MATCHDAY",LandscapeUI.Green,()=>{if(resolved)owner.CloseMatchday();else owner.RetryMissedResponses();}));
            return;
        }
        if(owner.HasStarted&&street&&street.IsCurrent)
        {
            Choices(street.Summary,new[]{"world.matchday.arrivals","world.matchday.roads","world.matchday.pub",
                    street.HasGateObjective?"world.matchday.gates":"world.matchday.custody"},
                ("RAIL ARRIVALS",LandscapeUI.Green,()=>street.FocusEvent(0)),
                ("ROAD CLOSURES",LandscapeUI.PanelColor,()=>street.FocusEvent(1)),
                ("PUB TRADE",LandscapeUI.PanelColor,()=>street.FocusEvent(2)),
                (street.HasGateObjective?"STADIUM GATES":"CUSTODY",LandscapeUI.PanelColor,
                    ()=>street.FocusEvent(street.HasGateObjective?4:3)));
            return;
        }
        if(owner.HasStarted)
        {
            Choices("MATCHDAY COVERAGE",new[]{"world.matchday.response0","world.matchday.response1","world.matchday.escort"},
                ("STADIUM TEAM",LandscapeUI.Green,()=>MatchdayStrategySystem.Instance?.Assign(0)),
                ("PUB TEAM",LandscapeUI.PanelColor,()=>MatchdayStrategySystem.Instance?.Assign(1)),
                ("ESCORT",LandscapeUI.PanelColor,()=>MatchdayStrategySystem.Instance?.AssignEscort()));
            return;
        }
        bool ready=MatchdayStrategySystem.CanStart(GameManager.Data,out var reason);
        Choices(ready?"MATCHDAY · GUIDE FANS, OPEN ROADS AND KEEP THE PUB TRADING":reason,
            new[]{"world.matchday.start","world.matchday.plan"},
            (owner.HasStarted?"SHOW LIVE EVENT":"START MATCHDAY",LandscapeUI.Green,()=>{if(owner.HasStarted)FocusTutorial();else owner.BeginMatchday();}),
            ("APPROACH",LandscapeUI.PanelColor,ShowPlans));
    }
    public void ShowPlans()
    {
        if(owner.AutomaticMatchdaysEnabled)return;
        Choices("MATCHDAY APPROACH · paid once when STARTED",new[]{"world.matchday.plan0","world.matchday.plan1","world.matchday.plan2","world.matchday.back"},
            ("BALANCED · FREE",LandscapeUI.Green,()=>ChoosePlan(0)),
            ("ROUTES · £200 + 2",LandscapeUI.Gold,()=>ChoosePlan(1)),
            ("TRADE · £150 + 2",LandscapeUI.Gold,()=>ChoosePlan(2)),
            ("BACK",LandscapeUI.PanelColor,Activate));
    }
    void ChoosePlan(int plan)
    {if(MatchdayTactics.Choose(GameManager.Data,plan)){GameManager.Save();CityGameplay.Instance?.PostEvent(MatchdayTactics.Description(plan));}Activate();}
}
