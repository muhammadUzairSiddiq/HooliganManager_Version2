using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pastoral.UI
{    
public class ContinuousLeftToRightMovement : MonoBehaviour
{
    // Speed of movement
    public float speed = 5f;

    // Boundaries for movement
    float leftBoundary = -10f;
    float rightBoundary = 10f;

    private void Start() {
        Vector2 originalPos = transform.parent.parent.position;
        originalPos.y = transform.position.y;
        Vector2 bounds = UIp.GetBoundsOfUI_Scalar(transform.GetComponent<RectTransform>())/2;
        leftBoundary = originalPos.x-bounds.x;
        rightBoundary = originalPos.x+bounds.x;
        Debug.DrawRay(originalPos, Vector2.up*300, Color.grey, 400);
        Debug.DrawRay(new Vector3(leftBoundary , originalPos.y), Vector2.up*300, Color.green, 100);
        Debug.DrawRay(new Vector3(rightBoundary , originalPos.y), Vector2.up*300, Color.green, 100);

        bounds = UIp.GetBoundsOfUI_Scalar(MoveContent.GetComponent<RectTransform>())/2;
        leftBoundary -= bounds.x;
        rightBoundary += bounds.x;
        Debug.DrawRay(new Vector3(leftBoundary , originalPos.y), Vector2.up*100, Color.red, 100);
        Debug.DrawRay(new Vector3(rightBoundary , originalPos.y), Vector2.up*100, Color.red, 100);

        if(speed > 0)
            MovementUpdate = ()=>{
                // Move the object to the right continuously
                MoveContent.Translate(Vector3.right * speed * Time.deltaTime);

                // Check if the object reaches the right boundary
                if (MoveContent.position.x >= rightBoundary)
                {
                    // Reset the object's position to the left boundary
                    Vector3 newPosition = MoveContent.position;
                    newPosition.x = leftBoundary;
                    MoveContent.position = newPosition;
                }
            };
        else
            MovementUpdate = ()=>{
                // Move the object to the left continuously
                MoveContent.Translate(Vector3.right * speed * Time.deltaTime);

                // Check if the object reaches the left boundary
                if (MoveContent.position.x <= leftBoundary)
                {
                    // Reset the object's position to the left boundary
                    Vector3 newPosition = MoveContent.position;
                    newPosition.x = rightBoundary;
                    MoveContent.position = newPosition;
                }
            };
    }
    Action MovementUpdate;
    [SerializeField] private Transform MoveContent;
    void Update()
    {
        MovementUpdate();
    }
}
}