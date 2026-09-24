using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime title, mode select, and away-only dashboard.
/// The saved scenes still contain the older town shell; this replaces what the player sees.
/// </summary>
public static class SimpleShellLayout
{
    static GameObject modePanel;

    public static void Apply(LandscapeFrontEnd shell)
    {
        if (!shell) return;
        if (shell.mainMenu) BuildTitle(shell);
        else BuildAway(shell);
    }

    /// <summary>Opens the mode list over the existing menu. Returns false if that list is not ready.</summary>
    public static bool ShowModes()
    {
        if (!modePanel) return false;
        modePanel.SetActive(true);
        modePanel.transform.SetAsLastSibling();
        return true;
    }

    static void BuildTitle(LandscapeFrontEnd shell)
    {
        var overlay = shell.transform.Find("SimpleTitle");
        if (overlay) overlay.gameObject.SetActive(false);
        foreach (var name in new[] { "MenuShade", "Accent", "Edition", "TitleArtwork", "Tagline", "NewGame", "Settings", "Credits", "Exit", "Version", "CityCaption", "CityCaptionBody" })
        {
            var child = shell.transform.Find(name);
            if (child) child.gameObject.SetActive(true);
        }
        var away = shell.transform.Find("LoadGame");
        if (away) away.gameObject.SetActive(false);
        var playLabel = shell.transform.Find("NewGame")?.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (playLabel) playLabel.text = "PLAY";
        PrepareContinue(shell.transform);
        AlignMenuColumn(shell.transform);

        if (shell.transform.Find("ModeSelect")) return;
        var modes = LandscapeUI.Rect("ModeSelect", shell.transform, 0, 0, 1600, 900);
        modePanel = modes.gameObject;
        modePanel.SetActive(false);
        var dim = LandscapeUI.Image("Dim", modes, 0, 0, 1600, 900, null, new Color(0.02f, 0.04f, 0.06f, 0.92f));
        dim.raycastTarget = true;
        LandscapeUI.Text("ModeTitle", modes, "CHOOSE A MODE", 200, 180, 1200, 60, 36, LandscapeUI.White, true, TextAlignmentOptions.Center);

        var awayBtn = LandscapeUI.Button("AwayTrip", modes, "AWAY TRIP", 500, 300, 600, 88, "red");
        var homeBtn = LandscapeUI.Button("HomeTerritory", modes, "HOME TERRITORY", 500, 410, 600, 88);
        var police = LandscapeUI.Button("PlayPolice", modes, "PLAY AS POLICE    COMING SOON", 500, 520, 600, 88);
        police.interactable = false;
        var back = LandscapeUI.Button("BackToTitle", modes, "BACK", 500, 680, 600, 64, "outline");

        back.onClick.AddListener(() => modePanel.SetActive(false));
        awayBtn.onClick.AddListener(() => GameManager.instance?.TryOpenAwayTrip(shell.clubs));
        homeBtn.onClick.AddListener(() => GameManager.instance?.EnterHomeTerritoryOrStartNew(shell.clubs));
    }

    static void AlignMenuColumn(Transform root)
    {
        const float left = 52f;
        const float width = 460f;
        const float gap = 14f;
        const float playY = 460f;
        const float playH = 72f;
        const float rowH = 64f;
        float continueY = playY + playH + gap;
        const float noteH = 28f;
        float noteY = continueY + rowH + 4f;
        float rowY = noteY + noteH + gap;
        float half = (width - gap) * 0.5f;
        Place(root, "NewGame", left, playY, width, playH);
        Place(root, "Continue", left, continueY, width, rowH);
        WriteContinueNote(root, left, noteY, width, noteH);
        Place(root, "Settings", left, rowY, half, rowH);
        Place(root, "Credits", left + half + gap, rowY, half, rowH);
        Place(root, "Exit", left, rowY + rowH + gap, width, rowH);
    }

    static void PrepareContinue(Transform root)
    {
        var child = root.Find("Continue");
        if (!child) return;
        child.gameObject.SetActive(true);
        var label = child.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (label) label.text = "CONTINUE";
        var button = child.GetComponent<Selectable>();
        if (button) button.interactable = GameManager.HasSaveData();
    }

    static void WriteContinueNote(Transform root, float x, float y, float w, float h)
    {
        var existing = root.Find("ContinueSaveNote");
        var note = existing ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (!note)
            note = LandscapeUI.Text("ContinueSaveNote", root, "", x, y, w, h, 15, new Color(0.78f, 0.84f, 0.88f, 0.92f), false, TextAlignmentOptions.Left);
        else
            LandscapeUI.Place(note.rectTransform, x, y, w, h);
        note.text = "LAST SAVE    " + GameManager.LastSaveCaption();
        note.fontSize = 15f;
        note.enableAutoSizing = false;
        note.enableWordWrapping = false;
        note.overflowMode = TextOverflowModes.Ellipsis;
        note.alignment = TextAlignmentOptions.Left;
        note.raycastTarget = false;
    }

    static void Place(Transform root, string name, float x, float y, float w, float h)
    {
        var child = root.Find(name) as RectTransform;
        if (child) LandscapeUI.Place(child, x, y, w, h);
    }

    static void BuildAway(LandscapeFrontEnd shell)
    {
        if (shell.home) shell.home.SetActive(false);
        foreach (var page in new[] { shell.recruitment, shell.rankings, shell.club, shell.squad, shell.missions })
            if (page) page.SetActive(false);
        if (shell.trips) shell.trips.SetActive(true);
        shell.currentPage = "trips";
        if (shell.screenTitle) shell.screenTitle.text = "AWAY TRIP";
        HideNamed(shell.transform, "Navigation");
        HideNamed(shell.transform, "EndMatchday");
        HideNamed(shell.transform, "Home");
        HideNamed(shell.transform, "MotivationTicker");
        if (shell.reputation) shell.reputation.gameObject.SetActive(false);
        if (shell.day) shell.day.gameObject.SetActive(false);
        HideNamed(shell.transform, "REPUTATION");
        HideNamed(shell.transform, "MATCHDAY");
        HideNamed(shell.transform, "REPUTATIONIcon");
        HideNamed(shell.transform, "MATCHDAYIcon");
        if (shell.trips)
        {
            var sidebar = shell.trips.transform.Find("Sidebar");
            if (sidebar) sidebar.gameObject.SetActive(false);
            foreach (Transform child in shell.trips.transform)
                if (child.name.StartsWith("Tab_") || child.name == "SideNote") child.gameObject.SetActive(false);
        }
        if (shell.transform.Find("AwayBack")) return;
        var back = LandscapeUI.Button("AwayBack", shell.transform, "BACK", 28, 20, 150, 54, "outline");
        back.onClick.AddListener(() => GameManager.instance?.OnReturnToMainMenu());
    }

    static void HideNamed(Transform root, string name)
    {
        var matches = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in matches)
            if (t.name == name) t.gameObject.SetActive(false);
    }

}
