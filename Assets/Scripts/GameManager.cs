using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central scene-navigation manager.
/// GameData handles all save/load; GameManager handles screen flow.
/// </summary>
public class GameManager : MonoBehaviour
{
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

    /// <summary>Support both landscape rotations across menus and gameplay.</summary>
    public static void ForceLandscapeOrientation()
    {
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.orientation = ScreenOrientation.AutoRotation;
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

        // Resume at the player's current campaign level with a standard rival fight.
        int enemyCount = 6;
        int enemyStr = 28;
        int reward = 3500;
        string firm = d.RivalBots != null && d.RivalBots.Count > 0
            ? d.RivalBots[0].firmName
            : "Rival Firm";

        EnterHomeTerritory();
    }

    /// <summary>
    /// New Game — auto-select the first club and open the dashboard.
    /// Team change is available on the dashboard via Change Team.
    /// </summary>
    public void StartNewGameWithDefaultClub(ClubRegistry registry)
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
        if (d != null) d.CurrentLevel = 1;
        GameData.instance.SaveData();

        LoadScene(SCENE_DASHBOARD, "YOUR FIRM", "Pick a trip and hit the streets.");
    }

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
        if (d != null) d.CurrentLevel = 1;
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
        CityGameplay.HomeMode=false;
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
        CityGameplay.HomeMode=true;
        PendingBattleMode=BattleManager.BattleMode.RivalFight;
        PendingEnemyCount=6; PendingEnemyStrength=20; PendingBattleReward=0;
        PendingEnemyFirmName="Rival Visitors";
        LoadScene(SCENE_GAME,"HOME TERRITORY","Returning to headquarters");
    }

    // ── Utility ───────────────────────────────────────────────────────────
    public static bool HasSaveData()
    {
        return GameData.instance != null
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
