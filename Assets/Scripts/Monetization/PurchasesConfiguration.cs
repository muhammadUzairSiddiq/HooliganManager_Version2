using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GamePurchasesMode{Disabled,EditorTest,Store}
public enum PurchaseBenefit{FirmCash,RemoveInterruptions}

[CreateAssetMenu(menuName="Hooligan Manager/Purchases Configuration",fileName="PurchasesConfiguration")]
public sealed class PurchasesConfiguration:ScriptableObject
{
    public const string ResourcePath="Purchases/PurchasesConfiguration";
    [Tooltip("EditorTest is an explicit local simulator and never calls a payment store. Store requires configured native product IDs and store-account testing.")]
    public GamePurchasesMode mode=GamePurchasesMode.EditorTest;
    public List<PurchaseProductConfig> products=new List<PurchaseProductConfig>{
        new PurchaseProductConfig{key="firm_cash",title="OPTIONAL FIRM SUPPORT",description="Adds £1,000 to this firm. Businesses and missions earn money without purchases.",benefit=PurchaseBenefit.FirmCash,cash=1000},
        new PurchaseProductConfig{key="no_interruptions",title="NO FORCED AD INTERRUPTIONS",description="Permanently disables forced transition ads. Rewarded ads stay optional. Forced ads are already disabled in this review configuration.",benefit=PurchaseBenefit.RemoveInterruptions}};
    public PurchaseProductConfig Find(string key)=>products?.FirstOrDefault(p=>p!=null&&p.key==key);
    public bool ValidForStore(bool ios,out string reason)
    {
        if(products==null||products.Count==0){reason="No purchase products configured.";return false;}
        var ids=new HashSet<string>();var keys=new HashSet<string>();
        foreach(var product in products)
        {
            if(product==null||string.IsNullOrWhiteSpace(product.key)||!keys.Add(product.key)||string.IsNullOrWhiteSpace(product.StoreId(ios))||!ids.Add(product.StoreId(ios)))
            {reason="Each store product needs a unique key and the platform's actual configured product ID.";return false;}
            if(product.benefit==PurchaseBenefit.FirmCash&&(product.cash<1||product.cash>100000))
            {reason="Cash support amount must be between 1 and 100000.";return false;}
        }
        reason="";return true;
    }
}

[Serializable]
public sealed class PurchaseProductConfig
{
    public string key,title,description,androidId="",iosId="";
    public PurchaseBenefit benefit;
    [Min(0)] public int cash;
    public string StoreId(bool ios)=>ios?iosId:androidId;
}

/// <summary>One transaction changes the wallet and its receipt list together.</summary>
public static class PurchaseFulfillment
{
    public static bool TryApply(PlayerData wallet,PurchaseProductConfig product,string transactionId,string expectedWallet,bool restore,out string reason)
    {
        if(wallet==null||product==null||string.IsNullOrWhiteSpace(transactionId)){reason="Missing purchase, wallet or transaction identity.";return false;}
        if(product.benefit==PurchaseBenefit.FirmCash&&(restore||string.IsNullOrEmpty(expectedWallet)||wallet.PurchaseWalletId!=expectedWallet))
        {reason="Consumable purchase belongs to its initiating firm. It was not credited to a different campaign.";return false;}
        wallet.FulfilledPurchaseTransactions??=new List<string>();
        string token=product.key+":"+transactionId;
        if(wallet.FulfilledPurchaseTransactions.Contains(token)){reason="Already delivered.";return false;}
        if(product.benefit==PurchaseBenefit.FirmCash)
        {
            if(product.cash<=0||product.cash>100000||wallet.Money>int.MaxValue-product.cash){reason="Invalid cash support amount or wallet capacity.";return false;}
            wallet.Money+=product.cash;
        }
        else wallet.RemoveAdsEntitlement=true;
        wallet.FulfilledPurchaseTransactions.Add(token);reason="Delivered and ready to save.";return true;
    }
}
