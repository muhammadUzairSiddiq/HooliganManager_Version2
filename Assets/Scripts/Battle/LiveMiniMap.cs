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
            cameraHeight=300;orthographicSize=100;expandedOrthoSize=260;
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
            _compactSize = new Vector2(200f, 190f);
            // Aligns with camera buttons stacked under the map (bottom margin ~22).
            _compactPos = new Vector2(-24f, -588f);
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
        Stretch(rawGo.GetComponent<RectTransform>());
        _raw = rawGo.GetComponent<RawImage>();
        _raw.texture = _rt;
        _raw.color = Color.white;
        _raw.raycastTarget = false;

        // Muted tactical wash keeps city geometry readable without competing
        // with the high-contrast squad, rival, police and objective symbols.
        var washGo = new GameObject("TacticalWash", typeof(RectTransform), typeof(Image));
        washGo.transform.SetParent(frameGo.transform, false);
        Stretch(washGo.GetComponent<RectTransform>());
        var wash = washGo.GetComponent<Image>();
        wash.color = new Color(0.015f, 0.04f, 0.055f, 0.42f);
        wash.raycastTarget = false;

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
        borderImg.color = new Color(0.015f, 0.07f, 0.09f, 1f);

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
    }

    private void SnapCamToPan()
    {
        if (_miniCam == null) return;
        _miniCam.transform.position = new Vector3(_panFocus.x, cameraHeight, _panFocus.z);
    }

    private void LateUpdate()
    {
        if (_miniCam == null) return;
        UpdatePinchZoom();
        if (!_expanded)
            UpdateCameraFollow();
        else if (!_dragging)
            SnapCamToPan(); // keep stable while idle
        RenderMapWhenDue();
        if (Time.unscaledTime >= _nextIconRefresh)
        {
            _nextIconRefresh = Time.unscaledTime + (_expanded ? 0.08f : 0.12f);
            RefreshIcons();
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
                size = _expanded ? 40f : 20f,
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
                color = new Color(1f, 0.25f, 0.15f, blink),
                size = _expanded ? 44f : 24f,
                label = null
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
                size = _expanded ? 42f : 22f,
                label = null
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
                size = _expanded ? 38f : 21f,
                label = null
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
                    size = _expanded ? 40f : 20f,
                    label = null
                });
            }
            if (LivePoliceSystem.Instance.ResponseCar != null)
            {
                _marks.Add(new Mark
                {
                    world = LivePoliceSystem.Instance.ResponseCar.transform.position,
                    sprite = _policeSprite,
                    color = new Color(0.15f, 0.45f, 1f, blink),
                    size = _expanded ? 42f : 22f,
                    label = null
                });
            }
        }

        if (!_expanded)
        {
            foreach (var e in BattleManager.instance.EnemyAgents)
            {
                if (e == null || !e.IsAlive) continue;
                if (e.firmName == "POLICE")
                    _marks.Add(new Mark { world = e.transform.position, sprite = _policeSprite, color = new Color(0.35f, 0.65f, 1f), size = 14f });
                else
                    _marks.Add(new Mark { world = e.transform.position, sprite = _gangSprite, color = e.primaryColor.a > 0.01f ? e.primaryColor : new Color(0.95f, 0.2f, 0.2f), size = 14f });
            }
        }

        foreach (var m in _marks)
            PlaceMark(m);

        HideUnused();
    }

    private void RefreshSceneMarkerCache()
    {
        if (Time.unscaledTime < _nextSceneScan) return;
        _nextSceneScan = Time.unscaledTime + 0.75f;
        _gangAreas = FindObjectsByType<GangArea>(FindObjectsSortMode.None);
        _recruitAreas = FindObjectsByType<RecruitArea>(FindObjectsSortMode.None);
        _territoryPoints = FindObjectsByType<TerritoryControlPoint>(FindObjectsSortMode.None);
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
