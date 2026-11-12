using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum CityNewsChannel { General, Crew, Arrival, Raid, Mission, Police, Economy }

[Serializable]
public sealed class CityNewsRecord
{
    public string message,uniqueKey,place;
    public CityNewsChannel channel;
    public bool critical;
    public long utcTicks;
    public int clockSeconds,repeats=1;
}

/// <summary>Bounded campaign-owned news, independent of command targeting and pause.</summary>
public sealed class CityNewsFeed:MonoBehaviour
{
    public const int MaximumEntries=64;
    public const string ExpandedPreferenceKey="City.NewsFeed.Expanded.v1";
    public static CityNewsFeed Instance{get;private set;}
    public static bool IsExpanded=>Instance&&Instance.expanded&&Instance.panel&&Instance.panel.gameObject.activeInHierarchy;
    public RectTransform HeaderTarget=>header?header.GetComponent<RectTransform>():null;
    public RectTransform ToggleTarget=>HeaderTarget;
    public RectTransform PanelBounds=>panel;
    public int EntryCount=>data?.CityNews?.Count??0;
    public int VisibleRowCount=>rows.Count;
    public int UnreadCount=>unread;
    public IReadOnlyList<CityNewsRecord> Entries=>data?.CityNews;
    RectTransform panel;
    Button header;
    TextMeshProUGUI headerLabel;
    GameObject body;
    ScrollRect scroll;
    PlayerData data;
    readonly List<Row> rows=new List<Row>(MaximumEntries);
    readonly HashSet<int> casualties=new HashSet<int>();
    readonly Queue<int> casualtyOrder=new Queue<int>();
    readonly Vector3[] corners=new Vector3[4];
    string session=Guid.NewGuid().ToString("N");
    bool expanded,dirty,savePending,scrollLatest;
    float nextRefresh,nextSave;
    int unread;
    sealed class Row{public GameObject root;public TextMeshProUGUI stamp,text;public Image stripe;}

