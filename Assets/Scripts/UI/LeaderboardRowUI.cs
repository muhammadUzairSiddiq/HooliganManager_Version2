using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single row in the Rankings leaderboard table.
/// Attach to the leaderboard row prefab.
/// Wire all fields in Inspector.
/// </summary>
public class LeaderboardRowUI : MonoBehaviour
{
    [Header("Row Fields")]
    public TextMeshProUGUI rankText;
    public Image           logoImage;
    public TextMeshProUGUI firmNameText;
    public TextMeshProUGUI reputationText;
    public TextMeshProUGUI winsText;

    [Header("Row Background")]
    public Image rowBackground;

    public void Setup(int rank, string firmName, int reputation, int wins,
                      Sprite logo, Color bgColor)
    {
        if (rankText)       rankText.text       = rank.ToString();
        if (firmNameText)   firmNameText.text   = firmName.ToUpper();
        if (reputationText) reputationText.text = reputation.ToString("N0");
        if (winsText)       winsText.text       = wins.ToString();
        if (logoImage && logo != null) logoImage.sprite = logo;
        if (rowBackground)  rowBackground.color = bgColor;
    }
}
