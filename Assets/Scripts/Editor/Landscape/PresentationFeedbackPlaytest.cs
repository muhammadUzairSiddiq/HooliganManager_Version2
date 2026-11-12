#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Native playback/settings/geometry checks in an isolated campaign. Does not certify audible device mix.</summary>
[InitializeOnLoad]
public static class PresentationFeedbackPlaytest
{
    const string Key = "HM.PresentationFeedbackPlaytest", Save = "Artifacts/Recovery/presentation-save.json", Report = "Artifacts/Recovery/presentation-feedback-playtest.txt";
    static readonly string[] FloatKeys = { "HM.MasterVolume", "HM.Audio.Music", "HM.Audio.Sfx", CrewMovementPreference.PaceKey };
    static readonly string[] IntKeys = { "HM.Audio.MusicMute", "HM.Audio.SfxMute", "HM.Presentation.ReducedMotion", CrewMovementPreference.RunKey, GameCacheBootstrap.InstallMarkerKey };
    static readonly List<string> checks = new List<string>();
    static bool running, finished; static double deadline;
    [Serializable] sealed class Pref { public string key; public bool exists, integer; public float value; }
    [Serializable] sealed class Snapshot { public List<Pref> entries = new List<Pref>(); }
    static PresentationFeedbackPlaytest()
    {
        if (SessionState.GetBool(Key, false)) SaveDataInLocal.EditorSaveOverride = Path.GetFullPath(Save);
        EditorApplication.playModeStateChanged += Changed; EditorApplication.update += Tick;
    }
    public static void BeginBatch() { SessionState.SetBool(Key + ".batch", true); Begin(); }
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run presentation checks from edit mode.");
        // A compilation failure can refuse Play after Begin has backed up user
        // preferences. Resume that pending fixture instead of overwriting its
        // original backup with the temporary test values.
        if(SessionState.GetBool(Key,false))
        {
            SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(Save);
            File.WriteAllText(Report,"RUNNING resumed pending fixture");
            EditorApplication.isPlaying=true;return;
        }
        Directory.CreateDirectory("Artifacts/Recovery");
        var snapshot = new Snapshot();
        foreach (var k in FloatKeys) snapshot.entries.Add(new Pref { key = k, exists = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetFloat(k), integer = false });
        foreach (var k in IntKeys) snapshot.entries.Add(new Pref { key = k, exists = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k), integer = true });
        SessionState.SetString(Key + ".prefs", JsonUtility.ToJson(snapshot));
        SessionState.SetString(Key + ".old", SaveDataInLocal.EditorSaveOverride ?? "");
        foreach (var k in FloatKeys.Concat(IntKeys)) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey, 1); CrewMovementPreference.ReloadPreferences();
        SaveDataInLocal.EditorSaveOverride = Path.GetFullPath(Save);
        var data = new PlayerData { PlayerName = "Presentation QA", MusicVolume = .35f, SFXVolume = .65f };
        RecoveryProgression.Begin(data); SaveDataInLocal.DataSave(data);
        SessionState.SetBool(Key, true); File.WriteAllText(Report, "RUNNING"); EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); running = finished = false; deadline = EditorApplication.timeSinceStartup + 90;
            SceneManager.LoadScene(GameManager.SCENE_MAIN_MENU);
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false); SaveDataInLocal.EditorSaveOverride = SessionState.GetString(Key + ".old", "");
            var snapshot = JsonUtility.FromJson<Snapshot>(SessionState.GetString(Key + ".prefs", "{}"));
            if (snapshot?.entries != null) foreach (var p in snapshot.entries)
            {
                if (!p.exists) PlayerPrefs.DeleteKey(p.key);
                else if (p.integer) PlayerPrefs.SetInt(p.key, Mathf.RoundToInt(p.value));
                else PlayerPrefs.SetFloat(p.key, p.value);
            }
            PlayerPrefs.Save(); CrewMovementPreference.ReloadPreferences(); AudioListener.volume = GameFeedback.MasterVolume;
            if (SessionState.GetBool(Key + ".batch", false)) { SessionState.SetBool(Key + ".batch", false); EditorApplication.Exit(checks.Any(c => c.StartsWith("FAIL ")) ? 1 : 0); }
        }
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || finished) return;
        if (EditorApplication.timeSinceStartup > deadline) { Finish("Presentation watchdog expired"); return; }
        if (running || GameManager.Data == null || !UnityEngine.Object.FindFirstObjectByType<LandscapeFrontEnd>() || !UnityEngine.Object.FindFirstObjectByType<GameFeedback>()) return;
        running = true;
        var host = new GameObject("Presentation QA runner"); UnityEngine.Object.DontDestroyOnLoad(host); host.AddComponent<PresentationFeedbackRunner>().StartCoroutine(Observe(Run()));
    }
    static IEnumerator Observe(IEnumerator routine)
    {
        while (true)
        {
            bool more = false; object value = null;
            try { more = routine.MoveNext(); if (more) value = routine.Current; }
            catch (Exception ex) { Finish(ex.ToString()); yield break; }
            if (!more) { Finish(null); yield break; } yield return value;
        }
    }
    static void Check(bool passed, string label) { checks.Add((passed ? "PASS " : "FAIL ") + label); File.WriteAllLines(Report, checks); }
    static void Finish(string error)
    {
        if (finished) return; finished = true; if (error != null) checks.Add("FAIL " + error);
        checks.Add("RESULT " + checks.Count(x => x.StartsWith("PASS ")) + " passed, " + checks.Count(x => x.StartsWith("FAIL ")) + " failed");
        File.WriteAllLines(Report, checks); Time.timeScale=1;EditorApplication.isPlaying = false;
    }
    static bool Same(Vector3[] before, Vector3[] after) => before.Zip(after, (a, b) => Vector3.Distance(a, b) < .01f).All(v => v);
    static bool CaptionFits(RectTransform button)
    {
        var label=button.Find("Label") as RectTransform;if(!label)return false;
        var corners=new Vector3[4];label.GetWorldCorners(corners);
        Vector3 center=button.TransformPoint(button.rect.center);
        if(Vector3.Distance((corners[0]+corners[2])*.5f,center)>.01f)return false;
        foreach(var corner in corners)
        {
            Vector3 local=button.InverseTransformPoint(corner);
            if(local.x<button.rect.xMin||local.x>button.rect.xMax||local.y<button.rect.yMin||local.y>button.rect.yMax)return false;
        }
        return true;
    }
    static void CheckTrafficReload()
    {
        var host=new GameObject("Traffic cache lifecycle QA");
        try
        {
            var car=host.AddComponent<ITHappy.Car>();
            var wheelHost=new GameObject("Wheel",typeof(MeshFilter),typeof(MeshRenderer));wheelHost.transform.SetParent(host.transform,false);
            var wheel=wheelHost.AddComponent<ITHappy.Wheel>();car.Init(host.transform,2,.25f);car.SetTransport(2,-2,4,.25f);
            Vector3 position=Vector3.right,forward=Vector3.forward,up=Vector3.up;
            car.SetTransform(ref position,ref forward,ref up,true,.1f);position=Vector3.right*3;car.SetTransform(ref position,ref forward,ref up,true,.1f);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(ITHappy.Car).GetField("m_Parts",flags).SetValue(car,null);
            typeof(ITHappy.Car).GetField("streamRenderers",flags).SetValue(car,null);
            typeof(ITHappy.Car).GetField("m_SampleTime",flags).SetValue(car,Time.time-.05f);
            typeof(ITHappy.Wheel).GetField("m_Renderer",flags).SetValue(wheel,null);
            var tick=typeof(ITHappy.Car).GetMethod("LateUpdate",flags);tick.Invoke(car,null);
            Check(((ITHappy.ICarPart[])typeof(ITHappy.Car).GetField("m_Parts",flags).GetValue(car)).Contains(wheel)&&host.transform.position.x>1&&host.transform.position.x<3,"Traffic hot-reload cache recovery preserves and advances the existing interpolation sample");
            Check(typeof(ITHappy.Wheel).GetField("m_Renderer",flags).GetValue(wheel)!=null&&Quaternion.Angle(wheel.transform.localRotation,Quaternion.identity)>.001f,"Actual wheel movement restores its lost renderer cache without exceptions");
            wheel.enabled=false;var rotation=wheel.transform.localRotation;tick.Invoke(car,null);
            Check(Quaternion.Angle(rotation,wheel.transform.localRotation)<.001f,"Disabled cached traffic parts do not receive movement updates");
            UnityEngine.Object.DestroyImmediate(wheel);tick.Invoke(car,null);
            Check(host.transform.position.x>=1,"Destroyed Unity components held by an interface cache are safely skipped");
            var prior=host.transform.position;host.SetActive(false);position=Vector3.one*99;car.SetTransform(ref position,ref forward,ref up,true,.1f);tick.Invoke(car,null);
            Check(host.transform.position==prior,"Disabled traffic targets ignore late incoming transform samples");host.SetActive(true);
            car.SetTransport(-1,-2,4,0);car.SetTransform(ref position,ref forward,ref up,true,.1f);tick.Invoke(car,null);car.GetTrafficStructs(out var data,out var route,out var pose);
            Check(route.index==-1&&data.speed==0&&car.Speed==0&&host.transform.position==prior,"Missing traffic routes retain the last safe pose and export an empty buffer slot");
            var target=new GameObject("Disposed traffic target");car.Init(target.transform,2,0);UnityEngine.Object.DestroyImmediate(target);tick.Invoke(car,null);car.GetTrafficStructs(out data,out route,out pose);
            Check(route.index==-1&&data.speed==0,"Destroyed traffic targets safely export an empty slot without stopping other traffic");
        }
        finally{UnityEngine.Object.DestroyImmediate(host);}
    }
    static void CheckLiveLayout(AudioSettingsControls controls,Vector2 logicalSize,string aspect)
    {
        var panel=(RectTransform)controls.transform;
        var overlay=(RectTransform)panel.parent;
        var frame=(RectTransform)overlay.parent;
        var original=frame.sizeDelta;
        try
        {
            frame.sizeDelta=logicalSize;Canvas.ForceUpdateCanvases();
            var backdrop=overlay.Find("SettingsBackdrop") as RectTransform;
            var edge=new Vector3[4];var cover=new Vector3[4];var card=new Vector3[4];
            frame.GetWorldCorners(edge);backdrop.GetWorldCorners(cover);panel.GetWorldCorners(card);
            bool centered=Vector3.Distance((card[0]+card[2])*.5f,(edge[0]+edge[2])*.5f)<.01f;
            Check(Same(edge,cover)&&centered,"Live settings backdrop covers the entire "+aspect+" frame and its panel remains centered");
        }
        finally {frame.sizeDelta=original;Canvas.ForceUpdateCanvases();}
    }
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.3f);
        CheckTrafficReload();
        var revealHost=new GameObject("Panel reveal lifecycle QA");var missingHost=new GameObject("Missing group cache QA");
        var requiredHost=new GameObject("Required group QA");int revealErrors=0;
        Application.LogCallback observeReveal=(message,stack,kind)=>{if(kind==LogType.Exception&&((stack??"").Contains("PanelRevealFeedback")||message.Contains("CanvasGroup")))revealErrors++;};
        Application.logMessageReceived+=observeReveal;
        try
        {
            var existingGroup=revealHost.AddComponent<CanvasGroup>();var reveal=revealHost.AddComponent<PanelRevealFeedback>();
            requiredHost.AddComponent<PanelRevealFeedback>();
            Check(requiredHost.GetComponent<CanvasGroup>()&&revealHost.GetComponents<CanvasGroup>().Length==1,"Panel reveal requires a real CanvasGroup and reuses an existing group without duplicates");
            Check(existingGroup.alpha>=.7f&&existingGroup.alpha<1,"Panel reveal sets the actual existing CanvasGroup opacity on enable");
            yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Approximately(existingGroup.alpha,1),"Native frame updates complete the real panel opacity reveal");
            var revealFlags=BindingFlags.Instance|BindingFlags.NonPublic;
            var groupCache=typeof(PanelRevealFeedback).GetField("group",revealFlags);
            groupCache.SetValue(reveal,missingHost.GetComponent<CanvasGroup>());
            typeof(PanelRevealFeedback).GetMethod("Update",revealFlags).Invoke(reveal,null);
            Check((CanvasGroup)groupCache.GetValue(reveal)==existingGroup,"A Unity missing-component cache is reacquired from the actual panel before alpha is accessed");
            yield return null;
            Check(revealErrors==0,"Actual panel lifecycle and bad-cache recovery produce no CanvasGroup exceptions");
        }
        finally
        {
            Application.logMessageReceived-=observeReveal;UnityEngine.Object.DestroyImmediate(revealHost);UnityEngine.Object.DestroyImmediate(missingHost);UnityEngine.Object.DestroyImmediate(requiredHost);
        }
        var owner = UnityEngine.Object.FindFirstObjectByType<GameFeedback>();
        var profile = Resources.Load<GameFeedbackProfile>("Presentation/GameFeedbackProfile");
        Check(profile && profile.cues.Length == Enum.GetValues(typeof(GameFeedbackEvent)).Length && profile.cues.All(c => c.clip && c.clip.samples > 0), "Every semantic event references an imported, non-empty authored clip");
        Check(profile.menuMusic && profile.explorationMusic && profile.matchdayMusic && profile.menuMusic != profile.matchdayMusic, "Menu/exploration and matchday use distinct authored music cues");
        Check(UnityEngine.Object.FindObjectsByType<GameFeedback>(FindObjectsSortMode.None).Length == 1 && owner.GetComponentsInChildren<AudioSource>().Length == 10, "One persistent service owns exactly two music sources and eight effects voices");
        Check(Mathf.Approximately(GameFeedback.MusicVolume, .35f) && Mathf.Approximately(GameFeedback.SfxVolume, .65f), "Existing campaign MusicVolume/SFXVolume are honored when new global preferences are absent");
        var front = UnityEngine.Object.FindFirstObjectByType<LandscapeFrontEnd>(); front.Navigate("settings"); yield return null;
        var panel = front.settingsPanel.transform.Find("Settings");
        Canvas.ForceUpdateCanvases();
        Check(CaptionFits(panel.Find("CloseSettings") as RectTransform)&&CaptionFits(panel.Find("Performance") as RectTransform),"Resized authored Save and Performance buttons retain centered captions wholly inside their actual hit areas");
        var musicSlider = panel.Find("MusicVolume").GetComponent<Slider>(); var sfxSlider = panel.Find("SfxVolume").GetComponent<Slider>();
        musicSlider.value = .27f; sfxSlider.value = .41f;
        Check(Mathf.Approximately(GameFeedback.MusicVolume, .27f) && Mathf.Approximately(GameFeedback.SfxVolume, .41f), "Actual settings slider callbacks independently update music and effects volumes");
        var mute = panel.Find("MusicMute").GetComponent<Button>(); mute.onClick.Invoke(); yield return null;
        var musicSources = owner.GetComponentsInChildren<AudioSource>().Where(s => s.gameObject.name.StartsWith("Music ")).ToArray();
        Check(GameFeedback.MusicMuted && !GameFeedback.SfxMuted && musicSources.All(s => s.mute), "Music mute affects both music channels without muting effects");
        mute.onClick.Invoke(); panel.Find("SfxMute").GetComponent<Button>().onClick.Invoke(); yield return null;
        Check(!GameFeedback.MusicMuted && GameFeedback.SfxMuted && owner.GetComponentsInChildren<AudioSource>().Where(s => s.gameObject.name.StartsWith("SFX ")).All(s => s.mute), "Effects mute affects all eight voices without muting music");
        var crowdHost = new GameObject("Muted stadium QA"); crowdHost.AddComponent<StadiumCrowdAudio>(); yield return null;
        Check(crowdHost.GetComponents<AudioSource>().Length == 2 && crowdHost.GetComponents<AudioSource>().All(s => s.mute && s.volume == 0), "New stadium sources immediately honor an already-muted effects bus");
        UnityEngine.Object.Destroy(crowdHost); panel.Find("SfxMute").GetComponent<Button>().onClick.Invoke(); yield return null;
        var polish = mute.GetComponent<LandscapeButtonPolish>(); var rect = mute.GetComponent<RectTransform>(); var before = new Vector3[4]; var after = new Vector3[4]; rect.GetWorldCorners(before);
        var pointer = new PointerEventData(EventSystem.current); polish.OnPointerDown(pointer); yield return null; rect.GetWorldCorners(after);
        Check(Same(before, after) && polish.label.transform.localScale.x < 1, "Press response decorates the label while the actual button hitbox stays fixed");
        polish.OnPointerUp(pointer); yield return new WaitForSecondsRealtime(.07f); rect.GetWorldCorners(after);
        Check(Same(before, after) && polish.label.transform.localScale.x > 1, "Release microbounce animates only decorative content, preserving the exact tap area");
        GameFeedback.ReducedMotion = true; polish.OnPointerDown(pointer); yield return null; rect.GetWorldCorners(after);
        Check(Same(before, after) && Mathf.Approximately(polish.label.transform.localScale.x, 1), "Reduced motion suppresses decorative bounce with unchanged button geometry"); polish.OnPointerUp(pointer);
        GameFeedback.ReducedMotion = false;
        for (int i = 0; i < 100; i++) GameFeedback.Emit(GameFeedbackEvent.CombatImpact, new Vector3(4, 0, 9));
        var impact = profile.cues.First(c => c.kind == GameFeedbackEvent.CombatImpact).clip;
        Check(owner.GetComponentsInChildren<AudioSource>().Count(s => s.clip == impact) == 1, "One hundred same-frame combat events are throttled to one actual pooled voice");
        for (int i = 0; i < 100; i++) GameFeedback.Emit(GameFeedbackEvent.Reward);
        var burst = owner.GetComponent<GameFeedbackBurst>();
        Check(burst && burst.ActiveCount == 1 && burst.GetComponentsInChildren<GameFeedbackStar>(true).Length == 36, "A burst of one hundred rewards reuses the six-slot decoration pool without allocating more stars");
        Check(!burst.GetComponentInChildren<GraphicRaycaster>(true) && burst.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "Success effects have no raycast surface and cannot block world commands");
        yield return new WaitForSecondsRealtime(.75f); Check(burst.ActiveCount == 0, "Success effects retire themselves after their bounded lifetime");
        GameFeedback.ReducedMotion = true; GameFeedback.Emit(GameFeedbackEvent.BuildComplete); yield return null;
        Check(burst.ActiveCount == 0, "Reduced motion suppresses success particles while leaving semantic audio available"); GameFeedback.ReducedMotion = false;
        // Change only the runtime profile copy to drive the real crossfade with two real clips.
        var clone = UnityEngine.Object.Instantiate(profile); clone.menuMusic = profile.matchdayMusic;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(GameFeedback).GetField("profile", flags).SetValue(owner, clone);
        typeof(GameFeedback).GetMethod("SelectMusic", flags).Invoke(owner, null);
        yield return new WaitForSecondsRealtime(.15f);
        Check(musicSources.All(s => s.volume > 0) && musicSources.Select(s => s.clip).Distinct().Count() == 2, "The real music mixer crossfades two imported cues concurrently");
        yield return new WaitForSecondsRealtime(1.6f);
        Check(musicSources.Single(s => s.clip == profile.matchdayMusic).volume > 0 && musicSources.Single(s => s.clip == profile.menuMusic).volume == 0, "Completed crossfade retires the outgoing cue rather than accumulating music sources");
        typeof(GameFeedback).GetField("profile", flags).SetValue(owner, profile); UnityEngine.Object.Destroy(clone);
        front.Navigate("close-settings"); var saved = SaveDataInLocal.DataLoad();
        Check(saved != null && Mathf.Approximately(saved.MusicVolume, .27f) && Mathf.Approximately(saved.SFXVolume, .41f), "Save & Close durably preserves both channel values in the isolated native campaign");
        float priorScale=Time.timeScale;GameFeedback.OpenSettings();yield return null;
        var live=owner.GetComponentInChildren<AudioSettingsControls>(true);
        Check(GameFeedback.SettingsOpen&&live&&Mathf.Approximately(Time.timeScale,priorScale),"Public live settings opens the shared controls without pausing or leaving the current scene");
        CheckLiveLayout(live,new Vector2(2000,900),"20:9");
        CheckLiveLayout(live,new Vector2(1600,1200),"4:3");
        Check(AgentSelectionManager.BlocksWorldTap(),"Open live settings blocks world commands through the shared input guard");
        live.transform.Find("MusicVolume").GetComponent<Slider>().value=.33f;live.transform.Find("SfxVolume").GetComponent<Slider>().value=.55f;
        Check(Mathf.Approximately(GameFeedback.MusicVolume,.33f)&&Mathf.Approximately(GameFeedback.SfxVolume,.55f),"Live settings sliders use the same independent music/effects callbacks");
        live.transform.Find("MusicVolume").GetComponent<Slider>().value=.27f;live.transform.Find("SfxVolume").GetComponent<Slider>().value=.41f;
        live.transform.Find("CloseSettings").GetComponent<Button>().onClick.Invoke();
        Check(!GameFeedback.SettingsOpen&&GameFeedback.SettingsInputBlocked&&Mathf.Approximately(Time.timeScale,priorScale),"Close consumes its release while retaining the live simulation speed");
        yield return null;yield return null;
        Check(!GameFeedback.SettingsInputBlocked,"Dismissal guard retires after its bounded release window");
        Time.timeScale=0;GameFeedback.OpenSettings();yield return null;GameFeedback.CloseSettings();
        Check(Mathf.Approximately(Time.timeScale,0),"Closing audio settings over an explicit Pause does not resume the simulation");Time.timeScale=priorScale;
        front.Navigate("settings"); yield return null; ScreenCapture.CaptureScreenshot("Artifacts/Recovery/presentation-settings.png"); yield return null;
        panel.Find("AudioCredits").GetComponent<Button>().onClick.Invoke(); yield return new WaitForSecondsRealtime(.2f);
        Check(GamePopup.AnyOpen && GameFeedback.AudioCredits.Contains("Gregor Quendel") && GameFeedback.AudioCredits.Contains("CC BY 4.0") && GameFeedback.AudioCredits.Contains("MintoDog"), "Settings exposes in-app credits for existing attributed crowd and new CC0 audio");
        ScreenCapture.CaptureScreenshot("Artifacts/Recovery/presentation-audio-credits.png"); yield return null; GamePopup.Instance.Hide();
        front.Navigate("close-settings");
        GameFeedback.MusicMuted = true; GameFeedback.SfxMuted = true; GameFeedback.ReducedMotion = true; GameFeedback.SavePreferences();
        SceneManager.LoadScene(GameManager.SCENE_MAIN_MENU); yield return new WaitForSecondsRealtime(.8f);
        Check(GameFeedback.MusicMuted && GameFeedback.SfxMuted && GameFeedback.ReducedMotion && Mathf.Approximately(GameFeedback.MusicVolume, .27f) && Mathf.Approximately(GameFeedback.SfxVolume, .41f), "Actual menu reload preserves independent volume, mute and reduced-motion preferences");
        Check(UnityEngine.Object.FindObjectsByType<GameFeedback>(FindObjectsSortMode.None).Length == 1 && owner.GetComponentsInChildren<AudioSource>().Length == 10, "Scene reload retains one bounded audio service rather than stacking music owners");
    }
}
public sealed class PresentationFeedbackRunner : MonoBehaviour { }
#endif
