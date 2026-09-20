using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// The firm's crew van. Walk the squad up to it and an ENTER VAN pin appears (same
/// style as the location pins). Choosing a destination boards up to five members,
/// draws a live map-style route line and drives the van there along the streets.
/// </summary>
public sealed class CrewVanSystem : MonoBehaviour
{
    public static CrewVanSystem Instance { get; private set; }

    public const int Capacity = 5;
    const float BoardRange = 16f;
    const float DriveSpeed = 14f;
    const float TurnSpeed = 6f;
    static readonly Color RouteColor = new Color(.22f, .90f, 1f, .92f);

    CityGameplay city;
    GameObject van;
    TextMeshPro label;
    RectTransform frame, pin;
    TextMeshProUGUI pinLabel;
    GameObject destinationPanel;
    RectTransform destinationList;
    LineRenderer route;
    GameObject destinationRing;
    Material routeMaterial;
    readonly List<Passenger> passengers = new List<Passenger>();
    bool travelling;
    float nextScan;
    int nearbyCount;

    struct Passenger
    {
        public AgentController Agent;
        public Renderer[] Renderers;
        public Collider[] Colliders;
        public NavMeshAgent Nav;
    }

    public bool IsTravelling => travelling;
    public bool Ready => van;
    public Vector3 Position => van ? van.transform.position : Vector3.zero;

    public static CrewVanSystem Ensure(CityGameplay owner)
    {
        if (Instance)
        {
            if (Instance.van) Instance.van.SetActive(false);
            Instance.enabled = false;
        }
        return null;
    }

