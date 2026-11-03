#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Purchasing;

public static class PurchasesChecks
{
    [MenuItem("Hooligan/Purchases/Run isolated purchase and commerce checks")]
    public static void Run()
    {
        if(EditorApplication.isPlaying||PoliceCityMode.Active)throw new InvalidOperationException("Run from edit mode without an active police projection.");
        Directory.CreateDirectory("Artifacts/Recovery");var rows=new List<string>();
        void Check(bool value,string name)=>rows.Add((value?"PASS ":"FAIL ")+name);
        string previous=SaveDataInLocal.EditorSaveOverride;var config=ScriptableObject.CreateInstance<PurchasesConfiguration>();
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath("Artifacts/Recovery/purchases-check-save.json");
        try
        {
            Check(config.mode==GamePurchasesMode.EditorTest,"Default purchase configuration uses an explicit editor simulator");
            Check(!config.ValidForStore(false,out _)&&!config.ValidForStore(true,out _),"Unconfigured owner IDs cannot connect to a real payment store");
            for(int i=0;i<config.products.Count;i++){config.products[i].androidId="test.product.android."+i;config.products[i].iosId="test.product.ios."+i;}
            Check(config.ValidForStore(false,out _)&&config.ValidForStore(true,out _),"Distinct platform IDs form a valid store catalog");
            config.products[1].androidId=config.products[0].androidId;Check(!config.ValidForStore(false,out _),"Duplicate native product IDs are rejected");
            var wallet=new PlayerData{PurchaseWalletId="wallet-a",Money=50};wallet.RecruitedAgents.Add(new AgentData("Purchase QA"));
            var cash=config.Find("firm_cash");var entitlement=config.Find("no_interruptions");
            Check(!PurchaseFulfillment.TryApply(wallet,cash,"","wallet-a",false,out _)&&wallet.Money==50,"Missing transaction ID cannot deliver currency");
            Check(!PurchaseFulfillment.TryApply(wallet,cash,"txn-1","wallet-b",false,out _)&&wallet.Money==50,"A callback for another firm cannot credit this campaign");
            Check(!PurchaseFulfillment.TryApply(wallet,cash,"txn-1","wallet-a",true,out _)&&wallet.Money==50,"Restoring past consumables cannot create another currency grant");
            Check(PurchaseFulfillment.TryApply(wallet,cash,"txn-1","wallet-a",false,out _)&&wallet.Money==1050,"A valid matching transaction credits the declared currency once");
            Check(!PurchaseFulfillment.TryApply(wallet,cash,"txn-1","wallet-a",false,out _)&&wallet.Money==1050,"Duplicate callback does not grant currency twice");
            SaveDataInLocal.DataSave(wallet);var loaded=SaveDataInLocal.DataLoad();
            Check(loaded.Money==1050&&loaded.PurchaseWalletId=="wallet-a"&&loaded.FulfilledPurchaseTransactions.Contains("firm_cash:txn-1"),"Native campaign save stores currency and fulfillment token together");
            Check(!PurchaseFulfillment.TryApply(loaded,cash,"txn-1","wallet-a",false,out _)&&loaded.Money==1050,"Transaction replay after native reload cannot duplicate delivery");
            Check(PurchaseFulfillment.TryApply(loaded,entitlement,"ent-1",null,true,out _)&&loaded.RemoveAdsEntitlement,"Restored non-consumable unlocks its entitlement without currency");
            Check(!PurchaseFulfillment.TryApply(loaded,entitlement,"ent-1",null,true,out _)&&loaded.Money==1050,"Repeated entitlement restore cannot alter currency");
            var ledger=new PurchaseReceiptState{removeInterruptions=true};ledger.delivered.Add("firm_cash:txn-1");ledger.intents.Add(new PurchaseIntent{product="firm_cash",wallet="wallet-a"});PurchaseReceiptLedger.Save(ledger);
            var saved=PurchaseReceiptLedger.Load();Check(saved.removeInterruptions&&saved.delivered.Count==1&&saved.intents.Single().wallet=="wallet-a","Device receipt ledger and wallet-bound pending intent survive isolated native serialization");
            var commerce=new PlayerData{Money=1000,CitySupplies=5};CityBusinessLedger.Ensure(commerce);
            Check(!CityCommerceRules.Trade(commerce,"burger",true)&&commerce.Money==1000,"Uncontrolled venue cannot buy/sell resources");
            CityBusinessLedger.Control(commerce,"burger");
            Check(CityCommerceRules.Trade(commerce,"burger",true)&&commerce.Money==880&&commerce.CitySupplies==6,"Buying supplies exchanges the exact declared resources");
            Check(CityCommerceRules.Trade(commerce,"burger",false)&&commerce.Money==950&&commerce.CitySupplies==5,"Selling supplies has a lower price than buying and creates no arbitrage");
            Check(CityCommerceRules.Invest(commerce,"burger")&&commerce.Money==600&&commerce.CitySupplies==3,"First business improvement spends its cash/supply cost once");
            Check(CityBusinessLedger.Yield(commerce,CityBusinessLedger.Definitions[0],false)==122,"Improvement raises future venue income by its declared level multiplier");
            Check(!CityCommerceRules.Invest(commerce,"burger")&&commerce.Money==600,"Insufficient funds reject the next improvement without payment");
            commerce.Money=1000;Check(CityCommerceRules.Invest(commerce,"burger")&&CityBusinessLedger.Find(commerce,"burger").investmentLevel==2,"Second venue improvement reaches the saved maximum level");
            Check(!CityCommerceRules.Invest(commerce,"burger"),"Fully developed venues cannot replay upgrade spending");
            commerce.CitySupplies=99;Check(!CityCommerceRules.Trade(commerce,"burger",true),"Full supply storage cannot consume purchase funds");
            commerce.CitySupplies=0;Check(!CityCommerceRules.Trade(commerce,"burger",false),"An empty store cannot sell nonexistent resources");
            var away=new PlayerData{Money=1000};CityBusinessLedger.Ensure(away);
            Check(away.CityBusinesses.Count==6,"Fresh Home initializes only its original six business records");
            CityBusinessLedger.EnsureDistrict(away,2);CityBusinessLedger.EnsureDistrict(away,2);CityBusinessLedger.EnsureDistrict(away,3);
            Check(away.CityBusinesses.Count==12&&CityBusinessLedger.DistrictDefinitions(2).Length==3,"Visited Away districts add three scoped venues idempotently");
            Check(CityBusinessLedger.TryResolveDefinition("M2:burger",out var awayBurger)&&awayBurger.id=="M2:burger"&&awayBurger.income==72&&!CityBusinessLedger.TryResolveDefinition("M9:burger",out _),"Scoped definitions use declared district yields and reject unknown districts");
            CityBusinessLedger.Control(away,"M2:burger");
            Check(!CityBusinessLedger.Find(away,"burger").controlled&&!CityBusinessLedger.Find(away,"M3:burger").controlled,"Away ownership cannot grant Home or another district's venue control");
            CityBusinessLedger.Control(away,"burger");CityBusinessLedger.Control(away,"M3:burger");
            int districtIncome=CityBusinessLedger.Tick(away,90,false,2);
            Check(districtIncome==162&&CityBusinessLedger.Find(away,"M3:burger").accruedIncome==0,"One Away tick earns Home plus active district once, without inactive Away income");
            int beforeLoot=away.Money;Check(CityBusinessLedger.Loot(away,"M2:burger")&&away.Money==beforeLoot+awayBurger.loot,"Scoped Away loot pays its district amount once");
            Check(CityBusinessLedger.Find(away,"burger").lootCooldownSeconds==0&&!CityBusinessLedger.Loot(away,"M2:burger"),"Away loot cooldown is independent from Home and repeated loot cannot pay again");
            float awayCooldown=CityBusinessLedger.Find(away,"M2:burger").lootCooldownSeconds;
            CityBusinessLedger.Tick(away,90,false);
            Check(CityBusinessLedger.Find(away,"M2:burger").lootCooldownSeconds==awayCooldown,"Returning Home does not simulate an inactive Away venue's cooldown or trade");
            SaveDataInLocal.DataSave(away);var awayReload=SaveDataInLocal.DataLoad();
            Check(CityBusinessLedger.Find(awayReload,"M2:burger").controlled&&CityBusinessLedger.Find(awayReload,"M2:burger").lootCooldownSeconds==awayCooldown&&CityBusinessLedger.Find(awayReload,"burger").controlled,"Native save retains separate Home/Away ownership and cooldowns");
            var actual=Resources.Load<PurchasesConfiguration>(PurchasesConfiguration.ResourcePath);
            Check(actual&&actual.mode==GamePurchasesMode.EditorTest,"Project purchase asset is configured for explicit test review, with no native payment calls");
            try{CheckPendingOrderRecovery(config,Check);}
            catch(Exception error){Check(false,"Actual pending-order recovery fixture completed without exception: "+error.GetBaseException().Message);}
        }
        finally
        {
            SaveDataInLocal.EditorSaveOverride=previous;UnityEngine.Object.DestroyImmediate(config);
            rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
            rows.Add("Configuration, local fulfillment and native serialization only. Google/Apple checkout, refunds, server receipt validation and physical devices are not certified.");
            File.WriteAllLines("Artifacts/Recovery/purchases-checks.txt",rows);
        }
        if(rows.Any(r=>r.StartsWith("FAIL ")))throw new Exception("Purchase/commerce checks failed; see purchases-checks.txt");
    }

