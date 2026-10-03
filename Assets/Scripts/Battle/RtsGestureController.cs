using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>One owner for world gestures. A command gesture can never also pan or move.</summary>
[DefaultExecutionOrder(-100)]
public sealed class RtsGestureController : MonoBehaviour
{
    public enum InputMode { Camera, BoxSelect, GuardArea, PatrolA, PatrolB, PatrolReady, Menu }
    public static RtsGestureController Instance { get; private set; }
    public InputMode Mode { get; private set; }
    public static bool BlocksCamera => Instance && (Instance.Mode != InputMode.Camera || Instance.releaseFrame == Time.frameCount);
    AgentSelectionManager selection;
    Camera view;
    Vector2 start, current;
    Vector3 routeA, routeB;
    float began;
    bool down, dragged, suppressed, held;
    int finger = -1, releaseFrame = -1;
    Transform anchor;
    LineRenderer preview;
    GameObject routePreview;
    const float HoldSeconds = .5f;
    float Tolerance => Mathf.Max(18f, Screen.height * .018f);

    void Awake() { Instance = this; selection = GetComponent<AgentSelectionManager>(); view = Camera.main; }
    void OnDisable() { Cancel(); down = false; }
    void OnDestroy() { if (anchor) Destroy(anchor.gameObject); if (Instance == this) Instance = null; }
    public void BeginBoxSelection() { Cancel(); Mode = InputMode.BoxSelect; Hint("DRAG · draw a square over crew to select"); }
    public void BeginGuard() { Cancel(); if (!HasCrew()) return; Mode = InputMode.GuardArea; Hint("GUARD · drag an area or tap a position"); }
    public void BeginPatrol() { Cancel(); if (!HasCrew()) return; Mode = InputMode.PatrolA; Hint("PATROL · tap point A"); }
    bool HasCrew() { if (selection.SelectedAgents.Count > 0) return true; Hint("Select crew first"); return false; }
    public void Cancel()
    {
        Mode = InputMode.Camera;
        if (anchor) anchor.GetComponent<WorldChoiceBar>()?.Hide();
        if (preview) Destroy(preview.gameObject);
        preview = null;
        if(routePreview)Destroy(routePreview);routePreview=null;
        suppressed = true;
        releaseFrame = Time.frameCount;
    }

