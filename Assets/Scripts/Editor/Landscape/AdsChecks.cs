#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AdsChecks
{
    [MenuItem("Hooligan/Ads/Run configuration and reward checks")]
    public static void Run()
    {
        Directory.CreateDirectory("Artifacts/Recovery");var rows=new List<string>();
        void Check(bool value,string name)=>rows.Add((value?"PASS ":"FAIL ")+name);
        var config=ScriptableObject.CreateInstance<AdsConfiguration>();
        try
        {
            Check(config.mode==GameAdsMode.Test&&config.ValidFor(false,out _)&&config.ValidFor(true,out _),"Fresh configuration defaults to valid Google sample IDs on Android and iOS");
            Check(config.RewardedId(false).EndsWith("5224354917")&&config.RewardedId(true).EndsWith("1712485313"),"Rewarded sample units use the correct platform IDs");
            config.android.testRewardedId="ca-app-pub-1234567890123456/1234567890";
            Check(!config.ValidFor(false,out _),"Test mode rejects a non-sample rewarded unit");
            config.android.testRewardedId=AdsConfiguration.DemoPublisher+"/5224354917";
            config.mode=GameAdsMode.Live;
            Check(!config.ValidFor(false,out _),"Live mode rejects missing publisher IDs");
            config.android.liveAppId=config.android.testAppId;config.android.liveRewardedId=config.android.testRewardedId;
            Check(!config.ValidFor(false,out _),"Live mode rejects Google demonstration IDs");
            config.android.liveAppId="ca-app-pub-1234567890123456~1234567890";config.android.liveRewardedId="ca-app-pub-1234567890123456/1234567890";
            Check(config.ValidFor(false,out _),"Live configuration accepts correctly formed publisher app and rewarded IDs");
            config.interstitialEnabled=true;
            Check(!config.ValidFor(false,out _),"Enabled interstitials require their own ad-unit ID");
            config.mode=GameAdsMode.Disabled;config.android=null;
            Check(config.ValidFor(false,out _),"Disabled mode requires no advertising account IDs");
            int grants=0,closes=0;bool rewarded=false;
            var attempt=new RewardedAdAttempt(()=>grants++,value=>{closes++;rewarded=value;});
            Check(attempt.Grant()&&!attempt.Grant()&&grants==1,"A duplicate SDK reward callback awards once");
            attempt.Close();attempt.Close();
            Check(closes==1&&rewarded,"A completed fullscreen attempt closes once and reports its reward");
            var cancelled=new RewardedAdAttempt(()=>grants++,value=>{closes++;rewarded=value;});cancelled.Close();
            Check(!cancelled.Grant()&&grants==1&&!rewarded,"Cancel/failure or a late callback cannot award a reward");
            var next=new RewardedAdAttempt(()=>grants++,null);attempt.Grant();
            Check(!next.RewardGranted&&grants==1,"A callback from an older attempt cannot credit a newer attempt");
            var actual=Resources.Load<AdsConfiguration>(AdsConfiguration.ResourcePath);
            Check(actual&&actual.mode==GameAdsMode.Test&&actual.ValidFor(false,out _)&&actual.ValidFor(true,out _),"Committed project asset is configured for test ads on both platforms");
            Check(!actual.interstitialEnabled,"Forced transition ads are disabled by default");
        }
        finally{UnityEngine.Object.DestroyImmediate(config);File.WriteAllLines("Artifacts/Recovery/ads-checks.txt",rows);}
        if(rows.Any(x=>x.StartsWith("FAIL ")))throw new Exception("Ads checks failed; inspect Artifacts/Recovery/ads-checks.txt.");
        Debug.Log($"Ads configuration/reward checks: {rows.Count} passed. No network ad or device validation claimed.");
    }
}
#endif
