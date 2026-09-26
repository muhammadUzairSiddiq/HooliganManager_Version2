using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Desaturates the rendered world; overlay UI retains its own colors.</summary>
public sealed class ModalPresentation : MonoBehaviour
{
    public static ModalPresentation Instance { get; private set; }
    Volume volume;
    Camera worldCamera;
    bool _stamp;
    float _stampUntil;
    Action _stampThen;
    GameObject _stampRoot;
    TextMeshProUGUI _stampLabel;

    /// <summary>Silver world, then a red word, without pausing the city. The callback runs when the word leaves.</summary>
    public static void ShowStamp(string word, float seconds, Action then)
    {
        if (Instance == null)
        {
            then?.Invoke();
            return;
        }
        Instance.BeginStamp(word, seconds, then);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("Modal silver post processing");
        DontDestroyOnLoad(go); go.AddComponent<ModalPresentation>();
    }
    void Awake()
    {
        Instance = this;
        volume = gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10000;
        volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var color = volume.profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-100); color.contrast.Override(-12); color.postExposure.Override(.15f);
        color.colorFilter.Override(new Color(.82f, .84f, .86f));
        volume.weight = 0;
    }
    void BeginStamp(string word, float seconds, Action then)
    {
        EnsureStamp();
        _stamp = true;
        _stampUntil = Time.unscaledTime + Mathf.Clamp(seconds, 3f, 5f);
        _stampThen = then;
        if (_stampLabel) _stampLabel.text = string.IsNullOrEmpty(word) ? "WASTED" : word.ToUpperInvariant();
        if (_stampRoot) _stampRoot.SetActive(true);
    }

    void EnsureStamp()
    {
        if (_stampRoot) return;
        var canvasGo = new GameObject("WipeStamp", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 1f;
        var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(canvasGo.transform, false);
        var plateRt = plate.GetComponent<RectTransform>();
        plateRt.anchorMin = Vector2.zero;
        plateRt.anchorMax = Vector2.one;
        plateRt.offsetMin = plateRt.offsetMax = Vector2.zero;
        var plateImage = plate.GetComponent<Image>();
        plateImage.color = new Color(0f, 0f, 0f, 0.01f);
        plateImage.raycastTarget = true;
        var textGo = new GameObject("Word", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);
        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1400, 220);
        _stampLabel = textGo.AddComponent<TextMeshProUGUI>();
        if (LandscapeTheme.Current != null && LandscapeTheme.Current.font) _stampLabel.font = LandscapeTheme.Current.font;
        _stampLabel.alignment = TextAlignmentOptions.Center;
        _stampLabel.fontSize = 132;
        _stampLabel.fontStyle = FontStyles.Bold;
        _stampLabel.color = new Color(0.92f, 0.05f, 0.06f, 1f);
        _stampLabel.raycastTarget = false;
        _stampLabel.enableWordWrapping = false;
        _stampRoot = canvasGo;
        _stampRoot.SetActive(false);
    }

    void LateUpdate()
    {
        if (_stamp && Time.unscaledTime >= _stampUntil)
        {
            _stamp = false;
            if (_stampRoot) _stampRoot.SetActive(false);
            var then = _stampThen;
            _stampThen = null;
            then?.Invoke();
        }
        bool open = _stamp || Time.timeScale == 0 || GamePopup.AnyOpen || RecruitDialogBox.AnyOpen;
        volume.weight = open ? 1 : 0;
        if (!worldCamera) worldCamera = Camera.main;
        if (worldCamera && worldCamera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
        { data.renderPostProcessing = open; data.volumeLayerMask |= 1 << gameObject.layer; }
    }
    void OnDestroy() { if (volume && volume.profile) Destroy(volume.profile); }
}
