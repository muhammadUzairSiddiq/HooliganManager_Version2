using System;
using System.IO;
using UnityEngine;

public static class GameCacheBootstrap
{
    // Older editor fixtures still preserve this preference.
    public const string InstallMarkerKey = "HM.InstallCleared.v3";
    const string CacheVersionKey = "HM.CacheVersion";

    internal static string InstalledVersion
    {
        get
        {
#if UNITY_EDITOR
            return null;
#else
            return Application.version;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        string version = InstalledVersion;
        if (string.IsNullOrEmpty(version) || PlayerPrefs.GetString(CacheVersionKey, "") == version)
            return;

        bool temporaryCleared = ClearTemporaryCache(Application.temporaryCachePath);
        bool downloadsCleared = false;
        try
        {
            downloadsCleared = Caching.ClearCache();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameCacheBootstrap] Could not clear Unity's download cache: " + e.Message);
        }

        // A locked cache can be retried on the next launch. Tutorial refresh
        // has its own saved version and does not depend on the cache result.
        if (temporaryCleared && downloadsCleared)
        {
            PlayerPrefs.SetString(CacheVersionKey, version);
            PlayerPrefs.Save();
        }
    }

    public static bool ClearTemporaryCache(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return true;

        bool cleared = true;
        try
        {
            foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
            {
                try
                {
                    if (entry is DirectoryInfo directory)
                        directory.Delete((entry.Attributes & FileAttributes.ReparsePoint) == 0);
                    else
                        entry.Delete();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    cleared = false;
                    Debug.LogWarning("[GameCacheBootstrap] Temporary cache item could not be cleared: " + e.Message);
                }
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            cleared = false;
            Debug.LogWarning("[GameCacheBootstrap] Temporary cache could not be read: " + e.Message);
        }
        return cleared;
    }

    public static bool RefreshFirmTutorial(PlayerData data, string version)
    {
        if (!RefreshTutorialVersion(data, version)) return false;
        data.MobileTutorialVersion = 0;
        data.MobileTutorialStep = 0;
        data.MobileTutorialRequired = true;
        data.FirstSessionCoachHidden = false;
        data.MatchdayTutorialPending = false;
        return true;
    }

    public static bool RefreshPoliceTutorial(PoliceCampaignData career, string version)
    {
        if (!RefreshTutorialVersion(career?.officers, version)) return false;
        career.PoliceTutorialVersion = 0;
        career.PoliceTutorialStep = 0;
        career.PoliceTutorialRequired = true;
        career.PoliceTutorialRoadId = null;
        return true;
    }

    static bool RefreshTutorialVersion(PlayerData data, string version)
    {
        if (data == null || string.IsNullOrEmpty(version) || data.TutorialAppVersion == version)
            return false;
        data.TutorialAppVersion = version;
        return true;
    }
}
