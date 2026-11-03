using UnityEngine;

/// <summary>Optional per-campaign supply grant; core RTS/recovery never asks for ads.</summary>
public static class GameAdRewards
{
    static float lastRewardAt=-10000f;
    public static string Availability
    {
        get
        {
            var ads=GameAdsService.Instance;var d=GameManager.Data;
            if(ads.IsDisabled)return "ADS DISABLED";
            if(d==null)return "ENTER YOUR FIRM FIRST";
            if(d.AdSupplyRewardMatchday>=d.MatchDay)return "SUPPLY REWARD CLAIMED TODAY";
            if(Time.realtimeSinceStartup-lastRewardAt<ads.Configuration.rewardIntervalSeconds)return "REWARD COOLDOWN";
            return ads.CanShowRewarded?"OPTIONAL SUPPLY REWARD":ads.Status;
        }
    }
    public static bool CanClaim()
    {
        var d=GameManager.Data;var ads=GameAdsService.Instance;
        return d!=null&&!ads.IsDisabled&&d.AdSupplyRewardMatchday<d.MatchDay&&Time.realtimeSinceStartup-lastRewardAt>=ads.Configuration.rewardIntervalSeconds;
    }
    public static bool TryOfferSupplyGrant()
    {
        var ads=GameAdsService.Instance;ads.Initialize();var d=GameManager.Data;
        if(!CanClaim())
        {GamePopup.Instance.Show("OPTIONAL SUPPLIES",Availability,new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));return false;}
        int day=d.MatchDay;int amount=ads.Configuration.optionalSupplyCash;
        GamePopup.Instance.Show("OPTIONAL SUPPLIES",$"Watch an optional reward ad for £{amount}. One supply reward per matchday.\n\nYou can earn money through businesses and missions without watching ads.\n\n"+ads.Status,
            new GamePopup.Option(ads.IsEditorSimulation?"EDITOR TEST SIMULATION":"WATCH REWARD AD",LandscapeUI.Green,()=>
            {
                bool shown=ads.TryShowRewarded("firm-supplies",()=>
                {
                    // A scene/new campaign change during a fullscreen ad cannot
                    // credit the wrong save or repeat a previously claimed day.
                    if(GameManager.Data!=d||d.MatchDay!=day||d.AdSupplyRewardMatchday>=day)return;
                    d.AdSupplyRewardMatchday=day;d.Money+=amount;lastRewardAt=Time.realtimeSinceStartup;GameManager.Save();
                    CityGameplay.Instance?.PostEvent($"OPTIONAL SUPPLIES +£{amount} - reward saved.");
                });
                if(!shown)GamePopup.Instance.Show("AD NOT READY",ads.Status+"\nNo reward was awarded. You can keep playing.",new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));
            },ads.CanShowRewarded),new GamePopup.Option("SKIP",LandscapeUI.PanelColor,null));
        return true;
    }
}