    static void CheckPendingOrderRecovery(PurchasesConfiguration configuration,Action<bool,string> check)
    {
        // Real Unity IAP model objects and the actual service callback are used.
        // Only store acknowledgement is observed locally; no store is created.
        var oldData=GameData.instance;var oldSaving=GameData.OnSavingData;
        var serviceSingleton=typeof(GamePurchasesService).GetField("instance",BindingFlags.NonPublic|BindingFlags.Static);
        var oldService=serviceSingleton.GetValue(null);
        var host=new GameObject("Isolated pending IAP recovery fixture");host.hideFlags=HideFlags.HideAndDontSave;host.SetActive(false);
        try
        {
            var data=host.AddComponent<GameData>();var service=host.AddComponent<GamePurchasesService>();
            GameData.instance=data;GameData.OnSavingData=null;
            typeof(GamePurchasesService).GetField("config",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(service,configuration);
            var cash=configuration.Find("firm_cash");
            var definition=new ProductDefinition(cash.key,"fixture.firm.cash",ProductType.Consumable);
            var constructor=typeof(Product).GetConstructor(BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(ProductDefinition),typeof(ProductMetadata),typeof(bool)},null);
            if(constructor==null)throw new InvalidOperationException("Installed IAP Product constructor changed; update the native recovery fixture.");
            var product=(Product)constructor.Invoke(new object[]{definition,new ProductMetadata("TEST ONLY","Support","Local callback fixture","GBP",0m),true});
            var orderInfoType=typeof(PendingOrder).Assembly.GetType("UnityEngine.Purchasing.OrderInfo",true);
            var orderInfo=(IOrderInfo)Activator.CreateInstance(orderInfoType,new object[]{"fixture-only-receipt","pending-recovery-1","LocalTest"});
            var pending=new PendingOrder(new Cart(new CartItem(product)),orderInfo);
            // Null confirmed orders specifically exercises pending recovery
            // before the confirmed-order guard. This was the regression.
            var orders=new Orders(null,new[]{pending},Array.Empty<DeferredOrder>());
            var fetched=typeof(GamePurchasesService).GetMethod("RestoreFetched",BindingFlags.NonPublic|BindingFlags.Instance);
            var original=new PlayerData{PlayerName="Pending Original",PurchaseWalletId="pending-original",Money=25,RecoveryVersion=RecoveryProgression.Version};original.RecruitedAgents.Add(new AgentData("Original"));
            var different=new PlayerData{PlayerName="Pending Different",PurchaseWalletId="pending-different",Money=75,RecoveryVersion=RecoveryProgression.Version};different.RecruitedAgents.Add(new AgentData("Different"));
            var intent=new PurchaseReceiptState();intent.intents.Add(new PurchaseIntent{product=cash.key,wallet=original.PurchaseWalletId});PurchaseReceiptLedger.Save(intent);
            int confirmations=0;bool savedBeforeConfirmation=true;bool exactOrder=true;
            string token=cash.key+":pending-recovery-1";
            service.EditorConfirmPurchaseOverride=order=>
            {
                confirmations++;exactOrder&=ReferenceEquals(order,pending);
                var saved=SaveDataInLocal.DataLoad();
                savedBeforeConfirmation&=saved!=null&&saved.PurchaseWalletId==original.PurchaseWalletId&&saved.Money==1025&&saved.FulfilledPurchaseTransactions.Contains(token)
                    &&PurchaseReceiptLedger.Load().delivered.Contains(token);
            };
            data.PlayerData=different;SaveDataInLocal.DataSave(different);fetched.Invoke(service,new object[]{orders});
            check(different.Money==75&&confirmations==0&&PurchaseReceiptLedger.Load().intents.Single().wallet==original.PurchaseWalletId,
                "Actual fetched pending order waits unconfirmed in the wrong firm and retains its original wallet intent");
            data.PlayerData=original;SaveDataInLocal.DataSave(original);fetched.Invoke(service,new object[]{orders});
            check(original.Money==1025&&confirmations==1&&PurchaseReceiptLedger.Load().intents.Count==0,
                "Returning to the original firm retries the same fetched pending order without an app restart");
            check(savedBeforeConfirmation&&exactOrder,
                "Pending purchase acknowledgement follows native currency/token save and acknowledges the exact SDK order");
            fetched.Invoke(service,new object[]{orders});
            check(original.Money==1025&&confirmations==2&&original.FulfilledPurchaseTransactions.Count(t=>t==token)==1,
                "Repeated fetched pending orders can retry acknowledgement without delivering currency twice");
            data.PlayerData=SaveDataInLocal.DataLoad();fetched.Invoke(service,new object[]{orders});
            check(data.PlayerData.Money==1025&&confirmations==3&&savedBeforeConfirmation,
                "Pending-order replay after native wallet reload remains fulfilled once and acknowledgement-ready");
            GameData.instance=null;fetched.Invoke(service,new object[]{orders});
            check(confirmations==3,
                "Fetched pending order remains unconfirmed when its campaign service is unavailable");
        }
        finally
        {
            GameData.instance=oldData;GameData.OnSavingData=oldSaving;serviceSingleton.SetValue(null,oldService);UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
#endif
