using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Handles tap-to-select and tap-to-deselect for player agents.
/// On mobile: single tap → toggle selection on tapped agent.
/// Tap on empty space → deselect all.
/// Updates BattleUIController with the current selected list.
///
/// Attach to a singleton GameObject in BattleScene.
/// Uses the new Unity Input System (EnhancedTouch) — no legacy Input polling.
/// </summary>
public class AgentSelectionManager : MonoBehaviour
{
    public static AgentSelectionManager instance;

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>
    /// Fired whenever the selection list changes. Passes the current selected list.
    /// Subscribe in PoliceRaidGameplayController, BattleUIController, etc.
    /// </summary>
    public event System.Action<List<AgentController>> OnSelectionChanged;

    // ── State ─────────────────────────────────────────────────────────────
    private readonly List<AgentController> _selected = new List<AgentController>();
    public IReadOnlyList<AgentController> SelectedAgents => _selected;

    // ── Internal ──────────────────────────────────────────────────────────
    private Camera _cam;
    private Vector2 _mouseDown;
    private bool _mouseTap, _mouseDragged;

    [Header("Tap vs Drag")]
    [Tooltip("Max finger travel (px) still counted as a tap. Beyond this it's a camera drag and is ignored.")]
    [SerializeField] private float dragThresholdPixels = 18f;

    // A gesture that ever had 2+ fingers (pinch/zoom) or dragged far is not a tap.
    private bool _multiTouchThisGesture;
    private bool _touchDragged, _touchStartedOnUI;
    private int _fingersDown;

    // Double-tap: skip the unit-order tap when camera consumed a recenter double-tap.
    private static bool _doubleTapConsumed;
    private float _pendingTapTime = -1f;
    private Vector2 _pendingTapPos;
    private const float DoubleTapDelay = 0.28f;

    public static void NotifyDoubleTapConsumed()
    {
        _doubleTapConsumed = true;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        _cam = Camera.main;
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += OnFingerDown;
        Touch.onFingerUp += OnFingerUp;
    }

    void OnDisable()
    {
        _pendingTapTime=-1;_mouseTap=false;_fingersDown=0;
        Touch.onFingerDown -= OnFingerDown;
        Touch.onFingerUp -= OnFingerUp;
    }

    void Update()
    {
        var mouse=UnityEngine.InputSystem.Mouse.current;
        if (mouse!=null && Touch.activeTouches.Count==0)
        {
            var position=mouse.position.ReadValue();
            if(mouse.leftButton.wasPressedThisFrame)
            { _mouseDown=position; _mouseTap=!IsPointerOverUI(position); _mouseDragged=false; }
            if(mouse.leftButton.isPressed)
                _mouseDragged |= Vector2.Distance(position,_mouseDown)>dragThresholdPixels;
            if(mouse.leftButton.wasReleasedThisFrame && _mouseTap && !_mouseDragged)
                QueueTap(position);
        }
        foreach(var touch in Touch.activeTouches)
            if(Vector2.Distance(touch.screenPosition,touch.startScreenPosition)>dragThresholdPixels)_touchDragged=true;
        // Fire deferred single-tap only if a double-tap never arrived.
        if (_pendingTapTime > 0f && Time.unscaledTime >= _pendingTapTime)
        {
            _pendingTapTime = -1f;
            if (!_doubleTapConsumed)
                HandleTap(_pendingTapPos);
            _doubleTapConsumed = false;
        }
    }

    private void OnFingerDown(Finger finger)
    {
        if(_fingersDown==0){_touchDragged=false;_touchStartedOnUI=IsPointerOverUI(finger.currentTouch.screenPosition);}
        _fingersDown++;
        if (_fingersDown >= 2) _multiTouchThisGesture = true;
    }

    private void OnFingerUp(Finger finger)
    {
        var t = finger.currentTouch;
        float dpiThreshold = Mathf.Max(dragThresholdPixels, Screen.height * 0.02f);
        bool wasTap = !_multiTouchThisGesture && !_touchDragged && !_touchStartedOnUI &&
                      Vector2.Distance(t.screenPosition, t.startScreenPosition) <= dpiThreshold;

        _fingersDown = Mathf.Max(0, _fingersDown - 1);
        if (_fingersDown == 0) _multiTouchThisGesture = false;

        if (!wasTap) return;

        QueueTap(t.screenPosition);
    }

