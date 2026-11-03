using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UnityConsent;

/// <summary>Optional Unity IAP 5 store adapter. No gameplay command depends on payment.</summary>
public sealed class GamePurchasesService:MonoBehaviour
{
    static GamePurchasesService instance;
    public static GamePurchasesService Instance
    {
        get{if(!instance)instance=new GameObject("Optional Purchases Service").AddComponent<GamePurchasesService>();return instance;}
    }
    PurchasesConfiguration config;
    StoreController store;
    bool connecting,ready,purchasing,eventsBound;
    string activePurchaseKey,activePurchaseWallet;
#if UNITY_EDITOR
    // Local integration checks observe the confirmation boundary without a store.
    public Action<PendingOrder> EditorConfirmPurchaseOverride;
#endif
    string status="Optional purchases have not been initialized.";
    public string Status=>status;
    public PurchasesConfiguration Configuration=>config;
    public bool IsEditorTest=>Application.isEditor&&config&&config.mode==GamePurchasesMode.EditorTest;
    public bool IsDisabled=>!config||config.mode==GamePurchasesMode.Disabled||config.mode==GamePurchasesMode.EditorTest&&!Application.isEditor;
    static bool Ios=>Application.platform==RuntimePlatform.IPhonePlayer;
    public static bool HasNoInterruptions=>GameManager.Data?.RemoveAdsEntitlement==true||PurchaseReceiptLedger.Load().removeInterruptions;
    bool HasPlacement(PurchaseProductConfig product)
    {
        if(product.benefit!=PurchaseBenefit.RemoveInterruptions)return true;
        var ads=Resources.Load<AdsConfiguration>(AdsConfiguration.ResourcePath);
        return ads&&ads.interstitialEnabled&&ads.mode!=GameAdsMode.Disabled;
    }
    void Awake()
    {
        if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;DontDestroyOnLoad(gameObject);
        config=Resources.Load<PurchasesConfiguration>(PurchasesConfiguration.ResourcePath);
    }
    public static void OpenShop()=>Instance.ShowShop();
    public async void Initialize()
    {
        if(IsDisabled){status="Purchases are disabled on this build. Core game remains available.";return;}
        if(PoliceCityMode.Active){status="Optional cash support belongs to a firm campaign. Return to your firm to use the shop.";return;}
        if(IsEditorTest){status="EDITOR PURCHASE TEST - no payment, no store charge.";return;}
        if(Application.isEditor||!Application.isMobilePlatform){status="Native store purchasing runs only on configured Android/iOS store builds.";return;}
        if(!config.ValidForStore(Ios,out string reason)){status=reason;return;}
        if(ready||connecting)return;
        connecting=true;status="Connecting to the platform store...";
        try
        {
            // This local catalog uses platform-native billing, without remote
            // catalog/authentication. Optional Unity analytics/ad use is denied
            // unless the product later implements its own consent preferences.
            EndUserConsent.SetConsentState(new ConsentState{AnalyticsIntent=ConsentStatus.Denied,AdsIntent=ConsentStatus.Denied});
            if(store==null)
            {
                store=UnityIAPServices.StoreController();
                // Retry fetched, unconfirmed orders ourselves. The SDK suppresses
                // repeat pending events within a session, but our wallet may have
                // been unavailable during Police or a different firm campaign.
                store.ProcessPendingOrdersOnPurchasesFetched(false);
            }
            if(!eventsBound)
            {
            eventsBound=true;
            store.OnPurchasePending+=Pending;
            store.OnPurchaseFailed+=order=>{
                purchasing=false;var ledger=PurchaseReceiptLedger.Load();
                ledger.intents.RemoveAll(i=>i.product==activePurchaseKey&&i.wallet==activePurchaseWallet);PurchaseReceiptLedger.Save(ledger);
                activePurchaseKey=activePurchaseWallet=null;status="Purchase failed or cancelled. No benefit was granted.";};
            store.OnPurchaseDeferred+=order=>{purchasing=false;status="Purchase awaits store approval. No benefit has been granted.";};
            store.OnStoreDisconnected+=failure=>{ready=connecting=false;status="Store unavailable. You can continue playing without purchases.";};
            store.OnProductsFetchFailed+=failure=>{ready=connecting=false;status="Products unavailable. Verify configured store IDs and tester account.";};
            store.OnPurchasesFetchFailed+=failure=>{status="Purchase restore unavailable. Retry when the store is connected.";};
            store.OnProductsFetched+=products=>{connecting=false;ready=true;status="Store connected. Prices below are supplied by the store.";store.FetchPurchases();};
            store.OnPurchasesFetched+=RestoreFetched;
            }
            await store.Connect();
            store.FetchProducts(config.products.Select(product=>new ProductDefinition(product.key,product.StoreId(Ios),product.benefit==PurchaseBenefit.FirmCash?ProductType.Consumable:ProductType.NonConsumable)).ToList());
        }
        catch(Exception error){connecting=false;ready=false;status="Store connection failed: "+error.Message;}
    }
    void EnsureWallet()
    {
        var wallet=GameManager.Data;if(wallet==null||PoliceCityMode.Active)return;
        bool changed=false;
        if(string.IsNullOrEmpty(wallet.PurchaseWalletId)){wallet.PurchaseWalletId=Guid.NewGuid().ToString("N");changed=true;}
        if(PurchaseReceiptLedger.Load().removeInterruptions&&!wallet.RemoveAdsEntitlement){wallet.RemoveAdsEntitlement=true;changed=true;}
        if(changed)GameManager.Save();
    }
    string Price(PurchaseProductConfig product)
    {
        if(IsEditorTest)return "EDITOR TEST · NO CHARGE";
        var fetched=store?.GetProducts()?.FirstOrDefault(p=>p.definition.id==product.key);
        return ready&&fetched!=null&&fetched.availableToPurchase?fetched.metadata.localizedPriceString:"STORE UNAVAILABLE";
    }
    public void ShowShop()
    {
        EnsureWallet();Initialize();
        var options=new List<GamePopup.Option>();
        if(config!=null&&config.products!=null)
        foreach(var product in config.products)
        {
            var item=product;bool owned=item.benefit==PurchaseBenefit.RemoveInterruptions&&HasNoInterruptions;
            if(!owned&&!HasPlacement(item))continue;
            options.Add(new GamePopup.Option(owned?item.title+" · OWNED":item.title+" · "+Price(item),LandscapeUI.Green,()=>Request(item.key),!owned&&!IsDisabled&&!PoliceCityMode.Active&&!purchasing&&(IsEditorTest||ready)));
        }
        options.Add(new GamePopup.Option("RESTORE PURCHASES",LandscapeUI.PanelColor,Restore,!IsDisabled&&!IsEditorTest&&ready));
        options.Add(new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));
        GamePopup.Instance.Show("OPTIONAL SUPPORT SHOP",status+"\n\n"+(config==null?"Configuration missing.":string.Join("\n\n",config.products.Select(p=>p.title+": "+p.description)))+"\n\nMovement, guard, patrol, recovery and campaign progression never require a purchase.",options.ToArray());
    }
    public bool Request(string key)
    {
        EnsureWallet();var wallet=GameManager.Data;var product=config?.Find(key);
        if(IsDisabled||wallet==null||PoliceCityMode.Active||product==null||purchasing)return false;
        if(!HasPlacement(product)){status="This build has no interrupting ads, so an ad-removal purchase is unnecessary.";return false;}
        if(product.benefit==PurchaseBenefit.RemoveInterruptions&&HasNoInterruptions)return false;
        if(IsEditorTest)
        {
            string walletId=wallet.PurchaseWalletId;
            GamePopup.Instance.Show("EDITOR PURCHASE TEST","This is a local simulator. It does not contact Google Play or Apple and will not charge money.\n\n"+product.description,
                new GamePopup.Option("SIMULATE SUCCESS · NO CHARGE",LandscapeUI.Green,()=>{
                    if(GameManager.Data!=wallet||PoliceCityMode.Active){status="Campaign changed. Editor test cancelled without delivery.";return;}
                    Deliver(product,"editor-"+Guid.NewGuid().ToString("N"),walletId,false);}),
                new GamePopup.Option("CANCEL",LandscapeUI.PanelColor,()=>status="Editor purchase test cancelled. No benefit granted."));return true;
        }
        if(!ready||store==null)return false;
        var fetched=store.GetProducts().FirstOrDefault(p=>p.definition.id==key&&p.availableToPurchase);
        if(fetched==null){status="This product is not available from the connected store.";return false;}
        var receipts=PurchaseReceiptLedger.Load();
        if(receipts.intents.Any(i=>i.product==key&&i.wallet!=wallet.PurchaseWalletId))
        {status="An unfinished purchase for this product belongs to another firm. Continue that firm to recover it before purchasing again.";return false;}
        receipts.intents.RemoveAll(i=>i.product==key);
        receipts.intents.Add(new PurchaseIntent{product=key,wallet=wallet.PurchaseWalletId});PurchaseReceiptLedger.Save(receipts);
        activePurchaseKey=key;activePurchaseWallet=wallet.PurchaseWalletId;purchasing=true;status="Waiting for platform purchase confirmation...";
        try{store.PurchaseProduct(fetched);return true;}catch(Exception error){purchasing=false;status="Purchase could not start: "+error.Message;return false;}
    }
    void Pending(PendingOrder order)
    {
        purchasing=false;
        var items=order?.CartOrdered?.Items();
        if(items==null||items.Count!=1||items[0].Quantity!=1){status="Unsupported purchase cart. It remains unconfirmed for recovery.";return;}
        var product=config?.Find(items[0].Product.definition.id);if(product==null)return;
        var receipts=PurchaseReceiptLedger.Load();var intent=receipts.intents.FirstOrDefault(i=>i.product==product.key);
        if(Deliver(product,order.Info.TransactionID,intent?.wallet,false))
        {
#if UNITY_EDITOR
            if(EditorConfirmPurchaseOverride!=null){EditorConfirmPurchaseOverride(order);return;}
#endif
            store.ConfirmPurchase(order);
        }
    }
    bool Deliver(PurchaseProductConfig product,string transaction,string expectedWallet,bool restore)
    {
        if(PoliceCityMode.Active||GameManager.Data==null){status="Purchase delivery waits for the original firm campaign.";return false;}
        if(string.IsNullOrEmpty(transaction)){status="Store has not supplied a transaction identity. Delivery remains pending.";return false;}
        var receipts=PurchaseReceiptLedger.Load();string token=product.key+":"+transaction;
        if(receipts.delivered.Contains(token)){status="Purchase already delivered. No duplicate benefit.";return true;}
        var wallet=GameManager.Data;
        if(!PurchaseFulfillment.TryApply(wallet,product,transaction,expectedWallet,restore,out string reason))
        {
            if(wallet.FulfilledPurchaseTransactions?.Contains(token)!=true){status=reason;return false;}
        }
        // The benefit and wallet receipt marker share one native campaign save.
        // A crash before the global ledger write is replay-safe through that marker.
        GameManager.Save();receipts.delivered.Add(token);
        if(product.benefit==PurchaseBenefit.RemoveInterruptions)receipts.removeInterruptions=true;
        receipts.intents.RemoveAll(i=>i.product==product.key);PurchaseReceiptLedger.Save(receipts);
        status="Purchase delivered and saved. Thank you for supporting the game.";CityGameplay.Instance?.PostEvent(status);return true;
    }
    void RestoreFetched(Orders orders)
    {
        if(orders==null)return;
        if(orders.PendingOrders!=null)
            foreach(var pending in orders.PendingOrders)Pending(pending);
        if(orders.ConfirmedOrders==null)return;
        foreach(var order in orders.ConfirmedOrders)
        foreach(var item in order.CartOrdered.Items())
        {
            var product=config?.Find(item.Product.definition.id);
            if(product?.benefit==PurchaseBenefit.RemoveInterruptions)Deliver(product,order.Info.TransactionID,null,true);
        }
    }
    void Restore()
    {
        if(!ready||store==null||PoliceCityMode.Active)return;
        status="Restoring non-consumable purchases from the platform store...";
        store.RestoreTransactions((success,message)=>{status=success?"Restore requested. Owned benefits will be synchronized.":"Restore unavailable: "+message;if(success)store.FetchPurchases();});
    }
}

