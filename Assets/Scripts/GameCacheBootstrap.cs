using System.IO;
using UnityEngine;

/// <summary>
/// Clears stale PlayerPrefs / Unity cache once per fresh install of this package.
/// Android updates that keep the same package id can leave a doomed campaign save
/// (all 0 HP / empty away selection) which immediately shows WASTED on gameplay load.
/// A brand-new package id already starts clean; this still runs as a safety net.
/// </summary>
public static class GameCacheBootstrap
{
    /// <summary>Bump when shipping a build that must force a one-time prefs wipe.</summary>
    public const string InstallMarkerKey = "HM.InstallCleared.v3";

    /// <summary>Runs before any scene Awake so GameData never loads a doomed save first.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        ClearAllGameCacheForFirstInstall();
    }

    /// <summary>
    /// MainMenu safety net — same first-install wipe. Safe to call repeatedly;
    /// only runs once per InstallMarkerKey.
    /// </summary>
    public static void ClearAllGameCacheForFirstInstall()
    {
        if (PlayerPrefs.HasKey(InstallMarkerKey))
            return;

        Debug.Log("[GameCacheBootstrap] First install of this package — clearing campaign cache.");
        ClearAllGameCache();
        PlayerPrefs.SetInt(InstallMarkerKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Wipes campaign saves, settings prefs, and Unity download cache.</summary>
    public static void ClearAllGameCache()
    {
        try
        {
            if (Caching.compressionEnabled || Caching.cacheCount > 0)
                Caching.ClearCache();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[GameCacheBootstrap] Caching.ClearCache failed: " + e.Message);
        }

        try
        {
            string temp = Application.temporaryCachePath;
            if (!string.IsNullOrEmpty(temp) && Directory.Exists(temp))
            {
                foreach (var path in Directory.GetFiles(temp))
                {
                    try { File.Delete(path); } catch { /* ignore locked files */ }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[GameCacheBootstrap] Temp cache wipe failed: " + e.Message);
        }

        // Campaign authority + settings. DeleteAll is intentional for first install
        // of a new package build so no prior doomed save can revive.
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
