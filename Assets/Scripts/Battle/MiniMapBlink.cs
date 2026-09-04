using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pulses a minimap dot's alpha so it reads as a flashing "police light" blip.
/// Attach to the Image returned by MiniMapView.Follow().
/// </summary>
public class MiniMapBlink : MonoBehaviour
{
    public float speed = 6f;
    public float minAlpha = 0.25f;
    public float maxAlpha = 1f;

    private Image _img;
    private Color _base;

    void Awake()
    {
        _img = GetComponent<Image>();
        if (_img != null) _base = _img.color;
    }

    void Update()
    {
        if (_img == null) return;
        float a = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f);
        _img.color = new Color(_base.r, _base.g, _base.b, a);
    }
}
