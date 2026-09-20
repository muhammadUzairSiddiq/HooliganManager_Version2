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

        ApplyHomeButtonState();

        SetButtonLabel(newGameBtn ? newGameBtn.gameObject : null, "NEW GAME    >");
        SetButtonLabel(GameObject.Find("LoadGame"), "OPEN HEADQUARTERS");

        // Hide the old in-menu club selection — team change is on the dashboard now.
        if (SelectClubPanel != null)
            SelectClubPanel.gameObject.SetActive(false);

        newGameBtn?.AfterClickAnimation.AddListener(OnNewGame);
        continueBtn?.AfterClickAnimation.AddListener(OnContinue);
        settingsBtn?.AfterClickAnimation.AddListener(OnSettings);
        exitBtn?.AfterClickAnimation.AddListener(OnExit);
    }

    void OnEnable() => ApplyHomeButtonState();

    /// <summary>Home Territory must stay clickable even after PlayerPrefs are wiped.</summary>
    void ApplyHomeButtonState()
    {
        if (continueBtn != null)
        {
            continueBtn.interactable = true;
            SetButtonLabel(continueBtn.gameObject, "ENTER HOME TERRITORY");
        }
        var home = transform.Find("Continue");
        if (home)
        {
            var selectable = home.GetComponent<UnityEngine.UI.Selectable>();
            if (selectable) selectable.interactable = true;
        }
    }

    void OnNewGame()
    {
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
        // Continue jumps straight into the streets — no dashboard detour.
        // Without a save, a default-club campaign is created first.
        GameManager.instance?.EnterHomeTerritoryOrStartNew(clubRegistry);
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
