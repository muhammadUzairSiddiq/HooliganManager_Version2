using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pastoral;
using System;
using UnityEngine.EventSystems;

namespace Pastoral.UI{
public class VerticalScrollUIp : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
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
    private Coroutine NavBarMovementUpdateCoroutine;
    public void NavBarBtnCreator()
    {
        if(NavBarMovementUpdateCoroutine!=null) StopCoroutine(NavBarMovementUpdateCoroutine);
        NavBarMovementUpdateCoroutine = StartCoroutine(NavBarMovementUpdate());
    }
    [Tooltip("Total number of elements")]
    public int ElementCount = 10;
    private int currentIndex_U;
    private int currentIndex_D;
    public IEnumerator NavBarMovementUpdate()
    {
        RefreshDelay = true;
        AlreadyStarted = true;
        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        Vector3 OriginalNavBarPanelPos = NavBarPanel.position;
#if ENABLE_INPUT_SYSTEM
        Vector2 screenPos = UnityEngine.InputSystem.Pointer.current != null ? UnityEngine.InputSystem.Pointer.current.position.ReadValue() : Vector2.zero;
        OriginalNavBarPanelPos.y = Camera.main.ScreenToWorldPoint(screenPos).y - OriginalNavBarPanelPos.y;
#else
        OriginalNavBarPanelPos.y = Camera.main.ScreenToWorldPoint(Input.mousePosition).y - OriginalNavBarPanelPos.y;
#endif
        Vector3[] LeftRightAnchorNavBarPos = GetLeftRightAnchorNavBarPos_V3();
        Vector3 currentNavBarPos = OriginalNavBarPanelPos;

        byte frameDelay = 1;
        const byte maxFrameDelay = 10;
        Vector2 previousPos = NavBarPanel.position;
        float distanceToMove = 0;
        float velocity = 0;
        float decreaseVelocity = 1.15f; // 1.05f;
        float minimumVelocity = 1;

        StopMoving = () => {
            if(!AlreadyStarted) return;
            AlreadyStarted = false;
            // Calculate the velocity
            float time = frameDelay * Time.deltaTime; // Time in seconds
            velocity = (currentNavBarPos.y-previousPos.y) / time;
        };
        Action<float, bool> ClampNavPanel = (float t, bool NotCountInputDirection) =>{
            float leftPoint = LeftRightAnchorNavBarPos[0].y + (NavBtnTransBounds[0].y/2);
            float rightPoint = LeftRightAnchorNavBarPos[1].y - (NavBtnTransBounds[NavBtnTrans.Count-1].y/2);
            if (currentIndex_U == 0 && General.NearestPointInList(LeftRightAnchorNavBarPos[0], NavBtnTrans) == 0 && (NavBarPanel.position.y < currentNavBarPos.y || NotCountInputDirection))
            {
                if(NavBtnTrans[0].position.y > leftPoint)
                {
                    currentNavBarPos.y = Mathf.Abs(leftPoint - NavBtnTrans[0].position.y);
                    currentNavBarPos.y = NavBarPanel.position.y - currentNavBarPos.y;
                    
                    currentNavBarPos.y = Mathf.Lerp(NavBarPanel.position.y, currentNavBarPos.y, t);
                    velocity = 0;
                }
            }
            else if(currentIndex_D == ElementCount-1 && General.NearestPointInList(LeftRightAnchorNavBarPos[1], NavBtnTrans) == NavBtnTrans.Count-1 && (NavBarPanel.position.y > currentNavBarPos.y || NotCountInputDirection))
            {
                if(NavBtnTrans[NavBtnTrans.Count-1].position.y < rightPoint)
                {
                    currentNavBarPos.y = Mathf.Abs(rightPoint - NavBtnTrans[NavBtnTrans.Count-1].position.y);
                    currentNavBarPos.y = NavBarPanel.position.y + currentNavBarPos.y;

                    currentNavBarPos.y = Mathf.Lerp(NavBarPanel.position.y, currentNavBarPos.y, t);
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

        // bool limitReached = true;
        float t = 0.01f;
        while (AlreadyStarted || RefreshDelay || IsUpdatedLastFrame)
        {
            yield return new WaitForEndOfFrame();

            // Move navbar
            if (AlreadyStarted)
            {
#if ENABLE_INPUT_SYSTEM
                Vector2 screenPosDrag = UnityEngine.InputSystem.Pointer.current != null ? UnityEngine.InputSystem.Pointer.current.position.ReadValue() : Vector2.zero;
                currentNavBarPos.y = Camera.main.ScreenToWorldPoint(screenPosDrag).y - OriginalNavBarPanelPos.y;
#else
                currentNavBarPos.y = Camera.main.ScreenToWorldPoint(Input.mousePosition).y - OriginalNavBarPanelPos.y;
#endif

                // Apply limit to stop infinity scroll
                if(Vector3.Distance(NavBtnTrans[nearestTransToNavParentPos_ID].position, OriginalNavBarParentPos) > NavBarPanelBounds.y/3)
                {
                    nearestTransToNavParentPos_ID = General.NearestPointInList(OriginalNavBarParentPos, NavBtnTrans);
                }
                    ClampNavPanel(0.01f, false);

                NavBarPanel.position = currentNavBarPos;
                if (frameDelay < maxFrameDelay)
                    frameDelay++;
                else{
                    frameDelay = 1;
                    previousPos = NavBarPanel.position;
                }
            }
            else if (RefreshDelay || IsUpdatedLastFrame)
            {
                distance = Vector3.Distance(NavBtnTrans[nearestTransToNavParentPos_ID].position, OriginalNavBarParentPos);
                // // Apply limit to stop infinity scroll
                // if(distance > NavBarPanelBounds.y/3 && limitReached)
                // {
                //     limitReached = false;
                //     float time = maxFrameDelay * Time.deltaTime; // Time in seconds
                //     velocity = (currentNavBarPos.y-previousPos.y) / time;
                //     // velocity = Mathf.Max(velocity, NavBarPanelBounds.y/40);
                //     velocity /= Mathf.Min(1, distance);
                //     decreaseVelocity = 2;
                //     Debug.Log($"velocity {velocity} distance {distance}");
                // }

                // Calculate the distance to move in one frame
                distanceToMove = velocity * Time.deltaTime;
                velocity /= decreaseVelocity; // inertiaDeduction

                // Move the transform towards the destination
                currentNavBarPos.y += distanceToMove;
                    ClampNavPanel(t, true);
                NavBarPanel.position = currentNavBarPos;
                // Debug.Log($"velocity {velocity} distanceToMove {distanceToMove}");
                
                if (t <= 1)
                    t += Time.deltaTime*10;
                else if (Mathf.Abs(velocity) < minimumVelocity)
                {
                    t = 1;
                    velocity = 0;
                    RefreshDelay = false;
                }
                
            }
            HideContentOutSidetheBounds(LeftRightAnchorNavBarPos);
        }
        CanUpdateNavBar = false;

        // StartCoroutine(SnapNavBtnUpdate(nearestTransToNavParentPos_ID));
    }
    public Action StopMoving;
    private Vector2 NavBarPanelBounds;
    [Tooltip("The number of data content to load in the display area")]
    public int displayAreaCount = 10;
    public bool StartFromDown;
    private bool IsNavSetup;
    /// <summary>
    /// // Testing Demo // Also subscribe OnSettingNewElementCoroutine like this // OnSettingNewElementCoroutine = CreatElement;
    /// private IEnumerator CreatElement(int index, bool AddFromDown)
    /// {
    ///     yield return StartCoroutine(SurahContentToLoad((byte)(FromToSurah[0]+index+1)));
    ///     yield return new WaitForEndOfFrame();
    ///     VerticalScrollUIp.SetInsideView(LinePanelParent, AddFromDown);
    /// }
    /// </summary>
    /// <param name="int index"></param>
    /// <param name="bool AddFromDown"></param>
    public void SetupNavElements(int ElementCount, SetElementCoroutine OnSettingNewElementCreation)
    {
        this.ElementCount = ElementCount;
        this.OnSettingNewElementCoroutine = OnSettingNewElementCreation;
        if(IsNavSetup || OnSettingNewElementCoroutine == null) return;
        StartCoroutine(SetupNavElementsUpdate());
    }
    private IEnumerator SetupNavElementsUpdate()
    {
        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        NavBarPanelBounds = UIp.GetBoundsOfUI_Scalar(NavBarPanel.parent.GetComponent<RectTransform>());
        
        NavBtnTrans = new List<Transform>();
        NavBtnTransBounds = new List<Vector2>();
        

        float[] LeftRightAnchorNavBarPos = GetLeftRightAnchorNavBarPos_Y();
        if(StartFromDown)
        {
            currentIndex_U = 0;
            currentIndex_D = currentIndex_U;
            for(; currentIndex_D<=displayAreaCount; currentIndex_D++)
            {
                yield return StartCoroutine(SetElement(currentIndex_D, !StartFromDown, NavBarPanel, LeftRightAnchorNavBarPos));
            }
        }
        else
        {
            currentIndex_D = ElementCount-1;
            currentIndex_U = currentIndex_D;
            for(; currentIndex_U>=currentIndex_D-displayAreaCount; currentIndex_U--)
            {
                yield return StartCoroutine(SetElement(currentIndex_U, !StartFromDown, NavBarPanel, LeftRightAnchorNavBarPos));
            }
        }

        nearestTransToNavParentPos_ID = General.NearestPointInList(OriginalNavBarParentPos, NavBtnTrans);
        IsNavSetup = true;
    }
    private Vector3[] GetLeftRightAnchorNavBarPos_V3()
    {
        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        float scrollThreshold = NavBarPanelBounds.y/2;
        Vector3[] LeftRightAnchorNavBarPos = new Vector3[2];
        LeftRightAnchorNavBarPos[0] = OriginalNavBarParentPos;
        LeftRightAnchorNavBarPos[0].y -= scrollThreshold;
        LeftRightAnchorNavBarPos[1] = OriginalNavBarParentPos;
        LeftRightAnchorNavBarPos[1].y += scrollThreshold;
        return LeftRightAnchorNavBarPos;
    }
    private float[] GetLeftRightAnchorNavBarPos_Y()
    {
        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        float scrollThreshold = NavBarPanelBounds.y/2;
        float[] LeftRightAnchorNavBarPos = new float[2];
        LeftRightAnchorNavBarPos[0] = OriginalNavBarParentPos.y;
        LeftRightAnchorNavBarPos[0] -= scrollThreshold;
        LeftRightAnchorNavBarPos[1] = OriginalNavBarParentPos.y;
        LeftRightAnchorNavBarPos[1] += scrollThreshold;
        return LeftRightAnchorNavBarPos;
    }
    private Transform GetNavBarPanelAccordingTo(bool AddFromDown = true)
    {
        if(AddFromDown)
            return NavBarPanel.GetChild(NavBarPanel.childCount-1);
        else
            return NavBarPanel.GetChild(0);
    }
    private int GetNavBtnTransIndexAccordingTo(bool AddFromDown = true)
    {
        if(AddFromDown)
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
        float scrollThreshold = NavBarPanelBounds.y;
        LeftRightAnchorNavBarPos[0] -= Offset*scrollThreshold;
        LeftRightAnchorNavBarPos[1] += Offset*scrollThreshold;
    }
    private bool IsUpdateingNavBar;
    private IEnumerator UpdateNavBar()
    {
        if(IsUpdateingNavBar) yield break;
        IsUpdateingNavBar = true;
        while(!IsNavSetup)
        {
            yield return new WaitForEndOfFrame();
        }

        Vector3 OriginalNavBarParentPos = NavBarPanel.parent.position;
        Vector3[] LeftRightAnchorNavBarPosV3 = GetLeftRightAnchorNavBarPos_V3();
        float[] LeftRightAnchorNavBarPos = {LeftRightAnchorNavBarPosV3[0].y, LeftRightAnchorNavBarPosV3[1].y};
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
            if(NavBtnTrans[0] != null && NavBtnTrans[0].position.y > LeftRightAnchorNavBarPos[0])
            {
                tempIndex = General.GetIndexInList(currentIndex_U, ElementCount, -1);
                if(tempIndex > currentIndex_U){ // For not looping
                    setNearsetBtn();
                    continue;
                }
                currentIndex_U = tempIndex;
                currentIndex_D = General.GetIndexInList(currentIndex_U, ElementCount, displayAreaCount+1);
                IsUpdatedLastFrame = true;

                yield return StartCoroutine(SetElement(currentIndex_U, true, NavBarPanel, LeftRightAnchorNavBarPos));
            }
            // Adding at Right
            else if(NavBtnTrans[displayAreaCount-1] != null && NavBtnTrans[displayAreaCount-1].position.y < LeftRightAnchorNavBarPos[1])
            {
                tempIndex = General.GetIndexInList(currentIndex_D, ElementCount, 1);
                if(currentIndex_D > tempIndex){ // For not looping
                    setNearsetBtn();
                    continue;
                }
                currentIndex_D = tempIndex;
                currentIndex_U = General.GetIndexInList(currentIndex_D, ElementCount, -displayAreaCount-1);
                IsUpdatedLastFrame = true;

                yield return StartCoroutine(SetElement(currentIndex_D, false, NavBarPanel, LeftRightAnchorNavBarPos));
            }
            else
            {
                setNearsetBtn();
            }
            HideContentOutSidetheBounds(LeftRightAnchorNavBarPosV3);
        }
        IsUpdateingNavBar = false;
        // StartCoroutine(SnapNavBtnUpdate(nearestTransToNavParentPos_ID));
    }

    public delegate IEnumerator SetElementCoroutine(int index, bool AddFromDown);
    public SetElementCoroutine OnSettingNewElementCoroutine;
    private IEnumerator SetElement_Previous(int index, bool AddFromDown, Transform NavBarPanel, float[] LeftRightAnchorNavBarPos)
    {
        yield return StartCoroutine(OnSettingNewElementCoroutine(index, AddFromDown));
        int previousElementIndex = GetNavBtnTransIndexAccordingTo(AddFromDown);
        Vector3 position = NavBtnTrans.Count != 0?
                                                NavBtnTrans[previousElementIndex].position
                                                : new Vector3(NavBarPanel.position.x , LeftRightAnchorNavBarPos[AddFromDown?1:0]);
        Vector2 previousElementBounds = NavBtnTrans.Count != 0? NavBtnTransBounds[previousElementIndex]: Vector2.zero; 

        Transform currentElementtrans = GetNavBarPanelAccordingTo(AddFromDown);
        Vector2 bounds = UIp.GetBoundsOfUI_Scalar(currentElementtrans.GetComponent<RectTransform>());
        if(AddFromDown)
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

        float difference = previousElementBounds.y/2 + bounds.y/2;
        if(AddFromDown)
            position.y -= difference;
        else position.y += difference;
        currentElementtrans.position = position;
    }
    private IEnumerator SetElement(int index, bool AddFromDown, Transform NavBarPanel, float[] LeftRightAnchorNavBarPos)
    {
        yield return StartCoroutine(OnSettingNewElementCoroutine(index, AddFromDown));

        int previousElementIndex = GetNavBtnTransIndexAccordingTo(AddFromDown);
        Vector3 previousPosition = NavBtnTrans.Count > 0 ? NavBtnTrans[previousElementIndex].position : new Vector3(NavBarPanel.position.x, LeftRightAnchorNavBarPos[AddFromDown ? 1 : 0]);
        Vector2 previousElementBounds = NavBtnTrans.Count > 0 ? NavBtnTransBounds[previousElementIndex] : Vector2.zero;

        Transform currentElementTrans = GetNavBarPanelAccordingTo(AddFromDown);
        Vector2 currentBounds = UIp.GetBoundsOfUI_Scalar(currentElementTrans.GetComponent<RectTransform>());

        Vector3 newPosition = previousPosition;
        newPosition.y += AddFromDown ? -((previousElementBounds.y / 2) + (currentBounds.y / 2)) : ((previousElementBounds.y / 2) + (currentBounds.y / 2));
        
        currentElementTrans.position = newPosition;

        if (AddFromDown)
        {
            if (NavBtnTrans.Count > displayAreaCount)
            {
                Destroy(NavBtnTrans[NavBtnTrans.Count - 1].gameObject);
                NavBtnTrans.RemoveAt(NavBtnTrans.Count - 1);
                NavBtnTransBounds.RemoveAt(NavBtnTransBounds.Count - 1);
            }
            NavBtnTrans.Insert(0, currentElementTrans);
            NavBtnTransBounds.Insert(0, currentBounds);
        }
        else
        {
            if (NavBtnTrans.Count > displayAreaCount)
            {
                Destroy(NavBtnTrans[0].gameObject);
                NavBtnTrans.RemoveAt(0);
                NavBtnTransBounds.RemoveAt(0);
            }
            NavBtnTrans.Add(currentElementTrans);
            NavBtnTransBounds.Add(currentBounds);
        }
    }

    public void SetInsideView(Transform elementTrans, bool AddFromDown)
    {
        elementTrans.SetParent(NavBarPanel, true);
        if (!AddFromDown)
        {
            elementTrans.SetAsFirstSibling();
        }
    }

    private List<int> nearestTransIndex = new List<int>();
    private void HideContentOutSidetheBounds(Vector3[] LeftRightAnchorNavBarPos)
    {
        nearestTransIndex.Clear();
        int startNearestTransToNavParentPos_ID = General.NearestPointInList(LeftRightAnchorNavBarPos[0], NavBtnTrans);
        if(startNearestTransToNavParentPos_ID-1>=0) nearestTransIndex.Add(startNearestTransToNavParentPos_ID-1);
        int endNearestTransToNavParentPos_ID = General.NearestPointInList(LeftRightAnchorNavBarPos[1], NavBtnTrans);
        if(endNearestTransToNavParentPos_ID+1<NavBtnTrans.Count) nearestTransIndex.Add(endNearestTransToNavParentPos_ID+1);
        for (; startNearestTransToNavParentPos_ID <= endNearestTransToNavParentPos_ID; startNearestTransToNavParentPos_ID++)
        {
            nearestTransIndex.Add(startNearestTransToNavParentPos_ID);
        }

        // nearestTransIndex.Add(nearestTransToNavParentPos_ID);
        // if(nearestTransToNavParentPos_ID-1>=0) nearestTransIndex.Add(nearestTransToNavParentPos_ID-1);
        // if(nearestTransToNavParentPos_ID+1<NavBtnTrans.Count) nearestTransIndex.Add(nearestTransToNavParentPos_ID+1);


        for (int i = 0; i < NavBtnTrans.Count; i++)
        {
            if (nearestTransIndex.Contains(i))
                NavBtnTrans[i].gameObject.SetActive(true);
            else
                NavBtnTrans[i].gameObject.SetActive(false);
        }
    }
}
}