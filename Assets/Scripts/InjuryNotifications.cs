using System.Collections.Generic;
using UnityEngine;

public sealed class InjuryNotifications : MonoBehaviour
{
    static InjuryNotifications instance;
    readonly Queue<AgentData> pending = new Queue<AgentData>();
    public static void Report(AgentData agent)
    {
        if (agent == null) return;
        if (!instance) instance = new GameObject("Squad injury notifications").AddComponent<InjuryNotifications>();
        if (!instance.pending.Contains(agent)) instance.pending.Enqueue(agent);
    }
    void Update()
    {
        if (pending.Count == 0 || GamePopup.AnyOpen || CityActionSystem.TaxiSessionActive || Time.timeScale == 0) return;
        var agent = pending.Dequeue();
        if (agent.CurrentHp >= agent.MaxHp) return;
        GamePopup.Instance.Show(agent.IsAlive ? "MEMBER INJURED" : "MEMBER DOWN", agent.AgentName + (agent.IsAlive ? " is badly injured." : " is out of action.") + " They remain in your squad. Recover them at headquarters or the recovery point for £" + GameplayTuning.Current.recoveryCost + ".",
            new GamePopup.Option("UNDERSTOOD", LandscapeUI.PanelColor, null));
    }
}
