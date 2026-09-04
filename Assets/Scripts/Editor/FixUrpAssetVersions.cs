#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Unity 6000.0 / URP 17.0.4 expects RP asset version 12 and GlobalSettings version 8.
/// Opening the project in a newer Unity stamps higher versions and then Android builds fail with
/// "is not at last version". This clamps versions back to what the installed package expects.
/// </summary>
[InitializeOnLoad]
public static class FixUrpAssetVersions
{
    private const int ExpectedRpAssetVersion = 12;
    private const int ExpectedGlobalSettingsVersion = 8;
    private const string GlobalSettingsPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";

    static FixUrpAssetVersions()
    {
        EditorApplication.delayCall += ClampAll;
    }

    [MenuItem("Tools/URP/Fix Asset Versions For Build")]
    private static void ClampAllMenu()
    {
        ClampAll();
        AssetDatabase.SaveAssets();
        Debug.Log("[FixUrpAssetVersions] Clamped URP assets to versions compatible with this Editor.");
    }

    private static void ClampAll()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null) continue;

            var so = new SerializedObject(asset);
            var ver = so.FindProperty("k_AssetVersion");
            var prev = so.FindProperty("k_AssetPreviousVersion");
            if (ver == null) continue;

            bool dirty = false;
            if (ver.intValue != ExpectedRpAssetVersion)
            {
                ver.intValue = ExpectedRpAssetVersion;
                dirty = true;
            }
            if (prev != null && prev.intValue != ExpectedRpAssetVersion)
            {
                prev.intValue = ExpectedRpAssetVersion;
                dirty = true;
            }
            if (dirty)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                Debug.Log($"[FixUrpAssetVersions] {path} → k_AssetVersion={ExpectedRpAssetVersion}");
            }
        }

        // GlobalSettings type is internal — load as Object and edit via SerializedObject.
        var global = AssetDatabase.LoadMainAssetAtPath(GlobalSettingsPath);
        if (global != null)
        {
            var so = new SerializedObject(global);
            var ver = so.FindProperty("m_AssetVersion");
            if (ver != null && ver.intValue != ExpectedGlobalSettingsVersion)
            {
                ver.intValue = ExpectedGlobalSettingsVersion;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(global);
                Debug.Log($"[FixUrpAssetVersions] {GlobalSettingsPath} → m_AssetVersion={ExpectedGlobalSettingsVersion}");
            }
        }
    }
}
#endif
