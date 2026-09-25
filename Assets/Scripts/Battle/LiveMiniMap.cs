using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Arikan;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

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
    private RectTransform _feedRt;
    private RectTransform _washRt;
    private RectTransform _dragRt;
    private Canvas _canvas;
    private GameObject _expandedRoot;
    private GameObject _closeBtn;
    private GameObject _zoomInBtn;
    private GameObject _zoomOutBtn;
    private TextMeshProUGUI _legend;
    private TextMeshProUGUI _hint;
    private bool _expanded;
    private bool _compactVisible=true;
    private Vector2 _compactPos;
    private Vector2 _compactSize;
    private float _minExpandedZoom;
    private float _maxExpandedZoom;
    private float _lastPinchDistance;
    private float _nextIconRefresh;
    private float _nextSceneScan;
    private float _nextMapRender;
    private readonly List<Mark> _marks = new List<Mark>(48);
    private GangArea[] _gangAreas = new GangArea[0];
    private RecruitArea[] _recruitAreas = new RecruitArea[0];
    private TerritoryControlPoint[] _territoryPoints = new TerritoryControlPoint[0];
    private MatchdayGroupMarker[] _matchdayGroups = new MatchdayGroupMarker[0];
    private Vector2 _lastFrameSize = new Vector2(-1f, -1f);

    private Vector3 _panFocus;
    private bool _dragging;
    private Vector2 _lastPointer;
    private bool _savedCamEnabled = true;
    private bool _savedSelectionEnabled = true;

    /// <summary>True while the fullscreen town map is open (main camera frozen).</summary>
    public static bool IsExpanded => Instance != null && Instance._expanded;
    public int ActiveLabelCount => _labelUsed;
    public bool LabelPoolHealthy
    {
        get
        {
            var seen = new HashSet<TextMeshProUGUI>();
            foreach (var label in _labelPool)
                if (label == null || !seen.Add(label)) return false;
            return true;
        }
    }

    public bool RunDragResponseSelfCheck()
    {
        if (_miniCam == null || _frame == null) return false;
        bool wasExpanded = _expanded;
        Vector3 savedFocus = _panFocus;
        float savedZoom = _miniCam.orthographicSize;
        if (!wasExpanded) Expand();
        SnapCamToPan();
        RefreshIcons();
        TextMeshProUGUI visible = null;
        foreach (var label in _labelPool)
            if (label && label.transform.parent.gameObject.activeInHierarchy) { visible = label; break; }
        if (!visible)
        {
            if (!wasExpanded) Collapse();
            return false;
        }
        var host = visible.transform.parent as RectTransform;
        Vector2 before = host.anchorMin;
        OnMapPointerDown(Vector2.zero);
        OnMapPointerDrag(new Vector2(96f, 48f));
        LayoutCachedMarks();
        OnMapPointerUp();
        bool moved = (host.anchorMin - before).sqrMagnitude > .000001f;
        _panFocus = savedFocus;
        _miniCam.orthographicSize = savedZoom;
        SnapCamToPan();
        LayoutCachedMarks();
        if (!wasExpanded) Collapse();
        return moved && LabelPoolHealthy;
    }

    private readonly List<Image> _iconPool = new List<Image>();
    private readonly List<TextMeshProUGUI> _labelPool = new List<TextMeshProUGUI>();
    private int _iconUsed;
    private int _labelUsed;

    private Sprite _playerSprite, _gangSprite, _policeSprite, _recruitSprite, _objectiveSprite;
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
        if(gameObject.scene.name=="Gameplay")
        {
            cameraHeight=300;orthographicSize=46;expandedOrthoSize=70;
            _minExpandedZoom=55f;_maxExpandedZoom=480f;
        }
        else {_minExpandedZoom=12f;_maxExpandedZoom=90f;}
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
        _rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        _rt.antiAliasing = 1;
        _rt.Create();

        var camGo = new GameObject("LiveMiniMapCamera");
        camGo.transform.SetParent(transform, false);
        _miniCam = camGo.AddComponent<Camera>();
        _miniCam.orthographic = true;
        _miniCam.orthographicSize = orthographicSize;
        _miniCam.aspect = 1f;
        _miniCam.nearClipPlane = 0.3f;
        _miniCam.farClipPlane = 1500f;
        _miniCam.clearFlags = CameraClearFlags.SolidColor;
        _miniCam.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        _miniCam.targetTexture = _rt;
        _miniCam.depth = -50f;
        _miniCam.cullingMask = ~(1 << 5);
        _miniCam.enabled = false;
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
        LandscapeUI.ConfigureLandscapeScaler(canvasGo.GetComponent<CanvasScaler>());

        var safeGo = new GameObject("SafeArea", typeof(RectTransform));
        safeGo.transform.SetParent(canvasGo.transform, false);
        Stretch(safeGo.GetComponent<RectTransform>());
        safeGo.AddComponent<MiniMapSafeArea>();
        Transform uiRoot = safeGo.transform;

        // Gameplay: leave bottom-right margin; stack CAMERA / RECENTRE under the map.
        if (gameObject.scene.name == "Gameplay")
        {
            _compactSize = new Vector2(220f, 220f);
            // Aligns with camera buttons stacked under the map (bottom margin ~22).
            _compactPos = new Vector2(-24f, -430f);
        }
        else
        {
            _compactPos = new Vector2(-24f, -122f);
            _compactSize = new Vector2(220f, 200f);
        }

        // Dimmer (expanded) — does NOT close on click (use X). Blocks world taps.
        _expandedRoot = new GameObject("ExpandedDim", typeof(RectTransform), typeof(Image));
        _expandedRoot.transform.SetParent(uiRoot, false);
        Stretch(_expandedRoot.GetComponent<RectTransform>());
        var dimImg = _expandedRoot.GetComponent<Image>();
        dimImg.sprite = _uiSprite;
        dimImg.type = Image.Type.Sliced;
        dimImg.color = new Color(0f, 0f, 0f, 0.82f);
        dimImg.raycastTarget = true;
        _expandedRoot.SetActive(false);

        var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(uiRoot, false);
        _borderRt = borderGo.GetComponent<RectTransform>();
        _borderRt.anchorMin = _borderRt.anchorMax = new Vector2(1f, 1f);
        _borderRt.pivot = new Vector2(1f, 1f);
        _borderRt.anchoredPosition = _compactPos;
        _borderRt.sizeDelta = _compactSize + new Vector2(8f, 8f);
        var borderImg = borderGo.GetComponent<Image>();
        borderImg.sprite = LandscapeTheme.Current ? LandscapeTheme.Current.panel : _uiSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color=new Color(.025f,.08f,.10f,.98f);
        borderImg.raycastTarget = false;
        BuildNeonEdges(_borderRt);

        var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(Button));
        frameGo.transform.SetParent(uiRoot, false);
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
        _feedRt = rawGo.GetComponent<RectTransform>();
        Stretch(_feedRt);
        _raw = rawGo.GetComponent<RawImage>();
        _raw.texture = _rt;
        _raw.color = Color.white;
        _raw.raycastTarget = false;

        // Muted tactical wash keeps city geometry readable without competing
        // with the high-contrast squad, rival, police and objective symbols.
        var washGo = new GameObject("TacticalWash", typeof(RectTransform), typeof(Image));
        washGo.transform.SetParent(frameGo.transform, false);
        _washRt = washGo.GetComponent<RectTransform>();
        Stretch(_washRt);
        var wash = washGo.GetComponent<Image>();
        wash.color = new Color(0.01f, 0.02f, 0.03f, 0.08f);
        wash.raycastTarget = false;

        // Drag catcher on the feed (expanded only uses this for pan).
        var dragGo = new GameObject("DragCatcher", typeof(RectTransform), typeof(Image));
        dragGo.transform.SetParent(frameGo.transform, false);
        _dragRt = dragGo.GetComponent<RectTransform>();
        Stretch(_dragRt);
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
        _closeBtn.transform.SetParent(uiRoot, false);
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

        _zoomInBtn = BuildZoomButton(uiRoot, "ZoomIn", "+", new Vector2(-22f, -154f), () => AdjustZoom(0.78f));
        _zoomOutBtn = BuildZoomButton(uiRoot, "ZoomOut", "−", new Vector2(-22f, -246f), () => AdjustZoom(1.28f));
        _zoomInBtn.SetActive(false);
        _zoomOutBtn.SetActive(false);

        var legendGo = new GameObject("Legend", typeof(RectTransform));
        legendGo.transform.SetParent(uiRoot, false);
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
        _legend.text = GameManager.IsPolicePlayer
            ? "<color=#4E8FFF>◆ POLICE UNIT</color>     <color=#FF4935>■ RIVAL</color>     <color=#FFD936>● OBJECTIVE</color>"
            : "<color=#48FF91>◆ YOU</color>     <color=#FF4935>■ RIVAL</color>     <color=#FFD936>● OBJECTIVE</color>     <color=#4E8FFF>■ POLICE</color>";
        _legend.gameObject.SetActive(false);

        var hintGo = new GameObject("PanHint", typeof(RectTransform));
        hintGo.transform.SetParent(uiRoot, false);
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
        _hint.text = "DRAG TO EXPLORE  ·  PINCH / + − TO ZOOM  ·  TAP X TO CLOSE";
        _hint.gameObject.SetActive(false);
    }

    private GameObject BuildZoomButton(Transform parent, string name, string glyph, Vector2 position,
                                       UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(76f, 76f);
        var image = go.GetComponent<Image>();
        image.sprite = LandscapeTheme.Current ? LandscapeTheme.Current.darkButton : _uiSprite;
        image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = image.sprite != null ? Color.white : new Color(0.08f, 0.11f, 0.14f, 0.96f);
        go.GetComponent<Button>().onClick.AddListener(action);
        var text = new GameObject("Glyph", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(go.transform, false);
        Stretch(text.rectTransform);
        text.text = glyph;
        text.fontSize = 48f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return go;
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
        _frame.gameObject.SetActive(true);_borderRt.gameObject.SetActive(true);
        _closeBtn.SetActive(true);
        _zoomInBtn.SetActive(true);
        _zoomOutBtn.SetActive(true);
        _closeBtn.transform.SetAsLastSibling();
        _zoomInBtn.transform.SetAsLastSibling();
        _zoomOutBtn.transform.SetAsLastSibling();
        _legend.gameObject.SetActive(false);
        _hint.gameObject.SetActive(false);
        _closeBtn.transform.SetAsLastSibling();

        // Full-screen map (small margins for hint + legend + X).
        StretchFull(_expandedRoot.GetComponent<RectTransform>(), left: 0f, bottom: 128f, right: 0f, top: 0f);
        StretchFull(_frame, left: 8f, bottom: 136f, right: 8f, top: 70f);
        StretchFull(_borderRt, left: 0f, bottom: 128f, right: 0f, top: 62f);

        var borderImg = _borderRt.GetComponent<Image>();
        borderImg.sprite = _uiSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.015f, 0.07f, 0.09f, 1f);

        var frameImg = _frame.GetComponent<Image>();
        frameImg.sprite = _uiSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.color = Color.white;

        var btn = _frame.GetComponent<Button>();
        if (btn != null) btn.enabled = false;

        if (_miniCam != null)
        {
            _panFocus = GetPlayerFocus();
            _miniCam.orthographicSize = expandedOrthoSize;
            _miniCam.aspect = 1f;
        }
        SnapCamToPan();
        _nextIconRefresh = 0f;
        _nextMapRender = 0f;
        RefreshIcons();
    }

    private void Collapse()
    {
        _expanded = false;
        _dragging = false;

        FreezeMainGame(false);

        if (_canvas != null) _canvas.sortingOrder = 14000;

        _expandedRoot.SetActive(false);
        _closeBtn.SetActive(false);
        _zoomInBtn.SetActive(false);
        _zoomOutBtn.SetActive(false);
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
        _borderRt.GetComponent<Image>().color = new Color(.025f,.08f,.10f,.98f);

        var frameImg = _frame.GetComponent<Image>();
        frameImg.sprite = _uiSprite;
        frameImg.type = Image.Type.Simple;
        frameImg.color = Color.white;

        var btn = _frame.GetComponent<Button>();
        if (btn != null) btn.enabled = true;

        if (_miniCam != null) _miniCam.orthographicSize = orthographicSize;
        _frame.gameObject.SetActive(_compactVisible);_borderRt.gameObject.SetActive(_compactVisible);
    }

    public void SetCompactVisible(bool visible)
    {
        _compactVisible=visible;
        if(_expanded)return;
        if(_frame)_frame.gameObject.SetActive(visible);
        if(_borderRt)_borderRt.gameObject.SetActive(visible);
    }

    static void BuildNeonEdges(RectTransform parent)
    {
        Color glow=new Color(.10f,1f,.92f,.95f);
        Edge("NeonTop",new Vector2(0,1),new Vector2(1,1),new Vector2(0,-2),new Vector2(0,4));
        Edge("NeonBottom",new Vector2(0,0),new Vector2(1,0),new Vector2(0,2),new Vector2(0,4));
        Edge("NeonLeft",new Vector2(0,0),new Vector2(0,1),new Vector2(2,0),new Vector2(4,0));
        Edge("NeonRight",new Vector2(1,0),new Vector2(1,1),new Vector2(-2,0),new Vector2(4,0));
        void Edge(string name,Vector2 min,Vector2 max,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;
            var image=go.GetComponent<Image>();image.color=glow;image.raycastTarget=false;
            var outline=image.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.1f,1f,.92f,.38f);outline.effectDistance=new Vector2(3,-3);
        }
    }

    private void FreezeMainGame(bool freeze)
    {
        if (freeze)
        {
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
        float mapW = Mathf.Max(200f, _frame.rect.width);
        float mapH = Mathf.Max(200f, _frame.rect.height);
        float halfH = _miniCam.orthographicSize;
        float halfW = halfH * Mathf.Max(0.5f, _miniCam.aspect);
        _panFocus.x -= delta.x * (halfW * 2f) / mapW;
        _panFocus.z -= delta.y * (halfH * 2f) / mapH;
        // Soft town bounds so you can explore the whole map.
        _panFocus.x = Mathf.Clamp(_panFocus.x, -1000f, 1450f);
        _panFocus.z = Mathf.Clamp(_panFocus.z, -600f, 450f);
        SnapCamToPan();
    }

    public void OnMapPointerUp()
    {
        _dragging = false;
    }

    public void OnMapScroll(float scrollDelta)
    {
        if (!_expanded || Mathf.Approximately(scrollDelta, 0f)) return;
        AdjustZoom(scrollDelta > 0f ? 0.82f : 1.22f);
    }

    private void AdjustZoom(float multiplier)
    {
        if (!_expanded || _miniCam == null) return;
        _miniCam.orthographicSize = Mathf.Clamp(
            _miniCam.orthographicSize * multiplier, _minExpandedZoom, _maxExpandedZoom);
        _nextMapRender = 0f;
        LayoutCachedMarks();
    }

    void FitMapFrame()
    {
        if (_frame == null || _miniCam == null) return;
        float w = _frame.rect.width;
        float h = _frame.rect.height;
        if (w < 8f || h < 8f) return;
        Vector2 size = new Vector2(w, h);
        if ((size - _lastFrameSize).sqrMagnitude < .01f) return;
        _lastFrameSize = size;
        foreach (var rt in new[] { _feedRt, _washRt, _dragRt, _iconLayer })
        {
            if (!rt) continue;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
        _miniCam.aspect = w / h;
    }

    void FrameWholeCity()
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        int n = 0;
        void Enc(Vector3 p)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            n++;
        }
        var city = CityGameplay.Instance;
        if (city != null && city.Locations != null)
            foreach (var p in city.Locations) Enc(p);
        foreach (var g in FindObjectsByType<GangArea>(FindObjectsSortMode.None))
            if (g) Enc(g.transform.position);
        if (BattleManager.instance != null)
            foreach (var a in BattleManager.instance.PlayerAgents)
                if (a && a.IsAlive) Enc(a.transform.position);
        if (n == 0 || _miniCam == null) return;
        _panFocus = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        float span = Mathf.Max(maxX - minX, maxZ - minZ);
        _miniCam.orthographicSize = Mathf.Clamp(span * 0.62f + 12f, 48f, 420f);
    }

    static string MapName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        value = value.ToUpperInvariant();
        return value.Length <= 14 ? value : value.Substring(0, 13);
    }

    static Color AreaColor(int index)
    {
        switch (index)
        {
            case 0: return new Color(0.25f, 0.85f, 1f);
            case 1: return new Color(1f, 0.55f, 0.15f);
            case 2: return new Color(1f, 0.82f, 0.15f);
            case 4: return new Color(0.35f, 1f, 0.7f);
            case 5: return new Color(0.95f, 0.95f, 1f);
            default: return new Color(0.55f, 0.75f, 1f);
        }
    }

    private void SnapCamToPan()
    {
        if (_miniCam == null) return;
        _miniCam.transform.position = new Vector3(_panFocus.x, cameraHeight, _panFocus.z);
    }

    private void LateUpdate()
    {
        if (_miniCam == null) return;
        FitMapFrame();
        UpdatePinchZoom();
        if (!_expanded)
            UpdateCameraFollow();
        else if (!_dragging)
            SnapCamToPan(); // keep stable while idle
        RenderMapWhenDue();
        if (Time.unscaledTime >= _nextIconRefresh)
        {
            _nextIconRefresh = Time.unscaledTime + (_expanded ? 0.06f : 0.12f);
            RefreshIcons();
        }
        else if (_expanded && _dragging)
        {
            // The world snapshot need not be rebuilt every frame, but UI-space
            // positions must track the camera immediately while dragging.
            LayoutCachedMarks();
        }
    }

    private void RenderMapWhenDue()
    {
        if (_miniCam == null) return;
        if (Time.unscaledTime < _nextMapRender) return;
        _nextMapRender = Time.unscaledTime + (_expanded ? 0.05f : 0.10f);
        _miniCam.Render();
    }

    private void UpdatePinchZoom()
    {
        if (!_expanded || Touch.activeTouches.Count < 2)
        {
            _lastPinchDistance = 0f;
            return;
        }
        float distance = Vector2.Distance(Touch.activeTouches[0].screenPosition, Touch.activeTouches[1].screenPosition);
        if (_lastPinchDistance > 1f && distance > 1f)
            AdjustZoom(_lastPinchDistance / distance);
        _lastPinchDistance = distance;
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
        var selected = AgentSelectionManager.instance != null ? AgentSelectionManager.instance.SelectedAgents : null;
        if (selected != null)
        {
            foreach (var a in selected)
            {
                if (a != null && a.IsAlive) { focus += a.transform.position; n++; }
            }
        }
        if (n == 0 && BattleManager.instance != null)
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
        if (BattleManager.instance == null)
        {
            _marks.Clear();
            LayoutCachedMarks();
            return;
        }

        float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
        RefreshSceneMarkerCache();
        _marks.Clear();

        foreach (var a in BattleManager.instance.PlayerAgents)
        {
            if (a == null || !a.IsAlive) continue;
            _marks.Add(new Mark
            {
                world = a.transform.position,
                sprite = _playerSprite,
                color = GameManager.IsPolicePlayer
                    ? new Color(0.28f, 0.58f, 1f, blink)
                    : new Color(0.25f, 1f, 0.55f, blink),
                size = _expanded ? 22f : 14f,
                label = null
            });
        }

        foreach (var g in _gangAreas)
        {
            if (g == null || !g.gameObject.activeInHierarchy) continue;
            _marks.Add(new Mark
            {
                world = g.transform.position,
                sprite = _gangSprite,
                color = g.ZoneColor.a > 0.2f ? g.ZoneColor : new Color(1f, 0.25f, 0.15f, blink),
                size = _expanded ? 34f : 18f,
                label = MapName(g.GangName)
            });
        }

        foreach (var r in _recruitAreas)
        {
            if (r == null || !r.gameObject.activeInHierarchy) continue;
            _marks.Add(new Mark
            {
                world = r.transform.position,
                sprite = _recruitSprite,
                color = new Color(1f, 0.85f, 0.15f, blink),
                size = _expanded ? 30f : 16f,
                label = MapName(r.AreaName)
            });
        }

        foreach (var objective in _territoryPoints)
        {
            if (objective == null || !objective.gameObject.activeInHierarchy) continue;
            _marks.Add(new Mark
            {
                world = objective.transform.position,
                sprite = _objectiveSprite,
                color = new Color(1f, 0.82f, 0.12f, blink),
                size = _expanded ? 26f : 14f,
                label = MapName(objective.zoneName)
            });
        }

        foreach (var group in _matchdayGroups)
        {
            if (group == null || !group.gameObject.activeInHierarchy) continue;
            bool police = group.Role == MatchdayUnitRole.Police;
            bool home = group.Role == MatchdayUnitRole.HomeSupporter;
            _marks.Add(new Mark
            {
                world = group.transform.position,
                sprite = police ? _policeSprite : (home ? _playerSprite : _gangSprite),
                color = group.GroupColor,
                size = _expanded ? 32f : 17f,
                label = MapName(group.GroupName)
            });
        }

        if (LivePoliceSystem.Instance != null)
        {
            foreach (var car in LivePoliceSystem.Instance.ActivePatrolCars)
            {
                if (car == null) continue;
                _marks.Add(new Mark
                {
                    world = car.transform.position,
                    sprite = _policeSprite,
                    color = new Color(0.25f, 0.55f, 1f, blink),
                    size = _expanded ? 28f : 16f,
                    label = "POLICE"
                });
            }
            if (LivePoliceSystem.Instance.ResponseCar != null)
            {
                _marks.Add(new Mark
                {
                    world = LivePoliceSystem.Instance.ResponseCar.transform.position,
                    sprite = _policeSprite,
                    color = new Color(0.15f, 0.45f, 1f, blink),
                    size = _expanded ? 28f : 16f,
                    label = "POLICE"
                });
            }
        }

        foreach (var e in BattleManager.instance.EnemyAgents)
        {
            if (e == null || !e.IsAlive) continue;
            bool police = e.firmName == "POLICE" || e.MatchdayRole == MatchdayUnitRole.Police;
            bool home = e.MatchdayRole == MatchdayUnitRole.HomeSupporter;
            _marks.Add(new Mark
            {
                world = e.transform.position,
                sprite = police ? _policeSprite : (home ? _playerSprite : _gangSprite),
                color = police
                    ? new Color(0.35f, 0.65f, 1f)
                    : (e.primaryColor.a > 0.01f ? e.primaryColor : new Color(0.95f, 0.2f, 0.2f)),
                size = _expanded ? 26f : 16f
            });
        }

        if (CityOperationsSystem.Instance != null)
        {
            foreach (var node in CityOperationsSystem.Instance.Nodes)
            {
                if (node == null || !node.gameObject.activeInHierarchy) continue;
                if (CityOperationsLedger.IsComplete(GameManager.Data, node.Type)) continue;
                _marks.Add(new Mark
                {
                    world = node.transform.position,
                    sprite = _objectiveSprite,
                    color = node.IsRunning ? new Color(0.2f, 1f, 0.85f, blink) : new Color(1f, 0.82f, 0.12f, blink),
                    size = _expanded ? 18f : 12f,
                    label = null
                });
            }
        }

        if (CityGameplay.Instance != null && CityGameplay.Instance.Locations != null)
        {
            var names = CityGameplay.Instance.LocationNames;
            var spots = CityGameplay.Instance.Locations;
            for (int i = 0; i < spots.Length; i++)
            {
                string name = names != null && i < names.Length ? names[i] : "AREA";
                _marks.Add(new Mark
                {
                    world = spots[i],
                    sprite = _objectiveSprite,
                    color = AreaColor(i),
                    size = _expanded ? 22f : 12f,
                    label = MapName(name)
                });
            }
        }

        LayoutCachedMarks();
    }

    private void LayoutCachedMarks()
    {
        _iconUsed = 0;
        _labelUsed = 0;
        foreach (var mark in _marks) PlaceMark(mark);
        HideUnused();
    }

    private void RefreshSceneMarkerCache()
    {
        if (Time.unscaledTime < _nextSceneScan) return;
        _nextSceneScan = Time.unscaledTime + 0.75f;
        _gangAreas = FindObjectsByType<GangArea>(FindObjectsSortMode.None);
        _recruitAreas = FindObjectsByType<RecruitArea>(FindObjectsSortMode.None);
        _territoryPoints = FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None);
        _matchdayGroups = FindObjectsByType<MatchdayGroupMarker>(FindObjectsSortMode.None);
    }

    private void PlaceMark(Mark m)
    {
        if (_miniCam == null || _iconLayer == null) return;

        Vector3 camPos = _miniCam.transform.position;
        float halfH = _miniCam.orthographicSize;
        float halfW = halfH * Mathf.Max(0.5f, _miniCam.aspect);
        float nx = (m.world.x - camPos.x) / (halfW * 2f) + 0.5f;
        float ny = (m.world.z - camPos.z) / (halfH * 2f) + 0.5f;
        if (nx < -0.05f || nx > 1.05f || ny < -0.05f || ny > 1.05f) return;

        var img = GetIcon();
        img.sprite = m.sprite;
        img.color = m.color;
        img.enabled = true;
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(m.size, m.size);
        rt.anchorMin = rt.anchorMax = new Vector2(nx, ny);
        rt.anchoredPosition = Vector2.zero;

        if (!string.IsNullOrEmpty(m.label) && _expanded)
        {
            var lab = GetLabel();
            lab.text = m.label;
            lab.enabled = true;
            lab.fontSize = 16f;
            lab.fontStyle = FontStyles.Bold;
            lab.color = Color.white;
            lab.outlineWidth = 0.25f;
            lab.outlineColor = new Color(0f, 0f, 0f, 1f);
            var host = lab.transform.parent as RectTransform;
            host.sizeDelta = new Vector2(150f, 28f);
            host.anchorMin = host.anchorMax = new Vector2(nx, ny);
            host.anchoredPosition = new Vector2(0f, m.size * 0.55f + 10f);
            host.gameObject.SetActive(true);
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
            t.transform.parent.gameObject.SetActive(true);
            return t;
        }
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(_iconLayer, false);
        var plateGo = new GameObject("Plate", typeof(RectTransform), typeof(Image));
        plateGo.transform.SetParent(go.transform, false);
        var plate = plateGo.GetComponent<Image>();
        plate.raycastTarget = false;
        plate.color = new Color(0.02f, 0.03f, 0.04f, 0.92f);
        Stretch(plate.rectTransform);
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.Bold;
        _labelPool.Add(tmp);
        _labelUsed++;
        return tmp;
    }

    private void HideUnused()
    {
        for (int i = _iconUsed; i < _iconPool.Count; i++)
            _iconPool[i].gameObject.SetActive(false);
        for (int i = _labelUsed; i < _labelPool.Count; i++)
            _labelPool[i].transform.parent.gameObject.SetActive(false);
    }

    private void BuildSprites()
    {
        _playerSprite = MakeDiamondSprite(Color.white);
        _gangSprite = MakeSoftSquare(new Color(1f, 0.25f, 0.2f));
        _policeSprite = MakeSoftSquare(new Color(0.3f, 0.55f, 1f));
        _recruitSprite = MakeSoftSquare(new Color(1f, 0.85f, 0.2f));
        _objectiveSprite = MakeRingSprite(48, new Color(1f, 0.82f, 0.12f), 7);
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
public class MiniMapDragCatcher : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
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

    public void OnScroll(PointerEventData eventData)
    {
        Owner?.OnMapScroll(eventData.scrollDelta.y);
    }
}

/// <summary>Keeps the runtime tactical map clear of notches and rounded display corners.</summary>
public sealed class MiniMapSafeArea : MonoBehaviour
{
    private Rect _lastSafeArea;
    private Vector2Int _lastScreen;

    private void OnEnable() => Apply();
    private void LateUpdate()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (Screen.safeArea != _lastSafeArea || size != _lastScreen) Apply();
    }

    private void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        var rt = transform as RectTransform;
        if (rt == null) return;
        Rect safe = Screen.safeArea;
        rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _lastSafeArea = safe;
        _lastScreen = new Vector2Int(Screen.width, Screen.height);
    }
}