    void Update()
    {
        if (!view) view = Camera.main;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
        if (AgentSelectionManager.BlocksWorldTap()) { if (down) suppressed = true; return; }
        var touches = Touch.activeTouches;
        if (touches.Count > 1) { suppressed = true; down = false; finger = -1; return; }
        if (touches.Count == 1)
        {
            var t = touches[0];
            if (t.phase == UnityEngine.InputSystem.TouchPhase.Began) { finger = t.touchId; Begin(t.screenPosition); }
            if (finger != t.touchId) return;
            Track(t.screenPosition);
            if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            { End(t.screenPosition, t.phase == UnityEngine.InputSystem.TouchPhase.Canceled); finger = -1; }
            return;
        }
        var mouse = Mouse.current;
        if (mouse == null) return;
        var p = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame) Begin(p);
        if (mouse.leftButton.isPressed) Track(p);
        if (mouse.leftButton.wasReleasedThisFrame) End(p, false);
    }

    void Begin(Vector2 p)
    {
        start = current = p; began = Time.unscaledTime; down = true; held = dragged = false;
        suppressed = selection.IsPointerOverUI(p) || AgentSelectionManager.BlocksWorldTap();
    }
    void Track(Vector2 p)
    {
        if (!down || suppressed) return;
        current = p;
        dragged |= Vector2.Distance(start, p) > Tolerance;
        if (Mode == InputMode.GuardArea && dragged && Ground(start, out var a) && Ground(p, out var b))
            DrawPreview(GuardBounds(a, b));
        if (Mode == InputMode.Camera && !held && !dragged && Time.unscaledTime - began >= HoldSeconds && selection.SelectedAgents.Count > 0 && Ground(p, out var point))
        { held = true; OpenMenu(point); }
    }
    void End(Vector2 p, bool canceled)
    {
        if (!down) return;
        down = false;
        if (canceled || suppressed || held || selection.IsPointerOverUI(p)) return;
        var mode = Mode;
        if (mode == InputMode.BoxSelect)
        {
            SelectRectangle(start, p); Cancel(); return;
        }
        if (mode == InputMode.GuardArea)
        {
            if (Ground(start, out var a) && Ground(p, out var b)) IssueGuard(GuardBounds(a, dragged ? b : a + new Vector3(8, 0, 8)));
            return;
        }
        if (dragged) return;
        if (mode == InputMode.PatrolA || mode == InputMode.PatrolB)
        {
            if (!Ground(p, out var point)) { Hint("Choose a walkable street"); return; }
            if (mode == InputMode.PatrolA) { routeA = point; routePreview=RtsOrderVisual.Pin(transform,point,"A",Color.cyan); Mode = InputMode.PatrolB; Hint("PATROL · tap point B"); }
            else
            {
                if (Vector3.Distance(routeA, point) < 3f) { Hint("Choose point B farther away"); return; }
                routeB = point; Mode = InputMode.PatrolReady; Anchor(point);
                RtsOrderVisual.Pin(routePreview.transform,point,"B",Color.cyan);
                WorldChoiceBar.Present(anchor, "PATROL A → B", Cancel,
                    ("START PATROL", LandscapeUI.Green, StartPatrol), ("CANCEL", LandscapeUI.PanelColor, Cancel));
            }
            return;
        }
        if (mode == InputMode.Menu || mode == InputMode.PatrolReady) { Cancel(); return; }
        selection.HandleTap(p);
    }

    void Anchor(Vector3 point)
    {
        if (!anchor) { anchor = new GameObject("RTS world orders").transform; anchor.SetParent(transform); }
        anchor.position = point;
    }
    void OpenMenu(Vector3 point)
    {
        Anchor(point); Mode = InputMode.Menu;
        WorldChoiceBar.Present(anchor, "CREW ORDERS", Cancel,
            ("MOVE", LandscapeUI.Green, () => { Cancel(); selection.CommandSelectedMoveTo(point); }),
            ("GUARD", LandscapeUI.Green, BeginGuard),
            ("PATROL", LandscapeUI.Green, BeginPatrol),
            ("MORE", LandscapeUI.PanelColor, () => WorldChoiceBar.Present(anchor, "TACTICS", Cancel,
                ("ATTACK", LandscapeUI.Red, () => { Cancel(); selection.CommandSelectedAttack(); }),
                ("RETREAT", LandscapeUI.PanelColor, () => { Cancel(); selection.CommandSelectedRetreat(); }),
                ("SCOUT", LandscapeUI.PanelColor, () => { Cancel(); RtsOrderVisual.Scout(point,selection.SelectedAgents); }),
                ("PAY", LandscapeUI.Gold, () => { Cancel(); RivalSettlement.Offer(anchor); }))));
    }
    public bool Ground(Vector2 p, out Vector3 point)
    {
        point = Vector3.zero;
        if (!view) return false;
        var ray = view.ScreenPointToRay(p);
        // Buildings never intercept street commands. NavMesh validates the ground projection.
        float y = selection.SelectedAgents.Count > 0 && selection.SelectedAgents[0] ? selection.SelectedAgents[0].transform.position.y : 0f;
        if (!new Plane(Vector3.up, new Vector3(0, y, 0)).Raycast(ray, out var distance)) return false;
        if (!NavMesh.SamplePosition(ray.GetPoint(distance), out var hit, 4f, NavMesh.AllAreas)) return false;
        point = hit.position; return true;
    }
    public void SelectRectangle(Vector2 a, Vector2 b)
    {
        var rect = Rect.MinMaxRect(Mathf.Min(a.x,b.x), Mathf.Min(a.y,b.y), Mathf.Max(a.x,b.x), Mathf.Max(a.y,b.y));
        if (!BattleManager.instance || !view) return;
        selection.DeselectAll();
        foreach (var member in BattleManager.instance.PlayerAgents)
        {
            if (!member || !member.IsAlive || !member.gameObject.activeInHierarchy) continue;
            var p = view.WorldToScreenPoint(member.transform.position + Vector3.up);
            if (p.z > 0 && rect.Contains(p)) selection.Select(member);
        }
        Hint(selection.SelectedAgents.Count + " crew selected");
    }
    public static Bounds GuardBounds(Vector3 a, Vector3 b)
    {
        var size = new Vector3(Mathf.Clamp(Mathf.Abs(a.x-b.x), 4, 60), 8, Mathf.Clamp(Mathf.Abs(a.z-b.z), 4, 60));
        return new Bounds((a+b)*.5f, size);
    }
    void IssueGuard(Bounds area)
    {
        var members = new List<AgentController>();
        int index=0, total=selection.SelectedAgents.Count;
        foreach (var member in selection.SelectedAgents)
        {
            float angle=index++*Mathf.PI*2/Mathf.Max(1,total);
            var post=area.center+new Vector3(Mathf.Cos(angle)*area.extents.x*.6f,0,Mathf.Sin(angle)*area.extents.z*.6f);
            if (member && member.CommandGuard(area,post)) members.Add(member);
        }
        if (members.Count == 0) { Hint("Guard area unreachable or crew busy"); return; }
        RtsDutyBoundary.Create(area, members); Cancel(); Hint("GUARD · " + members.Count + " assigned");
    }
    public void StartPatrol()
    {
        int count = 0;
        var accepted=new List<AgentController>();
        foreach (var member in selection.SelectedAgents) if (member && member.CommandPatrol(routeA, routeB)) {count++;accepted.Add(member);}
        if (count == 0) { Mode = InputMode.PatrolB; Hint("Route unreachable · choose another point B"); return; }
        RtsOrderVisual.Patrol(routeA,routeB,accepted);Cancel(); Hint("PATROL · " + count + " assigned");
    }
    void DrawPreview(Bounds bounds)
    {
        if (!preview) preview = RtsDutyBoundary.MakeLine("Guard preview");
        RtsDutyBoundary.SetCorners(preview, bounds);
    }
    void OnGUI()
    {
        if (!down || suppressed || Mode != InputMode.BoxSelect) return;
        var r = Rect.MinMaxRect(Mathf.Min(start.x,current.x), Screen.height-Mathf.Max(start.y,current.y), Mathf.Max(start.x,current.x), Screen.height-Mathf.Min(start.y,current.y));
        var old = GUI.color; GUI.color = new Color(.2f,1f,.45f,.14f); GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.green;
        GUI.DrawTexture(new Rect(r.x,r.y,r.width,2),Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x,r.yMax-2,r.width,2),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,r.y,2,r.height),Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMax-2,r.y,2,r.height),Texture2D.whiteTexture); GUI.color = old;
    }
    static void Hint(string message) { CityGameplay.Instance?.PostEvent(message); BattleUIController.instance?.ShowAlert(message, 2f); }
}

