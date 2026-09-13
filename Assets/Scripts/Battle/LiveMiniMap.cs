using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Arikan;

/// <summary>
/// Corner circular minimap. Tap to expand into a near-fullscreen square map
/// with drag-to-pan, blinking markers/labels, and a visible close button.
/// </summary>
public class LiveMiniMap : MonoBehaviour
{
    public static LiveMiniMap Instance { get; private set; }

    [Header("View")]
    public float orthographicSize = 28f;
    public float expandedOrthoSize = 42f;
    public float cameraHeight = 55f;
    public float followSmooth = 8f;
    public float panSpeed = 0.085f;
    public int textureSize = 512;

    private Camera _miniCam;
    private RenderTexture _rt;
    private RawImage _raw;
    private RectTransform _iconLayer;
    private RectTransform _frame;
    private RectTransform _borderRt;
    private Canvas _canvas;
    private GameObject _expandedRoot;
    private GameObject _closeBtn;
    private TextMeshProUGUI _legend;
    private TextMeshProUGUI _hint;
    private bool _expanded;
    private Vector2 _compactPos;
    private Vector2 _compactSize;

    private Vector3 _panFocus;
    private bool _dragging;
    private Vector2 _lastPointer;
    private float _savedTimeScale = 1f;
    private bool _savedCamEnabled = true;
    private bool _savedSelectionEnabled = true;

    /// <summary>True while the fullscreen town map is open (main camera frozen).</summary>
    public static bool IsExpanded => Instance != null && Instance._expanded;

    private readonly List<Image> _iconPool = new List<Image>();
    private readonly List<TextMeshProUGUI> _labelPool = new List<TextMeshProUGUI>();
    private int _iconUsed;
    private int _labelUsed;

    private Sprite _playerSprite, _gangSprite, _policeSprite, _recruitSprite;
    private Sprite _uiSprite;

