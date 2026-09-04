#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor Window — Portrait Capture Tool
/// Open via:  Window → Hooligan → Portrait Capture Tool
///
/// Workflow
/// ────────
/// 1. Drag your CharacterPortraitRegistry asset into the "Registry" slot.
/// 2. Add your model prefabs to the "Models to Capture" list.
///    (These should be the same prefabs used in BattleManager.characterList)
/// 3. Adjust camera framing, resolution, and save folder as needed.
/// 4. Click "Capture All Portraits" — each model is instantiated in a temp
///    scene context, photographed, saved as PNG, imported as a Sprite, and
///    assigned to the registry.
/// 5. The registry is saved automatically. Drag it to BattleManager / 
///    AgentPortraitCard as needed.
/// </summary>
public class PortraitCaptureEditor : EditorWindow
{
    // ── Window state ──────────────────────────────────────────────────────

    private CharacterPortraitRegistry _registry;
    private List<GameObject>          _models     = new List<GameObject>();
    private SerializedObject          _serialized;

    [Header("Save Settings")]
    private string _saveFolder   = "Assets/Art/Portraits";
    private int    _resolution   = 256;
    private string _filePrefix   = "portrait_";

    [Header("Camera Framing")]
    private Vector3 _camOffset   = new Vector3(0f, 1.55f, 2.2f);   // in front of model
    private Vector3 _lookAt      = new Vector3(0f, 1.2f,  0f);     // chest/face
    private float   _fov         = 30f;
    private Color   _bgColor     = new Color(0.08f, 0.08f, 0.08f, 0f); // transparent dark

    private Vector2 _scroll;
    private bool    _showFramingFoldout = true;

    [Header("General Animation Settings")]
    private RuntimeAnimatorController _generalAnimatorController;
    private AnimationClip             _generalClip;
    private float                     _generalSampleTime = 0.1f;

    // ─────────────────────────────────────────────────────────────────────

    [MenuItem("Window/Hooligan/Portrait Capture Tool")]
    public static void Open() =>
        GetWindow<PortraitCaptureEditor>("Portrait Capture Tool");

    // ── GUI ───────────────────────────────────────────────────────────────

    void OnGUI()
    {
        EditorGUILayout.Space(6);
        GUILayout.Label("Portrait Capture Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ── Registry ──────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Registry", EditorStyles.boldLabel);
        _registry = (CharacterPortraitRegistry)EditorGUILayout.ObjectField(
            "Character Portrait Registry", _registry,
            typeof(CharacterPortraitRegistry), false);

        EditorGUILayout.Space(8);

        // ── Models list ───────────────────────────────────────────────────
        EditorGUILayout.LabelField("Models to Capture", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag the same prefabs you use in BattleManager.characterList. " +
            "One portrait will be captured and saved per prefab.",
            MessageType.Info);

        EditorGUI.indentLevel++;
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(200));

        for (int i = 0; i < _models.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _models[i] = (GameObject)EditorGUILayout.ObjectField(
                $"Model {i}", _models[i], typeof(GameObject), false);

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                _models.RemoveAt(i);
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUI.indentLevel--;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Slot"))
            _models.Add(null);

        if (GUILayout.Button("Sync from Registry") && _registry != null)
            SyncModelsFromRegistry();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // ── Save settings ─────────────────────────────────────────────────
        EditorGUILayout.LabelField("Save Settings", EditorStyles.boldLabel);
        _saveFolder  = EditorGUILayout.TextField("Save Folder",  _saveFolder);
        _filePrefix  = EditorGUILayout.TextField("File Prefix",  _filePrefix);
        _resolution  = EditorGUILayout.IntSlider("Resolution",   _resolution, 64, 1024);

        EditorGUILayout.Space(6);

        // ── Camera framing ────────────────────────────────────────────────
        _showFramingFoldout = EditorGUILayout.Foldout(
            _showFramingFoldout, "Camera Framing", true);

        if (_showFramingFoldout)
        {
            EditorGUI.indentLevel++;
            _camOffset = EditorGUILayout.Vector3Field("Camera Offset (front of model)", _camOffset);
            _lookAt    = EditorGUILayout.Vector3Field("Look-At Point (relative)",       _lookAt);
            _fov       = EditorGUILayout.Slider("Field of View", _fov, 10f, 90f);
            _bgColor   = EditorGUILayout.ColorField(
                 new GUIContent("Background Color", "Use alpha=0 for transparent BG"),
                 _bgColor, true, true, false);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        // ── General Animation settings ────────────────────────────────────
        EditorGUILayout.LabelField("General Capture Animation", EditorStyles.boldLabel);
        _generalAnimatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
            "Animator Controller", _generalAnimatorController, typeof(RuntimeAnimatorController), false);
        _generalClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Animation Clip", _generalClip, typeof(AnimationClip), false);
        _generalSampleTime = EditorGUILayout.Slider(
            "Sample Time (0 to 1)", _generalSampleTime, 0f, 1f);

        EditorGUILayout.Space(12);

        // ── Action buttons ────────────────────────────────────────────────
        bool canCapture = _registry != null && _models.Count > 0;

        GUI.enabled = canCapture;
        GUI.backgroundColor = canCapture ? new Color(0.4f, 0.8f, 0.4f) : Color.grey;

        if (GUILayout.Button("▶  Capture All Portraits", GUILayout.Height(36)))
            CaptureAll();

        GUI.backgroundColor = Color.white;
        GUI.enabled         = true;

        if (!canCapture)
            EditorGUILayout.HelpBox(
                "Assign a CharacterPortraitRegistry and add at least one model to enable capture.",
                MessageType.Warning);
    }

