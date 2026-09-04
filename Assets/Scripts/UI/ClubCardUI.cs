using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pastoral;

/// <summary>
/// Reusable component for a single Club card in the Club Selection screen.
/// Attach to each club card prefab / GameObject.
/// Wire the Image and TextMeshPro fields in Inspector.
/// </summary>
public class ClubCardUI : MonoBehaviour
{
    [Header("Card visuals")]
    public Image       bannerImage;       // coloured drape at top
    public Image       crestImage;        // club badge
    public TextMeshProUGUI clubNameText;  // e.g. "NORTH CITY FC"
    public TextMeshProUGUI firmNameText;  // e.g. "North City Crew"

    [Header("Stats row")]
    public TextMeshProUGUI fansText;
    public TextMeshProUGUI reputationText;
    public TextMeshProUGUI cashText;

    [Header("Selection highlight")]
    public GameObject selectedBorder;     // a highlighted border GO — toggle on/off

    // ── API ───────────────────────────────────────────────────────────────
    public void Setup(string clubName, string firmName,
                      int fans, int reputation, int money,
                      Sprite crest, Sprite banner, Color primaryColor)
    {
        if (clubNameText)  clubNameText.text  = clubName.ToUpper();
        if (firmNameText)  firmNameText.text  = firmName;
        if (fansText)      fansText.text       = fans.ToString("N0");
        if (reputationText) reputationText.text = reputation.ToString();
        if (cashText)      cashText.text       = $"£{money:N0}";

        if (crestImage  && crest   != null) crestImage.sprite   = crest;
        if (bannerImage && banner  != null) bannerImage.sprite  = banner;
        if (bannerImage) bannerImage.color = primaryColor;
    }

    public void SetSelected(bool selected)
    {
        if (selectedBorder) selectedBorder.SetActive(selected);
        var currentRect = GetComponent<RectTransform>();
        if(selected)
            StartCoroutine(UIp.UIScaleTweening(currentRect, Vector3.one, Vector3.one*1.05f, DisableOnScaleComplete:false, GameManager.BUTTON_ANIMATION_MULTIPLIER));
        else if (currentRect.localScale != Vector3.one)
            StartCoroutine(UIp.UIScaleTweening(currentRect, currentRect.localScale, Vector3.one, DisableOnScaleComplete:false, GameManager.BUTTON_ANIMATION_MULTIPLIER));
    }
}
