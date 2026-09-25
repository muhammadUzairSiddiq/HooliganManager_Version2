using System.Linq;
using UnityEngine;

/// <summary>One recovery transaction shared by HQ, field hospital and squad packages.</summary>
public static class SquadCare
{
    /// <summary>One new lad from the cheapest campaign. A dead member costs at least this much.</summary>
    public static int NewHireRate(PlayerData d)
    {
        int pack = RecruitPackages.EffectiveCost(0, d);
        int heads = Mathf.Max(1, RecruitPackages.All[0].MinFans);
        return Mathf.Max(1, pack / heads);
    }

    /// <summary>Lower health costs more. Zero health costs more than hiring a replacement.</summary>
    public static int RecoveryCost(AgentData agent, PlayerData d)
    {
        if (agent == null || agent.MaxHp <= 0f || agent.CurrentHp >= agent.MaxHp) return 0;
        int hire = NewHireRate(d);
        float missing = Mathf.Clamp01(1f - Mathf.Max(0f, agent.CurrentHp) / agent.MaxHp);
        return Mathf.Max(1, Mathf.RoundToInt(hire * Mathf.Lerp(0.35f, 1.15f, missing)));
    }

    public static int ReviveCrewCost(PlayerData d)
    {
        if (d?.RecruitedAgents == null || d.RecruitedAgents.Count == 0) return NewHireRate(d);
        int sum = 0;
        int people = 0;
        foreach (var agent in d.RecruitedAgents)
        {
            if (agent == null) continue;
            people++;
            if (agent.CurrentHp < agent.MaxHp) sum += RecoveryCost(agent, d);
        }
        if (sum <= 0) sum = NewHireRate(d) * Mathf.Max(1, people);
        return sum;
    }

    public static int UpgradeCost(int rank) => 400 + Mathf.Max(0, rank) * 250;

    public static bool Upgrade(AgentData agent, string stat)
    {
        var d = GameManager.Data;
        if (d == null || agent == null || d.RecruitedAgents == null || !d.RecruitedAgents.Contains(agent)) return false;
        agent.EnsureManagementProfile();
        if (agent.MaxStamina < 1f) agent.MaxStamina = 100f;
        int rank = stat switch
        {
            "health" => agent.HealthRank,
            "power" => agent.PowerRank,
            "speed" => agent.SpeedRank,
            "stamina" => agent.StaminaRank,
            "intel" => agent.IntelRank,
            _ => 99
        };
        if (rank >= 8) return false;
        int cost = UpgradeCost(rank);
        if (d.Money < cost) return false;
        d.Money -= cost;
        switch (stat)
        {
            case "health":
                agent.HealthRank++;
                agent.MaxHp += 10f;
                if (agent.CurrentHp > 0f) agent.CurrentHp = Mathf.Min(agent.MaxHp, agent.CurrentHp + 10f);
                break;
            case "power":
                agent.PowerRank++;
                agent.Strength += 2f;
                break;
            case "speed":
                agent.SpeedRank++;
                agent.Speed += 0.2f;
                break;
            case "stamina":
                agent.StaminaRank++;
                agent.MaxStamina += 15f;
                agent.Stamina = Mathf.Min(agent.MaxStamina, agent.Stamina + 15f);
                break;
            case "intel":
                agent.IntelRank++;
                agent.Intelligence += 1;
                break;
            default: return false;
        }
        var live = BattleManager.instance?.PlayerAgents.FirstOrDefault(a => a && a.Data != null && a.Data.AgentId == agent.AgentId);
        live?.ApplyRosterStats();
        GameManager.Save();
        GameAudio.Play("recovery");
        return true;
    }

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
