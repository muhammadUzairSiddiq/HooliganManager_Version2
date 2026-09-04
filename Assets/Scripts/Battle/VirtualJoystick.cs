using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Virtual d-pad joystick for the bottom-left of the battle HUD.
/// Supports both mouse drag (editor) and touch input.
///
/// Attach to the joystick background Image GameObject.
/// Assign joystickKnob (the inner circle Image RectTransform).
/// </summary>
public class VirtualJoystick : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    public RectTransform joystickKnob;     // the inner draggable circle

    [Header("Settings")]
    [Tooltip("Max pixels the knob can move from centre")]
    public float handleRange = 60f;

    [Tooltip("Deadzone — direction ignored below this normalised magnitude")]
    public float deadZone = 0.1f;

    public enum JoystickControlMode { CameraOnly, AgentsOnly, Both }

    [Header("Control Mode")]
    [Tooltip("What this joystick controls: Camera, Agents, or Both.")]
    public JoystickControlMode controlMode = JoystickControlMode.CameraOnly;

    // ── Singleton ─────────────────────────────────────────────────────────
    public static VirtualJoystick instance;

    // ── Output ────────────────────────────────────────────────────────────
    /// <summary>Normalised input direction [-1,1] on X and Y.</summary>
    public Vector2 Direction { get; private set; }

    public bool IsActive { get; private set; }

    // ── Internal ──────────────────────────────────────────────────────────
    private RectTransform _background;
    private Vector2       _origin;
    private Canvas        _canvas;

    void Awake()
    {
        instance = this;
        _background = GetComponent<RectTransform>();
        _canvas     = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData data)
    {
        IsActive = true;
        _origin  = RectPosition(data.position);
        OnDrag(data);
    }

    public void OnDrag(PointerEventData data)
    {
        Vector2 pos    = RectPosition(data.position);
        Vector2 delta  = pos - _origin;
        Vector2 clamped = Vector2.ClampMagnitude(delta, handleRange);

        Direction = (clamped.magnitude / handleRange) > deadZone
            ? clamped / handleRange
            : Vector2.zero;

        if (joystickKnob != null)
            joystickKnob.anchoredPosition = clamped;

        // Drive selected agents if controlMode allows it
        if (controlMode == JoystickControlMode.AgentsOnly || controlMode == JoystickControlMode.Both)
        {
            AgentSelectionManager.instance?.ApplyJoystickToSelected(Direction);
        }
    }

    public void OnPointerUp(PointerEventData data)
    {
        Direction  = Vector2.zero;
        IsActive   = false;

        if (joystickKnob != null)
            joystickKnob.anchoredPosition = Vector2.zero;

        // Stop agents if controlMode allows it
        if (controlMode == JoystickControlMode.AgentsOnly || controlMode == JoystickControlMode.Both)
        {
            AgentSelectionManager.instance?.ApplyJoystickToSelected(Vector2.zero);
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────
    private Vector2 RectPosition(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _background, screenPos,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out Vector2 localPoint);
        return localPoint;
    }
}
