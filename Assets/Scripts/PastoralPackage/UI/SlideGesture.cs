using UnityEngine;
using UnityEngine.EventSystems; // For event handling
using UnityEngine.Events; // For Unity events

namespace Pastoral.UI
{
    public class SlideGesture : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public HorizontalDragOnScroll[] horizontalDragOnScrolls;
        private void Awake() {
            foreach (var horizontalDragOnScroll in horizontalDragOnScrolls)
            {
                horizontalDragOnScroll.ActivateHorizontalDrag += ()=>{
                    // ScrollPanel.SetActive(true);

                    PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
#if ENABLE_INPUT_SYSTEM
                    Vector2 currentPos = UnityEngine.InputSystem.Pointer.current != null ? UnityEngine.InputSystem.Pointer.current.position.ReadValue() : Vector2.zero;
                    pointerEventData.position = currentPos;
#else
                    pointerEventData.position = Input.mousePosition; // or your touch position
#endif
                    ExecuteEvents.Execute(gameObject, pointerEventData, ExecuteEvents.beginDragHandler);
                };
            }
        }
        public GameObject ScrollPanel;
        // UnityEvent that will be triggered on drag complete
        public UnityEvent<Vector2> onDragComplete;
        public UnityEvent<Vector2> onDragStarted;
        public UnityEvent<Vector2> onDragging;

        // Variables to store the start and end positions of the drag
        private Vector2 dragStartPos;
        private Vector2 dragEndPos;

        // Called when dragging begins
        public void OnBeginDrag(PointerEventData eventData)
        {
            // Store the starting position of the drag
            Debug.Log("OnBeginDrag slide guster");
            dragStartPos = eventData.position;
            onDragStarted?.Invoke(eventData.position);
        }

        // Called during dragging
        public void OnDrag(PointerEventData eventData)
        {
            onDragging?.Invoke(eventData.position);
        }

        // Called when dragging ends (complete)
        public void OnEndDrag(PointerEventData eventData)
        {
            // Store the end position of the drag
            dragEndPos = eventData.position;

            // Determine the direction of the drag
            Vector2 dragVector = dragEndPos - dragStartPos;
            string direction = GetDragDirection(dragVector);

            // Optionally, log the direction of the drag
            Debug.Log("Drag Complete. Direction: " + direction);
            
            // Trigger the UnityEvent
            onDragComplete?.Invoke(dragVector);

            ScrollPanel.SetActive(false);
        }

        // Function to determine the direction of the drag
        private string GetDragDirection(Vector2 dragVector)
        {
            float angle = Mathf.Atan2(dragVector.y, dragVector.x) * Mathf.Rad2Deg;

            if (Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y))
            {
                // Horizontal drag (left or right)
                if (dragVector.x > 0)
                    return "Right";
                else
                    return "Left";
            }
            else
            {
                // Vertical drag (up or down)
                if (dragVector.y > 0)
                    return "Up";
                else
                    return "Down";
            }
        }
    }
}
