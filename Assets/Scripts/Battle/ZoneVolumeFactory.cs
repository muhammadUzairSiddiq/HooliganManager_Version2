using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Soft transparent 3D zone cubes for gang turf / recruit areas.
/// Each gang gets its own tint. Culls back faces so the camera never fills
/// the screen when panning past a volume. Alpha is kept low so gameplay stays visible.
/// </summary>
public static class ZoneVolumeFactory
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static GameObject Create(Transform parent, Color tint, float footprint, float height = 2.8f)
    {
        DestroyExisting(parent);

        // Flat ground pad only — tall transparent cubes made buildings look hollow.
        float size = Mathf.Max(3f, footprint * 1.7f);
        float h = 0.12f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ZoneVolume";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, h * 0.5f + 0.02f, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(size, h, size);

        // No physics — walk/camera pass through freely.
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.material = BuildTransparentMaterial(tint);

        return go;
    }

    private static Material BuildTransparentMaterial(Color tint)
    {
        // Prefer URP Unlit; fall back carefully — never Sprites/Default on a 3D cube
        // (that combo caused the full-screen yellow wash).
        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");

        var mat = new Material(shader);

        Color c = tint;
        // Flat ground marker — readable, never a tall glass box over buildings.
        c.a = 0.35f;

        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, c);
        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, c);
        mat.color = c;

        // Force transparent pipeline when the shader supports it.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // Alpha
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Back);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_SrcBlend"))
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend"))
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;

        // Standard shader fallback path.
        if (shader != null && shader.name == "Standard")
        {
            mat.SetFloat("_Mode", 3f); // Transparent
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        return mat;
    }

    private static void DestroyExisting(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child != null && child.name == "ZoneVolume")
                Object.Destroy(child.gameObject);
        }
    }
}
