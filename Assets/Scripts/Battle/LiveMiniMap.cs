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
    readonly Vector3[] renderFrameCorners=new Vector3[4];
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
    GameObject layerBar;
    readonly Dictionary<string,bool> visibleLayers=new Dictionary<string,bool>();
    PedestrianController[] civilians=new PedestrianController[0];
    bool Layer(string name)=>!visibleLayers.TryGetValue(name,out bool on)||on;
    private bool _compactVisible=true;
    private Vector2 _compactPos;
    private Vector2 _compactSize;
    private float _minExpandedZoom;
    private float _maxExpandedZoom;
    private float _lastPinchDistance;
    private float _nextIconRefresh;
    private float _nextSceneScan;
    private float _nextMapRender;
    Vector3 renderedFocus;
    float renderedHalfHeight,renderedAspect;
    Matrix4x4 renderedWorldToClip;
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
    TacticalMapUI tactical;
    TacticalMapOverlay tacticalOverlay;
    Vector2 mapPointerStart;
    bool mapPointerMoved,rerouteMode;

    public Camera MapCamera=>_miniCam;
    public RectTransform MapFrame=>_frame;
    public Vector2Int RenderTargetSize=>_rt?new Vector2Int(_rt.width,_rt.height):Vector2Int.zero;
    public RectTransform CompactTapTarget=>_frame;
    public TacticalMapUI TacticalControls=>tactical;
    public TacticalMapOverlay TacticalOverlay=>tacticalOverlay;
    public Image ExpandedBackdrop=>_expandedRoot?_expandedRoot.GetComponent<Image>():null;
    public bool RerouteMode=>rerouteMode;
    public IReadOnlyList<TerritoryControlPoint> Territories=>_territoryPoints;
    public Vector2 WorldToMap(Vector3 point)=>TacticalMapProjection.WorldToMap(_miniCam,point);
    public Vector3 MapToWorld(Vector2 normalized,float groundY=0)=>TacticalMapProjection.MapToWorld(_miniCam,normalized,groundY);
    public bool IsLayerVisible(string name)=>Layer(name);
    public void OpenTacticalMap(){if(!_expanded)Expand();}
    public void CloseTacticalMap(){if(_expanded)Collapse();}
    public void ShowHomeOnMap()
    {
        if(!_expanded||!CityGameplay.Instance)return;
        var home=HomeTerritorySystem.Instance;
        var bounds=CityGameplay.HomeMode&&home&&home.WorldBounds.size.x>0?home.WorldBounds:new Bounds(CityGameplay.Instance.Home,Vector3.one*45);
        if(PoliceCityMode.Active&&PoliceCityMode.Instance)bounds=new Bounds(PoliceCityMode.Instance.Station,new Vector3(70,0,70));
        _panFocus=bounds.center;
        _miniCam.orthographicSize=Mathf.Clamp(Mathf.Max(bounds.extents.z,bounds.extents.x/Mathf.Max(.1f,_miniCam.aspect))*1.5f,_minExpandedZoom,_maxExpandedZoom);
        SnapCamToPan();RefreshMapNow();
    }
    public void ShowWholeCity(){FrameWholeCity();SnapCamToPan();RefreshMapNow();}
    public void RefreshMapNow(){_nextIconRefresh=0;_nextMapRender=0;RenderMapWhenDue();RefreshIcons();tacticalOverlay?.RefreshSnapshot();}
    /// <summary>Independent actual last-render camera projection plus displayed texture crop.</summary>
    public bool TryProjectDisplayedTerrain(Vector3 world,out Vector2 normalized)
    {
        normalized=default;if(renderedHalfHeight<=0||!_raw)return false;
        var clip=renderedWorldToClip*new Vector4(world.x,world.y,world.z,1);
        if(Mathf.Abs(clip.w)<.00001f)return false;
        var textureUv=new Vector2(clip.x/clip.w*.5f+.5f,clip.y/clip.w*.5f+.5f);var crop=_raw.uvRect;
        normalized=new Vector2((textureUv.x-crop.x)/Mathf.Max(.00001f,crop.width),(textureUv.y-crop.y)/Mathf.Max(.00001f,crop.height));return true;
    }
    public bool TryMapScreenPoint(Vector2 screen,out Vector3 world)
    {
        world=default;
        if(!_frame||!RectTransformUtility.ScreenPointToLocalPointInRectangle(_frame,screen,null,out var local))return false;
        var rect=_frame.rect;if(!rect.Contains(local))return false;
        world=MapToWorld(new Vector2((local.x-rect.xMin)/rect.width,(local.y-rect.yMin)/rect.height));return true;
    }
    public void ToggleMapMember(AgentController member)
    {
        AgentSelectionManager.ConsumeUiPointer();
        if(!member||!member.IsAlive||!AgentSelectionManager.instance)return;
        PolicePatrolVehicle.Instance?.Deselect();PoliceRecoveryVehicle.Instance?.Deselect();
        AgentSelectionManager.instance.ToggleSelect(member);RefreshMapNow();
    }
    public void FocusMember(AgentController member)
    {
        if(!member||!member.IsAlive)return;
        CloseTacticalMap();CrewTrackingSystem.FocusMember(member);
    }
    public void TrackMember(AgentController member)
    {
        if(!member||!member.IsAlive)return;
        CrewTrackingSystem.TrackMember(member);RefreshMapNow();
        tactical?.SetStatus("TRACKING "+member.Data.AgentName+" · close the map to follow this individual.");
    }
    public void SetRerouteMode(bool enabled)
    {
        AgentSelectionManager.ConsumeUiPointer();
        rerouteMode=enabled&&_expanded;
        if(rerouteMode){RtsGestureController.Instance?.Cancel();AgentSelectionManager.instance?.ArmMoveCommand();}
        tactical?.SetStatus(rerouteMode?"REROUTE: tap a street on the map. Only selected available crew will move.":"Explore the map. Tap a crew marker/name to add or remove that member.");
        tactical?.RefreshCrew();
    }
    public bool TryIssueMapMove(Vector3 destination)
    {
        if(!_expanded||!rerouteMode)return false;
        AgentSelectionManager.ConsumeUiPointer();var selection=AgentSelectionManager.instance;
        if(PoliceCityMode.Active)
        {
            bool recovery=PoliceRecoveryVehicle.Instance&&PoliceRecoveryVehicle.Instance.Selected;
            bool patrol=PolicePatrolVehicle.Instance&&PolicePatrolVehicle.Instance.Selected;
            if(recovery||patrol)
            {
                bool moved=recovery?PoliceRecoveryVehicle.Instance.DriveTo(destination):PolicePatrolVehicle.Instance.DriveTo(destination);
                tactical?.SetStatus(moved?"VEHICLE ROUTE ACCEPTED · real connected roads only.":recovery?PoliceRecoveryVehicle.Instance.Status:PolicePatrolVehicle.Instance.Status);
                if(moved)rerouteMode=false;RefreshMapNow();return moved;
            }
        }
        if(!selection||selection.SelectedAgents.Count==0){tactical?.SetStatus("Select crew on the map first. No movement order was issued.");return false;}
        RtsGestureController.Instance?.Cancel();selection.ArmMoveCommand();selection.CommandSelectedGround(destination);
        int accepted=selection.LastAcceptedOrderCount;
        tactical?.SetStatus(accepted>0?$"MOVE ACCEPTED · {accepted}/{selection.SelectedAgents.Count} selected members. Other groups keep their duties.":"MOVE BLOCKED · no selected member can reach that map location; previous duties remain.");
        if(accepted>0)rerouteMode=false;
        RefreshMapNow();return accepted>0;
    }

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
        public AgentController member;
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
        _rt = new RenderTexture(Application.isMobilePlatform?384:512, Application.isMobilePlatform?384:512, 16, RenderTextureFormat.ARGB32);
        _rt.wrapMode=TextureWrapMode.Clamp;
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
        _miniCam.allowHDR=false;_miniCam.allowMSAA=false;
        _miniCam.useOcclusionCulling=false;
        _miniCam.depth = -50f;
        _miniCam.cullingMask = ~(1 << 5);
        _miniCam.enabled = false;
        var cameraData=camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        cameraData.renderShadows=false;cameraData.renderPostProcessing=false;
        cameraData.antialiasing=UnityEngine.Rendering.Universal.AntialiasingMode.None;
        cameraData.requiresColorOption=UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
        cameraData.requiresDepthOption=UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
        // Renderer features (for example PC SSAO) are independent of the
        // postprocessing switch and can request a depth/normal prepass. Reuse
        // the configured feature-free forward renderer for this small feed;
        // leave the main camera and pipeline default renderer unchanged.
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if(pipeline)
        {
            var renderers=pipeline.rendererDataList;
            for(int i=0;i<renderers.Length;i++)
            if(renderers[i] is UnityEngine.Rendering.Universal.UniversalRendererData renderer&&
                renderer.renderingMode==UnityEngine.Rendering.Universal.RenderingMode.Forward&&renderer.rendererFeatures.Count==0)
            {cameraData.SetRenderer(i);break;}
        }
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
        // Match the gameplay HUD's safe-area scale, including notched 16:9 screens.
        var uiFrame = LandscapeUI.Rect("Landscape", safeGo.transform, 0, 0, 1600, 900);
        var viewport = safeGo.AddComponent<LandscapeViewport>();
        viewport.frame = uiFrame;
        viewport.Fit();
        Transform uiRoot = uiFrame;
        BuildLayers(uiRoot);

        // Gameplay: leave bottom-right margin; stack CAMERA / RECENTRE under the map.
        if (gameObject.scene.name == "Gameplay")
        {
            _compactSize = new Vector2(220f, 220f);
            // Aligns with camera buttons stacked under the map (bottom margin ~22).
            _compactPos = new Vector2(24f, -600f);
        }
        else
        {
            _compactPos = new Vector2(-24f, -122f);
            _compactSize = new Vector2(220f, 200f);
        }

        // Full-display solid backdrop, including outside the authored safe-area
        // frame. It sits behind this canvas's controls and above the old HUD.
        _expandedRoot = new GameObject("ExpandedDim", typeof(RectTransform), typeof(Image));
        _expandedRoot.transform.SetParent(_canvas.transform, false);
        _expandedRoot.transform.SetAsFirstSibling();
        Stretch(_expandedRoot.GetComponent<RectTransform>());
        var dimImg = _expandedRoot.GetComponent<Image>();
        dimImg.sprite = null;
        dimImg.type = Image.Type.Simple;
        dimImg.color = new Color(.025f,.045f,.055f,1f);
        dimImg.raycastTarget = true;
        _expandedRoot.SetActive(false);

        var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(uiRoot, false);
        _borderRt = borderGo.GetComponent<RectTransform>();
        _borderRt.anchorMin = _borderRt.anchorMax = CompactAnchor;
        _borderRt.pivot = CompactAnchor;
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
        _frame.anchorMin = _frame.anchorMax = CompactAnchor;
        _frame.pivot = CompactAnchor;
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
        var footprint = new GameObject("TacticalWorldOverlay", typeof(RectTransform), typeof(TacticalMapOverlay));
        footprint.transform.SetParent(frameGo.transform, false);
        Stretch(footprint.GetComponent<RectTransform>());
        tacticalOverlay=footprint.GetComponent<TacticalMapOverlay>();tacticalOverlay.Initialize(this);

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
        TutorialAnchor.Tag(_closeBtn.GetComponent<Button>(),"city.mapClose");
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
        tactical=gameObject.AddComponent<TacticalMapUI>();tactical.Initialize(this,uiRoot);
        tactical.SetVisible(false);
        var layers=layerBar.GetComponent<RectTransform>();layers.anchoredPosition=new Vector2(0,14);layers.sizeDelta=new Vector2(1540,46);
        foreach(var caption in layerBar.GetComponentsInChildren<TextMeshProUGUI>())caption.fontSize=16;
    }

    void BuildLayers(Transform parent)
    {
        layerBar=new GameObject("Map layers",typeof(RectTransform),typeof(Image),typeof(HorizontalLayoutGroup));
        layerBar.transform.SetParent(parent,false);
        var rect=layerBar.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,28);rect.sizeDelta=new Vector2(1320,68);
        layerBar.GetComponent<Image>().color=new Color(.025f,.055f,.065f,.96f);
        var layout=layerBar.GetComponent<HorizontalLayoutGroup>();layout.spacing=12;layout.padding=new RectOffset(12,12,8,8);layout.childControlWidth=true;layout.childControlHeight=true;
        foreach(string name in new[]{"AREAS","GANGS","POLICE","CROWDS","CIVILIANS","LABELS"})
        {
            string key=name;visibleLayers[key]=PlayerPrefs.GetInt("Map.Layer."+key,key=="CIVILIANS"||key=="LABELS"||key=="CROWDS"?0:1)==1;
            var go=new GameObject(key,typeof(RectTransform),typeof(Toggle),typeof(LayoutElement));go.transform.SetParent(layerBar.transform,false);go.GetComponent<LayoutElement>().preferredWidth=204;
            var box=new GameObject("Checkbox",typeof(RectTransform),typeof(Image));box.transform.SetParent(go.transform,false);
            var br=box.GetComponent<RectTransform>();br.anchorMin=br.anchorMax=new Vector2(0,.5f);br.pivot=new Vector2(0,.5f);br.sizeDelta=new Vector2(30,30);box.GetComponent<Image>().color=new Color(.16f,.23f,.25f);
            var check=new GameObject("Check",typeof(RectTransform),typeof(Image));check.transform.SetParent(box.transform,false);Stretch(check.GetComponent<RectTransform>());check.GetComponent<Image>().color=new Color(.12f,.65f,.3f);
            for(int i=0;i<2;i++){var stroke=new GameObject("Tick",typeof(RectTransform),typeof(Image));stroke.transform.SetParent(check.transform,false);var sr=stroke.GetComponent<RectTransform>();sr.sizeDelta=new Vector2(i==0?10:19,4);sr.anchoredPosition=new Vector2(i==0?-5:3,i==0?-2:1);sr.localRotation=Quaternion.Euler(0,0,i==0?-45:45);stroke.GetComponent<Image>().raycastTarget=false;}
            var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);Stretch(label.GetComponent<RectTransform>());label.GetComponent<RectTransform>().offsetMin=new Vector2(38,0);var text=label.GetComponent<TextMeshProUGUI>();text.text=key;text.fontSize=19;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;
            var toggle=go.GetComponent<Toggle>();toggle.targetGraphic=box.GetComponent<Image>();toggle.graphic=check.GetComponent<Image>();toggle.isOn=visibleLayers[key];check.SetActive(toggle.isOn);
            toggle.onValueChanged.AddListener(on=>{check.SetActive(on);visibleLayers[key]=on;PlayerPrefs.SetInt("Map.Layer."+key,on?1:0);PlayerPrefs.Save();_nextSceneScan=0;RefreshIcons();});
        }
        layerBar.SetActive(false);
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
        if(_expanded)return;
        AgentSelectionManager.ConsumeUiPointer();
        CityUiSheets.Open(this,Collapse);
        _expanded = true;
        rerouteMode=false;
        _panFocus = GetPlayerFocus();
        _dragging = false;

        // Freeze main game camera + input — only this map may pan.
        FreezeMainGame(true);

        if (_canvas != null) _canvas.sortingOrder = 29400;

        _expandedRoot.SetActive(true);
        _frame.gameObject.SetActive(true);_borderRt.gameObject.SetActive(true);
        _closeBtn.SetActive(true);
        _zoomInBtn.SetActive(true);
        _zoomOutBtn.SetActive(true);
        _closeBtn.transform.SetAsLastSibling();
        layerBar.SetActive(true);layerBar.transform.SetAsLastSibling();
        _zoomInBtn.transform.SetAsLastSibling();
        _zoomOutBtn.transform.SetAsLastSibling();
        _legend.gameObject.SetActive(false);
        _hint.gameObject.SetActive(false);
        _closeBtn.transform.SetAsLastSibling();

        // Full-screen map (small margins for hint + legend + X).
        StretchFull(_expandedRoot.GetComponent<RectTransform>(), left: 0f, bottom: 0f, right: 0f, top: 0f);
        StretchFull(_frame, left: 20f, bottom: 154f, right: 386f, top: 102f);
        StretchFull(_borderRt, left: 16f, bottom: 150f, right: 382f, top: 98f);
        var closeRect=_closeBtn.GetComponent<RectTransform>();closeRect.sizeDelta=new Vector2(150,62);closeRect.anchoredPosition=new Vector2(-18,-16);
        _closeBtn.GetComponentInChildren<TextMeshProUGUI>().text="CLOSE";_closeBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize=23;
        var plus=_zoomInBtn.GetComponent<RectTransform>();plus.sizeDelta=new Vector2(54,54);plus.anchoredPosition=new Vector2(-360,-20);
        var minus=_zoomOutBtn.GetComponent<RectTransform>();minus.sizeDelta=new Vector2(54,54);minus.anchoredPosition=new Vector2(-294,-20);

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
        Canvas.ForceUpdateCanvases();_lastFrameSize=Vector2.zero;FitMapFrame();FrameWholeCity();SnapCamToPan();
        tactical?.SetVisible(true);
        _closeBtn.transform.SetAsLastSibling();_zoomInBtn.transform.SetAsLastSibling();_zoomOutBtn.transform.SetAsLastSibling();
        _nextIconRefresh = 0f;
        _nextMapRender = 0f;
        RenderMapWhenDue();RefreshIcons();tacticalOverlay?.RefreshSnapshot();
    }

    private Vector2 CompactAnchor => gameObject.scene.name == "Gameplay" ? new Vector2(0,1) : new Vector2(1,1);

    private void Collapse()
    {
        if(!_expanded)return;
        AgentSelectionManager.ConsumeUiPointer();rerouteMode=false;
        _expanded = false;
        tactical?.SetVisible(false);
        if(layerBar)layerBar.SetActive(false);
        _dragging = false;

        FreezeMainGame(false);

        if (_canvas != null) _canvas.sortingOrder = 14000;

        _expandedRoot.SetActive(false);
        _closeBtn.SetActive(false);
        _zoomInBtn.SetActive(false);
        _zoomOutBtn.SetActive(false);
        _legend.gameObject.SetActive(false);
        _hint.gameObject.SetActive(false);

        _frame.anchorMin = _frame.anchorMax = CompactAnchor;
        _frame.pivot = CompactAnchor;
        _frame.offsetMin = Vector2.zero;
        _frame.offsetMax = Vector2.zero;
        _frame.anchoredPosition = _compactPos;
        _frame.sizeDelta = _compactSize;

        _borderRt.anchorMin = _borderRt.anchorMax = CompactAnchor;
        _borderRt.pivot = CompactAnchor;
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
        _frame.gameObject.SetActive(_compactVisible&&GuidedCityTutorial.AllowsFeature("map"));_borderRt.gameObject.SetActive(_compactVisible&&GuidedCityTutorial.AllowsFeature("map"));
        tacticalOverlay?.RefreshSnapshot();
    }

    public void SetCompactVisible(bool visible)
    {
        _compactVisible=visible;
        if(_expanded)return;
        if(_frame)_frame.gameObject.SetActive(visible&&GuidedCityTutorial.AllowsFeature("map"));
        if(_borderRt)_borderRt.gameObject.SetActive(visible&&GuidedCityTutorial.AllowsFeature("map"));
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
        AgentSelectionManager.ConsumeUiPointer();mapPointerStart=screenPos;mapPointerMoved=false;
        if (!_expanded) return;
        _dragging = true;
        _lastPointer = screenPos;
    }

    public void OnMapPointerDrag(Vector2 screenPos)
    {
        if (!_expanded){if((screenPos-mapPointerStart).sqrMagnitude>100)mapPointerMoved=true;return;}
        if (!_dragging || _miniCam == null) return;
        mapPointerMoved=true;
        Vector2 delta = screenPos - _lastPointer;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(_frame,screenPos,null,out var current)&&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_frame,_lastPointer,null,out var previous))delta=current-previous;
        _lastPointer = screenPos;
        if((screenPos-mapPointerStart).sqrMagnitude>100)mapPointerMoved=true;

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
        SnapCamToPan();RenderMapWhenDue();LayoutCachedMarks();tacticalOverlay?.SetVerticesDirty();
    }

    public void OnMapPointerUp()
    {
        _dragging = false;
    }
    public void OnMapPointerClick(Vector2 screen)
    {
        // The feed's child catcher owns pointer clicks, so the parent Button
        // never receives a normal tap. Route that real input explicitly.
        if(!_expanded){if(!mapPointerMoved)Expand();return;}
        if(_expanded&&!mapPointerMoved&&TryMapScreenPoint(screen,out var world))TryIssueMapMove(world);
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
        RenderMapWhenDue();LayoutCachedMarks();tacticalOverlay?.SetVerticesDirty();
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
        ResizeRenderTarget();
    }

    void ResizeRenderTarget()
    {
        if(!_frame||!_miniCam)return;
        int width,height;
        if(!_expanded){width=height=Application.isMobilePlatform?384:Mathf.Clamp(textureSize,256,512);}
        else
        {
            // The expanded map gets its own bounded, aspect-matched feed.
            // Include the existing padded render patch in the pixel budget.
            _frame.GetWorldCorners(renderFrameCorners);
            float physicalWidth=Mathf.Max(1,Vector3.Distance(renderFrameCorners[0],renderFrameCorners[3]));
            float physicalHeight=Mathf.Max(1,Vector3.Distance(renderFrameCorners[0],renderFrameCorners[1]));
            float longest=Mathf.Max(physicalWidth,physicalHeight)*1.4f;
            float factor=Mathf.Min(1,(Application.isMobilePlatform?1024f:1280f)/longest)*1.4f;
            width=Mathf.Max(128,Mathf.RoundToInt(physicalWidth*factor/16)*16);
            height=Mathf.Max(128,Mathf.RoundToInt(physicalHeight*factor/16)*16);
        }
        if(_rt&&_rt.width==width&&_rt.height==height)return;
        var previous=_rt;_miniCam.targetTexture=null;
        _rt=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32){name="Tactical map bounded feed",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,antiAliasing=1,useMipMap=false,autoGenerateMips=false};
        _rt.Create();_miniCam.targetTexture=_rt;if(_raw)_raw.texture=_rt;
        _miniCam.aspect=_frame.rect.width/Mathf.Max(1,_frame.rect.height);
        renderedHalfHeight=0;_nextMapRender=0;
        if(previous){previous.Release();Destroy(previous);}
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
        if(CityGameplay.HomeMode&&HomeTerritorySystem.Instance&&HomeTerritorySystem.Instance.WorldBounds.size.x>0)
        {Enc(HomeTerritorySystem.Instance.WorldBounds.min);Enc(HomeTerritorySystem.Instance.WorldBounds.max);}
        if(PoliceCityMode.Active&&PoliceCityMode.Instance)Enc(PoliceCityMode.Instance.Station);
        RefreshSceneMarkerCache();
        foreach (var g in _gangAreas)
            if (g) Enc(g.transform.position);
        if (BattleManager.instance != null)
            foreach (var a in BattleManager.instance.PlayerAgents)
                if (a && a.IsAlive) Enc(a.transform.position);
        if (n == 0 || _miniCam == null) return;
        _panFocus = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        float required=Mathf.Max((maxZ-minZ)*.5f,(maxX-minX)*.5f/Mathf.Max(.1f,_miniCam.aspect))*1.12f+12;
        _maxExpandedZoom=Mathf.Max(_maxExpandedZoom,required);
        _miniCam.orthographicSize=Mathf.Clamp(required,_minExpandedZoom,_maxExpandedZoom);
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
        if(!_expanded)
        {
            bool visible=_compactVisible&&GuidedCityTutorial.AllowsFeature("map");
            if(_frame.gameObject.activeSelf!=visible)_frame.gameObject.SetActive(visible);
            if(_borderRt.gameObject.activeSelf!=visible)_borderRt.gameObject.SetActive(visible);
        }
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
        else
        {
            // Cached world markers must follow the displayed crop even while
            // the compact map follows a moving camera between render snapshots.
            LayoutCachedMarks();
        }
        tactical?.Tick();tacticalOverlay?.Tick();
    }

    private void RenderMapWhenDue()
    {
        if (_miniCam == null||!_raw) return;
        if (!_expanded && !_compactVisible) return;
        // Render a padded patch at a bounded rate. Between renders, crop that
        // same patch in UV space so terrain and labels follow input together.
        // This avoids a second full city render on every drag frame.
        float half=_miniCam.orthographicSize;
        var pos=_miniCam.transform.position;
        bool patchExhausted=renderedHalfHeight<=0 || Mathf.Abs(pos.x-renderedFocus.x)>renderedHalfHeight*renderedAspect*.18f ||
            Mathf.Abs(pos.z-renderedFocus.z)>renderedHalfHeight*.18f || Mathf.Abs(_miniCam.aspect-renderedAspect)>.01f ||
            half>renderedHalfHeight*.9f || half<renderedHalfHeight*.4f;
        if(patchExhausted||Time.unscaledTime>=_nextMapRender)
        {
            _nextMapRender=Time.unscaledTime+(_expanded?.12f:.2f);
            renderedFocus=pos;renderedHalfHeight=half*1.4f;renderedAspect=_miniCam.aspect;
            _miniCam.orthographicSize=renderedHalfHeight;
            renderedWorldToClip=_miniCam.projectionMatrix*_miniCam.worldToCameraMatrix;
            _miniCam.Render();
            _miniCam.orthographicSize=half;
        }
        float scale=half/renderedHalfHeight;
        _raw.uvRect=new Rect(.5f+(pos.x-renderedFocus.x)/(2*renderedHalfHeight*renderedAspect)-scale*.5f,
            .5f+(pos.z-renderedFocus.z)/(2*renderedHalfHeight)-scale*.5f,scale,scale);
    }

    private void UpdatePinchZoom()
    {
        if (!_expanded || Touch.activeTouches.Count < 2)
        {
            _lastPinchDistance = 0f;
            return;
        }
        mapPointerMoved=true;
        float distance = Vector2.Distance(Touch.activeTouches[0].screenPosition, Touch.activeTouches[1].screenPosition);
        if (_lastPinchDistance > 1f && distance > 1f)
            AdjustZoom(_lastPinchDistance / distance);
        _lastPinchDistance = distance;
    }

    private void UpdateCameraFollow()
    {
        Vector3 focus = Camera.main ? CityActivityStreaming.GroundFocus(Camera.main) : GetPlayerFocus();
        // The map must contain the camera's street footprint, including when
        // the player zooms out. A fixed close-up crop hid the entire outline.
        var main=Camera.main;
        if(main)
        {
            float extent=orthographicSize;
            var ground=new Plane(Vector3.up,Vector3.zero);
            for(int i=0;i<4;i++)
            {
                var ray=main.ViewportPointToRay(new Vector3(i%2,i/2,0));
                if(!ground.Raycast(ray,out float distance))continue;
                var delta=ray.GetPoint(distance)-focus;
                extent=Mathf.Max(extent,Mathf.Abs(delta.x)/Mathf.Max(.1f,_miniCam.aspect),Mathf.Abs(delta.z));
            }
            _miniCam.orthographicSize=Mathf.Clamp(extent*1.12f,orthographicSize,480f);
        }
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
            if (a == null || !a.IsAlive || !a.gameObject.activeInHierarchy||PolicePatrolVehicle.IsMounted(a)) continue;
            _marks.Add(new Mark
            {
                world = a.transform.position,
                sprite = _playerSprite,
                color = GameManager.IsPolicePlayer
                    ? new Color(0.28f, 0.58f, 1f, blink)
                    : new Color(0.25f, 1f, 0.55f, blink),
                size = a.IsSelected ? (_expanded ? 28f : 18f) : (_expanded ? 22f : 14f),
                label = _expanded ? a.Data?.AgentName : null,
                member=a
            });
        }

        foreach (var g in _gangAreas)
        {
            if(!Layer("GANGS"))continue;
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
            if(!Layer("AREAS"))continue;
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
            if(!Layer("AREAS"))continue;
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
            if(!Layer(police?"POLICE":"CROWDS"))continue;
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

        if (LivePoliceSystem.Instance != null&&Layer("POLICE"))
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
            if (e == null || !e.IsAlive || !e.gameObject.activeInHierarchy || e.IsAmbientMatchdayUnit) continue;
            bool police = e.firmName == "POLICE" || e.MatchdayRole == MatchdayUnitRole.Police;
            if(!Layer(police?"POLICE":"GANGS"))continue;
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

        if(PoliceCityMode.Instance&&PoliceCityMode.Instance.Ready&&Layer("AREAS"))
        {
            var mode=PoliceCityMode.Instance;
            if(PolicePatrolVehicle.Instance)_marks.Add(new Mark{world=PolicePatrolVehicle.Instance.transform.position,sprite=_policeSprite,color=new Color(.3f,.9f,1f),size=_expanded?30:18,label=_expanded?"PATROL VAN":null});
            if(PoliceRecoveryVehicle.Instance)_marks.Add(new Mark{world=PoliceRecoveryVehicle.Instance.transform.position,sprite=_policeSprite,color=LandscapeUI.Gold,size=_expanded?30:18,label=_expanded?"RECOVERY TRUCK":null});
            _marks.Add(new Mark{world=mode.Station,sprite=_policeSprite,color=Color.cyan,size=_expanded?32:20,label=_expanded?"POLICE STATION":null});
            for(int i=0;i<mode.PatrolPoints.Count;i++)_marks.Add(new Mark{world=mode.PatrolPoints[i],sprite=_objectiveSprite,color=Color.cyan,size=_expanded?22:12,label=_expanded?"PATROL "+(i+1):null});
            foreach(var call in mode.Calls)if(call.state<4)_marks.Add(new Mark{world=mode.CallActionPoint(call),sprite=_objectiveSprite,color=new Color(1f,.75f,.1f,blink),size=_expanded?26:15,label=_expanded?call.title:null});
        }
        else if(CityGameplay.Instance&&CityGameplay.HomeMode&&Layer("AREAS"))
            _marks.Add(new Mark{world=CityGameplay.Instance.Home,sprite=_objectiveSprite,color=new Color(.1f,1f,.6f),size=_expanded?32:20,label=_expanded?"HOME TERRITORY":null});
        if(CityBusinessSystem.Instance&&CityBusinessSystem.Instance.enabled&&Layer("AREAS"))
        {
            foreach(var site in CityBusinessSystem.Instance.Sites)
            {
                if(!site)continue;
                var business=CityBusinessLedger.Find(GameManager.Data,site.Definition.id);
                _marks.Add(new Mark{world=site.transform.position,sprite=_objectiveSprite,
                    color=business.rivalPressureSeconds>0?new Color(1f,.22f,.15f,blink):business.controlled?new Color(.2f,1f,.55f):new Color(1f,.78f,.2f),
                    size=_expanded?20f:11f,label=_expanded?site.Definition.name:null});
            }
        }
        var responses=MatchdayStrategySystem.Instance;
        if(responses&&StadiumMatchdayActivity.Instance&&StadiumMatchdayActivity.Instance.HasStarted&&Layer("AREAS"))
            for(int i=0;i<responses.ResponsePoints.Count;i++)
                _marks.Add(new Mark{world=responses.ResponsePoints[i],sprite=_objectiveSprite,
                    color=responses.ResponseComplete(i)?new Color(.2f,1f,.55f):new Color(1f,.8f,.15f,blink),
                    size=_expanded?24f:14f,label=_expanded?(i==0?"STADIUM RESPONSE":"PUB RESPONSE"):null});

        if (CityOperationsSystem.Instance != null&&CityOperationsSystem.Instance.enabled&&Layer("AREAS"))
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

        if (!PoliceCityMode.Active&&CityGameplay.Instance != null && CityGameplay.Instance.Locations != null&&Layer("AREAS"))
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

        if(Layer("CIVILIANS"))foreach(var civilian in civilians)
            if(civilian&&civilian.gameObject.activeInHierarchy)_marks.Add(new Mark{world=civilian.transform.position,sprite=_playerSprite,color=Color.white,size=_expanded?10:5});
        LayoutCachedMarks();
    }

    private void LayoutCachedMarks()
    {
        _iconUsed = 0;
        _labelUsed = 0;
        labelRects.Clear();
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
        civilians=Layer("CIVILIANS")?FindObjectsByType<PedestrianController>(FindObjectsSortMode.None):new PedestrianController[0];
    }

    private void PlaceMark(Mark m)
    {
        if (_miniCam == null || _iconLayer == null) return;

        Vector2 mapped=WorldToMap(m.world);float nx=mapped.x,ny=mapped.y;
        if (nx < -0.05f || nx > 1.05f || ny < -0.05f || ny > 1.05f) return;

        var img = GetIcon();
        img.sprite = m.sprite;
        img.color = m.color;
        img.enabled = true;
        img.raycastTarget=_expanded&&m.member;
        var target=img.GetComponent<TacticalMapMemberTarget>();
        if(m.member&&!target)target=img.gameObject.AddComponent<TacticalMapMemberTarget>();
        if(target)target.Bind(this,m.member);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(m.size, m.size);
        rt.anchorMin = rt.anchorMax = new Vector2(nx, ny);
        rt.anchoredPosition = Vector2.zero;

        if (!string.IsNullOrEmpty(m.label) && _expanded&&(m.member||Layer("LABELS"))&&_labelUsed<32)
        {
            var candidate=new Rect(nx*_iconLayer.rect.width-75,ny*_iconLayer.rect.height+m.size*.55f,150,28);
            if(labelRects.Exists(r=>r.Overlaps(candidate)))return;
            labelRects.Add(candidate);
            var lab = GetLabel();
            lab.text = m.label;
            var plate=lab.transform.parent.GetComponentInChildren<Image>();
            if(plate)
            {
                plate.raycastTarget=m.member;var labelTarget=plate.GetComponent<TacticalMapMemberTarget>();
                if(m.member&&!labelTarget)labelTarget=plate.gameObject.AddComponent<TacticalMapMemberTarget>();
                if(labelTarget)labelTarget.Bind(this,m.member);
            }
            lab.enabled = true;
            lab.fontSize = 16f;
            lab.fontStyle = FontStyles.Bold;
            lab.color = m.member?CrewTrackingSystem.MemberColor(m.member):Color.white;
            lab.outlineWidth = 0.25f;
            lab.outlineColor = new Color(0f, 0f, 0f, 1f);
            var host = lab.transform.parent as RectTransform;
            host.sizeDelta = new Vector2(150f, 28f);
            host.anchorMin = host.anchorMax = new Vector2(nx, ny);
            host.anchoredPosition = new Vector2(0f, m.size * 0.55f + 10f);
            host.gameObject.SetActive(true);
        }
    }
    readonly List<Rect> labelRects=new List<Rect>();

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
public class MiniMapDragCatcher : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler, IScrollHandler
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
    public void OnPointerClick(PointerEventData eventData)
    {
        AgentSelectionManager.ConsumeUiPointer();Owner?.OnMapPointerClick(eventData.position);
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
