using Pastoral;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the player's current club info card on the Dashboard.
///
/// Reads club name / firm name from GameData (saved PlayerData),
/// then looks up the matching sprites and colour from the shared ClubRegistry asset.
///
/// Inspector: assign the ClubRegistry asset (same one used everywhere else).
/// </summary>
public class ClubInfoController : MonoBehaviour
{
    [Header("Card UI reference")]
    public ClubCardUI clubCard;

    [Header("Club Data (shared ScriptableObject asset)")]
    [Tooltip("Drag the ClubRegistry asset here — same asset used by ClubSelectionController and MainDashboardController.")]
    public ClubRegistry clubRegistry;

    [Header("Buttons")]
    public ButtonUI backBtn;

    void Start()
    {
        backBtn?.AfterClickAnimation.AddListener(OnBack);
    }

    void OnEnable()
    {
        // Refresh every time the panel is shown so it reflects latest save data
        BuildCard();
    }

    void BuildCard()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null)
        {
            Debug.LogWarning("[ClubInfoController] No PlayerData found.");
            return;
        }

        if (clubRegistry == null)
        {
            Debug.LogError("[ClubInfoController] ClubRegistry asset not assigned!");
            return;
        }

        var club = clubRegistry.FindPlayerClub(d);
        if (club == null)
        {
            Debug.LogWarning($"[ClubInfoController] Club '{d.ClubName}' / firm '{d.FirmName}' not found in ClubRegistry.");
            // Still populate text fields even without sprites
            clubCard?.Setup(d.ClubName, d.FirmName,
                            d.Fans, d.Reputation, d.Money,
                            null, null, Color.white);
            return;
        }

        clubCard?.Setup(d.ClubName, d.FirmName,
                        d.Fans, d.Reputation, d.Money,
                        club.crestSprite, club.bannerSprite,
                        ClubRegistry.ColorFromHex(d.PrimaryColor));
    }

    void OnBack()
    {
        var landscape = GetComponentInParent<LandscapeFrontEnd>();
        if (landscape != null) { landscape.Navigate("home"); return; }
        UIp.UITweeningOutsideScreenViewFrom(
            this,
            GetComponent<RectTransform>(),
            transform.parent.GetComponent<RectTransform>(),
            Vector2.right, 
            GameManager.SLIDE_ANIMATION_MULTIPLIER);
    }
}
