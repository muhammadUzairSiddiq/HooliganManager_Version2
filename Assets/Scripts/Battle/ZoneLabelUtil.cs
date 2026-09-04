using UnityEngine;
using TMPro;

/// <summary>
/// Shared bold white floating labels for turf / recruit zones.
/// </summary>
public static class ZoneLabelUtil
{
    public static TextMeshPro Create(Transform parent, string text, float height, float fontSize)
    {
        var labelGo = new GameObject("ZoneLabel");
        labelGo.transform.SetParent(parent, false);
        labelGo.transform.localPosition = new Vector3(0f, height, 0f);
        var label = labelGo.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.outlineWidth = 0.28f;
        label.outlineColor = new Color(0f, 0f, 0f, 0.95f);
        label.enableWordWrapping = false;
        var rt = label.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(18f, 3f);
        return label;
    }
}
