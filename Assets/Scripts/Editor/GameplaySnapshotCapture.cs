#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders the live gameplay city (no HUD) from the play camera, a top view, and a second 3D angle.
/// Launch: Unity -batchmode -hmCaptureCity -executeMethod GameplaySnapshotCapture.StartFromCommandLine
/// </summary>
[InitializeOnLoad]
public static class GameplaySnapshotCapture
{
    const string Key = "HM.CaptureCityShots";
    const string Folder = "Assets/UI/GameplayShots";
    const string StatusPath = "Artifacts/city-shots/status.txt";

    static readonly Shot[] Shots =
    {
        new Shot("city_iso", 52f, 45f, 58f),
        new Shot("city_top", 80f, 20f, 95f),
        new Shot("city_angle", 46f, 140f, 72f)
    };

    static int stage = -1;
    static int shot;
    static double deadline;
    static double poseAt;
    static bool grabbing;
    static int grabToken;
    static double grabDeadline;
    static Bounds city;
    static Camera cam;

    struct Shot
    {
        public string name;
        public float pitch, yaw, distance;
        public Shot(string name, float pitch, float yaw, float distance)
        {
            this.name = name;
            this.pitch = pitch;
            this.yaw = yaw;
            this.distance = distance;
        }
    }

    static GameplaySnapshotCapture()
    {
        if (!Armed()) return;
        EditorApplication.playModeStateChanged += OnPlay;
        EditorApplication.update += Tick;
    }

    public static void ApplySavedShots()
    {
        stage = 100;
        AssignAndQuit();
    }

    public static void StartFromCommandLine()
    {
        Directory.CreateDirectory("Artifacts/city-shots");
        SessionState.SetBool(Key, true);
        Log("command start");
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.isPlaying = true;
    }

