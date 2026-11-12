using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameFeedbackEvent
{
    UiPress, UiOpen, UiClose, UnitSelected, MoveAccepted, CommandAccepted,
    CommandBlocked, BuildStarted, BuildComplete, TaskComplete, TaskFailed,
    CombatImpact, Loot, Arrest, Reward, Warning, TrainArrival, UnitFootstep
}

/// <summary>One bounded audio owner for authored music, semantic cues and decorative UI effects.</summary>
public sealed class GameFeedback : MonoBehaviour
{
    const string MusicKey = "HM.Audio.Music", SfxKey = "HM.Audio.Sfx";
    const string MusicMuteKey = "HM.Audio.MusicMute", SfxMuteKey = "HM.Audio.SfxMute";
    const string MotionKey = "HM.Presentation.ReducedMotion";
    static GameFeedback instance;
    GameFeedbackProfile profile;
    readonly Dictionary<GameFeedbackEvent, GameFeedbackProfile.Cue> cues = new Dictionary<GameFeedbackEvent, GameFeedbackProfile.Cue>();
    readonly Dictionary<AudioClip, float> played = new Dictionary<AudioClip, float>();
    readonly Dictionary<string, AudioClip> legacyClips = new Dictionary<string, AudioClip>();
    readonly AudioSource[] music = new AudioSource[2];
    readonly AudioSource[] voices = new AudioSource[8];
    readonly float[] voiceGains = new float[8];
    int nextUi, nextWorld, nextNotice, activeMusic;
    float transition = 1, duckUntil, nextStateCheck;
    GameFeedbackBurst bursts;
    AudioSettingsSheet settings;
    static int settingsDismissedFrame = -10;

