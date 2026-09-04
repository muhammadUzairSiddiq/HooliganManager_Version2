using System;
using System.Collections;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Pastoral.UI{
public class GearRotationTextmeshProGUI : MonoBehaviour
{  
    public Transform ParentTrans;
    private bool isCountingDown = true;

    // private MonoBehaviour this;

    // private void Start() {
    //     SetupCounter(new TimeSpan(100000));
    // }
    private void OnEnable() {
        if (timeSpan != null)
            SetupCounter();
    }
    private IEnumerator SetInitialFontSize(TMP_Text textComponent)
    {
        yield return new WaitForFixedUpdate();
        float fontSize = textComponent.fontSize;
        textComponent.enableAutoSizing = false;
        textComponent.fontSize = fontSize;
    }
    private TimeSpan timeSpan;
    public void SetupCounter(TimeSpan timeSpan)
    {
        this.timeSpan = timeSpan;
        SetupCounter();
    }
    private void SetupCounter()
    {
        StopAllCoroutines();
        ResetClock();
        for (int i = 0; i < ParentTrans.childCount; i++)
            StartCoroutine(SetInitialFontSize(ParentTrans.GetChild(i).GetComponent<TextMeshProUGUI>()));
        StartCoroutine(Countdown());
    }
    private void ResetClock(){
        float y = transform.position.y;
        Transform letterTrans;
        for (int i = 0; i < ParentTrans.childCount; i++){
            letterTrans = ParentTrans.GetChild(i);
            letterTrans.position = new Vector3(letterTrans.position.x, y);
        }
    }

    private string OriginalFormattedTime;
    private IEnumerator Countdown()
    {
        yield return new WaitForFixedUpdate();
        isCountingDown = true;
        OriginalFormattedTime = string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}",
                                            0,
                                            0,
                                            0,
                                            0);
        while (timeSpan.TotalSeconds > 0)
        {
            yield return new WaitForSeconds(1);
            timeSpan = timeSpan.Subtract(TimeSpan.FromSeconds(1));

            string formattedTime = string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}",
                                            timeSpan.Days,
                                            timeSpan.Hours,
                                            timeSpan.Minutes,
                                            timeSpan.Seconds);

            int[] index = Stringp.GetChangedIndices(OriginalFormattedTime, formattedTime).ToArray();
            // Debug.Log("index:"+index);
            try{
                foreach (var i in index)
                    UpdateDisplay(ParentTrans.GetChild(i).GetComponent<TextMeshProUGUI>(), formattedTime[i].ToString());
            }
            catch{
            }

            OriginalFormattedTime = formattedTime;
        }
        isCountingDown = false;
    }

    private void UpdateDisplay(TMP_Text textComponent, string newValue)
    {
        // string newText = newValue.ToString("D2");
        string currentText = textComponent.text;

        // Check if the value has changed to animate it
        if (newValue != currentText)
            this.StartCoroutine(AnimateDigitChange(textComponent, newValue+currentText, newValue));
    }

    private IEnumerator AnimateDigitChange(TMP_Text textComponent, string oldText, string newText)
    {
        float fontSize = textComponent.fontSize;
        // Set the initial position for downward movement
        Vector3 originalPos = textComponent.rectTransform.anchoredPosition;
        textComponent.text = oldText;
        textComponent.rectTransform.anchoredPosition = originalPos+new Vector3(0, fontSize-10);
        originalPos = textComponent.rectTransform.anchoredPosition;

        // Move current digit down
        Vector2 targetPosition = textComponent.rectTransform.anchoredPosition + new Vector2(0, -(fontSize+10)); // Adjust based on font size
        float duration = 0.3f;
        yield return LerpPosition(textComponent, targetPosition, duration);

        // Replace with new text and move in from top
        textComponent.text = newText;
        textComponent.rectTransform.anchoredPosition += new Vector2(0, fontSize-10);

        // Move new digit to its final position
        targetPosition = originalPos-new Vector3(0, fontSize-10);
        yield return LerpPosition(textComponent, targetPosition, duration);
    }

    private IEnumerator LerpPosition(TMP_Text textComponent, Vector2 targetPosition, float duration)
    {
        Vector2 startPosition = textComponent.rectTransform.anchoredPosition;
        float timeElapsed = 0;

        while (timeElapsed < duration)
        {
            textComponent.rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, timeElapsed / duration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        
        textComponent.rectTransform.anchoredPosition = targetPosition;
    }
}
}