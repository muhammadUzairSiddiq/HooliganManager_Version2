#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using static LandscapeUI;
using Object = UnityEngine.Object;

/// <summary>Replaces the shipped portrait canvases with editable, wired landscape scene objects.</summary>
public static partial class LandscapeSceneBuilder
{
    const string Prefabs="Assets/UI/Landscape/";
    static LandscapeTheme theme;
    static readonly string[] SceneNames={"MainMenu","DashboardScene","Gameplay"};

    [MenuItem("Hooligan/Landscape/Rebuild all UI scenes")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before rebuilding scenes.");
        foreach (var name in SceneNames)
        {
            var open=SceneManager.GetSceneByName(name);
            if (open.IsValid() && open.isDirty) EditorSceneManager.SaveScene(open);
        }
        Directory.CreateDirectory(Prefabs); Directory.CreateDirectory(".utmp/landscape-backup");
        foreach (var name in SceneNames)
        {
            string backup=".utmp/landscape-backup/"+name+".unity";
            if (!File.Exists(backup)) File.Copy("Assets/Scenes/"+name+".unity",backup);
        }
        AssetDatabase.Refresh(); theme=LandscapeThemeImporter.Import();
        BuildMenu(); BuildDashboard(); BuildBattle();
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft=true; PlayerSettings.allowedAutorotateToLandscapeRight=false;
        PlayerSettings.allowedAutorotateToPortrait=false; PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
        PlayerSettings.defaultScreenWidth=1600; PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.defaultWebScreenWidth=1600; PlayerSettings.defaultWebScreenHeight=900;
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        Validate();
        Debug.Log("[Landscape UI] All three game UI scenes rebuilt and wired successfully.");
    }
    static T Find<T>() where T:Component => Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(c=>c.gameObject.scene==SceneManager.GetActiveScene());
    static Canvas[] OldCanvases() => Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.gameObject.scene==SceneManager.GetActiveScene() && c.isRootCanvas && c.renderMode!=RenderMode.WorldSpace).ToArray();
    static T Copy<T>(T old, GameObject go) where T:Component
    {
        var c=go.AddComponent<T>(); if (old) EditorUtility.CopySerialized(old,c); return c;
    }
    static void RemoveOld(Canvas[] canvases)
    {
        foreach (var canvas in canvases)
        {
            if (!canvas) continue;
            // Keep scene managers even when an older scene stored them beneath a canvas.
            foreach(var m in canvas.GetComponentsInChildren<GameManager>(true)) Copy(m,new GameObject("GameManager"));
            foreach(var d in canvas.GetComponentsInChildren<GameData>(true)) Copy(d,new GameObject("GameData"));
            Object.DestroyImmediate(canvas.gameObject);
        }
    }
    static RectTransform CanvasFrame(string name, Sprite background, int order, out Canvas canvas)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=order;
        LandscapeUI.ConfigureLandscapeScaler(go.GetComponent<CanvasScaler>());
        if(background)
        {
            var bg=Image("Backdrop",go.transform,0,0,1600,900,background);
            Stretch(bg.rectTransform);
            var aspect=bg.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio=background.rect.width/background.rect.height;
        }
        var safe=Rect("SafeArea",go.transform,0,0,1600,900); Stretch(safe);
        var frame=Rect("Landscape",safe,0,0,1600,900);
        var fit=safe.gameObject.AddComponent<LandscapeViewport>(); fit.frame=frame; fit.Fit();
        return frame;
    }
    static LandscapeScreenMotion Motion(LandscapeFrontEnd shell, RectTransform frame, TextMeshProUGUI rotatingText = null)
    {
        var motion = shell.gameObject.AddComponent<LandscapeScreenMotion>();
        motion.shell = shell;
        motion.rotatingText = rotatingText;
        var canvas = frame.GetComponentInParent<Canvas>();
        var backdrop = canvas ? canvas.transform.Find("Backdrop") as RectTransform : null;
        motion.background = backdrop;
        return motion;
    }
    static Button Action(LandscapeFrontEnd shell, Transform parent, string name, string label, float x,float y,float w,float h,string action,string style="dark")
    {
        var b=Button(name,parent,label,x,y,w,h,style); var route=b.gameObject.AddComponent<LandscapeAction>(); route.target=shell; route.action=action; return b;
    }
    static RectTransform Page(string name, Transform frame)
    {
        var page=Rect(name,frame,0,96,1600,696);
        page.gameObject.AddComponent<CanvasGroup>();
        Image("Surface",page,0,0,1600,696,null,Ink).raycastTarget=true;
        return page;
    }
    static void Sidebar(LandscapeFrontEnd shell,Transform page,string[] labels,string[] actions,int selected=0)
    {
        Image("Sidebar",page,0,0,254,696,null,new Color32(11,19,25,255));
        for(int i=0;i<labels.Length;i++) Action(shell,page,"Tab_"+actions[i],labels[i],22,30+i*78,212,60,actions[i],i==selected?"red":"dark");
        Text("SideNote",page,"EST. ON THE TERRACES\nBUILT IN THE STREETS",28,595,203,66,14,Muted);
    }
    static void Header(LandscapeFrontEnd shell,Transform frame,string title)
    {
        Image("Header",frame,0,0,1600,96,null,new Color32(8,14,19,250));
        if(!shell.mainMenu) Action(shell,frame,"Home","<",26,20,52,54,"home");
        shell.screenTitle=Text("ScreenTitle",frame,title,shell.mainMenu?38:96,23,525,50,34,null,true);
        shell.money=Resource(frame,theme.cash,"CASH",690,180);
        shell.fans=Resource(frame,theme.crew,"MEMBERS",888,140);
        shell.reputation=Resource(frame,theme.star,"REPUTATION",1046,155);
        shell.day=Resource(frame,theme.shield,"MATCHDAY",1218,164);
        var settings=Action(shell,frame,"Settings","",1390,20,65,54,"settings");
        Image("Gear",settings.transform,18,12,30,30,theme.settings,null,true);
        if(!shell.mainMenu) Action(shell,frame,"MainMenu","MENU",1460,20,116,54,"menu");
        Image("Divider",frame,24,95,1552,1,null,new Color32(66,78,82,255));
    }
    static TextMeshProUGUI Resource(Transform frame,Sprite icon,string label,float x,float w)
    {
        Image("Resource",frame,x,18,w,58,null,new Color32(19,29,35,255));
        Image(label+"Icon",frame,x+12,29,33,32,icon,null,true);
        Text(label,frame,label,x+54,22,w-61,16,11,Muted,true);
        return Text(label+"Value",frame,"—",x+54,40,w-61,29,24,null,true);
    }
    static void Footer(LandscapeFrontEnd shell,Transform frame)
    {
        Image("Navigation",frame,0,804,1600,96,null,new Color32(8,14,19,250));
        string[] labels={"TOWN","MISSIONS","SQUAD","RECRUIT","AWAY TRIPS","RANKINGS"};
        string[] routes={"home","missions","squad","recruitment","trips","rankings"};
        for(int i=0;i<labels.Length;i++) Action(shell,frame,"Nav_"+routes[i],labels[i],28+i*191,823,177,54,routes[i]);
        Action(shell,frame,"EndMatchday","END MATCHDAY  >",1194,823,376,54,"end-day","green");
    }
    static void Settings(LandscapeFrontEnd shell,Transform frame)
    {
        var root=Rect("SettingsOverlay",frame,0,0,1600,900); shell.settingsPanel=root.gameObject;
        root.gameObject.AddComponent<CanvasGroup>();
        Image("Dim",root,0,0,1600,900,null,new Color(0,0,0,.82f)).raycastTarget=true;
        var panel=Panel("Settings",root,370,150,860,600,true);
        Text("Heading",panel.transform,"SETTINGS",44,34,700,65,40,null,true);
        Text("Description",panel.transform,"Make yourself at home.",44,102,700,40,23,Muted);
        Text("Audio",panel.transform,"MASTER VOLUME",44,189,690,35,23,null,true);
        shell.volumeSlider=Slider(panel.transform,"MasterVolume",44,245,770,32,true);
        Action(shell,panel.transform,"Performance","TOGGLE 30 / 60 FPS",44,329,770,64,"framerate");
        Text("Controls",panel.transform,"Tap to select and move  ·  Drag to pan  ·  Pinch / scroll to zoom\nEsc returns to town. Use the map to explore the streets.",44,415,770,60,20,Muted);
        Action(shell,panel.transform,"CloseSettings","SAVE & CLOSE",44,512,770,57,"close-settings","red");
        root.gameObject.SetActive(false);
    }
    static Slider Slider(Transform p,string name,float x,float y,float w,float h,bool handle=false)
    {
        var root=Rect(name,p,x,y,w,h); var s=root.gameObject.AddComponent<Slider>(); s.minValue=0;s.maxValue=1;s.value=1;
        Image("Track",root,0,h/2-4,w,8,null,new Color32(45,57,61,255));
        var area=Rect("FillArea",root,0,h/2-4,w,8); var fill=Image("Fill",area,0,0,w,8,null,Green); Stretch(fill.rectTransform); s.fillRect=fill.rectTransform;
        if(handle) {var thumb=Image("Handle",root,0,0,26,h,theme.shield,White,true); s.handleRect=thumb.rectTransform;s.targetGraphic=thumb;thumb.raycastTarget=true;}
        else s.interactable=false;
        return s;
    }
    static void BuildMenu()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); var old=Find<MainMenuController>(); var canvases=OldCanvases();
        var frame=CanvasFrame("LandscapeMainMenu",theme.mainBackground,100,out var canvas);
        var shell=frame.gameObject.AddComponent<LandscapeFrontEnd>(); shell.mainMenu=true; shell.clubs=old?old.clubRegistry:null;
        var ctrl=Copy(old,frame.gameObject); ctrl.SelectClubPanel=null;
        Image("MenuShade",frame,0,0,590,900,null,new Color(.018f,.032f,.044f,.83f));
        Image("Accent",frame,48,55,48,4,null,Red);
        Text("Edition",frame,"FOOTBALL. LOYALTY. TERRITORY.",112,40,428,33,17,Muted,true);
        Image("TitleArtwork",frame,44,119,484,216,theme.logo,null,true);
        Text("Tagline",frame,"BUILD YOUR FIRM.\nRULE THE TERRACES.",58,348,465,74,26,White,true);
        ctrl.newGameBtn=LegacyButton("NewGame",frame,"NEW GAME    >",52,460,460,72,"red");
        ctrl.continueBtn=LegacyButton("Continue",frame,"ENTER HOME TERRITORY",52,548,460,64);
        shell.continueCampaign=Action(shell,frame,"LoadGame","OPEN HEADQUARTERS",52,627,460,60,"load");
        ctrl.settingsBtn=LegacyButton("Settings",frame,"SETTINGS",52,702,224,58);
        Action(shell,frame,"Credits","CREDITS",288,702,224,58,"credits");
        ctrl.exitBtn=LegacyButton("Exit",frame,"QUIT GAME",52,777,224,52,"outline");
        ctrl.versionLabel=Text("Version",frame,"",58,853,400,22,14,Muted);
        Text("CityCaption",frame,"THE CITY IS YOURS TO TAKE.",833,750,685,55,38,null,true,TextAlignmentOptions.Right);
        var rotating = Text("CityCaptionBody",frame,"One crew. Every street. A reputation to build.",864,813,652,37,22,Muted,false,TextAlignmentOptions.Right);
        shell.money=Resource(frame,theme.cash,"CASH",781,207); shell.fans=Resource(frame,theme.crew,"MEMBERS",1008,162);
        shell.reputation=Resource(frame,theme.star,"REPUTATION",1190,170); shell.day=Resource(frame,theme.shield,"MATCHDAY",1380,184);
        Motion(shell,frame,rotating); Settings(shell,frame); RemoveOld(canvases); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    static void BuildDashboard()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DashboardScene.unity");
        var oldDash=Find<MainDashboardController>(); var oldRecruit=Find<RecruitFansController>(); var oldTrips=Find<PlanAwayTripController>();
        var oldRank=Find<RankingsController>(); var oldClub=Find<ClubInfoController>(); var canvases=OldCanvases();
        var frame=CanvasFrame("LandscapeHeadquarters",theme.townBackground,100,out var canvas);
        var shell=frame.gameObject.AddComponent<LandscapeFrontEnd>(); shell.clubs=oldDash?oldDash.clubRegistry:null;
        var dash=Copy(oldDash,frame.gameObject); shell.dashboard=dash;
        // Navigation is handled by the new shell; original data and end-of-day subscriptions stay active.
        dash.eventsContainer=null; dash.eventRowPrefab=null; dash.noEventsInfo=null;
        dash.recruitFansBtn=null;dash.planAwayTripBtn=null;dash.viewRankingsBtn=null;dash.clubFirmInfoBtn=null;dash.endMatchDayBtn=null;
        dash.policeHeatFill=null;dash.policeHeatLabel=null;dash.policeHeatDescriptionText=null;dash.layLowBtn=null;
        dash.bribePolicePanel=null;dash.prestigeBadge=null;
        Header(shell,frame,"YOUR TOWN");
        var townTicker=Text("MotivationTicker",frame,"One crew. Every street. A reputation to build.",244,67,420,22,14,Muted,false);
        var home=Rect("Town",frame,0,96,1600,696); shell.home=home.gameObject;
        Image("TownTint",home,0,0,1600,696,null,new Color(0,0,0,.10f));
        var objective=Panel("Objectives",home,28,22,326,317,true);
        Text("Heading",objective.transform,"OBJECTIVES",24,18,270,36,24,null,true);
        shell.homeObjectives=Text("LiveObjectives",objective.transform,"YOUR CAMPAIGN",24,68,276,230,22);
        var events=Panel("Events",home,28,357,326,315,true);
        Text("Heading",events.transform,"RECENT EVENTS",24,18,270,36,24,null,true);
        shell.eventContent=Rect("EventContent",events.transform,24,65,276,234);
        var firm=Panel("FirmCard",home,1144,22,426,263,true);
        dash.firmShieldImage=Image("Crest",firm.transform,28,28,75,84,theme.emblem,null,true);
        dash.firmNameText=Text("FirmName",firm.transform,"YOUR FIRM",120,27,279,60,27,null,true);
        dash.clubLocationText=Text("Club",firm.transform,"",120,94,279,35,21,Muted);
        dash.firmStatusText=Text("Status",firm.transform,"",28,143,370,30,21,Green);
        dash.firmRecordText=Text("Record",firm.transform,"",28,191,172,34,25,null,true);
        dash.hooliganRankText=Text("Rank",firm.transform,"",243,191,151,34,26,Gold,true,TextAlignmentOptions.Right);
        var rival=Panel("NextRival",home,1144,308,426,184,true);
        Text("Label",rival.transform,"NEXT RIVAL",25,19,370,25,17,Muted,true);
        dash.nextRivalText=Text("RivalName",rival.transform,"",25,59,370,48,27,null,true);
        dash.nextRivalMatchText=Text("Match",rival.transform,"",25,115,370,34,20,Gold);
        Action(shell,home,"ClubDetails","FIRM DETAILS",1144,516,202,56,"club");
        Action(shell,home,"ChangeTeam","CHANGE TEAM",1360,516,210,56,"change-team");
        Action(shell,home,"StreetEntry","PLAN AN AWAY TRIP    >",1144,590,426,78,"trips","red");
        Action(shell,home,"TownSquadMarker","YOUR CREW  /  SQUAD",392,115,250,57,"squad");
        Action(shell,home,"TownMissionMarker","TERRITORY  /  MISSIONS",726,304,310,57,"missions");
        Action(shell,home,"TownRecruitMarker","THE LOCAL  /  RECRUIT",429,488,289,57,"recruitment");
        dash.moraleText=Text("Morale",home,"",393,632,360,35,22,Green,true);
        dash.rankChangePopupText=Text("RankChange",home,"",640,18,480,70,30,Gold,true,TextAlignmentOptions.Center);dash.rankChangePopupText.gameObject.SetActive(false);
        BuildTrips(shell,frame,oldTrips); BuildRecruitment(shell,frame,oldRecruit); BuildRankings(shell,frame,oldRank); BuildClub(shell,frame,oldClub);
        BuildSquadPage(shell,frame); BuildMissionPage(shell,frame); Footer(shell,frame); Settings(shell,frame);
        Motion(shell,frame,townTicker);
        dash.recruitFansPanel=shell.recruitment;dash.planAwayTripPanel=shell.trips;dash.rankingsPanel=shell.rankings;dash.clubFirmInfoPanel=shell.club;
        foreach(var p in new[]{shell.trips,shell.recruitment,shell.rankings,shell.club,shell.squad,shell.missions}) p.SetActive(false);
        RemoveOld(canvases); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    static void BuildTrips(LandscapeFrontEnd shell,Transform frame,PlanAwayTripController old)
    {
        var page=Page("AwayTrips",frame);shell.trips=page.gameObject; var c=Copy(old,page.gameObject);
        Sidebar(shell,page,new[]{"DESTINATIONS","YOUR SQUAD","RECRUIT MEMBERS"},new[]{"trips","squad","recruitment"});
        Text("Heading",page,"PICK YOUR NEXT AWAY DAY",286,24,840,42,31,null,true);
        Text("Description",page,"Choose a destination. Check the cost. Bring your crew home.",286,71,1030,32,21,Muted);
        for(int i=0;i<c.destinations.Count;i++)
        {
            var d=c.destinations[i]; int col=i%2,row=i/2;
            var card=Panel("Destination_"+i,page,286+col*343,126+row*246,323,223,true);
            Image("Location",card.transform,12,12,299,110,theme.townBackground);
            Image("Shade",card.transform,12,73,299,49,null,new Color(0,0,0,.7f));
            Text("Name",card.transform,d.name.ToUpper(),25,81,279,34,25,null,true);
            Text("Details",card.transform,$"{d.distanceKm} KM   /   {d.rivalStrength} RIVALS",20,139,283,28,17,Muted);
            d.mapButton=Button("Select",card.transform,"SELECT DESTINATION",18,176,287,35,"outline");
        }
        var deploy=Panel("DeploySelection",page,286,596,666,88,true);
        Text("DeployTitle",deploy.transform,"TRAVELLING CREW",18,10,210,28,22,null,true);
        c.deploymentSummaryText=Text("DeploySummary",deploy.transform,"",18,41,210,28,16,Muted);
        c.selectAllMembersBtn=Button("SelectAllMembers",deploy.transform,"ALL",244,16,70,28,"green");
        c.clearMembersBtn=Button("ClearMembers",deploy.transform,"NONE",244,48,70,28,"dark");
        c.deploymentContent=Scroll("DeployList",deploy.transform,326,11,326,66,true).content;
        var detail=Panel("TripBriefing",page,1000,124,565,494,true);
        c.detailLocationImage=Image("BriefingImage",detail.transform,365,25,170,96,null,Color.white,true);
        c.detailNameText=Text("Name",detail.transform,"DESTINATION",28,25,320,54,35,null,true);
        c.detailDescriptionText=Text("Description",detail.transform,"",28,100,510,85,23,Muted);
        Text("RewardLabel",detail.transform,"POTENTIAL REWARD",28,207,300,32,20,Muted);
        c.detailRewardText=Text("Reward",detail.transform,"",315,201,220,44,30,Gold,true,TextAlignmentOptions.Right);
        Text("CostLabel",detail.transform,"TRAVEL COST",28,268,300,32,20,Muted);
        c.detailTravelCostText=Text("Cost",detail.transform,"",315,262,220,44,30,null,true,TextAlignmentOptions.Right);
        Text("RiskLabel",detail.transform,"RIVAL STRENGTH",28,331,290,32,20,Muted);
        c.detailRivalStrengthText=Text("Risk",detail.transform,"",315,325,220,44,28,null,true,TextAlignmentOptions.Right);
        c.startTripBtn=LegacyButton("StartTrip",detail.transform,"START AWAY TRIP    >",28,405,510,61,"red");
        Text("Requirements",page,"Select at least one match-ready member, keep fans available and cover the travel cost.",1000,636,565,47,19,Muted);
        c.backBtn=null;c.detailPoliceText=null;c.detailArchetypeText=null;c.detailMottoText=null;c.detailInfamyText=null;c.detailNetResultText=null;c.detailLockOverlay=null;c.detailLockReasonText=null;
    }
    static void BuildRecruitment(LandscapeFrontEnd shell,Transform frame,RecruitFansController old)
    {
        var page=Page("Recruitment",frame);shell.recruitment=page.gameObject;var c=Copy(old,page.gameObject);
        Sidebar(shell,page,new[]{"RECRUITMENT","YOUR SQUAD","FALLEN MEMBERS"},new[]{"recruitment","squad","squad"});
        Text("Heading",page,"GROW YOUR FOLLOWING",286,25,850,44,32,null,true);
        Text("Subheading",page,"Choose a campaign. New fans arrive when you end the matchday.",286,73,1230,34,22,Muted);
        c.optionCards=new List<RecruitOptionCardUI>();
        for(int i=0;i<c.recruitOptions.Count;i++)
        {
            var card=Panel("RecruitOption_"+i,page,286+i*322,135,302,320,true);var cardUI=card.gameObject.AddComponent<RecruitOptionCardUI>();
            var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card;Colors(button);
            cardUI.selectedHighlight=Image("Selection",card.transform,0,0,302,4,null,Red).gameObject;cardUI.selectedHighlight.SetActive(false);
            cardUI.iconImage=Image("Icon",card.transform,22,25,64,60,theme.crew,null,true);
            cardUI.titleText=Text("Title",card.transform,"",22,105,259,58,25,null,true);
            cardUI.descriptionText=Text("Description",card.transform,"",22,172,259,61,20,Muted);
            cardUI.costText=Text("Cost",card.transform,"",22,250,150,37,27,Gold,true);
            cardUI.fanRangeText=Text("Gain",card.transform,"",174,248,106,45,17,Green,true,TextAlignmentOptions.Right);
            c.optionCards.Add(cardUI);
            int rep=ReputationUnlockSystem.GetRecruitOptionRequiredRep(i);
            Text("RequiredReputation",page,rep>0?"REQUIRES "+rep+" REPUTATION":"AVAILABLE FROM DAY ONE",286+i*322,467,302,32,16,Muted,false,TextAlignmentOptions.Center);
        }
        c.topMoneyText=null;c.topFansText=null;c.topRepText=null;c.topMoraleText=null;c.backBtn=null;
        c.footerLabel=Text("RecruitmentFeedback",page,"CHOOSE YOUR RECRUITMENT CAMPAIGN.",286,529,874,77,23,Muted);
        c.recruitBtn=LegacyButton("Recruit",page,"CONFIRM RECRUITMENT",1191,544,382,68,"green");
        c.watchlistWarningBanner=Rect("Watchlist",page,286,622,1287,54).gameObject;
        Image("BG",c.watchlistWarningBanner.transform,0,0,1287,54,null,new Color(.22f,.1f,.06f,.9f));
        c.watchlistWarningText=Text("Warning",c.watchlistWarningBanner.transform,"",16,6,1255,42,20,Gold);
        c.reviveSectionRoot=null;c.reviveSectionHeader=null;c.reviveRowContainer=null;c.reviveRowPrefab=null;
    }
    static void BuildRankings(LandscapeFrontEnd shell,Transform frame,RankingsController old)
    {
        var page=Page("Rankings",frame);shell.rankings=page.gameObject;var c=Copy(old,page.gameObject);
        Sidebar(shell,page,new[]{"LEADERBOARD","YOUR FIRM","MISSIONS"},new[]{"rankings","club","missions"});
        var summary=Panel("YourFirm",page,284,25,1288,138);
        c.yourFirmLogo=Image("Logo",summary.transform,20,22,88,90,theme.emblem,null,true);
        c.yourFirmNameText=Text("Name",summary.transform,"",135,18,525,47,30,null,true);
        c.yourRankText=Text("Rank",summary.transform,"",705,18,159,47,35,Gold,true);
        c.yourReputationText=Text("Rep",summary.transform,"",930,18,150,47,30,null,true);
        c.yourFansText=Text("Fans",summary.transform,"",1120,18,140,47,30,null,true);
        c.yourUnlockStatusText=Text("Unlocks",summary.transform,"",135,82,900,42,19,Muted);
        Text("RankLabel",summary.transform,"RANK",705,62,159,23,14,Muted);
        Text("RepLabel",summary.transform,"REPUTATION",930,62,150,23,14,Muted);
        Text("FansLabel",summary.transform,"MEMBERS",1120,62,140,23,14,Muted);
        Text("Columns",page,"RANK                  FIRM                                                        REPUTATION                 WINS",309,184,1240,35,18,Muted,true);
        c.leaderboardContainer=Scroll("Leaderboard",page,284,231,1288,436).content;
        var row=Panel("LeaderboardRow",null,0,0,1270,72); LayoutSize(row.gameObject,1270,72);
        var r=row.gameObject.AddComponent<LeaderboardRowUI>();r.rowBackground=row;
        r.rankText=Text("Rank",row.transform,"",18,15,75,42,24,Gold,true);
        r.logoImage=Image("Crest",row.transform,102,10,55,52,theme.emblem,null,true);
        r.firmNameText=Text("Firm",row.transform,"",184,12,610,47,23,null,true);
        r.reputationText=Text("Reputation",row.transform,"",864,12,180,47,23,null,true);
        r.winsText=Text("Wins",row.transform,"",1110,12,145,47,23,null,true);
        c.leaderboardRowPrefab=SavePrefab(row.gameObject,"LeaderboardRow"); c.backBtn=null;c.yourMoraleText=null;
    }
    static void BuildClub(LandscapeFrontEnd shell,Transform frame,ClubInfoController old)
    {
        var page=Page("Firm",frame);shell.club=page.gameObject;var c=Copy(old,page.gameObject);c.backBtn=null;
        Sidebar(shell,page,new[]{"YOUR FIRM","YOUR SQUAD","RANKINGS","CHANGE TEAM"},new[]{"club","squad","rankings","change-team"});
        var card=Panel("ClubIdentity",page,288,39,1278,607);c.clubCard=card.gameObject.AddComponent<ClubCardUI>();
        var v=c.clubCard;v.crestImage=Image("Crest",card.transform,75,90,270,285,theme.emblem,null,true);
        v.firmNameText=Text("FirmName",card.transform,"",420,78,796,66,48,null,true);
        v.clubNameText=Text("ClubName",card.transform,"",420,161,796,46,29,Muted);
        Text("Labels",card.transform,"MEMBERS                    REPUTATION                    CASH",420,257,796,34,18,Muted,true);
        v.fansText=Text("Fans",card.transform,"",420,306,220,60,39,Green,true);
        v.reputationText=Text("Reputation",card.transform,"",688,306,220,60,39,Gold,true);
        v.cashText=Text("Cash",card.transform,"",955,306,275,60,39,null,true);
        Text("IdentityNote",card.transform,"Every away day adds to your story.\nBuild a loyal following and put your firm on the map.",420,415,750,95,26,Muted);
    }
    static void BuildSquadPage(LandscapeFrontEnd shell,Transform frame)
    {
        var page=Page("Squad",frame);shell.squad=page.gameObject;
        Sidebar(shell,page,new[]{"ACTIVE SQUAD","RECOVERING","FALLEN MEMBERS","RECRUIT MEMBERS"},new[]{"squad-active","squad-injured","squad-fallen","recruitment"});
        Text("Heading",page,"THE PEOPLE BEHIND YOUR FIRM",286,25,1230,45,32,null,true);
        shell.rosterSummary=Text("RosterSummary",page,"YOUR ROSTER",286,83,1230,35,21,Muted);
        shell.squadContent=Scroll("SquadRoster",page,280,139,1292,503,true).content;
        Text("SquadNote",page,"Drag to browse the full roster. Choose travelling members from the Away Trips screen.",292,655,1250,31,19,Muted);
    }
    static void BuildMissionPage(LandscapeFrontEnd shell,Transform frame)
    {
        var page=Page("Missions",frame);shell.missions=page.gameObject;
        Sidebar(shell,page,new[]{"CAMPAIGN","MATCHDAY","YOUR SQUAD"},new[]{"campaign","matchday","squad"});
        Text("Heading",page,"MAKE EVERY MATCHDAY COUNT",285,24,1200,44,32,null,true);
        Text("Subheading",page,"Real objectives. Lasting progress. Rewards for your firm.",285,78,1200,31,22,Muted);
        shell.missionContent=Scroll("MissionList",page,280,131,880,544).content;
        var bonus=Panel("MatchdayBonus",page,1190,137,382,528,true);
        Image("BonusEmblem",bonus.transform,111,20,160,145,theme.emblem,null,true);
        shell.missionSummary=Text("BonusProgress",bonus.transform,"MATCHDAY BONUS",30,186,322,238,22,Muted,false,TextAlignmentOptions.Center);
        shell.missionBonusButton=Action(shell,bonus.transform,"ClaimBonus","CLAIM BONUS",29,449,324,57,"bonus","green");
    }
    static GameObject SavePrefab(GameObject go,string name)
    {
        var saved=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+name+".prefab");Object.DestroyImmediate(go);return saved;
    }
}
#endif
