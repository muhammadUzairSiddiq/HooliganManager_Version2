using System;
using System.Collections.Concurrent;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

/// <summary>Consent-gated Google Mobile Ads adapter. Gameplay never depends on an ad.</summary>
public sealed class GameAdsService : MonoBehaviour
{
    public static GameAdsService Instance
    {
        get
        {
            if(!instance)instance=new GameObject("Optional Ads Service").AddComponent<GameAdsService>();
            return instance;
        }
    }
    static GameAdsService instance;
    readonly ConcurrentQueue<Action> mainThread=new ConcurrentQueue<Action>();
    AdsConfiguration config;
    RewardedAd rewarded;
    InterstitialAd interstitial;
    RewardedAdAttempt attempt;
    Action<bool> interstitialClosed;
    bool gatheringConsent,initializing,initialized,loadingReward,loadingInterstitial;
    float nextRewardLoad,nextInterstitialLoad,rewardLoadedAt,interstitialLoadedAt,lastInterstitialAt=-10000;
    float priorTimeScale=1f;
    bool fullscreen;
    string status="Optional ads have not been initialized.";
    public AdsConfiguration Configuration=>config;
    public string Status=>status;
    public bool IsShowing=>fullscreen;
    public bool IsEditorSimulation=>Application.isEditor&&config&&config.mode==GameAdsMode.Test&&config.editorSimulation;
    public bool IsDisabled=>!config||config.mode==GameAdsMode.Disabled;
    public bool CanShowRewarded=>!fullscreen&&!IsDisabled&&config.rewardedEnabled&&(IsEditorSimulation||rewarded!=null&&rewarded.CanShowAd());
    public bool CanShowInterstitial=>!fullscreen&&!IsDisabled&&config.interstitialEnabled&&!GamePurchasesService.HasNoInterruptions&&!Application.isEditor&&interstitial!=null&&interstitial.CanShowAd()&&Time.realtimeSinceStartup-lastInterstitialAt>=config.interstitialIntervalSeconds;
    public bool PrivacyOptionsRequired=>!Application.isEditor&&ConsentInformation.PrivacyOptionsRequirementStatus==PrivacyOptionsRequirementStatus.Required;
    static bool IsIos=>Application.platform==RuntimePlatform.IPhonePlayer;

