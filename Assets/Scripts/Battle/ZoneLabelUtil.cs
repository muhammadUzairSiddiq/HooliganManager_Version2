using UnityEngine;
using TMPro;

/// <summary>
/// Shared bold white floating labels for turf / recruit zones.
/// Uses TMP outline + underlay only — a 3D quad behind SDF text paints over the
/// glyphs in URP's transparent queue and turns every label into a black slab.
/// </summary>
public static class ZoneLabelUtil
{
    public static TextMeshPro Create(Transform parent, string text, float height, float fontSize)
    {
        var labelGo = new GameObject("ZoneLabel");
        // Kept on the main camera, excluded by the tactical minimap camera.
        labelGo.layer = 5;
        labelGo.transform.SetParent(parent, false);
        labelGo.transform.localPosition = new Vector3(0f, height, 0f);
        var label = labelGo.AddComponent<TextMeshPro>();
        label.text = text;
        label.richText = true;
        label.fontSize = Mathf.Clamp(fontSize * 1.2f, 6.4f, 12.5f);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        ApplyReadableMaterial(label);
        var mr = label.GetComponent<Renderer>();
        if (mr)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.allowOcclusionWhenDynamic = false;
            mr.sortingOrder = 40;
        }
        var rt = label.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(36f, 6.2f);
        var billboard = labelGo.GetComponent<ZoneLabelBillboard>() ?? labelGo.AddComponent<ZoneLabelBillboard>();
        billboard.ForceWhite = true;
        return label;
    }

    static void ApplyReadableMaterial(TextMeshPro label)
    {
        if (!label) return;
        ShaderUtilities.GetShaderPropertyIDs();
        var mat = label.fontMaterial;
        if (!mat) return;
        mat.EnableKeyword("OUTLINE_ON");
        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
        if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.95f));
        mat.EnableKeyword("UNDERLAY_ON");
        if (mat.HasProperty(ShaderUtilities.ID_UnderlayColor))
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.02f, 0.03f, 0.04f, 0.82f));
        if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.08f);
        if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.08f);
        if (mat.HasProperty(ShaderUtilities.ID_UnderlayDilate))
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.55f);
        if (mat.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.18f);
        mat.renderQueue = ZoneVolumeFactory.LabelQueue;
        int zTest = Shader.PropertyToID("_ZTestMode");
        if (mat.HasProperty(zTest)) mat.SetFloat(zTest, 8f);
        label.fontMaterial = mat;
    }
}

/// <summary>Keeps world labels facing the gameplay camera and locked to white.</summary>
public sealed class ZoneLabelBillboard : MonoBehaviour
{
    public bool ForceWhite = true;
    TextMeshPro label;

    void Awake()
    {
        label = GetComponent<TextMeshPro>();
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam) transform.rotation = cam.transform.rotation;
        if (!ForceWhite) return;
        if (!label) label = GetComponent<TextMeshPro>();
        if (label) label.color = Color.white;
    }
}
