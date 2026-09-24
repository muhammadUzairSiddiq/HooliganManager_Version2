using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Interactive street actions layered over the campaign: conversations, civilian
/// shakedowns, protection income, taxi fast travel and destructive sabotage.
/// Every action has a world presentation, a consequence and campaign progress.
/// </summary>
public sealed class CityActionSystem : MonoBehaviour
{
    public static CityActionSystem Instance { get; private set; }

    CityGameplay city;
    GameObject taxi;
    TextMeshPro taxiLabel;
    RectTransform taxiHudFrame, taxiPin;
    TextMeshProUGUI taxiPinLabel;
    float taxiNextScan;
    int taxiNearby;
    bool taxiBoarded, taxiTravelling;
    readonly List<TaxiPassenger> taxiPassengers = new List<TaxiPassenger>();
    readonly List<CitySabotageTarget> sabotageTargets = new List<CitySabotageTarget>();
    bool actionBusy;
    const int TaxiCapacity = 5;
    const float TaxiBoardRange = 9f;
    GameObject taxiDestPanel;
    RectTransform taxiDestList, taxiExitPin;
    TextMeshProUGUI taxiDestBrief;
    public bool TaxiReady=>taxi;
    public Transform TaxiTransform=>taxi?taxi.transform:null;
    public static bool TaxiSessionActive=>Instance&&Instance.taxiBoarded&&!Instance.taxiTravelling;
    public int ActiveSabotageTargets=>sabotageTargets.Count(t=>t&&!t.Complete);

    struct TaxiPassenger
    {
        public AgentController Agent;
        public Renderer[] Renderers;
        public Collider[] Colliders;
        public NavMeshAgent Nav;
    }

    public static CityActionSystem Ensure(CityGameplay owner)
    {
        if(Instance)return Instance;
        var root=new GameObject("City Interactive Actions");
        var system=root.AddComponent<CityActionSystem>();
        system.city=owner;
        return system;
    }

    void Awake(){Instance=this;}
    void OnDestroy()
    {
        if(Instance==this)Instance=null;
        if(taxiPin)Destroy(taxiPin.gameObject);
        if(taxiExitPin)Destroy(taxiExitPin.gameObject);
        if(taxiDestPanel)Destroy(taxiDestPanel);
    }

    IEnumerator Start()
    {
        while(city==null||city.Locations==null||city.Locations.Length<9)yield return null;
        BuildTaxi();
        BuildSabotageTargets();
        LandscapeBattleHUD hud=null;
        while(!(hud=FindFirstObjectByType<LandscapeBattleHUD>())||!hud.frame)yield return null;
        BuildTaxiHud(hud.frame);
    }

    public void OpenPedestrianActions(SocialNpc npc)
    {
        if(!npc||actionBusy){Feedback(actionBusy?"FINISH THE CURRENT STREET ACTION FIRST":"NO CIVILIAN AVAILABLE");return;}
        var crew=SelectedCrew();
        if(crew.Length==0){Feedback("SELECT A CREW MEMBER FIRST");return;}
        npc.PauseForConversation(true,crew[0].transform.position);
        GamePopup.Instance.Show(npc.DisplayName.ToUpperInvariant(),
            "This civilian has stopped and is facing your crew.\nChoose how to handle them.",
            ()=>npc.PauseForConversation(false,crew[0].transform.position),
            new GamePopup.Option("TALK",new Color(.12f,.62f,.88f),()=>npc.Talk()),
            new GamePopup.Option("ASK FOR MONEY",new Color(.80f,.58f,.12f),()=>AskForMoney(npc,crew)),
            new GamePopup.Option("ROB BY FORCE",LandscapeUI.Red,()=>StartCoroutine(StreetFight(npc,crew,false))),
            new GamePopup.Option("ASK TO JOIN",LandscapeUI.Green,()=>AskToJoin(npc,crew)),
            new GamePopup.Option("KILL",new Color(.55f,.08f,.10f),()=>StartCoroutine(StreetFight(npc,crew,true))));
    }

    void AskForMoney(SocialNpc npc,AgentController[] crew)
    {
        if(!npc||crew==null||crew.Length==0)return;
        npc.PauseForConversation(true,crew[0].transform.position);
        bool given=Random.value<0.40f;
        int cash=given?Random.Range(25,71):0;
        var d=GameManager.Data;
        if(given&&d!=null)
        {
            d.Money+=cash;
            GameManager.Save();
            AgentSelectionManager.CreateCommandMarker(npc.transform.position,LandscapeUI.Gold,"+£"+cash.ToString("N0"));
        }
        Feedback(given?npc.DisplayName+" HANDED OVER £"+cash.ToString("N0")+" · NO HEAT":npc.DisplayName+" REFUSED TO GIVE ANYTHING");
        GamePopup.Instance.Show(given?"THEY GAVE YOU CASH":"THEY REFUSED",
            given?$"{npc.DisplayName} handed over £{cash:N0}.\nNo force was used and police heat did not change."
                 :$"{npc.DisplayName} refused. They walk away unharmed.\nNo police heat.",
            new GamePopup.Option("CONTINUE",LandscapeUI.Green,()=>npc.PauseForConversation(false,crew[0].transform.position)));
    }

    void AskToJoin(SocialNpc npc,AgentController[] crew)
    {
        if(!npc)return;
        npc.PauseForConversation(true,crew[0].transform.position);
        float roll=Random.value;
        bool joined=npc.TryInvite(roll,out string response);
        GamePopup.Instance.Show(joined?"CREW INVITE ACCEPTED":"CREW INVITE REFUSED",response,
            new GamePopup.Option("CONTINUE",LandscapeUI.Green,()=>
            {
                if(joined)npc.FinishJoinedConversation();
                else npc.PauseForConversation(false,crew[0].transform.position);
            }));
    }