    // ── Capture logic ─────────────────────────────────────────────────────

    private void CaptureAll()
    {
        // Ensure output folder exists
        if (!AssetDatabase.IsValidFolder(_saveFolder))
            CreateFolderRecursive(_saveFolder);

        int captured = 0;

        for (int i = 0; i < _models.Count; i++)
        {
            GameObject prefab = _models[i];
            if (prefab == null)
            {
                Debug.LogWarning($"[Portrait Capture] Slot {i} is empty — skipping.");
                continue;
            }

            EditorUtility.DisplayProgressBar(
                "Capturing Portraits",
                $"Rendering {prefab.name} ({i + 1}/{_models.Count})",
                (float)(i + 1) / _models.Count);

            string assetPath = $"{_saveFolder}/{_filePrefix}{prefab.name}.png";

            Sprite result = CapturePortrait(prefab, assetPath);

            if (result != null)
            {
                RegisterInRegistry(prefab, result);
                captured++;
            }
        }

        EditorUtility.ClearProgressBar();

        if (_registry != null)
        {
            EditorUtility.SetDirty(_registry);
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[Portrait Capture] Done — {captured}/{_models.Count} portraits captured.");
        EditorUtility.DisplayDialog("Portrait Capture",
            $"Captured {captured} portrait(s).\nSaved to: {_saveFolder}", "OK");
    }

    /// <summary>
    /// Instantiates the model prefab, renders it with a temporary camera,
    /// saves the result as a PNG Sprite asset, and returns the Sprite.
    /// </summary>
    private Sprite CapturePortrait(GameObject prefab, string assetPath)
    {
        // ── Instantiate model in scene ────────────────────────────────────
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // Face the camera (rotate 180° so it faces +Z by default)
        instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // Apply general pose / animation if configured
        if (_generalClip != null)
        {
            Animator anim = instance.GetComponent<Animator>();
            if (anim == null)
            {
                anim = instance.AddComponent<Animator>();
            }
            if (_generalAnimatorController != null)
            {
                anim.runtimeAnimatorController = _generalAnimatorController;
            }
            
            // Sample the animation clip at the specific normalized time
            float clipTime = _generalSampleTime * _generalClip.length;
            _generalClip.SampleAnimation(instance, clipTime);
        }

        // ── Create temporary camera ───────────────────────────────────────
        var camGO = new GameObject("__PortraitCaptureCam__");
        var cam   = camGO.AddComponent<Camera>();

        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = _bgColor;
        cam.fieldOfView     = _fov;
        cam.nearClipPlane   = 0.01f;
        cam.farClipPlane    = 20f;
        cam.cullingMask     = -1; // everything

        // Position relative to the model root
        camGO.transform.position = instance.transform.position +
                                   new Vector3(_camOffset.x, _camOffset.y, -_camOffset.z);

        Vector3 lookTarget = instance.transform.position + _lookAt;
        camGO.transform.LookAt(lookTarget);

        // ── Render to RenderTexture ───────────────────────────────────────
        var rt = new RenderTexture(_resolution, _resolution, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        // ── Read pixels ───────────────────────────────────────────────────
        RenderTexture.active = rt;
        var tex = new Texture2D(_resolution, _resolution, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, _resolution, _resolution), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // ── Save PNG ──────────────────────────────────────────────────────
        byte[] bytes = tex.EncodeToPNG();
        string fullPath = Path.GetFullPath(assetPath);
        File.WriteAllBytes(fullPath, bytes);
        AssetDatabase.ImportAsset(assetPath);

        // ── Set import settings → Sprite ──────────────────────────────────
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType        = TextureImporterType.Sprite;
            importer.spriteImportMode   = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled      = false;
            importer.filterMode         = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        // ── Cleanup ───────────────────────────────────────────────────────
        DestroyImmediate(instance);
        DestroyImmediate(camGO);
        rt.Release();
        DestroyImmediate(rt);
        DestroyImmediate(tex);

        return sprite;
    }

    /// <summary>
    /// Finds or creates the entry in the registry for this prefab and assigns the sprite.
    /// </summary>
    private void RegisterInRegistry(GameObject prefab, Sprite sprite)
    {
        if (_registry == null) return;

        // Look for existing entry
        foreach (var e in _registry.entries)
        {
            if (e.modelPrefab == prefab)
            {
                e.portrait = sprite;
                return;
            }
        }

        // No existing entry — create one
        _registry.entries.Add(new CharacterPortraitRegistry.Entry
        {
            modelPrefab = prefab,
            portrait    = sprite
        });
    }

    /// <summary>Populates the models list from the registry's existing entries.</summary>
    private void SyncModelsFromRegistry()
    {
        _models.Clear();
        foreach (var e in _registry.entries)
            if (e.modelPrefab != null)
                _models.Add(e.modelPrefab);
    }

    /// <summary>Creates nested folders recursively (AssetDatabase version).</summary>
    private static void CreateFolderRecursive(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
