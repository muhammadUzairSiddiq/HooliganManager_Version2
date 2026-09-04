using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating world-space health bar rendered above an agent.
/// Attach to the health bar child GameObject of each agent prefab.
/// The Canvas component on this GO should be set to World Space.
///
/// Inspector: assign fillImage (the coloured fill bar) and set isEnemy flag.
/// </summary>
public class AgentHealthBar : MonoBehaviour
{
    [Header("Bar Fill")]
    public Image fillImage;

    [Header("Colors")]
    public Color playerColor = new Color(0.85f, 0.10f, 0.10f); // red
    public Color enemyColor  = new Color(0.10f, 0.45f, 0.85f); // blue
    public Color lowHpColor  = new Color(1.00f, 0.55f, 0.00f); // orange warning

    [Header("Is this an enemy bar?")]
    public bool isEnemy = false;

    // ── Internal ──────────────────────────────────────────────────────────
    private Camera _mainCam;
    private Transform _target;   // the agent transform this bar follows

    public void Initialise(Transform agentTransform, bool enemy)
    {
        _target  = agentTransform;
        isEnemy  = enemy;
        _mainCam = Camera.main;

        if (fillImage)
            fillImage.color = isEnemy ? enemyColor : playerColor;
    }

    public void SetHealth(float current, float max)
    {
        if (fillImage == null) return;
        float ratio = Mathf.Clamp01(current / max);
        fillImage.fillAmount = ratio;

        // Tint orange when below 30%
        if (!isEnemy)
            fillImage.color = ratio < 0.30f ? lowHpColor : playerColor;
    }

    void LateUpdate()
    {
        // Always face the camera (billboard)
        if (_mainCam != null)
            transform.rotation = Quaternion.LookRotation(
                transform.position - _mainCam.transform.position);
    }
}