    const float StreetMeleeRange=2.15f;

    IEnumerator StreetFight(SocialNpc npc,AgentController[] crew,bool lethal)
    {
        if(!npc||crew==null||crew.Length==0)yield break;
        actionBusy=true;
        npc.PauseForConversation(true,crew[0].transform.position);
        var pedestrian=npc.GetComponent<PedestrianController>();
        Feedback((lethal?"KILL":"ROB")+" · CLOSING IN");
        CameraPanTouchOnly.Instance?.FocusOn(npc.transform.position);
        AgentSelectionManager.CreateCommandMarker(npc.transform.position,LandscapeUI.Red,lethal?"KILL":"ROB");

        yield return CloseInOn(crew,npc,StreetMeleeRange);
        if(!npc)
        {
            foreach(var member in crew)if(member)member.SetActivityLocked(false,member.transform.position);
            actionBusy=false;yield break;
        }

        bool inReach=crew.Any(a=>a&&a.IsAlive&&Horizontal(a.transform.position,npc.transform.position)<=StreetMeleeRange+1.6f);
        if(!inReach)
        {
            Feedback("COULD NOT GET CLOSE ENOUGH");
            npc.PauseForConversation(false,crew[0].transform.position);
            actionBusy=false;yield break;
        }

        foreach(var member in crew)if(member)member.SetActivityLocked(true,npc.transform.position);
        pedestrian?.Face(crew[0].transform.position);
        pedestrian?.BeginStreetFight();
        yield return new WaitForSeconds(.12f);

        if(lethal)
        {
            if(crew[0])crew[0].PlayStreetPunch();
            yield return new WaitForSeconds(.42f);
            if(npc)
            {
                SpawnImpact(npc.transform.position+Vector3.up*1.2f,new Color(1f,.18f,.08f));
                GameAudio.Play("impact");
            }
            yield return new WaitForSeconds(.18f);
            pedestrian?.PlayDie();
            var killData=GameManager.Data;
            if(killData!=null){killData.PoliceHeat=Mathf.Clamp(killData.PoliceHeat+4,0,10);GameManager.Save();}
            foreach(var member in crew)if(member)member.SetActivityLocked(false,npc?npc.transform.position:member.transform.position);
            Feedback(npc?npc.DisplayName+" IS DOWN · POLICE HEAT UP":"TARGET IS DOWN");
            yield return new WaitForSeconds(1.7f);
            if(npc)Destroy(npc.gameObject);
            city?.CheckCampaignCompletion();
            actionBusy=false;
            yield break;
        }

        float npcHp=24f;
        float elapsed=0f;
        int swing=-1;
        while(npc&&npcHp>0f&&elapsed<7.5f)
        {
            elapsed+=Time.deltaTime;
            Vector3 face=crew[0]?crew[0].transform.position:npc.transform.position;
            pedestrian?.Face(face);
            int next=Mathf.FloorToInt(elapsed/.7f);
            if(next!=swing)
            {
                swing=next;
                if(crew[0])crew[0].PlayStreetPunch();
                pedestrian?.PlayAttack();
                npcHp-=Random.Range(7f,13f);
                if(swing%2==1&&crew[0])crew[0].TakeDamage(Random.Range(3f,7f));
                SpawnImpact(npc.transform.position+Vector3.up*1.2f,new Color(1f,.35f,.12f));
                GameAudio.Play("impact");
            }
            yield return null;
        }
        foreach(var member in crew)if(member)member.SetActivityLocked(false,npc?npc.transform.position:member.transform.position);
        var d=GameManager.Data;
        if(d!=null)
        {
            d.PoliceHeat=Mathf.Clamp(d.PoliceHeat+2,0,10);
            int cash=Random.Range(90,180);d.Money+=cash;CampaignMissions.RecordAction(d,"extort");
            GameManager.Save();
        }
        if(npcHp<=0f)
        {
            pedestrian?.PlayDie();
            Feedback(npc.DisplayName+" DROPPED THE CASH AND FELL");
            yield return new WaitForSeconds(1.7f);
            if(npc)Destroy(npc.gameObject);
        }
        else
        {
            pedestrian?.EndStreetFight();
            npc.PauseForConversation(false,crew[0].transform.position);
        }
        city?.CheckCampaignCompletion();
        actionBusy=false;
    }

    IEnumerator CloseInOn(AgentController[] crew,SocialNpc npc,float range)
    {
        if(!npc||crew==null||crew.Length==0)yield break;
        Vector3 target=npc.transform.position;
        if(crew.Any(a=>a&&a.IsAlive&&Horizontal(a.transform.position,target)<=range))yield break;

        for(int i=0;i<crew.Length;i++)
        {
            var member=crew[i];
            if(!member||!member.IsAlive)continue;
            Vector3 from=member.transform.position;
            Vector3 dir=target-from;dir.y=0f;
            Vector3 point=dir.sqrMagnitude<.01f?target:target-dir.normalized*1.55f;
            float angle=i*.85f;
            point+=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*.4f;
            member.CommandMoveTo(point);
        }

        float timeout=14f;
        while(timeout>0f&&npc)
        {
            timeout-=Time.deltaTime;
            target=npc.transform.position;
            var pedestrian=npc.GetComponent<PedestrianController>();
            if(crew[0])pedestrian?.Face(crew[0].transform.position);
            if(crew.Any(a=>a&&a.IsAlive&&Horizontal(a.transform.position,target)<=range))yield break;
            yield return null;
        }
    }