    private void QueueTap(Vector2 position)
    {
        // Resolve both mouse and touch taps here, independent of camera Update order.
        if (_pendingTapTime > 0f && Time.unscaledTime < _pendingTapTime && Vector2.Distance(position,_pendingTapPos)<55)
        {
            _pendingTapTime = -1f;
            _doubleTapConsumed = true;
            CameraPanTouchOnly.Instance?.CenterOnSelection();
            return;
        }

        _doubleTapConsumed = false;
        _pendingTapPos = position;
        _pendingTapTime = Time.unscaledTime + 0.34f;
    }

    // ── Tap handling ──────────────────────────────────────────────────────

    private void HandleTap(Vector2 screenPos)
    {
        if (LiveMiniMap.IsExpanded || Time.timeScale == 0 || !_cam) return;
        // Ignore taps on UI (joystick, buttons)
        if (IsPointerOverUI(screenPos))
            return;

        // 3D raycast from camera through tap point
        Ray ray = _cam.ScreenPointToRay(screenPos);
        
        // ── 1. Check if we hit an Agent (Player or Enemy) ──
        if (Physics.Raycast(ray, out RaycastHit hit, 2500f, LayerMask.GetMask("Agent")))
        {
            var agent = hit.collider.GetComponent<AgentController>();
            if (agent == null)
                agent = hit.collider.GetComponentInParent<AgentController>();

            if (agent != null && agent.IsAlive)
            {
                ToggleSelect(agent);
                return;
            }

            var enemy = hit.collider.GetComponent<EnemyController>();
            if (enemy == null)
                enemy = hit.collider.GetComponentInParent<EnemyController>();

            if (enemy != null && enemy.IsAlive)
            {
                if (!enemy.isHostile)
                {
                    // Tapped a passive rival gang member. Open recruiting options!
                    int playerUnits = BattleManager.instance.PlayerAgents.Count(a => a.IsAlive);
                    string gangName = enemy.firmName;
                    int enemyUnits = BattleManager.instance.EnemyAgents.Count(e => e != null && e.IsAlive && e.firmName == gangName);

                    float baseChance = 0.4f;
                    float ratio = (float)playerUnits / Mathf.Max(1, enemyUnits);
                    float chance = baseChance * ratio;
                    if (GameData.instance?.PlayerData != null)
                    {
                        chance *= (float)GameData.instance.PlayerData.Strength / 40f;
                    }
                    chance = Mathf.Clamp(chance, 0.1f, 0.9f);

                    BattleUIController.instance?.ShowGangInteractionPanel(
                        gangName,
                        playerUnits,
                        enemyUnits,
                        chance,
                        onAttack: () => {
                            BattleUIController.instance?.ShowGangResponsePanel(
                                gangName,
                                "Oh, do you really want to fight?!",
                                "FIGHT!",
                                () => BattleManager.instance?.AttackGang(gangName)
                            );
                        },
                        onRecruit: () => {
                            bool success = UnityEngine.Random.value <= chance;
                            if (success)
                            {
                                BattleUIController.instance?.ShowGangResponsePanel(
                                    gangName,
                                    "Yeah, we will join your gang!",
                                    "WELCOME!",
                                    () => BattleManager.instance?.RecruitGang(gangName, true)
                                );
                            }
                            else
                            {
                                BattleUIController.instance?.ShowGangResponsePanel(
                                    gangName,
                                    "Nah, your team is too weak!",
                                    "FIGHT!",
                                    () => BattleManager.instance?.RecruitGang(gangName, false)
                                );
                            }
                        },
                        onClose: () => {
                        }
                    );
                }
                else
                {
                    // If we have selected agents, order them to attack this target
                    if (_selected.Count > 0)
                    {
                        foreach (var a in _selected)
                        {
                            if (a != null && a.IsAlive)
                            {
                                a.CommandAttackTarget(enemy);
                            }
                        }
                    }
                }
                return;
            }
        }

        // ── 2. Tapped empty space (Ground movement) ──
        Vector3 groundPoint = Vector3.zero;
        bool groundHit = false;

        // Physics raycast against Default layer (which represents ground/environment)
        if (Physics.Raycast(ray, out RaycastHit groundHitInfo, 2500f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
        {
            groundPoint = groundHitInfo.point;
            groundHit = true;
        }
        else
        {
            // Fallback: mathematical plane at Y = 0
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                groundPoint = ray.GetPoint(enter);
                groundHit = true;
            }
        }

        if (groundHit && UnityEngine.AI.NavMesh.SamplePosition(groundPoint,out var walkHit,3f,UnityEngine.AI.NavMesh.AllAreas))
        {
            groundPoint=walkHit.position;
            // If nothing is currently selected, auto-select all alive units to make movement default
            if (_selected.Count == 0)
            {
                SelectAll();
            }

            if (_selected.Count > 0)
            {
                CommandSelectedMoveTo(groundPoint);
            }
        }
    }

    // ── Selection API ─────────────────────────────────────────────────────

    public void ToggleSelect(AgentController agent)
    {
        if (_selected.Contains(agent))
            Deselect(agent);
        else
            Select(agent);
    }

    public void Select(AgentController agent)
    {
        if (!_selected.Contains(agent))
        {
            _selected.Add(agent);
            agent.SetSelected(true);
        }
        NotifyUI();
    }

    public void Deselect(AgentController agent)
    {
        _selected.Remove(agent);
        agent.SetSelected(false);
        NotifyUI();
    }

    public void DeselectAll()
    {
        foreach (var a in _selected) if(a)a.SetSelected(false);
        _selected.Clear();
        NotifyUI();
    }

    /// <summary>Select all currently alive player agents.</summary>
    public void SelectAll()
    {
        DeselectAll();
        if (BattleManager.instance == null) return;
        foreach (var a in BattleManager.instance.PlayerAgents)
            if (a != null && a.IsAlive)
                Select(a);
    }

    // ── Commands — forwarded to all selected agents ───────────────────────

    public void CommandSelectedAttack()
    {
        foreach (var a in _selected)
            if (a != null && a.IsAlive) a.CommandAttack();
    }

    public void CommandSelectedRetreat()
    {
        CameraPanTouchOnly.Instance?.FollowSelection();
        foreach (var a in _selected)
            if (a != null && a.IsAlive) a.CommandRetreat();
    }

    public void CommandSelectedMoveTo(Vector3 worldPoint)
    {
        var leader=_selected.Find(a=>a && a.IsAlive);
        if(!leader)return;
        var path=new UnityEngine.AI.NavMeshPath();
        if(!UnityEngine.AI.NavMesh.CalculatePath(leader.transform.position,worldPoint,UnityEngine.AI.NavMesh.AllAreas,path) || path.status!=UnityEngine.AI.NavMeshPathStatus.PathComplete)
        {CityGameplay.Instance?.PostEvent("DESTINATION BLOCKED - CHOOSE A STREET APPROACH");return;}
        CameraPanTouchOnly.Instance?.FollowSelection();
        Debug.DrawRay(worldPoint, Vector3.up*10, Color.green, 3f);
        // Spread agents in a formation row along X around the target point
        for (int i = 0; i < _selected.Count; i++)
        {
            if (_selected[i] == null || !_selected[i].IsAlive) continue;
            float offset = (i - _selected.Count / 2f) * 0.6f;
            Vector3 target = worldPoint + new Vector3(offset, 0, 0);
            _selected[i].CommandMoveTo(target);
        }
    }

    public void ApplyJoystickToSelected(Vector2 dir)
    {
        foreach (var a in _selected)
            if (a != null && a.IsAlive) a.ApplyJoystickVelocity(dir);
    }

    private void NotifyUI()
    {
        BattleUIController.instance?.RefreshSelection(_selected);
        OnSelectionChanged?.Invoke(_selected);
    }

    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;
        var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            position = screenPos
        };
        var results = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
