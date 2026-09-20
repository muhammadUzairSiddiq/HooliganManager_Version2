using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Capturable pub/turf point. Does NOT silently start fights — that is owned by
/// <see cref="GangArea"/> (HAVE IT / MOVE ON). This only handles capture progress
/// when no rival fighters are present.
/// </summary>
public class TerritoryControlPoint : MonoBehaviour
{
    public string zoneName = "Local Pub";
    public float captureDuration = 12f;
    public int moneyReward = 450;
    public int occupationTax = 300;
    public int reputationReward = 10;
    public int heatGained = 2;

    public float detectionRadius = 8f;

    [Header("Faction Setup")]
    public string owningFaction = "";

    private float _captureProgress = 0f;
    private bool _isCaptured = false;

    private readonly List<AgentController> _playerUnits = new List<AgentController>();
    private readonly List<EnemyController> _enemyUnits = new List<EnemyController>();

    public bool IsCaptured => _isCaptured;
    TMPro.TextMeshPro statusLabel;
    LineRenderer zoneRing, progressRing, zoneGlow;
    int lastPercent = -1;
    void Start()
    {
        captureDuration = GameplayTuning.Current.captureSeconds;
        _isCaptured = GameManager.Data?.CityCapturedZones?.Contains(zoneName) == true;
        statusLabel = ZoneLabelUtil.Create(transform, zoneName, 4.4f, 7.6f);
        MiniMapIconFactory.Register(transform, MiniMapIconFactory.Kind.Turf, zoneName);
        zoneGlow=BuildRing("TerritoryGlow",detectionRadius+.18f,.78f);
        zoneRing=BuildRing("TerritoryBoundary",detectionRadius,.42f);
        progressRing=BuildRing("CaptureProgress",detectionRadius-.48f,.62f);
        UpdateLabel();
    }
    void UpdateLabel()
    {
        if (!statusLabel) return;
        string line;
        if (_isCaptured)
            line = zoneName.ToUpperInvariant() + "\n<size=68%>SECURED · COLLECT INCOME</size>";
        else if (_playerUnits.Count > 0 && _enemyUnits.Count > 0)
            line = zoneName.ToUpperInvariant() + $"\n<size=68%>DEFEAT {_enemyUnits.Count} RIVAL{(_enemyUnits.Count==1?"":"S")} TO TAKE CONTROL</size>";
        else if (_playerUnits.Count == 0)
            line = zoneName.ToUpperInvariant() + $"\n<size=68%>HOLD AREA · OCCUPATION TAX £{occupationTax:N0}</size>";
        else
        {
            int pct = Mathf.RoundToInt(_captureProgress * 100);
            line = zoneName.ToUpperInvariant() + $"\n<size=68%>KEEP CREW INSIDE · {pct}%</size>";
        }
        statusLabel.richText = true;
        statusLabel.text = line;
        statusLabel.color = Color.white;

        Color stateColor = _isCaptured ? new Color(.18f, 1f, .42f, 1f) : _enemyUnits.Count > 0 ? new Color(1f, .18f, .12f, 1f) : new Color(1f, .72f, .12f, 1f);
        if (zoneGlow)
        {
            float pulse=.15f+Mathf.PingPong(Time.time*.22f,.14f);
            zoneGlow.startColor=zoneGlow.endColor=new Color(stateColor.r,stateColor.g,stateColor.b,pulse);
        }
        if (zoneRing) { zoneRing.startColor = zoneRing.endColor = stateColor; zoneRing.startWidth = zoneRing.endWidth = _enemyUnits.Count > 0 ? .56f : .42f; }
        if (progressRing)
        {
            progressRing.startColor = progressRing.endColor = new Color(.18f, 1f, .42f, 1f);
            int count = _isCaptured ? 64 : Mathf.Clamp(Mathf.CeilToInt(_captureProgress * 64), 0, 64);
            progressRing.positionCount = Mathf.Max(2, count);
            progressRing.enabled = count > 1;
            float radius = detectionRadius - .35f;
            for (int i = 0; i < progressRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / 64f;
                progressRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }
    }

    LineRenderer BuildRing(string name, float radius, float width)
    {
        return ZoneVolumeFactory.BuildFlatCircle(transform, name, radius, width, Color.white, loop: name == "TerritoryBoundary", segments: 64);
    }

    void OnDisable()
    {
        if (Arikan.MiniMapView.Instance != null)
            Arikan.MiniMapView.Instance.UnfollowTarget(transform);
    }

    void Update()
    {
        if (statusLabel && Camera.main) statusLabel.transform.rotation = Camera.main.transform.rotation;
        if (_isCaptured) return;
        if (BattleManager.instance == null) return;

        _playerUnits.Clear();
        _enemyUnits.Clear();

        foreach (var a in BattleManager.instance.PlayerAgents)
        {
            if (a != null && a.IsAlive &&
                Vector3.Distance(transform.position, a.transform.position) <= detectionRadius)
                _playerUnits.Add(a);
        }

        foreach (var e in BattleManager.instance.EnemyAgents)
        {
            if (e != null && e.IsAlive && e.firmName != "POLICE" &&
                Vector3.Distance(transform.position, e.transform.position) <= detectionRadius)
                _enemyUnits.Add(e);
        }

        UpdateLabel();
        // Capture only when the pad is clear of rivals — never auto-aggro them.
        if (_playerUnits.Count > 0 && _enemyUnits.Count == 0)
        {
            _captureProgress += Time.deltaTime / captureDuration;
            _captureProgress = Mathf.Clamp01(_captureProgress);

            if (BattleUIController.instance != null && BattleUIController.instance.objectiveText != null)
                BattleUIController.instance.objectiveText.text =
                    $"SECURING {zoneName.ToUpper()}: {Mathf.RoundToInt(_captureProgress * 100f)}%";

            if (_captureProgress >= 1f)
            {
                if(GameManager.Data!=null&&GameManager.Data.Money<occupationTax)
                {
                    _captureProgress=.94f;
                    CityGameplay.Instance?.PostEvent($"{zoneName.ToUpperInvariant()} NEEDS £{occupationTax:N0} OCCUPATION TAX · EARN CASH FIRST");
                }
                else CaptureZone();
            }
        }
        else if (_enemyUnits.Count > 0 && _playerUnits.Count == 0)
        {
            if (_captureProgress > 0f)
                _captureProgress = Mathf.Max(0f, _captureProgress - Time.deltaTime / captureDuration);
        }
    }

    private void CaptureZone()
    {
        _isCaptured = true;
        owningFaction = GameManager.Data?.FirmName ?? "YOUR FIRM";
        UpdateLabel(); GameAudio.Play("capture");
        if(gameObject.scene.name=="Gameplay" && GameManager.Data!=null)
        {
            var data=GameManager.Data;
            if(data.CityCapturedZones==null)data.CityCapturedZones=new List<string>();
            if(data.CityCapturedZones.Contains(zoneName))return;
            data.CityCapturedZones.Add(zoneName);
            data.Money=Mathf.Max(0,data.Money-occupationTax);data.Reputation+=reputationReward;
            data.PoliceHeat=Mathf.Clamp(data.PoliceHeat+heatGained,0,10);
            CampaignMissions.RecordTerritory(data,zoneName);
            int secured=CampaignMissions.TerritoryCount(data,data.CurrentLevel);
            string benefit=ApplyStrategicBenefit(data,secured);
            GameManager.Save();
            CityGameplay.Instance?.PostEvent("TERRITORY SECURED - "+zoneName.ToUpperInvariant()+" / "+benefit.ToUpperInvariant());
        }

        if (gameObject.scene.name != "Gameplay" && BattleManager.instance != null)
        {
            BattleManager.instance.sessionMoneyEarned += moneyReward;
            BattleManager.instance.sessionReputationGained += reputationReward;
            BattleManager.instance.sessionHeatGained += heatGained;
        }

        if (BattleUIController.instance != null)
        {
            if (BattleUIController.instance.objectiveText != null)
                BattleUIController.instance.objectiveText.text = $"SECURED {zoneName.ToUpper()} · TAX PAID £{occupationTax:N0}";
            BattleUIController.instance.ShowAlert(
                $"Secured {zoneName}! Occupation tax £{occupationTax:N0}", 4f);
        }

        bool missionComplete=CityGameplay.Instance?.CheckCampaignCompletion()??false;
        if(!missionComplete)
        {
            int secured=CampaignMissions.TerritoryCount(GameManager.Data,GameManager.Data?.CurrentLevel??1);
            string benefit=BenefitName(secured);
            GamePopup.Instance.Show(
                "TERRITORY WON",
                $"You secured {zoneName.ToUpperInvariant()}.\n\nOccupation tax paid: £{occupationTax:N0}  ·  Reputation: +{reputationReward}\nProtection income available: £{moneyReward:N0}\nStrategic benefit: {benefit}\nPolice heat: +{heatGained}\n\nReturn to this area and use ACTIONS → COLLECT INCOME.",
                new GamePopup.Option("KEEP MOVING", new Color(0.18f, 0.55f, 0.25f), () =>
                {
                    AgentSelectionManager.instance?.SelectAll();
                    CameraPanTouchOnly.Instance?.CenterOnSelection();
                }));
        }

        Debug.Log($"[TerritoryControlPoint] Secured {zoneName}!");
        UpdateLabel();
    }

    static string ApplyStrategicBenefit(PlayerData data,int secured)
    {
        switch(secured)
        {
            case 1:data.CityIntel=Mathf.Min(10,data.CityIntel+1);break;
            case 2:data.CitySupplies+=1;break;
            default:data.SocialMomentum+=1;data.FanMorale=Mathf.Min(100,data.FanMorale+5);break;
        }
        return BenefitName(secured);
    }

    static string BenefitName(int secured)=>secured switch
    {
        1=>"Safe route (+1 intel)",
        2=>"Supply cache (+1 supplies)",
        _=>"Local network (+1 social, +5 morale)"
    };

    public bool CanCollectIncome
    {
        get
        {
            var d=GameManager.Data;if(!_isCaptured||d==null)return false;
            string token=zoneName+":DAY"+d.MatchDay;
            return d.CampaignActionKeys==null||!d.CampaignActionKeys.Contains($"M{d.CurrentLevel}:tax:{token}");
        }
    }

    public bool CollectIncome()
    {
        var d=GameManager.Data;if(!CanCollectIncome||d==null)return false;
        string token=zoneName+":DAY"+d.MatchDay;
        d.Money+=moneyReward;
        CampaignMissions.RecordAction(d,"tax",token);
        GameManager.Save();
        AgentSelectionManager.CreateCommandMarker(transform.position,LandscapeUI.Green,"+£"+moneyReward.ToString("N0"));
        CityGameplay.Instance?.PostEvent($"{zoneName.ToUpperInvariant()} PROTECTION INCOME +£{moneyReward:N0}");
        GamePopup.Instance.Show("INCOME COLLECTED",$"{zoneName.ToUpperInvariant()} generated £{moneyReward:N0}.\n\nThis is earned income from controlled turf—not a capture reward. Another payment becomes available on a future matchday.",new GamePopup.Option("DONE",LandscapeUI.Green,null));
        CityGameplay.Instance?.CheckCampaignCompletion();
        UpdateLabel();
        return true;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = _isCaptured ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
