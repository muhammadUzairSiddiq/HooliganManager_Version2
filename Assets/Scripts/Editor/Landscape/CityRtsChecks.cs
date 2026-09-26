#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class CityRtsChecks
{
    public static void BeginStreamingProbe()
    {
        if(CityGameplay.Instance)CityGameplay.Instance.StartCoroutine(StreamingProbe());
    }
    static System.Collections.IEnumerator StreamingProbe()
    {
        var city=CityGameplay.Instance;
        var matchday=Object.FindFirstObjectByType<StadiumMatchdayActivity>();
        if(!city||!matchday)yield break;
        var original=CityActivityStreaming.GroundFocus(Camera.main);
        var lines=new List<string>();
        void Check(bool ok,string title)=>lines.Add((ok?"PASS ":"FAIL ")+title);
        CameraPanTouchOnly.Instance?.FocusOn(city.Locations[5]);
        yield return new WaitForSeconds(12);
        int nearby=matchday.StreamedBodyCount;
        int groups=Object.FindObjectsByType<MatchdayGroupMarker>(FindObjectsSortMode.None).Length;
        Check(nearby>0,$"Camera approach loads supporter bodies ({nearby})");
        CameraPanTouchOnly.Instance?.FocusOn(city.Home);
        yield return new WaitForSeconds(5);
        Check(matchday.StreamedBodyCount==0,$"Leaving stadium releases live bodies ({matchday.StreamedBodyCount})");
        Check(Object.FindObjectsByType<MatchdayGroupMarker>(FindObjectsSortMode.None).Length==groups,"Off-screen group records and map markers survive");
        CameraPanTouchOnly.Instance?.FocusOn(city.Locations[5]);
        yield return new WaitForSeconds(10);
        Check(matchday.StreamedBodyCount>0,"Returning to stadium restores living group presentation");
        Check(CityActivityStreaming.Instance.LiveAmbientBodies<=CityActivityStreaming.BodyBudget,"Streaming churn remains under global actor cap");
        CameraPanTouchOnly.Instance?.FocusOn(original);
        lines.Add($"RESULT {lines.Count(x=>x.StartsWith("PASS"))} passed, {lines.Count(x=>x.StartsWith("FAIL"))} failed");
        Directory.CreateDirectory("Artifacts/CityQA");File.WriteAllLines("Artifacts/CityQA/rts-streaming-playtest.txt",lines);
    }
    public static void Run()
    {
        var lines=new List<string>();
        void Check(bool ok,string title)=>lines.Add((ok?"PASS ":"FAIL ")+title);
        var d=new PlayerData{Money=2000,CitySupplies=10,CityCapturedZones=new List<string>()};
        CityDevelopmentLedger.Ensure(d);
        Check(d.CityDevelopment.Count==3,"Three development sites, no duplicate initialization");
        CityDevelopmentLedger.Ensure(d);
        Check(d.CityDevelopment.Count==3,"Initialization is idempotent");
        Check(CityDevelopmentLedger.Start(d,0)&&d.Money==1700,"Construction charges exact cost");
        Check(!CityDevelopmentLedger.Start(d,0)&&d.Money==1700,"Double purchase blocked");
        Check(CityDevelopmentLedger.Start(d,1),"Independent sites build concurrently");
        CityDevelopmentLedger.Tick(d,44,null);
        Check(d.CityDevelopment[0].level==0&&d.CityDevelopment[0].building,"No early construction rewards");
        var restored=JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(d));
        CityDevelopmentLedger.Tick(restored,1,null);
        Check(restored.CityDevelopment[0].level==1&&!restored.CityDevelopment[0].building,"Save round-trip resumes construction once");
        Check(!CityDevelopmentLedger.CanBuild(restored,0,out _),"Higher upgrades require territory");
        restored.CityCapturedZones.Add("Test district");
        Check(CityDevelopmentLedger.CanBuild(restored,0,out _),"District control unlocks level two");
        int money=restored.Money;
        CityDevelopmentLedger.Tick(restored,0,null);
        Check(restored.Money==money,"Paused time earns no income");
        CityDevelopmentLedger.Tick(restored,120,null);
        Check(restored.Money==money+120,"Built club produces bounded scheduled income");
        var poor=new PlayerData{Money=0};
        Check(!CityDevelopmentLedger.Start(poor,0)&&poor.Money==0,"Insufficient funds never deducted");
        if(Application.isPlaying)
        {
            var hud=Object.FindFirstObjectByType<LandscapeBattleHUD>();
            Check(hud&&hud.frame,"Gameplay HUD exists");
            if(hud)
            {
                var frame=hud.frame;
                Check(!frame.Find("MissionLedger")&&!frame.Find("LeftRetreat")&&!frame.Find("ObjectivesCard"),"Legacy duplicate controls removed");
                Check(frame.Find("CommandRail/FeedScroll")&&frame.Find("CommandRail/GuideNext"),"Command desk has scrollable feed and actionable next step");
                foreach(string name in new[]{"CityDistrict","CityOperations","TopMission","TopIntel","TopVehicles","TopDestinations"})
                {
                    var button=frame.Find(name);
                    Check(button&&(!button.Find("Icon")||!button.Find("Icon").gameObject.activeSelf),name+" is text-only");
                }
                Check(frame.Find("TopMission")?.GetComponentInChildren<TextMeshProUGUI>().text.Contains("/")==true,"Top mission includes live completed/total count");
                Check(frame.Find("StatusStrip/Stat2/HeatFill"),"Heat indicator is in top strip");
                Check(!frame.Find("BattleHUD/ObjectiveSlot").gameObject.activeSelf,"Home objective panel removed");
                Check((frame.Find("CameraSetup") as RectTransform).anchorMin.x==0,"Camera controls pinned left");
                Check(frame.Find("StatusStrip/Stat2/Value").GetComponent<HudValuePulse>(),"Police heat has local resource animation");
                Check(hud.cash.GetComponent<HudValuePulse>(),"Cash has local resource animation");
            }
            var matchday=Object.FindFirstObjectByType<StadiumMatchdayActivity>();
            Check(matchday&&matchday.SimulatedGroupCount>=7,"Home, rival and police groups exist as simulation state");
            Check(matchday&&matchday.StreamedBodyCount<=CityActivityStreaming.BodyBudget,"Matchday actors remain within global crowd budget");
            Check(CityActivityStreaming.Instance&&CityActivityStreaming.Instance.LiveAmbientBodies<=CityActivityStreaming.BodyBudget,"Total ambient actor budget respected");
            Check(!CityActivityStreaming.Interested(new Vector3(100000,0,100000)),"Distant districts do not instantiate ambient actors");
            var map=LiveMiniMap.Instance;
            Check(map&&map.RunDragResponseSelfCheck(),"Expanded minimap labels respond during drag");
            Check(map&&map.LabelPoolHealthy,"Minimap labels have unique pooled objects");
            var node=CityOperationsSystem.Instance?.Nodes.FirstOrDefault(n=>n&&!CityOperationsLedger.IsComplete(GameManager.Data,n.Type));
            node?.OpenInteraction();
            var world=node?node.GetComponent<WorldChoiceBar>():null;
            Check(world&&world.IsOpen,"Operation GO/START world choices open");world?.Hide();
            var gang=Object.FindObjectsByType<GangScreenChoice>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault();
            gang?.Show(()=>{},()=>{},()=>{});
            Check(gang&&gang.IsOpen,"Rival tactical popup opens");gang?.Hide();
            GamePopup.Instance.Show("RTS CHECK","Popup presentation check",new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));
            Check(GamePopup.AnyOpen,"2D decision popup opens");GamePopup.Instance.Hide();
            Check(!GamePopup.AnyOpen,"2D decision popup closes");
            CityOperationsSystem.Instance?.OpenBoard();
            Check(CityOperationsSystem.Instance&&CityOperationsSystem.Instance.IsBoardOpen,"Operations board opens");
            CityOperationsSystem.Instance?.CloseBoard();
            CityDevelopmentSystem.Instance?.OpenBoard();
            Check(CityDevelopmentSystem.Instance&&CityDevelopmentSystem.Instance.IsOpen,"Development board opens");
            Check(AgentSelectionManager.BlocksWorldTap(),"Development panel owns world input");
            CityDevelopmentSystem.Instance?.CloseBoard();
            Check(CityDevelopmentSystem.Instance&&!CityDevelopmentSystem.Instance.IsOpen,"Development board closes");
            if(CityGameplay.Instance)Check(CityLandmarks.HasTacticalClearance(CityGameplay.Instance.Locations[2]),"Shops approach has overhead and camera clearance");
            lines.Add("INFO Camera focus: "+CityActivityStreaming.GroundFocus(Camera.main));
            if(CityGameplay.Instance)for(int i=0;i<CityGameplay.Instance.Locations.Length;i++)lines.Add("INFO "+CityGameplay.Instance.LocationNames[i]+" "+CityGameplay.Instance.Locations[i]);
        }
        lines.Add($"RESULT {lines.Count(x=>x.StartsWith("PASS"))} passed, {lines.Count(x=>x.StartsWith("FAIL"))} failed");
        Directory.CreateDirectory("Artifacts/CityQA");File.WriteAllLines("Artifacts/CityQA/rts-clarity-checks.txt",lines);
        Debug.Log(lines[lines.Count-1]);
    }
}
#endif
