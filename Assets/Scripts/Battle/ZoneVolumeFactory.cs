using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ground rings for interactive areas. Laid flat on the floor with a double-sided
/// material so the far edge never disappears, and drawn under world labels so
/// the ribbon cannot cut taxi / task text.
/// </summary>
public static class ZoneVolumeFactory
{
    static Material sharedRingMaterial;
    public const int RingQueue = 2450;
    public const int LabelQueue = 3200;

    public static GameObject Create(Transform parent, Color tint, float footprint, float height = 0.12f)
    {
        DestroyExisting(parent);
        var root = new GameObject("ZoneRing");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.up * 0.05f;
        float outer = Mathf.Max(2.2f, footprint);
        BuildFlatCircle(root.transform, "Outer", outer, 0.12f, new Color(tint.r, tint.g, tint.b, .72f),true,48);
        return root;
    }

    public static LineRenderer BuildFlatCircle(Transform parent, string name, float radius, float width, Color color, bool loop = true, int segments = 72)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = Mathf.Max(2, segments);
        line.startWidth = line.endWidth = width;
        line.numCornerVertices = 2;
        ApplyDoubleSidedMaterial(line, color);
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / Mathf.Max(1, segments);
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
        return line;
    }

    public static void ApplyDoubleSidedMaterial(LineRenderer line, Color color)
    {
        if (!line) return;
        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (!shader) return;
        var mat = sharedRingMaterial ? sharedRingMaterial : (sharedRingMaterial=new Material(shader));
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        mat.color = Color.white;
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_ZTest")) mat.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_DOUBLE_SIDED_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = RingQueue;
        mat.doubleSidedGI = true;
        line.sharedMaterial = mat;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.startColor = line.endColor = color;
        line.allowOcclusionWhenDynamic = false;
    }

    static void DestroyExisting(Transform parent)
    {
        if (!parent) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child && (child.name == "ZoneVolume" || child.name == "ZoneRing"))
                Object.Destroy(child.gameObject);
        }
    }
}
