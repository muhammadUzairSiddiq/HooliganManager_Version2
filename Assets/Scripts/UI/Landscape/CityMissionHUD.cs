using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Live city objectives, resources and command bar, backed by the management ledger.</summary>
public sealed class CityMissionHUD : MonoBehaviour
{
    RectTransform frame, content;
    GameObject missionPanel;
    TextMeshProUGUI resources, objectives, events;
    float nextRefresh;
    int tab;
    string lastState;
    public void Build(RectTransform root)
    {
        frame=root;
        var hud=frame.Find("BattleHUD");
        if(hud)
        {
            Place(hud,"SquadRail",18,550,265,204);
            Place(hud,"SquadTitle",34,558,235,28);
            Place(hud,"MemberCount",34,590,235,24);
            Place(hud,"PortraitScroll",24,620,251,128);
            Place(hud,"Heading",35,25,500,32);
            var heading=hud.Find("Heading")?.GetComponent<TextMeshProUGUI>();
            if(heading)heading.text=GameManager.Data?.FirmName.ToUpperInvariant()??"YOUR FIRM";
            foreach(var name in new[]{"Cash","Reputation","Timer","Round"})
                if(hud.Find(name))hud.Find(name).gameObject.SetActive(false);
            Place(hud,"SelectionStatus",370,724,870,40);
            Place(hud,"Move",380,794,165,70);
            Place(hud,"Attack",560,794,165,70);
            Place(hud,"Retreat",920,794,165,70);
            Place(hud,"EnemyCount",1303,121,271,27);
            Place(hud,"ObjectiveSlot",1303,168,271,158);
            Place(hud,"HeatSlot",1303,349,271,48);
            if(hud.Find("Joystick"))hud.Find("Joystick").gameObject.SetActive(false);
        }
        resources=LandscapeUI.Text("LiveResources",frame,"",570,27,815,53,21,LandscapeUI.White,true);
        LandscapeUI.Panel("ObjectivesCard",frame,18,115,265,230,true);
        LandscapeUI.Text("ObjectivesTitle",frame,"OBJECTIVES",34,126,235,30,22,null,true);
        objectives=LandscapeUI.Text("LiveObjectives",frame,"",34,163,233,128,18,LandscapeUI.Muted);
        LandscapeUI.Button("MissionLedger",frame,"MISSIONS / CLAIM",33,296,235,40).onClick.AddListener(()=>{missionPanel.SetActive(true);RefreshMissions();});
        LandscapeUI.Panel("EventCard",frame,18,360,265,172,true);
        LandscapeUI.Text("EventTitle",frame,"DISTRICT FEED",34,371,235,30,21,null,true);
        events=LandscapeUI.Text("LiveEvents",frame,"",34,411,231,105,18,LandscapeUI.Muted);
        LandscapeUI.Button("Capture",frame,"CAPTURE",740,794,165,70,"green").onClick.AddListener(()=>CityGameplay.Instance?.CaptureNearest());
        missionPanel=LandscapeUI.Panel("CityMissionLedger",frame,340,175,910,550,true).gameObject;
        LandscapeUI.Text("Title",missionPanel.transform,"FIRM OPERATIONS",22,15,530,38,28,null,true);
        LandscapeUI.Button("Close",missionPanel.transform,"CLOSE",755,14,135,42).onClick.AddListener(()=>missionPanel.SetActive(false));
        LandscapeUI.Button("Campaign",missionPanel.transform,"CAMPAIGN",22,64,235,44).onClick.AddListener(()=>{tab=0;RefreshMissions();});
        LandscapeUI.Button("Matchday",missionPanel.transform,"MATCHDAY",272,64,235,44).onClick.AddListener(()=>{tab=1;RefreshMissions();});
        LandscapeUI.Button("DailyBonus",missionPanel.transform,"CLAIM £750 BONUS",545,64,345,44).onClick.AddListener(()=>{
            if(FirmMissions.ClaimBonus(GameManager.Data)){GameManager.Save();CityGameplay.Instance?.PostEvent("MATCHDAY BONUS +£750");}
            RefreshMissions();
        });
        content=LandscapeUI.Scroll("MissionScroll",missionPanel.transform,16,124,877,407).content;
        missionPanel.SetActive(false);
        Style(frame);
    }
    public static void Style(Transform parent)
    {
        foreach(var button in parent.GetComponentsInChildren<Button>(true))
        {
            var bg=button.targetGraphic as Image;if(!bg)continue;
            bg.sprite=null;
            bg.color=button.name=="Attack"?new Color(.38f,.09f,.11f,.97f):button.name=="Capture"?new Color(.16f,.32f,.22f,.97f):new Color(.07f,.12f,.16f,.97f);
            button.GetComponent<LandscapeButtonPolish>()?.SetBaseColor(bg.color);
            var outline=bg.GetComponent<Outline>()??bg.gameObject.AddComponent<Outline>();
            outline.effectColor=new Color(.43f,.57f,.6f,.55f);outline.effectDistance=new Vector2(1,-1);
        }
        foreach(var bg in parent.GetComponentsInChildren<Image>(true))
        {
            if(bg.sprite==LandscapeTheme.Current?.panel)
            {bg.sprite=null;bg.color=new Color(.025f,.055f,.07f,.95f);}
        }
    }
    static void Place(Transform parent,string name,float x,float y,float w,float h)
    { var r=parent.Find(name) as RectTransform;if(r)LandscapeUI.Place(r,x,y,w,h); }
    void Update()
    {
        if(!resources || Time.unscaledTime<nextRefresh)return;
        nextRefresh=Time.unscaledTime+.3f;
        var d=GameManager.Data;if(d==null)return;
        resources.text=$"£{d.Money:N0}     CREW {d.Fans}     REP {d.Reputation}     MORALE {d.FanMorale}%\n<size=17><color=#9BADB5>POLICE HEAT {d.PoliceHeat} / 10     MATCHDAY {d.MatchDay:00}</color></size>";
        var visible=FirmMissions.Get(d,tab).Where(m=>!FirmMissions.Claimed(d,m.id)).Take(3);
        objectives.text=string.Join("\n",visible.Select(m=>$"<color=#E8BA5A>{m.title}</color>\n{Mathf.Min(m.progress,m.target)} / {m.target}"));
        if(string.IsNullOrEmpty(objectives.text))objectives.text="ALL OBJECTIVES CLAIMED\nCheck Matchday operations.";
        events.text=CityGameplay.Instance?.EventText??"District ready. Hold and drag to explore.";
        var state=string.Join("|",FirmMissions.Get(d,tab).Select(m=>$"{m.progress}:{FirmMissions.Claimed(d,m.id)}"));
        if(missionPanel.activeSelf && state!=lastState)RefreshMissions();
        lastState=state;
    }
    void RefreshMissions()
    {
        foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        var d=GameManager.Data;if(d==null)return;
        foreach(var m in FirmMissions.Get(d,tab))
        {
            var row=LandscapeUI.Panel(m.id,content,0,0,848,133,true);
            LandscapeUI.LayoutSize(row.gameObject,848,133);
            bool claimed=FirmMissions.Claimed(d,m.id),ready=m.progress>=m.target;
            LandscapeUI.Text("Name",row.transform,m.title,17,10,590,29,22,null,true);
            LandscapeUI.Text("Body",row.transform,m.body,17,43,592,33,18,LandscapeUI.Muted);
            LandscapeUI.Text("Progress",row.transform,$"{Mathf.Min(m.progress,m.target)} / {m.target}    •    £{m.reward:N0}",17,89,570,29,20,LandscapeUI.Gold);
            var button=LandscapeUI.Button("Action",row.transform,claimed?"CLAIMED":ready?"CLAIM":"GO TO LOCATION",631,38,200,60,ready?"green":"dark");
            button.interactable=!claimed;
            button.onClick.AddListener(()=>{
                if(FirmMissions.Claim(GameManager.Data,m.id)){GameManager.Save();CityGameplay.Instance?.PostEvent(m.title+" +£"+m.reward);RefreshMissions();}
                else {missionPanel.SetActive(false);CityGameplay.Instance?.OpenLocation(m.location);}
            });
        }
        missionPanel.transform.Find("DailyBonus").GetComponent<Button>().interactable=FirmMissions.BonusReady(d);
        Style(missionPanel.transform);
    }
}