    public static CityNewsFeed Ensure()
    {
        if(Instance)return Instance;
        if(!CityGameplay.Instance)return null;
        return new GameObject("City News Feed").AddComponent<CityNewsFeed>();
    }
    void Awake()
    {
        if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;
        expanded=PlayerPrefs.GetInt(ExpandedPreferenceKey,1)!=0;scrollLatest=expanded;Build();BindCampaign();
    }
    void OnDestroy(){Flush();if(Instance==this)Instance=null;}
    void OnDisable(){Flush();}
    void BindCampaign()
    {
        var current=GameManager.Data;if(ReferenceEquals(data,current))return;
        Flush();savePending=false;data=current;casualties.Clear();casualtyOrder.Clear();unread=0;
        if(data!=null){data.CityNews??=new List<CityNewsRecord>();data.CityNews.RemoveAll(r=>r==null||string.IsNullOrWhiteSpace(r.message));while(data.CityNews.Count>MaximumEntries)data.CityNews.RemoveAt(0);}
        dirty=true;
    }
    public static bool Post(string message,CityNewsChannel channel=CityNewsChannel.General,bool critical=false,string uniqueKey=null)
    {var feed=Ensure();return feed&&feed.Add(message,channel,critical,uniqueKey);}
    bool Add(string message,CityNewsChannel channel,bool critical,string uniqueKey)
    {
        if(string.IsNullOrWhiteSpace(message))return false;BindCampaign();if(data==null)return false;
        message=message.Trim();if(message.Length>400)message=message.Substring(0,397)+"...";
        var history=data.CityNews;
        if(!string.IsNullOrEmpty(uniqueKey))for(int i=history.Count-1;i>=0;i--)if(history[i].uniqueKey==uniqueKey)return false;
        long now=DateTime.UtcNow.Ticks;
        if(string.IsNullOrEmpty(uniqueKey)&&history.Count>0)
        {
            var latest=history[history.Count-1];
            if(latest.message==message&&latest.channel==channel&&now-latest.utcTicks<TimeSpan.TicksPerSecond*8)
            {latest.repeats=Mathf.Min(999,latest.repeats+1);latest.utcTicks=now;MarkChanged();return true;}
        }
        history.Add(new CityNewsRecord{message=message,channel=channel,critical=critical,uniqueKey=uniqueKey,utcTicks=now,clockSeconds=(int)Time.timeSinceLevelLoad,place=PoliceCityMode.Active?"POLICE":CityGameplay.HomeMode?"HOME":"AWAY"});
        while(history.Count>MaximumEntries)history.RemoveAt(0);
        MarkChanged();return true;
    }
    void MarkChanged(){dirty=savePending=true;unread=Mathf.Min(999,unread+1);nextSave=Time.unscaledTime+1;}
    public static void ReportCasualty(AgentController member)
    {
        if(!member||member.Data==null)return;var feed=Ensure();if(!feed)return;feed.BindCampaign();
        int actorId=member.GetInstanceID();if(feed.casualties.Contains(actorId))return;
        if(feed.casualties.Count>=128)feed.casualties.Remove(feed.casualtyOrder.Dequeue());
        feed.casualties.Add(actorId);feed.casualtyOrder.Enqueue(actorId);
        // The existing game treats a fallen member as recoverable; report the
        // actual downed state without inventing irreversible roster deletion.
        feed.Add(member.Data.AgentName+" is down. Their current order has ended; check crew recovery before sending another team.",CityNewsChannel.Crew,true,"casualty:"+feed.session+":"+member.Data.AgentId);
    }
    public static CityNewsChannel ClassifyChannel(string message)
    {
        if(string.IsNullOrWhiteSpace(message))return CityNewsChannel.General;
        string m=message.ToUpperInvariant();
        if(m.Contains("RAID")||m.Contains("LOOT"))return CityNewsChannel.Raid;
        if(m.Contains("ARRIV")||m.Contains("TRAIN")||m.Contains("STATION WAVE"))return CityNewsChannel.Arrival;
        if(m.Contains("POLICE")||m.Contains("ARREST")||m.Contains("OFFICER")||m.Contains("PATROL"))return CityNewsChannel.Police;
        if(m.Contains("MATCHDAY")||m.Contains("MISSION")||m.Contains("STADIUM")||m.Contains("ESCORT"))return CityNewsChannel.Mission;
        if(m.Contains("SUPPL")||m.Contains("TRADE")||m.Contains("INCOME")||m.Contains("SHOP")||m.Contains("BUILT"))return CityNewsChannel.Economy;
        if(m.Contains("CREW")||m.Contains("MEMBER")||m.Contains("RECRUIT")||m.Contains("RECOVER"))return CityNewsChannel.Crew;
        return CityNewsChannel.General;
    }
    public void Toggle()=>SetVisible(!expanded);
    public void SetVisible(bool visible)
    {
        expanded=visible;PlayerPrefs.SetInt(ExpandedPreferenceKey,visible?1:0);
        if(visible)scrollLatest=true;
        AgentSelectionManager.ConsumeUiPointer();dirty=true;
    }
    public bool TryGetScreenBounds(out Rect bounds)
    {
        bounds=default;if(!panel||!panel.gameObject.activeInHierarchy)return false;
        panel.GetWorldCorners(corners);bounds=Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);return true;
    }
    void Update()
    {
        BindCampaign();
        bool suppressed=!GuidedCityTutorial.AllowsFeature("news")||LiveMiniMap.IsExpanded||GameFeedback.SettingsOpen||GamePopup.AnyOpen||
            (ModernCityHud.Instance&&(ModernCityHud.Instance.SheetOpen||ModernCityHud.Instance.OrdersOpen))||
            (SquadBoard.Instance&&SquadBoard.Instance.IsOpen);
        if(panel.gameObject.activeSelf==suppressed)panel.gameObject.SetActive(!suppressed);
        if(dirty&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.1f;Render();}
        if(savePending&&Time.unscaledTime>=nextSave)Flush();
    }
    void Flush()
    {if(!savePending||data==null||!ReferenceEquals(data,GameManager.Data))return;savePending=false;GameManager.Save();}
    void Build()
    {
        var canvasObject=new GameObject("CityNewsFeedCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasObject.transform.SetParent(transform,false);
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=14100;
        LandscapeUI.ConfigureLandscapeScaler(canvasObject.GetComponent<CanvasScaler>());
        var safe=LandscapeUI.Rect("SafeArea",canvas.transform,0,0,1600,900);LandscapeUI.Stretch(safe);
        var frame=LandscapeUI.Rect("Landscape",safe,0,0,1600,900);var viewport=safe.gameObject.AddComponent<LandscapeViewport>();viewport.frame=frame;viewport.Fit();
        panel=LandscapeUI.Rect("NewsFeed",frame,0,0,410,312);panel.anchorMin=panel.anchorMax=Vector2.one;panel.pivot=Vector2.one;
        // A bottom-right anchor keeps the toggle hitbox stationary when folded.
        panel.anchorMin=panel.anchorMax=new Vector2(1,0);panel.pivot=new Vector2(1,0);panel.anchoredPosition=new Vector2(-18,18);
        header=LandscapeUI.Button("NewsToggle",panel,"NEWS FEED · HIDE",0,256,410,56,"dark");
        TutorialAnchor.Tag(header,"city.news");
        var headerRect=header.GetComponent<RectTransform>();headerRect.anchorMin=Vector2.zero;headerRect.anchorMax=new Vector2(1,0);headerRect.pivot=new Vector2(.5f,0);headerRect.anchoredPosition=Vector2.zero;headerRect.sizeDelta=new Vector2(0,56);
        header.onClick.AddListener(Toggle);headerLabel=header.GetComponentInChildren<TextMeshProUGUI>();headerLabel.fontSize=18;
        body=LandscapeUI.Panel("NewsBody",panel,0,0,410,250,true).gameObject;var bodyRect=body.GetComponent<RectTransform>();bodyRect.anchorMin=bodyRect.anchorMax=Vector2.zero;bodyRect.pivot=Vector2.zero;bodyRect.anchoredPosition=new Vector2(0,62);
        scroll=LandscapeUI.Scroll("NewsHistory",body.transform,8,8,394,234);scroll.content.GetComponent<VerticalLayoutGroup>().spacing=6;
        body.AddComponent<UiWorldTapBlocker>();header.gameObject.AddComponent<UiWorldTapBlocker>();
    }
    void Render()
    {
        dirty=false;body.SetActive(expanded);panel.sizeDelta=new Vector2(410,expanded?312:56);
        bool atLatest=scrollLatest||scroll.verticalNormalizedPosition<=.025f||rows.Count==0;var savedPosition=scroll.content.anchoredPosition;
        var history=data?.CityNews;int count=history?.Count??0;
        while(rows.Count<count)rows.Add(CreateRow());
        for(int i=0;i<rows.Count;i++)
        {
            var row=rows[i];row.root.SetActive(i<count);if(i>=count)continue;var entry=history[i];
            string message=entry.message;row.text.text=message;row.text.richText=false;
            float height=Mathf.Clamp(row.text.GetPreferredValues(message,358,0).y+32,68,112);row.root.GetComponent<LayoutElement>().preferredHeight=height;
            row.stamp.text=entry.place+" · "+(entry.clockSeconds/60).ToString("00")+":"+(entry.clockSeconds%60).ToString("00")+" · "+entry.channel.ToString().ToUpperInvariant()+(entry.repeats>1?" ×"+entry.repeats:"");
            var color=entry.critical?new Color(1,.36f,.29f):entry.channel==CityNewsChannel.Police?new Color(.38f,.66f,1):entry.channel==CityNewsChannel.Raid?LandscapeUI.Gold:LandscapeUI.Green;
            row.stripe.color=color;row.stamp.color=color;
            var textRect=row.text.rectTransform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=new Vector2(12,6);textRect.offsetMax=new Vector2(-12,-29);
        }
        if(expanded&&atLatest){unread=0;scrollLatest=false;Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;}
        else scroll.content.anchoredPosition=savedPosition;
        headerLabel.text="NEWS FEED"+(unread>0?" · "+unread+" NEW":"")+(expanded?" · HIDE":" · SHOW");
    }
    Row CreateRow()
    {
        var image=LandscapeUI.Panel("NewsEntry",scroll.content,0,0,378,92);image.color=new Color(.05f,.08f,.10f,.96f);image.raycastTarget=false;
        LandscapeUI.LayoutSize(image.gameObject,378,92);
        var stripe=LandscapeUI.Image("Priority",image.transform,0,3,3,86,null,LandscapeUI.Green);
        var stamp=LandscapeUI.Text("Stamp",image.transform,"",12,5,354,21,14,LandscapeUI.Muted,true);stamp.richText=false;
        var text=LandscapeUI.Text("Message",image.transform,"",12,29,354,57,18,LandscapeUI.White,false);
        return new Row{root=image.gameObject,stamp=stamp,text=text,stripe=stripe};
    }
}
