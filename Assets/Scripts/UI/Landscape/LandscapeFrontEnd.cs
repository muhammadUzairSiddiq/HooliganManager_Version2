using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using static LandscapeUI;

/// <summary>Navigation and live views layered over the original campaign, economy and battle systems.</summary>
public sealed class LandscapeFrontEnd : MonoBehaviour
{
    public bool mainMenu;
    public ClubRegistry clubs;
    public MainDashboardController dashboard;
    public GameObject home, trips, recruitment, rankings, club, squad, missions, settingsPanel;
    public RectTransform squadContent, missionContent, eventContent;
    public TextMeshProUGUI money, fans, reputation, day, screenTitle, homeObjectives, rosterSummary, missionSummary;
    public Slider volumeSlider;
    public Button continueCampaign;
    public Button missionBonusButton;
    public string currentPage = "home";
    string squadFilter = "active";
    int missionTab;
    float nextRefresh;
    LandscapeScreenMotion motion;

    void OnEnable() { GameData.OnSavingData += Refresh; GameData.OnMatchDayEnded += Refresh; }
    void OnDisable() { GameData.OnSavingData -= Refresh; GameData.OnMatchDayEnded -= Refresh; }
    void Start()
    {
        if (volumeSlider)
        {
            volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("HM.MasterVolume", 1));
            volumeSlider.onValueChanged.AddListener(value => { AudioListener.volume = value; PlayerPrefs.SetFloat("HM.MasterVolume", value); });
        }
        AudioListener.volume = PlayerPrefs.GetFloat("HM.MasterVolume", 1);
        Application.targetFrameRate = PlayerPrefs.GetInt("HM.FrameRate", 60);
        motion = GetComponent<LandscapeScreenMotion>();
        Refresh();
        motion?.HighlightButtons(currentPage);
        if (PlayerPrefs.GetInt("OpenRankingsOnLoad", 0) == 1)
        {
            PlayerPrefs.DeleteKey("OpenRankingsOnLoad"); Navigate("rankings");
        }
    }
    void Update()
    {
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 1f; RefreshStats(); }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (settingsPanel && settingsPanel.activeSelf) Navigate("close-settings");
            else if (!mainMenu && currentPage != "home") Navigate("home");
        }
    }
    public void Navigate(string action)
    {
        if (action == "settings") { ShowSettings(true); return; }
        if (action == "close-settings") { ShowSettings(false); PlayerPrefs.Save(); return; }
        if (action == "credits") { GamePopup.Instance.Show("HOOLIGAN MANAGER", "Build your firm. Rule the terraces.\nA football firm strategy game.\nUI artwork: the project's Updated UI collection.", new GamePopup.Option("BACK", PanelColor, null)); return; }
        if (action == "load") { if (GameManager.HasSaveData()) GameManager.instance?.ReturnToDashboard(); return; }
        if (action == "menu") { GameManager.instance?.OnReturnToMainMenu(); return; }
        if (action == "change-team") { dashboard?.OpenChangeTeamPopup(); return; }
        if (action == "end-day")
        {
            GamePopup.Instance.Show("END MATCHDAY", "Resolve pending recruitment, restore the lads and advance the rival firms.",
                new GamePopup.Option("STAY AT HQ", PanelColor, null), new GamePopup.Option("END MATCHDAY", new Color(.18f,.43f,.25f), () => { GameManager.instance?.OnEndMatchDay(); Navigate("home"); })); return;
        }
        if (action == "framerate")
        {
            int fps = Application.targetFrameRate == 60 ? 30 : 60;
            Application.targetFrameRate = fps; PlayerPrefs.SetInt("HM.FrameRate", fps);
            GamePopup.Instance.Show("PERFORMANCE", "Frame limit set to " + fps + " FPS.", new GamePopup.Option("DONE", PanelColor, null)); return;
        }
        if (action.StartsWith("squad-")) { squadFilter = action.Substring(6); BuildSquad(); return; }
        if (action == "campaign" || action == "matchday") { missionTab = action == "campaign" ? 0 : 1; BuildMissions(); return; }
        if (action == "bonus") { ClaimBonus(); return; }
        var pages = new[] { home, trips, recruitment, rankings, club, squad, missions };
        var names = new[] { "home", "trips", "recruitment", "rankings", "club", "squad", "missions" };
        if (Array.IndexOf(names, action) < 0) return;
        currentPage = action;
        if (motion && Application.isPlaying)
        {
            motion.ShowPage(pages, names, action);
        }
        else for (int i = 0; i < pages.Length; i++) if (pages[i])
        {
            pages[i].transform.localScale = Vector3.one;
            pages[i].SetActive(names[i] == action);
        }
        motion?.HighlightButtons(action);
        if (screenTitle) screenTitle.text = action == "home" ? "YOUR TOWN" : action == "trips" ? "AWAY TRIPS" : action.ToUpper();
        if (action == "squad") BuildSquad();
        if (action == "missions") BuildMissions();
        Refresh();
    }
    void ShowSettings(bool visible)
    {
        if (!settingsPanel) return;
        settingsPanel.SetActive(visible);
        if (visible)
        {
            settingsPanel.transform.SetAsLastSibling();
            var rt = settingsPanel.GetComponent<RectTransform>();
            var group = settingsPanel.GetComponent<CanvasGroup>();
            if (!group) group = settingsPanel.AddComponent<CanvasGroup>();
            group.alpha = 1;
            if (rt) rt.localScale = Vector3.one;
            motion?.HighlightButtons("settings");
        }
        else motion?.HighlightButtons(currentPage);
    }
    public void Refresh()
    {
        RefreshStats();
        if (currentPage == "home") BuildEvents();
    }
    void RefreshStats()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;
        if (money) money.text = "£" + d.Money.ToString("N0");
        if (fans) fans.text = d.Fans.ToString("N0");
        if (reputation) reputation.text = d.Reputation.ToString();
        if (day) day.text = "MD " + d.MatchDay.ToString("00");
        if (continueCampaign) continueCampaign.interactable = GameManager.HasSaveData();
        if (homeObjectives) homeObjectives.text = $"<color=#E8BA5A>01</color>  BUILD YOUR CREW\n<size=19><color=#9BADB5>{d.Fans} active members  /  {d.PendingFansGain} incoming</color></size>\n\n<color=#E8BA5A>02</color>  RISE THROUGH THE RANKS\n<size=19><color=#9BADB5>Rank #{d.Ranking}  /  Reputation {d.Reputation}</color></size>\n\n<color=#E8BA5A>03</color>  CONTROL THE STREETS\n<size=19><color=#9BADB5>Campaign level {d.CurrentLevel} of {LevelSystem.MaxLevels}</color></size>";
    }
    void BuildEvents()
    {
        if (!eventContent) return;
        var d = GameData.instance?.PlayerData; if (d == null) return;
        Clear(eventContent);
        int count = 0;
        if (d.RecentEvents != null) foreach (string ev in d.RecentEvents)
        {
            if (count >= 4) break;
            string clean = CleanEvent(ev);
            Text("Event", eventContent, clean, 0, count * 65, 270, 57, 18, Muted);
            count++;
        }
        if (count == 0) Text("Empty", eventContent, "Your story starts here.\nRecruit your crew and plan your first away trip.", 0, 0, 270, 130, 20, Muted);
    }
    static string CleanEvent(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int i = 0; while (i < s.Length && !char.IsLetterOrDigit(s[i])) i++;
        return s.Substring(i);
    }
    public void BuildSquad()
    {
        if (!squadContent) return;
        Clear(squadContent);
        var d = GameData.instance?.PlayerData; if (d == null) return;
        int active = 0, shown = 0;
        if (d.RecruitedAgents != null) foreach (var a in d.RecruitedAgents) if (a.IsAlive) active++;
        if (rosterSummary) rosterSummary.text = $"{active} ACTIVE MEMBERS   /   {d.PendingFansGain} ARRIVING NEXT MATCHDAY";
        if (d.RecruitedAgents != null) foreach (var agent in d.RecruitedAgents)
        {
            if (squadFilter == "fallen" ? agent.IsAlive : !agent.IsAlive) continue;
            if (squadFilter == "injured" && agent.CurrentHp >= agent.MaxHp) continue;
            var a = agent;
            var card = Panel("Member_" + a.AgentId, squadContent, 0, 0, 234, 470);
            LayoutSize(card.gameObject, 234, 470);
            var theme = LandscapeTheme.Current;
            Sprite portrait = theme && theme.portraits != null && theme.portraits.Length > 0 ? theme.portraits[Mathf.Abs(a.PortraitIndex) % theme.portraits.Length] : null;
            Image("Portrait", card.transform, 10, 10, 214, 200, portrait, Color.white, true);
            Text("Name", card.transform, a.AgentName.ToUpper(), 18, 223, 200, 32, 27, null, true);
            Text("Status", card.transform, a.IsAlive ? a.CurrentHp < a.MaxHp ? "RECOVERING" : "MATCH READY" : "FALLEN MEMBER", 18, 260, 200, 25, 17, a.IsAlive ? Green : Red, true);
            Text("Stats", card.transform, $"STRENGTH   {a.Strength:0}\nSPEED          {a.Speed:0.0}\nHEALTH       {Mathf.Max(0,a.CurrentHp):0} / {a.MaxHp:0}", 18, 300, 200, 88, 20, Muted);
            Progress(card.transform, 18, 394, 198, a.MaxHp > 0 ? a.CurrentHp / a.MaxHp : 0, a.IsAlive ? Green : Red);
            int reviveCost = Mathf.CeilToInt(600 * PoliceHeatSystem.GetRecruitCostMultiplier(d));
            var b = Button("MemberAction", card.transform, a.IsAlive ? "VIEW MEMBER" : $"REVIVE · £{reviveCost:N0}", 14, 420, 206, 40, a.IsAlive ? "dark" : "green");
            b.interactable = a.IsAlive || d.Money >= reviveCost;
            b.onClick.AddListener(() =>
            {
                if (!a.IsAlive) { if (GameData.instance.ReviveFan(a, reviveCost)) BuildSquad(); return; }
                GamePopup.Instance.Show(a.AgentName.ToUpper(), $"Health {a.CurrentHp:0}/{a.MaxHp:0}  ·  Strength {a.Strength:0}  ·  Speed {a.Speed:0.0}\nChoose travelling members from the Away Trips screen. End the matchday to recover health.", new GamePopup.Option("BACK TO SQUAD", PanelColor, null));
            });
            shown++;
        }
        if (shown == 0)
        {
            var empty = Text("EmptyRoster", squadContent, squadFilter == "fallen" ? "NO FALLEN MEMBERS\nKeep your crew safe out there." : squadFilter == "injured" ? "YOUR CREW IS FIT\nEvery active member is at full health." : "YOUR FIRM NEEDS MEMBERS\nRecruit fans, then end the matchday to bring them into the squad.", 0, 0, 900, 300, 30, Muted);
            LayoutSize(empty.gameObject, 900, 300);
        }
    }
    FirmMissions.Mission[] GetMissions(PlayerData d) => FirmMissions.Get(d, missionTab);
    public void BuildMissions()
    {
        if (!missionContent) return;
        Clear(missionContent);
        var d = GameData.instance?.PlayerData; if (d == null) return;
        if (d.LandscapeClaimedMissions == null) d.LandscapeClaimedMissions = new List<string>();
        foreach (var item in GetMissions(d))
        {
            var m = item; bool claimed = d.LandscapeClaimedMissions.Contains(m.id); bool ready = m.progress >= m.target;
            var row = Panel("Mission_"+m.id, missionContent, 0, 0, 865, 155); LayoutSize(row.gameObject, 865, 155);
            Image("Icon", row.transform, 22, 30, 64, 64, LandscapeTheme.Current.star, claimed ? Muted : Gold, true);
            Text("Title", row.transform, m.title, 110, 20, 470, 32, 25, null, true);
            Text("Description", row.transform, m.body, 110, 56, 470, 33, 20, Muted);
            Text("ProgressText", row.transform, claimed ? "COMPLETED" : $"{Mathf.Min(m.progress,m.target):N0} / {m.target:N0}", 110, 98, 180, 25, 18, claimed ? Green : Muted);
            Progress(row.transform, 295, 109, 275, (float)m.progress/m.target, Green);
            Text("Reward", row.transform, "£"+m.reward.ToString("N0"), 632, 25, 205, 35, 27, Gold, true, TextAlignmentOptions.Center);
            var b = Button("Claim", row.transform, claimed ? "CLAIMED" : ready ? "CLAIM" : "IN PROGRESS", 630, 78, 207, 49, ready && !claimed ? "green" : "dark");
            b.interactable = ready && !claimed;
            b.onClick.AddListener(() => ClaimMission(m.id));
        }
        int prior = missionTab; missionTab = 1; var daily = GetMissions(d); missionTab = prior;
        int completed = 0; foreach (var m in daily) if (d.LandscapeClaimedMissions.Contains(m.id)) completed++;
        if (missionSummary) missionSummary.text = $"MATCHDAY {d.MatchDay:00}\n\n{completed} / {daily.Length} OBJECTIVES CLAIMED\n\nComplete and claim every matchday objective to collect your bonus.\n\n<size=34><color=#E8BA5A>£750</color></size>";
        if (missionBonusButton) missionBonusButton.interactable = completed == daily.Length && d.LandscapeBonusMatchday != d.MatchDay;
    }
    void ClaimMission(string id)
    {
        if(FirmMissions.Claim(GameManager.Data,id)) GameManager.Save();
        BuildMissions();
    }
    void ClaimBonus()
    {
        if(FirmMissions.ClaimBonus(GameManager.Data)) GameManager.Save();
        BuildMissions();
    }
}