[Serializable]public sealed class PurchaseIntent{public string product,wallet;}
[Serializable]public sealed class PurchaseReceiptState
{
    public List<string> delivered=new List<string>();
    public List<PurchaseIntent> intents=new List<PurchaseIntent>();
    public bool removeInterruptions;
}
public static class PurchaseReceiptLedger
{
    const string Key="HM.OptionalPurchases.v1";
    static string IsolatedPath
    {
        get{
#if UNITY_EDITOR
            if(!string.IsNullOrEmpty(SaveDataInLocal.EditorSaveOverride))return SaveDataInLocal.EditorSaveOverride+".purchases.json";
#endif
            return null;
        }
    }
    public static PurchaseReceiptState Load()
    {
        string json=IsolatedPath!=null?(File.Exists(IsolatedPath)?File.ReadAllText(IsolatedPath):""):PlayerPrefs.GetString(Key,"");
        PurchaseReceiptState state=null;try{if(!string.IsNullOrEmpty(json))state=JsonUtility.FromJson<PurchaseReceiptState>(json);}catch(ArgumentException){}
        state??=new PurchaseReceiptState();state.delivered??=new List<string>();state.intents??=new List<PurchaseIntent>();return state;
    }
    public static void Save(PurchaseReceiptState state)
    {
        string json=JsonUtility.ToJson(state);
        if(IsolatedPath!=null){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(IsolatedPath)));File.WriteAllText(IsolatedPath,json);}
        else{PlayerPrefs.SetString(Key,json);PlayerPrefs.Save();}
    }
}
