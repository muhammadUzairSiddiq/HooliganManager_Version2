#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Exclusive tutorial raycasts, real arrival/work, first-use saves and skip without rewards.</summary>
[InitializeOnLoad]
public static class GuidedTutorialPlaytest
{
    const string Key="HM.GuidedTutorialPlaytest",Save="Artifacts/Recovery/guided-tutorial-save.json";
    static bool Preview=>SessionState.GetBool(Key+".preview",false);
    static bool UiSmoke=>SessionState.GetBool(Key+".uiSmoke",false);
    static string Report=>Preview?"Artifacts/Recovery/tutorial-preview-status.txt":UiSmoke?"Artifacts/Recovery/mobile-tutorial-ui-playtest.txt":"Artifacts/Recovery/guided-tutorial-playtest.txt";
    static string SavePath=>Preview?"Artifacts/Recovery/tutorial-preview-save.json":UiSmoke?"Artifacts/Recovery/mobile-tutorial-ui-save.json":Save;
    static readonly List<string> rows=new List<string>();
    static bool running,finished;static double deadline;
    static Touchscreen touchDevice;
    static readonly List<string> runtimeErrors=new List<string>();
    static GuidedTutorialPlaytest(){EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;if(SessionState.GetBool(Key,false))SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(SavePath);}
    [MenuItem("Hooligan/Recovery/Run exclusive first-use tutorial check")]
    public static void Begin()
    {SessionState.SetBool(Key+".preview",false);SessionState.SetBool(Key+".uiSmoke",false);BeginInternal();}
    [MenuItem("Hooligan/Recovery/Run mobile tutorial presentation regression")]
    public static void BeginUiSmoke()
    {SessionState.SetBool(Key+".preview",false);SessionState.SetBool(Key+".uiSmoke",true);BeginInternal();}
    [MenuItem("Hooligan/Recovery/Preview tutorial with disposable save")]
    public static void BeginPreview()
    {SessionState.SetBool(Key+".preview",true);SessionState.SetBool(Key+".uiSmoke",false);BeginInternal();}
    static void BeginInternal()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Start from edit mode.");
        if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Preserve the unsaved scene before starting this isolated review.");
        Directory.CreateDirectory("Artifacts/Recovery");RedesignAssets.Ensure();
        SessionState.SetString(Key+".old",SaveDataInLocal.EditorSaveOverride??"");
        SessionState.SetString(Key+".scene",SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key+".news",PlayerPrefs.GetInt(CityNewsFeed.ExpandedPreferenceKey,-1));
        SessionState.SetInt(Key+".marker",PlayerPrefs.GetInt(GameCacheBootstrap.InstallMarkerKey,-1));PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,1);
        SaveDataInLocal.EditorSaveOverride=Path.GetFullPath(SavePath);
        var d=new PlayerData{PlayerName="Guided tutorial QA",Money=5000,Fans=3};RecoveryProgression.Begin(d);
        foreach(var name in new[]{"Brick","Dex","Ash"})d.RecruitedAgents.Add(new AgentData(name,hp:100,speed:2.5f));
        SaveDataInLocal.DataSave(d);SessionState.SetBool(Key,true);File.WriteAllText(Report,"RUNNING "+DateTime.UtcNow.ToString("O"));
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){rows.Clear();runtimeErrors.Clear();Application.logMessageReceived+=Log;running=finished=false;GuidedCityTutorial.EditorExerciseTutorial=true;deadline=EditorApplication.timeSinceStartup+180;CityGameplay.HomeMode=true;LandscapeSceneBuilder.SetGameViewSize("1280x720");SceneManager.LoadScene("Gameplay");}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);GuidedCityTutorial.EditorExerciseTutorial=false;SaveDataInLocal.EditorSaveOverride=SessionState.GetString(Key+".old","");
            int old=SessionState.GetInt(Key+".marker",-1);if(old<0)PlayerPrefs.DeleteKey(GameCacheBootstrap.InstallMarkerKey);else PlayerPrefs.SetInt(GameCacheBootstrap.InstallMarkerKey,old);
            int news=SessionState.GetInt(Key+".news",-1);if(news<0)PlayerPrefs.DeleteKey(CityNewsFeed.ExpandedPreferenceKey);else PlayerPrefs.SetInt(CityNewsFeed.ExpandedPreferenceKey,news);
            string scene=SessionState.GetString(Key+".scene","");if(!string.IsNullOrEmpty(scene)&&SceneManager.GetActiveScene().path!=scene)UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene);
            Application.logMessageReceived-=Log;
            SessionState.SetBool(Key+".preview",false);SessionState.SetBool(Key+".uiSmoke",false);
        }
    }
    static void Log(string message,string stack,LogType type)
    {if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)if(runtimeErrors.Count<12)runtimeErrors.Add(message);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||finished||Preview)return;
        if(EditorApplication.timeSinceStartup>deadline){Finish($"Tutorial watchdog expired. Active={GuidedCityTutorial.BlocksInput}; Battle={BattleManager.instance?.BattleActive}; Crew={BattleManager.instance?.PlayerAgents.Count}; HUD={ModernCityHud.Instance}; Time={Time.timeScale}; Step={GuidedCityTutorial.Instance?.Step}");return;}
        if(running||!GuidedCityTutorial.BlocksInput)return;running=true;deadline=EditorApplication.timeSinceStartup+1150;
        var host=new GameObject("Isolated guided tutorial fixture");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<GuidedTutorialTestRunner>().StartCoroutine(Observe(UiSmoke?RunUiSmoke():Run()));
    }
    static IEnumerator Observe(IEnumerator routine)
    {while(true){bool more;object next=null;try{more=routine.MoveNext();if(more)next=routine.Current;}catch(Exception e){Finish(e.ToString());yield break;}if(!more){Finish(null);yield break;}yield return next;}}
    static void Check(bool value,string label){rows.Add((value?"PASS ":"FAIL ")+label);RecoveryReport.WriteLines(Report,rows);}
    static void Finish(string error)
    {
        if(finished)return;finished=true;if(error!=null)rows.Add("FAIL "+error);
        Application.logMessageReceived-=Log;
        if(touchDevice!=null){InputSystem.RemoveDevice(touchDevice);touchDevice=null;}
        Check(runtimeErrors.Count==0,"No runtime exceptions or error logs during tutorial input: "+string.Join(" | ",runtimeErrors));
        rows.Add($"RESULT {rows.Count(r=>r.StartsWith("PASS "))} passed, {rows.Count(r=>r.StartsWith("FAIL "))} failed");
        rows.Add(UiSmoke?"Isolated real gameplay Home walkthrough: native UI button passthrough, black rectangular highlights without blur, 100 HP/2.5 base-speed crew, real movement/guard/patrol/build/recruit results, explicit result confirmation, combat protection and restoration. Police selection/skip checked; Police physical jobs not repeated. No device validation.":"Isolated full real-gameplay Home and Police walkthrough: 100 HP/2.5 base-speed actors, native UI targets, result confirmation, actual movement/guard/patrol/build/recruit and Police boarding/driving/custody/crowd/recovery. No artificial speed or disabling rival directors. No device or natural-session validation.");rows.Add("UTC "+DateTime.UtcNow.ToString("O"));RecoveryReport.WriteLines(Report,rows);
        Time.timeScale=1;GuidedCityTutorial.EditorExerciseTutorial=false;if(PoliceCityMode.Active)PoliceCityMode.ExitToMenu();EditorApplication.isPlaying=false;
    }
    static IEnumerator Until(Func<bool> done,float seconds)
    {float end=Time.realtimeSinceStartup+seconds;while(!done()&&Time.realtimeSinceStartup<end)yield return null;}
    static GameObject TopHit(Vector2 p)
    {var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=p},hits);return hits.FirstOrDefault().gameObject;}
    static bool Tap(Vector2 p)
    {
        var top=TopHit(p);if(!top)return false;var data=new PointerEventData(EventSystem.current){position=p,button=PointerEventData.InputButton.Left};
        ExecuteEvents.ExecuteHierarchy(top,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(top,data,ExecuteEvents.pointerUpHandler);
        return ExecuteEvents.ExecuteHierarchy(top,data,ExecuteEvents.pointerClickHandler)!=null;
    }
    static bool ApprovedTap()
    {
        var tutorial=GuidedCityTutorial.Instance;var rect=tutorial.AllowedScreenRect;
        if(rect.width>1)
        {
            if(tutorial.CurrentGesture==TutorialGesture.Pan)
            {
                var top=TopHit(rect.center);if(!top)return false;
                var data=new PointerEventData(EventSystem.current){position=rect.center,delta=new Vector2(80,0),button=PointerEventData.InputButton.Left};
                ExecuteEvents.ExecuteHierarchy(top,data,ExecuteEvents.beginDragHandler);
                data.position+=data.delta;ExecuteEvents.ExecuteHierarchy(top,data,ExecuteEvents.dragHandler);
                ExecuteEvents.ExecuteHierarchy(top,data,ExecuteEvents.endDragHandler);return true;
            }
            if(tutorial.CurrentGesture==TutorialGesture.DoubleTap){Tap(rect.center);return Tap(rect.center);}
            return Tap(rect.center);
        }
        var view=UnityEngine.Object.FindAnyObjectByType<TutorialSpotlight>();var next=view?view.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Continue"&&b.gameObject.activeInHierarchy):null;
        return next&&Tap(GuidedCityTutorial.ScreenRect((RectTransform)next.transform).center);
    }
    static bool Navigation(string name)
    {
        var view=UnityEngine.Object.FindAnyObjectByType<TutorialSpotlight>();
        var button=view?view.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==name):null;
        return button&&button.IsInteractable()&&Tap(GuidedCityTutorial.ScreenRect((RectTransform)button.transform).center);
    }
    static IEnumerator DragTouch()
    {
        if(touchDevice==null)touchDevice=InputSystem.AddDevice<Touchscreen>("Tutorial isolated touch");
        var center=GuidedCityTutorial.Instance.AllowedScreenRect.center;
        for(int i=0;i<=10;i++)
        {
            InputSystem.QueueStateEvent(touchDevice,new TouchState{touchId=1,phase=i==0?UnityEngine.InputSystem.TouchPhase.Began:UnityEngine.InputSystem.TouchPhase.Moved,position=center+Vector2.right*Mathf.Lerp(-50,70,i/10f)});
            yield return null;
        }
        InputSystem.QueueStateEvent(touchDevice,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=center+Vector2.right*70});yield return null;
    }
    static IEnumerator Pinch(TutorialGesture gesture)
    {
        if(touchDevice==null)touchDevice=InputSystem.AddDevice<Touchscreen>("Tutorial isolated touch");
        var center=GuidedCityTutorial.Instance.AllowedScreenRect.center;
        float start=gesture==TutorialGesture.PinchOut?35:115,end=gesture==TutorialGesture.PinchOut?115:35;
        for(int i=0;i<=8;i++)
        {
            float gap=Mathf.Lerp(start,end,i/8f);
            var phase=i==0?UnityEngine.InputSystem.TouchPhase.Began:UnityEngine.InputSystem.TouchPhase.Moved;
            InputSystem.QueueStateEvent(touchDevice,new TouchState{touchId=1,phase=phase,position=center+Vector2.left*gap});
            InputSystem.QueueStateEvent(touchDevice,new TouchState{touchId=2,phase=phase,position=center+Vector2.right*gap});
            yield return null;
        }
        InputSystem.QueueStateEvent(touchDevice,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=center+Vector2.left*end});
        InputSystem.QueueStateEvent(touchDevice,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=center+Vector2.right*end});
        yield return null;
    }
    static IEnumerator RunUiSmoke(bool fullPolice=false)
    {
        yield return new WaitForSecondsRealtime(1);
        var guide=GuidedCityTutorial.Instance;var data=GameManager.Data;var selection=AgentSelectionManager.instance;
        Check(guide.LessonCount==32,"Essential Home guide reduced from 51 to 32 steps");
        Check(!UnityEngine.Object.FindAnyObjectByType<TutorialBackdropBlur>(),"No captured environment or blur layer replaces the real gameplay/UI");
        foreach(string size in new[]{"2400x1080","1024x768","1280x720"})
        {
            LandscapeSceneBuilder.SetGameViewSize(size);yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
            var view=UnityEngine.Object.FindAnyObjectByType<TutorialSpotlight>();var actual=guide.ActualTarget;
            Check(actual&&TopHit(guide.AllowedScreenRect.center)?.GetComponentInParent<Button>()==actual,"Pointer reaches the actual gameplay Button at "+size);
            var veil=view.GetComponentsInChildren<TutorialVeil>().First(v=>v.name=="InputShield");
            Check(veil.Rectangular&&veil.material==UnityEngine.UI.Graphic.defaultGraphicMaterial&&veil.color.a>.5f&&veil.color.a<.7f,"Real UI uses a rectangular highlight with translucent black, no blur, at "+size);
            Check(view.GetComponentsInChildren<Button>().All(b=>{var r=GuidedCityTutorial.ScreenRect((RectTransform)b.transform);return r.xMin>=0&&r.yMin>=0&&r.xMax<=Screen.width+1&&r.yMax<=Screen.height+1;}),"Navigation fits "+size);
            Check(!Navigation("Continue")&&Time.timeScale==0&&ModalPresentation.Instance.GetComponent<UnityEngine.Rendering.Volume>().weight==0,"NEXT cannot bypass a real action and tutorial pause keeps colour at "+size);
            ScreenCapture.CaptureScreenshot("Artifacts/Recovery/real-tutorial-"+size+".png");yield return null;
        }
        var protectedCrew=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();
        var health=protectedCrew.ToDictionary(a=>a,a=>a.CurrentHp);
        bool patrolWalked=false,walked=false,combatChecked=false,stadiumSeen=false;int safety=0;
        while(GuidedCityTutorial.BlocksInput&&safety++<80)
        {
            yield return new WaitForSecondsRealtime(.25f);
            int step=guide.Step;string title=guide.CurrentTitle;
            if(guide.ShowingResult)
            {
                Check(Time.timeScale==0,"Real result remains paused for inspection: "+title);
                yield return new WaitForSecondsRealtime(.8f);
                Check(guide.Step==step&&guide.ShowingResult,"Result does not immediately disappear: "+title);
                if(title=="SELECT A MEMBER")
                {
                    int selected=selection.SelectedAgents.Count;
                    Check(Navigation("Previous"),"BACK opens the preceding explanation");yield return new WaitForSecondsRealtime(.8f);
                    Check(selection.SelectedAgents.Count==selected&&Navigation("Continue"),"Reviewing BACK does not silently change real selection");yield return new WaitForSecondsRealtime(.8f);
                    Check(guide.Step==step&&guide.ShowingResult&&selection.SelectedAgents.Count==selected,"NEXT returns to the same visible result after review");
                }
                if(title=="SELECT A MEMBER"||title=="ADD A MEMBER"||title=="REMOVE ONE MEMBER"||title=="DESELECT ALL"||title=="START PATROL"||title=="STADIUM")
                {ScreenCapture.CaptureScreenshot("Artifacts/Recovery/real-tutorial-result-"+title.Replace(' ','-').ToLowerInvariant()+".png");yield return null;}
                if(title=="STADIUM")
                {
                    var sign=Camera.main.WorldToViewportPoint(StadiumMatchdayControl.Instance.TapPoint);
                    stadiumSeen=sign.z>0&&sign.x>.1f&&sign.x<.9f&&sign.y>.1f&&sign.y<.9f;
                    Check(stadiumSeen&&!TutorialAnchor.Find("world.matchday.start"),"Stadium lesson frames the live location without creating a start button");
                    var bounds=StadiumMatchdayControl.Instance.TutorialBounds;bool entireStadium=true;
                    for(int i=0;i<8;i++)
                    {
                        var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        var projected=Camera.main.WorldToViewportPoint(corner);
                        entireStadium&=projected.z>0&&projected.x>.02f&&projected.x<.98f&&projected.y>.02f&&projected.y<.98f;
                    }
                    Check(entireStadium,"Camera frames the actual stadium model as well as the sign, not just its nearby approach");
                }
                Check(Navigation("Continue"),"NEXT advances only after the visible real result: "+title);continue;
            }
            Check(Time.timeScale==0&&!guide.IsWaiting,"Instruction pauses real gameplay: "+title);
            var allowed=guide.AllowedScreenRect;var target=guide.ActualTarget;
            if(target)
            {
                var bounds=GuidedCityTutorial.ScreenRect((RectTransform)target.transform);
                Check(Vector2.Distance(bounds.center,allowed.center)<1&&Vector2.Distance(bounds.size,allowed.size)<1&&TopHit(allowed.center)?.GetComponentInParent<Button>()==target,"Highlight and native pointer match the real visible Button: "+title);
            }
            if(title=="ADD A MEMBER"){ScreenCapture.CaptureScreenshot("Artifacts/Recovery/real-tutorial-ui-add-member.png");yield return null;}
            if(guide.CurrentGesture==TutorialGesture.Pan)yield return DragTouch();
            else if(guide.CurrentGesture==TutorialGesture.PinchIn||guide.CurrentGesture==TutorialGesture.PinchOut)yield return Pinch(guide.CurrentGesture);
            else Check(ApprovedTap(),"Actual gameplay target accepts input: "+title);
            yield return null;
            Check(guide.Step==step,"An accepted action never skips straight to the next lesson: "+title);
            if(guide.IsWaiting)
            {
                Check(Time.timeScale>0&&!Navigation("Continue"),"Only real work resumes; NEXT cannot skip its result: "+title);
                var actor=selection.SelectedAgents.FirstOrDefault();var start=actor?actor.transform.position:Vector3.zero;
                float end=Time.realtimeSinceStartup+170;bool moved=false;
                while(guide.IsWaiting&&Time.realtimeSinceStartup<end)
                {
                    if(actor&&Vector3.Distance(start,actor.transform.position)>2)moved=true;
                    if(!combatChecked)
                    {
                        var rival=BattleManager.instance.EnemyAgents.FirstOrDefault(e=>e&&e.IsAlive);
                        float hp=protectedCrew[0].CurrentHp,enemyHp=rival?rival.CurrentHp:0;
                        if(rival){rival.AlertToTarget(protectedCrew[0]);rival.TakeDamage(99999,protectedCrew[0]);}
                        protectedCrew[0].TakeDamage(99999,rival);
                        Check(protectedCrew[0].IsAlive&&protectedCrew[0].CurrentHp==hp&&(!rival||rival.CurrentHp==enemyHp),"Tutorial blocks real damage to crew and rivals even during resumed movement");combatChecked=true;
                    }
                    yield return null;
                }
                Check(!guide.IsWaiting&&guide.ShowingResult,"Real arrival/work completes before presenting a result: "+title);
                if(guide.IsWaiting)throw new Exception("Real work timed out: "+title);
                if(title=="MOVE"){walked=moved;Check(moved,"MOVE physically travels through the actual city");}
                if(title=="START PATROL"){patrolWalked=moved;Check(moved&&data.LearnedPatrol,"Patrol physically visits both chosen points before NEXT becomes available");}
                Check(protectedCrew.All(a=>a&&a.IsAlive&&a.CurrentHp>=health[a]),"No tutorial fight kills or injures a real member: "+title);
            }
        }
        Check(!GuidedCityTutorial.BlocksInput&&walked&&patrolWalked&&stadiumSeen,"Real Home walkthrough completes with movement, patrol and stadium activities");
        Check(HomeTerritorySystem.Built(data,0)&&!HomeTerritorySystem.Built(data,1)&&!HomeTerritorySystem.Built(data,2)&&data.RecruitedAgents.Count==4&&data.LearnedGuard&&data.LearnedPatrol,"Only practically completed building, recruitment and command results are saved");
        yield return null;
        Check(!GuidedCityTutorial.CombatSuppressed&&Time.timeScale>0&&!UnityEngine.Object.FindAnyObjectByType<TutorialSpotlight>(),"Tutorial exit restores live combat, time and normal input");
        float previousHp=protectedCrew[0].CurrentHp;protectedCrew[0].TakeDamage(1);
        Check(protectedCrew[0].CurrentHp<previousHp,"Normal gameplay damage works again after the tutorial");
        BattlePauseMenuController.instance.Show();yield return null;yield return null;
        Check(ModalPresentation.Instance.GetComponent<UnityEngine.Rendering.Volume>().weight==1,"Real pause still applies the silver world");
        BattlePauseMenuController.instance.Hide();yield return null;
        PoliceCityMode.Launch(true);yield return Until(()=>PoliceCityMode.Instance&&PoliceCityMode.Instance.Ready&&GuidedCityTutorial.BlocksInput,120);
        guide=GuidedCityTutorial.Instance;
        for(int i=0;i<2;i++)
        {
            yield return new WaitForSecondsRealtime(.3f);Check(ApprovedTap(),"Real Police officer UI accepts its own native button");
            yield return new WaitForSecondsRealtime(.8f);Check(guide.ShowingResult&&Navigation("Continue"),"Police selection also waits for visible result and NEXT");
        }
        safety=0;
        if(fullPolice)
        {
            var officers=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();
            var officerHealth=officers.ToDictionary(a=>a,a=>a.CurrentHp);
            while(GuidedCityTutorial.BlocksInput&&safety++<100)
            {
                yield return new WaitForSecondsRealtime(.3f);string title=guide.CurrentTitle;
                if(guide.ShowingResult)
                {
                    yield return new WaitForSecondsRealtime(.8f);
                    Check(Time.timeScale==0&&Navigation("Continue"),"Police result remains paused until NEXT: "+title);continue;
                }
                Check(Time.timeScale==0&&ApprovedTap(),"Real Police action uses its existing target: "+title);yield return null;
                if(guide.IsWaiting)
                {
                    Check(Time.timeScale>0&&!Navigation("Continue"),"Police real work resumes without premature NEXT: "+title);
                    yield return Until(()=>!guide.IsWaiting,200);
                    Check(!guide.IsWaiting&&guide.ShowingResult,"Police physical action completes before its result: "+title);
                    if(guide.IsWaiting)throw new Exception("Police work timed out: "+title);
                    Check(officers.All(a=>a&&a.IsAlive&&a.CurrentHp>=officerHealth[a]),"Tutorial keeps real officers alive during work: "+title);
                }
            }
            var career=PoliceCityMode.Career;
            Check(!GuidedCityTutorial.BlocksInput&&Time.timeScale>0&&career.PoliceTutorialVersion==GuidedCityTutorial.Version,"Police physical walkthrough completes and releases input");
            Check(PoliceCityMode.Instance.PrimaryArrestCall?.resolution=="BOOKED"&&career.booked>0,"Police tutorial books the actual suspect after loading and driving back");
            Check(PoliceCityMode.Instance.CrowdOperations.CurrentState>=2,"Police crowd response is a completed physical operation");
            Check(PoliceCityMode.Instance.TutorialRoadIncident?.IsCleared==true&&PoliceRecoveryVehicle.Instance&&!PoliceRecoveryVehicle.Instance.IsDriving,"Police clears the road and physically drives the recovery truck");
            Check(career.acceptedBribes==0,"The tutorial never forces a bribe");
        }
        else
        {
            while(GuidedCityTutorial.BlocksInput&&safety++<12)
            {yield return new WaitForSecondsRealtime(.25f);Check(Navigation("SkipPart"),"Explicit Police per-part skip remains clickable");}
            Check(!GuidedCityTutorial.BlocksInput&&PoliceCityMode.Career.booked==0&&PoliceCityMode.Career.arrests==0&&PoliceCityMode.Career.acceptedBribes==0,"Skipping Police physical lessons grants no invented custody/bribe results");
        }
    }
    static IEnumerator Run() {yield return RunUiSmoke(true);}

}
public sealed class GuidedTutorialTestRunner:MonoBehaviour{}
#endif
