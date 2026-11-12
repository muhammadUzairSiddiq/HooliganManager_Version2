using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Rim, sheen and label response. The selectable RectTransform never scales or moves.</summary>
public sealed class LandscapeButtonPolish : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
{
    public RectTransform shine;
    public TextMeshProUGUI label;
    public Graphic selectedGlow;
    public Graphic responseRim;
    public bool selected;
    public bool primaryShine;
    Selectable selectable;
    Image image;
    bool held, hovered;
    float response, releaseAge = 1, sheenAge = 1;
    Vector3 labelScale = Vector3.one;
    void Awake()
    {
        selectable = GetComponent<Selectable>(); image = GetComponent<Image>();
    }
    void Start()
    {
        // Existing authored scenes already contain this component. Upgrade their
        // decoration at runtime without requiring the scene generator to run.
        if (!responseRim)
        {
            var r = (RectTransform)transform;
            responseRim = LandscapeUI.Image("ResponseRim", transform, 2, 2, Mathf.Max(1, r.rect.width - 4), 2, null, new Color(.4f, .89f, .83f, 0));
        }
        Configure();
    }
    public void Configure()
    {
        if (label)
        {
            labelScale = label.transform.localScale;
            var rt = label.rectTransform;
            var delta = new Vector2(.5f, .5f) - rt.pivot;
            rt.anchoredPosition += Vector2.Scale(delta, rt.rect.size);
            rt.pivot = new Vector2(.5f, .5f);
        }
        if (shine) { var g = shine.GetComponent<Graphic>(); if (g) g.raycastTarget = false; shine.gameObject.SetActive(false); }
        if (selectedGlow) selectedGlow.raycastTarget = false;
        if (responseRim) responseRim.raycastTarget = false;
        RefreshDecorations(0);
    }
    void OnEnable() { held = hovered = false; response = 0; releaseAge = sheenAge = 1; }
    void OnDisable() { if (label) label.transform.localScale = labelScale; }
    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool active = !selectable || selectable.IsInteractable();
        if (!active) { held = hovered = false; }
        response = Mathf.MoveTowards(response, active && (held || hovered || selected) ? 1 : 0, dt * 9);
        releaseAge += dt; sheenAge += dt;
        float scale = 1;
        if (active && !GameFeedback.ReducedMotion)
        {
            if (held) scale = .975f;
            else if (releaseAge < .2f) scale = 1 + Mathf.Sin(releaseAge / .2f * Mathf.PI) * .022f;
        }
        if (label) { label.transform.localScale = labelScale * scale; label.alpha = active ? 1 : .58f; }
        RefreshDecorations(active ? response : 0);
    }
    void RefreshDecorations(float strength)
    {
        if (responseRim) responseRim.color = new Color(.4f, .89f, .83f, strength * .35f);
        if (selectedGlow)
        {
            selectedGlow.gameObject.SetActive(selected);
            selectedGlow.color = new Color(.37f, .91f, .73f, selected ? .9f : 0);
        }
        if (!shine) return;
        bool show = !GameFeedback.ReducedMotion && sheenAge < .28f;
        shine.gameObject.SetActive(show);
        if (show)
        {
            float width = ((RectTransform)transform).rect.width, t = sheenAge / .28f;
            shine.anchoredPosition = new Vector2(Mathf.Lerp(-width * .3f, width * 1.2f, t), shine.anchoredPosition.y);
            var g = shine.GetComponent<Graphic>(); if (g) g.color = new Color(1, 1, 1, Mathf.Sin(t * Mathf.PI) * .1f);
        }
    }
    public void SetBaseColor(Color color) { if (image) image.color = color; }
    public void SetSelected(bool value) { selected = value; }
    public void OnPointerEnter(PointerEventData eventData) { if (!selectable || selectable.IsInteractable()) { hovered = true; sheenAge = 0; } }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!selectable || selectable.IsInteractable()) { held = true; sheenAge = 0; GameFeedback.Emit(GameFeedbackEvent.UiPress); }
        AgentSelectionManager.ConsumeUiPointer();
    }
    public void OnPointerUp(PointerEventData eventData) { if (held) releaseAge = 0; held = false; }
    public void OnSubmit(BaseEventData eventData) { if (!selectable || selectable.IsInteractable()) { releaseAge = 0; sheenAge = 0; GameFeedback.Emit(GameFeedbackEvent.UiPress); } }
}