    public void TalkNearest()
    {
        if(actionBusy)return;
        var crew=SelectedCrew();if(crew.Length==0){Feedback("SELECT A CREW MEMBER FIRST");return;}
        var npc=FindObjectsByType<SocialNpc>(FindObjectsSortMode.None)
            .Where(n=>n&&!n.JoinedCrew)
            .OrderBy(n=>Horizontal(n.transform.position,crew[0].transform.position)).FirstOrDefault();
        if(!npc){Feedback("NO CIVILIAN AVAILABLE TO TALK");return;}
        StartCoroutine(Approach(crew,npc.transform.position,7f,()=>npc.Talk(),"TALK TO "+npc.DisplayName));
    }

    public void OpenActions()
    {
        if(actionBusy){Feedback("FINISH THE CURRENT STREET ACTION FIRST");return;}
        var d=GameManager.Data;
        int incomeReady=FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None).Count(t=>t&&t.CanCollectIncome);
        int sabotageReady=sabotageTargets.Count(t=>t&&!t.Complete);
        GamePopup.Instance.Show("STREET ACTIONS",
            "Choose a meaningful action. Cash actions raise risk; transport saves stamina; sabotage changes the mission map.\n\n"+
            $"Income ready: {incomeReady}   ·   Rival vehicles: {sabotageReady}   ·   Police heat: {d?.PoliceHeat??0}/10",
            new GamePopup.Option("SHAKE DOWN",new Color(.75f,.36f,.12f),ShakeDownNearest),
            new GamePopup.Option("COLLECT INCOME",LandscapeUI.Green,CollectNearestIncome),
            new GamePopup.Option("USE TAXI",new Color(.10f,.68f,.88f),UseTaxi),
            new GamePopup.Option("SABOTAGE VEHICLE",LandscapeUI.Red,SabotageNearest),
            new GamePopup.Option("CLOSE",LandscapeUI.PanelColor,null));
    }

    public void FocusNearestTaxi()
    {
        if(!taxi){Feedback("TAXI IS NOT READY");return;}
        CameraPanTouchOnly.Instance?.FocusOn(taxi.transform.position);
        AgentSelectionManager.CreateCommandMarker(taxi.transform.position,LandscapeUI.Green,"TAXI");
    }

    void ShakeDownNearest()
    {
        var crew=SelectedCrew();if(crew.Length==0){Feedback("SELECT A CREW MEMBER FIRST");return;}
        var npc=FindObjectsByType<SocialNpc>(FindObjectsSortMode.None)
            .Where(n=>n&&!n.JoinedCrew&&!n.IsConversationOpenSafe())
            .OrderBy(n=>Horizontal(n.transform.position,crew[0].transform.position)).FirstOrDefault();
        if(!npc){Feedback("NO CIVILIAN AVAILABLE");return;}
        StartCoroutine(Approach(crew,npc.transform.position,6.5f,()=>ShowShakedown(npc,crew),"SHAKE DOWN "+npc.DisplayName));
    }

    void ShowShakedown(SocialNpc npc,AgentController[] crew)
    {
        if(!npc)return;
        npc.PauseForConversation(true,crew[0].transform.position);
        GamePopup.Instance.Show("STREET SHAKEDOWN",
            $"{npc.DisplayName} has stopped. Demand money or rob them by force.\n\nBoth choices add +2 police heat. If the civilian resists, your members take damage.",
            new GamePopup.Option("DEMAND CASH",new Color(.80f,.58f,.12f),()=>StartCoroutine(ResolveShakedown(npc,crew,false))),
            new GamePopup.Option("ROB BY FORCE",LandscapeUI.Red,()=>StartCoroutine(ResolveShakedown(npc,crew,true))),
            new GamePopup.Option("LET THEM GO",LandscapeUI.PanelColor,()=>npc.PauseForConversation(false,crew[0].transform.position)));
    }

    IEnumerator ResolveShakedown(SocialNpc npc,AgentController[] crew,bool force)
    {
        actionBusy=true;
        var d=GameManager.Data;if(d==null||!npc){actionBusy=false;yield break;}
        d.PoliceHeat=Mathf.Clamp(d.PoliceHeat+2,0,10);
        float combinedStrength=crew.Where(a=>a&&a.Data!=null).Sum(a=>a.Data.Strength);
        bool resists=force||Random.value>Mathf.Clamp01(.42f+combinedStrength/240f);
        int cash=force?Random.Range(130,221):Random.Range(55,121);
        if(resists)
        {
            Feedback(npc.DisplayName+" FIGHTS BACK · POLICE HEAT +2");
            foreach(var member in crew)if(member)member.SetActivityLocked(true,npc.transform.position);
            float elapsed=0f;
            while(elapsed<3.2f&&npc)
            {
                elapsed+=Time.deltaTime;
                if(Mathf.FloorToInt(elapsed*3f)!=Mathf.FloorToInt((elapsed-Time.deltaTime)*3f))
                    SpawnImpact(npc.transform.position+Vector3.up*1.2f,new Color(1f,.42f,.12f));
                yield return null;
            }
            foreach(var member in crew)
            {
                if(!member)continue;
                member.TakeDamage(Random.Range(4f,10f));
                member.SetActivityLocked(false,npc?npc.transform.position:member.transform.position);
            }
            if(npc)Destroy(npc.gameObject);
        }
        else
        {
            Feedback(npc.DisplayName+" HANDS OVER £"+cash.ToString("N0")+" · POLICE HEAT +2");
            npc.PauseForConversation(false,crew[0].transform.position);
        }
        d.Money+=cash;
        CampaignMissions.RecordAction(d,"extort");
        BattleManager.instance?.PersistBattleProgress();GameManager.Save();
        AgentSelectionManager.CreateCommandMarker(crew[0].transform.position,LandscapeUI.Gold,"+£"+cash.ToString("N0"));
        GamePopup.Instance.Show(resists?"CIVILIAN DEFEATED":"CASH COLLECTED",
            $"Street cash: +£{cash:N0}\nPolice heat: +2\n"+(resists?"The civilian fought back and your active members took damage.":"The civilian paid without a fight."),
            new GamePopup.Option("CONTINUE",LandscapeUI.Green,null));
        city.CheckCampaignCompletion();
        actionBusy=false;
    }

    void CollectNearestIncome()
    {
        var crew=SelectedCrew();if(crew.Length==0){Feedback("SELECT A CREW MEMBER FIRST");return;}
        var target=FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None)
            .Where(t=>t&&t.CanCollectIncome)
            .OrderBy(t=>Horizontal(t.transform.position,crew[0].transform.position)).FirstOrDefault();
        if(!target){Feedback("NO SECURED AREA HAS INCOME READY");return;}
        StartCoroutine(Approach(crew,target.transform.position,7.5f,()=>target.CollectIncome(),"COLLECT INCOME · "+target.zoneName));
    }

    void UseTaxi()
    {
        if(!taxi){Feedback("TAXI IS NOT READY");return;}
        var crew=SelectedCrew();if(crew.Length==0){Feedback("SELECT A CREW MEMBER FIRST");return;}
        StartCoroutine(Approach(crew,taxi.transform.position,TaxiBoardRange,BoardAndOpenDestinations,"BOARD TAXI"));
    }

    void BoardAndOpenDestinations()
    {
        if(taxiTravelling||actionBusy)return;
        if(!taxiBoarded)
        {
            var crew=CrewInTaxiRange().Take(TaxiCapacity).ToList();
            if(crew.Count==0){Feedback("MOVE YOUR CREW INTO THE TAXI CIRCLE");return;}
            BoardTaxi(crew);
        }
        ShowTaxiDestinations();
    }

    void ShowTaxiDestinations()
    {
        if(!taxi||city==null||!taxiBoarded||!taxiDestPanel||!taxiDestList)return;
        LandscapeUI.Clear(taxiDestList);
        int boarded=taxiPassengers.Count(p=>p.Agent);
        if(taxiDestBrief)taxiDestBrief.text=$"{boarded} of {TaxiCapacity} boarded. Pick a district — farther trips cost more. EXIT leaves without travelling.";
        var ordered=Enumerable.Range(0,city.LocationNames.Length)
            .Where(i=>Horizontal(city.Locations[i],taxi.transform.position)>=12f)
            .OrderBy(i=>Horizontal(city.Locations[i],taxi.transform.position));
        foreach(int index in ordered)
        {
            int location=index;
            float distance=Horizontal(city.Locations[index],taxi.transform.position);
            int fare=TaxiFareFor(distance);
            var row=LandscapeUI.Button("TaxiDest"+index,taxiDestList,$"{city.LocationNames[index]}    £{fare}",0,0,448,48,index==0?"green":"dark");
            LandscapeUI.LayoutSize(row.gameObject,448,48);
            row.onClick.AddListener(()=>StartCoroutine(TryTaxiTravel(location,fare)));
        }
        CityMissionHUD.Style(taxiDestPanel.transform);
        taxiDestPanel.transform.SetAsLastSibling();
        taxiDestPanel.SetActive(true);
        GameAudio.Play("popup");
    }

    IEnumerator TryTaxiTravel(int location,int fare)
    {
        var d=GameManager.Data;
        if(d==null||d.Money<fare)
        {
            GamePopup.Instance.ShowTimed("NOT ENOUGH FUNDS","You don't have enough cash for this fare.",1.7f);
            while(GamePopup.AnyOpen)yield return null;
            if(taxiBoarded&&!taxiTravelling)ShowTaxiDestinations();
            yield break;
        }
        if(taxiDestPanel)taxiDestPanel.SetActive(false);
        yield return TaxiTravel(location,fare);
    }

    IEnumerator TaxiTravel(AgentController[] crew,int location)
    {
        if(!taxiBoarded&&crew!=null&&crew.Length>0)BoardTaxi(crew.Where(a=>a&&a.IsAlive).Take(TaxiCapacity).ToList());
        yield return TaxiTravel(location,TaxiFareFor(Horizontal(city.Locations[location],taxi.transform.position)));
    }

    IEnumerator TaxiTravel(int location,int fare)
    {
        if(!taxi||city==null||location<0||location>=city.Locations.Length)yield break;
        var d=GameManager.Data;
        if(d==null||d.Money<fare)
        {
            GamePopup.Instance.ShowTimed("NOT ENOUGH FUNDS","You don't have enough cash for this fare.",1.7f);
            yield break;
        }
        actionBusy=true;
        taxiTravelling=true;
        if(taxiDestPanel)taxiDestPanel.SetActive(false);
        d.Money-=fare;
        var fade=BuildFade();
        yield return Fade(fade,0f,1f,.28f);
        yield return new WaitForSecondsRealtime(1f);
        Vector3 destination=city.Locations[location];
        taxi.transform.position=destination+new Vector3(5f,0,4f);
        SnapTaxiToGround();
        DropPassengers(destination);
        CameraPanTouchOnly.Instance?.FocusOn(destination);
        CampaignMissions.RecordAction(d,"taxi");GameManager.Save();
        yield return Fade(fade,1f,0f,.35f);
        Destroy(fade.gameObject);
        Feedback($"TAXI ARRIVAL · {city.LocationNames[location]} · -£{fare}");
        city.CheckCampaignCompletion();
        taxiTravelling=false;
        actionBusy=false;
    }

    void SabotageNearest()
    {
        var crew=SelectedCrew();if(crew.Length==0){Feedback("SELECT A CREW MEMBER FIRST");return;}
        var target=sabotageTargets.Where(t=>t&&!t.Complete)
            .OrderBy(t=>Horizontal(t.transform.position,crew[0].transform.position)).FirstOrDefault();
        if(!target){Feedback("ALL RIVAL VEHICLES DESTROYED");return;}
        StartCoroutine(Approach(crew,target.transform.position,8f,()=>StartCoroutine(PlantCharge(target,crew)),"PLANT CHARGE · "+target.DisplayName));
    }

    IEnumerator PlantCharge(CitySabotageTarget target,AgentController[] crew)
    {
        if(!target||target.Complete)yield break;
        actionBusy=true;
        foreach(var member in crew)if(member)member.SetActivityLocked(true,target.transform.position);
        target.SetStatus("PLANTING CHARGE · 0%");
        float elapsed=0f;
        while(elapsed<4f&&target)
        {
            elapsed+=Time.deltaTime;
            target.SetStatus($"PLANTING CHARGE · {Mathf.Min(100,Mathf.RoundToInt(elapsed/4f*100f))}%");
            yield return null;
        }
        if(!target){actionBusy=false;yield break;}
        foreach(var member in crew)if(member)member.SetActivityLocked(false,target.transform.position);
        target.Complete=true;
        SpawnExplosion(target.transform.position);
        foreach(var renderer in target.GetComponentsInChildren<Renderer>())renderer.enabled=false;
        var d=GameManager.Data;int loot=250+Mathf.Clamp(d?.CurrentLevel??1,1,5)*75;
        if(d!=null)
        {
            d.Money+=loot;d.PoliceHeat=Mathf.Clamp(d.PoliceHeat+3,0,10);
            CampaignMissions.RecordAction(d,"sabotage",target.TargetId);GameManager.Save();
        }
        Feedback($"RIVAL VEHICLE DESTROYED · SALVAGE +£{loot:N0} · HEAT +3");
        yield return new WaitForSeconds(.65f);
        GamePopup.Instance.Show("SABOTAGE COMPLETE",$"The rival supply vehicle is destroyed.\n\nSalvaged cargo: +£{loot:N0}\nPolice heat: +3\nThis vehicle is permanently cleared for the active mission.",new GamePopup.Option("MOVE",LandscapeUI.Green,null));
        city.CheckCampaignCompletion();actionBusy=false;
    }

    IEnumerator Approach(AgentController[] crew,Vector3 target,float range,System.Action arrived,string label)
    {
        actionBusy=true;
        AgentSelectionManager.instance?.CommandSelectedMoveTo(target);
        AgentSelectionManager.CreateCommandMarker(target,LandscapeUI.Green,label);
        CameraPanTouchOnly.Instance?.FocusOn(target);
        Feedback(label+" · CREW EN ROUTE");
        float timeout=35f;
        while(timeout>0f)
        {
            timeout-=Time.deltaTime;
            if(crew.Where(a=>a&&a.IsAlive).Any(a=>Horizontal(a.transform.position,target)<=range))break;
            yield return null;
        }
        actionBusy=false;
        if(timeout<=0f){Feedback("ROUTE BLOCKED · TRY A CLOSER APPROACH");yield break;}
        arrived?.Invoke();
    }

    AgentController[] SelectedCrew()
    {
        var selection=AgentSelectionManager.instance;
        return selection?.SelectedAgents.Where(a=>a&&a.IsAlive&&!a.IsActivityLocked).ToArray()??new AgentController[0];
    }

    void BuildTaxi()
    {
        var prefab=LandscapeTheme.Current?.taxiPrefab;
        taxi=prefab?Instantiate(prefab):new GameObject("Taxi Vehicle");
        taxi.name="MISSION TAXI";
        taxi.transform.position=CityGameplay.ReachableApproach(city.Home,city.Locations[0]+new Vector3(12,0,-8));
        taxi.transform.rotation=Quaternion.Euler(0,135,0);
        foreach(var behaviour in taxi.GetComponentsInChildren<MonoBehaviour>())
            if(behaviour&&behaviour.GetType().Name=="Car")behaviour.enabled=false;
        foreach(var body in taxi.GetComponentsInChildren<Rigidbody>()){body.isKinematic=true;body.useGravity=false;}
        SnapTaxiToGround();
        ZoneVolumeFactory.Create(taxi.transform,new Color(.12f,1f,.35f,1f),5.2f);
        taxiLabel=ZoneLabelUtil.Create(taxi.transform,"▼  TAXI FAST TRAVEL\n<size=68%>WALK INTO CIRCLE · ENTER · 5 MAX</size>",6.4f,8.2f);
        MiniMapIconFactory.Register(taxi.transform,MiniMapIconFactory.Kind.Turf,"TAXI");
    }

    void BuildTaxiHud(RectTransform frame)
    {
        taxiHudFrame=frame;
        var button=LandscapeUI.Button("TaxiEnter",frame,"ENTER  ·  5 MAX",0,0,185,43,"green");
        button.onClick.AddListener(BoardAndOpenDestinations);
        taxiPin=button.transform as RectTransform;
        taxiPin.SetAsFirstSibling();
        taxiPinLabel=button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if(taxiPinLabel){taxiPinLabel.color=Color.white;taxiPinLabel.fontSize=20f;taxiPinLabel.fontSizeMin=16f;taxiPinLabel.fontSizeMax=22f;taxiPinLabel.overflowMode=TextOverflowModes.Overflow;}
        CityMissionHUD.Style(taxiPin);
        taxiPin.gameObject.SetActive(false);

        var exit=LandscapeUI.Button("TaxiExit",frame,"EXIT TAXI",0,0,168,40,"red");
        exit.onClick.AddListener(ExitTaxi);
        taxiExitPin=exit.transform as RectTransform;
        taxiExitPin.SetAsFirstSibling();
        CityMissionHUD.Style(taxiExitPin);
        taxiExitPin.gameObject.SetActive(false);

        taxiDestPanel=LandscapeUI.Panel("TaxiDestinations",frame,540,70,520,760,true).gameObject;
        LandscapeUI.Text("Title",taxiDestPanel.transform,"TAXI FAST TRAVEL",22,14,476,36,24,LandscapeUI.Gold,true);
        taxiDestBrief=LandscapeUI.Text("Brief",taxiDestPanel.transform,"Choose a district. Farther trips cost more.",22,52,476,58,15,LandscapeUI.Muted);
        taxiDestList=LandscapeUI.Scroll("Destinations",taxiDestPanel.transform,16,118,488,530).content;
        LandscapeUI.Button("Exit",taxiDestPanel.transform,"EXIT",22,668,476,70,"red").onClick.AddListener(ExitTaxi);
        CityMissionHUD.Style(taxiDestPanel.transform);
        taxiDestPanel.SetActive(false);
    }

    void Update()
    {
        if(!taxi||!taxiHudFrame)return;
        if(taxiLabel&&Camera.main)taxiLabel.transform.rotation=Camera.main.transform.rotation;

        bool modal=GamePopup.AnyOpen||RecruitPackagePanel.AnyOpen||RecruitDialogBox.AnyOpen||Time.timeScale==0f||BattleManager.instance==null;
        if(taxiBoarded&&!taxiTravelling)
        {
            if(taxiPin&&taxiPin.gameObject.activeSelf)taxiPin.gameObject.SetActive(false);
            if(modal)
            {
                if(taxiDestPanel&&taxiDestPanel.activeSelf)taxiDestPanel.SetActive(false);
            }
            else if(taxiDestPanel&&!taxiDestPanel.activeSelf)
                ShowTaxiDestinations();
            PlaceTaxiPin(taxiExitPin,true,modal);
            return;
        }
        if(taxiDestPanel&&taxiDestPanel.activeSelf)taxiDestPanel.SetActive(false);
        if(taxiExitPin&&taxiExitPin.gameObject.activeSelf)taxiExitPin.gameObject.SetActive(false);

        if(!taxiPin)return;
        if(Time.unscaledTime>=taxiNextScan)
        {
            taxiNextScan=Time.unscaledTime+.2f;
            taxiNearby=taxiTravelling?0:CrewInTaxiRange().Count;
            if(taxiPinLabel)taxiPinLabel.text=taxiNearby>0?$"ENTER  ·  {Mathf.Min(taxiNearby,TaxiCapacity)}/{TaxiCapacity}":"ENTER  ·  5 MAX";
        }

        bool blocked=taxiTravelling||actionBusy||modal;
        PlaceTaxiPin(taxiPin,!blocked&&taxiNearby>0,false);
    }

    void PlaceTaxiPin(RectTransform pin,bool allowed,bool hide)
    {
        if(!pin)return;
        if(hide||!allowed||!Camera.main){if(pin.gameObject.activeSelf)pin.gameObject.SetActive(false);return;}
        Vector3 projected=Camera.main.WorldToScreenPoint(taxi.transform.position+Vector3.up*4.6f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(taxiHudFrame,projected,null,out var local);
        float x=local.x-taxiHudFrame.rect.xMin,y=taxiHudFrame.rect.yMax-local.y;
        bool visible=projected.z>0&&x>220&&x<taxiHudFrame.rect.width-180&&y>90&&y<taxiHudFrame.rect.height-70;
        if(pin.gameObject.activeSelf!=visible)pin.gameObject.SetActive(visible);
        if(visible)LandscapeUI.Place(pin,x-84,y-21,168,40);
    }

    List<AgentController> CrewInTaxiRange()
    {
        var result=new List<AgentController>();
        var bm=BattleManager.instance;
        if(bm==null||!taxi)return result;
        float r2=TaxiBoardRange*TaxiBoardRange;
        var selected=AgentSelectionManager.instance?.SelectedAgents;
        foreach(var a in bm.PlayerAgents)
        {
            if(!a||!a.IsAlive||a.IsActivityLocked)continue;
            Vector3 d=a.transform.position-taxi.transform.position;d.y=0f;
            if(d.sqrMagnitude<=r2)result.Add(a);
        }
        return result
            .OrderByDescending(a=>selected!=null&&selected.Contains(a))
            .ThenBy(a=>Horizontal(a.transform.position,taxi.transform.position))
            .ToList();
    }

    static int TaxiFareFor(float distance)=>Mathf.Clamp(Mathf.RoundToInt(distance/3.2f/5f)*5,25,220);

    void SnapTaxiToGround()
    {
        if(!taxi)return;
        Vector3 p=taxi.transform.position;
        if(Physics.Raycast(p+Vector3.up*6f,Vector3.down,out var hit,30f,~0,QueryTriggerInteraction.Ignore))
            taxi.transform.position=new Vector3(p.x,hit.point.y+.02f,p.z);
    }

    void BoardTaxi(List<AgentController> crew)
    {
        taxiPassengers.Clear();
        foreach(var member in crew)
        {
            if(!member)continue;
            member.SetActivityLocked(true,taxi.transform.position);
            var p=new TaxiPassenger
            {
                Agent=member,
                Renderers=member.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray(),
                Colliders=member.GetComponentsInChildren<Collider>().Where(c=>c.enabled).ToArray(),
                Nav=member.GetComponent<NavMeshAgent>()
            };
            if(p.Nav)p.Nav.enabled=false;
            foreach(var r in p.Renderers)r.enabled=false;
            foreach(var c in p.Colliders)c.enabled=false;
            member.transform.position=taxi.transform.position+Vector3.up*.4f;
            taxiPassengers.Add(p);
        }
        taxiBoarded=taxiPassengers.Count>0;
        if(taxiPin)taxiPin.gameObject.SetActive(false);
        Feedback($"{taxiPassengers.Count} MEMBER{(taxiPassengers.Count==1?"":"S")} ENTERED THE TAXI · PICK A DISTRICT");
        GameAudio.Play("popup");
    }

    void ExitTaxi()
    {
        if(taxiTravelling)return;
        if(taxiDestPanel)taxiDestPanel.SetActive(false);
        DropPassengers(taxi?taxi.transform.position:Vector3.zero);
        Feedback("CREW LEFT THE TAXI");
    }

    void DropPassengers(Vector3 around)
    {
        for(int i=0;i<taxiPassengers.Count;i++)
        {
            var p=taxiPassengers[i];
            if(!p.Agent)continue;
            Vector3 offset=new Vector3(Mathf.Cos(i*1.26f)*2.4f,0f,Mathf.Sin(i*1.26f)*2.4f);
            Vector3 drop=around+offset;
            if(NavMesh.SamplePosition(drop,out var hit,8f,NavMesh.AllAreas))drop=hit.position;
            else if(NavMesh.SamplePosition(around,out hit,12f,NavMesh.AllAreas))drop=hit.position;
            foreach(var c in p.Colliders)if(c)c.enabled=true;
            foreach(var r in p.Renderers)if(r)r.enabled=true;
            if(p.Nav){p.Nav.enabled=true;if(p.Nav.isOnNavMesh)p.Nav.Warp(drop);else p.Agent.transform.position=drop;}
            else p.Agent.transform.position=drop;
            p.Agent.SetActivityLocked(false,around);
        }
        var selection=AgentSelectionManager.instance;
        if(selection)
        {
            selection.DeselectAll();
            foreach(var p in taxiPassengers)if(p.Agent&&p.Agent.IsAlive)selection.Select(p.Agent);
        }
        taxiPassengers.Clear();
        taxiBoarded=false;
        if(taxiDestPanel)taxiDestPanel.SetActive(false);
        if(taxiExitPin)taxiExitPin.gameObject.SetActive(false);
        BattleManager.instance?.PersistBattleProgress();
        GameManager.Save();
    }

    void BuildSabotageTargets()
    {
        int level=GameManager.Data?.CurrentLevel??1;
        int count=Mathf.Clamp((level+1)/2,1,3);
        int[] locations={8,6,3};
        for(int i=0;i<count;i++)
        {
            string id="vehicle-"+(i+1);
            if(CampaignMissions.ActionCount(GameManager.Data,GameManager.Data?.CurrentLevel??1,"sabotage")>i)continue;
            Vector3 pos=CityGameplay.ReachableApproach(city.Home,city.Locations[locations[i]]+new Vector3(7+i*2,0,-6));
            var prefab=LandscapeTheme.Current?.rivalVehiclePrefab;
            var go=prefab?Instantiate(prefab,pos,Quaternion.Euler(0,35+i*70,0)):new GameObject("Rival Supply Vehicle");
            go.name="RIVAL SUPPLY VEHICLE "+(i+1);
            go.transform.position=pos;
            foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>())if(behaviour&&behaviour.GetType().Name=="Car")behaviour.enabled=false;
            foreach(var body in go.GetComponentsInChildren<Rigidbody>()){body.isKinematic=true;body.useGravity=false;}
            var target=go.AddComponent<CitySabotageTarget>();target.Setup(id,"RIVAL SUPPLY CAR "+(i+1));
            sabotageTargets.Add(target);
        }
    }

    static CanvasGroup BuildFade()
    {
        var root=new GameObject("TaxiFade",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(CanvasGroup));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=35000;
        LandscapeUI.ConfigureLandscapeScaler(root.GetComponent<UnityEngine.UI.CanvasScaler>());
        var image=LandscapeUI.Image("Blackout",root.transform,0,0,1600,900,null,Color.black);image.raycastTarget=true;
        var title=LandscapeUI.Text("Travel",root.transform,"TAXI TRANSFER",0,410,1600,80,38,Color.white,true,TextAlignmentOptions.Center);
        var group=root.GetComponent<CanvasGroup>();group.alpha=0f;return group;
    }

    static IEnumerator Fade(CanvasGroup group,float from,float to,float seconds)
    {
        float elapsed=0f;while(elapsed<seconds){elapsed+=Time.unscaledDeltaTime;group.alpha=Mathf.Lerp(from,to,elapsed/seconds);yield return null;}group.alpha=to;
    }

    static void SpawnImpact(Vector3 position,Color color)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="StreetFightImpact";go.transform.position=position;go.transform.localScale=Vector3.one*.18f;
        var col=go.GetComponent<Collider>();if(col)Destroy(col);
        var renderer=go.GetComponent<Renderer>();var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");
        if(shader)renderer.material=new Material(shader){color=color};Destroy(go,.22f);
    }

    static void SpawnExplosion(Vector3 position)
    {
        var root=new GameObject("Sabotage Explosion");root.transform.position=position+Vector3.up*1.2f;
        var ps=root.AddComponent<ParticleSystem>();
        var main=ps.main;main.duration=.9f;main.startLifetime=new ParticleSystem.MinMaxCurve(.45f,1.1f);main.startSpeed=new ParticleSystem.MinMaxCurve(8f,17f);main.startSize=new ParticleSystem.MinMaxCurve(.35f,1.2f);main.startColor=new ParticleSystem.MinMaxGradient(new Color(1f,.85f,.18f),new Color(1f,.12f,.02f));main.gravityModifier=.8f;main.maxParticles=90;
        var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0f,70)});
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=1.2f;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Particles/Standard Unlit");if(shader)renderer.material=new Material(shader);
        var light=root.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1f,.28f,.04f);light.intensity=8f;light.range=22f;
        ps.Play();Destroy(root,2.2f);GameAudio.Play("hit");
    }

    static float Horizontal(Vector3 a,Vector3 b){a.y=0;b.y=0;return Vector3.Distance(a,b);}
    static void Feedback(string message){CityGameplay.Instance?.PostEvent(message);BattleUIController.instance?.ShowAlert(message,2.2f);}

