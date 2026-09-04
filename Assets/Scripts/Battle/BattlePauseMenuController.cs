using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the in-battle pause menu panel.
///
/// Inspector wiring required (add a child Panel to your BattleScene Canvas):
///   — Assign this script's pausePanel, resumeButton, abortButton fields.
///   — The panel should sit above all other HUD elements (high sort order).
///
/// The panel is hidden on Start and toggled via Show/Hide.
/// </summary>
public class BattlePauseMenuController : MonoBehaviour
{
    public static BattlePauseMenuController instance;

    [Header("Panel")]
    [Tooltip("Root GameObject of the pause menu overlay.")]
    public GameObject pausePanel;

    [Header("Buttons")]
    public Button resumeButton;
    public Button abortButton;

    // ── Abort confirm state ───────────────────────────────────────────────
    // Two-tap safety: first tap shows a warning, second tap actually aborts.
    private bool _awaitingAbortConfirm = false;
    private const string ABORT_FIRST_TAP  = "ABANDON BATTLE?\nTAP AGAIN TO CONFIRM";
    private const string ABORT_BUTTON_LABEL = "ABORT BATTLE";

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    void Start()
    {
        // Ensure panel starts hidden
        if (pausePanel) pausePanel.SetActive(false);

        resumeButton?.onClick.AddListener(OnResumePressed);
        abortButton?.onClick.AddListener(OnAbortPressed);

        ResetAbortButton();
    }

    // ── Public API (called by BattleUIController) ─────────────────────────

    /// <summary>Opens the pause menu and pauses the battle.</summary>
    public void Show()
    {
        ResetAbortButton();
        if (pausePanel) pausePanel.SetActive(true);
        BattleManager.instance?.SetPaused(true);
    }

    /// <summary>Closes the pause menu and resumes the battle.</summary>
    public void Hide()
    {
        ResetAbortButton();
        if (pausePanel) pausePanel.SetActive(false);
        BattleManager.instance?.SetPaused(false);
    }

    /// <summary>True while the pause panel is visible.</summary>
    public bool IsVisible => pausePanel != null && pausePanel.activeSelf;

    // ── Button handlers ───────────────────────────────────────────────────

    private void OnResumePressed()
    {
        Hide();

        // Sync the HUD pause button icon back to the paused state
        BattleUIController.instance?.SyncPauseButtonIcon(false);
    }

    private void OnAbortPressed()
    {
        if (!_awaitingAbortConfirm)
        {
            // First tap — ask the player to confirm
            _awaitingAbortConfirm = true;
            SetAbortButtonText(ABORT_FIRST_TAP);
        }
        else
        {
            // Second tap — confirmed, leave the battle
            ConfirmAbort();
        }
    }

    private void ConfirmAbort()
    {
        // Ensure time is running again before loading a new scene
        Time.timeScale = 1f;

        // Trigger matchday end so all strategy systems fire (heat cool, morale,
        // rival simulation, ranking, advisor tips) even on a retreat.
        GameData.instance?.EndMatchDay();

        // Use GameManager to return to the dashboard cleanly
        if (GameManager.instance != null)
            GameManager.instance.ReturnToDashboard();
        else
        {
            // Fallback — still use the faded loading transition.
            GameManager.LoadScene(GameManager.SCENE_DASHBOARD);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ResetAbortButton()
    {
        _awaitingAbortConfirm = false;
        SetAbortButtonText(ABORT_BUTTON_LABEL);
    }

    private void SetAbortButtonText(string text)
    {
        if (abortButton == null) return;
        var label = abortButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label) label.text = text;
    }
}
