using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pastoral.UI{
[RequireComponent(typeof(Toggle))]
public class ToggleUIp : MonoBehaviour
{
    public RectTransform BtnCircle;
    public RectTransform FromPos;
    public RectTransform ToPos;
    public Image BGImage;
    public Image IconImage;
    public Sprite[] FromToSprite;
    private void Start() {
        Toggle toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener((isOn)=>
        {
            if(!toggle.isActiveAndEnabled) return;
            // if(isOn == toggle.isOn) return;
            // isOn = !isOn;
            if(BtnCircle){
                Vector2 circleScale = UIp.GetBoundsOfUI_Scalar(BtnCircle);
                Vector2 midPos;
                if(isOn){
                    midPos = ToPos.position;
                    midPos.x += Vector2.Distance(transform.position.normalized, ToPos.position.normalized)*circleScale.x*4;
                    StartCoroutine(UIp.UIColorTweening(BGImage, BGImage.color, Color.green, 4));
                    StartCoroutine(UIp.UITweening(this, BtnCircle, transform.position, midPos, ToPos.position, Muiltiplier: 4));
                    StartCoroutine(UIp.UIScaleTweening(this, BtnCircle, transform.localScale, new Vector2(0.7f, 1.1f), Vector2.one, Muiltiplier: 4));
                }
                else{
                    midPos = FromPos.position;
                    midPos.x += Vector2.Distance(transform.position.normalized, FromPos.position.normalized)*-circleScale.x*4;
                    StartCoroutine(UIp.UIColorTweening(BGImage, BGImage.color, Color.red, 4));
                    StartCoroutine(UIp.UITweening(this, BtnCircle, transform.position, midPos, FromPos.position, Muiltiplier: 4));
                    StartCoroutine(UIp.UIScaleTweening(this, BtnCircle, transform.localScale, new Vector2(0.7f, 0.5f), new Vector2(0.5f, 0.5f), Muiltiplier: 4));
                }
                if(FromToSprite != null)
                    IconImage.sprite = FromToSprite[Convert.ToByte(isOn)];
            }
        });
    }
    public void OnEnable()
    {
        Toggle toggle = GetComponent<Toggle>();
        if(toggle.isOn){
            BGImage.color = Color.green;
            // BtnCircle.position = ToPos.position;
            BtnCircle.localPosition = ToPos.localPosition;
            BtnCircle.localScale = Vector2.one;
        }
        else{
            BGImage.color = Color.red;
            BtnCircle.localPosition = FromPos.localPosition;
            BtnCircle.localScale = new Vector2(0.5f, 0.5f);
        }
        if(FromToSprite != null)
            IconImage.sprite = FromToSprite[Convert.ToByte(toggle.isOn)];
    }
}
}