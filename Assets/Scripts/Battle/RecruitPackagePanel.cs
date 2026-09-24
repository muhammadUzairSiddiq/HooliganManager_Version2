using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static LandscapeUI;

/// <summary>
/// In-city recruitment screen. Mirrors the headquarters "GROW YOUR FOLLOWING" page —
/// same four campaigns, prices, gains and reputation gates — but because the squad is
/// live in the streets, purchased members walk onto the map immediately (up to the
/// squad cap; any overflow is queued for the next matchday).
/// </summary>
public sealed class RecruitPackagePanel : MonoBehaviour
{
    static RecruitPackagePanel instance;
    public static RecruitPackagePanel Instance
    {
        get { if (!instance) { instance = new GameObject("RecruitPackagePanel").AddComponent<RecruitPackagePanel>(); instance.Build(); } return instance; }
    }
    public static bool AnyOpen => instance && instance.IsOpen;
    public bool IsOpen => root && root.activeSelf;

    static readonly string[] RecruitNames = { "Dex", "Mace", "Tiny", "Bruiser", "Kev", "Sully", "Roach", "Tommo", "Baz", "Nash", "Fitz", "Gaz" };

    GameObject root;
    TextMeshProUGUI heading, feedback, confirmLabel, moneyLabel;
    readonly List<Image> cards = new List<Image>();
    readonly List<GameObject> highlights = new List<GameObject>();
    readonly List<TextMeshProUGUI> costLabels = new List<TextMeshProUGUI>();
    readonly List<TextMeshProUGUI> lockLabels = new List<TextMeshProUGUI>();
    Button confirm;
    int selected;
    Vector3 spawnPoint;
    string venue;
    Action onClosed;

    /// <summary>Open the recruitment board. Members spawn around <paramref name="spawnAt"/>.</summary>
    public void Show(Vector3 spawnAt, string venueName = "RECRUITMENT", Action closed = null)
    {
        if (!root) Build();
        spawnPoint = spawnAt; venue = string.IsNullOrEmpty(venueName) ? "RECRUITMENT" : venueName.ToUpperInvariant(); onClosed = closed;
        if (heading) heading.text = "GROW YOUR FOLLOWING  ·  " + venue;
        GameAudio.Play("popup");
        root.SetActive(true);
        Select(FirstUnlocked());
        RefreshCards();
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
        var cb = onClosed; onClosed = null; cb?.Invoke();
    }

    int FirstUnlocked()
    {
        var d = GameManager.Data;
        for (int i = 0; i < RecruitPackages.All.Length; i++) if (RecruitPackages.IsUnlocked(i, d)) return i;
        return 0;
    }

