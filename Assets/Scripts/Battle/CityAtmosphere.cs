using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Battle visual cleanup for solid city rendering + clean lighting.
/// Removes post-processing / PSX / motion-blur look, disables bad occlusion
/// culling holes, and applies opaque building / road / sidewalk textures.
/// </summary>
public static class CityAtmosphere
{
    private static Material _roadMat;
    private static Material _buildingMat;
    private static Material _sidewalkMat;
    private static Material _roofMat;

    public static IEnumerator ApplyAfterMapReady()
    {
        Apply();
        // Additive map objects / lightmaps finish waking over a couple frames.
        yield return null;
        yield return null;
        StripPostProcessing();
        ConfigureCameras();
        ReapplyEnvironmentTextures();
    }

    public static void Apply()
    {
        StripPostProcessing();
        StripPsxEffects();
        SetupCleanLighting();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gameplay")
        {
            BuildMaterialsIfNeeded();
            SolidifyEnvironment();
        }
        ConfigureCameras();
    }

    public static void ReapplyEnvironmentTextures()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Gameplay") return;
        BuildMaterialsIfNeeded();
        SolidifyEnvironment();
    }

    private static void StripPostProcessing()
    {
        // Kill every Volume in loaded scenes (Global Volume + URP default leftovers).
        foreach (var vol in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (vol == null || vol.GetComponent<ModalPresentation>()) continue;
            vol.weight = 0f;
            vol.enabled = false;
            vol.gameObject.SetActive(false);
        }

        RenderSettings.fog = false;
    }

    private static void StripPsxEffects()
    {
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n.IndexOf("PSX", System.StringComparison.OrdinalIgnoreCase) >= 0)
                Object.Destroy(mb);
        }
    }

    private static void SetupCleanLighting()
    {
        // Keep baked lightmaps from the map scene; only normalize ambient so
        // missing-texture buildings don't blow out to white / yellow wash.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.26f, 0.22f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 0.35f;
        RenderSettings.fog = false;

        // Ensure a readable main directional light exists / is sane.
        var sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l != null && l.type == LightType.Directional)
                {
                    sun = l;
                    break;
                }
            }
        }

        if (sun != null)
        {
            sun.shadows = LightShadows.Soft;
            sun.intensity = Mathf.Clamp(sun.intensity, 0.85f, 1.35f);
            sun.color = Color.Lerp(sun.color, new Color(1f, 0.96f, 0.88f), 0.35f);
            RenderSettings.sun = sun;
        }

        if (QualitySettings.shadowDistance > 45f)
            QualitySettings.shadowDistance = 45f;
        QualitySettings.shadowCascades = Mathf.Min(QualitySettings.shadowCascades, 2);
    }

    private static void ConfigureCameras()
    {
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam == null) continue;
            // Bad occlusion data makes buildings vanish / look hollow.
            cam.useOcclusionCulling = false;

            if (cam.CompareTag("MainCamera") || cam == Camera.main)
            {
                cam.nearClipPlane = Mathf.Max(cam.nearClipPlane, 1.5f);
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 400f);
                cam.allowHDR = false;
                cam.allowMSAA = true;
            }

            if (cam.TryGetComponent(out UniversalAdditionalCameraData urp))
            {
                urp.renderPostProcessing = false;
                urp.renderShadows = true;
                urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
        }

        // Keep RTS camera above hollow rooftops so interiors never show through.
        var pan = CameraPanTouchOnly.Instance;
        if (pan != null)
            pan.ForceHeight(Mathf.Max(18f, Camera.main != null ? Camera.main.transform.position.y : 18f));
    }

    private static void BuildMaterialsIfNeeded()
    {
        if (_roadMat != null && _buildingMat != null && _sidewalkMat != null && _roofMat != null)
            return;

        Texture2D road = LoadOrMake("CityTextures/road_asphalt", MakeAsphalt);
        Texture2D facade = LoadOrMake("CityTextures/building_facade", MakeFacade);
        Texture2D walk = LoadOrMake("CityTextures/sidewalk_concrete", MakeSidewalk);
        Texture2D roof = MakeRoof();

        _roadMat = MakeOpaqueLit(road, new Color(0.35f, 0.35f, 0.36f), tiling: 8f, smooth: 0.25f, doubleSided: false);
        // Double-sided walls hide hollow-mesh "see through corner" clipping.
        _buildingMat = MakeOpaqueLit(facade, new Color(0.92f, 0.90f, 0.84f), tiling: 2.5f, smooth: 0.35f, doubleSided: true);
        _sidewalkMat = MakeOpaqueLit(walk, new Color(0.72f, 0.72f, 0.70f), tiling: 6f, smooth: 0.3f, doubleSided: false);
        _roofMat = MakeOpaqueLit(roof, new Color(0.22f, 0.22f, 0.24f), tiling: 4f, smooth: 0.2f, doubleSided: true);
    }

    private static Material MakeOpaqueLit(Texture2D albedo, Color tint, float tiling, float smooth, bool doubleSided)
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Standard");

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);

        // Force opaque — never transparent / cutout.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.Zero);
        // 0 = Off (both sides), 2 = Back. Hollow ColorfulCity shells need Off.
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", doubleSided ? 0f : 2f);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = (int)RenderQueue.Geometry;

        if (mat.HasProperty("_BaseMap"))
            mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
        if (mat.HasProperty("_MainTex"))
            mat.SetTextureScale("_MainTex", new Vector2(tiling, tiling));

        mat.enableInstancing = true;
        return mat;
    }

    private static void SolidifyEnvironment()
    {
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var mr in renderers)
        {
            if (mr == null) continue;
            if (ShouldSkip(mr)) continue;

            string path = BuildPath(mr.transform).ToLowerInvariant();
            string name = mr.gameObject.name.ToLowerInvariant();

            Material chosen = null;
            if (IsRoad(path, name)) chosen = _roadMat;
            else if (IsSidewalk(path, name) || IsGround(path, name)) chosen = _sidewalkMat;
            else if (IsRoof(path, name)) chosen = _roofMat;
            else if (IsBuilding(path, name) || IsTallCityProp(mr, path, name)) chosen = _buildingMat;
            else if (LooksLikeBrokenEnv(mr))
            {
                chosen = IsLikelyHorizontal(mr) ? _sidewalkMat : _buildingMat;
            }

            if (chosen == null)
            {
                // Still force existing building mats opaque + double-sided.
                if (IsBuilding(path, name) || IsTallCityProp(mr, path, name))
                    ForceSolidDoubleSided(mr);
                continue;
            }

            mr.sharedMaterial = chosen;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
    }

    private static void ForceSolidDoubleSided(MeshRenderer mr)
    {
        var mats = mr.materials; // instance copies — OK for one-time battle fix
        for (int i = 0; i < mats.Length; i++)
        {
            var mat = mats[i];
            if (mat == null) continue;
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.One);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = (int)RenderQueue.Geometry;
            Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
            c.a = 1f;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }
        mr.materials = mats;
    }

    private static bool IsTallCityProp(MeshRenderer mr, string path, string name)
    {
        if (name.Contains("tree") || name.Contains("car") || name.Contains("bench") ||
            name.Contains("lamp") || name.Contains("hedge") || name.Contains("fence"))
            return false;
        var b = mr.bounds;
        // Tall solid-looking meshes near streets are usually building shells.
        return b.size.y > 3.5f && b.size.y > Mathf.Max(b.size.x, b.size.z) * 0.45f;
    }

    private static bool ShouldSkip(MeshRenderer mr)
    {
        if (mr.GetComponentInParent<AgentController>() != null) return true;
        if (mr.GetComponentInParent<EnemyController>() != null) return true;
        if (mr.GetComponentInParent<Canvas>() != null) return true;
        string n = mr.gameObject.name;
        if (n == "ZoneVolume" || n.StartsWith("MiniMap") || n.Contains("Vignette")) return true;
        if (n.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (n.IndexOf("indicator", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        // Keep character / prop materials that already look fine.
        if (mr.GetComponentInParent<Animator>() != null && mr.GetComponentInParent<AgentController>() == null
            && mr.GetComponentInParent<EnemyController>() == null)
        {
            // Pedestrians / props under animators — skip.
            var t = mr.transform;
            while (t != null)
            {
                string tn = t.name.ToLowerInvariant();
                if (tn.Contains("character") || tn.Contains("pedestrian") || tn.Contains("human") || tn.Contains("agent"))
                    return true;
                t = t.parent;
            }
        }
        return false;
    }

    private static bool LooksLikeBrokenEnv(MeshRenderer mr)
    {
        var mat = mr.sharedMaterial;
        if (mat == null) return true;
        if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f) return true;
        if (mat.renderQueue >= (int)RenderQueue.Transparent) return true;
        if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") == null &&
            mat.HasProperty("_BaseColor"))
        {
            Color c = mat.GetColor("_BaseColor");
            // Pure white / neon yellow missing-texture look.
            if (c.r > 0.85f && c.g > 0.85f && c.b > 0.4f) return true;
        }
        return false;
    }

    private static bool IsRoad(string path, string name) =>
        path.Contains("/roads") || name.Contains("road") || name.Contains("asphalt") || name.Contains("street");

    private static bool IsSidewalk(string path, string name) =>
        name.Contains("sidewalk") || name.Contains("pavement") || name.Contains("curb") || name.Contains("walkway");

    private static bool IsGround(string path, string name) =>
        path.Contains("/ground") || name == "ground" || name.Contains("terrain") || name.Contains("floor");

    private static bool IsRoof(string path, string name) =>
        name.Contains("roof") || name.Contains("rooftop");

    private static bool IsBuilding(string path, string name) =>
        path.Contains("/buildings") || name.Contains("building") || name.Contains("house") ||
        name.Contains("shop") || name.Contains("wall") || name.Contains("facade") ||
        name.Contains("apartment") || name.Contains("structure");

    private static bool IsLikelyHorizontal(MeshRenderer mr)
    {
        var b = mr.bounds;
        return b.size.y < Mathf.Max(b.size.x, b.size.z) * 0.35f;
    }

    private static string BuildPath(Transform t)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
        while (t != null)
        {
            sb.Insert(0, "/" + t.name);
            t = t.parent;
        }
        return sb.ToString();
    }

    // ── Textures ─────────────────────────────────────────────────────────
    private static Texture2D LoadOrMake(string resourcesPath, System.Func<Texture2D> fallback)
    {
        var tex = Resources.Load<Texture2D>(resourcesPath);
        if (tex != null)
        {
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
        return fallback();
    }

    private static Texture2D MakeAsphalt()
    {
        const int s = 256;
        var tex = new Texture2D(s, s, TextureFormat.RGB24, true);
        var rng = new System.Random(11);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float n = (float)rng.NextDouble();
            float v = 0.22f + n * 0.08f;
            // Subtle lane grit bands
            if ((y % 64) < 2) v += 0.04f;
            tex.SetPixel(x, y, new Color(v, v, v * 1.02f));
        }
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply(true);
        return tex;
    }

    private static Texture2D MakeFacade()
    {
        const int s = 256;
        var tex = new Texture2D(s, s, TextureFormat.RGB24, true);
        Color plaster = new Color(0.86f, 0.83f, 0.76f);
        Color window = new Color(0.35f, 0.42f, 0.50f);
        Color frame = new Color(0.92f, 0.92f, 0.90f);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            int cx = x % 64;
            int cy = y % 64;
            Color c = plaster;
            if (cx > 10 && cx < 54 && cy > 14 && cy < 50)
            {
                c = (cx < 13 || cx > 51 || cy < 17 || cy > 47) ? frame : window;
            }
            tex.SetPixel(x, y, c);
        }
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply(true);
        return tex;
    }

    private static Texture2D MakeSidewalk()
    {
        const int s = 256;
        var tex = new Texture2D(s, s, TextureFormat.RGB24, true);
        var rng = new System.Random(22);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float v = 0.62f + (float)rng.NextDouble() * 0.06f;
            if (x % 64 < 2 || y % 64 < 2) v *= 0.78f; // grout
            tex.SetPixel(x, y, new Color(v, v * 0.99f, v * 0.96f));
        }
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply(true);
        return tex;
    }

    private static Texture2D MakeRoof()
    {
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGB24, true);
        var rng = new System.Random(33);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float v = 0.18f + (float)rng.NextDouble() * 0.05f;
            tex.SetPixel(x, y, new Color(v, v, v * 1.05f));
        }
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply(true);
        return tex;
    }
}