public sealed class RtsDutyBoundary : MonoBehaviour
{
    Bounds area;
    List<AgentController> members;
    public static void Create(Bounds bounds, List<AgentController> crew)
    {
        var line = MakeLine("Assigned guard area"); SetCorners(line,bounds);
        var boundary = line.gameObject.AddComponent<RtsDutyBoundary>(); boundary.area = bounds; boundary.members = crew;
        RtsOrderVisual.Pin(line.transform,bounds.center,"GUARD",new Color(.15f,1f,.35f));
    }
    public static LineRenderer MakeLine(string name)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>(); line.loop = true; line.positionCount = 4;
        line.startWidth = line.endWidth = .22f; line.useWorldSpace = true;
        ZoneVolumeFactory.ApplyDoubleSidedMaterial(line, new Color(.15f,1f,.35f,.8f)); line.alignment=LineAlignment.View; return line;
    }
    public static void SetCorners(LineRenderer line, Bounds b)
    {
        float y = b.center.y + .15f;
        line.SetPositions(new[] { new Vector3(b.min.x,y,b.min.z), new Vector3(b.max.x,y,b.min.z), new Vector3(b.max.x,y,b.max.z), new Vector3(b.min.x,y,b.max.z) });
    }
    void Update()
    {
        if (members == null) return;
        members.RemoveAll(a => !a || !a.IsAlive || a.CurrentOrder != AgentController.StandingOrder.Guard || a.GuardRegion != area);
        if (members.Count == 0) Destroy(gameObject);
    }
}
