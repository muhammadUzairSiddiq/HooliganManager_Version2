using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime-generated HUD glyphs (pause / play). Fonts in the project do not carry
/// ⏸ / ▶, so the icons are drawn into small textures instead of relying on text.
/// </summary>
public static class HudIconFactory
{
    static Sprite pause, play;
    const int Size = 64;

    public static Sprite Pause()
    {
        if (pause) return pause;
        var tex = NewTexture();
        // Two rounded bars.
        FillRect(tex, 14, 12, 14, 40);
        FillRect(tex, 36, 12, 14, 40);
        tex.Apply();
        pause = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f);
        return pause;
    }

    public static Sprite Play()
    {
        if (play) return play;
        var tex = NewTexture();
        // Right-pointing triangle.
        for (int y = 0; y < Size; y++)
        {
            float t = 1f - Mathf.Abs((y - Size * .5f) / (Size * .5f - 10f));
            if (t <= 0f) continue;
            int width = Mathf.RoundToInt(Mathf.Clamp01(t) * 40f);
            for (int x = 16; x < 16 + width && x < Size; x++) tex.SetPixel(x, y, Color.white);
        }
        tex.Apply();
        play = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f);
        return play;
    }

    /// <summary>
    /// Give a landscape button an icon on the left and push its label right.
    /// Returns the icon image (created or reused).
    /// </summary>
    public static Image EnsureButtonIcon(Button button, Sprite sprite, float iconSize = 22f)
    {
        if (!button) return null;
        var existing = button.transform.Find("Icon");
        Image icon = existing ? existing.GetComponent<Image>() : null;
        var rect = button.transform as RectTransform;
        float width = rect && rect.sizeDelta.x > 1f ? rect.sizeDelta.x : (rect && rect.rect.width > 1f ? rect.rect.width : 154f);
        float height = rect && rect.sizeDelta.y > 1f ? rect.sizeDelta.y : (rect && rect.rect.height > 1f ? rect.rect.height : 56f);
        var mask = button.GetComponent<RectMask2D>();
        if (mask) mask.enabled = false;
        if (!icon)
        {
            icon = LandscapeUI.Image("Icon", button.transform, 12, (height - iconSize) * .5f, iconSize, iconSize, sprite, Color.white, true);
            icon.raycastTarget = false;
        }
        icon.sprite = sprite;
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.gameObject.SetActive(true);
        LandscapeUI.Place(icon.rectTransform, 12, (height - iconSize) * .5f, iconSize, iconSize);
        var label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label)
        {
            LandscapeUI.Place(label.rectTransform, 12 + iconSize + 6, 4, Mathf.Max(50, width - iconSize - 24), height - 8);
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = 20;
        }
        return icon;
    }

    static Texture2D NewTexture()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var clear = new Color(1, 1, 1, 0);
        var pixels = new Color[Size * Size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);
        return tex;
    }

    static void FillRect(Texture2D tex, int x, int y, int w, int h)
    {
        for (int i = x; i < x + w; i++)
            for (int j = y; j < y + h; j++)
                if (i >= 0 && j >= 0 && i < tex.width && j < tex.height) tex.SetPixel(i, j, Color.white);
    }
}
