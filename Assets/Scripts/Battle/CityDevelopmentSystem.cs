using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class CityDevelopmentProject
{
    public int site, level;
    public bool building;
    public float remaining;
}

/// <summary>Persistent, bounded city investment loop. No wall-clock/offline rewards.
/// Construction and economy advance only during unpaused gameplay.</summary>
public static class CityDevelopmentLedger
{
    public static readonly string[] Names={"SUPPORTERS' CLUB","SUPPLY DEPOT","RECRUITMENT NETWORK"};
    public static readonly string[] Benefits={"£60 per level / minute","1 supply per level / minute","1 queued supporter per level / 2 minutes"};
    public static void Ensure(PlayerData data)
    {
        if(data==null)return;
        data.CityDevelopment ??= new List<CityDevelopmentProject>();
        for(int i=0;i<3;i++)if(!data.CityDevelopment.Any(p=>p!=null&&p.site==i))data.CityDevelopment.Add(new CityDevelopmentProject{site=i});
    }
    public static int Cost(CityDevelopmentProject p)=>(p.site==0?300:p.site==1?450:500)*(p.level+1);
    public static bool CanBuild(PlayerData d,int site,out string reason)
    {
        reason="";Ensure(d);
        var p=d?.CityDevelopment?.Find(x=>x!=null&&x.site==site);
        if(p==null){reason="Site unavailable";return false;}
        if(p.building){reason="Construction in progress";return false;}
        if(p.level>=3){reason="Maximum level reached";return false;}
        if(p.level>0&&(d.CityCapturedZones?.Count??0)<p.level){reason=$"Control {p.level} district(s) before upgrading";return false;}
        if(d.Money<Cost(p)){reason=$"Needs £{Cost(p):N0}";return false;}
        if(d.CitySupplies<p.level*2){reason=$"Needs {p.level*2} supplies";return false;}
        return true;
    }
    public static bool Start(PlayerData d,int site)
    {
        if(!CanBuild(d,site,out _))return false;
        var p=d.CityDevelopment.Find(x=>x.site==site);
        d.Money-=Cost(p);d.CitySupplies-=p.level*2;p.building=true;p.remaining=45f+p.level*30f;
        return true;
    }
    public static void Tick(PlayerData d,float seconds,Action<string> report)
    {
        if(d==null||seconds<=0)return;
        Ensure(d);
        foreach(var p in d.CityDevelopment)
        {
            if(p==null||!p.building)continue;
            p.remaining=Mathf.Max(0,p.remaining-seconds);
            if(p.remaining>0)continue;
            p.building=false;p.level=Mathf.Min(3,p.level+1);
            report?.Invoke(Names[p.site]+$" · LEVEL {p.level} READY");
        }
        d.CityEconomySeconds+=seconds;
        while(d.CityEconomySeconds>=120f)
        {
            d.CityEconomySeconds-=120f;
            int income=0,supplies=0,recruits=0;
            foreach(var p in d.CityDevelopment)
            {
                if(p==null)continue;
                if(p.site==0)income+=120*p.level;
                if(p.site==1)supplies+=2*p.level;
                if(p.site==2)recruits+=p.level;
            }
            d.Money+=income;d.CitySupplies=Mathf.Min(99,d.CitySupplies+supplies);
            d.PendingFansGain=Mathf.Min(30,d.PendingFansGain+recruits);
            if(income+supplies+recruits>0)report?.Invoke($"NETWORK INCOME · £{income} / {supplies} SUPPLIES / {recruits} SUPPORTERS QUEUED");
        }
    }
}

