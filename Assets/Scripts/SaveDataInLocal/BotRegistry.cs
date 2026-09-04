#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScriptableObject — the central source of truth for initial bot (rival firm) settings.
/// </summary>
[CreateAssetMenu(menuName = "Hooligan/Bot Registry", fileName = "BotRegistry")]
public class BotRegistry : ScriptableObject
{
    [System.Serializable]
    public class BotEntry
    {
        public string clubName;
        public string clubShortName;
        public string firmName;
        public string primaryColor;       // hex e.g. "#D71920"
        public string secondaryColor;
        public int fans;
        public int strength;
        public int reputation;
        public int wins;
        public int losses;
        public Sprite crestSprite;        // drag in Inspector
        public Sprite bannerSprite;       // drag in Inspector
        public DifficultyTier difficultyTier = DifficultyTier.Medium;

        // ── Personality ───────────────────────────────────────────────────
        public FirmArchetype archetype = FirmArchetype.Tactical;

        [Tooltip("Short flavour tagline shown in the trip detail panel.")]
        public string motto = "";

        // Archetype-driven stat modifiers (1–10 scale)
        public int policeResistance   = 5;
        public int recruitEfficiency  = 5;
        public int brawlBonus         = 5;
    }

    public List<BotEntry> bots = new List<BotEntry>
    {
        // ── Hard tier ─────────────────────────────────────────────────────
        new BotEntry
        {
            clubName="East End FC", clubShortName="EEFC", firmName="East End Crew",
            primaryColor="#1A3E8C", secondaryColor="#FFFFFF",
            fans=1, strength=24, reputation=15230, wins=28, losses=10,
            difficultyTier=DifficultyTier.Hard,
            archetype=FirmArchetype.Brawler,
            motto="We hit first, we hit hardest.",
            policeResistance=4, recruitEfficiency=5, brawlBonus=9
        },
        new BotEntry
        {
            clubName="West Lions FC", clubShortName="WLFC", firmName="West Lions",
            primaryColor="#C8A800", secondaryColor="#1A1A1A",
            fans=1, strength=25, reputation=11980, wins=22, losses=15,
            difficultyTier=DifficultyTier.Hard,
            archetype=FirmArchetype.Veteran,
            motto="Been doing this since before you were born.",
            policeResistance=7, recruitEfficiency=4, brawlBonus=8
        },

        // ── Medium tier ───────────────────────────────────────────────────
        new BotEntry
        {
            clubName="Green Street FC", clubShortName="GSFC", firmName="Green Street Boys",
            primaryColor="#2E8B3A", secondaryColor="#FFFFFF",
            fans=1, strength=19, reputation=13890, wins=26, losses=12,
            difficultyTier=DifficultyTier.Medium,
            archetype=FirmArchetype.Tactical,
            motto="We don't lose our heads. We take yours.",
            policeResistance=6, recruitEfficiency=6, brawlBonus=5
        },

        // ── Easy tier ─────────────────────────────────────────────────────
        new BotEntry
        {
            clubName="Northside FC", clubShortName="NSFC", firmName="Northside Boys",
            primaryColor="#D71920", secondaryColor="#FFFFFF",
            fans=1, strength=20, reputation=10760, wins=19, losses=18,
            difficultyTier=DifficultyTier.Easy,
            archetype=FirmArchetype.Ambitious,
            motto="We're hungry. We're coming for everyone.",
            policeResistance=3, recruitEfficiency=9, brawlBonus=4
        },
        new BotEntry
        {
            clubName="South City FC", clubShortName="SCFC", firmName="South City Firm",
            primaryColor="#555555", secondaryColor="#CCCCCC",
            fans=1, strength=17, reputation=9940, wins=18, losses=20,
            difficultyTier=DifficultyTier.Easy,
            archetype=FirmArchetype.Slippery,
            motto="You can't catch what you can't see.",
            policeResistance=9, recruitEfficiency=5, brawlBonus=3
        }
    };

    public BotEntry FindByFirmName(string firmName)
    {
        if (string.IsNullOrEmpty(firmName)) return null;
        return bots.FirstOrDefault(b => b.firmName.Equals(firmName, System.StringComparison.OrdinalIgnoreCase));
    }
}

#if UNITY_EDITOR
public static class BotRegistryAssetCreator
{
    [InitializeOnLoadMethod]
    private static void CreateRegistryAsset()
    {
        string directory = "Assets/Data";
        string path = directory + "/BotRegistry.asset";
        
        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder(directory))
        {
            AssetDatabase.CreateFolder("Assets", "Data");
        }

        if (AssetDatabase.LoadAssetAtPath<BotRegistry>(path) == null)
        {
            BotRegistry asset = ScriptableObject.CreateInstance<BotRegistry>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created BotRegistry asset at {path}");
        }
    }
}
#endif
