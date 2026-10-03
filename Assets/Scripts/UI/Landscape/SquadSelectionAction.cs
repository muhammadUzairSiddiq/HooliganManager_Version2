using UnityEngine;
public sealed class SquadSelectionAction:MonoBehaviour
{
    public void SelectAll()
    {
        RtsGestureController.Instance?.Cancel();
        AgentSelectionManager.instance?.SelectAll();
        BattleUIController.instance?.ShowAlert("ALL CREW SELECTED · Use DRAG for a box selection",2f);
    }
}