    static bool Armed()
    {
        if (SessionState.GetBool(Key, false)) return true;
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg == "-hmCaptureCity") return true;
        return false;
    }

    static void OnPlay(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Key, false) && !HasArg()) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            CityGameplay.HomeMode = true;
            stage = 0;
            shot = 0;
            grabbing = false;
            deadline = EditorApplication.timeSinceStartup + 180;
            Log("entered play, loading Gameplay");
            SceneManager.LoadScene("Gameplay");
        }
        else if (change == PlayModeStateChange.EnteredEditMode && stage == 50)
        {
            stage = 100;
        }
    }

    static bool HasArg()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg == "-hmCaptureCity") return true;
        return false;
    }

    static void Tick()
    {
        if (!Armed() || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            if (!SessionState.GetBool(Key, false))
                SessionState.SetBool(Key, true);

            if (stage < 0 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Log("arming play mode");
                EditorApplication.isPlaying = true;
                stage = 0;
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                if (stage == 100) AssignAndQuit();
                return;
            }

            if (grabbing)
            {
                if (EditorApplication.timeSinceStartup > grabDeadline)
                {
                    grabToken++;
                    Advance("grab timed out");
                }
                return;
            }
            if (stage == 0) WaitForCity();
            else if (stage == 1 && EditorApplication.timeSinceStartup >= poseAt) BeginGrab();
        }
        catch (Exception e)
        {
            Log(e.ToString());
            SessionState.SetBool(Key, false);
            stage = 101;
        }
    }

    static void WaitForCity()
    {
        var battle = BattleManager.instance;
        if (!battle || !battle.playerSpawnRoot || !CityGameplay.Instance)
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Gameplay city was not ready in time.");
            return;
        }
        if (EditorApplication.timeSinceStartup < deadline - 168) return;

        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            canvas.enabled = false;
        foreach (var label in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (label) label.gameObject.SetActive(false);
        foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!behaviour) continue;
            var name = behaviour.GetType().Name;
            if (name == "CameraPanTouchOnly" || name == "GameplayReadability")
                behaviour.enabled = false;
        }

        cam = Camera.main;
        if (!cam) throw new Exception("Gameplay camera is missing.");
        city = MeasureCity();
        Log("city center=" + city.center + " size=" + city.size);
        stage = 1;
        Pose();
    }

    static Bounds MeasureCity()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.one);
        bool any = false;
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!renderer || !renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
            if (renderer.GetComponentInParent<Canvas>()) continue;
            var size = renderer.bounds.size;
            if (size.x > 500f || size.z > 500f || size.sqrMagnitude < 0.25f) continue;
            if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!any) throw new Exception("No city geometry to photograph.");
        return bounds;
    }

    static void Pose()
    {
        var view = Shots[shot];
        var focus = new Vector3(city.center.x, 0f, city.center.z);
        float distance = view.distance;
        if (distance <= 0f)
        {
            float radius = Mathf.Max(city.extents.x, city.extents.z, 30f);
            float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            distance = radius / Mathf.Tan(halfFov) / Mathf.Max(0.25f, Mathf.Sin(view.pitch * Mathf.Deg2Rad));
            distance *= 1.08f;
        }
        var rotation = Quaternion.Euler(view.pitch, view.yaw, 0f);
        cam.orthographic = false;
        cam.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, distance + city.size.magnitude);
        Log("pose " + view.name + " at " + cam.transform.position + " dist " + distance.ToString("F0"));
        poseAt = EditorApplication.timeSinceStartup + 0.6;
    }

    static void BeginGrab()
    {
        grabbing = true;
        grabToken++;
        grabDeadline = EditorApplication.timeSinceStartup + 8;
        int token = grabToken;
        var host = new GameObject("CityShotGrabber");
        UnityEngine.Object.DontDestroyOnLoad(host);
        var grabber = host.AddComponent<CityShotGrabber>();
        grabber.fileName = Shots[shot].name;
        grabber.camera = cam;
        grabber.onDone = note =>
        {
            if (token != grabToken) return;
            Advance(note);
        };
    }

    static void Advance(string note)
    {
        Log(note);
        grabbing = false;
        shot++;
        if (shot < Shots.Length)
        {
            Pose();
            return;
        }
        stage = 50;
        EditorApplication.isPlaying = false;
    }

    static void AssignAndQuit()
    {
        stage = 101;
        AssetDatabase.Refresh();
        var iso = LoadSprite("city_iso.png");
        var top = LoadSprite("city_top.png");
        var angle = LoadSprite("city_angle.png");
        if (!iso || !top || !angle) throw new Exception("One of the gameplay shots failed to import.");

        var theme = LandscapeTheme.Current;
        if (theme)
        {
            theme.mainBackground = angle;
            theme.townBackground = top;
            theme.tacticalBackground = iso;
            EditorUtility.SetDirty(theme);
        }

        AssignBackdrop("Assets/Scenes/MainMenu.unity", angle);
        AssignBackdrop("Assets/Scenes/DashboardScene.unity", top);
        AssetDatabase.SaveAssets();
        SessionState.SetBool(Key, false);
        Log("assigned menu=city_angle dashboard=city_top");
    }

    static Sprite LoadSprite(string file)
    {
        var path = Folder + "/" + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return null;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void AssignBackdrop(string scenePath, Sprite sprite)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        foreach (var image in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.gameObject.name != "Backdrop") continue;
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
            EditorUtility.SetDirty(image);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void Log(string message)
    {
        Directory.CreateDirectory("Artifacts/city-shots");
        File.AppendAllText(StatusPath, DateTime.Now.ToString("s") + " " + message + "\n");
        Debug.Log("[CityShots] " + message);
    }

    sealed class CityShotGrabber : MonoBehaviour
    {
        public string fileName;
        public Camera camera;
        public Action<string> onDone;
        float waited;

        void Update()
        {
            waited += Time.unscaledDeltaTime;
            if (waited < 0.35f) return;
            try
            {
                var directory = Path.Combine(Application.dataPath, "UI/GameplayShots");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, fileName + ".png");
                var tex = Grab(camera);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
                onDone?.Invoke("saved " + path + " " + new FileInfo(path).Length);
            }
            catch (Exception e)
            {
                onDone?.Invoke("FAILED " + fileName + " " + e);
            }
            Destroy(gameObject);
        }

        static Texture2D Grab(Camera source)
        {
            var previous = RenderTexture.active;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var prior = source.targetTexture;
            source.targetTexture = rt;
            source.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            source.targetTexture = prior;
            RenderTexture.active = previous;
            UnityEngine.Object.Destroy(rt);
            return tex;
        }
    }
}
#endif
