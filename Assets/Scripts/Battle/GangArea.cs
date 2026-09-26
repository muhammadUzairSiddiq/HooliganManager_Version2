using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Rival gang turf. Crew standing inside raises a small world marker.
/// The city keeps running until the player taps that marker.
/// </summary>
public class GangArea : MonoBehaviour
{
    private string _gangName;
    private float _radius;
    private float _detectRadius;
    private Color _color;
    private TextMeshPro _label;
    private GangScreenChoice _choice;
    private bool _dismissed;

    public string GangName => _gangName;
    public Color ZoneColor => _color;
    public float Radius => _radius;
    float initialRadius;
    public void SetGrowth(int ticks)
    {
        if(initialRadius<=0)initialRadius=_radius;
        _radius=initialRadius+Mathf.Clamp(ticks,0,5)*.6f;
        _detectRadius=Mathf.Max(_detectRadius,_radius*1.7f);
        var ring=transform.Find("ZoneRing");
        if(ring)ring.localScale=Vector3.one*(_radius/Mathf.Max(.1f,initialRadius));
    }
    public float DetectRadius => _detectRadius;

    public void Setup(string gangName, Vector3 center, float radius, Color color)
    {
        _gangName = gangName;
        _radius = radius;
        _detectRadius = Mathf.Max(radius * 2.2f, radius * 1.7f * 0.71f + 3.5f);
        _color = SanitizeGangColor(color);

        transform.position = GroundedCenter(center);
        ZoneVolumeFactory.Create(transform, _color, radius, height: 2.6f);
        _label = ZoneLabelUtil.Create(transform, _gangName.ToUpperInvariant() + "\n<size=68%>RIVAL TURF</size>", 4.4f, 8.2f);
        _choice = gameObject.AddComponent<GangScreenChoice>();
        MiniMapIconFactory.Register(transform, MiniMapIconFactory.Kind.Gang, _gangName);
        SetGrowth(RivalGrowthSystem.GrowthTicks(GameManager.Data, _gangName));
    }

    private static Color SanitizeGangColor(Color c)
    {
        float lum = c.r * 0.3f + c.g * 0.6f + c.b * 0.1f;
        if (c.a < 0.01f || lum > 0.92f || lum < 0.08f)
            return new Color(0.9f, 0.15f, 0.15f, 1f);
        c.a = 1f;
        return c;
    }

