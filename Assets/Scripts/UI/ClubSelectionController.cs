using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Club selection screen. Displays available clubs as cards;
/// the player picks one and confirms to start a new game.
///
/// Attach to the root Canvas / Controller GameObject in ClubSelection scene.
/// Inspector: assign the shared ClubRegistry asset and the clubCards list.
/// </summary>
public class ClubSelectionController : MonoBehaviour
{
    [Header("Club Data (shared ScriptableObject asset)")]
    [Tooltip("Drag the ClubRegistry asset here — same asset used by Dashboard controllers.")]
    public ClubRegistry clubRegistry;

    [Header("Card UI references — one per club (same order as registry clubs list)")]
    public List<ClubCardUI> clubCards;

    [Header("Buttons")]
    public ButtonUI confirmBtn;
    public ButtonUI backBtn;

    // ── State ─────────────────────────────────────────────────────────────
    private int selectedIndex = 0;

    void Start()
    {
        if (clubRegistry == null)
        {
            Debug.LogError("[ClubSelectionController] ClubRegistry asset not assigned!");
            return;
        }

        BuildCards();
        SelectCard(0);

        confirmBtn?.AfterClickAnimation.AddListener(OnConfirm);
        backBtn?.AfterClickAnimation.AddListener(OnBack);
    }

    void BuildCards()
    {
        var clubs = clubRegistry.clubs;
        for (int i = 0; i < clubCards.Count && i < clubs.Count; i++)
        {
            var card = clubCards[i];
            var club = clubs[i];
            card.Setup(club.clubName, club.firmName,
                       club.fans, club.reputation, club.money,
                       club.crestSprite, club.bannerSprite,
                       ClubRegistry.ColorFromHex(club.primaryColor));

            int idx = i; // capture for lambda
            card.GetComponent<Button>()?.onClick.AddListener(() => SelectCard(idx));
        }
    }

    void SelectCard(int index)
    {
        selectedIndex = index;
        for (int i = 0; i < clubCards.Count; i++)
            clubCards[i].SetSelected(i == index);
    }

    void OnConfirm()
    {
        var clubs = clubRegistry.clubs;
        if (selectedIndex < 0 || selectedIndex >= clubs.Count) return;
        var c = clubs[selectedIndex];
        GameManager.instance.OnClubConfirmed(
            c.clubName, c.clubShortName,
            c.primaryColor, c.secondaryColor,
            c.firmName, c.rivalClubName,
            c.fans, c.strength, c.reputation,
            c.policeHeat, c.ranking, c.money);

        GameManager.LoadScene(GameManager.SCENE_DASHBOARD);
    }

    void OnBack()
    {
        Pastoral.UIp.UITweeningOutsideScreenViewFrom(this, GetComponent<RectTransform>(), transform.parent.GetComponent<RectTransform>(), Vector2.left, GameManager.SLIDE_ANIMATION_MULTIPLIER);
    }
}
