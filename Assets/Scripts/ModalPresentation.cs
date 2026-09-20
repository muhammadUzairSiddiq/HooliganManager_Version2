using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Desaturates the rendered world; overlay UI retains its own colors.</summary>
public sealed class ModalPresentation : MonoBehaviour
{
    Volume volume;
    Camera worldCamera;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("Modal silver post processing");
        DontDestroyOnLoad(go); go.AddComponent<ModalPresentation>();
    }
    void Awake()
    {
        volume = gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10000;
        volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var color = volume.profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-100); color.contrast.Override(-12); color.postExposure.Override(.15f);
        color.colorFilter.Override(new Color(.82f, .84f, .86f));
        volume.weight = 0;
    }
    void LateUpdate()
    {
        bool open = Time.timeScale == 0 || GamePopup.AnyOpen || RecruitDialogBox.AnyOpen;
        volume.weight = open ? 1 : 0;
        if (!worldCamera) worldCamera = Camera.main;
        if (worldCamera && worldCamera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
        { data.renderPostProcessing = open; data.volumeLayerMask |= 1 << gameObject.layer; }
    }
    void OnDestroy() { if (volume && volume.profile) Destroy(volume.profile); }
}
