using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pastoral;
using System;
using UnityEngine.EventSystems;

public class HorizontalScrollUIp : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // Called when the panel is pressed or touched
        if(!AlreadyStarted)
            NavBarBtnCreator();
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        StopMoving?.Invoke();
    }
    void Start()
    {
        SetupNavElements();
    }
    private void OnEnable()
    {
        previousbtn = null;
    }

    [SerializeField] private RectTransform NavBarPanel;
    private int nearestTransToNavParentPos_ID;
    private bool RefreshDelay;
    private bool AlreadyStarted;
    private List<Transform> NavBtnTrans;
    private List<Vector2> NavBtnTransBounds;
    public void NavBarBtnCreator()
    {
        if(!IsNavSetup) return;
        StopAllCoroutines();
        StartCoroutine(NavBarMovementUpdate());
    }
    [Tooltip("Total number of elements")]
    public int ElementCount = 10;
    private int currentIndex_R;
    private int currentIndex_L;
    public IEnumerator NavBarMovementUpdate()
    {
        RefreshDelay = true;
        AlreadyStarted = true;
        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        Vector3 OriginalNavBarPanelPos = NavBarPanel.position;
#if ENABLE_INPUT_SYSTEM
        Vector2 screenPos = UnityEngine.InputSystem.Pointer.current != null ? UnityEngine.InputSystem.Pointer.current.position.ReadValue() : Vector2.zero;
        OriginalNavBarPanelPos.x = Camera.main.ScreenToWorldPoint(screenPos).x - OriginalNavBarPanelPos.x;
#else
        OriginalNavBarPanelPos.x = Camera.main.ScreenToWorldPoint(Input.mousePosition).x - OriginalNavBarPanelPos.x;
#endif
        Vector3[] LeftRightAnchorNavBarPos = GetLeftRightAnchorNavBarPos_V3();
        Vector3 currentNavBarPos = OriginalNavBarPanelPos;

        byte frameDelay = 1;
        Vector2 previousPos = NavBarPanel.position;
        float distanceToMove = 0;
        float velocity = 0;
        float decreaseVelocity = 1.05f;
        float minimumVelocity = 1;

        StopMoving = () => {
            if(!AlreadyStarted) return;
            AlreadyStarted = false;
            // Calculate the velocity
            float time = frameDelay * Time.deltaTime; // Time in seconds
            velocity = (currentNavBarPos.x-previousPos.x) / time;
        };
        Action<float, bool> ClampNavPanel = (float t, bool NotCountInputDirection) =>{
            float leftPoint = LeftRightAnchorNavBarPos[0].x + (NavBtnTransBounds[0].x/2);
            float rightPoint = LeftRightAnchorNavBarPos[1].x - (NavBtnTransBounds[NavBtnTrans.Count-1].x/2);
            if (currentIndex_L == 0 && General.NearestPointInList(LeftRightAnchorNavBarPos[0], NavBtnTrans) == 0 && (NavBarPanel.position.x < currentNavBarPos.x || NotCountInputDirection))
            {
                if(NavBtnTrans[0].position.x > leftPoint)
                {
                    currentNavBarPos.x = Mathf.Abs(leftPoint - NavBtnTrans[0].position.x);
                    currentNavBarPos.x = NavBarPanel.position.x - currentNavBarPos.x;
                    
                    currentNavBarPos.x = Mathf.Lerp(NavBarPanel.position.x, currentNavBarPos.x, t);
                    velocity = 0;
                }
            }
            else if(currentIndex_R == ElementCount-1 && General.NearestPointInList(LeftRightAnchorNavBarPos[1], NavBtnTrans) == NavBtnTrans.Count-1 && (NavBarPanel.position.x > currentNavBarPos.x || NotCountInputDirection))
            {
                if(NavBtnTrans[NavBtnTrans.Count-1].position.x < rightPoint)
                {
                    currentNavBarPos.x = Mathf.Abs(rightPoint - NavBtnTrans[NavBtnTrans.Count-1].position.x);
                    currentNavBarPos.x = NavBarPanel.position.x + currentNavBarPos.x;

                    currentNavBarPos.x = Mathf.Lerp(NavBarPanel.position.x, currentNavBarPos.x, t);
                    velocity = 0;
                }
            }
        };

        nearestTransToNavParentPos_ID = 0;
        Transform btn = NavBtnTrans[0];
        Transform previousbtn = NavBtnTrans[1];

        CanUpdateNavBar = true;
        StartCoroutine(UpdateNavBar());

        float distance = 0;

        bool limitReached = true;
        float t = 0.01f;
        while (AlreadyStarted || RefreshDelay || IsUpdatedLastFrame)
        {
            yield return new WaitForEndOfFrame();

            // Move navbar
            if (AlreadyStarted)
            {
#if ENABLE_INPUT_SYSTEM
                Vector2 screenPosDrag = UnityEngine.InputSystem.Pointer.current != null ? UnityEngine.InputSystem.Pointer.current.position.ReadValue() : Vector2.zero;
                currentNavBarPos.x = Camera.main.ScreenToWorldPoint(screenPosDrag).x - OriginalNavBarPanelPos.x;
#else
                currentNavBarPos.x = Camera.main.ScreenToWorldPoint(Input.mousePosition).x - OriginalNavBarPanelPos.x;
#endif

                // Apply limit to stop infinity scroll
                if(Vector3.Distance(NavBtnTrans[nearestTransToNavParentPos_ID].position, OriginalNavBarParentPos) > NavBarPanelBounds.x/3)
                {
                    nearestTransToNavParentPos_ID = General.NearestPointInList(OriginalNavBarParentPos, NavBtnTrans);
                }
                    ClampNavPanel(0.01f, false);

                NavBarPanel.position = currentNavBarPos;
                if (frameDelay < 20)
                    frameDelay++;
                else{
                    frameDelay = 1;
                    previousPos = NavBarPanel.position;
                }
            }
            else if (RefreshDelay || IsUpdatedLastFrame)
            {
                velocity /= decreaseVelocity; // inertiaDeduction

                distance = Vector3.Distance(NavBtnTrans[nearestTransToNavParentPos_ID].position, OriginalNavBarParentPos);
                // Apply limit to stop infinity scroll
                if(distance > NavBarPanelBounds.x/3 && limitReached)
                {
                    limitReached = false;
                    float time = frameDelay * Time.deltaTime; // Time in seconds
                    velocity = (currentNavBarPos.x-previousPos.x) / time;
                    velocity = Mathf.Max(velocity, NavBarPanelBounds.x/40);
                    velocity /= distance;
                    decreaseVelocity = 2;
                }

                // Calculate the distance to move in one frame
                distanceToMove = velocity * Time.deltaTime;
                // Move the transform towards the destination
                currentNavBarPos.x += distanceToMove;
                    ClampNavPanel(t, true);
                NavBarPanel.position = currentNavBarPos;
                // Debug.Log($"velocity {velocity} distanceToMove {distanceToMove}");
                
                if (t <= 1)
                    t += Time.deltaTime*10;
                else if (Mathf.Abs(velocity) < minimumVelocity)
                {
                    t = 1;
                    velocity = 0;
                    // Debug.Log($"applyingInertia");
                    RefreshDelay = false;
                }
                
            }
        }
        CanUpdateNavBar = false;

        // StartCoroutine(SnapNavBtnUpdate(nearestTransToNavParentPos_ID));
    }
    public Action StopMoving;
    private Vector2 NavBarPanelBounds;
    [Tooltip("The number of data content to load in the display area")]
    public int displayAreaCount = 10;
    public bool StartFromLeft;
    private bool IsNavSetup;
    public void SetupNavElements()
    {
        if(IsNavSetup || OnSettingNewElementCoroutine == null) return;
        StartCoroutine(SetupNavElementsUpdate());
    }
    private IEnumerator SetupNavElementsUpdate()
    {
        IsNavSetup = false;
        if(OnSettingNewElementCoroutine == null) yield break;

        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        NavBarPanelBounds = UIp.GetBoundsOfUI_Scalar(NavBarPanel.parent.GetComponent<RectTransform>());
        
        NavBtnTrans = new List<Transform>();
        NavBtnTransBounds = new List<Vector2>();

        float[] LeftRightAnchorNavBarPos = GetLeftRightAnchorNavBarPos_X();
        
        if(StartFromLeft)
        {
            currentIndex_L = 0;
            currentIndex_R = displayAreaCount;
            for(int i=currentIndex_L; i<=currentIndex_R; i++)
            {
                yield return StartCoroutine(SetElement(i, !StartFromLeft, NavBarPanel, LeftRightAnchorNavBarPos));
            }
        }
        else
        {
            currentIndex_R = ElementCount-1;
            currentIndex_L = currentIndex_R-displayAreaCount;
            for(int i=currentIndex_R; i>=currentIndex_L; i--)
            {
                yield return StartCoroutine(SetElement(i, !StartFromLeft, NavBarPanel, LeftRightAnchorNavBarPos));
            }
        }

        nearestTransToNavParentPos_ID = General.NearestPointInList(OriginalNavBarParentPos, NavBtnTrans);
        IsNavSetup = true;
    }
    private Vector3[] GetLeftRightAnchorNavBarPos_V3()
    {
        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        float scrollThreshold = NavBarPanelBounds.x/2;
        Vector3[] LeftRightAnchorNavBarPos = new Vector3[2];
        LeftRightAnchorNavBarPos[0] = OriginalNavBarParentPos;
        LeftRightAnchorNavBarPos[0].x -= scrollThreshold;
        LeftRightAnchorNavBarPos[1] = OriginalNavBarParentPos;
        LeftRightAnchorNavBarPos[1].x += scrollThreshold;
        return LeftRightAnchorNavBarPos;
    }
    private float[] GetLeftRightAnchorNavBarPos_X()
    {
        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        float scrollThreshold = NavBarPanelBounds.x/2;
        float[] LeftRightAnchorNavBarPos = new float[2];
        LeftRightAnchorNavBarPos[0] = OriginalNavBarParentPos.x;
        LeftRightAnchorNavBarPos[0] -= scrollThreshold;
        LeftRightAnchorNavBarPos[1] = OriginalNavBarParentPos.x;
        LeftRightAnchorNavBarPos[1] += scrollThreshold;
        return LeftRightAnchorNavBarPos;
    }
    private Transform GetNavBarPanelAccordingTo(bool AddFromLeft = true)
    {
        if(AddFromLeft)
            return NavBarPanel.GetChild(NavBarPanel.childCount-1);
        else
            return NavBarPanel.GetChild(0);
    }
    private int GetNavBtnTransIndexAccordingTo(bool AddFromLeft = true)
    {
        if(AddFromLeft)
            return 0;
        else
            return NavBtnTrans.Count-1;
    }
    private Transform previousbtn;
    private IEnumerator SnapNavBtnUpdate(int newBtnSelectedID, Sides SnapToSide)
    {
        Vector2 fromPos = NavBarPanel.position;
        Vector2 toPos;
        switch (SnapToSide)
        {
            case Sides.Left:
                toPos = fromPos + (Vector2)(NavBarPanel.parent.position - NavBtnTrans[newBtnSelectedID].position);
                break;
            default:
                toPos = fromPos + (Vector2)(NavBarPanel.parent.position - NavBtnTrans[newBtnSelectedID].position);
                break;
        }
        
        Transform btn;
        Vector2 pos = NavBarPanel.parent.position;
        float t=0;
        while (t<1)
        {
            yield return new WaitForEndOfFrame();
            t += Time.deltaTime;
            NavBarPanel.position = Vector2.Lerp(fromPos, toPos, (float)easeInOutCubic(t));

            nearestTransToNavParentPos_ID = General.NearestPointInList(pos, NavBtnTrans);
            btn = NavBtnTrans[nearestTransToNavParentPos_ID];
            if(btn == previousbtn) continue;
            previousbtn = btn;
        }
        NavBarPanel.position = toPos;
    }
    public static double easeInOutCubic(double t)
    {
        // If t is less than half of the duration, apply the cubic easing in formula
        if (t < 0.5)
        {
            return 4 * t * t * t;
        }
        // Otherwise, apply the cubic easing out formula
        else
        {
            return 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }
    }
    private bool CanUpdateNavBar;
    private bool IsUpdatedLastFrame;
    [Tooltip("Offset value increase the element creation point by LeftRightAnchorNavBarPos += Offset*NavBarPanelBounds.x")]
    public float Offset;
    private void SetOffset(float[] LeftRightAnchorNavBarPos)
    {
        float scrollThreshold = NavBarPanelBounds.x;
        LeftRightAnchorNavBarPos[0] -= Offset*scrollThreshold;
        LeftRightAnchorNavBarPos[1] += Offset*scrollThreshold;
    }
    private IEnumerator UpdateNavBar()
    {
        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        float[] LeftRightAnchorNavBarPos = GetLeftRightAnchorNavBarPos_X();
        SetOffset(LeftRightAnchorNavBarPos);

        // Transform btn;
        // Transform previousbtn = NavBtnTrans[1];
        IsUpdatedLastFrame = false;
        int tempIndex;

        Action setNearsetBtn = ()=>{
            IsUpdatedLastFrame = false;

            nearestTransToNavParentPos_ID = General.NearestPointInList(OriginalNavBarParentPos, NavBtnTrans);
            // btn = NavBtnTrans[nearestTransToNavParentPos_ID];
            // if(btn == previousbtn) return;
            // previousbtn = btn;
        };
        while (CanUpdateNavBar || IsUpdatedLastFrame)
        {
            if(!IsUpdatedLastFrame)
                yield return new WaitForEndOfFrame();
            // Adding At left
            if(NavBtnTrans[0] != null && NavBtnTrans[0].position.x > LeftRightAnchorNavBarPos[0])
            {
                tempIndex = General.GetIndexInList(currentIndex_L, ElementCount, -1);
                if(tempIndex > currentIndex_L){ // For not looping
                    setNearsetBtn();
                    continue;
                }
                currentIndex_L = tempIndex;
                currentIndex_R = General.GetIndexInList(currentIndex_L, ElementCount, displayAreaCount+1);
                IsUpdatedLastFrame = true;

                yield return StartCoroutine(SetElement(currentIndex_L, true, NavBarPanel, LeftRightAnchorNavBarPos));
            }
            // Adding at Right
            else if(NavBtnTrans[displayAreaCount-1] != null && NavBtnTrans[displayAreaCount-1].position.x < LeftRightAnchorNavBarPos[1])
            {
                tempIndex = General.GetIndexInList(currentIndex_R, ElementCount, 1);
                if(currentIndex_R > tempIndex){ // For not looping
                    setNearsetBtn();
                    continue;
                }
                currentIndex_R = tempIndex;
                currentIndex_L = General.GetIndexInList(currentIndex_R, ElementCount, -displayAreaCount-1);
                IsUpdatedLastFrame = true;

                yield return StartCoroutine(SetElement(currentIndex_R, false, NavBarPanel, LeftRightAnchorNavBarPos));
            }
            else
            {
                setNearsetBtn();
            }
        }
        // StartCoroutine(SnapNavBtnUpdate(nearestTransToNavParentPos_ID));
    }

    public delegate IEnumerator SetElementCoroutine(int index, bool AddFromLeft, Transform NavBarPanel);
    public SetElementCoroutine OnSettingNewElementCoroutine;
    private IEnumerator SetElement(int index, bool AddFromLeft, Transform NavBarPanel, float[] LeftRightAnchorNavBarPos)
    {
        yield return StartCoroutine(OnSettingNewElementCoroutine(index, AddFromLeft, NavBarPanel));
        int previousElementIndex = GetNavBtnTransIndexAccordingTo(AddFromLeft);
        Vector3 position = NavBtnTrans.Count != 0?
                                                NavBtnTrans[previousElementIndex].position
                                                : new Vector3(LeftRightAnchorNavBarPos[AddFromLeft?1:0], NavBarPanel.position.y);
        Vector2 previousElementBounds = NavBtnTrans.Count != 0? NavBtnTransBounds[previousElementIndex]: Vector2.zero; 

        Transform currentElementtrans = GetNavBarPanelAccordingTo(AddFromLeft);
        Vector2 bounds = UIp.GetBoundsOfUI(currentElementtrans.GetComponent<RectTransform>());
        if(AddFromLeft)
        {
            if(NavBtnTrans.Count > displayAreaCount)
            {
                Destroy(NavBtnTrans[NavBtnTrans.Count-1].gameObject);
                NavBtnTrans.RemoveAt(NavBtnTrans.Count-1);
                NavBtnTransBounds.RemoveAt(NavBtnTrans.Count-1);
            }
            NavBtnTrans.Insert(0, currentElementtrans);
            NavBtnTransBounds.Insert(0, bounds);
        } 
        else
        {
            if(NavBtnTrans.Count > displayAreaCount)
            {
                Destroy(NavBtnTrans[0].gameObject);
                NavBtnTrans.RemoveAt(0);
                NavBtnTransBounds.RemoveAt(0);
            }
            NavBtnTrans.Add(currentElementtrans);
            NavBtnTransBounds.Add(bounds);
        }

        float difference = previousElementBounds.x/2 + bounds.x/2;
        if(AddFromLeft)
            position.x -= difference;
        else position.x += difference;

        currentElementtrans.position = position;
    }
    
    // // Testing Demo // Also subscribe OnSettingNewElementCoroutine like this // OnSettingNewElementCoroutine = CreatElement;
    // public GameObject testGameObject;
    // private IEnumerator CreatElement(int index, bool AddFromLeft, Transform NavBarPanel)
    // {
    //     // Create element here
    //     RectTransform elementTrans = Instantiate(testGameObject).GetComponent<RectTransform>();
    //     elementTrans.sizeDelta = new Vector2(2, index+1);

    //     yield return new WaitForEndOfFrame();
    //     SetInsideView(elementTrans, AddFromDown);
    // }
    public void SetInsideView(Transform elementTrans, bool AddFromDown)
    {
        elementTrans.SetParent(NavBarPanel, true);
        if (!AddFromDown)
        {
            elementTrans.SetAsFirstSibling();
        }
    }
}