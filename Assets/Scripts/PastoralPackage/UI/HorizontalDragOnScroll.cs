using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System;

namespace Pastoral.UI
{
public class HorizontalDragOnScroll : ScrollRect
{
    private Vector2 lastDragPosition;
    private bool isHorizontalDragDetected = false;

    // This threshold will help us determine if the horizontal drag is significant enough
    public float horizontalDragThreshold = 50f; // Change this as needed

    // Override the OnDrag method
    public override void OnDrag(PointerEventData eventData)
    {
        if(isHorizontalDragDetected){
            ExecuteEvents.Execute(slideGesture, eventData, ExecuteEvents.dragHandler);
            return;
        }

        base.OnDrag(eventData); // Call the base method to ensure default behavior

        // Get the difference in position between the current and previous drag positions
        Vector2 dragDelta = eventData.position - lastDragPosition;

        // Check if the horizontal drag is greater than the vertical drag
        if (Mathf.Abs(dragDelta.x) > Mathf.Abs(dragDelta.y) && Mathf.Abs(dragDelta.x) > horizontalDragThreshold)
        {
            ActivateHorizontalDrag?.Invoke();
            isHorizontalDragDetected = true;
            Debug.Log("Horizontal Drag Detected");
            // You can trigger your custom logic for horizontal drag here.
            // Example: ActivateHorizontalDragPanel();
        }
        else
        {
            isHorizontalDragDetected = false;
        }

        // Update the last drag position to the current one
        lastDragPosition = eventData.position;
    }
    public Action ActivateHorizontalDrag;

    [SerializeField] public GameObject slideGesture;
    public override void OnBeginDrag(PointerEventData eventData){
        if(isHorizontalDragDetected){
            ExecuteEvents.Execute(slideGesture, eventData, ExecuteEvents.beginDragHandler);
            return;
        }
        base.OnBeginDrag(eventData);
    }
    public override void OnEndDrag(PointerEventData eventData){
        if(isHorizontalDragDetected){
            ExecuteEvents.Execute(slideGesture, eventData, ExecuteEvents.endDragHandler);
            isHorizontalDragDetected = false;
            return;
        }
        base.OnEndDrag(eventData);
    }
    
}
}
