using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PageSwiper : MonoBehaviour, IDragHandler, IEndDragHandler, IPointerClickHandler{
    public Vector3 panelOriginalLocation;
    private Vector3 panelLocation;
    public float percentThreshold = 0.2f;
    public float easing = 0.5f;
    public int totalPages = 1;
    private int currentPage = 1;
    public bool isInX = true;
    public bool inverse;

    void Start(){
        panelLocation = transform.position;

        if(isInX){
            Drag = (data)=>{
                float difference = data.pressPosition.x - data.position.x;
                transform.position = panelLocation - new Vector3(difference, 0, 0);    
            };
            EndDrag = (data)=>{
                float percentage = (data.pressPosition.x - data.position.x)*(!inverse?1:-1) / Screen.width;
                if(Mathf.Abs(percentage) >= percentThreshold){
                    Vector3 newLocation = panelLocation;
                    if(percentage > 0 && currentPage < totalPages){
                        currentPage++;
                        newLocation += new Vector3(Screen.width*((!inverse)?-1:1), 0, 0);
                    }else if(percentage < 0 && currentPage > 1){
                        currentPage--;
                        newLocation += new Vector3(Screen.width*((!inverse)?1:-1), 0, 0);
                    }
                    StartCoroutine(SmoothMove(transform.position, newLocation, easing));
                    panelLocation = newLocation;
                }else{
                    StartCoroutine(SmoothMove(transform.position, panelLocation, easing));
                }
            };
        }
        else{
            Drag = (data)=>{
                float difference = data.pressPosition.y - data.position.y;
                transform.position = panelLocation - new Vector3(0, difference, 0);    
            };
            EndDrag = (data)=>{
                float percentage = (data.pressPosition.y - data.position.y)*(!inverse?1:-1) / Screen.height;
                if(Mathf.Abs(percentage) >= percentThreshold){
                    Vector3 newLocation = panelLocation;
                    if(percentage > 0 && currentPage < totalPages){
                        currentPage++;
                        newLocation += new Vector3(0, Screen.height*((!inverse)?-1:1), 0);
                    }else if(percentage < 0 && currentPage > 1){
                        currentPage--;
                        newLocation += new Vector3(0, Screen.height*((!inverse)?1:-1), 0);
                    }
                    StartCoroutine(SmoothMove(transform.position, newLocation, easing));
                    panelLocation = newLocation;
                }else{
                    StartCoroutine(SmoothMove(transform.position, panelLocation, easing));
                }
            };
        }
    }
    public void SetContentPanelAccordingToPage(int PageIndex)
    {
        currentPage = PageIndex;
        Vector3 newLocation = panelOriginalLocation;
        if(PageIndex != 1)
        {   
            if(isInX)
                newLocation.x += Screen.width*(PageIndex-1);
            else
                newLocation.y += Screen.height*(PageIndex-1);
        }
        panelLocation = newLocation;
        transform.position = newLocation;
    }

    // Click
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isDraging)
        {
            OnClick?.Invoke();
        }
    }
    [SerializeField]
    public Button.ButtonClickedEvent OnClick = new Button.ButtonClickedEvent();

    // Drag
    private bool isDraging;
    private Action<PointerEventData> Drag;
    public void OnDrag(PointerEventData data){
        isDraging = true;
        Drag.Invoke(data);
    }
    private Action<PointerEventData> EndDrag;
    public void OnEndDrag(PointerEventData data){
        EndDrag.Invoke(data);
        isDraging = false;
    }

    IEnumerator SmoothMove(Vector3 startpos, Vector3 endpos, float seconds){
        float t = 0f;
        while(t <= 1.0){
            t += Time.deltaTime / seconds;
            transform.position = Vector3.Lerp(startpos, endpos, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
    }
}