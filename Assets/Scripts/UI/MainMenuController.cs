using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main Menu — Continue jumps straight into gameplay; New Game starts with the
/// default club and opens the dashboard (team change lives on the dashboard).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public ButtonUI newGameBtn;
    public ButtonUI continueBtn;
    public ButtonUI settingsBtn;
    public ButtonUI exitBtn;

    [Header("Version Label (optional)")]
    public TextMeshProUGUI versionLabel;

    [Header("Other Panel")]
    [Tooltip("Legacy club selection panel — no longer opened from New Game.")]
    public RectTransform SelectClubPanel;

    [Header("Club Data")]
    public ClubRegistry clubRegistry;

    void Start()
    {
        if (versionLabel != null)
            versionLabel.text = "v0.2 - ENHANCED";

        ApplyContinueState();

        SetButtonLabel(newGameBtn ? newGameBtn.gameObject : null, "PLAY");
        var headquarters = GameObject.Find("LoadGame");
        if (headquarters) headquarters.SetActive(false);

        // Hide the old in-menu club selection — team change is on the dashboard now.
        if (SelectClubPanel != null)
            SelectClubPanel.gameObject.SetActive(false);

        newGameBtn?.AfterClickAnimation.AddListener(OnNewGame);
        continueBtn?.AfterClickAnimation.AddListener(OnContinue);
        settingsBtn?.AfterClickAnimation.AddListener(OnSettings);
        exitBtn?.AfterClickAnimation.AddListener(OnExit);
    }

    void OnEnable() => ApplyContinueState();

    /// <summary>CONTINUE stays visible and only accepts a tap once a campaign exists.</summary>
    void ApplyContinueState()
    {
        if (continueBtn != null)
        {
            continueBtn.gameObject.SetActive(true);
            continueBtn.interactable = GameManager.HasSaveData();
        }
        SetButtonLabel(continueBtn ? continueBtn.gameObject : null, "CONTINUE");
        var note = transform.Find("ContinueSaveNote")?.GetComponent<TextMeshProUGUI>();
        if (note) note.text = "LAST SAVE    " + GameManager.LastSaveCaption();
    }

    void OnNewGame()
    {
        if (SimpleShellLayout.ShowModes()) return;
        if (GameManager.HasSaveData())
        {
            GamePopup.Instance.Show(
                "START NEW GAME?",
                "This will overwrite your saved campaign, including cash, members, recovered injuries, territory and police heat.",
                new GamePopup.Option("KEEP CURRENT SAVE", new Color(.15f, .18f, .22f), null),
                new GamePopup.Option("START NEW GAME", new Color(.70f, .08f, .08f), StartNewGameNow));
            return;
        }

        StartNewGameNow();
    }

    void StartNewGameNow()
    {
        GameManager.instance?.StartNewGameWithDefaultClub(clubRegistry);
    }

    void OnContinue()
    {
        if (!GameManager.HasSaveData()) return;
        GameManager.instance?.ContinueIntoGameplay();
    }

    void OnSettings() => FindFirstObjectByType<LandscapeFrontEnd>()?.Navigate("settings");

    void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static void SetButtonLabel(GameObject root, string label)
    {
        if (!root) return;
        var text = root.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text) text.text = label;
    }
}
