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

        // A second tap close to the first recentres the camera. The first tap is handled immediately.
    private float _lastTapTime = -1f;
    private Vector2 _lastTapPos;
    private bool _moveCommandArmed;
    float _ignoreWorldTapUntil;
    public bool IsMoveCommandArmed => _moveCommandArmed;

    public static void ConsumeUiPointer()
    {
        if (instance) instance._ignoreWorldTapUntil = Time.unscaledTime + .16f;
    }

    public static bool BlocksWorldTap()
    {
        if (instance && Time.unscaledTime < instance._ignoreWorldTapUntil) return true;
        if (Time.timeScale == 0f || LiveMiniMap.IsExpanded) return true;
        if (GamePopup.AnyOpen || RecruitPackagePanel.AnyOpen || RecruitDialogBox.AnyOpen) return true;
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
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += OnFingerDown;
        Touch.onFingerUp += OnFingerUp;
    }

    void OnDisable()
    {
        _lastTapTime=-1;_mouseTap=false;_fingersDown=0;
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
            {
                _mouseDown=position;
                bool overUi=IsPointerOverUI(position)||BlocksWorldTap();
                _mouseTap=!overUi;
                _mouseDragged=false;
                if(overUi) ConsumeUiPointer();
            }
            if(mouse.leftButton.isPressed)
                _mouseDragged |= Vector2.Distance(position,_mouseDown)>dragThresholdPixels;
            if(mouse.leftButton.wasReleasedThisFrame && _mouseTap && !_mouseDragged)
                QueueTap(position);
        }
        foreach(var touch in Touch.activeTouches)
            if(Vector2.Distance(touch.screenPosition,touch.startScreenPosition)>dragThresholdPixels)_touchDragged=true;
    }

    private void OnFingerDown(Finger finger)
    {
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
        if (BlocksWorldTap() || IsPointerOverUI(position))
        {
            ConsumeUiPointer();
            return;
        }
        bool second = _lastTapTime > 0f && Time.unscaledTime - _lastTapTime < 0.28f && Vector2.Distance(position, _lastTapPos) < 55f;
        _lastTapTime = Time.unscaledTime;
        _lastTapPos = position;
        bool onPerson = false;
        if (_cam != null && Physics.Raycast(_cam.ScreenPointToRay(position), out var personHit, 2500f, LayerMask.GetMask("Agent")))
            onPerson = personHit.collider.GetComponentInParent<AgentController>() != null || personHit.collider.GetComponentInParent<EnemyController>() != null;
        HandleTap(position);
        if (second && !onPerson) CameraPanTouchOnly.Instance?.CenterOnSelection();
    }

    // ── Tap handling ──────────────────────────────────────────────────────

    private void HandleTap(Vector2 screenPos)
    {
        if (!_cam || BlocksWorldTap() || IsPointerOverUI(screenPos)) return;

        // 3D raycast from camera through tap point
        Ray ray = _cam.ScreenPointToRay(screenPos);

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
                if (_selected.Count > 0 && enemy.firmName != "POLICE")
                {
                    WorldChoiceBar.Present(enemy.transform, enemy.firmName,
                        ("FIGHT", new Color(0.72f, 0.14f, 0.14f), () => BattleManager.instance?.AttackGang(enemy.firmName)),
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
            if (_selected.Count > 0)
            {
                CommandSelectedMoveTo(groundPoint);
                _moveCommandArmed = false;
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
        var enemies = BattleManager.instance != null
            ? BattleManager.instance.EnemyAgents.Where(e => e != null && e.IsAlive && e.firmName != "POLICE").ToArray()
            : System.Array.Empty<EnemyController>();
        if (_selected.Count == 0) { NotifyCommand("SELECT A CREW MEMBER FIRST"); return; }
        if (enemies.Length == 0) { NotifyCommand("NO RIVAL CREW REMAINS"); return; }
        EnemyController marked = null;
        foreach (var a in _selected)
        {
            if (a == null || !a.IsAlive) continue;
            var target = enemies.OrderBy(e => (e.transform.position - a.transform.position).sqrMagnitude).FirstOrDefault();
            if (target != null) { a.CommandAttackTarget(target); marked ??= target; }
        }
        if (marked) CreateCommandMarker(marked.transform.position, new Color(1f, .18f, .12f, .95f), "ATTACK");
        NotifyCommand("ATTACK ORDER CONFIRMED");
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
        var leader=_selected.Find(a=>a && a.IsAlive);
        if(!leader)return;
        var path=new UnityEngine.AI.NavMeshPath();
        if(!UnityEngine.AI.NavMesh.CalculatePath(leader.transform.position,worldPoint,UnityEngine.AI.NavMesh.AllAreas,path) || path.status!=UnityEngine.AI.NavMeshPathStatus.PathComplete)
        {CityGameplay.Instance?.PostEvent("DESTINATION BLOCKED - CHOOSE A STREET APPROACH");return;}
        Debug.DrawRay(worldPoint, Vector3.up*10, Color.green, 3f);
        CreateCommandMarker(worldPoint, new Color(0.25f, 1f, 0.55f, 0.9f), "MOVE");
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
            _selected[i].CommandMoveTo(target);
        }
        NotifyCommand("MOVE ORDER CONFIRMED");
    }

    /// <summary>Drop ordered crew from the selection. Their ring turns yellow until they finish or you select them again.</summary>
    public void ReleaseOrdered(List<AgentController> agents)
    {
        if (agents == null) return;
        bool changed = false;
        foreach (var agent in agents)
        {
            if (!agent || !_selected.Contains(agent)) continue;
            _selected.Remove(agent);
            agent.SetSelected(false);
            changed = true;
        }
        if (changed) NotifyUI();
    }

    void PresentFightChoice(string gangName)
    {
        var crew = _selected.Where(a => a && a.IsAlive && !a.IsActivityLocked).ToList();
        if (crew.Count == 0)
        {
            NotifyCommand("SELECT THE CREW FOR THIS FIGHT");
            return;
        }
        GamePopup.Instance.Show(
            gangName.ToUpperInvariant() + " TURF",
            "CONFRONT starts a fight with the crew you have selected.\nMOVE ON leaves them where they are.\nAnyone you deselected is not sent.",
            new GamePopup.Option("HAVE IT!", new Color(0.7f, 0.15f, 0.15f), () =>
            {
                BattleManager.instance?.AttackGang(gangName, crew);
            }),
            new GamePopup.Option("MOVE ON", new Color(0.25f, 0.32f, 0.4f), () => { })
        );
    }

    public void ArmMoveCommand()
    {
        if (_selected.Count == 0) { NotifyCommand("SELECT A CREW MEMBER FIRST"); return; }
        _moveCommandArmed = true;
        NotifyCommand(_moveCommandArmed ? "MOVE READY - TAP A STREET" : "NO CREW AVAILABLE");
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

/// <summary>Marks overlay graphics so a UI press cannot also issue a world move order.</summary>
public sealed class UiWorldTapBlocker : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    public void OnPointerDown(PointerEventData eventData) => AgentSelectionManager.ConsumeUiPointer();
    public void OnPointerClick(PointerEventData eventData) => AgentSelectionManager.ConsumeUiPointer();
}
