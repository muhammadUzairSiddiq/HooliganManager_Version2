using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pastoral.UI
{
public static class TextColorAdjuster
{
    public static void AdjustTextColor(TMP_Text text, Image backgroundImage)
    {
        // Calculate average color of the image
        Color averageColor = GetAverageColor(backgroundImage);

        // Determine brightness of the color
        float brightness = GetBrightness(averageColor);

        // Set text color: use black for light backgrounds, white for dark backgrounds
        text.color = brightness > 0.5f ? Color.black : Color.white;
    }

    public static void AdjustTextColor(TMP_Text text, Color backgroundImageColor)
    {
        // Determine brightness of the color
        float brightness = GetBrightness(backgroundImageColor);

        // Set text color: use black for light backgrounds, white for dark backgrounds
        text.color = brightness > 0.5f ? Color.black : Color.white;
    }

    // Method to get the average color of an Image
    public static Color GetAverageColor(Image image)
    {
        Texture2D texture = image.sprite.texture;
        Color[] pixels = texture.GetPixels();
        
        float totalR = 0, totalG = 0, totalB = 0;
        foreach (Color pixel in pixels)
        {
            totalR += pixel.r;
            totalG += pixel.g;
            totalB += pixel.b;
        }
        
        int pixelCount = pixels.Length;
        return new Color(totalR / pixelCount, totalG / pixelCount, totalB / pixelCount);
    }

    // Method to get brightness from a color (returns a value between 0 and 1)
    public static float GetBrightness(Color color)
    {
        // Common formula for perceived brightness
        return (0.299f * color.r)+ (0.587f * color.g) + (0.114f * color.b);
    }
}
}
