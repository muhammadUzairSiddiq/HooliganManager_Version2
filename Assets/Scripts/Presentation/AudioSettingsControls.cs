using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static LandscapeUI;

/// <summary>Shared settings controls created over the existing menu artwork.</summary>
public sealed class AudioSettingsControls : MonoBehaviour
{
    Slider music, sfx;
    Button musicMute, sfxMute, movement, pace, motion;
    TextMeshProUGUI musicLabel, sfxLabel;
    public void Build(Slider master)
    {
        var panel = transform;
        PlaceExisting("Heading", 44, 22, 700, 50);
        PlaceExisting("Description", 44, 78, 770, 30);
        var description = panel.Find("Description")?.GetComponent<TextMeshProUGUI>();
        if (description) { description.text = "Audio, movement and display"; description.fontSize = 22; }
        PlaceExisting("Audio", 44, 130, 770, 30);
        if (master) ConfigureMaster(master);
        PlaceExisting("Performance", 44, 378, 770, 44);
        PlaceExisting("CloseSettings", 44, 512, 500, 57);
        foreach (var old in new[] { "Controls", "TapMovementTitle", "TapMovement" }) { var t = panel.Find(old); if (t) t.gameObject.SetActive(false); }
        musicLabel = Text("MusicLabel", panel, "MUSIC", 44, 212, 550, 30, 22, null, true);
        music = VolumeSlider("MusicVolume", panel, 44, 244, 550, GameFeedback.MusicVolume);
        music.onValueChanged.AddListener(v => { GameFeedback.MusicVolume = v; Refresh(); });
        musicMute = Button("MusicMute", panel, "", 614, 211, 200, 58);
        musicMute.onClick.AddListener(() => { GameFeedback.MusicMuted = !GameFeedback.MusicMuted; Refresh(); });
        sfxLabel = Text("SfxLabel", panel, "EFFECTS", 44, 293, 550, 30, 22, null, true);
        sfx = VolumeSlider("SfxVolume", panel, 44, 325, 550, GameFeedback.SfxVolume);
        sfx.onValueChanged.AddListener(v => { GameFeedback.SfxVolume = v; Refresh(); });
        sfxMute = Button("SfxMute", panel, "", 614, 292, 200, 58);
        sfxMute.onClick.AddListener(() => { GameFeedback.SfxMuted = !GameFeedback.SfxMuted; Refresh(); });
        movement = Button("MovementMode", panel, "", 44, 434, 245, 54);
        movement.onClick.AddListener(() => { CrewMovementPreference.RunOnTap = !CrewMovementPreference.RunOnTap; Refresh(); });
        pace = Button("TravelPace", panel, "", 307, 434, 245, 54);
        pace.onClick.AddListener(() => { float p = CrewMovementPreference.SpeedMultiplier; CrewMovementPreference.SpeedMultiplier = p < 1.3f ? 1.65f : p < 1.9f ? 2.1f : 1; Refresh(); });
        motion = Button("ReducedMotion", panel, "", 570, 434, 244, 54);
        motion.onClick.AddListener(() => { GameFeedback.ReducedMotion = !GameFeedback.ReducedMotion; Refresh(); });
        var credits = Button("AudioCredits", panel, "AUDIO CREDITS", 564, 512, 250, 57);
        credits.onClick.AddListener(() =>
        {
            var live=GetComponentInParent<AudioSettingsSheet>();
            GamePopup.Instance.Show("AUDIO CREDITS",GameFeedback.AudioCredits,new GamePopup.Option("BACK",PanelColor,live?(System.Action)live.Show:null));
        });
        Refresh();
    }
    void PlaceExisting(string name, float x, float y, float w, float h)
    {
        var rt = transform.Find(name) as RectTransform;if(!rt)return;
        Place(rt,x,y,w,h);
        if(!rt.GetComponent<Button>())return;
        // Authored buttons have fixed-size decoration. Fit it to the new settings
        // slot as well as the hit area, preserving centered captions after resize.
        var label=rt.Find("Label") as RectTransform;
        if(label)
        {
            label.pivot=new Vector2(.5f,.5f);label.anchorMin=Vector2.zero;label.anchorMax=Vector2.one;
            label.offsetMin=new Vector2(16,4);label.offsetMax=new Vector2(-16,-4);
        }
        var shine=rt.Find("Shine") as RectTransform;if(shine)Place(shine,-w*.35f,9,Mathf.Max(30,w*.12f),Mathf.Max(1,h-18));
        var glow=rt.Find("SelectedGlow") as RectTransform;if(glow)Place(glow,18,h-7,Mathf.Max(1,w-36),3);
        var rim=rt.Find("ResponseRim") as RectTransform;if(rim)Place(rim,2,2,Mathf.Max(1,w-4),2);
    }
    public void Refresh()
    {
        if (music) music.SetValueWithoutNotify(GameFeedback.MusicVolume);
        if (sfx) sfx.SetValueWithoutNotify(GameFeedback.SfxVolume);
        if (musicLabel) musicLabel.text = "MUSIC  " + Mathf.RoundToInt(GameFeedback.MusicVolume * 100) + "%";
        if (sfxLabel) sfxLabel.text = "EFFECTS  " + Mathf.RoundToInt(GameFeedback.SfxVolume * 100) + "%";
        Caption(musicMute, GameFeedback.MusicMuted ? "MUSIC: MUTED" : "MUSIC: ON");
        Caption(sfxMute, GameFeedback.SfxMuted ? "EFFECTS: MUTED" : "EFFECTS: ON");
        Caption(movement, CrewMovementPreference.RunOnTap ? "TAP: RUN" : "TAP: WALK");
        Caption(pace, "PACE: " + CrewMovementPreference.PaceLabel);
        Caption(motion, GameFeedback.ReducedMotion ? "MOTION: REDUCED" : "MOTION: FULL");
    }
    static void Caption(Button b, string text) { if (b) { var label = b.GetComponentInChildren<TextMeshProUGUI>(); if (label) label.text = text; } }
    static void ConfigureMaster(Slider master)
    {
        Place(master.GetComponent<RectTransform>(), 44, 162, 770, 44);
        var hit = master.GetComponent<Image>() ?? master.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, .01f); hit.raycastTarget = true;
        var track = master.transform.Find("Track") as RectTransform; if (track) Place(track, 16, 19, 738, 6);
        var area = master.transform.Find("FillArea") as RectTransform; if (area) Place(area, 16, 19, 738, 6);
        if (master.handleRect)
        {
            if(master.handleRect.parent==master.transform)
            {
                var handle=master.handleRect;
                var handles=Rect("HandleArea",master.transform,16,0,738,44);
                handle.SetParent(handles,false);master.handleRect=null;master.handleRect=handle;
            }
            master.handleRect.pivot = new Vector2(.5f, .5f);
            master.handleRect.anchorMin = new Vector2(master.normalizedValue, 0); master.handleRect.anchorMax = new Vector2(master.normalizedValue, 1);
            master.handleRect.sizeDelta = new Vector2(24, -12); master.handleRect.anchoredPosition = Vector2.zero;
        }
    }
    public static Slider VolumeSlider(string name, Transform parent, float x, float y, float width, float value)
    {
        var rt = Rect(name, parent, x, y, width, 44);
        var hit = rt.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, .01f);
        Image("Track", rt, 16, 19, width - 32, 6, null, new Color(.15f, .22f, .25f));
        var area = Rect("FillArea", rt, 16, 19, width - 32, 6);
        var fill = Image("Fill", area, 0, 0, width - 32, 6, null, new Color(.31f, .84f, .69f)); Stretch(fill.rectTransform);
        var handles = Rect("HandleArea", rt, 16, 0, width - 32, 44);
        var handle = Image("Handle", handles, 0, 6, 24, 32, null, White);
        handle.rectTransform.pivot = new Vector2(.5f, .5f);
        handle.rectTransform.anchorMin = new Vector2(0, 0);
        handle.rectTransform.anchorMax = new Vector2(0, 1);
        handle.rectTransform.sizeDelta = new Vector2(24, -12);
        handle.rectTransform.anchoredPosition = Vector2.zero;
        handle.raycastTarget = false;
        var slider = rt.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1;
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.SetValueWithoutNotify(value); return slider;
    }
}
