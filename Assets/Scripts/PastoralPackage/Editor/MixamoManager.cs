using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class MixamoManager : EditorWindow
{
    private static MixamoManager editor;
    private static int width  = 460;
    private static int height = 200;

    // Path the user wants to scan — defaults to the entire Assets folder
    private string searchPath = "";

    // Cleared on every run to prevent accumulation bugs
    private List<string> allFiles = new List<string>();

    [MenuItem("Window/Mixamo Manager")]
    static void ShowEditor()
    {
        editor = EditorWindow.GetWindow<MixamoManager>("Mixamo Manager");
        CenterWindow();
    }

    private void OnGUI()
    {
        // Initialise lazily so Application.dataPath is available
        if (string.IsNullOrEmpty(searchPath))
            searchPath = Application.dataPath; // defaults to <project>/Assets

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Mixamo Clean-up Tools", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // ── Path row ─────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search Path", GUILayout.Width(80));
        searchPath = EditorGUILayout.TextField(searchPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string chosen = EditorUtility.OpenFolderPanel(
                "Select folder to scan for FBX files",
                string.IsNullOrEmpty(searchPath) ? Application.dataPath : searchPath,
                "");
            if (!string.IsNullOrEmpty(chosen))
                searchPath = chosen;
        }
        EditorGUILayout.EndHorizontal();
        // ─────────────────────────────────────────────────────────────────────

        EditorGUILayout.Space(8);

        if (GUILayout.Button("Fix Animation Clip Names", GUILayout.Height(30)))
            RenameClipsInFolder();

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Fix Rig Bone Names  ( mixamorig12: → mixamorig: )", GUILayout.Height(30)))
            FixRigBonesInFolder();
    }

    // -------------------------------------------------------------------------
    //  Animation clip name fixer
    // -------------------------------------------------------------------------

    public void RenameClipsInFolder()
    {
        allFiles.Clear();
        DirSearch();

        if (allFiles.Count == 0)
        {
            Debug.LogWarning($"[MixamoManager] No FBX files found in: {searchPath}");
            return;
        }

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string filePath in allFiles)
            {
                int idx = filePath.IndexOf("Assets");
                string assetPath = filePath.Substring(idx);

                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                string fileName = Path.GetFileNameWithoutExtension(filePath);
                RenameAndImport(importer, fileName);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            Debug.Log($"[MixamoManager] Processed {allFiles.Count} file(s) for clip renaming.");
        }
    }

    private void RenameAndImport(ModelImporter modelImporter, string name)
    {
        ModelImporterClipAnimation[] clips = modelImporter.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = modelImporter.defaultClipAnimations;

        if (clips == null || clips.Length == 0) return;

        for (int i = 0; i < clips.Length; i++)
            clips[i].name = name;

        modelImporter.clipAnimations = clips;
        modelImporter.SaveAndReimport();
    }

    // -------------------------------------------------------------------------
    //  Rig bone name fixer
    //  Patches humanDescription.skeleton[] and humanDescription.human[] inside
    //  the ModelImporter so the Avatar maps to the canonical "mixamorig:" bones,
    //  then reimports to also trigger OnPostprocessModel (which renames Transforms).
    // -------------------------------------------------------------------------

    public void FixRigBonesInFolder()
    {
        allFiles.Clear();
        DirSearch();

        if (allFiles.Count == 0)
        {
            Debug.LogWarning($"[MixamoManager] No FBX files found in: {searchPath}");
            return;
        }

        int fixedCount = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string filePath in allFiles)
            {
                int idx = filePath.IndexOf("Assets");
                string assetPath = filePath.Substring(idx);

                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                bool patched = FixImporterBoneNames(importer);
                if (patched)
                {
                    importer.SaveAndReimport();
                    fixedCount++;
                    Debug.Log($"[MixamoManager] Fixed rig bones: {assetPath}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            Debug.Log($"[MixamoManager] Fixed rig bones on {fixedCount} / {allFiles.Count} FBX file(s).");
        }
    }

    /// <summary>
    /// Renames skeleton and humanoid bone references inside the ModelImporter's
    /// humanDescription, normalising mixamorig12:, mixamorig4: → mixamorig:.
    /// Returns true if anything was changed.
    /// </summary>
    private static bool FixImporterBoneNames(ModelImporter importer)
    {
        // Only humanoid rigs have an avatar/humanDescription to fix
        if (importer.animationType != ModelImporterAnimationType.Human)
            return false;

        HumanDescription hd = importer.humanDescription;
        bool dirty = false;

        // Fix raw SkeletonBone names (straight from the FBX node names)
        SkeletonBone[] skeleton = hd.skeleton;
        for (int i = 0; i < skeleton.Length; i++)
        {
            string original = skeleton[i].name;
            string fixed_   = MixamoBonePurgePostprocessor.NormaliseBoneName(original);
            if (original == fixed_) continue;

            skeleton[i] = new SkeletonBone
            {
                name     = fixed_,
                position = skeleton[i].position,
                rotation = skeleton[i].rotation,
                scale    = skeleton[i].scale
            };
            dirty = true;
        }

        // Fix HumanBone.boneName references (what each humanoid muscle maps to)
        HumanBone[] human = hd.human;
        for (int i = 0; i < human.Length; i++)
        {
            string original = human[i].boneName;
            string fixed_   = MixamoBonePurgePostprocessor.NormaliseBoneName(original);
            if (original == fixed_) continue;

            human[i] = new HumanBone
            {
                boneName  = fixed_,
                humanName = human[i].humanName,
                limit     = human[i].limit
            };
            dirty = true;
        }

        if (dirty)
        {
            hd.skeleton          = skeleton;
            hd.human             = human;
            importer.humanDescription = hd;
        }

        return dirty;
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    private static void CenterWindow()
    {
        editor = EditorWindow.GetWindow<MixamoManager>();
        int x = (Screen.currentResolution.width  - width)  / 2;
        int y = (Screen.currentResolution.height - height) / 2;
        editor.position = new Rect(x, y, width, height);
        editor.maxSize  = new Vector2(width, height * 2); // allow resize for long paths
        editor.minSize  = new Vector2(width, height);
    }

    private void DirSearch()
    {
        string info = string.IsNullOrWhiteSpace(searchPath)
            ? Application.dataPath
            : searchPath;

        if (!Directory.Exists(info))
        {
            Debug.LogWarning($"[MixamoManager] Folder not found: {info}");
            return;
        }

        string[] files = Directory.GetFiles(info, "*.fbx", SearchOption.AllDirectories);
        foreach (string f in files)
            allFiles.Add(f);
    }
}

// =============================================================================
//  Asset postprocessor — fires automatically on every FBX import/reimport
//  1. OnPostprocessModel     → renames the actual bone Transform names
//  2. OnPostprocessAnimation → renames bone path strings inside animation curves
// =============================================================================
public class MixamoBonePurgePostprocessor : AssetPostprocessor
{
    // Shared compiled regex: matches mixamorig followed by 1+ digits then ':'
    // e.g. mixamorig4:  mixamorig12:  mixamorig100:
    // Does NOT match the already-clean "mixamorig:" (no digits), leaving it untouched.
    private static readonly Regex BoneRegex =
        new Regex(@"mixamorig\d+:", RegexOptions.Compiled);

    /// <summary>Normalises a single bone name string.</summary>
    public static string NormaliseBoneName(string name) =>
        BoneRegex.Replace(name, "mixamorig:");

    // ── 1. Rename bone Transform names in the imported hierarchy ──────────────
    void OnPostprocessModel(GameObject go)
    {
        int renamed = FixBoneTransforms(go.transform);
        if (renamed > 0)
            Debug.Log($"[MixamoBonePurge] Renamed {renamed} bone(s) in {assetPath}");
    }

    private static int FixBoneTransforms(Transform t)
    {
        int count = 0;
        string fixedName = NormaliseBoneName(t.name);
        if (t.name != fixedName)
        {
            t.name = fixedName;
            count++;
        }
        foreach (Transform child in t)
            count += FixBoneTransforms(child);
        return count;
    }

    // ── 2. Rename bone path strings inside animation curves ───────────────────
    void OnPostprocessAnimation(GameObject go, AnimationClip clip)
    {
        var bindings = AnimationUtility.GetCurveBindings(clip);

        for (int i = 0; i < bindings.Length; i++)
        {
            var    binding = bindings[i];
            string oldPath = binding.path;
            string newPath = NormaliseBoneName(oldPath);

            if (oldPath == newPath) continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            AnimationUtility.SetEditorCurve(clip, binding, null); // remove old
            binding.path = newPath;
            AnimationUtility.SetEditorCurve(clip, binding, curve); // add fixed
        }
    }
}