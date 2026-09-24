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
        string line = agent.IsAlive
            ? agent.AgentName + " is badly injured."
            : agent.AgentName + " is down.";
        BattleUIController.instance?.ShowAlert(line, 2.4f);
    }
}
