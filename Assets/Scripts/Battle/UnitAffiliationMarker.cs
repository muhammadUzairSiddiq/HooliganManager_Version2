using UnityEngine;

/// <summary>High-contrast ground rings that keep squads readable against the city.</summary>
public sealed class UnitAffiliationMarker : MonoBehaviour
{
    LineRenderer ring;
    readonly LineRenderer[] arrows = new LineRenderer[4];
    Color baseColor;
    float radius;
    bool showArrows;
    public bool KeepVisibleWhenIdle;

    public Color AffiliationColor => baseColor;
    public bool HasDirectionalArrows => showArrows && arrows[0];
    public bool IsHollow => ring && ring.loop;

    public static UnitAffiliationMarker Attach(Transform owner, Color color, float radius = .85f, bool directionalArrows = false)
    {
        if (!owner) return null;
        var marker = owner.GetComponent<UnitAffiliationMarker>() ?? owner.gameObject.AddComponent<UnitAffiliationMarker>();
        marker.Configure(color, radius, directionalArrows);
        return marker;
    }

    public void Configure(Color color, float ringRadius, bool directionalArrows = false)
    {
        baseColor = color;
        radius = ringRadius;
        showArrows = directionalArrows;
        if (!ring)
        {
            var go = new GameObject("HollowAffiliationRing");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * .08f;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 48;
            ring.numCornerVertices = 2;
        }
        ZoneVolumeFactory.ApplyDoubleSidedMaterial(ring, baseColor);
        ring.startColor = ring.endColor = baseColor;
        ring.startWidth = ring.endWidth = .18f;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
        BuildArrows();
        // Player rings stay hidden until selected.
        if (directionalArrows)
            SetVisible(false);
    }

    void BuildArrows()
    {
        for (int i = 0; i < arrows.Length; i++)
        {
            if (!showArrows)
            {
                if (arrows[i]) arrows[i].gameObject.SetActive(false);
                continue;
            }

            if (!arrows[i])
            {
                var go = new GameObject("GreenDirectionArrow" + (i + 1));
                go.transform.SetParent(ring.transform, false);
                arrows[i] = go.AddComponent<LineRenderer>();
                arrows[i].useWorldSpace = false;
                arrows[i].positionCount = 3;
                arrows[i].textureMode = LineTextureMode.Stretch;
                arrows[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arrows[i].receiveShadows = false;
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader) arrows[i].material = new Material(shader) { color = Color.white };
                ZoneVolumeFactory.ApplyDoubleSidedMaterial(arrows[i], baseColor);
            }
            arrows[i].gameObject.SetActive(true);
            float angle = i * Mathf.PI * .5f;
            Vector3 outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 side = new Vector3(-outward.y, outward.x, 0f);
            Vector3 tip = outward * (radius * .70f);
            Vector3 basePoint = outward * (radius * 1.30f);
            arrows[i].SetPosition(0, basePoint + side * radius * .27f);
            arrows[i].SetPosition(1, tip);
            arrows[i].SetPosition(2, basePoint - side * radius * .27f);
            arrows[i].startColor = arrows[i].endColor = baseColor;
            arrows[i].startWidth = arrows[i].endWidth = .16f;
        }
    }

    public void ApplyColor(Color color)
    {
        baseColor = color;
        if (!ring) return;
        ring.startColor = ring.endColor = color;
        for (int i = 0; i < arrows.Length; i++)
            if (arrows[i]) arrows[i].startColor = arrows[i].endColor = color;
    }

    public void SetSelected(bool selected)
    {
        if (!ring) return;
        // Green while chosen. Yellow stays visible for a crew already on an order.
        bool show = !showArrows || selected || KeepVisibleWhenIdle;
        ring.enabled = show;
        ring.gameObject.SetActive(show);
        if (!show)
        {
            for (int i = 0; i < arrows.Length; i++)
                if (arrows[i]) arrows[i].gameObject.SetActive(false);
            return;
        }

        ring.startWidth = ring.endWidth = selected ? .32f : .18f;
        Color color = baseColor;
        ring.startColor = ring.endColor = color;
        for (int i = 0; i < arrows.Length; i++)
        {
            if (!arrows[i]) continue;
            arrows[i].gameObject.SetActive(showArrows);
            arrows[i].startColor = arrows[i].endColor = color;
            arrows[i].startWidth = arrows[i].endWidth = selected ? .25f : .16f;
        }
        ring.transform.localScale = selected ? Vector3.one * 1.18f : Vector3.one;
    }

    /// <summary>Hide player affiliation ring until the unit is selected.</summary>
    public void SetVisible(bool visible)
    {
        if (!ring) return;
        ring.enabled = visible;
        ring.gameObject.SetActive(visible);
        for (int i = 0; i < arrows.Length; i++)
            if (arrows[i]) arrows[i].gameObject.SetActive(visible && showArrows);
    }
}
