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
            versionLabel.text = "v0.1 - PROTOTYPE";

        if (continueBtn != null)
            continueBtn.interactable = GameManager.HasSaveData();

        // Hide the old in-menu club selection — team change is on the dashboard now.
        if (SelectClubPanel != null)
            SelectClubPanel.gameObject.SetActive(false);

        newGameBtn?.AfterClickAnimation.AddListener(OnNewGame);
        continueBtn?.AfterClickAnimation.AddListener(OnContinue);
        settingsBtn?.AfterClickAnimation.AddListener(OnSettings);
        exitBtn?.AfterClickAnimation.AddListener(OnExit);
    }

    void OnNewGame()
    {
        GameManager.instance?.StartNewGameWithDefaultClub(clubRegistry);
    }

    void OnContinue()
    {
        // Continue jumps straight into the streets — no dashboard detour.
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
}