    private Vector3 GroundedCenter(Vector3 center)
    {
        if (Physics.Raycast(center + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
            center.y = hit.point.y + 0.02f;
        return center;
    }

    void Update()
    {
        if (_label != null && Camera.main != null)
            _label.transform.rotation = Camera.main.transform.rotation;

        if (_choice == null || BattleManager.instance == null || string.IsNullOrEmpty(_gangName)) return;
        bool offer = CrewInside().Count > 0 && !AlreadyFighting();
        if (!offer)
        {
            _dismissed = false;
            _choice.Hide();
            return;
        }
        if (!_dismissed && !_choice.IsOpen)
            _choice.Show(Confront, MoveOn, () => { _dismissed = true; _choice.Hide(); });
    }

    void Confront()
    {
        var crew = CrewInside();
        _choice.Hide();
        if (crew.Count == 0) return;
        BattleManager.instance?.AttackGang(_gangName, crew);
    }

    void MoveOn()
    {
        var crew = CrewInside();
        _choice.Hide();
        _dismissed = true;
        WalkOut(crew);
    }

    List<AgentController> CrewInside()
    {
        var found = new List<AgentController>();
        float limit = _radius;
        foreach (var agent in BattleManager.instance.PlayerAgents)
        {
            if (!agent || !agent.IsAlive || agent.IsActivityLocked) continue;
            Vector3 delta = agent.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= limit * limit) found.Add(agent);
        }
        return found;
    }

    bool AlreadyFighting()
    {
        foreach (var enemy in BattleManager.instance.EnemyAgents)
        {
            if (!enemy || !enemy.IsAlive || enemy.firmName != _gangName || !enemy.isHostile) continue;
            return true;
        }
        return false;
    }

    void WalkOut(List<AgentController> crew)
    {
        foreach (var agent in crew)
        {
            if (!agent || !agent.IsAlive) continue;
            Vector3 delta = agent.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.04f) delta = Vector3.forward;
            Vector3 dest = transform.position + delta.normalized * (_radius + 4f);
            dest.y = agent.transform.position.y;
            if (UnityEngine.AI.NavMesh.SamplePosition(dest, out var hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
                dest = hit.position;
            agent.CommandMoveTo(dest);
        }
    }
}

/// <summary>Fight / Move On / Close, the same size as the Headquarters button, kept on the gang.</summary>
public sealed class GangScreenChoice : MonoBehaviour
{
    public bool IsOpen { get; private set; }
    RectTransform _fight, _move, _close, _frame;
    Action _onFight, _onMove, _onClose;

    public void Show(Action onFight, Action onMove, Action onClose)
    {
        Show(gameObject.name, onFight, onMove, onClose);
    }

    public void Show(string title, Action onFight, Action onMove, Action onClose)
    {
        _onFight = onFight;
        _onMove = onMove;
        _onClose = onClose;
        Ensure();
        if (!_frame || !_fight || !_move || !_close) return;
        IsOpen = true;
        SetVisible(true);
    }

    public void Hide()
    {
        IsOpen = false;
        SetVisible(false);
    }

    void Ensure()
    {
        if (_fight) return;
        _frame = WorldButtonLayer.ChoiceFrame();
        if (!_frame) return;
        _fight = Make("Fight", "FIGHT", new Color(0.72f, 0.14f, 0.14f), () => _onFight?.Invoke());
        _move = Make("MoveOn", "MOVE ON", new Color(0.16f, 0.38f, 0.62f), () => _onMove?.Invoke());
        _close = Make("Close", "X", new Color(0.18f, 0.2f, 0.24f), () => _onClose?.Invoke());
    }

    RectTransform Make(string id, string caption, Color color, Action action)
    {
        var button = LandscapeUI.Button("Gang" + id, _frame, caption, 0, 0, caption == "X" ? 48 : 168, 48, "dark");
        var image = button.targetGraphic as UnityEngine.UI.Image;
        if (image) image.color = color;
        button.onClick.AddListener(() => action?.Invoke());
        var label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label) { label.color = Color.white; label.fontSize = caption == "X" ? 26 : 20; }
        button.transform.SetAsLastSibling();
        return button.transform as RectTransform;
    }

    void LateUpdate()
    {
        if (!IsOpen || !_frame || !Camera.main) return;
        Vector3 projected = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 3.2f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_frame, projected, null, out var local);
        float x = local.x - _frame.rect.xMin;
        float y = _frame.rect.yMax - local.y;
        bool visible = projected.z > 0.1f;
        SetVisible(visible);
        if (!visible) return;
        LandscapeUI.Place(_fight, x - 196, y - 24, 168, 48);
        LandscapeUI.Place(_move, x - 20, y - 24, 168, 48);
        LandscapeUI.Place(_close, x + 156, y - 24, 48, 48);
        WorldButtonLayer.CoverChoice(new Rect(x - 204f, y - 28f, 416f, 56f));
    }

    void SetVisible(bool on)
    {
        if (_fight) _fight.gameObject.SetActive(on && IsOpen);
        if (_move) _move.gameObject.SetActive(on && IsOpen);
        if (_close) _close.gameObject.SetActive(on && IsOpen);
    }

    void OnDestroy()
    {
        if (_fight) Destroy(_fight.gameObject);
        if (_move) Destroy(_move.gameObject);
        if (_close) Destroy(_close.gameObject);
    }
}

public sealed class RecruitmentPin : MonoBehaviour
{
    public Vector3 Point;
    RectTransform _pin, _frame;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (!cam) return;
        if (!_pin)
        {
            _frame = WorldButtonLayer.Frame();
            if (!_frame) return;
            var button = LandscapeUI.Button("RecruitmentPin", _frame, "RECRUITMENT", 0, 0, 210, 48);
            button.onClick.AddListener(() => RecruitPackagePanel.Instance.Show(Point, "RECRUITMENT"));
            _pin = button.transform as RectTransform;
            CityMissionHUD.Style(_pin);
            var label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label) { label.color = Color.white; label.fontSize = 20f; }
        }
        Vector3 projected = cam.WorldToScreenPoint(transform.position + Vector3.up * 4f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_frame, projected, null, out var local);
        float x = local.x - _frame.rect.xMin, y = _frame.rect.yMax - local.y;
        var pinRect = new Rect(x - 105f, y - 24f, 210f, 48f);
        bool visible = projected.z > 0.1f && x > 250f && x < _frame.rect.width - 190f && y > 120f && y < _frame.rect.height - 70f
            && !WorldButtonLayer.ChoiceCovers(pinRect);
        _pin.gameObject.SetActive(visible);
        if (visible) LandscapeUI.Place(_pin, x - 105f, y - 24f, 210f, 48f);
    }

    void OnDestroy() { if (_pin) Destroy(_pin.gameObject); }
}

