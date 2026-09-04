using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pastoral{
    public static class UIEffect{
        public static IEnumerator AnimateButton(MonoBehaviour mono, RectTransform rectTransform, float Multiplier = 6, Action action = null)
        {
            Vector2 bounds = UIp.GetBoundsOfUI_Scalar(rectTransform); 
            Vector2 fromPos = rectTransform.position;
            Vector2 toPos = bounds;
            toPos.y = fromPos.y + (bounds.y/10);
            float distanceX = bounds.x/20;
            
            Quaternion fromRotation = rectTransform.rotation;
            Quaternion[] toRotation = new Quaternion[2];

            toPos.x = fromPos.x - distanceX;
            mono.StartCoroutine(UIp.UITweening(rectTransform, fromPos, toPos, Multiplier));
            toRotation[0] = new Quaternion(0.168403581f,0.10593643f,0.0917580649f,0.975703955f);
            yield return mono.StartCoroutine(UIRotation(rectTransform, toRotation[0], Multiplier));

            toPos.x = fromPos.x + distanceX;
            mono.StartCoroutine(UIp.UITweening(rectTransform, rectTransform.position, toPos, Multiplier));
            toRotation[1] = new Quaternion(-0.127741188f,-0.0760934502f,-0.140314862f,0.978878856f);
            yield return mono.StartCoroutine(UIRotation(rectTransform, toRotation[1], Multiplier));

            toPos.x = fromPos.x - distanceX;
            mono.StartCoroutine(UIp.UITweening(rectTransform, rectTransform.position, toPos, Multiplier));
            toRotation[0] = Quaternion.Lerp(rectTransform.rotation, toRotation[0], 0.8f);
            yield return mono.StartCoroutine(UIRotation(rectTransform, toRotation[0], Multiplier));

            toPos.x = fromPos.x + distanceX;
            mono.StartCoroutine(UIp.UITweening(rectTransform, rectTransform.position, toPos, Multiplier));
            toRotation[1] = Quaternion.Lerp(rectTransform.rotation, toRotation[1], 0.8f);
            yield return mono.StartCoroutine(UIRotation(rectTransform, toRotation[1], Multiplier));

            mono.StartCoroutine(UIp.UITweening(rectTransform, toPos, fromPos, Multiplier));
            yield return mono.StartCoroutine(UIRotation(rectTransform, fromRotation, Multiplier));

            action?.Invoke();
        }
        public static IEnumerator UIRotation(RectTransform rectTransform, Quaternion toRotation, float Muiltiplier = 1)
        {
            Quaternion fromRotation = rectTransform.rotation;
            float t = 0;
            while (t<1)
            {
                rectTransform.rotation = Quaternion.Lerp(fromRotation, toRotation, t);
                yield return new WaitForFixedUpdate();
                t += Time.deltaTime*Muiltiplier;
            }
            rectTransform.rotation = toRotation;
        }
    }
}