    void Awake()
    {
        if(instance&&instance!=this){Destroy(gameObject);return;}
        instance=this;DontDestroyOnLoad(gameObject);config=Resources.Load<AdsConfiguration>(AdsConfiguration.ResourcePath);
        if(!config)status="AdsConfiguration is missing; ads remain disabled.";
    }
    void Update()
    {
        for(int i=0;i<16&&mainThread.TryDequeue(out var action);i++)
            try{action?.Invoke();}catch(Exception error){Debug.LogException(error);}
        if(!initialized||IsDisabled||Application.isEditor||fullscreen)return;
        if(rewarded!=null&&Time.realtimeSinceStartup-rewardLoadedAt>=3300){rewarded.Destroy();rewarded=null;}
        if(interstitial!=null&&Time.realtimeSinceStartup-interstitialLoadedAt>=3300){interstitial.Destroy();interstitial=null;}
        if(config.rewardedEnabled&&rewarded==null&&!loadingReward&&Time.realtimeSinceStartup>=nextRewardLoad)LoadRewarded();
        if(config.interstitialEnabled&&interstitial==null&&!loadingInterstitial&&Time.realtimeSinceStartup>=nextInterstitialLoad)LoadInterstitial();
    }
    public void Initialize()
    {
        if(IsDisabled){status="Ads are disabled in the project configuration.";return;}
        if(!config.ValidFor(IsIos,out var reason)){status=reason;return;}
        if(Application.isEditor)
        {status=IsEditorSimulation?"EDITOR TEST SIMULATION - no real ad request or revenue.":"Device ads run only on Android/iOS. Editor simulation is disabled.";return;}
        if(!Application.isMobilePlatform){status="Ads are unavailable on this platform.";return;}
        if(initialized||initializing||gatheringConsent)return;
        gatheringConsent=true;status="Checking ad privacy choices...";
        var parameters=new ConsentRequestParameters{TagForUnderAgeOfConsent=config.tagForUnderAgeOfConsent};
        if(config.mode==GameAdsMode.Test&&config.testDeviceIds.Count>0)
            parameters.ConsentDebugSettings=new ConsentDebugSettings{TestDeviceHashedIds=config.testDeviceIds};
        try
        {
            ConsentInformation.Update(parameters,error=>mainThread.Enqueue(()=>
            {
                if(error!=null)
                {gatheringConsent=false;status="Privacy check unavailable: "+error.Message;if(ConsentInformation.CanRequestAds())InitializeSdk();return;}
                ConsentForm.LoadAndShowConsentFormIfRequired(formError=>mainThread.Enqueue(()=>
                {
                    gatheringConsent=false;
                    if(ConsentInformation.CanRequestAds())InitializeSdk();
                    else status=formError==null?"Ads are unavailable with the current privacy choices.":"Privacy form unavailable: "+formError.Message;
                }));
            }));
        }
        catch(Exception error){gatheringConsent=false;status="Ad privacy service unavailable: "+error.Message;}
    }
    void InitializeSdk()
    {
        if(initialized||initializing||!ConsentInformation.CanRequestAds()||IsDisabled)return;
        initializing=true;status="Preparing optional ads...";
        try
        {
            MobileAds.SetiOSAppPauseOnBackground(true);
            MobileAds.SetRequestConfiguration(new RequestConfiguration{TestDeviceIds=config.testDeviceIds});
            MobileAds.Initialize(result=>mainThread.Enqueue(()=>
            {
                initializing=false;initialized=result!=null;
                status=initialized?"Loading optional ads...":"Ad service could not initialize. You can keep playing.";
                if(initialized){if(config.rewardedEnabled)LoadRewarded();if(config.interstitialEnabled)LoadInterstitial();}
            }));
        }
        catch(Exception error){initializing=false;status="Ad service unavailable: "+error.Message;}
    }
    void LoadRewarded()
    {
        if(loadingReward||!ConsentInformation.CanRequestAds())return;
        loadingReward=true;nextRewardLoad=Time.realtimeSinceStartup+30;
        RewardedAd.Load(config.RewardedId(IsIos),new AdRequest(),(ad,error)=>mainThread.Enqueue(()=>
        {
            loadingReward=false;
            if(error!=null||ad==null){status="No rewarded ad available. Try later or continue playing.";ad?.Destroy();return;}
            rewarded?.Destroy();rewarded=ad;rewardLoadedAt=Time.realtimeSinceStartup;status=config.mode==GameAdsMode.Test?"GOOGLE TEST AD READY":"Optional reward available";
        }));
    }
    void LoadInterstitial()
    {
        if(loadingInterstitial||!ConsentInformation.CanRequestAds())return;
        loadingInterstitial=true;nextInterstitialLoad=Time.realtimeSinceStartup+30;
        InterstitialAd.Load(config.InterstitialId(IsIos),new AdRequest(),(ad,error)=>mainThread.Enqueue(()=>
        {
            loadingInterstitial=false;
            if(error!=null||ad==null){ad?.Destroy();return;}
            interstitial?.Destroy();interstitial=ad;interstitialLoadedAt=Time.realtimeSinceStartup;
        }));
    }
    public bool TryShowRewarded(string placement,Action onReward,Action<bool> onClosed=null)
    {
        Initialize();if(!CanShowRewarded)return false;
        attempt=new RewardedAdAttempt(onReward,onClosed);
        if(IsEditorSimulation){ShowEditorSimulation();return true;}
        var currentAttempt=attempt;
        var current=rewarded;rewarded=null;EnterFullscreen();
        current.OnAdFullScreenContentClosed+=()=>mainThread.Enqueue(()=>{if(attempt==currentAttempt)FinishReward();current.Destroy();});
        current.OnAdFullScreenContentFailed+=error=>mainThread.Enqueue(()=>{status="Ad could not open. No reward was awarded.";if(attempt==currentAttempt)FinishReward();current.Destroy();});
        try{current.Show(reward=>mainThread.Enqueue(()=>currentAttempt.Grant()));}
        catch(Exception error){status="Ad could not open: "+error.Message;FinishReward();current.Destroy();}
        return true;
    }
    public bool TryShowTransitionInterstitial(Action<bool> onClosed=null)
    {
        Initialize();if(!CanShowInterstitial)return false;
        // Calling this API is deliberate; the service has no automatic timer trigger.
        var current=interstitial;interstitial=null;interstitialClosed=onClosed;lastInterstitialAt=Time.realtimeSinceStartup;EnterFullscreen();
        current.OnAdFullScreenContentClosed+=()=>mainThread.Enqueue(()=>{FinishInterstitial(true);current.Destroy();});
        current.OnAdFullScreenContentFailed+=error=>mainThread.Enqueue(()=>{FinishInterstitial(false);current.Destroy();});
        try{current.Show();}catch(Exception){FinishInterstitial(false);current.Destroy();}
        return true;
    }
    public void ShowPrivacyOptions()
    {
        if(!PrivacyOptionsRequired)return;
        ConsentForm.ShowPrivacyOptionsForm(error=>mainThread.Enqueue(()=>
        {
            if(error!=null)status="Privacy options unavailable: "+error.Message;
            if(!ConsentInformation.CanRequestAds()){rewarded?.Destroy();rewarded=null;interstitial?.Destroy();interstitial=null;status="Ads unavailable with the current privacy choices.";}
        }));
    }
    void EnterFullscreen(){if(fullscreen)return;priorTimeScale=Time.timeScale;fullscreen=true;Time.timeScale=0;}
    void LeaveFullscreen(){if(!fullscreen)return;fullscreen=false;Time.timeScale=priorTimeScale;}
    void FinishReward(){LeaveFullscreen();var current=attempt;attempt=null;current?.Close();nextRewardLoad=Time.realtimeSinceStartup+2;}
    void FinishInterstitial(bool shown){LeaveFullscreen();var callback=interstitialClosed;interstitialClosed=null;callback?.Invoke(shown);nextInterstitialLoad=Time.realtimeSinceStartup+2;}
    void ShowEditorSimulation()
    {
        EnterFullscreen();
        GamePopup.Instance.Show("EDITOR TEST AD SIMULATION","This is a local test panel, not a Google ad. No ad request, impression or revenue is generated.\n\nComplete simulates the SDK reward callback. Cancel gives no reward.",
            ()=>FinishReward(),new GamePopup.Option("SIMULATE COMPLETION",LandscapeUI.Green,()=>{attempt?.Grant();FinishReward();}),
            new GamePopup.Option("CANCEL - NO REWARD",LandscapeUI.PanelColor,()=>FinishReward()));
    }
    void OnDestroy()
    {
        if(instance!=this)return;LeaveFullscreen();attempt?.Close();attempt=null;
        rewarded?.Destroy();interstitial?.Destroy();instance=null;
    }
}
