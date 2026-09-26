using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One opposing firm on the living-city map. Wiped firms stop hiring.</summary>
[Serializable]
public class RivalStreetState
{
    public string firmName;
    public int members = 3;
    public int daysAlive;
    public bool wiped;
    public int pending;
    public bool secondPocket;
    public int cityGrowthTicks;
}

/// <summary>
/// All rival firms stay in the city and grow unless the player wipes them.
/// Every 3 player recruits, living firms hire 2 between them.
/// Each matchday, each living firm hires 1 more.
/// </summary>
public static class RivalGrowthSystem
{
    public const int StartMembers = 3;
    public const int MaxPerFirm = 8;
    public const int MaxOnMap = 24;

    public struct SpawnOrder
    {
        public string firmName;
        public int count;
        public bool secondPocket;
        public BotData bot;
    }

    public static void Ensure(PlayerData d)
    {
        if (d == null) return;
        d.RivalStreets ??= new List<RivalStreetState>();
        if (d.RivalBots == null) return;
        foreach (var bot in d.RivalBots)
        {
            if (bot == null || string.IsNullOrEmpty(bot.firmName)) continue;
            if (Find(d, bot.firmName) != null) continue;
            d.RivalStreets.Add(new RivalStreetState { firmName = bot.firmName, members = StartMembers });
        }
    }

    public static void NoteQueuedRecruits(int count) => NotePlayerRecruits(count);

    public static void NotePlayerRecruits(int count)
    {
        var d = GameManager.Data;
        if (d == null || count <= 0) return;
        Ensure(d);
        d.RecruitEchoRemainder += count;
        bool hired = false;
        while (d.RecruitEchoRemainder >= 3)
        {
            d.RecruitEchoRemainder -= 3;
            Hire(d, 2);
            hired = true;
        }
        if (hired)
        {
            GameManager.Save();
            BattleManager.instance?.ReinforceLivingRivals();
        }
    }

    public static void AdvanceMatchday(PlayerData d)
    {
        if (d == null) return;
        Ensure(d);
        foreach (var street in d.RivalStreets)
        {
            if (street == null || street.wiped) continue;
            street.daysAlive++;
            if (street.members < MaxPerFirm) street.members++;
            else street.pending++;
            if (street.daysAlive >= 2) street.secondPocket = true;
        }
    }

    public static void AdvanceLivingCity(PlayerData d)
    {
        Ensure(d);if(d?.RivalStreets==null)return;
        int grew=0;
        foreach(var street in d.RivalStreets)
        {
            if(street==null||street.wiped)continue;
            street.cityGrowthTicks=Mathf.Min(5,street.cityGrowthTicks+1);
            if(street.members<MaxPerFirm){street.members++;grew++;}
            if(street.cityGrowthTicks>=2)street.secondPocket=true;
            foreach(var area in UnityEngine.Object.FindObjectsByType<GangArea>(FindObjectsSortMode.None))
                if(area&&BaseName(area.GangName)==BaseName(street.firmName))area.SetGrowth(street.cityGrowthTicks);
        }
        if(grew>0)CityGameplay.Instance?.PostEvent($"RIVAL NETWORKS EXPANDING · {grew} FIRMS HIRED · SCOUT BEFORE ATTACKING");
        GameManager.Save();
    }

    public static void NoteMemberDown(PlayerData d, string firmName)
    {
        var street = Find(d, firmName);
        if (street == null || street.wiped) return;
        street.members = Mathf.Max(0, street.members - 1);
        if (street.members <= 0)
        {
            street.members = 0;
            street.pending = 0;
            street.wiped = true;
        }
    }

    public static void MarkWiped(PlayerData d, string firmName)
    {
        var street = Find(d, firmName);
        if (street == null) return;
        street.members = 0;
        street.pending = 0;
        street.wiped = true;
        street.secondPocket = false;
    }

    public static List<SpawnOrder> BuildSpawnPlan(PlayerData d)
    {
        var plan = new List<SpawnOrder>();
        if (d == null) return plan;
        Ensure(d);
        int remaining = MaxOnMap;
        foreach (var street in d.RivalStreets)
        {
            if (street == null || street.wiped || remaining <= 0) continue;
            int give = Mathf.Min(street.members, MaxPerFirm, remaining);
            if (give <= 0) continue;
            remaining -= give;
            var bot = d.RivalBots?.Find(b => b != null && string.Equals(b.firmName, street.firmName, StringComparison.OrdinalIgnoreCase));
            plan.Add(new SpawnOrder
            {
                firmName = street.firmName,
                count = give,
                secondPocket = street.secondPocket && give >= 4,
                bot = bot
            });
        }
        return plan;
    }

    public static int DesiredVisible(PlayerData d, string firmName)
    {
        var street = Find(d, firmName);
        if (street == null || street.wiped) return 0;
        return Mathf.Min(street.members, MaxPerFirm);
    }

    public static int GrowthTicks(PlayerData d, string firmName) => Find(d, firmName)?.cityGrowthTicks ?? 0;

    static void Hire(PlayerData d, int count)
    {
        var living = new List<RivalStreetState>();
        foreach (var street in d.RivalStreets)
            if (street != null && !street.wiped) living.Add(street);
        if (living.Count == 0) return;
        for (int i = 0; i < count; i++)
        {
            var street = living[i % living.Count];
            if (street.members < MaxPerFirm) street.members++;
            else street.pending++;
        }
    }

    static RivalStreetState Find(PlayerData d, string firmName)
    {
        if (d?.RivalStreets == null || string.IsNullOrEmpty(firmName)) return null;
        string key = BaseName(firmName);
        foreach (var street in d.RivalStreets)
            if (street != null && string.Equals(BaseName(street.firmName), key, StringComparison.OrdinalIgnoreCase))
                return street;
        return null;
    }

    public static string BaseName(string firmName)
    {
        if (string.IsNullOrEmpty(firmName)) return "";
        int hash = firmName.LastIndexOf(" #", StringComparison.Ordinal);
        return hash > 0 ? firmName.Substring(0, hash) : firmName;
    }
}
