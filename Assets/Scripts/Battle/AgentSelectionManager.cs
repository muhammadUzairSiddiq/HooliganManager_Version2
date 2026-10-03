using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Tap a crew member to add them. Tap the same member again to remove only them.
/// A ground tap orders whoever is still selected. They stay selected after the order.
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
    /// <summary>The crew member the player selected most recently. Police hunt this person when nobody is in a fight.</summary>
    public static AgentController LastPicked { get; private set; }

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

    public enum TargetCommand { Move, Attack, Guard, Patrol }
    public TargetCommand PendingCommand { get; private set; }
    int _ignoreWorldTapFrame = -1;
    public bool IsMoveCommandArmed => PendingCommand == TargetCommand.Move;
    readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
    PointerEventData _pointerData;
    EventSystem _pointerEventSystem;

    public static void ConsumeUiPointer()
    {
        if (instance) instance._ignoreWorldTapFrame = Time.frameCount;
    }

    public static bool BlocksWorldTap()
    {
        if (instance && Time.frameCount == instance._ignoreWorldTapFrame) return true;
        if (Time.timeScale == 0f || LiveMiniMap.IsExpanded) return true;
        if (GamePopup.AnyOpen || RecruitPackagePanel.AnyOpen || RecruitDialogBox.AnyOpen) return true;
        if (CityMissionHUD.MissionOpen || (CityDevelopmentSystem.Instance && CityDevelopmentSystem.Instance.IsOpen)) return true;
        if (NpcConversationUI.Instance && NpcConversationUI.Instance.IsConversationOpen) return true;
        if (CityOperationsSystem.Instance && CityOperationsSystem.Instance.IsBoardOpen) return true;
        return false;
    }

    public static void NotifyDoubleTapConsumed() { }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        _cam = Camera.main;
        if (gameObject.scene.name == "Gameplay" && !GetComponent<RtsGestureController>()) gameObject.AddComponent<RtsGestureController>();
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += OnFingerDown;
        Touch.onFingerUp += OnFingerUp;
    }

    void OnDisable()
    {
        _mouseTap=false;_fingersDown=0;_multiTouchThisGesture=false;
        Touch.onFingerDown -= OnFingerDown;
        Touch.onFingerUp -= OnFingerUp;
    }

    void Update()
    {
        if (RtsGestureController.Instance) return;
        var mouse=UnityEngine.InputSystem.Mouse.current;
        if (mouse!=null && Touch.activeTouches.Count==0)
        {
            var position=mouse.position.ReadValue();
            if(mouse.leftButton.wasPressedThisFrame)
            {
                _mouseDown=position;
                bool overUi=IsPointerOverUI(position)||BlocksWorldTap();
                _mouseTap=!overUi;
                _mouseDragged=false;
                if(overUi) ConsumeUiPointer();
            }
            if(mouse.leftButton.isPressed)
                _mouseDragged |= Vector2.Distance(position,_mouseDown)>dragThresholdPixels;
            if(mouse.leftButton.wasReleasedThisFrame && _mouseTap && !_mouseDragged && Vector2.Distance(position,_mouseDown)<=dragThresholdPixels)
                QueueTap(position);
        }
        foreach(var touch in Touch.activeTouches)
            if(Vector2.Distance(touch.screenPosition,touch.startScreenPosition)>dragThresholdPixels)_touchDragged=true;
    }

    private void OnFingerDown(Finger finger)
    {
        if (RtsGestureController.Instance) return;
        if(_fingersDown==0)
        {
            _touchDragged=false;
            _touchStartedOnUI=IsPointerOverUI(finger.currentTouch.screenPosition)||BlocksWorldTap();
            if(_touchStartedOnUI) ConsumeUiPointer();
        }
        _fingersDown++;
        if (_fingersDown >= 2) _multiTouchThisGesture = true;
    }

    private void OnFingerUp(Finger finger)
    {
        if (RtsGestureController.Instance) return;
        var t = finger.currentTouch;
        float dpiThreshold = Mathf.Max(dragThresholdPixels, Screen.height * 0.02f);
        bool wasTap = t.phase != UnityEngine.InputSystem.TouchPhase.Canceled && !_multiTouchThisGesture && !_touchDragged && !_touchStartedOnUI &&
                      Vector2.Distance(t.screenPosition, t.startScreenPosition) <= dpiThreshold;

        _fingersDown = Mathf.Max(0, _fingersDown - 1);
        if (_fingersDown == 0) _multiTouchThisGesture = false;

        if (!wasTap) return;

        QueueTap(t.screenPosition);
    }

    private void QueueTap(Vector2 position)
    {
        if (BlocksWorldTap() || IsPointerOverUI(position))
        {
            ConsumeUiPointer();
            return;
        }
        HandleTap(position);
    }

    // ── Tap handling ──────────────────────────────────────────────────────

    public void HandleTap(Vector2 screenPos)
    {
        if (!_cam || BlocksWorldTap() || IsPointerOverUI(screenPos)) return;

        // 3D raycast from camera through tap point
        Ray ray = _cam.ScreenPointToRay(screenPos);

        // A crew tap always toggles that member, even over a mission pad.
        if (Physics.Raycast(ray, out var crewHit, 2500f, LayerMask.GetMask("Agent")))
        {
            var member = crewHit.collider.GetComponentInParent<AgentController>();
            if (member && member.IsAlive) { ToggleSelect(member); return; }
        }

        // Explicit attack targeting takes precedence over a group's interaction pad.
        if (PendingCommand == TargetCommand.Attack && Physics.Raycast(ray, out var attackHit, 2500f, LayerMask.GetMask("Agent")))
        {
            var target = attackHit.collider.GetComponentInParent<EnemyController>();
            if (target) { CommandSelectedAttackTarget(target); return; }
        }

        // City operation pads are world interactions. Resolve them before the
        // generic ground order so tapping a task never accidentally moves the crew.
        foreach (var hitInfo in Physics.RaycastAll(ray, 2500f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
        {
            var choice = hitInfo.collider.GetComponentInParent<WorldChoiceButton>();
            if (choice && choice.IsLive)
            {
                choice.Press();
                return;
            }
            var bubble = hitInfo.collider.GetComponentInParent<WorldInteractBubble>();
            if (bubble && bubble.IsLive)
            {
                bubble.Activate();
                return;
            }
            var operationHit = hitInfo.collider.GetComponentInParent<CityOperationNode>();
            if (operationHit)
            {
                operationHit.OpenInteraction();
                return;
            }
        }
        
        // ── 1. Check if we hit an Agent (Player or Enemy) ──
        if (Physics.Raycast(ray, out RaycastHit hit, 2500f, LayerMask.GetMask("Agent")))
        {
            var socialNpc = hit.collider.GetComponentInParent<SocialNpc>();
            if (socialNpc != null)
            {
                socialNpc.Talk();
                return;
            }

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
                if (PendingCommand == TargetCommand.Attack)
                {
                    CommandSelectedAttackTarget(enemy);
                    return;
                }
                if (_selected.Count > 0 && enemy.firmName != "POLICE")
                {
                    WorldChoiceBar.Present(enemy.transform, enemy.firmName,
                        ("FIGHT", new Color(0.72f, 0.14f, 0.14f), () => BattleManager.instance?.AttackGang(enemy.firmName)),
                        ("PAY",LandscapeUI.Gold,()=>RivalSettlement.Offer(enemy.transform)),
                        ("MOVE ON", new Color(0.16f, 0.38f, 0.62f), () => { }));
                    return;
                }
                if (!enemy.isHostile)
                {
                    NotifyCommand("SELECT THE CREW, THEN TAP THE YELLOW MARKER");
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
        if (RtsGestureController.Instance && RtsGestureController.Instance.Ground(screenPos, out groundPoint))
        {
            groundHit = true;
        }
        else if (!RtsGestureController.Instance && Physics.Raycast(ray, out RaycastHit groundHitInfo, 2500f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
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
            if (_selected.Count > 0)
            {
                if (PendingCommand == TargetCommand.Attack) NotifyCommand("ATTACK READY - TAP A RIVAL, NOT THE GROUND");
                else CommandSelectedGround(groundPoint);
            }
        }
        else if (_selected.Count > 0) NotifyCommand("DESTINATION BLOCKED - TAP A WALKABLE STREET");
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
        if (!agent || !agent.IsAlive || !agent.gameObject.activeInHierarchy) return;
        if (!_selected.Contains(agent))
        {
            _selected.Add(agent);
            agent.SetSelected(true);
        }
        LastPicked = agent;
        NotifyUI();
    }

    public void Deselect(AgentController agent)
    {
        if (!_selected.Remove(agent)) return;
        if (agent) agent.SetSelected(false);
        if (_selected.Count == 0) PendingCommand = TargetCommand.Move;
        NotifyUI();
    }

    public void DeselectAll()
    {
        foreach (var a in _selected) if(a)a.SetSelected(false);
        _selected.Clear();
        PendingCommand = TargetCommand.Move;
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
        ArmCommand(TargetCommand.Attack, "ATTACK READY - TAP A RIVAL GROUP; FIGHTING INCREASES POLICE PRESSURE");
    }

    public void CommandSelectedAttackTarget(EnemyController target)
    {
        var crew = _selected.Where(a => a && a.IsAlive && !a.IsActivityLocked && a.gameObject.activeInHierarchy).ToList();
        if (crew.Count == 0) { NotifyCommand("NO AVAILABLE SELECTED CREW"); return; }
        if (!target || !target.IsAlive || target.IsHomeMatchdaySupporter) { NotifyCommand("CHOOSE A LIVING RIVAL OR HOSTILE OFFICER"); return; }
        if (target.firmName == "POLICE" && !target.isHostile) { NotifyCommand("POLICE ARE NOT HOSTILE - USE THEIR INTERACTION BUBBLE"); return; }
        if (target.firmName != "POLICE" && BattleManager.instance) BattleManager.instance.AttackGang(target.firmName, crew);
        else foreach (var agent in crew) agent.CommandAttackTarget(target);
        PendingCommand = TargetCommand.Move;
        CreateCommandMarker(target.transform.position, new Color(1f, .18f, .12f, .95f), "ATTACK");
        RtsOrderVisual.Attack(target,crew);
        NotifyCommand("ATTACK ORDER CONFIRMED - SELECTION RETAINED");
    }

    public void CommandSelectedRetreat()
    {
        if (_selected.Count == 0) { NotifyCommand("SELECT A CREW MEMBER FIRST"); return; }
        bool safeTransport = GameManager.Data != null && GameManager.Data.TransportPrepared;
        foreach (var a in _selected)
            if (a != null && a.IsAlive)
            {
                // The city-management layer directly affects the tactical layer:
                // a prepared transport route gives retreating members a short speed window.
                if (safeTransport) a.ApplyTemporaryBoost(BoostType.Speed, .30f, 12f);
                a.CommandRetreat();
            }
        var retreat = BattleManager.instance != null && BattleManager.instance.retreatPoint
            ? BattleManager.instance.retreatPoint.position : Vector3.zero;
        CreateCommandMarker(retreat, new Color(.20f, .60f, 1f, .95f), "RETREAT");
        NotifyCommand(safeTransport ? "SAFE TRANSPORT ROUTE ACTIVE - RETREAT SPEED +30%" :
            CityGameplay.HomeMode ? "REGROUPING AT HEADQUARTERS" : "RETREAT ORDER CONFIRMED");
    }

    public void CommandSelectedMoveTo(Vector3 worldPoint)
    {
        PendingCommand = TargetCommand.Move;
        CommandSelectedGround(worldPoint);
    }

    public void CommandSelectedGround(Vector3 worldPoint)
    {
        if (PendingCommand == TargetCommand.Attack) { NotifyCommand("TAP A RIVAL GROUP TO ATTACK"); return; }
        if (_selected.Count == 0) { NotifyCommand("SELECT A CREW MEMBER FIRST"); return; }
        int accepted = 0;
        string label = PendingCommand.ToString().ToUpperInvariant();
        // Use a roomy two-row formation so affiliation rings remain distinct.
        const float spacing = 3.2f;
        int columns = Mathf.Min(4, Mathf.CeilToInt(Mathf.Sqrt(_selected.Count)));
        for (int i = 0; i < _selected.Count; i++)
        {
            if (_selected[i] == null || !_selected[i].IsAlive) continue;
            int row = i / columns;
            int column = i % columns;
            float width = (Mathf.Min(columns, _selected.Count - row * columns) - 1) * spacing;
            Vector3 target = worldPoint + new Vector3(column * spacing - width * .5f, 0f, row * spacing);
            bool ordered = PendingCommand == TargetCommand.Move
                ? _selected[i].TryCommandMoveTo(target)
                : _selected[i].CommandArea(target, PendingCommand == TargetCommand.Patrol);
            if (ordered) accepted++;
        }
        if (accepted == 0) { NotifyCommand("ORDER BLOCKED - CREW BUSY OR DESTINATION UNREACHABLE"); return; }
        CreateCommandMarker(worldPoint, new Color(.25f, 1f, .55f, .9f), label);
        NotifyCommand(label + " CONFIRMED: " + accepted + "/" + _selected.Count + " SELECTED MEMBERS");
        PendingCommand = TargetCommand.Move;
    }

    public void ArmMoveCommand()
    {
        ArmCommand(TargetCommand.Move, "MOVE READY - TAP A STREET");
    }

    public void ArmGuardCommand() => ArmCommand(TargetCommand.Guard, "GUARD READY - TAP AN AREA; DEFEND NEARBY AND RETURN");
    public void ArmPatrolCommand() => ArmCommand(TargetCommand.Patrol, "PATROL READY - TAP THE OTHER END OF THE ROUTE; AUTO-DEFEND AND RESUME");

    void ArmCommand(TargetCommand command, string hint)
    {
        if (_selected.Count == 0) { NotifyCommand("SELECT A CREW MEMBER FIRST"); return; }
        PendingCommand = command;
        NotifyCommand(hint);
    }

    public static void CreateCommandMarker(Vector3 point, Color color, string label)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = label + "OrderFeedback";
        marker.transform.position = point + Vector3.up * 0.08f;
        marker.transform.localScale = new Vector3(1.4f, 0.025f, 1.4f);
        var collider = marker.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var material = new Material(shader) { color = color };
                renderer.material = material;
            }
        }
        var text = ZoneLabelUtil.Create(marker.transform, label, 2.2f, 2.2f);
        if (text) text.color = color;
        Destroy(marker, 1.6f);
    }

    private static void NotifyCommand(string message)
    {
        BattleUIController.instance?.ShowAlert(message, 1.6f);
        CityGameplay.Instance?.PostEvent(message);
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

    public bool IsPointerOverUI(Vector2 screenPos)
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;
        if (_pointerData == null || _pointerEventSystem != EventSystem.current)
        {
            _pointerEventSystem = EventSystem.current;
            _pointerData = new PointerEventData(_pointerEventSystem);
        }
        _pointerData.position = screenPos;
        _uiHits.Clear();
        EventSystem.current.RaycastAll(_pointerData, _uiHits);
        return _uiHits.Count > 0;
    }
}

/// <summary>Marks overlay graphics so a UI press cannot also issue a world move order.</summary>
public sealed class UiWorldTapBlocker : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    public void OnPointerDown(PointerEventData eventData) => AgentSelectionManager.ConsumeUiPointer();
    public void OnPointerClick(PointerEventData eventData) => AgentSelectionManager.ConsumeUiPointer();
}
