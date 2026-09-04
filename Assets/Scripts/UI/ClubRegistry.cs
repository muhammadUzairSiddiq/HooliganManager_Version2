using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScriptableObject — the single source of truth for every selectable club.
///
/// HOW TO CREATE:
///   Right-click in the Project window → Create → Hooligan → Club Registry
///   Name it "ClubRegistry" and populate the clubs list in the Inspector.
///
/// HOW TO USE:
///   Drag the asset into the 'clubRegistry' field of:
///     • ClubSelectionController  (MainMenu scene)
///     • MainDashboardController  (Dashboard scene)
///     • ClubInfoController       (Dashboard scene)
///
/// This replaces the old cross-scene ClubSelectionController reference.
/// </summary>
[CreateAssetMenu(menuName = "Hooligan/Club Registry", fileName = "ClubRegistry")]
public class ClubRegistry : ScriptableObject
{
    // ── Club definition ───────────────────────────────────────────────────

    [System.Serializable]
    public class ClubEntry
    {
        public string clubName;
        public string clubShortName;
        public string primaryColor;       // hex e.g. "#D71920"
        public string secondaryColor;
        public string firmName;
        public string rivalClubName;
        public int    fans;
        public int    strength;
        public int    reputation;
        public int    policeHeat;
        public int    ranking;
        public int    money;
        public Sprite crestSprite;        // drag in Inspector
        public Sprite bannerSprite;       // coloured banner at top of card
    }

    // ── Data ──────────────────────────────────────────────────────────────

    public List<ClubEntry> clubs = new List<ClubEntry>
    {
        // Pre-populated defaults — customise in the Inspector asset
        new ClubEntry { clubName="North City FC", clubShortName="NCFC",
                        primaryColor="#D71920", secondaryColor="#FFFFFF",
                        firmName="North City Crew", rivalClubName="East Town FC",
                        fans=1, strength=20, reputation=10,
                        policeHeat=0, ranking=5, money=5000 },
        new ClubEntry { clubName="East Town FC",  clubShortName="ETFC",
                        primaryColor="#1A3E8C", secondaryColor="#FFFFFF",
                        firmName="East Town Boys", rivalClubName="North City FC",
                        fans=1, strength=22, reputation=15,
                        policeHeat=0, ranking=4, money=5500 },
        new ClubEntry { clubName="South United",  clubShortName="SUFC",
                        primaryColor="#2E8B3A", secondaryColor="#FFFFFF",
                        firmName="Green Street Mob", rivalClubName="West End FC",
                        fans=1, strength=19, reputation=12,
                        policeHeat=0, ranking=6, money=4800 },
        new ClubEntry { clubName="West End FC",   clubShortName="WEFC",
                        primaryColor="#C8A800", secondaryColor="#1A1A1A",
                        firmName="Yellow Firm", rivalClubName="South United",
                        fans=1, strength=25, reputation=20,
                        policeHeat=0, ranking=3, money=6000 },
    };

    // ── Lookup helpers ────────────────────────────────────────────────────

    /// <summary>Find the entry matching the player's saved club + firm name. Returns null if not found.</summary>
    public ClubEntry FindPlayerClub(PlayerData d)
    {
        if (d == null) return null;
        return clubs.FirstOrDefault(c => c.clubName == d.ClubName && c.firmName == d.FirmName);
    }

    /// <summary>Find by club name alone (for cases where firm name may differ).</summary>
    public ClubEntry FindByClubName(string clubName)
        => clubs.FirstOrDefault(c => c.clubName == clubName);

    /// <summary>Parse a hex colour string into a UnityEngine.Color.</summary>
    public static Color ColorFromHex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
