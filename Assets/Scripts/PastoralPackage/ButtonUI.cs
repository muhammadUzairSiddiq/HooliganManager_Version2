using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using Pastoral;

public class ButtonUI : Selectable, IPointerClickHandler, IEventSystemHandler, ISubmitHandler
{
    public override void Select()
    {
        base.Select();
    }
    private bool isClicking;
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsActive() || !IsInteractable() || eventData.button != PointerEventData.InputButton.Left) return;
        if (!isClicking)
        {
            isClicking = true;
            OnClick?.Invoke();
            if (GetComponent<LandscapeButtonPolish>())
            {
                AfterPointerClick(eventData);
                return;
            }
            StartCoroutine(UIp.UIScaleTweening(GetComponent<RectTransform>(), Vector3.one, Vector3.one*1.05f, DisableOnScaleComplete:false, GameManager.BUTTON_ANIMATION_MULTIPLIER, ()=> {
                AfterPointerClick(eventData);
                transform.localScale = Vector3.one;
                }));
        }
    }
    public void AfterPointerClick(PointerEventData eventData)
    {
        AfterClickAnimation?.Invoke();
        isClicking=false;
    }
    public void OnSubmit(BaseEventData eventData)
    {
        if (!IsActive() || !IsInteractable() || isClicking) return;
        OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopAllCoroutines();
        isClicking = false;
        transform.localScale = Vector3.one;
    }

    [SerializeField]
    public Button.ButtonClickedEvent OnClick = new Button.ButtonClickedEvent();
    [SerializeField]
    public Button.ButtonClickedEvent AfterClickAnimation = new Button.ButtonClickedEvent();

    public class ButtonClickedEvent : UnityEvent
    {
        public ButtonClickedEvent(){
        }
    }
}