    void Awake() { Instance = this; }
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (pin) Destroy(pin.gameObject);
        if (destinationPanel) Destroy(destinationPanel);
    }

    IEnumerator Start()
    {
        while (city == null || city.Locations == null || city.Locations.Length < 9) yield return null;
        BuildVan();
        LandscapeBattleHUD hud = null;
        while (!(hud = FindFirstObjectByType<LandscapeBattleHUD>()) || !hud.frame) yield return null;
        frame = hud.frame;
        BuildHud();
    }

    // ── World ────────────────────────────────────────────────────────────────

    void BuildVan()
    {
        var theme = LandscapeTheme.Current;
        var prefab = theme ? (theme.vanPrefab ? theme.vanPrefab : theme.taxiPrefab) : null;
        van = prefab ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        van.name = "CREW VAN";
        Vector3 anchor = city.Home + new Vector3(-16f, 0f, 12f);
        van.transform.position = CityGameplay.ReachableApproach(city.Home, anchor);
        van.transform.rotation = Quaternion.Euler(0f, 60f, 0f);
        if (!prefab) van.transform.localScale = new Vector3(2.2f, 2f, 4.6f);
        foreach (var behaviour in van.GetComponentsInChildren<MonoBehaviour>())
            if (behaviour && behaviour.GetType().Name == "Car") behaviour.enabled = false;
        foreach (var body in van.GetComponentsInChildren<Rigidbody>()) { body.isKinematic = true; body.useGravity = false; }
        SnapToGround();
        ZoneVolumeFactory.Create(van.transform, new Color(.22f, .78f, 1f, 1f), 5.2f);
        label = ZoneLabelUtil.Create(van.transform, "CREW VAN\n<size=68%>WALK UP  ·  ENTER  ·  " + Capacity + " SEATS</size>", 5.6f, 8f);
        MiniMapIconFactory.Register(van.transform, MiniMapIconFactory.Kind.Turf, "CREW VAN");
    }

    void SnapToGround()
    {
        if (!van) return;
        Vector3 p = van.transform.position;
        if (Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            van.transform.position = new Vector3(p.x, hit.point.y + .02f, p.z);
    }

    // ── HUD ──────────────────────────────────────────────────────────────────

    void BuildHud()
    {
        var button = LandscapeUI.Button("VanPin", frame, "ENTER  ·  5 MAX", 0, 0, 185, 43);
        button.onClick.AddListener(OpenDestinations);
        pin = button.transform as RectTransform;
        pin.SetAsFirstSibling();
        pinLabel = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (pinLabel) { pinLabel.color = Color.white; pinLabel.fontSize = 20f; pinLabel.fontSizeMin = 16f; pinLabel.fontSizeMax = 22f; pinLabel.overflowMode = TextOverflowModes.Overflow; }
        CityMissionHUD.Style(pin);
        var bg = button.targetGraphic as Image;
        if (bg)
        {
            var outline = bg.GetComponent<Outline>() ?? bg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.22f, .90f, 1f, .55f);
        }
        pin.gameObject.SetActive(false);

        destinationPanel = LandscapeUI.Panel("VanDestinations", frame, 560, 120, 480, 660, true).gameObject;
        LandscapeUI.Text("Title", destinationPanel.transform, "CREW VAN  ·  DESTINATIONS", 22, 14, 436, 36, 24, LandscapeUI.Gold, true);
        LandscapeUI.Text("Brief", destinationPanel.transform,
            $"ENTER — max {Capacity} members. Nearest crew board first. Pick an area; the van follows the streets with a live route line.",
            22, 52, 436, 58, 15, LandscapeUI.Muted);
        destinationList = LandscapeUI.Scroll("Destinations", destinationPanel.transform, 16, 116, 448, 470).content;
        LandscapeUI.Button("Close", destinationPanel.transform, "CLOSE", 22, 598, 436, 46).onClick.AddListener(() => destinationPanel.SetActive(false));
        destinationPanel.SetActive(false);
    }

    void Update()
    {
        if (!van) return;
        if (label && Camera.main) label.transform.rotation = Camera.main.transform.rotation;
        if (routeMaterial) routeMaterial.mainTextureOffset -= new Vector2(Time.deltaTime * 1.6f, 0f);

        if (!frame || !pin) return;

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + .2f;
            nearbyCount = travelling ? 0 : CrewInRange().Count;
            if (pinLabel) pinLabel.text = nearbyCount > 0 ? $"ENTER  ·  {Mathf.Min(nearbyCount, Capacity)}/{Capacity}" : "ENTER  ·  5 MAX";
        }

        bool blocked = travelling || GamePopup.AnyOpen || RecruitPackagePanel.AnyOpen || RecruitDialogBox.AnyOpen
                       || (destinationPanel && destinationPanel.activeSelf) || Time.timeScale == 0f || BattleManager.instance == null;
        if (blocked || nearbyCount == 0 || !Camera.main) { if (pin.gameObject.activeSelf) pin.gameObject.SetActive(false); return; }

        Vector3 projected = Camera.main.WorldToScreenPoint(van.transform.position + Vector3.up * 4.6f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, projected, null, out var local);
        float x = local.x - frame.rect.xMin, y = frame.rect.yMax - local.y;
        bool visible = projected.z > 0 && x > 220 && x < frame.rect.width - 180 && y > 90 && y < frame.rect.height - 70;
        if (pin.gameObject.activeSelf != visible) pin.gameObject.SetActive(visible);
        if (visible) LandscapeUI.Place(pin, x - 92, y - 21, 185, 43);
    }

    List<AgentController> CrewInRange()
    {
        var result = new List<AgentController>();
        var bm = BattleManager.instance;
        if (bm == null || !van) return result;
        float r2 = BoardRange * BoardRange;
        var selected = AgentSelectionManager.instance?.SelectedAgents;
        foreach (var a in bm.PlayerAgents)
        {
            if (!a || !a.IsAlive || a.IsActivityLocked) continue;
            Vector3 d = a.transform.position - van.transform.position; d.y = 0f;
            if (d.sqrMagnitude <= r2) result.Add(a);
        }
        // Selected members get the seats first, then the closest.
        return result
            .OrderByDescending(a => selected != null && selected.Contains(a))
            .ThenBy(a => Horizontal(a.transform.position, van.transform.position))
            .ToList();
    }

    // ── Destinations ─────────────────────────────────────────────────────────

    public void OpenDestinations()
    {
        if (travelling || !van || !destinationPanel) return;
        var crew = CrewInRange();
        if (crew.Count == 0) { Feedback("MOVE YOUR CREW NEXT TO THE VAN FIRST"); return; }

        LandscapeUI.Clear(destinationList);
        int boarding = Mathf.Min(Capacity, crew.Count);
        var brief = destinationPanel.transform.Find("Brief")?.GetComponent<TextMeshProUGUI>();
        if (brief) brief.text = $"<color=#70F2A0>{boarding} of {Capacity} seats</color> will be taken by the crew standing at the van" +
                                (crew.Count > Capacity ? $" — {crew.Count - Capacity} stay behind." : ".") +
                                " Choose a destination; the van drives there with a live route line.";

        var ordered = Enumerable.Range(0, city.Locations.Length)
            .Where(i => Horizontal(city.Locations[i], van.transform.position) >= 12f)
            .OrderBy(i => Horizontal(city.Locations[i], van.transform.position));
        foreach (int index in ordered)
        {
            int location = index;
            float distance = Horizontal(city.Locations[index], van.transform.position);
            var row = LandscapeUI.Button("Destination" + index, destinationList, $"{city.LocationNames[index]}    <size=70%><color=#9BADB5>{distance:0} m</color></size>", 0, 0, 430, 48, index == 0 ? "green" : "dark");
            LandscapeUI.LayoutSize(row.gameObject, 430, 48);
            row.onClick.AddListener(() => StartCoroutine(Travel(location)));
        }
        CityMissionHUD.Style(destinationPanel.transform);
        destinationPanel.transform.SetAsLastSibling();
        destinationPanel.SetActive(true);
        GameAudio.Play("popup");
    }

    IEnumerator Travel(int location)
    {
        if (travelling || !van || city == null || location < 0 || location >= city.Locations.Length) yield break;
        destinationPanel.SetActive(false);
        var crew = CrewInRange().Take(Capacity).ToList();
        if (crew.Count == 0) { Feedback("NOBODY IS CLOSE ENOUGH TO BOARD"); yield break; }

        travelling = true;
        Vector3 destination = city.Locations[location];
        string name = city.LocationNames[location];

        // Route along the streets (NavMesh); fall back to a straight line.
        var corners = BuildRoute(van.transform.position, destination);
        DrawRoute(corners);
        destinationRing = ZoneVolumeFactory.Create(new GameObject("VanDestination").transform, RouteColor, 5.5f);
        destinationRing.transform.parent.position = destination;
        AgentSelectionManager.CreateCommandMarker(destination, RouteColor, "VAN  ·  " + name);

        // Board.
        passengers.Clear();
        foreach (var member in crew)
        {
            member.SetActivityLocked(true, van.transform.position);
            var p = new Passenger
            {
                Agent = member,
                Renderers = member.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray(),
                Colliders = member.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray(),
                Nav = member.GetComponent<NavMeshAgent>()
            };
            if (p.Nav) p.Nav.enabled = false;
            foreach (var r in p.Renderers) r.enabled = false;
            foreach (var c in p.Colliders) c.enabled = false;
            passengers.Add(p);
        }
        Feedback($"{passengers.Count} MEMBER{(passengers.Count == 1 ? "" : "S")} BOARDED  ·  VAN TO {name}");
        GameAudio.Play("popup");
        yield return new WaitForSeconds(.35f);

        // Drive.
        var camCtl = CameraPanTouchOnly.Instance;
        int segment = 0;
        float stuckTimer = 0f;
        while (segment < corners.Length - 1)
        {
            Vector3 target = corners[segment + 1];
            Vector3 pos = van.transform.position;
            Vector3 toTarget = target - pos; toTarget.y = 0f;
            float remaining = toTarget.magnitude;
            float step = DriveSpeed * Time.deltaTime;
            if (remaining <= Mathf.Max(step, .35f)) { segment++; stuckTimer = 0f; continue; }

            Vector3 dir = toTarget / remaining;
            van.transform.rotation = Quaternion.Slerp(van.transform.rotation, Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * TurnSpeed);
            Vector3 next = pos + dir * step;
            if (Physics.Raycast(next + Vector3.up * 6f, Vector3.down, out var ground, 30f, ~0, QueryTriggerInteraction.Ignore)) next.y = ground.point.y + .02f;
            van.transform.position = next;

            foreach (var p in passengers) if (p.Agent) p.Agent.transform.position = next + Vector3.up * .4f;
            UpdateRoute(corners, segment, next);
            camCtl?.FocusOn(next);

            stuckTimer += Time.deltaTime;
            if (stuckTimer > 40f) break;
            yield return null;
        }

        // Unload.
        Vector3 arrival = van.transform.position;
        for (int i = 0; i < passengers.Count; i++)
        {
            var p = passengers[i];
            if (!p.Agent) continue;
            Vector3 offset = new Vector3(Mathf.Cos(i * 1.26f) * 2.6f, 0f, Mathf.Sin(i * 1.26f) * 2.6f) + van.transform.right * 2.2f;
            Vector3 drop = arrival + offset;
            if (NavMesh.SamplePosition(drop, out var hit, 8f, NavMesh.AllAreas)) drop = hit.position;
            else if (NavMesh.SamplePosition(destination, out hit, 12f, NavMesh.AllAreas)) drop = hit.position;
            foreach (var c in p.Colliders) if (c) c.enabled = true;
            foreach (var r in p.Renderers) if (r) r.enabled = true;
            if (p.Nav) { p.Nav.enabled = true; if (p.Nav.isOnNavMesh) p.Nav.Warp(drop); else p.Agent.transform.position = drop; }
            else p.Agent.transform.position = drop;
            p.Agent.SetActivityLocked(false, destination);
        }
        var selection = AgentSelectionManager.instance;
        if (selection)
        {
            selection.DeselectAll();
            foreach (var p in passengers) if (p.Agent && p.Agent.IsAlive) selection.Select(p.Agent);
        }
        passengers.Clear();
        ClearRoute();
        BattleManager.instance?.PersistBattleProgress();
        GameManager.Save();
        Feedback($"VAN ARRIVED  ·  {name}  ·  CREW UNLOADED");
        GameAudio.Play("recovery");
        camCtl?.CenterOnSelection();
        travelling = false;
    }

    // ── Route line ───────────────────────────────────────────────────────────

    static Vector3[] BuildRoute(Vector3 from, Vector3 to)
    {
        var path = new NavMeshPath();
        Vector3 start = from, end = to;
        if (NavMesh.SamplePosition(from, out var a, 10f, NavMesh.AllAreas)) start = a.position;
        if (NavMesh.SamplePosition(to, out var b, 10f, NavMesh.AllAreas)) end = b.position;
        if (NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) && path.corners != null && path.corners.Length >= 2)
        {
            var corners = path.corners;
            corners[0] = from;
            return corners;
        }
        return new[] { from, to };
    }

    void DrawRoute(Vector3[] corners)
    {
        ClearRoute();
        var go = new GameObject("VanRoute");
        route = go.AddComponent<LineRenderer>();
        route.useWorldSpace = true;
        route.alignment = LineAlignment.View;
        route.textureMode = LineTextureMode.Tile;
        route.numCapVertices = 4;
        route.numCornerVertices = 4;
        route.widthMultiplier = 1.65f;
        route.shadowCastingMode = ShadowCastingMode.Off;
        route.receiveShadows = false;
        routeMaterial = RouteMaterial();
        route.material = routeMaterial;
        route.startColor = route.endColor = RouteColor;
        UpdateRoute(corners, 0, corners[0]);
    }

    void UpdateRoute(Vector3[] corners, int segment, Vector3 current)
    {
        if (!route) return;
        int remaining = corners.Length - (segment + 1);
        var points = new Vector3[remaining + 1];
        points[0] = current + Vector3.up * .32f;
        for (int i = 0; i < remaining; i++) points[i + 1] = corners[segment + 1 + i] + Vector3.up * .32f;
        route.positionCount = points.Length;
        route.SetPositions(points);
    }

    void ClearRoute()
    {
        if (route) Destroy(route.gameObject);
        route = null;
        if (destinationRing) Destroy(destinationRing.transform.parent ? destinationRing.transform.parent.gameObject : destinationRing);
        destinationRing = null;
    }

    static Material RouteMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        var m = new Material(shader) { color = RouteColor, mainTexture = DashTexture() };
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", m.mainTexture);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", RouteColor);
        if (m.HasProperty("_Surface")) { m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); }
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.renderQueue = (int)RenderQueue.Transparent + 5;
        return m;
    }

    static Texture2D dash;
    static Texture2D DashTexture()
    {
        if (dash) return dash;
        dash = new Texture2D(16, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 4; y++)
                dash.SetPixel(x, y, x < 10 ? Color.white : new Color(1, 1, 1, 0));
        dash.Apply();
        return dash;
    }

    static float Horizontal(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
    static void Feedback(string message) { CityGameplay.Instance?.PostEvent(message); BattleUIController.instance?.ShowAlert(message, 2.4f); }
}
