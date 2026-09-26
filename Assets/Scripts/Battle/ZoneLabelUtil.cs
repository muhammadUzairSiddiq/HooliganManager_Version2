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
        label.fontSize = Mathf.Clamp(fontSize * .85f, 3.4f, 6f);
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
        if (rt != null) rt.sizeDelta = new Vector2(24f, 4.5f);
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
    Renderer cachedRenderer;

    void Awake()
    {
        label = GetComponent<TextMeshPro>();
        cachedRenderer=GetComponent<Renderer>();
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam) transform.rotation = cam.transform.rotation;
        if(cachedRenderer&&cam)
            cachedRenderer.enabled=WorldAnnotationBudget.Reserve(cam,transform.position,170f,28f);
        if (!ForceWhite) return;
        if (!label) label = GetComponent<TextMeshPro>();
        if (label) label.color = Color.white;
    }
}

/// <summary>A small shared screen-space budget prevents world titles colliding.
/// Full descriptions remain accessible through the command desk and task board.</summary>
public static class WorldAnnotationBudget
{
    static readonly System.Collections.Generic.List<Rect> occupied=new System.Collections.Generic.List<Rect>(8);
    static int frame=-1;
    public static bool Reserve(Camera cam,Vector3 point,float width,float height)
    {
        if(frame!=Time.frameCount){occupied.Clear();frame=Time.frameCount;}
        if(!cam||occupied.Count>=6)return false;
        var p=cam.WorldToViewportPoint(point);
        if(p.z<=0||p.x<.21f||p.x>.79f||p.y<.12f||p.y>.82f)return false;
        float scale=Screen.height/900f;
        var rect=new Rect(p.x*Screen.width-width*scale*.5f,p.y*Screen.height-height*scale*.5f,width*scale,height*scale);
        foreach(var other in occupied)if(other.Overlaps(rect))return false;
        occupied.Add(rect);return true;
    }
}
