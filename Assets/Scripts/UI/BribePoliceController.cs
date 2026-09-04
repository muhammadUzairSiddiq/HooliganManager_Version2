using UnityEngine;
using TMPro;

/// <summary>
/// Controls the BribePolice panel on the Dashboard screen.
/// Automatically calculates the bribe cost based on current police heat,
/// handles the payment logic to clear heat, and closes the panel.
/// </summary>
public class BribePoliceController : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI bribeAmountText;
    public ButtonUI bribeBtn;
    public ButtonUI cancelBtn;

    private void Start()
    {
        if (bribeBtn != null)
        {
            bribeBtn.AfterClickAnimation.AddListener(OnBribeClicked);
        }
        if (cancelBtn != null)
        {
            cancelBtn.AfterClickAnimation.AddListener(OnCancelClicked);
        }
    }

    private void OnEnable()
    {
        UpdateBribeUI();
    }

    public void UpdateBribeUI()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;

        // Bribe cost formula: £500 per level of heat.
        int bribeCost = CalculateBribeCost(d.PoliceHeat);

        if (bribeAmountText != null)
        {
            bribeAmountText.text = $"£{bribeCost:N0}";
        }

        if (bribeBtn != null)
        {
            // Bribe is only allowed if they have heat and have enough money to cover the cost
            bribeBtn.interactable = d.Money >= bribeCost && d.PoliceHeat > 0;
        }
    }

    private int CalculateBribeCost(int heat)
    {
        return heat * 200;
    }

    private void OnBribeClicked()
    {
        var d = GameData.instance?.PlayerData;
        if (d == null) return;

        int bribeCost = CalculateBribeCost(d.PoliceHeat);
        if (d.Money >= bribeCost)
        {
            d.Money -= bribeCost;
            int oldHeat = d.PoliceHeat;
            d.PoliceHeat = 5;

            // Log event to the dashboard events log
            GameData.instance.AddEventLog($"🟢 Bribed the police for £{bribeCost:N0}. Heat cooled from {oldHeat} to 5.");

            // Save and refresh
            GameData.instance.SaveData();

            if (MainDashboardController.instance != null)
            {
                MainDashboardController.instance.RefreshUI();
            }

            // Close the bribe panel
            gameObject.SetActive(false);
        }
    }

    private void OnCancelClicked()
    {
        gameObject.SetActive(false);
    }
}
