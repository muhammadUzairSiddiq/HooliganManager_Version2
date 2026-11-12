using UnityEngine;
using UnityEngine.UI;
using static LandscapeUI;

/// <summary>Shared live settings. Leaves city simulation and explicit Pause state untouched.</summary>
public sealed class AudioSettingsSheet : MonoBehaviour
{
    GameObject root;
    AudioSettingsControls controls;
    Slider master;
    public bool IsOpen => root && root.activeSelf;
    public void Show()
    {
        if (!root) Build();
        CityUiSheets.Open(this, HideFromOwner);
        master.SetValueWithoutNotify(GameFeedback.MasterVolume); controls.Refresh();
        root.SetActive(true); AgentSelectionManager.ConsumeUiPointer(); GameFeedback.Emit(GameFeedbackEvent.UiOpen);
    }
    void HideFromOwner() => Hide();
    public void Hide(bool commit = true)
    {
        if (!IsOpen) return;
        root.SetActive(false); GameFeedback.SettingsDismissed();
        if (commit) GameFeedback.SavePreferences();
        GameFeedback.Emit(GameFeedbackEvent.UiClose);
    }
    void Build()
    {
        var host = new GameObject("Sound Motion Settings", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        host.transform.SetParent(transform, false);
        var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 29500;
        ConfigureLandscapeScaler(host.GetComponent<CanvasScaler>());
        var safe = Rect("SafeArea", host.transform, 0, 0, 1600, 900); Stretch(safe);
        var frame = Rect("Frame", safe, 0, 0, 1600, 900); var viewport = safe.gameObject.AddComponent<LandscapeViewport>(); viewport.frame = frame; viewport.Fit();
        root = Rect("SettingsOverlay", frame, 0, 0, 1600, 900).gameObject;
        Stretch((RectTransform)root.transform);
        var dim = Image("SettingsBackdrop", root.transform, 0, 0, 1600, 900, null, new Color(0, 0, 0, .7f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true; dim.gameObject.AddComponent<UiWorldTapBlocker>();
        var panel = Panel("Settings", root.transform, 370, 150, 860, 600, true);
        panel.rectTransform.anchorMin=panel.rectTransform.anchorMax=panel.rectTransform.pivot=new Vector2(.5f,.5f);
        panel.rectTransform.anchoredPosition=Vector2.zero;
        panel.gameObject.AddComponent<UiWorldTapBlocker>(); panel.gameObject.AddComponent<PanelRevealFeedback>();
        Text("Heading", panel.transform, "SOUND & MOTION", 44, 22, 700, 50, 36, null, true);
        Text("Description", panel.transform, "Audio, movement and display", 44, 78, 770, 30, 22, Muted);
        Text("Audio", panel.transform, "MASTER VOLUME", 44, 130, 770, 30, 22, null, true);
        master = AudioSettingsControls.VolumeSlider("MasterVolume", panel.transform, 44, 162, 770, GameFeedback.MasterVolume);
        master.onValueChanged.AddListener(v => GameFeedback.MasterVolume = v);
        var performance = Button("Performance", panel.transform, "TOGGLE 30 / 60 FPS", 44, 378, 770, 44);
        performance.onClick.AddListener(() =>
        {
            int fps = Application.targetFrameRate == 60 ? 30 : 60;
            Application.targetFrameRate = fps; PlayerPrefs.SetInt("HM.FrameRate", fps);
            var label = performance.GetComponentInChildren<TMPro.TextMeshProUGUI>(); if (label) label.text = "FRAME LIMIT: " + fps + " FPS";
        });
        var close = Button("CloseSettings", panel.transform, "SAVE & CLOSE", 44, 512, 500, 57, "red"); close.onClick.AddListener(() => Hide());
        controls = panel.gameObject.AddComponent<AudioSettingsControls>(); controls.Build(master);
        root.SetActive(false);
    }
}