    public static bool ReducedMotion { get => PlayerPrefs.GetInt(MotionKey, 0) != 0; set => PlayerPrefs.SetInt(MotionKey, value ? 1 : 0); }
    public static bool MusicMuted { get => PlayerPrefs.GetInt(MusicMuteKey, 0) != 0; set => PlayerPrefs.SetInt(MusicMuteKey, value ? 1 : 0); }
    public static bool SfxMuted { get => PlayerPrefs.GetInt(SfxMuteKey, 0) != 0; set => PlayerPrefs.SetInt(SfxMuteKey, value ? 1 : 0); }
    public static float MasterVolume { get => Mathf.Clamp01(PlayerPrefs.GetFloat("HM.MasterVolume", 1)); set { PlayerPrefs.SetFloat("HM.MasterVolume", Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); } }
    public static float MusicVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, GameManager.Data?.MusicVolume ?? 1));
        set { float v = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, v); if (GameManager.Data != null) GameManager.Data.MusicVolume = v; }
    }
    public static float SfxVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, GameManager.Data?.SFXVolume ?? 1));
        set { float v = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxKey, v); if (GameManager.Data != null) GameManager.Data.SFXVolume = v; }
    }
    public static float SfxGain => SfxMuted ? 0 : SfxVolume;
    public static bool SettingsOpen => instance && instance.settings && instance.settings.IsOpen;
    public static bool SettingsInputBlocked => SettingsOpen || Time.frameCount <= settingsDismissedFrame + 1;
    public static void OpenSettings()
    {
        var owner = Ensure(); if (!owner) return;
        if (!owner.settings) owner.settings = owner.gameObject.AddComponent<AudioSettingsSheet>();
        owner.settings.Show();
    }
    public static void CloseSettings() { if (instance && instance.settings) instance.settings.Hide(); }
    internal static void SettingsDismissed() { settingsDismissedFrame = Time.frameCount; AgentSelectionManager.ConsumeUiPointer(); }
    public const string AudioCredits = "MUSIC (CC0 1.0)\nUrban Theme — MintoDog · opengameart.org/content/urban-theme\nEspionage — brandon75689, submitted by HaelDB · opengameart.org/content/espionage\n\nUI / FOLEY / JINGLES (CC0 1.0)\nKenney · Kenney.nl · opengameart.org/users/kenney\ncreativecommons.org/publicdomain/zero/1.0/\n\nCROWD RECORDINGS (CC BY 4.0)\nGregor Quendel — Free Crowd Cheering Sounds · gregorquendel.com\nopengameart.org/content/free-crowd-cheering-sounds\ncreativecommons.org/licenses/by/4.0/ · original recordings unchanged";
    public static void SavePreferences() { PlayerPrefs.Save(); GameManager.Save(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() { settingsDismissedFrame=-10;Ensure(); }
    static GameFeedback Ensure()
    {
        if (!instance && Application.isPlaying)
        {
            var go = new GameObject("Game Feedback");
            DontDestroyOnLoad(go); instance = go.AddComponent<GameFeedback>();
        }
        return instance;
    }
    void Awake()
    {
        if (instance && instance != this) { Destroy(gameObject); return; }
        instance = this;
        profile = Resources.Load<GameFeedbackProfile>("Presentation/GameFeedbackProfile");
        if (profile && profile.cues != null) foreach (var cue in profile.cues) if (cue != null && cue.clip) cues[cue.kind] = cue;
        for (int i = 0; i < music.Length; i++)
        {
            music[i] = MakeSource("Music " + i); music[i].loop = true; music[i].priority = 32;
        }
        for (int i = 0; i < voices.Length; i++) voices[i] = MakeSource("SFX " + i);
        SceneManager.sceneLoaded += SceneLoaded;
        SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }
    AudioSource MakeSource(string label)
    {
        var child = new GameObject(label); child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>(); source.playOnAwake = false;
        source.ignoreListenerPause = true; source.dopplerLevel = 0; source.volume = 0;
        return source;
    }
    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        if (settings) settings.Hide(false);
        if (bursts) bursts.Clear();
        nextStateCheck = 0; SelectMusic();
    }
    void SelectMusic()
    {
        if (!profile) return;
        bool gameplay = SceneManager.GetActiveScene().name == GameManager.SCENE_GAME;
        var clip = gameplay ? (StadiumMatchdayActivity.IsMatchdayActive ? profile.matchdayMusic : profile.explorationMusic) : profile.menuMusic;
        if (!clip || music[activeMusic].clip == clip) return;
        activeMusic = 1 - activeMusic;
        var target = music[activeMusic]; target.Stop(); target.clip = clip; target.volume = 0;
        if (!MusicMuted && MusicVolume > 0 && MasterVolume > 0) target.Play();
        transition = 0;
    }
    void Update()
    {
        AudioListener.volume = MasterVolume;
        if (Time.unscaledTime >= nextStateCheck) { nextStateCheck = Time.unscaledTime + .5f; SelectMusic(); }
        transition = Mathf.Clamp01(transition + Time.unscaledDeltaTime / (profile ? Mathf.Max(.1f, profile.crossfadeSeconds) : 1.5f));
        float gain = (MusicMuted ? 0 : MusicVolume) * (profile ? profile.musicGain : .2f) * (Time.unscaledTime < duckUntil ? .35f : 1);
        for (int i = 0; i < music.Length; i++)
        {
            var source = music[i];
            source.volume = gain * (i == activeMusic ? Mathf.SmoothStep(0, 1, transition) : 1 - Mathf.SmoothStep(0, 1, transition));
            if (i == activeMusic && source.clip && !source.isPlaying && gain > 0 && MasterVolume > 0) source.Play();
            if (i != activeMusic && transition >= 1 && source.isPlaying) source.Stop();
            source.mute = MusicMuted;
        }
        for (int i = 0; i < voices.Length; i++) { voices[i].volume = voiceGains[i] * SfxGain; voices[i].mute = SfxMuted; }
    }
    public static void Emit(GameFeedbackEvent kind, Vector3? worldPoint = null)
    {
        var owner = Ensure(); if (!owner) return;
        bool sounded=owner.cues.TryGetValue(kind,out var cue)&&owner.PlayClip(cue.clip,cue.gain,cue.cooldown,kind,cue.spatial?worldPoint:null);
        bool success = kind == GameFeedbackEvent.BuildComplete || kind == GameFeedbackEvent.TaskComplete || kind == GameFeedbackEvent.Loot || kind == GameFeedbackEvent.Arrest || kind == GameFeedbackEvent.Reward;
        if (success && !ReducedMotion)
        {
            if (!owner.bursts) owner.bursts = owner.gameObject.AddComponent<GameFeedbackBurst>();
            owner.bursts.Play(worldPoint, kind == GameFeedbackEvent.Loot || kind == GameFeedbackEvent.Reward ? LandscapeUI.Gold : LandscapeUI.Green);
        }
        if (sounded&&(kind == GameFeedbackEvent.TaskComplete || kind == GameFeedbackEvent.TaskFailed)) owner.duckUntil = Time.unscaledTime + 1.8f;
    }
    bool PlayClip(AudioClip clip, float gain, float cooldown, GameFeedbackEvent kind, Vector3? point)
    {
        if (!clip || SfxGain <= 0 || MasterVolume <= 0) return false;
        if (played.TryGetValue(clip, out float when) && Time.unscaledTime - when < Mathf.Max(.065f, cooldown)) return false;
        bool ui = kind <= GameFeedbackEvent.UnitSelected;
        bool notice = kind == GameFeedbackEvent.TaskComplete || kind == GameFeedbackEvent.TaskFailed || kind == GameFeedbackEvent.BuildComplete || kind == GameFeedbackEvent.Warning || kind == GameFeedbackEvent.TrainArrival || kind == GameFeedbackEvent.Arrest || kind == GameFeedbackEvent.Loot || kind == GameFeedbackEvent.Reward;
        int slot = ui ? nextUi++ % 2 : notice ? 6 + nextNotice++ % 2 : 2 + nextWorld++ % 4;
        if(kind==GameFeedbackEvent.UnitFootstep)
        {
            slot=-1;for(int i=2;i<6;i++)if(!voices[i].isPlaying){slot=i;break;}
            if(slot<0)return false; // A footstep must not cut off a command or impact.
        }
        played[clip] = Time.unscaledTime;
        var source = voices[slot]; source.Stop(); source.clip = clip; source.pitch = 1;
        voiceGains[slot] = Mathf.Clamp01(gain); source.volume = voiceGains[slot] * SfxGain;
        source.mute = SfxMuted; source.spatialBlend = point.HasValue ? .55f : 0;
        source.priority = notice ? 48 : ui ? 64 : kind==GameFeedbackEvent.UnitFootstep?180:128;
        source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 8; source.maxDistance = 80;
        source.transform.position = point ?? Vector3.zero; source.Play();return true;
    }
    public static void PlayLegacy(string name)
    {
        switch (name)
        {
            case "click": Emit(GameFeedbackEvent.UiPress); return;
            case "popup": Emit(GameFeedbackEvent.UiOpen); return;
            case "move": Emit(GameFeedbackEvent.MoveAccepted); return;
            case "impact": Emit(GameFeedbackEvent.CombatImpact); return;
            case "hit": Emit(GameFeedbackEvent.CombatImpact); return;
            case "select": Emit(GameFeedbackEvent.UnitSelected); return;
            case "loading": Emit(GameFeedbackEvent.UiOpen); return;
            case "capture": Emit(GameFeedbackEvent.Loot); return;
            case "recovery": Emit(GameFeedbackEvent.TaskComplete); return;
            case "injury": Emit(GameFeedbackEvent.TaskFailed); return;
        }
        var owner = Ensure(); if (!owner || string.IsNullOrEmpty(name)) return;
        if (!owner.legacyClips.TryGetValue(name, out var clip)) { clip = Resources.Load<AudioClip>("Audio/" + name); owner.legacyClips[name] = clip; }
        owner.PlayClip(clip, .45f, .15f, GameFeedbackEvent.CommandAccepted, null);
    }
    void OnDestroy() { SceneManager.sceneLoaded -= SceneLoaded; if (instance == this) instance = null; }
}