    void Build()
    {
        var canvas = new GameObject("RecruitPackageCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); canvas.transform.SetParent(transform, false);
        var c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 29500;
        ConfigureLandscapeScaler(canvas.GetComponent<CanvasScaler>());
        var safe = Rect("SafeArea", canvas.transform, 0, 0, 1600, 900); Stretch(safe);
        var frame = Rect("Frame", safe, 0, 0, 1600, 900); var viewport = safe.gameObject.AddComponent<LandscapeViewport>(); viewport.frame = frame; viewport.Fit();
        root = Rect("Modal", frame, 0, 0, 1600, 900).gameObject;
        var dim = Image("Dimmer", root.transform, 0, 0, 1600, 900, null, new Color(.02f, .05f, .07f, .62f)); dim.raycastTarget = true;
        dim.gameObject.AddComponent<UiWorldTapBlocker>();

        var panel = Panel("Board", root.transform, 150, 96, 1300, 708, true);
        Image("TitleBand", panel.transform, 24, 18, 1252, 74, null, new Color(.01f, .02f, .03f, .52f));
        Image("Accent", panel.transform, 24, 18, 1252, 6, null, Green);
        heading = Text("Heading", panel.transform, "GROW YOUR FOLLOWING", 44, 30, 900, 50, 34, Gold, true);
        moneyLabel = Text("Money", panel.transform, "", 960, 36, 300, 40, 26, Green, true, TextAlignmentOptions.Right);
        Text("Subheading", panel.transform, "Choose a campaign. In the city, new members join your squad on the spot — overflow arrives next matchday.", 44, 100, 1220, 34, 20, Muted);

        for (int i = 0; i < RecruitPackages.All.Length; i++)
        {
            int index = i; var p = RecruitPackages.All[i];
            var card = Panel("Package_" + i, panel.transform, 44 + i * 306, 146, 290, 330, true);
            var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card; Colors(button);
            button.onClick.AddListener(() => Select(index));
            var highlight = Image("Selection", card.transform, 0, 0, 290, 5, null, Red).gameObject; highlight.SetActive(false);
            Image("Icon", card.transform, 22, 24, 60, 56, LandscapeTheme.Current?.crew, Color.white, true);
            Text("Title", card.transform, p.Title, 22, 96, 250, 60, 24, null, true);
            Text("Description", card.transform, p.Description, 22, 160, 250, 62, 18, Muted);
            costLabels.Add(Text("Cost", card.transform, $"£{p.Cost:N0}", 22, 236, 150, 38, 27, Gold, true));
            Text("Gain", card.transform, RecruitPackages.GainLabel(i), 160, 238, 112, 44, 16, Green, true, TextAlignmentOptions.Right);
            int rep = RecruitPackages.RequiredReputation(i);
            lockLabels.Add(Text("Lock", card.transform, rep > 0 ? "REQUIRES " + rep + " REPUTATION" : "AVAILABLE FROM DAY ONE", 14, 292, 262, 28, 14, Muted, false, TextAlignmentOptions.Center));
            cards.Add(card); highlights.Add(highlight);
        }

        feedback = Text("Feedback", panel.transform, "CHOOSE YOUR RECRUITMENT CAMPAIGN.", 44, 500, 860, 70, 21, Muted);
        confirm = Button("Confirm", panel.transform, "CONFIRM RECRUITMENT", 916, 500, 340, 66, "green");
        confirmLabel = confirm.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        confirm.onClick.AddListener(Purchase);
        Button("Close", panel.transform, "CLOSE", 916, 578, 340, 54, "dark").onClick.AddListener(Hide);
        Text("Footer", panel.transform, "Prices rise while police are watching. Low supporter morale reduces intake.", 44, 590, 860, 40, 16, Muted);
        root.SetActive(false);
    }

    void Select(int index)
    {
        var d = GameManager.Data;
        if (!RecruitPackages.IsUnlocked(index, d))
        {
            if (feedback) feedback.text = $"LOCKED — REQUIRES {RecruitPackages.RequiredReputation(index)} REPUTATION.";
            return;
        }
        selected = index;
        for (int i = 0; i < highlights.Count; i++) highlights[i].SetActive(i == index);
        int cost = RecruitPackages.EffectiveCost(index, d);
        bool affordable = d != null && d.Money >= cost;
        if (confirm) confirm.interactable = affordable;
        if (confirmLabel) confirmLabel.text = affordable ? $"CONFIRM  ·  £{cost:N0}" : $"NEED £{cost:N0}";
        if (feedback)
            feedback.text = d != null && d.PoliceWatchlisted
                ? $"POLICE WATCHING — cost raised to £{cost:N0}. Intake reduced."
                : $"{RecruitPackages.All[index].Title}: {RecruitPackages.GainLabel(index)} join your squad now.";
    }

    void RefreshCards()
    {
        var d = GameManager.Data;
        if (moneyLabel) moneyLabel.text = d != null ? $"£{d.Money:N0}" : "";
        for (int i = 0; i < cards.Count; i++)
        {
            bool unlocked = RecruitPackages.IsUnlocked(i, d);
            var b = cards[i].GetComponent<Button>(); if (b) b.interactable = unlocked;
            if (costLabels[i]) costLabels[i].text = $"£{RecruitPackages.EffectiveCost(i, d):N0}";
            if (lockLabels[i]) lockLabels[i].color = unlocked ? Muted : new Color(1f, .55f, .4f);
        }
    }

    void Purchase()
    {
        var d = GameManager.Data; var bm = BattleManager.instance;
        if (d == null) return;
        if (!RecruitPackages.IsUnlocked(selected, d)) { Select(selected); return; }
        int cost = RecruitPackages.EffectiveCost(selected, d);
        if (d.Money < cost) { if (feedback) feedback.text = $"NOT ENOUGH CASH — £{cost:N0} REQUIRED."; return; }

        var package = RecruitPackages.All[selected];
        int gain = RecruitPackages.RollGain(selected, d);
        d.Money -= cost;

        int spawned = 0;
        if (bm != null)
        {
            int free = Mathf.Max(0, bm.maxPlayerAgents - bm.PlayerAgents.Count(a => a && a.IsAlive));
            int toSpawn = Mathf.Min(gain, free);
            for (int i = 0; i < toSpawn; i++)
            {
                int nameIndex = ((d.RecruitedAgents?.Count ?? 0) + i) % RecruitNames.Length;
                Vector3 offset = new Vector3(Mathf.Cos(i * 1.7f) * 2.2f, 0, Mathf.Sin(i * 1.7f) * 2.2f);
                if (bm.SpawnRecruitedAgentAt(spawnPoint + offset, RecruitNames[nameIndex]) != null) spawned++;
            }
        }
        int queued = gain - spawned;
        if (queued > 0) { d.PendingFansGain += queued; RivalGrowthSystem.NoteQueuedRecruits(queued); }

        bm?.PersistBattleProgress();
        GameManager.Save();
        GameAudio.Play("recovery");
        RefreshCards(); Select(selected);

        string summary = spawned > 0 ? $"+{spawned} MEMBER{(spawned == 1 ? "" : "S")} JOINED" : "SQUAD FULL";
        if (queued > 0) summary += $" · {queued} ARRIVE NEXT MATCHDAY";
        if (feedback) feedback.text = $"RECRUITMENT CONFIRMED · {summary} · £{cost:N0} SPENT.";
        CityGameplay.Instance?.PostEvent($"{venue} - {summary}");
        BattleUIController.instance?.ShowAlert($"{summary}", 2.4f);
        if (spawned > 0) AgentSelectionManager.CreateCommandMarker(spawnPoint, Green, $"+{spawned} RECRUITED");

        string body =
            $"{package.Title}   ·   £{cost:N0} spent\n\n" +
            (spawned > 0 ? $"<color=#70F2A0>+{spawned} new member{(spawned == 1 ? "" : "s")} joined your squad on the street.</color>\n" : "<color=#FFD36A>Your active squad is at capacity — nobody could join right now.</color>\n") +
            (queued > 0 ? $"<color=#E8BA5A>{queued}</color> more will arrive when you end the matchday.\n" : "") +
            $"\nActive squad: {(bm != null ? bm.PlayerAgents.Count(a => a && a.IsAlive) : 0)} / {(bm != null ? bm.maxPlayerAgents : 0)}";
        Hide();
        GamePopup.Instance.Show("RECRUITMENT CONFIRMED", body,
            new GamePopup.Option("BACK TO THE STREETS", PanelColor, null),
            new GamePopup.Option("RECRUIT MORE", new Color(.18f, .43f, .25f), () => Show(spawnPoint, venue, null)));
    }
}
