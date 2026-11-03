using System;
using System.Linq;

/// <summary>Saved learning driven by real RTS actions, independent of selection/order state.</summary>
public static class RecoveryProgression
{
    public const int Version = 1;
    public const int StarterCrew = 3;
    public const int StarterCash = 5000;
    public const int StarterHeat = 1;

    public static void Begin(PlayerData data)
    {
        if (data == null) return;
        data.RecoveryVersion = Version;
        data.RecoveryGuidedStart = true;
        data.CampaignStartingHeatApplied = true;
        data.PoliceHeat = StarterHeat;
    }

    public static void Ensure(PlayerData data)
    {
        if (data == null || data.RecoveryVersion >= Version) return;
        // Old saves keep heat, roster, cash and campaign unlocks. They are not
        // forced through new teaching or granted recurring starter resources.
        data.RecoveryVersion = Version;
        data.RecoveryGuidedStart = false;
        data.CampaignStartingHeatApplied = true;
    }

    public static bool OrientationComplete(PlayerData d) => d != null &&
        (!d.RecoveryGuidedStart || d.LearnedSelection && d.LearnedMoveOrders >= 2 &&
         d.LearnedMultiSelection && d.LearnedSplitSelection);

    public static bool DutiesComplete(PlayerData d) => d != null &&
        (!d.RecoveryGuidedStart || d.LearnedGuard && d.LearnedPatrol);

    public static bool RecordSelection(PlayerData d, int count, bool removed = false)
    {
        if (d == null || !d.RecoveryGuidedStart) return false;
        bool changed = false;
        if (count > 0 && !d.LearnedSelection) { d.LearnedSelection = true; changed = true; }
        if (count > 1 && !d.LearnedMultiSelection) { d.LearnedMultiSelection = true; changed = true; }
        if (removed && count > 0 && d.LearnedMultiSelection && !d.LearnedSplitSelection)
        { d.LearnedSplitSelection = true; changed = true; }
        return changed;
    }

    public static bool RecordOrder(PlayerData d, string command)
    {
        if (d == null || !d.RecoveryGuidedStart) return false;
        if (command == "MOVE" && d.LearnedMoveOrders < 2) { d.LearnedMoveOrders++; return true; }
        if (command == "GUARD" && !d.LearnedGuard) { d.LearnedGuard = true; return true; }
        if (command == "PATROL" && !d.LearnedPatrol) { d.LearnedPatrol = true; return true; }
        return false;
    }

    public static string OrientationHint(PlayerData d)
    {
        if (d == null) return "Enter Home Territory to begin.";
        if (!d.LearnedSelection) return "Tap a crew member to select them.";
        if (d.LearnedMoveOrders < 2) return "Tap a nearby street to MOVE. Tap another street - your crew stays selected.";
        if (!d.LearnedMultiSelection) return "Tap another member to add them to your selected group.";
        if (!d.LearnedSplitSelection) return "Tap one selected member again to remove only them. Move the remaining group.";
        return "Direct control learned. Scout the district and build the firm.";
    }

    public static CityOperationType? NextPreparation(PlayerData d)
    {
        var required = new[] { CityOperationType.GatherSupplies, CityOperationType.SupporterRally,
            CityOperationType.CollectTickets, CityOperationType.CommunityEvent,
            CityOperationType.PrepareTransport, CityOperationType.PrepareStadium };
        foreach (var op in required)
            if (!CampaignMissions.OperationComplete(d, op)) return op;
        return null;
    }

    public static string NextGoal(PlayerData d)
    {
        if (d == null) return "Enter Home Territory to begin.";
        if (CityGameplay.HomeMode && !OrientationComplete(d)) return OrientationHint(d);
        var next = CampaignMissions.Steps(d).FirstOrDefault(s => !s.Complete);
        return string.IsNullOrEmpty(next.id) ? "Core objectives complete. Prepare your next Away Trip." : next.title + " - " + next.body;
    }
}
