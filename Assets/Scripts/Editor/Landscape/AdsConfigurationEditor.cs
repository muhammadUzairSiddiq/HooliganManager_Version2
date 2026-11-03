#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[CustomEditor(typeof(AdsConfiguration))]
public sealed class AdsConfigurationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var config=(AdsConfiguration)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Test requests use Google sample IDs. Editor simulation is labelled and does not contact an ad network. Live mode requires your own platform IDs and configured AdMob privacy messages. Core play remains ad-free.",MessageType.Info);
        foreach(bool ios in new[]{false,true})
            if(!config.ValidFor(ios,out var reason))EditorGUILayout.HelpBox((ios?"iOS: ":"Android: ")+reason,MessageType.Warning);
        if(GUILayout.Button("Apply app IDs to Google Mobile Ads settings"))AdsBuildConfiguration.Apply(config);
        if(GUILayout.Button("Open Google's test ad documentation"))Application.OpenURL("https://developers.google.com/admob/unity/test-ads");
    }
    [MenuItem("Hooligan/Ads/Select ads configuration")]
    public static void SelectConfiguration()
    {
        var config=Resources.Load<AdsConfiguration>(AdsConfiguration.ResourcePath);
        if(!config)
        {
            Directory.CreateDirectory("Assets/Resources/Ads");config=CreateInstance<AdsConfiguration>();
            AssetDatabase.CreateAsset(config,"Assets/Resources/Ads/AdsConfiguration.asset");AssetDatabase.SaveAssets();
        }
        Selection.activeObject=config;EditorGUIUtility.PingObject(config);
    }
}

/// <summary>The single SO is copied into Google's required platform build settings.</summary>
public sealed class AdsBuildConfiguration : IPreprocessBuildWithReport
{
    public int callbackOrder=>-1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        var config=Resources.Load<AdsConfiguration>(AdsConfiguration.ResourcePath);
        if(!config)throw new BuildFailedException("AdsConfiguration asset is missing. Use Hooligan > Ads > Select ads configuration.");
        bool ios=report.summary.platform==BuildTarget.iOS;
        if(report.summary.platform!=BuildTarget.Android&&!ios)return;
        if(!config.ValidFor(ios,out var reason))throw new BuildFailedException(reason);
        Apply(config);
    }
    public static void Apply(AdsConfiguration config)
    {
        Type settingsType=null;
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {settingsType=assembly.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings");if(settingsType!=null)break;}
        if(settingsType==null)throw new InvalidOperationException("Google Mobile Ads package has not finished importing.");
        var settings=settingsType.GetMethod("LoadInstance",BindingFlags.NonPublic|BindingFlags.Static)?.Invoke(null,null) as ScriptableObject;
        if(!settings)throw new InvalidOperationException("Google Mobile Ads settings could not be loaded.");
        var serialized=new SerializedObject(settings);
        // Native manifests need an App ID even in Disabled builds. Public sample
        // IDs are harmless; Disabled mode never initializes or requests an ad.
        serialized.FindProperty("adMobAndroidAppId").stringValue=config.mode==GameAdsMode.Live?config.android.liveAppId:config.android.testAppId;
        serialized.FindProperty("adMobIOSAppId").stringValue=config.mode==GameAdsMode.Live?config.ios.liveAppId:config.ios.testAppId;
        serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
    }
}
#endif
