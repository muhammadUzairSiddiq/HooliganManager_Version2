#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using static LandscapeUI;
using Object = UnityEngine.Object;

public static partial class LandscapeSceneBuilder
{
    static void BuildBattle()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        var old=Find<BattleUIController>();var oldResult=Find<BattleResultController>();var oldPause=Find<BattlePauseMenuController>();
        var oldBuilding=Find<BuildingInteractionPanel>();var oldRaid=Find<PoliceRaidGameplayController>();var oldJoystick=Find<VirtualJoystick>();var canvases=OldCanvases();
        var frame=CanvasFrame("LandscapeBattle",null,11000,out var canvas);
        var hudRoot=Rect("BattleHUD",frame,0,0,1600,900);
        var ui=Copy(old,hudRoot.gameObject);ui.landscapeLayout=true;
        var hud=frame.gameObject.AddComponent<LandscapeBattleHUD>();hud.frame=frame;
        Image("TopBar",hudRoot,18,16,1564,81,null,new Color32(8,14,19,235));
        Text("Heading",hudRoot,"CONTROL THE STREETS",38,26,615,35,28,null,true);
        ui.objectiveText=Text("Objective",hudRoot,"SECURE TURF & RETURN TO HQ",38,64,615,24,18,Muted);
        hud.cash=Text("Cash",hudRoot,"£0",865,28,202,32,25,Green,true,TextAlignmentOptions.Right);
        hud.reputation=Text("Reputation",hudRoot,"",830,65,237,22,15,Muted,false,TextAlignmentOptions.Right);
        ui.roundTimerText=Text("Timer",hudRoot,"00:00",1110,26,173,35,30,null,true,TextAlignmentOptions.Center);
        ui.roundLabel=Text("Round",hudRoot,"MATCH",1110,64,173,23,14,Muted,true,TextAlignmentOptions.Center);
        ui.pauseButton=Button("Pause",hudRoot,"PAUSE",1404,29,157,53);
        Image("SquadRail",hudRoot,22,123,265,588,null,new Color32(8,15,20,224));
        Text("SquadTitle",hudRoot,"YOUR SQUAD",39,135,233,30,21,null,true);
        ui.playerCountText=Text("MemberCount",hudRoot,"MEMBERS: 0",39,173,233,28,18,Green,true);
        var roster=Scroll("PortraitScroll",hudRoot,29,215,250,472);
        ui.portraitStrip=roster.content;ui.portraitCardPrefab=BuildPortraitPrefab();
        ui.enemyCountText=Text("EnemyCount",hudRoot,"RIVALS: 0",1303,401,271,27,21,Gold,true);
        hud.objectiveSlot=Rect("ObjectiveSlot",hudRoot,1303,446,271,158);
        hud.heatSlot=Rect("HeatSlot",hudRoot,1303,619,271,48);
        ui.selectedUnitsLabel=Text("SelectionStatus",hudRoot,"SELECT A MEMBER OR TAP A DESTINATION",359,680,866,62,23,White,true,TextAlignmentOptions.Center);
        ui.attackBtn=Button("Attack",hudRoot,"ATTACK",498,794,185,70,"red");
        ui.moveBtn=Button("Move",hudRoot,"MOVE",700,794,185,70);
        ui.retreatBtn=Button("Retreat",hudRoot,"RETREAT",902,794,185,70);
        var center=Button("CenterCamera",hudRoot,"RECENTRE",1310,799,255,64);
        center.gameObject.AddComponent<CenterCameraButton>();
        Text("Controls",hudRoot,"DRAG TO PAN  /  PINCH OR SCROLL TO ZOOM",460,868,700,22,14,Muted,false,TextAlignmentOptions.Center);
        var joy=Image("Joystick",hudRoot,63,727,140,140,theme.panel,new Color(1,1,1,.6f));joy.raycastTarget=true;
        var joystick=Copy(oldJoystick,joy.gameObject);
        var knob=Image("Knob",joy.transform,45,45,50,50,theme.shield,null,true);
        knob.rectTransform.anchorMin=knob.rectTransform.anchorMax=knob.rectTransform.pivot=new Vector2(.5f,.5f);
        knob.rectTransform.anchoredPosition=Vector2.zero;joystick.joystickKnob=knob.rectTransform;joystick.handleRange=45;
        ui.policeRaidHeader=null;ui.policeHeatBarLabel=null;ui.policeHeatFill=null;
        ui.buildingInteractionPanel=BuildBuilding(frame,oldBuilding);
        // Results and pause each have their own sorting canvas so all late-created overlays are covered.
        var resultFrame=CanvasFrame("LandscapeResults",null,22000,out var resultsCanvas);
        var result=BuildResult(resultFrame,oldResult);ui.resultPanel=result;hud.result=result.gameObject;
        result.gameObject.SetActive(false);
        var pauseFrame=CanvasFrame("LandscapePause",null,23000,out var pauseCanvas);
        var pauseCtrl=Copy(oldPause,pauseFrame.gameObject);var panel=Rect("PauseOverlay",pauseFrame,0,0,1600,900);pauseCtrl.pausePanel=panel.gameObject;hud.pause=panel.gameObject;
        Image("Dim",panel,0,0,1600,900,null,new Color(0,0,0,.78f)).raycastTarget=true;
        var pauseCard=Panel("PauseCard",panel,421,228,758,442,true);
        Text("Title",pauseCard.transform,"TAKE A BREATHER",34,38,690,60,42,null,true,TextAlignmentOptions.Center);
        Text("Subtitle",pauseCard.transform,"Your crew is waiting. The streets can wait too.",38,121,682,68,24,Muted,false,TextAlignmentOptions.Center);
        pauseCtrl.resumeButton=Button("Resume",pauseCard.transform,"BACK TO THE STREETS",43,233,672,65,"green");
        pauseCtrl.abortButton=Button("Abort",pauseCard.transform,"ABORT BATTLE",43,321,672,68);
        panel.gameObject.SetActive(false);
        if(oldRaid) BuildRaid(frame,oldRaid);
        RemoveOld(canvases);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    static GameObject BuildPortraitPrefab()
    {
        var root=Panel("AgentPortrait",null,0,0,234,92,true);LayoutSize(root.gameObject,234,92);
        var card=root.gameObject.AddComponent<AgentPortraitCard>();
        card.fallbackPortrait=theme.portraits[0];card.portraitImage=Image("Portrait",root.transform,5,5,74,72,theme.portraits[0],null,true);
        card.nameLabel=Text("Name",root.transform,"MEMBER",88,10,134,27,18,null,true);
        card.hpLabel=Text("Health",root.transform,"0 / 0",88,42,134,24,15,Muted);
        Image("HealthTrack",root.transform,87,74,132,5,null,new Color32(40,53,58,255));
        card.hpFill=Image("HealthFill",root.transform,87,74,132,5,theme.darkButton,Green);
        card.hpFill.type=UnityEngine.UI.Image.Type.Filled;card.hpFill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;
        card.selectionGlow=Image("Selected",root.transform,0,0,3,92,null,Green);card.selectionGlow.gameObject.SetActive(false);
        return SavePrefab(root.gameObject,"AgentPortrait");
    }
    static BuildingInteractionPanel BuildBuilding(Transform frame,BuildingInteractionPanel old)
    {
        var root=Panel("BuildingInteraction",frame,326,448,946,288,true);
        var c=Copy(old,root.gameObject);c.panelRoot=root.rectTransform;c.slideDistance=950;
        c.headerAccentBar=Image("Accent",root.transform,0,0,946,4,null,Gold);
        c.buildingIconImage=Image("Icon",root.transform,24,23,65,65,theme.shield,null,true);
        c.buildingNameText=Text("Name",root.transform,"BUILDING",111,21,710,42,30,null,true);
        c.buildingDescText=Text("Description",root.transform,"",111,71,710,60,22,Muted);
        c.closeButton=Button("Close",root.transform,"X",866,18,57,49);
        var actions=Rect("Actions",root.transform,24,156,896,103);var layout=actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing=16;layout.childControlHeight=true;layout.childControlWidth=true;
        c.actionButtonContainer=actions;
        // Preserve the original action prefab and its cooldown/price bindings, but fit each action to the new row.
        if(c.actionButtonPrefab)
        {
            var prefab=Object.Instantiate(c.actionButtonPrefab);prefab.name="BuildingAction";
            var le=prefab.GetComponent<LayoutElement>()??prefab.AddComponent<LayoutElement>();le.minWidth=160;le.preferredWidth=240;le.preferredHeight=94;le.minHeight=70;
            var bg=prefab.GetComponent<Image>();if(bg){bg.sprite=theme.darkButton;bg.type=UnityEngine.UI.Image.Type.Sliced;bg.color=Color.white;}
            c.actionButtonPrefab=SavePrefab(prefab,"BuildingAction");
        }
        root.gameObject.SetActive(false);return c;
    }
    static BattleResultController BuildResult(Transform frame,BattleResultController old)
    {
        var root=Rect("ResultsPanel",frame,0,0,1600,900);var c=Copy(old,root.gameObject);
        Image("Backdrop",root,0,0,1600,900,null,Ink).raycastTarget=true;
        Image("Celebration",root,385,260,828,330,theme.celebration,new Color(1,1,1,.75f),true);
        c.titleHeaderText=Text("Eyebrow",root,"MATCHDAY RESULT",260,35,1080,34,20,Muted,true,TextAlignmentOptions.Center);
        c.outcomeLabel=Text("Outcome",root,"VICTORY",270,85,1060,99,82,Green,true,TextAlignmentOptions.Center);
        c.outcomeBannerImage=Image("OutcomeAccent",root,703,195,194,4,null,Green);
        c.subtitleText=Text("Subtitle",root,"YOU TOOK THE FIGHT. YOU GOT THE REWARD.",205,219,1190,57,24,Muted,false,TextAlignmentOptions.Center);
        Image("Emblem",root,624,310,356,286,theme.emblem,null,true);
        var rewards=Panel("Rewards",root,35,318,440,356);
        Text("Title",rewards.transform,"REWARDS",26,19,388,39,23,null,true);
        c.moneyEarnedValue=ResultRow(rewards.transform,"CASH EARNED",theme.cash,83);
        c.reputationValue=ResultRow(rewards.transform,"REPUTATION",theme.star,170);
        c.fansGainedValue=ResultRow(rewards.transform,"NEW FANS",theme.crew,257);
        var stats=Panel("BattleStats",root,1127,318,438,356);
        Text("Title",stats.transform,"BATTLE REPORT",25,19,385,39,23,null,true);
        c.enemiesDefeatedValue=StatRow(stats.transform,"RIVALS DEFEATED",84);
        c.unitsLostValue=StatRow(stats.transform,"MEMBERS LOST",150);
        c.policeHeatValue=StatRow(stats.transform,"POLICE HEAT",216);
        c.moraleChangeValue=StatRow(stats.transform,"MORALE CHANGE",282);
        c.playerFirmText=Text("PlayerFirm",root,"YOUR FIRM",484,621,300,45,23,null,true,TextAlignmentOptions.Center);
        c.enemyFirmText=Text("RivalFirm",root,"RIVAL FIRM",818,621,300,45,23,null,true,TextAlignmentOptions.Center);
        c.scoreText=Text("Score",root,"0 - 0",688,680,224,57,34,Gold,true,TextAlignmentOptions.Center);
        c.playerCrestImage=null;c.enemyCrestImage=null;c.playerSubText=null;c.enemySubText=null;
        c.ladsAreShakenbanner=Rect("MoraleWarning",root,34,744,1532,44).gameObject;
        c.ladsAreShakenText=Text("Warning",c.ladsAreShakenbanner.transform,"",0,0,1532,44,21,Gold,true,TextAlignmentOptions.Center);
        c.continueBtn=Button("Continue",root,"RETURN TO TOWN",35,817,481,59,"green");
        c.viewRankingsBtn=Button("Rankings",root,"VIEW RANKINGS",556,817,481,59);
        c.nextMatchdayBtn=Button("NextMatchday",root,"NEXT MATCHDAY    >",1077,817,488,59);
        c.tripTargetsHeaderText=null;c.targetRows=Array.Empty<BattleResultController.TargetRow>();
        return c;
    }
    static TextMeshProUGUI ResultRow(Transform p,string label,Sprite icon,float y)
    {
        Image("Icon",p,25,y,45,42,icon,null,true);Text("Label",p,label,84,y-2,300,25,15,Muted,true);
        return Text("Value",p,"0",84,y+22,305,38,30,Green,true);
    }
    static TextMeshProUGUI StatRow(Transform p,string label,float y)
    {
        Text("Label",p,label,25,y,281,31,18,Muted);
        return Text("Value",p,"0",318,y-3,94,37,25,null,true,TextAlignmentOptions.Right);
    }
    static void BuildRaid(Transform frame,PoliceRaidGameplayController old)
    {
        var page=Rect("PoliceRaidGameplay",frame,0,0,1600,900);var c=Copy(old,page.gameObject);
        c.backgroundFlashImage=Image("Backdrop",page,0,0,1600,900,null,Ink);c.backgroundFlashImage.raycastTarget=true;
        c.titleText=Text("Title",page,"POLICE RAID",160,49,1280,70,52,Red,true,TextAlignmentOptions.Center);
        c.subtitleText=Text("Subtitle",page,"SURVIVE THE RAID AND BRING YOUR CREW HOME",160,139,1280,54,26,Muted,false,TextAlignmentOptions.Center);
        c.playerFirmNameText=Text("Firm",page,"YOUR FIRM",80,275,560,59,38,null,true);
        c.playerCountText=Text("Members",page,"",80,355,560,46,28,Green);
        c.playerHealthBar=Slider(page,"PlayerHealth",80,430,560,24);c.playerHealthFill=c.playerHealthBar.fillRect.GetComponent<Image>();
        Text("Police",page,"POLICE",960,275,560,59,38,null,true);
        c.policeCountText=Text("Officers",page,"",960,355,560,46,28,Red);
        c.policeHealthBar=Slider(page,"PoliceHealth",960,430,560,24);c.policeHealthFill=c.policeHealthBar.fillRect.GetComponent<Image>();
        c.timerText=Text("Timer",page,"01:00",663,311,274,72,49,Gold,true,TextAlignmentOptions.Center);
        c.timerSlider=Slider(page,"TimerProgress",520,518,560,12);c.heatSlider=Slider(page,"Heat",520,577,560,12);
        c.heatLabel=Text("HeatLabel",page,"POLICE HEAT",520,606,560,35,22,Red,true,TextAlignmentOptions.Center);
        c.attackButton=Button("Attack",page,"ATTACK",393,737,250,69,"red");c.moveButton=Button("Move",page,"MOVE",675,737,250,69);c.retreatButton=Button("Retreat",page,"RETREAT",957,737,250,69);
        c.portraitStrip=null;c.selectedUnitsLabel=null;c.movePromptText=null;
        var outcome=Panel("RaidOutcome",page,354,227,892,432,true);c.outcomePanel=outcome.gameObject;
        c.outcomeHeaderText=Text("Title",outcome.transform,"",34,36,824,68,48,null,true,TextAlignmentOptions.Center);
        c.outcomeBodyText=Text("Body",outcome.transform,"",45,139,802,136,28,Muted,false,TextAlignmentOptions.Center);
        c.continueButton=Button("Continue",outcome.transform,"CONTINUE",42,326,808,70,"green");outcome.gameObject.SetActive(false);page.gameObject.SetActive(false);
    }
}
#endif
