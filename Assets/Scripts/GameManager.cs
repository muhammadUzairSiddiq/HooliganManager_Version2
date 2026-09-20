using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central scene-navigation manager.
/// GameData handles all save/load; GameManager handles screen flow.
/// </summary>
public class GameManager : MonoBehaviour
{
    private static readonly bool PlayablePoliceEnabled = false; // Milestone 3 feature gate.

    // ── Animation constants ───────────────────────────────────────────────
    public const float SLIDE_ANIMATION_MULTIPLIER  = 5;
    public const float BUTTON_ANIMATION_MULTIPLIER  = 10;

    // ── Scene constants ───────────────────────────────────────────────────
    public const string SCENE_MAIN_MENU      = "MainMenu";
    public const string SCENE_DASHBOARD      = "DashboardScene";
    public const string SCENE_GAME           = "Gameplay";
    public const string SCENE_BATTLE         = "Gameplay";

    // ── Singleton ─────────────────────────────────────────────────────────
    public static GameManager instance;

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        ForceLandscapeOrientation();
    }

    /// <summary>Lock the game to the requested left-landscape mobile orientation.</summary>
    public static void ForceLandscapeOrientation()
    {
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }

    // ── Convenience accessors ─────────────────────────────────────────────
    public static PlayerData Data => GameData.instance?.PlayerData;
    public static void Save()     => GameData.instance?.SaveData();

    // ── Battle context (set before loading BattleScene) ───────────────────
    public static BattleManager.BattleMode PendingBattleMode;
    public static int    PendingEnemyCount;
    public static int    PendingEnemyStrength;
    public static int    PendingBattleReward;
    public static string PendingEnemyFirmName;

    // ── Rival-fight chained after a Police Raid ────────────────────────────
    // Set by PlanAwayTripController when heat is max.
    // BattleManager reads these after a PoliceRaid VICTORY to auto-chain.
    public static bool   PendingRivalAfterRaid     = false;
    public static int    PendingRivalCount         = 0;
    public static int    PendingRivalStrength      = 0;
    public static int    PendingRivalReward        = 0;
    public static string PendingRivalFirmName      = "";

    // ── Scene flow ────────────────────────────────────────────────────────

    public void OnContinue()
    {
        ContinueIntoGameplay();
    }

    /// <summary>
    /// Continue button — load the saved campaign straight into gameplay.
    /// </summary>
    public void ContinueIntoGameplay()
    {
        if (!HasSaveData()) return;

        var d = GameData.instance.PlayerData;
        string dest = string.IsNullOrEmpty(d.LastSelectedDestination) ? "East Docks" : d.LastSelectedDestination;
        d.LastSelectedDestination = dest;

        EnterHomeTerritory();
    }

    /// <summary>
    /// New Game — auto-select the first club and open the dashboard.
    /// Team change is available on the dashboard via Change Team.
    /// </summary>
    public void StartNewGameWithDefaultClub(ClubRegistry registry)
    {
        CreateDefaultCampaign(registry);
        LoadScene(SCENE_DASHBOARD, "YOUR FIRM", "Pick a trip and hit the streets.");
    }

    /// <summary>
    /// Home Territory button — always available. With a saved campaign it continues;
    /// without one it silently creates the default-club campaign and drops the player
    /// straight into their home district.
    /// </summary>
    public void EnterHomeTerritoryOrStartNew(ClubRegistry registry)
    {
        if (HasSaveData()) { ContinueIntoGameplay(); return; }
        CreateDefaultCampaign(registry);
        var d = GameData.instance?.PlayerData;
        if (d != null && string.IsNullOrEmpty(d.LastSelectedDestination)) d.LastSelectedDestination = "East Docks";
        GameData.instance?.SaveData();
        CityGameplay.HomeMode = true;
        ReloadHomeTerritory("ENTERING HOME TERRITORY", "Your firm starts here. Take the streets.");
    }

    /// <summary>Creates and saves a fresh campaign using the first registered club.</summary>
    void CreateDefaultCampaign(ClubRegistry registry)
    {
        if (registry == null)
            registry = Resources.Load<ClubRegistry>("ClubRegistry");

        ClubRegistry.ClubEntry c = null;
        if (registry != null && registry.clubs != null && registry.clubs.Count > 0)
            c = registry.clubs[0];

        if (c == null)
        {
            // Hard fallback so New Game never dead-ends.
            GameData.instance.StartNewGame(
                "North City FC", "NCFC", "#D71920", "#FFFFFF",
                "North City Crew", "East Town FC",
                1, 40, 10, 0, 5, 5000);
        }
        else
        {
            GameData.instance.StartNewGame(
                c.clubName, c.clubShortName,
                c.primaryColor, c.secondaryColor,
                c.firmName, c.rivalClubName,
                c.fans, c.strength, c.reputation, c.policeHeat, c.ranking, c.money);
        }

        var d = GameData.instance.PlayerData;
        if (d != null) { d.CurrentLevel = 1; d.PoliceHeat = 7; CampaignMissions.Ensure(d); }
        GameData.instance.SaveData();
    }

    /// <summary>Milestone 3 hook. Current production build starts the firm campaign only.</summary>
    public void StartNewPoliceGame()
    {
        if (!PlayablePoliceEnabled)
        {
            StartNewGameWithDefaultClub(null);
            return;
        }

        GameData.instance.StartNewGame(
            "City Police", "CPD", "#2878D0", "#FFFFFF",
            "CITY POLICE", "City Gangs",
            8, 34, 10, 0, 5, 5000);

        var d = GameData.instance.PlayerData;
        if (d != null)
        {
            d.PlayerFaction = "Police";
            d.CurrentLevel = 1;
            d.PoliceHeat = 0;
            d.RecentEvents = new[] { "Police operations started. Secure contested districts and stop rival gangs." };
            if (d.RecruitedAgents != null)
                for (int i = 0; i < d.RecruitedAgents.Count; i++)
                    if (d.RecruitedAgents[i] != null)
                        d.RecruitedAgents[i].AgentName = $"Officer {i + 1:00}";
        }
        GameData.instance.SaveData();
        LoadScene(SCENE_DASHBOARD, "POLICE COMMAND", "Deploy your unit and secure the city.");
    }

    public static bool IsPolicePlayer =>
        PlayablePoliceEnabled &&
        string.Equals(Data?.PlayerFaction, "Police", System.StringComparison.OrdinalIgnoreCase);

    public void OnClubConfirmed(string clubName, string clubShortName,
                                string primaryColor, string secondaryColor,
                                string firmName, string rivalClubName,
                                int fans, int strength, int reputation,
                                int policeHeat, int ranking, int money)
    {
        GameData.instance.StartNewGame(
            clubName, clubShortName, primaryColor, secondaryColor,
            firmName, rivalClubName,
            fans, strength, reputation, policeHeat, ranking, money);

        var d = GameData.instance.PlayerData;
        if (d != null) { d.CurrentLevel = 1; d.PoliceHeat = 7; CampaignMissions.Ensure(d); }
        GameData.instance.SaveData();

        LoadScene(SCENE_DASHBOARD, "YOUR FIRM", "Pick a trip and hit the streets.");
    }

    public void OnEndMatchDay() => GameData.instance.EndMatchDay();

    public void OnReturnToMainMenu()
    {
        BattleManager.instance?.PersistBattleProgress();
        LoadScene(SCENE_MAIN_MENU);
    }

    // ── Battle entry points ───────────────────────────────────────────────

    /// <summary>
    /// Launch a Rival Fight battle.
    /// Called by PlanAwayTripController after StartTrip.
    /// </summary>
    public void StartRivalFight(int enemyCount, int enemyStrength, int reward, string enemyFirmName = "")
    {
        CityGameplay.HomeMode = false;
        // New away session — refresh mode context so Try Again never reuses a stale HQ snapshot.
        GameData.instance?.RefreshBattleSessionContext(homeMode: false);

        PendingBattleMode     = BattleManager.BattleMode.RivalFight;
        PendingEnemyCount     = enemyCount;
        PendingEnemyStrength  = enemyStrength;
        PendingBattleReward   = reward;
        PendingEnemyFirmName  = enemyFirmName;

        string dest = Data?.LastSelectedDestination;
        string title = string.IsNullOrEmpty(dest) ? "AWAY TRIP" : "HEADING TO " + dest.ToUpper();
        string sub = string.IsNullOrEmpty(enemyFirmName) ? null : "Rival firm: " + enemyFirmName;
        LoadScene(SCENE_BATTLE, title, sub);
    }

    /// <summary>
    /// Triggered when PoliceHeat is at max (10) and the player tries to start an
    /// Away Trip.  Loads Gameplay just like a normal rival fight.
    /// BattleManager.Start() detects PendingBattleMode == PoliceRaid and activates
    /// PoliceRaidGameplayController instead of running the normal 3D battle.
    /// </summary>
    public void StartPoliceRaid()
    {
        CityGameplay.HomeMode=false;
        GameData.instance?.RefreshBattleSessionContext(homeMode: false);
        // Derive officer params from registry (falls back to hardcoded if manager missing)
        int heat = GameData.instance?.PlayerData?.PoliceHeat ?? 10;
        (int policeCount, int policeStrength) = PoliceManager.instance != null
            ? PoliceManager.instance.GetPoliceParams(heat)
            : (10, 28); // safe fallback

        // Stash battle context — BattleManager reads these after scene loads
        PendingBattleMode    = BattleManager.BattleMode.PoliceRaid;
        PendingEnemyCount    = policeCount;
        PendingEnemyStrength = policeStrength;
        PendingBattleReward  = 0;
        PendingEnemyFirmName = "POLICE";

        // Load Gameplay — BattleManager.Start() will detect PoliceRaid mode
        // and activate PoliceRaidGameplayController instead of spawning agents.
        LoadScene(SCENE_BATTLE, "POLICE RAID", "The filth are onto your firm.");
    }

    /// <summary>
    /// Called by PoliceRaidGameplayController after a Police Raid VICTORY.
    /// Launches the originally planned rival fight which was deferred by the raid.
    /// </summary>
    public void ChainRivalFightAfterRaid()
    {
        if (!PendingRivalAfterRaid)
        {
            Debug.LogWarning("[GameManager] ChainRivalFightAfterRaid called but PendingRivalAfterRaid is false.");
            ReturnToDashboard();
            return;
        }

        // Clear the flag so it doesn't fire again on subsequent battles
        PendingRivalAfterRaid = false;

        StartRivalFight(PendingRivalCount, PendingRivalStrength, PendingRivalReward, PendingRivalFirmName);
    }

    /// <summary>Return to main dashboard after a battle.</summary>
    public void ReturnToDashboard() => EnterHomeTerritory();

    public void EnterHomeTerritory()
    {
        BattleManager.instance?.PersistBattleProgress();
        ReloadHomeTerritory("RETURNING TO HQ", "Dust yourself off at headquarters.");
    }

    /// <summary>
    /// Reload the session the player just died in. Pass the live HomeMode at defeat —
    /// do not trust a stale BattleStartHomeMode from an earlier HQ visit.
    /// City ops / micro-management ledger is left intact on the save.
    /// </summary>
    public void RetryLastBattleSession(bool wasHome)
    {
        var d = Data;
        if (d != null)
        {
            d.BattleStartHomeMode = wasHome;
            if (d.BattleStartLevel <= 0) d.BattleStartLevel = Mathf.Clamp(d.CurrentLevel, 1, 5);
            GameData.instance?.SaveData();
        }

        if (wasHome)
        {
            ReloadHomeTerritory("TRY AGAIN", "Loading your last save…");
            return;
        }

        CityGameplay.HomeMode = false;
        PendingBattleMode = BattleManager.BattleMode.RivalFight;
        if (PendingEnemyCount <= 0) PendingEnemyCount = 6;
        if (PendingEnemyStrength <= 0) PendingEnemyStrength = 30;
        string dest = d?.LastSelectedDestination;
        string sub = string.IsNullOrEmpty(PendingEnemyFirmName)
            ? (string.IsNullOrEmpty(dest) ? "Loading your last save…" : dest.ToUpperInvariant())
            : PendingEnemyFirmName;
        LoadScene(SCENE_BATTLE, "TRY AGAIN", sub);
    }

    void ReloadHomeTerritory(string title, string subtitle)
    {
        CityGameplay.HomeMode = true;
        GameData.instance?.RefreshBattleSessionContext(homeMode: true);
        PendingBattleMode = BattleManager.BattleMode.RivalFight;
        PendingEnemyCount = 6;
        PendingEnemyStrength = 20;
        PendingBattleReward = 0;
        PendingEnemyFirmName = "Rival Visitors";
        LoadScene(SCENE_GAME, title, subtitle);
    }

    // ── Utility ───────────────────────────────────────────────────────────
    public static bool HasSaveData()
    {
        return SaveDataInLocal.HasSavedCampaign()
               && GameData.instance != null
               && GameData.instance.PlayerData != null
               && GameData.instance.PlayerData.MatchDay > 0;
    }

    /// <summary>All scene changes route through the faded loading transition.</summary>
    public static void LoadScene(string sceneName) => LoadScene(sceneName, null, null);

    /// <summary>Scene change with a themed loading title/subtitle.</summary>
    public static void LoadScene(string sceneName, string title, string subtitle)
    {
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.Transition(sceneName, title, subtitle);
        else
            SceneManager.LoadScene(sceneName);
    }

    void Update() { }
}