public sealed class CityDevelopmentSystem : MonoBehaviour
{
    public static CityDevelopmentSystem Instance{get;private set;}
    GameObject panel;
    readonly TextMeshProUGUI[] details=new TextMeshProUGUI[3];
    readonly Button[] build=new Button[3];
    float tick,saveClock,uiClock;
    public bool IsOpen=>panel&&panel.activeInHierarchy;
    void Awake(){Instance=this;CityDevelopmentLedger.Ensure(GameManager.Data);}
    void OnDestroy(){if(Instance==this)Instance=null;}
    void OnApplicationPause(bool paused){if(paused&&GameManager.Data!=null)GameManager.Save();}
    void OnDisable(){if(GameManager.Data!=null)GameManager.Save();}
    void Update()
    {
        var d=GameManager.Data;if(d==null)return;
        tick+=Time.deltaTime;saveClock+=Time.deltaTime;
        if(tick>=1f)
        {
            CityDevelopmentLedger.Tick(d,tick,m=>CityGameplay.Instance?.PostEvent(m));
            d.CityRivalGrowthSeconds+=tick;tick=0;
            if(d.CityRivalGrowthSeconds>=180f)
            {
                d.CityRivalGrowthSeconds-=180f;
                RivalGrowthSystem.AdvanceLivingCity(d);
                BattleManager.instance?.ReinforceLivingRivals();
            }
        }
        if(saveClock>=15f){saveClock=0;GameManager.Save();}
        if(IsOpen&&Time.unscaledTime>=uiClock){uiClock=Time.unscaledTime+.5f;Refresh();}
    }
    public void OpenBoard()
    {
        if(GamePopup.AnyOpen)GamePopup.Instance.Hide();
        if(!panel)BuildBoard();
        if(panel){panel.SetActive(true);Refresh();}
    }
    public void CloseBoard(){if(panel)panel.SetActive(false);}
    void BuildBoard()
    {
        var go=new GameObject("CityDevelopmentCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        go.transform.SetParent(transform,false);
        go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=26000;
        LandscapeUI.ConfigureLandscapeScaler(go.GetComponent<CanvasScaler>());
        var safe=LandscapeUI.Rect("SafeArea",go.transform,0,0,1600,900);LandscapeUI.Stretch(safe);
        var frame=LandscapeUI.Rect("Frame",safe,0,0,1600,900);
        var viewport=safe.gameObject.AddComponent<LandscapeViewport>();viewport.frame=frame;viewport.Fit();
        panel=LandscapeUI.Panel("DevelopmentBoard",frame,280,145,1040,620,true).gameObject;
        LandscapeUI.Text("Title",panel.transform,"PLAN & BUILD YOUR NETWORK",24,20,790,38,27,LandscapeUI.White,true);
        LandscapeUI.Button("Close",panel.transform,"CLOSE",855,16,160,44).onClick.AddListener(CloseBoard);
        LandscapeUI.Text("Help",panel.transform,"Invest at city venues. Construction runs alongside squad tasks. Capture districts to unlock higher levels.",24,68,990,46,17,LandscapeUI.Muted);
        for(int i=0;i<3;i++)
        {
            int site=i;float y=128+i*148;
            LandscapeUI.Text("Site"+i,panel.transform,CityDevelopmentLedger.Names[i],24,y,690,28,22,LandscapeUI.Gold,true);
            details[i]=LandscapeUI.Text("Details"+i,panel.transform,"",24,y+35,720,82,17,LandscapeUI.White);
            build[i]=LandscapeUI.Button("Build"+i,panel.transform,"BUILD",780,y+10,230,46,"green");
            build[i].onClick.AddListener(()=>
            {
                if(CityDevelopmentLedger.Start(GameManager.Data,site))
                {GameManager.Save();CityGameplay.Instance?.PostEvent(CityDevelopmentLedger.Names[site]+" · CONSTRUCTION STARTED");}
                Refresh();
            });
            LandscapeUI.Button("ViewSite"+i,panel.transform,"VIEW SITE",780,y+68,230,40).onClick.AddListener(()=>
            {
                int location=site==0?1:site==1?2:0;
                var city=CityGameplay.Instance;
                if(city?.Locations!=null&&city.Locations.Length>location)CameraPanTouchOnly.Instance?.FocusOn(city.Locations[location]);
                CloseBoard();
            });
        }
        CityMissionHUD.Style(panel.transform);
    }
    void Refresh()
    {
        var d=GameManager.Data;if(d==null)return;CityDevelopmentLedger.Ensure(d);
        for(int i=0;i<3;i++)
        {
            var p=d.CityDevelopment.Find(x=>x.site==i);
            bool available=CityDevelopmentLedger.CanBuild(d,i,out string reason);
            details[i].text=$"LEVEL {p.level}/3 · {CityDevelopmentLedger.Benefits[i]}\n"+
                (p.building?$"BUILDING · {Mathf.CeilToInt(p.remaining)} seconds remaining":p.level>=3?"FULLY DEVELOPED":$"Upgrade: £{CityDevelopmentLedger.Cost(p):N0} + {p.level*2} supplies\n{(available?"Ready to invest":reason)}");
            build[i].interactable=available;
            build[i].GetComponentInChildren<TextMeshProUGUI>().text=p.building?"BUILDING":p.level>=3?"MAX LEVEL":p.level==0?"BUILD":"UPGRADE";
        }
    }
}
