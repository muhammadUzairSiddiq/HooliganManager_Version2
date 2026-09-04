using System;
using UnityEngine;
using UnityEngine.UI;

namespace Pastoral.UI{
public class CircularLoader : MonoBehaviour
{
    // Summary:
    //    Value can only range from 0 to 100.
    public float FillValue{
        get {return _fillValue;}
        set {
            _fillValue = value;
            OnFillValueChanged?.Invoke();
        }
    }
    public float _fillValue;
    public Action OnFillValueChanged;
    public Image circleFillimage;
    public RectTransform HandlerEdgeImage;
    public RectTransform FillHandler;
    void Awake()
    {
        OnFillValueChanged += FillCircleValue;
    }
    void FillCircleValue()
    {
        float fillAmount = FillValue / 100.0f;
        circleFillimage.fillAmount = fillAmount;
        float angle = fillAmount * 360;
        FillHandler.localEulerAngles = new Vector3(0, 0, -angle);
        HandlerEdgeImage.localEulerAngles = new Vector3(0, 0, angle);
    }

    private void Update() {
        FillCircleValue();
    }
}
}