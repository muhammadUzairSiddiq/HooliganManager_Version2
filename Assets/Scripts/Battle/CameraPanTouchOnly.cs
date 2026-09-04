using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.OnScreen;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Smooth isometric RTS camera for Android.
///
///   • One-finger drag  → pan the city (world-locked, very smooth)
///   • Pinch            → zoom in/out
///   • Double-tap       → snap camera to the player firm (Google Maps style)
///
/// Rotation is locked to a fixed isometric angle. No physics collision —
/// collision was causing camera shake + yellow full-screen flashes when the
/// camera clipped yellow sidewalks.
/// </summary>
public class CameraPanTouchOnly : MonoBehaviour
{
    public static CameraPanTouchOnly Instance { get; private set; }

    [Header("Isometric Lock")]
    [SerializeField] private Vector3 isometricEuler = new Vector3(45f, 45f, 0f);
    [SerializeField] private bool lockRotationEveryFrame = true;

    [Header("Pan")]
    [SerializeField] private float dragPanSpeed = 1.0f;
    [SerializeField] private float keyboardPanSpeed = 22f;
    [SerializeField] private bool invertDrag = false;

    [Header("Pan Bounds")]
    [SerializeField] private float panMinX = -250f;
    [SerializeField] private float panMaxX = 250f;
    [SerializeField] private float panMinZ = -250f;
    [SerializeField] private float panMaxZ = 250f;

    [Header("Zoom (perspective = height, ortho = size)")]
    [SerializeField] private float pinchZoomSpeed = 0.05f;
    [SerializeField] private float scrollZoomSpeed = 8f;
    [SerializeField] private float camMinHeight = 18f;
    [SerializeField] private float camMaxHeight = 45f;
    [SerializeField] private float orthoMin = 7f;
    [SerializeField] private float orthoMax = 20f;
    [SerializeField] private float defaultHeight = 24f;

    [Header("Smoothing (higher = smoother / less shake)")]
    [SerializeField] private float moveSmoothTime = 0.14f;
    [SerializeField] private float recenterSmoothTime = 0.22f;

    [Header("Double-tap")]
    [SerializeField] private float doubleTapWindow = 0.32f;
    [SerializeField] private float tapMaxMovePixels = 22f;

    [Header("Ground")]
    [SerializeField] private float groundY = 0f;

    [Header("Legacy Joystick")]
    [SerializeField] private bool hideLegacyJoystick = true;
    [SerializeField] private GameObject joystickVisualRoot;
    [SerializeField] private OnScreenStick joystick;

    private Camera _camera;
    private Vector3 _targetPosition;
    private float _targetOrthoSize;
    private Vector3 _velocity;
    private float _orthoVelocity;
    private float _smoothTime;

    // Double-tap tracking
    private float _lastTapTime = -10f;
    private Vector2 _lastTapPos;
    private int _fingerDownCount;
    private bool _movedThisGesture;
    private Vector2 _gestureStartPos;

    void Awake()
    {
        Instance = this;
        _camera = GetComponent<Camera>();
        if (_camera == null) _camera = Camera.main;
        _smoothTime = moveSmoothTime;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (BattleManager.instance != null)
            BattleManager.instance.OnMapSpawnedIn -= UpdateBoundsFromMap;
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        LockIsometric();
        _targetPosition = transform.position;
        // Keep a safe height so we never look into sidewalk meshes.
        if (_targetPosition.y < camMinHeight)
            _targetPosition.y = Mathf.Max(defaultHeight, camMinHeight);
        if (_camera != null && _camera.orthographic)
            _targetOrthoSize = _camera.orthographicSize;
        UpdateBoundsFromMap();
    }

    private void Start()
    {
        if (BattleManager.instance != null)
            BattleManager.instance.OnMapSpawnedIn += UpdateBoundsFromMap;
        else
            UpdateBoundsFromMap();

        if (hideLegacyJoystick) HideAllJoysticks();
        CenterCameraButton.EnsureExists();
        LockIsometric();

        // Soft start height for mobile readability.
        if (!_camera.orthographic && transform.position.y > camMaxHeight * 0.9f)
            ForceHeight(defaultHeight);
    }

