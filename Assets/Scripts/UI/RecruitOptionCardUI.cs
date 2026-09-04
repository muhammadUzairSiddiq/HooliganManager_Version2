using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pastoral;

/// <summary>
/// Reusable card UI for a single Recruit option row.
/// Attach to each option card prefab; wire fields in Inspector.
/// </summary>
public class RecruitOptionCardUI : MonoBehaviour
{
    [Header("Card Fields")]
    public Image           iconImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI fanRangeText;

    [Header("Selection Highlight")]
    public GameObject selectedHighlight;

    public void Setup(string title, string description, int cost, int minFans, int maxFans, Sprite icon)
    {
        if (titleText)       titleText.text       = title;
        if (descriptionText) descriptionText.text = description;
        if (costText)        costText.text         = $"€ {cost:N0}";
        if (fanRangeText)    fanRangeText.text     = $"+{minFans} - {maxFans} FANS";
        if (iconImage && icon != null) iconImage.sprite = icon;
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight) selectedHighlight.SetActive(selected);
        var currentRect = GetComponent<RectTransform>();
        if(selected)
            StartCoroutine(UIp.UIScaleTweening(currentRect, Vector3.one, Vector3.one*1.05f, DisableOnScaleComplete:false, GameManager.BUTTON_ANIMATION_MULTIPLIER));
        else if (currentRect.localScale != Vector3.one)
            StartCoroutine(UIp.UIScaleTweening(currentRect, currentRect.localScale, Vector3.one, DisableOnScaleComplete:false, GameManager.BUTTON_ANIMATION_MULTIPLIER));
    }
}
