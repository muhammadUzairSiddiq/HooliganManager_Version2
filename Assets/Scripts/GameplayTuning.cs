using UnityEngine;

[CreateAssetMenu(menuName = "Hooligan Manager/Gameplay Tuning")]
public sealed class GameplayTuning : ScriptableObject
{
    static GameplayTuning current;
    public static GameplayTuning Current => current ? current : (current = Resources.Load<GameplayTuning>("GameplayTuning") ?? CreateInstance<GameplayTuning>());
    [Header("Characters - all factions and civilians")]
    [Range(.5f, 3f)] public float characterScale = 1.5f;
    [Min(.7f)] public float attackInterval = 1.35f;
    [Range(.05f, .6f)] public float impactDelay = .28f;
    [Min(.1f)] public float playerDamageMultiplier = .85f;
    [Min(1)] public float turnSpeed = 420;
    [Range(.05f, .5f)] public float animationBlend = .18f;
    public float[] enemyHealth = { .85f, .95f, 1.05f, 1.15f, 1.25f };
    public float[] enemyDamage = { .75f, .85f, .95f, 1.05f, 1.15f };
    [Header("Camera")]
    [Range(18, 100)] public float explorationHeight = 30;
    [Range(18, 80)] public float combatHeight = 24;
    [Range(45, 80)] public float cameraPitch = 54;
    [Range(40, 85)] public float cameraFov = 54;
    public bool focusFights = true;
    [Header("Territory and recovery")]
    [Min(1)] public float captureSeconds = 8;
    [Min(1)] public int recoveryCost = 200;
    [Min(1)] public int healthPackageCost = 750;
    [Min(1)] public int powerPackageCost = 1000;
    [Range(1, 10)] public int maxPowerPackages = 3;
    [Min(.1f)] public float packageStrength = 1.5f;
    [Header("Android")]
    [Range(30, 60)] public int targetFps = 60;
    [Range(.5f, 1)] public float renderScale = .8f;
    [Range(10, 100)] public float shadowDistance = 35;
    [Range(2, 20)] public int maxPedestrians = 6;
    public float LevelValue(float[] values, int level) => values == null || values.Length == 0 ? 1 : Mathf.Max(.1f, values[Mathf.Clamp(level - 1, 0, values.Length - 1)]);
    public static void ScaleModel(Transform model) { if (model) model.localScale = Vector3.one * Current.characterScale; }
}
