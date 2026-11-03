using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameAdsMode { Disabled, Test, Live }

/// <summary>One project configuration for optional mobile advertisements.</summary>
[CreateAssetMenu(menuName="Hooligan Manager/Ads Configuration",fileName="AdsConfiguration")]
public sealed class AdsConfiguration : ScriptableObject
{
    public const string ResourcePath="Ads/AdsConfiguration";
    public const string DemoPublisher="ca-app-pub-3940256099942544";
    [Tooltip("Disabled does not initialize the SDK. Test uses Google sample IDs. Live requires your own IDs.")]
    public GameAdsMode mode=GameAdsMode.Test;
    [Tooltip("An explicitly labelled simulator in the Unity Editor. No network ad requests or revenue.")]
    public bool editorSimulation=true;
    public bool rewardedEnabled=true;
    [Tooltip("Only called at a deliberate menu/result transition. Never automatic during city RTS.")]
    public bool interstitialEnabled=false;
    [Min(60)] public float interstitialIntervalSeconds=180;
    [Min(60)] public float rewardIntervalSeconds=180;
    [Range(0,500)] public int optionalSupplyCash=200;
    public PlatformIds android=new PlatformIds {
        testAppId=DemoPublisher+"~3347511713", testRewardedId=DemoPublisher+"/5224354917",testInterstitialId=DemoPublisher+"/1033173712"};
    public PlatformIds ios=new PlatformIds {
        testAppId=DemoPublisher+"~1458002511", testRewardedId=DemoPublisher+"/1712485313",testInterstitialId=DemoPublisher+"/4411468910"};
    [Tooltip("Optional hashed test device IDs for UMP and SDK test-device requests.")]
    public List<string> testDeviceIds=new List<string>();
    [Tooltip("Ad requests stop until UMP reports CanRequestAds. Configure privacy messages in AdMob for Live mode.")]
    public bool tagForUnderAgeOfConsent;

    [Serializable]
    public sealed class PlatformIds
    {
        public string testAppId,testRewardedId,testInterstitialId;
        public string liveAppId="",liveRewardedId="",liveInterstitialId="";
    }
    public PlatformIds ForPlatform(bool isIos)=>isIos?ios:android;
    public string AppId(bool isIos)=>mode==GameAdsMode.Live?ForPlatform(isIos).liveAppId:ForPlatform(isIos).testAppId;
    public string RewardedId(bool isIos)=>mode==GameAdsMode.Live?ForPlatform(isIos).liveRewardedId:ForPlatform(isIos).testRewardedId;
    public string InterstitialId(bool isIos)=>mode==GameAdsMode.Live?ForPlatform(isIos).liveInterstitialId:ForPlatform(isIos).testInterstitialId;

    public bool ValidFor(bool isIos,out string reason)
    {
        reason="";
        if(mode==GameAdsMode.Disabled)return true;
        var ids=ForPlatform(isIos);
        if(ids==null){reason="Platform ad IDs are missing.";return false;}
        string app=AppId(isIos),reward=RewardedId(isIos),interstitial=InterstitialId(isIos);
        if(!ValidId(app,'~')||rewardedEnabled&&!ValidId(reward,'/')||interstitialEnabled&&!ValidId(interstitial,'/'))
        {reason="Enter valid platform App ID and enabled ad-unit IDs in AdsConfiguration.";return false;}
        if(mode==GameAdsMode.Test&&(!app.StartsWith(DemoPublisher)||rewardedEnabled&&!reward.StartsWith(DemoPublisher)||interstitialEnabled&&!interstitial.StartsWith(DemoPublisher)))
        {reason="Test mode requires Google sample IDs; use the supplied platform defaults.";return false;}
        if(mode==GameAdsMode.Live&&(app.StartsWith(DemoPublisher)||rewardedEnabled&&reward.StartsWith(DemoPublisher)||interstitialEnabled&&interstitial.StartsWith(DemoPublisher)))
        {reason="Live mode requires your AdMob IDs rather than sample IDs.";return false;}
        return true;
    }
    public static bool ValidId(string id,char separator)
    {
        if(string.IsNullOrEmpty(id)||!id.StartsWith("ca-app-pub-"))return false;
        string[] parts=id.Substring(11).Split(separator);
        return parts.Length==2&&parts[0].Length==16&&parts[1].Length==10&&Digits(parts[0])&&Digits(parts[1]);
    }
    static bool Digits(string value){foreach(char c in value)if(c<'0'||c>'9')return false;return true;}
}