    private struct Mark
    {
        public Vector3 world;
        public Sprite sprite;
        public Color color;
        public float size;
        public string label;
    }

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("LiveMiniMap");
        Instance = go.AddComponent<LiveMiniMap>();
        Instance.Build();
    }

    private void Build()
    {
        if(gameObject.scene.name=="Gameplay") {cameraHeight=300;orthographicSize=100;expandedOrthoSize=350;}
        _uiSprite = null;
        DisableLegacyMinimap();
        BuildSprites();
        BuildCamera();
        BuildUi();
    }

    private void DisableLegacyMinimap()
    {
        foreach (var view in FindObjectsByType<MiniMapView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (view != null) view.gameObject.SetActive(false);
        var legend = GameObject.Find("MiniMapLegend");
        if (legend != null) Destroy(legend);
        var legendCanvas = GameObject.Find("MiniMapLegendCanvas");
        if (legendCanvas != null) Destroy(legendCanvas);
    }

    private void BuildCamera()
    {
        _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
        _rt.antiAliasing = 1;
        _rt.Create();

        var camGo = new GameObject("LiveMiniMapCamera");
        camGo.transform.SetParent(transform, false);
        _miniCam = camGo.AddComponent<Camera>();
        _miniCam.orthographic = true;
        _miniCam.orthographicSize = orthographicSize;
        _miniCam.nearClipPlane = 0.3f;
        _miniCam.farClipPlane = 1500f;
        _miniCam.clearFlags = CameraClearFlags.SolidColor;
        _miniCam.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        _miniCam.targetTexture = _rt;
        _miniCam.depth = -50f;
        _miniCam.cullingMask = ~(1 << 5);
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("LiveMiniMapCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 14000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = LandscapeUI.Resolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        _compactPos = new Vector2(-27f, gameObject.scene.name=="Gameplay" ? -510f : -122f);
        _compactSize = new Vector2(266f, 250f);

        // Dimmer (expanded) — does NOT close on click (use X). Blocks world taps.
        _expandedRoot = new GameObject("ExpandedDim", typeof(RectTransform), typeof(Image));
        _expandedRoot.transform.SetParent(canvasGo.transform, false);
        Stretch(_expandedRoot.GetComponent<RectTransform>());
        var dimImg = _expandedRoot.GetComponent<Image>();
        dimImg.sprite = _uiSprite;
        dimImg.type = Image.Type.Sliced;
        dimImg.color = new Color(0f, 0f, 0f, 0.82f);
        dimImg.raycastTarget = true;
        _expandedRoot.SetActive(false);

        var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(canvasGo.transform, false);
        _borderRt = borderGo.GetComponent<RectTransform>();
        _borderRt.anchorMin = _borderRt.anchorMax = new Vector2(1f, 1f);
        _borderRt.pivot = new Vector2(1f, 1f);
        _borderRt.anchoredPosition = _compactPos;
        _borderRt.sizeDelta = _compactSize + new Vector2(8f, 8f);
        var borderImg = borderGo.GetComponent<Image>();
        borderImg.sprite = LandscapeTheme.Current ? LandscapeTheme.Current.panel : _uiSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.raycastTarget = false;

        var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(Button));
        frameGo.transform.SetParent(canvasGo.transform, false);
        _frame = frameGo.GetComponent<RectTransform>();
        _frame.anchorMin = _frame.anchorMax = new Vector2(1f, 1f);
        _frame.pivot = new Vector2(1f, 1f);
        _frame.anchoredPosition = _compactPos;
        _frame.sizeDelta = _compactSize;

        var frameImg = frameGo.GetComponent<Image>();
        frameImg.sprite = _uiSprite;
        frameImg.type = Image.Type.Simple;
        frameImg.color = Color.white;
        frameGo.GetComponent<Mask>().showMaskGraphic = false;
        frameGo.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (!_expanded) Expand();
        });

        var rawGo = new GameObject("Feed", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(frameGo.transform, false);
        Stretch(rawGo.GetComponent<RectTransform>());
        _raw = rawGo.GetComponent<RawImage>();
        _raw.texture = _rt;
        _raw.color = Color.white;
        _raw.raycastTarget = false;

        // Drag catcher on the feed (expanded only uses this for pan).
        var dragGo = new GameObject("DragCatcher", typeof(RectTransform), typeof(Image));
        dragGo.transform.SetParent(frameGo.transform, false);
        Stretch(dragGo.GetComponent<RectTransform>());
        var dragImg = dragGo.GetComponent<Image>();
        dragImg.color = new Color(1f, 1f, 1f, 0.01f);
        dragImg.raycastTarget = true;
        var drag = dragGo.AddComponent<MiniMapDragCatcher>();
        drag.Owner = this;

        var iconsGo = new GameObject("Icons", typeof(RectTransform));
        iconsGo.transform.SetParent(frameGo.transform, false);
        _iconLayer = iconsGo.GetComponent<RectTransform>();
        Stretch(_iconLayer);

        // Close — always last sibling, high on screen, with real sprite.
        _closeBtn = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        _closeBtn.transform.SetParent(canvasGo.transform, false);
        var crt = _closeBtn.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-18f, -18f);
        crt.sizeDelta = new Vector2(110f, 110f);
        var closeImg = _closeBtn.GetComponent<Image>();
        closeImg.sprite = _uiSprite;
        closeImg.type = Image.Type.Sliced;
        closeImg.color = new Color(0.78f, 0.12f, 0.12f, 1f);
        _closeBtn.GetComponent<Button>().onClick.AddListener(Collapse);
        var closeTxt = new GameObject("X", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        closeTxt.transform.SetParent(_closeBtn.transform, false);
        Stretch(closeTxt.rectTransform);
        closeTxt.text = "X";
        closeTxt.fontSize = 56f;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = Color.white;
        closeTxt.raycastTarget = false;
        _closeBtn.SetActive(false);

        var legendGo = new GameObject("Legend", typeof(RectTransform));
        legendGo.transform.SetParent(canvasGo.transform, false);
        var lrt = legendGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, 18f);
        lrt.sizeDelta = new Vector2(-24f, 56f);
        _legend = legendGo.AddComponent<TextMeshProUGUI>();
        _legend.fontSize = 24f;
        _legend.fontStyle = FontStyles.Bold;
        _legend.alignment = TextAlignmentOptions.Center;
        _legend.color = Color.white;
        _legend.text = "YOU  ·  GANG TURF  ·  RECRUIT  ·  POLICE";
        _legend.gameObject.SetActive(false);

        var hintGo = new GameObject("PanHint", typeof(RectTransform));
        hintGo.transform.SetParent(canvasGo.transform, false);
        var hrt = hintGo.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0.5f, 1f);
        hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -24f);
        hrt.sizeDelta = new Vector2(700f, 40f);
        _hint = hintGo.AddComponent<TextMeshProUGUI>();
        _hint.fontSize = 26f;
        _hint.fontStyle = FontStyles.Bold;
        _hint.alignment = TextAlignmentOptions.Center;
        _hint.color = new Color(1f, 1f, 1f, 0.9f);
        _hint.text = "DRAG MAP TO EXPLORE  ·  TAP X TO CLOSE";
        _hint.gameObject.SetActive(false);
    }

    private void Expand()
    {
        _expanded = true;
        _panFocus = GetPlayerFocus();
        _dragging = false;

        // Freeze main game camera + input — only this map may pan.
        FreezeMainGame(true);

        if (_canvas != null) _canvas.sortingOrder = 28000;

        _expandedRoot.SetActive(true);
        _closeBtn.SetActive(true);
        _closeBtn.transform.SetAsLastSibling();
        _legend.gameObject.SetActive(true);
        _legend.transform.SetAsLastSibling();
        _hint.gameObject.SetActive(true);
        _hint.transform.SetAsLastSibling();
        _closeBtn.transform.SetAsLastSibling();

        // Full-screen map (small margins for hint + legend + X).
        StretchFull(_frame, left: 8f, bottom: 58f, right: 8f, top: 70f);
        StretchFull(_borderRt, left: 0f, bottom: 50f, right: 0f, top: 62f);

        var borderImg = _borderRt.GetComponent<Image>();
        borderImg.sprite = _uiSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.85f, 0.16f, 0.16f, 1f);

        var frameImg = _frame.GetComponent<Image>();
        frameImg.sprite = _uiSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.color = Color.white;

        var btn = _frame.GetComponent<Button>();
        if (btn != null) btn.enabled = false;

        if (_miniCam != null)
            _miniCam.orthographicSize = expandedOrthoSize;
        SnapCamToPan();
    }

    private void Collapse()
    {
        _expanded = false;
        _dragging = false;

        FreezeMainGame(false);

        if (_canvas != null) _canvas.sortingOrder = 14000;

        _expandedRoot.SetActive(false);
        _closeBtn.SetActive(false);
        _legend.gameObject.SetActive(false);
        _hint.gameObject.SetActive(false);

        _frame.anchorMin = _frame.anchorMax = new Vector2(1f, 1f);
        _frame.pivot = new Vector2(1f, 1f);
        _frame.offsetMin = Vector2.zero;
        _frame.offsetMax = Vector2.zero;
        _frame.anchoredPosition = _compactPos;
        _frame.sizeDelta = _compactSize;

        _borderRt.anchorMin = _borderRt.anchorMax = new Vector2(1f, 1f);
        _borderRt.pivot = new Vector2(1f, 1f);
        _borderRt.offsetMin = Vector2.zero;
        _borderRt.offsetMax = Vector2.zero;
        _borderRt.anchoredPosition = _compactPos;
        _borderRt.sizeDelta = _compactSize + new Vector2(8f, 8f);
        _borderRt.GetComponent<Image>().sprite = LandscapeTheme.Current ? LandscapeTheme.Current.panel : _uiSprite;
        _borderRt.GetComponent<Image>().type = Image.Type.Sliced;
        _borderRt.GetComponent<Image>().color = Color.white;

        var frameImg = _frame.GetComponent<Image>();
        frameImg.sprite = _uiSprite;
        frameImg.type = Image.Type.Simple;
        frameImg.color = Color.white;

        var btn = _frame.GetComponent<Button>();
        if (btn != null) btn.enabled = true;

        if (_miniCam != null) _miniCam.orthographicSize = orthographicSize;
    }

    private void FreezeMainGame(bool freeze)
    {
        if (freeze)
        {
            _savedTimeScale = Time.timeScale;
            if (_savedTimeScale <= 0.001f) _savedTimeScale = 1f;
            Time.timeScale = 0f;

            var cam = CameraPanTouchOnly.Instance;
            if (cam != null)
            {
                _savedCamEnabled = cam.enabled;
                cam.enabled = false;
            }

            var sel = AgentSelectionManager.instance;
            if (sel != null)
            {
                _savedSelectionEnabled = sel.enabled;
                sel.enabled = false;
            }
        }
        else
        {
            Time.timeScale = _savedTimeScale > 0.001f ? _savedTimeScale : 1f;

            var cam = CameraPanTouchOnly.Instance;
            if (cam != null) cam.enabled = _savedCamEnabled;

            var sel = AgentSelectionManager.instance;
            if (sel != null) sel.enabled = _savedSelectionEnabled;
        }
    }

    private static void StretchFull(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // ── Drag pan (called from MiniMapDragCatcher) ─────────────────────────
    public void OnMapPointerDown(Vector2 screenPos)
    {
        if (!_expanded) return;
        _dragging = true;
        _lastPointer = screenPos;
    }

    public void OnMapPointerDrag(Vector2 screenPos)
    {
        if (!_expanded || !_dragging || _miniCam == null) return;
        Vector2 delta = screenPos - _lastPointer;
        _lastPointer = screenPos;

        // Screen drag → world XZ (orthographic top-down). Only moves minimap cam.
        float mapH = Mathf.Max(200f, _frame.rect.height);
        float worldPerPixel = (_miniCam.orthographicSize * 2f) / mapH;
        _panFocus.x -= delta.x * worldPerPixel;
        _panFocus.z -= delta.y * worldPerPixel;
        // Soft town bounds so you can explore the whole map.
        _panFocus.x = Mathf.Clamp(_panFocus.x, -1000f, 1450f);
        _panFocus.z = Mathf.Clamp(_panFocus.z, -600f, 450f);
        SnapCamToPan();
    }

    public void OnMapPointerUp()
    {
        _dragging = false;
    }

    private void SnapCamToPan()
    {
        if (_miniCam == null) return;
        _miniCam.transform.position = new Vector3(_panFocus.x, cameraHeight, _panFocus.z);
    }

    private void LateUpdate()
    {
        if (_miniCam == null) return;
        if (!_expanded)
            UpdateCameraFollow();
        else if (!_dragging)
            SnapCamToPan(); // keep stable while idle
        RefreshIcons();
    }

    private void UpdateCameraFollow()
    {
        Vector3 focus = GetPlayerFocus();
        Vector3 target = new Vector3(focus.x, cameraHeight, focus.z);
        _miniCam.transform.position = Vector3.Lerp(
            _miniCam.transform.position, target, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
    }

    private Vector3 GetPlayerFocus()
    {
        Vector3 focus = Vector3.zero;
        int n = 0;
        if (BattleManager.instance != null)
        {
            foreach (var a in BattleManager.instance.PlayerAgents)
            {
                if (a != null && a.IsAlive) { focus += a.transform.position; n++; }
            }
        }
        if (n > 0) return focus / n;

        if (Camera.main != null)
        {
            var ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
                return ray.GetPoint(enter);
            return Camera.main.transform.position;
        }
        return Vector3.zero;
    }

    private void RefreshIcons()
    {
        _iconUsed = 0;
        _labelUsed = 0;
        if (BattleManager.instance == null) { HideUnused(); return; }

        float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
        var marks = new List<Mark>(32);

        foreach (var a in BattleManager.instance.PlayerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            marks.Add(new Mark
            {
                world = a.transform.position,
                sprite = _playerSprite,
                color = new Color(0.25f, 1f, 0.55f, blink),
                size = _expanded ? 40f : 20f,
                label = _expanded ? "YOU" : null
            });
        }

        foreach (var g in FindObjectsByType<GangArea>(FindObjectsSortMode.None))
        {
            if (g == null) continue;
            marks.Add(new Mark
            {
                world = g.transform.position,
                sprite = _gangSprite,
                color = new Color(1f, 0.25f, 0.15f, blink),
                size = _expanded ? 44f : 24f,
                label = _expanded ? g.GangName.ToUpperInvariant() : null
            });
        }

        foreach (var r in FindObjectsByType<RecruitArea>(FindObjectsSortMode.None))
        {
            if (r == null) continue;
            marks.Add(new Mark
            {
                world = r.transform.position,
                sprite = _recruitSprite,
                color = new Color(1f, 0.85f, 0.15f, blink),
                size = _expanded ? 42f : 22f,
                label = _expanded ? r.AreaName : null
            });
        }

        if (LivePoliceSystem.Instance != null)
        {
            foreach (var car in LivePoliceSystem.Instance.ActivePatrolCars)
            {
                if (car == null) continue;
                marks.Add(new Mark
                {
                    world = car.transform.position,
                    sprite = _policeSprite,
                    color = new Color(0.25f, 0.55f, 1f, blink),
                    size = _expanded ? 40f : 20f,
                    label = _expanded ? "POLICE" : null
                });
            }
            if (LivePoliceSystem.Instance.ResponseCar != null)
            {
                marks.Add(new Mark
                {
                    world = LivePoliceSystem.Instance.ResponseCar.transform.position,
                    sprite = _policeSprite,
                    color = new Color(0.15f, 0.45f, 1f, blink),
                    size = _expanded ? 42f : 22f,
                    label = _expanded ? "POLICE" : null
                });
            }
        }

        if (!_expanded)
        {
            foreach (var e in BattleManager.instance.EnemyAgents)
            {
                if (e == null || !e.IsAlive) continue;
                if (e.firmName == "POLICE")
                    marks.Add(new Mark { world = e.transform.position, sprite = _policeSprite, color = new Color(0.35f, 0.65f, 1f), size = 14f });
                else
                    marks.Add(new Mark { world = e.transform.position, sprite = _gangSprite, color = e.primaryColor.a > 0.01f ? e.primaryColor : new Color(0.95f, 0.2f, 0.2f), size = 14f });
            }
        }

        foreach (var m in marks)
            PlaceMark(m);

        HideUnused();
    }

    private void PlaceMark(Mark m)
    {
        if (_miniCam == null || _iconLayer == null) return;

        Vector3 camPos = _miniCam.transform.position;
        float half = _miniCam.orthographicSize;
        float nx = (m.world.x - camPos.x) / (half * 2f) + 0.5f;
        float ny = (m.world.z - camPos.z) / (half * 2f) + 0.5f;
        if (nx < -0.05f || nx > 1.05f || ny < -0.05f || ny > 1.05f) return;

        var img = GetIcon();
        img.sprite = m.sprite;
        img.color = m.color;
        img.enabled = true;
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(m.size, m.size);
        rt.anchorMin = rt.anchorMax = new Vector2(nx, ny);
        rt.anchoredPosition = Vector2.zero;

        if (!string.IsNullOrEmpty(m.label))
        {
            var lab = GetLabel();
            lab.text = m.label;
            lab.enabled = true;
            lab.fontSize = 24f;
            lab.fontStyle = FontStyles.Bold;
            lab.color = Color.white;
            lab.outlineWidth = 0.3f;
            lab.outlineColor = Color.black;
            var lrt = lab.rectTransform;
            lrt.sizeDelta = new Vector2(260f, 40f);
            lrt.anchorMin = lrt.anchorMax = new Vector2(nx, ny);
            lrt.anchoredPosition = new Vector2(0f, m.size * 0.8f + 12f);
        }
    }

    private Image GetIcon()
    {
        if (_iconUsed < _iconPool.Count)
        {
            var img = _iconPool[_iconUsed++];
            img.gameObject.SetActive(true);
            return img;
        }
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_iconLayer, false);
        go.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        var imgNew = go.GetComponent<Image>();
        imgNew.raycastTarget = false;
        _iconPool.Add(imgNew);
        _iconUsed++;
        return imgNew;
    }

    private TextMeshProUGUI GetLabel()
    {
        if (_labelUsed < _labelPool.Count)
        {
            var t = _labelPool[_labelUsed++];
            t.gameObject.SetActive(true);
            return t;
        }
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(_iconLayer, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        _labelPool.Add(tmp);
        _labelUsed++;
        return tmp;
    }

    private void HideUnused()
    {
        for (int i = _iconUsed; i < _iconPool.Count; i++)
            _iconPool[i].gameObject.SetActive(false);
        for (int i = _labelUsed; i < _labelPool.Count; i++)
            _labelPool[i].gameObject.SetActive(false);
    }

    private void BuildSprites()
    {
        _playerSprite = MakeDiamondSprite(new Color(0.2f, 1f, 0.6f));
        _gangSprite = MakeSoftSquare(new Color(1f, 0.25f, 0.2f));
        _policeSprite = MakeSoftSquare(new Color(0.3f, 0.55f, 1f));
        _recruitSprite = MakeSoftSquare(new Color(1f, 0.85f, 0.2f));
    }

    private static Sprite MakeDiamondSprite(Color color)
    {
        const int s = 48;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = (Mathf.Abs(x - c) + Mathf.Abs(y - c)) / c;
                if (d < 0.75f) tex.SetPixel(x, y, color);
                else if (d < 0.95f) tex.SetPixel(x, y, Color.white);
                else tex.SetPixel(x, y, Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }

    private static Sprite MakeSoftSquare(Color color)
    {
        const int s = 48;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        int m = 6;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool inside = x >= m && x < s - m && y >= m && y < s - m;
                bool edge = inside && (x == m || x == s - m - 1 || y == m || y == s - m - 1);
                if (edge) tex.SetPixel(x, y, Color.white);
                else if (inside) tex.SetPixel(x, y, color);
                else tex.SetPixel(x, y, Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }

    private static Sprite MakeCircleSprite(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = size * 0.5f - 1f;
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                tex.SetPixel(x, y, d <= r ? color : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite MakeRingSprite(int size, Color color, int thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float outer = size * 0.5f - 1f;
        float inner = outer - thickness;
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                tex.SetPixel(x, y, (d <= outer && d >= inner) ? color : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (_expanded)
            FreezeMainGame(false);

        if (Instance == this) Instance = null;
        if (_miniCam != null) _miniCam.targetTexture = null;
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }
    }
}

/// <summary>Forwards pointer drag events from the expanded map feed to LiveMiniMap.</summary>
public class MiniMapDragCatcher : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public LiveMiniMap Owner;

    public void OnPointerDown(PointerEventData eventData)
    {
        Owner?.OnMapPointerDown(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Owner?.OnMapPointerDrag(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Owner?.OnMapPointerUp();
    }
}