/// <summary>Same camera-facing title and buttons used when the crew meets a rival.</summary>
public sealed class WorldChoiceBar : MonoBehaviour
{
    RectTransform _frame, _title, _close;
    readonly List<RectTransform> _buttons = new List<RectTransform>();
    bool _open;
    bool _allowClose = true;
    public bool IsOpen => _open;

    public static void Present(Transform anchor, string title, params (string label, Color color, Action action)[] options)
    {
        Present(anchor, title, true, options);
    }

    public static void Present(Transform anchor, string title, bool closable, params (string label, Color color, Action action)[] options)
    {
        if (!anchor) return;
        var bar = anchor.GetComponent<WorldChoiceBar>() ?? anchor.gameObject.AddComponent<WorldChoiceBar>();
        bar._allowClose = closable;
        bar.Build(title, options);
    }

    void Build(string title, (string label, Color color, Action action)[] options)
    {
        Clear();
        _frame = WorldButtonLayer.ChoiceFrame();
        if (!_frame) return;
        var titleGo = new GameObject("ChoiceTitle", typeof(RectTransform));
        titleGo.transform.SetParent(_frame, false);
        var text = titleGo.AddComponent<TextMeshProUGUI>();
        text.text = title.ToUpperInvariant();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 26f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        _title = titleGo.GetComponent<RectTransform>();
        int count = options == null ? 0 : Mathf.Min(options.Length, 4);
        for (int i = 0; i < count; i++)
        {
            var option = options[i];
            var button = LandscapeUI.Button("Choice" + i, _frame, option.label, 0, 0, 156, 48, "dark");
            var image = button.targetGraphic as UnityEngine.UI.Image;
            if (image) image.color = option.color;
            var act = option.action;
            button.onClick.AddListener(() => { Hide(); act?.Invoke(); });
            var label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label) { label.color = Color.white; label.fontSize = 18f; }
            _buttons.Add(button.transform as RectTransform);
        }
        if (_allowClose)
        {
            var close = LandscapeUI.Button("ChoiceClose", _frame, "X", 0, 0, 48, 48, "dark");
            var closeImage = close.targetGraphic as UnityEngine.UI.Image;
            if (closeImage) closeImage.color = new Color(0.18f, 0.2f, 0.24f);
            close.onClick.AddListener(Hide);
            _close = close.transform as RectTransform;
        }
        _open = true;
    }

    void LateUpdate()
    {
        if (!_open || !_frame || !Camera.main) return;
        Vector3 projected = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 3.2f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_frame, projected, null, out var local);
        float x = local.x - _frame.rect.xMin;
        float y = _frame.rect.yMax - local.y;
        float halfWidth=(164f*_buttons.Count+48f)*.5f;
        bool visible = projected.z > 0.1f && x > 80f && x < _frame.rect.width - 80f && y > 110f && y < _frame.rect.height - 70f;
        SetOn(visible);
        if (!visible) return;
        float row = 156f * _buttons.Count + 8f * Mathf.Max(0, _buttons.Count - 1);
        float start = x - (row + 56f) * 0.5f;
        if (_title) LandscapeUI.Place(_title, start, y - 78f, row + 56f, 36f);
        for (int i = 0; i < _buttons.Count; i++)
            LandscapeUI.Place(_buttons[i], start + i * 164f, y - 24f, 156f, 48f);
        if (_close) LandscapeUI.Place(_close, start + row + 8f, y - 24f, 48f, 48f);
        WorldButtonLayer.CoverChoice(new Rect(start - 8f, y - 86f, row + 72f, 116f));
    }

    public void Hide() { _open = false; SetOn(false); }

    void SetOn(bool on)
    {
        if (_title) _title.gameObject.SetActive(on && _open);
        if (_close) _close.gameObject.SetActive(on && _open);
        foreach (var button in _buttons) if (button) button.gameObject.SetActive(on && _open);
    }

    void Clear()
    {
        if (_title) Destroy(_title.gameObject);
        if (_close) Destroy(_close.gameObject);
        foreach (var button in _buttons) if (button) Destroy(button.gameObject);
        _buttons.Clear();
        _title = _close = null;
    }

    void OnDestroy() { Clear(); }
}

/// <summary>Camera-facing choice panel that stays in the world. The city keeps running.</summary>
public sealed class WorldChoicePanel : MonoBehaviour
{
    public bool IsOpen { get; private set; }
    TextMeshPro _title;

