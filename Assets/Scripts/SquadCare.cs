using System.Linq;
using UnityEngine;

/// <summary>One recovery transaction shared by HQ, field hospital and squad packages.</summary>
public static class SquadCare
{
    public static bool Recover(AgentData agent, int cost)
    {
        var d = GameManager.Data;
        if (d == null || agent == null || d.RecruitedAgents == null || !d.RecruitedAgents.Contains(agent) || cost < 0) return false;
        BattleManager.instance?.PersistBattleProgress();
        if (agent.CurrentHp >= agent.MaxHp || d.Money < cost) return false;
        d.Money -= cost; agent.FullHeal();
        BattleManager.instance?.RestoreRecoveredAgent(agent);
        GameData.instance.SyncFanCountWithAgents(); GameManager.Save(); GameAudio.Play("recovery");
        return true;
    }
    public static bool RecoverAll(int cost)
    {
        BattleManager.instance?.PersistBattleProgress();
        var d = GameManager.Data;
        if (d?.RecruitedAgents == null || cost < 0 || d.Money < cost) return false;
        var injured = d.RecruitedAgents.Where(a => a != null && a.CurrentHp < a.MaxHp).ToArray();
        if (injured.Length == 0) return false;
        d.Money -= cost;
        foreach (var a in injured) { a.FullHeal(); BattleManager.instance?.RestoreRecoveredAgent(a); }
        GameData.instance.SyncFanCountWithAgents(); GameManager.Save(); GameAudio.Play("recovery"); return true;
    }
    public static void ShowPackages()
    {
        var t = GameplayTuning.Current;
        GamePopup.Instance.Show("SQUAD SUPPORT", "Health care restores every injured or downed member. Power coaching adds strength to your roster, with a campaign limit of " + t.maxPowerPackages + " purchases. Prices use earned game cash.",
            new GamePopup.Option("HEALTH · £" + t.healthPackageCost, LandscapeUI.Green, () => Result(RecoverAll(t.healthPackageCost))),
            new GamePopup.Option("POWER · £" + t.powerPackageCost, LandscapeUI.Gold, () => Result(BuyPower())),
            new GamePopup.Option("CLOSE", LandscapeUI.PanelColor, null));
    }
    static bool BuyPower()
    {
        var d = GameManager.Data; var t = GameplayTuning.Current;
        if (d?.RecruitedAgents == null || d.RecruitedAgents.Count == 0 || d.Money < t.powerPackageCost || d.PowerPackagesPurchased >= t.maxPowerPackages) return false;
        d.Money -= t.powerPackageCost; d.PowerPackagesPurchased++;
        foreach (var a in d.RecruitedAgents) if (a != null) a.Strength += t.packageStrength;
        GameManager.Save(); GameAudio.Play("recovery"); return true;
    }
    static void Result(bool ok) => GamePopup.Instance.Show(ok ? "SQUAD UPDATED" : "PACKAGE UNAVAILABLE", ok ? "Your squad is ready. Changes saved." : "Check your cash, squad condition and upgrade limit.");
}
