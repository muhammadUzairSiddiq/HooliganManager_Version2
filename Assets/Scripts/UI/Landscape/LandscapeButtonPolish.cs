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
    public bool primaryShine;

    RectTransform rect;
    Selectable selectable;
    Image image;
    Vector3 baseScale;
    float hover;
    float press;
    float width;
    Color baseColor = Color.white;
    public void SetBaseColor(Color color) {baseColor=color;if(image)image.color=color;}

    bool held;
    float punch;
    float shown = 1f;

    void Awake()
    {
        rect = (RectTransform)transform;
        selectable = GetComponent<Selectable>();
        image = GetComponent<Image>();
        baseScale = rect.localScale;
        if (image) baseColor = image.color;
        width = rect.rect.width;
        primaryShine = false;
        if (shine) shine.gameObject.SetActive(false);
        if (selectedGlow) selectedGlow.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        hover = 0;
        press = 0;
        held = false;
        punch = 0;
        shown = 1f;
        if (rect) rect.localScale = baseScale;
    }

    void Update()
    {
        bool active = selectable == null || selectable.IsInteractable();
        if (!held) punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 2.4f);
        float target = !active ? 1f : held ? 0.9f : punch > 0f ? Mathf.Lerp(1.08f, 1f, 1f - punch) : 1f;
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
        if (rect) rect.localScale = baseScale * shown;
        if (image)
        {
            Color tint = active ? Color.Lerp(baseColor, Color.white, held ? 0.18f : 0f) : new Color(.58f, .62f, .65f, .65f);
            image.color = tint;
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
    public void OnPointerDown(PointerEventData eventData)
    {
        if (selectable == null || selectable.IsInteractable())
        {
            held = true;
            press = 1f;
            GameAudio.Play("click");
        }
        AgentSelectionManager.ConsumeUiPointer();
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        if (held) punch = 1f;
        held = false;
        press = 0;
    }
}