#if UNITY_EDITOR
    public void BeginAutomatedPlaytest(string reportPath)=>StartCoroutine(AutomatedPlaytest(reportPath));
    IEnumerator AutomatedPlaytest(string reportPath)
    {
        var rows=new List<string>{"RUNNING Milestone 4 interactive city playtest"};
        void Check(bool pass,string label)=>rows.Add((pass?"PASS ":"FAIL ")+label);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)??"Artifacts/CityQA");File.WriteAllLines(reportPath,rows);
        var d=GameManager.Data;var crew=SelectedCrew();
        Check(d!=null&&d.PoliceHeat>=7,"Existing campaign migrates to at least seven police heat bars");
        Check(taxi&&taxi.GetComponentsInChildren<Renderer>(true).Length>0,"A real 3D taxi vehicle is marked for fast travel");
        Check(sabotageTargets.Any(t=>t&&!t.Complete),"At least one physical rival vehicle is available for sabotage");
        Check(crew.Length>0,"Living selected crew is available for interactive actions");
        if(d==null||crew.Length==0){rows[0]="COMPLETE Milestone 4 interactive city playtest";File.WriteAllLines(reportPath,rows);yield break;}

        int oldMoney=d.Money,oldHeat=d.PoliceHeat;var oldActions=new List<string>(d.CampaignActionKeys??new List<string>());
        var oldPositions=crew.Select(a=>a.transform.position).ToArray();var oldHp=crew.Select(a=>a.CurrentHp).ToArray();Vector3 oldTaxi=taxi.transform.position;
        d.Money=Mathf.Max(d.Money,2000);d.PoliceHeat=7;

        var npc=FindObjectsByType<SocialNpc>(FindObjectsSortMode.None).FirstOrDefault(n=>n&&!n.JoinedCrew);
        float wait=12f;while(!npc&&wait>0){wait-=Time.deltaTime;yield return null;npc=FindObjectsByType<SocialNpc>(FindObjectsSortMode.None).FirstOrDefault(n=>n&&!n.JoinedCrew);}
        if(npc)
        {
            yield return ResolveShakedown(npc,crew,true);
            Check(d.PoliceHeat==9&&d.Money>2000,"Forced civilian robbery pays cash and adds exactly two heat");
            Check(CampaignMissions.ActionCount(d,d.CurrentLevel,"extort")>0,"Civilian interaction advances the mission action ledger");
            if(GamePopup.AnyOpen)GamePopup.Instance.Hide();
        }
        else Check(false,"A civilian is available to resist the shakedown");

        d.PoliceHeat=7;d.Money=2000;
        yield return TaxiTravel(crew,5);
        Check(crew.All(a=>!a||Horizontal(a.transform.position,city.Locations[5])<12f),"Taxi blackout transfer teleports selected crew to the chosen destination");
        Check(CampaignMissions.ActionCount(d,d.CurrentLevel,"taxi")>0,"Taxi use advances the mission action ledger");

        var sabotage=sabotageTargets.FirstOrDefault(t=>t&&!t.Complete);
        if(sabotage)
        {
            d.PoliceHeat=7;d.Money=2000;
            yield return PlantCharge(sabotage,crew);
            Check(sabotage.Complete&&d.PoliceHeat==10&&d.Money>2000,"Vehicle charge creates sabotage loot and adds three heat");
            Check(CampaignMissions.ActionCount(d,d.CurrentLevel,"sabotage")>0,"Vehicle sabotage advances the mission action ledger");
            if(GamePopup.AnyOpen)GamePopup.Instance.Hide();
        }
        else Check(false,"A rival vehicle can be destroyed in the live scene");

        d.Money=oldMoney;d.PoliceHeat=oldHeat;d.CampaignActionKeys.Clear();d.CampaignActionKeys.AddRange(oldActions);
        taxi.transform.position=oldTaxi;
        for(int i=0;i<crew.Length;i++)
        {
            var member=crew[i];if(!member)continue;
            var nav=member.GetComponent<NavMeshAgent>();if(nav&&nav.isOnNavMesh)nav.Warp(oldPositions[i]);else member.transform.position=oldPositions[i];
            if(member.CurrentHp<oldHp[i])member.HealAmount(oldHp[i]-member.CurrentHp);
            member.SetActivityLocked(false,member.transform.position);
        }
        GameManager.Save();
        rows[0]="COMPLETE Milestone 4 interactive city playtest";File.WriteAllLines(reportPath,rows);
        if(rows.Any(r=>r.StartsWith("FAIL")))Debug.LogError("Milestone 4 interactive playtest failed; see "+reportPath);
    }
#endif
}

public sealed class CitySabotageTarget:MonoBehaviour
{
    TextMeshPro label;
    public string TargetId{get;private set;}
    public string DisplayName{get;private set;}
    public bool Complete{get;set;}
    public void Setup(string id,string displayName)
    {
        TargetId=id;DisplayName=displayName;
        ZoneVolumeFactory.Create(transform,new Color(1f,.18f,.08f,1f),4.5f);
        label=ZoneLabelUtil.Create(transform,displayName+"\n<size=68%>ACTIONS → SABOTAGE</size>",4.8f,7.2f);
        MiniMapIconFactory.Register(transform,MiniMapIconFactory.Kind.Turf,displayName);
    }
    public void SetStatus(string status){if(label)label.text=DisplayName+"\n<size=68%>"+status+"</size>";}
    void LateUpdate(){if(label&&Camera.main)label.transform.rotation=Camera.main.transform.rotation;}
}

static class SocialNpcActionExtensions
{
    public static bool IsConversationOpenSafe(this SocialNpc npc)=>NpcConversationUI.Instance&&NpcConversationUI.Instance.IsConversationOpen;
}
