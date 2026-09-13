using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class LandscapeButtonPolish : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform shine;
    public TextMeshProUGUI label;
    public Graphic selectedGlow;
    public bool selected;

    RectTransform rect;
    Selectable selectable;
    Image image;
    Vector3 baseScale;
    float hover;
    float press;
    float width;
    Color baseColor = Color.white;
    public void SetBaseColor(Color color) {baseColor=color;if(image)image.color=color;}

    void Awake()
    {
        rect = (RectTransform)transform;
        selectable = GetComponent<Selectable>();
        image = GetComponent<Image>();
        baseScale = rect.localScale;
        if (image) baseColor = image.color;
        width = rect.rect.width;
    }

    void OnEnable()
    {
        hover = 0;
        press = 0;
        if (rect) rect.localScale = baseScale;
    }

    void Update()
    {
        bool active = selectable == null || selectable.IsInteractable();
        hover = Mathf.MoveTowards(hover, active && selected ? .55f : hover, Time.unscaledDeltaTime * .8f);
        press = Mathf.MoveTowards(press, 0, Time.unscaledDeltaTime * 8f);

        float glow = selected ? .22f + Mathf.Sin(Time.unscaledTime * 4.2f) * .05f : hover * .10f;
        if (image) image.color = active ? Color.Lerp(baseColor, Color.white, glow) : new Color(.58f, .62f, .65f, .65f);

        float scale = 1f + hover * .025f - press * .035f + (selected ? Mathf.Sin(Time.unscaledTime * 3.2f) * .004f : 0);
        if (rect) rect.localScale = baseScale * scale;

        if (shine)
        {
            float period = selected ? 1.45f : 3.6f;
            float sweep = Mathf.Repeat(Time.unscaledTime / period + transform.GetSiblingIndex() * .09f, 1.35f) - .25f;
            shine.anchoredPosition = new Vector2(Mathf.Lerp(-width * .45f, width * 1.15f, sweep), shine.anchoredPosition.y);
            var g = shine.GetComponent<Graphic>();
            if (g) g.color = new Color(1, 1, 1, active ? selected ? .055f : .025f : .01f);
        }

        if (selectedGlow)
        {
            selectedGlow.color = new Color(0.95f, 0.15f, 0.18f, selected ? .75f + Mathf.Sin(Time.unscaledTime * 5f) * .12f : 0);
        }

        if (label) label.alpha = active ? 1f : .58f;
    }

    public void SetSelected(bool value)
    {
        selected = value;
        hover = Mathf.Max(hover, value ? .55f : 0);
    }

    public void OnPointerEnter(PointerEventData eventData) { if (selectable == null || selectable.IsInteractable()) hover = 1f; }
    public void OnPointerExit(PointerEventData eventData) { hover = selected ? .55f : 0; }
    public void OnPointerDown(PointerEventData eventData) { if (selectable == null || selectable.IsInteractable()) press = 1f; }
    public void OnPointerUp(PointerEventData eventData) { press = 0; }
}
