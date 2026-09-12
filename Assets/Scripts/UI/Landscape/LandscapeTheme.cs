using UnityEngine;
using TMPro;

/// <summary>Shared, explicitly referenced artwork sliced from the supplied Updated UI sheets.</summary>
[CreateAssetMenu(menuName = "Hooligan/Landscape Theme")]
public sealed class LandscapeTheme : ScriptableObject
{
    public TMP_FontAsset font;
    public Sprite mainBackground, townBackground, tacticalBackground;
    public Sprite logo, cash, crew, star, shield, settings, emblem, celebration;
    public Sprite panel, card, redButton, greenButton, darkButton, outlineButton;
    public Sprite[] portraits;
    private static LandscapeTheme cached;
    public static LandscapeTheme Current => cached != null ? cached : (cached = Resources.Load<LandscapeTheme>("LandscapeTheme"));
}
