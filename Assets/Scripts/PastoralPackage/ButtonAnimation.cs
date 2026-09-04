using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pastoral;

public static class ButtonAnimation
{
    public static void BtnCartoon(this MonoBehaviour mono, System.Action<object> action, object obj)
    {
        // SoundManager.instance.PlaySound(3);
        // RectTransform rectTransform1 = GeneralObjectManager.instance.InstantiateClickEffect();
        // Vector2 bounds = UIp.GetBoundsOfUI_Scalar(rectTransform1);
        // GeneralObjectManager.instance.StartCoroutine(UIp.UIScaleTweening(rectTransform1, true, 3, ()=>Object.Destroy(rectTransform1.gameObject)));

        // RectTransform rectTransform2 = GeneralObjectManager.instance.InstantiateClickEffect(Random.Range(-bounds.x, bounds.x),Random.Range(-bounds.y, bounds.y));
        // GeneralObjectManager.instance.StartCoroutine(UIp.UIScaleTweening(rectTransform2, true, 3, ()=>Object.Destroy(rectTransform2.gameObject)));
        mono.StartCoroutine(UIEffect.AnimateButton(mono, mono.GetComponent<RectTransform>(), action: ()=> action?.Invoke(obj)));
    }
}