    private void LockIsometric()
    {
        // Preserve authored yaw if already isometric-ish; otherwise apply default.
        Vector3 e = transform.eulerAngles;
        float pitch = isometricEuler.x;
        float yaw = (Mathf.Abs(e.x - 45f) < 20f) ? e.y : isometricEuler.y;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void UpdateBoundsFromMap()
    {
        var bounds = FindAnyObjectByType<Arikan.MiniMapBounds>(FindObjectsInactive.Include);
        if (bounds != null)
        {
            var rect = bounds.GetWorldRect();
            panMinX = rect.min.x; panMaxX = rect.max.x;
            panMinZ = rect.min.z; panMaxZ = rect.max.z;
        }
    }

    void Update()
    {
        // Fullscreen town map owns input — do not move the main camera.
        if (LiveMiniMap.IsExpanded) return;

        if (lockRotationEveryFrame) LockIsometric();

        HandleTouch();
        HandleEditorInput();

        // Smooth toward target — NO collision. Collision caused shake + yellow flash.
        Vector3 desired = _targetPosition;
        desired.y = Mathf.Clamp(desired.y, camMinHeight, camMaxHeight);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime);

        if (_camera != null && _camera.orthographic)
            _camera.orthographicSize = Mathf.SmoothDamp(
                _camera.orthographicSize, _targetOrthoSize, ref _orthoVelocity, _smoothTime);

        // After a recenter finishes gliding, return to normal smooth time.
        if (_smoothTime > moveSmoothTime && _velocity.sqrMagnitude < 0.01f)
            _smoothTime = moveSmoothTime;
    }

    private bool _uiGesture; // finger started on HUD — don't pan/zoom

    // ── Touch ────────────────────────────────────────────────────────────
    private void HandleTouch()
    {
        var touches = Touch.activeTouches;
        int count = touches.Count;

        // Track gesture start for tap vs drag.
        if (count == 1)
        {
            var t = touches[0];
            if (t.phase == TouchPhase.Began)
            {
                _fingerDownCount = 1;
                _movedThisGesture = false;
                _gestureStartPos = t.screenPosition;
                _uiGesture = IsPointerOverUI(t.screenPosition);
            }
            else if (_uiGesture)
            {
                // Let ScrollRect / buttons own this finger.
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _fingerDownCount = 0;
                    _uiGesture = false;
                }
                return;
            }
            else if (t.phase == TouchPhase.Moved)
            {
                if (Vector2.Distance(t.screenPosition, _gestureStartPos) > tapMaxMovePixels)
                    _movedThisGesture = true;

                if (_movedThisGesture)
                {
                    Vector2 curr = t.screenPosition;
                    Vector2 prev = t.screenPosition - t.delta;
                    DragPan(prev, curr);
                }
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                // Tap (not a drag) — check double-tap for recenter.
                if (!_movedThisGesture &&
                    Vector2.Distance(t.screenPosition, _gestureStartPos) <= tapMaxMovePixels)
                {
                    TryDoubleTap(t.screenPosition);
                }
                _fingerDownCount = 0;
                _uiGesture = false;
            }
        }
        else if (count == 2)
        {
            if (_uiGesture) return;
            _movedThisGesture = true; // pinch cancels tap
            var t1 = touches[0];
            var t2 = touches[1];
            float prevDist = Vector2.Distance(t1.screenPosition - t1.delta, t2.screenPosition - t2.delta);
            float currDist = Vector2.Distance(t1.screenPosition, t2.screenPosition);
            float deltaDist = currDist - prevDist;
            if (Mathf.Abs(deltaDist) > 0.01f)
                Zoom(deltaDist * pinchZoomSpeed);
        }
    }

    private void TryDoubleTap(Vector2 pos)
    {
        float now = Time.unscaledTime;
        if (now - _lastTapTime <= doubleTapWindow &&
            Vector2.Distance(pos, _lastTapPos) <= tapMaxMovePixels * 2.5f)
        {
            _lastTapTime = -10f;
            // Google Maps style: glide to player.
            _smoothTime = recenterSmoothTime;
            CenterOnSelection();
            AgentSelectionManager.NotifyDoubleTapConsumed();
        }
        else
        {
            _lastTapTime = now;
            _lastTapPos = pos;
        }
    }

    private void HandleEditorInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += Vector3.forward;
            if (keyboard.sKey.isPressed) move += Vector3.back;
            if (keyboard.aKey.isPressed) move += Vector3.left;
            if (keyboard.dKey.isPressed) move += Vector3.right;
            if (move != Vector3.zero)
            {
                Vector3 fwd = transform.forward; fwd.y = 0; fwd.Normalize();
                Vector3 right = transform.right; right.y = 0; right.Normalize();
                PanBy((right * move.x + fwd * move.z) * keyboardPanSpeed * Time.deltaTime * ZoomFactor());
            }
            if (keyboard.upArrowKey.isPressed) Zoom(scrollZoomSpeed * Time.deltaTime);
            if (keyboard.downArrowKey.isPressed) Zoom(-scrollZoomSpeed * Time.deltaTime);
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                _smoothTime = recenterSmoothTime;
                CenterOnSelection();
            }
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) Zoom(scroll * scrollZoomSpeed * 0.01f);

            // Editor: middle-mouse / right-mouse drag pan
            if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0.01f)
                {
                    Vector2 curr = mouse.position.ReadValue();
                    DragPan(curr - delta, curr);
                }
            }

            // Editor double-click recenter
            if (mouse.leftButton.wasPressedThisFrame)
                TryDoubleTap(mouse.position.ReadValue());
        }