    public static WorldChoicePanel Create(Transform parent)
    {
        var root = new GameObject("WorldChoicePanel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, 6.2f, 0f);
        var panel = root.AddComponent<WorldChoicePanel>();
        panel.BuildPlate();
        panel._title = PanelLabel(root.transform, "RIVAL", new Vector3(0f, 1.15f, -0.08f), 5.5f);
        panel._title.name = "Title";
        root.SetActive(false);
        return panel;
    }

    public void Show(string title, Action confront, Action moveOn, Action close)
    {
        if (_title) _title.text = title.ToUpperInvariant();
        ClearButtons();
        AddButton("CONFRONT", new Vector3(-2.15f, -0.15f, -0.12f), new Color(0.72f, 0.14f, 0.14f), confront);
        AddButton("MOVE ON", new Vector3(0.15f, -0.15f, -0.12f), new Color(0.16f, 0.38f, 0.62f), moveOn);
        AddButton("CLOSE", new Vector3(2.15f, -0.15f, -0.12f), new Color(0.18f, 0.20f, 0.24f), close);
        IsOpen = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        IsOpen = false;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (!cam || !IsOpen) return;
        transform.rotation = cam.transform.rotation;
        float distance = Vector3.Distance(cam.transform.position, transform.position);
        float scale = Mathf.Clamp(distance / 32f, 0.85f, 1.45f);
        transform.localScale = Vector3.one * scale;
    }

    void BuildPlate()
    {
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Plate";
        plate.transform.SetParent(transform, false);
        plate.transform.localScale = new Vector3(6.6f, 2.7f, 0.08f);
        var collider = plate.GetComponent<Collider>();
        if (collider) collider.enabled = false;
        Paint(plate.GetComponent<Renderer>(), new Color(0.05f, 0.06f, 0.08f, 1f));
    }

    void AddButton(string caption, Vector3 localPos, Color color, Action onPressed)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = caption;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(1.9f, 1.15f, 0.16f);
        Paint(go.GetComponent<Renderer>(), color);
        var box = go.GetComponent<BoxCollider>();
        if (box) box.size = new Vector3(1.15f, 1.35f, 2.4f);
        var button = go.AddComponent<WorldChoiceButton>();
        button.Bind(onPressed);
        PanelLabel(transform, caption, localPos + new Vector3(0f, 0f, -0.2f), 3.4f);
    }

    void ClearButtons()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == "Plate" || child.name == "Title") continue;
            Destroy(child.gameObject);
        }
    }

    static TextMeshPro PanelLabel(Transform parent, string text, Vector3 localPos, float size)
    {
        var label = ZoneLabelUtil.Create(parent, text, 0f, size);
        label.name = "Caption";
        label.transform.localPosition = localPos;
        label.transform.localRotation = Quaternion.identity;
        var billboard = label.GetComponent<ZoneLabelBillboard>();
        if (billboard) billboard.enabled = false;
        if (label.rectTransform) label.rectTransform.sizeDelta = new Vector2(8f, 2f);
        return label;
    }

    static void Paint(Renderer renderer, Color color)
    {
        if (!renderer) return;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader) renderer.material = new Material(shader) { color = color };
    }
}

public sealed class WorldChoiceButton : MonoBehaviour
{
    public bool IsLive => _action != null && gameObject.activeInHierarchy;
    Action _action;
    public void Bind(Action action) { _action = action; }
    public void Press() { if (IsLive) _action(); }
}

/// <summary>Small tappable marker in the city. Showing it does not pause the simulation.</summary>
public sealed class WorldInteractBubble : MonoBehaviour
{
    public bool IsLive { get; private set; }
    Action _onTap;
    Transform _ball;
    TextMeshPro _text;
    float _pulse;

    public static WorldInteractBubble Create(Transform parent)
    {
        var root = new GameObject("InteractionBubble");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, 4.4f, 0f);
        var bubble = root.AddComponent<WorldInteractBubble>();

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.transform.SetParent(root.transform, false);
        ball.transform.localScale = Vector3.one * 1.25f;
        var renderer = ball.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader) renderer.material = new Material(shader) { color = new Color(1f, 0.78f, 0.12f, 1f) };
        bubble._ball = ball.transform;

        bubble._text = ZoneLabelUtil.Create(root.transform, "TAP", 1.35f, 5.2f);
        root.SetActive(false);
        return bubble;
    }

    public void Show(string caption, Action onTap)
    {
        _onTap = onTap;
        IsLive = true;
        if (_text) _text.text = caption;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (!IsLive && !gameObject.activeSelf) return;
        IsLive = false;
        _onTap = null;
        gameObject.SetActive(false);
    }

    public void Activate()
    {
        if (IsLive) _onTap?.Invoke();
    }

    void LateUpdate()
    {
        if (!IsLive) return;
        _pulse += Time.deltaTime;
        if (_ball) _ball.localScale = Vector3.one * (1.15f + Mathf.Sin(_pulse * 4.5f) * 0.12f);
    }
}
