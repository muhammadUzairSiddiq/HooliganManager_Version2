using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Persistent music and bounded SFX voices. Assets live in Resources/Audio.</summary>
public sealed class GameAudio : MonoBehaviour
{
    static GameAudio instance;
    AudioSource music;
    AudioSource[] voices;
    int voice;
    string musicName;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance) return;
        var go = new GameObject("Game Audio"); DontDestroyOnLoad(go); instance = go.AddComponent<GameAudio>();
    }
    void Awake()
    {
        music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.ignoreListenerPause = true;
        voices = new AudioSource[8];
        for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].ignoreListenerPause = true; }
        SceneManager.sceneLoaded += OnScene;
    }
    AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out var clip)) { clip = Resources.Load<AudioClip>("Audio/" + name); clips[name] = clip; }
        return clip;
    }
    void OnScene(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        string next = scene.name == "Gameplay" ? "gameplay_music" : "menu_music";
        if (next == musicName) return;
        musicName = next; music.clip = Clip(next); if (music.clip) music.Play();
    }
    void Update()
    {
        music.volume = (GameManager.Data?.MusicVolume ?? 1) * .22f;
        AudioListener.volume = PlayerPrefs.GetFloat("HM.MasterVolume", 1);
    }
    public static void Play(string name)
    {
        if (!instance) return;
        if (instance.lastPlayed.TryGetValue(name, out float time) && Time.unscaledTime - time < .065f) return;
        instance.lastPlayed[name] = Time.unscaledTime;
        var clip = instance.Clip(name); if (!clip) return;
        var source = instance.voices[instance.voice++ % instance.voices.Length];
        source.clip = clip; source.volume = (GameManager.Data?.SFXVolume ?? 1) * .65f;
        source.pitch = name == "impact" ? Random.Range(.9f, 1.1f) : 1; source.Play();
    }

    /// <summary>Short presentation cues pause the gameplay loop so victory and defeat
    /// screens read as deliberate moments even when no authored music asset exists.</summary>
    public static void PlayPresentationTheme(string kind)
    {
        if(instance)instance.StartCoroutine(instance.PresentationTheme(kind));
    }

    System.Collections.IEnumerator PresentationTheme(string kind)
    {
        bool defeat=string.Equals(kind,"defeat",System.StringComparison.OrdinalIgnoreCase);
        bool hadMusic=music!=null&&music.isPlaying;
        if(hadMusic)music.Pause();
        var source=voices[voice++%voices.Length];
        source.Stop();source.clip=BuildPresentationCue(defeat);source.volume=(GameManager.Data?.MusicVolume??1f)*.42f;source.pitch=1f;source.Play();
        yield return new WaitForSecondsRealtime(source.clip.length+.12f);
        if(hadMusic&&music!=null&&!music.isPlaying)music.UnPause();
    }

    static AudioClip BuildPresentationCue(bool defeat)
    {
        const int rate=22050;
        float[] notes=defeat?new[]{220f,196f,174.6f,146.8f}:new[]{261.6f,329.6f,392f,523.2f};
        int count=rate*2;
        var data=new float[count];
        for(int i=0;i<count;i++)
        {
            float time=i/(float)rate;
            int note=Mathf.Min(notes.Length-1,(int)(time/.46f));
            float local=(time-note*.46f)/.46f;
            float envelope=Mathf.Clamp01(local/.035f)*Mathf.Clamp01((.46f-local)/.18f);
            float tone=Mathf.Sin(time*Mathf.PI*2f*notes[note])*.58f+Mathf.Sin(time*Mathf.PI*2f*notes[note]*.5f)*.18f;
            data[i]=tone*envelope*.30f;
        }
        var clip=AudioClip.Create(defeat?"DefeatTheme":"VictoryTheme",count,1,rate,false);
        clip.SetData(data,0);return clip;
    }
    void OnDestroy() { SceneManager.sceneLoaded -= OnScene; if (instance == this) instance = null; }
}