#endif
    }

    private void DragPan(Vector2 prevScreen, Vector2 currScreen)
    {
        if (!GroundPointUnderScreen(prevScreen, out Vector3 worldPrev)) return;
        if (!GroundPointUnderScreen(currScreen, out Vector3 worldCurr)) return;

        Vector3 delta = (worldPrev - worldCurr) * dragPanSpeed;
        if (invertDrag) delta = -delta;
        delta.y = 0f;
        PanBy(delta);
    }

    private void PanBy(Vector3 worldDelta)
    {
        _targetPosition += worldDelta;
        ClampXZ();
    }

    private void ClampXZ()
    {
        _targetPosition.x = Mathf.Clamp(_targetPosition.x, panMinX, panMaxX);
        _targetPosition.z = Mathf.Clamp(_targetPosition.z, panMinZ, panMaxZ);
    }

    private void Zoom(float amount)
    {
        if (_camera != null && _camera.orthographic)
        {
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize - amount, orthoMin, orthoMax);
            return;
        }

        Vector3 focusBefore = FocusFromTarget();
        _targetPosition.y = Mathf.Clamp(_targetPosition.y - amount, camMinHeight, camMaxHeight);
        Vector3 focusAfter = FocusFromTarget();
        Vector3 shift = focusBefore - focusAfter;
        shift.y = 0f;
        _targetPosition += shift;
        ClampXZ();
    }

    private float ZoomFactor()
    {
        float denom = Mathf.Max(0.001f, camMinHeight);
        return Mathf.Clamp(_targetPosition.y / denom, 1f, 3.5f);
    }

    public void FocusOn(Vector3 worldPoint)
    {
        Vector3 currentFocus = FocusFromTarget();
        Vector3 shift = worldPoint - currentFocus;
        shift.y = 0f;
        _targetPosition += shift;
        ClampXZ();
    }

    public void CenterOnSelection()
    {
        if (TryGetSelectionCentroid(out Vector3 centroid))
            FocusOn(centroid);
    }

    private bool TryGetSelectionCentroid(out Vector3 centroid)
    {
        centroid = Vector3.zero;
        int count = 0;

        var sel = AgentSelectionManager.instance != null ? AgentSelectionManager.instance.SelectedAgents : null;
        if (sel != null)
        {
            foreach (var a in sel)
                if (a != null && a.IsAlive) { centroid += a.transform.position; count++; }
        }

        if (count == 0 && BattleManager.instance != null)
        {
            foreach (var a in BattleManager.instance.PlayerAgents)
                if (a != null && a.IsAlive) { centroid += a.transform.position; count++; }
        }

        if (count == 0) return false;
        centroid /= count;
        return true;
    }

    private bool GroundPointUnderScreen(Vector2 screenPos, out Vector3 point)
    {
        point = Vector3.zero;
        if (_camera == null) return false;
        Ray ray = _camera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }
        return false;
    }

    private Vector3 FocusFromTarget()
    {
        Ray ray = new Ray(_targetPosition, transform.forward);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);
        return new Vector3(_targetPosition.x, groundY, _targetPosition.z);
    }

    public void ForceHeight(float y)
    {
        y = Mathf.Clamp(y, camMinHeight, camMaxHeight);
        _targetPosition = transform.position;
        _targetPosition.y = y;
        transform.position = _targetPosition;
        _velocity = Vector3.zero;
    }

    public void ForceOrthoSize(float size)
    {
        _targetOrthoSize = Mathf.Clamp(size, orthoMin, orthoMax);
        if (_camera != null && _camera.orthographic)
            _camera.orthographicSize = _targetOrthoSize;
        _orthoVelocity = 0f;
    }

    private void HideAllJoysticks()
    {
        if (joystickVisualRoot != null) joystickVisualRoot.SetActive(false);
        if (joystick == null) joystick = FindObjectOfType<OnScreenStick>(true);
        if (joystick != null)
        {
            Transform root = joystick.transform;
            if (root.parent != null) root = root.parent;
            root.gameObject.SetActive(false);
        }
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            string n = t.name.ToLowerInvariant();
            if (n.Contains("joystick") || n == "dpad" || n.Contains("onscreenstick"))
                t.gameObject.SetActive(false);
        }
    }

    private static bool IsPointerOverUI(Vector2 screenPos)
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        var eventData = new UnityEngine.EventSystems.PointerEventData(es) { position = screenPos };
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        es.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
