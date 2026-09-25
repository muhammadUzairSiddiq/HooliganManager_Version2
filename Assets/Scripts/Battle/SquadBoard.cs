using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>City squad sheet and the paid recovery ward beside the hospital.</summary>
public sealed class SquadBoard : MonoBehaviour
{
    public static SquadBoard Instance { get; private set; }
    GameObject _root;
    RectTransform _list;
    string _tab = "active";
    bool _ward;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void ShowSquad() { _ward = false; _tab = "active"; Open(); }
    public void ShowWard() { _ward = true; _tab = "injured"; Open(); }

    void Open()
    {
        var hud = FindFirstObjectByType<LandscapeBattleHUD>();
        if (!hud || !hud.frame) return;
        if (!_root) Build(hud.frame);
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        Refresh();
    }

    void Build(Transform frame)
    {
        var panel = LandscapeUI.Panel("SquadBoard", frame, 280, 120, 1040, 680, true);
        _root = panel.gameObject;
        LandscapeUI.Text("Title", panel.transform, "SQUAD", 24, 16, 520, 36, 28, null, true);
        LandscapeUI.Button("Close", panel.transform, "CLOSE", 880, 14, 136, 42)
            .onClick.AddListener(() => _root.SetActive(false));
        string[] tabs = { "active", "injured", "fallen", "upgrades" };
        string[] labels = { "ACTIVE", "RECOVERING", "FALLEN", "UPGRADES" };
        for (int i = 0; i < tabs.Length; i++)
        {
            string tab = tabs[i];
            LandscapeUI.Button("Tab" + tab, panel.transform, labels[i], 24 + i * 168, 62, 160, 40, "dark")
                .onClick.AddListener(() => { _ward = false; _tab = tab; Refresh(); });
        }
        _list = LandscapeUI.Scroll("Roster", panel.transform, 16, 114, 1008, 548).content;
    }

    void Refresh()
    {
        if (!_list) return;
        var title = _root.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        if (title) title.text = _ward ? "RECOVERY WARD" : "SQUAD";
        LandscapeUI.Clear(_list);
        var d = GameManager.Data;
        if (d?.RecruitedAgents == null) return;
        foreach (var agent in d.RecruitedAgents)
        {
            if (agent == null) continue;
            if (_ward || _tab == "injured")
            {
                if (!agent.NeedsCare) continue;
            }
            else if (_tab == "fallen")
            {
                if (!agent.IsDown) continue;
            }
            else if (_tab == "active")
            {
                if (!agent.IsAlive || agent.CurrentHp < agent.MaxHp) continue;
            }
            AddRow(agent, d);
        }
        if (_list.childCount == 0)
            LandscapeUI.Text("Empty", _list, _tab == "upgrades" ? "NO CREW TO TRAIN" : "NOBODY IN THIS SECTION", 12, 12, 900, 48, 22, LandscapeUI.Muted);
    }

    void AddRow(AgentData agent, PlayerData d)
    {
        var row = LandscapeUI.Panel("Member", _list, 0, 0, 980, _tab == "upgrades" && !_ward ? 168 : 92, true);
        LandscapeUI.LayoutSize(row.gameObject, 980, _tab == "upgrades" && !_ward ? 168 : 92);
        string state = agent.IsDown ? "DOWN" : agent.CurrentHp < agent.MaxHp ? "INJURED" : "FIT";
        Color stateColor = agent.IsDown ? LandscapeUI.Red : agent.CurrentHp < agent.MaxHp ? LandscapeUI.Gold : LandscapeUI.Green;
        LandscapeUI.Text("Name", row.transform, agent.AgentName.ToUpperInvariant(), 16, 8, 360, 28, 22, null, true);
        LandscapeUI.Text("State", row.transform, state + "   LV " + agent.FightLevel + "   WINS " + agent.FightWins, 16, 36, 420, 22, 16, stateColor, true);
        LandscapeUI.Text("Hp", row.transform, $"HP {Mathf.Max(0, agent.CurrentHp):0}/{agent.MaxHp:0}   POW {agent.Strength:0}   SPD {agent.Speed:0.0}", 440, 12, 320, 22, 16, LandscapeUI.Muted);
        LandscapeUI.Text("Mind", row.transform, $"STA {agent.Stamina:0}/{Mathf.Max(1f, agent.MaxStamina):0}   INT {agent.Intelligence}", 440, 36, 320, 22, 16, LandscapeUI.Muted);
        if (agent.NeedsCare)
        {
            int cost = SquadCare.RecoveryCost(agent, d);
            var pay = LandscapeUI.Button("Recover", row.transform, "RECOVER  £" + cost.ToString("N0"), 760, 22, 200, 48, "green");
            pay.interactable = d.Money >= cost;
            var who = agent;
            pay.onClick.AddListener(() => { if (SquadCare.Recover(who, SquadCare.RecoveryCost(who, GameManager.Data))) Refresh(); });
        }
        if (_tab != "upgrades" || _ward) return;
        string[] stats = { "health", "power", "speed", "stamina", "intel" };
        string[] names = { "HEALTH", "POWER", "SPEED", "STAMINA", "INTEL" };
        int[] ranks = { agent.HealthRank, agent.PowerRank, agent.SpeedRank, agent.StaminaRank, agent.IntelRank };
        for (int i = 0; i < stats.Length; i++)
        {
            int cost = SquadCare.UpgradeCost(ranks[i]);
            string label = ranks[i] >= 8 ? names[i] + " MAX" : names[i] + "  £" + cost.ToString("N0");
            var button = LandscapeUI.Button(stats[i], row.transform, label, 16 + i * 190, 72, 180, 76, "dark");
            button.interactable = ranks[i] < 8 && d.Money >= cost;
            string stat = stats[i];
            var who = agent;
            button.onClick.AddListener(() => { if (SquadCare.Upgrade(who, stat)) Refresh(); });
        }
    }
}

/// <summary>Screen pin for the recovery yard beside the hospital.</summary>
public sealed class RecoveryWardPin : MonoBehaviour
{
    public Vector3 Point;
    RectTransform _pin, _frame;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (!cam) return;
        if (!_pin)
        {
            var hud = FindFirstObjectByType<LandscapeBattleHUD>();
            if (!hud) return;
            _frame = hud.frame;
            var button = LandscapeUI.Button("RecoveryWardPin", _frame, "RECOVERY WARD", 0, 0, 230, 48);
            button.onClick.AddListener(() =>
            {
                var board = SquadBoard.Instance ?? FindFirstObjectByType<CityGameplay>()?.gameObject.AddComponent<SquadBoard>();
                board.ShowWard();
                CameraPanTouchOnly.Instance?.FocusOn(Point);
            });
            _pin = button.transform as RectTransform;
            CityMissionHUD.Style(_pin);
            var label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label) { label.color = Color.white; label.fontSize = 18f; }
        }
        Vector3 projected = cam.WorldToScreenPoint(transform.position + Vector3.up * 4f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_frame, projected, null, out var local);
        float x = local.x - _frame.rect.xMin, y = _frame.rect.yMax - local.y;
        bool visible = projected.z > 0.1f && x > 250f && x < _frame.rect.width - 190f && y > 100f && y < _frame.rect.height - 80f;
        _pin.gameObject.SetActive(visible);
        if (visible) LandscapeUI.Place(_pin, x - 115f, y - 24f, 230f, 48f);
    }

    void OnDestroy() { if (_pin) Destroy(_pin.gameObject); }
}
