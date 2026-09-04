using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using System.Data.Common;
using Unity.VisualScripting;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Pastoral
{
    public static class Stringp
    {
        public static string Substring(string dataStr, int startIndex, int endIndex)
        {
            return dataStr.Substring(startIndex, endIndex - startIndex + 1);
        }
        public static List<int> GetChangedIndices(string original, string updated)
        {
            List<int> changedIndices = new List<int>();
            int minLength = Math.Min(original.Length, updated.Length);

            // Compare characters up to the length of the shorter string
            for (int i = 0; i < minLength; i++)
            {
                if (original[i] != updated[i])
                    changedIndices.Add(i);
            }

            // Handle extra characters in the longer string
            if (original.Length != updated.Length)
            {
                int longerLength = Math.Max(original.Length, updated.Length);
                for (int i = minLength; i < longerLength; i++)
                    changedIndices.Add(i);
            }

            return changedIndices;
        }
    }
    public static class Imagep
    {
        public static Sprite TextureToSprite(Texture2D texture2D)
        {
            return Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), Vector2.one * 0.5f);
        }
        public static Sprite CapturePanelToSprite(Transform panelTrans, Vector2 panelDimesions, Camera camera)
        {
            return Sprite.Create(CapturePanelToTexture(panelTrans ,panelDimesions, camera), new Rect(0, 0, (int)panelDimesions.x, (int)panelDimesions.y), Vector2.one * 0.5f);
        }
        public static Texture2D CapturePanelToTexture(Transform panelTrans, Vector2 panelDimesions, Camera camera)
        {
            // Create a new RenderTexture with the same size as the panel
            // RenderTexture renderTexture = new RenderTexture((int)panel.sizeDelta.x, (int)panel.sizeDelta.y, 24);
            RenderTexture renderTexture = new RenderTexture((int)panelDimesions.x, (int)panelDimesions.y, 24);
            renderTexture.Create();

            // Set the target texture for rendering
            RenderTexture.active = renderTexture;

            // // Set the camera position and size to capture only the panel
            // camera.transform.position = new Vector3(panelTrans.position.x, panelTrans.position.y, camera.transform.position.z);
            // camera.orthographicSize = panelDimesions.y * 0.5f;

            // Calculate the aspect ratio of the custom resolution
            float customAspectRatio = panelDimesions.x / panelDimesions.y;

            // Set the camera's aspect ratio to match the custom resolution
            camera.aspect = customAspectRatio;

            // Adjust the camera's orthographic size based on the custom resolution
            // float targetOrthographicSize = panelDimesions.y / 2f;
            // camera.orthographicSize = targetOrthographicSize;
            camera.orthographicSize = 0.621f;

            // Render the panel to the texture
            camera.targetTexture = renderTexture;
            camera.Render();

            // Create a new texture and read the pixels from the RenderTexture
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.Destroy(renderTexture);

            return texture;
        }
        public static Sprite CaptureCameraToSprite(Vector2 picSize, Camera camera)
        {
            return Sprite.Create(CaptureCameraToTexture(picSize, camera), new Rect(0, 0, (int)picSize.x, (int)picSize.y), Vector2.one * 0.5f);
        }
        public static Texture2D CaptureCameraToTexture(Vector2 picSize, Camera camera)
        {
            // Create a new RenderTexture with the same size as the panel
            RenderTexture renderTexture = new RenderTexture((int)picSize.x, (int)picSize.y, 24);
            renderTexture.Create();

            // Set the target texture for rendering
            RenderTexture.active = renderTexture;

            // Render the panel to the texture
            camera.targetTexture = renderTexture;
            camera.Render();

            // Create a new texture and read the pixels from the RenderTexture
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.Destroy(renderTexture);

            return texture;
        }
    }
    public static class Rotationp
    {
        public static void RotateAroundAxis(Transform transform, Vector3 RotateAroundAxis, float angle)
        {
            transform.Rotate(RotateAroundAxis, angle);
        }
        public static IEnumerator RotateAroundAxisUpdate(Transform transform, Vector3 RotateAroundAxis, float angle, Action OnRotationComplete, float SpeedMultiplier = 1)
        {
            float originAngle = GetAngle(transform.rotation, RotateAroundAxis);
            Transform ToTrans = new GameObject().transform;
            ToTrans.rotation = transform.rotation;
            ToTrans.Rotate(RotateAroundAxis, angle);

            Quaternion fromRotation = transform.rotation;
            Quaternion toRotation = ToTrans.rotation;
            float t = 0;
            while (t < 1)
            {
                yield return new WaitForFixedUpdate();
                transform.rotation = Quaternion.Lerp(fromRotation, toRotation, t);
                t += Time.deltaTime * SpeedMultiplier;
            }
            transform.rotation = toRotation;
            OnRotationComplete?.Invoke();
        }
        public static float GetAngle(Quaternion quaternion, Vector3 axis)
        {
            if (axis.x == 1)
                return quaternion.x;
            else if (axis.y == 1)
                return quaternion.y;
            return quaternion.z;
        }
    }
    public static class TransformExtentsionp
    {
        public static IEnumerator LookAtTransformUpdate(Transform ObjectTrans, Transform LookAtTrans, float t, bool rotateOnX=false, bool rotateOnY=false, bool rotateOnZ=false)
        {
            Vector3 upDirection = Vector3.up; // Specify the up direction for rotation (can be customized)
            while(true)
            {
                // Determine the desired forward direction based on target transform and selected axes
                Vector3 targetDirection = LookAtTrans.position - ObjectTrans.position;

                // Create the rotation using Quaternion.LookRotation() with specified axes
                Quaternion targetRotation = Quaternion.LookRotation(
                    new Vector3(rotateOnX ? 0f: targetDirection.x, rotateOnY ? 0f: targetDirection.y, rotateOnZ ? 0f: targetDirection.z),
                    upDirection
                );

                // Apply the rotation to the object's transform
                ObjectTrans.rotation = Quaternion.Lerp(ObjectTrans.rotation, targetRotation, t);
                yield return new WaitForFixedUpdate();
            }
        }
        public static IEnumerator LookAtTransformUpdate(Transform ObjectTrans, Transform LookAtTrans, float t, float forSeconds, bool rotateOnX=false, bool rotateOnY=false, bool rotateOnZ=false)
        {
            Vector3 upDirection = Vector3.up; // Specify the up direction for rotation (can be customized)
            while(forSeconds < 0)
            {
                // Determine the desired forward direction based on target transform and selected axes
                Vector3 targetDirection = LookAtTrans.position - ObjectTrans.position;

                // Create the rotation using Quaternion.LookRotation() with specified axes
                Quaternion targetRotation = Quaternion.LookRotation(
                    new Vector3(rotateOnX ? 0f: targetDirection.x, rotateOnY ? 0f: targetDirection.y, rotateOnZ ? 0f: targetDirection.z),
                    upDirection
                );

                // Apply the rotation to the object's transform
                ObjectTrans.rotation = Quaternion.Lerp(ObjectTrans.rotation, targetRotation, t);
                yield return new WaitForSeconds(Time.deltaTime);
                forSeconds -= Time.deltaTime;
            }
        }
    }
    public static class General
    {
        public static IEnumerator PlayerAnimationUntilTouch(Action action, float duration)
        {
            float startTime = Time.time;
            while (Time.time - startTime < duration)
            {
                yield return new WaitForEndOfFrame();
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.isPressed)
#else
                if (Input.GetMouseButton(0))
#endif
                {
                    action?.Invoke();
                    yield break;
                }
            }
            action?.Invoke();
        }
        public static IEnumerator Tweening(Transform Trans, Vector3 Point1, Vector3 Point2, float Muiltiplier, Action action=null)
        {
            float t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Trans.position = Vector3.Lerp(Point1, Point2, t);
                yield return new WaitForEndOfFrame();
            }

            action?.Invoke();
        }
        public static Vector2 GetRandomPointInside(Vector2 pos, float offset)
        {
            GetUpperAndLowerLimits(pos, out Vector2 upperLimit, out Vector2 lowerLimit, offset);
            return GetRandomPointInside(upperLimit, lowerLimit);
        }
        public static void GetUpperAndLowerLimits(Vector2 pos, out Vector2 UpperLimit, out Vector2 LowerLimit, float offset)
        {
            UpperLimit = pos*offset;
            LowerLimit = pos/offset;
        }
        public static Vector2 GetRandomPointInside(Vector2 UpperLimit, Vector2 LowerLimit)
        {
            return new Vector2(Mathf.Lerp(UpperLimit.x, LowerLimit.x, UnityEngine.Random.value), Mathf.Lerp(UpperLimit.y, LowerLimit.y, UnityEngine.Random.value));
        }
        public static Vector3 GetRandomPointInside(Vector3 pos, float offset)
        {
            GetUpperAndLowerLimits(pos, out Vector3 upperLimit, out Vector3 lowerLimit, offset);
            return GetRandomPointInside(upperLimit, lowerLimit);
        }
        public static void GetUpperAndLowerLimits(Vector3 pos, out Vector3 UpperLimit, out Vector3 LowerLimit, float offset)
        {
            UpperLimit = pos*offset;
            LowerLimit = pos/offset;
        }
        public static Vector3 GetRandomPointInside(Vector3 UpperLimit, Vector3 LowerLimit)
        {
            return new Vector3(Mathf.Lerp(UpperLimit.x, LowerLimit.x, UnityEngine.Random.value), Mathf.Lerp(UpperLimit.y, LowerLimit.y, UnityEngine.Random.value), Mathf.Lerp(UpperLimit.z, LowerLimit.z, UnityEngine.Random.value));
        }
        public static IEnumerator AfterEndOfFrame(this MonoBehaviour mono, IEnumerator coroutine)
        {
            yield return new WaitForEndOfFrame();
            yield return mono.StartCoroutine(coroutine);
        }
        public static IEnumerator AfterEndOfFrame(Action action)
        {
            yield return new WaitForEndOfFrame();
            action?.Invoke();
        }
        public static IEnumerator AfterFixedUpdate(this MonoBehaviour mono, IEnumerator coroutine)
        {
            yield return new WaitForFixedUpdate();
            yield return mono.StartCoroutine(coroutine);
        }
        public static IEnumerator AfterFixedUpdate(Action action)
        {
            yield return new WaitForFixedUpdate();
            action?.Invoke();
        }
        public static void DestroyChildren(Transform parentObject)
        {
            for (int i = parentObject.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parentObject.GetChild(i).gameObject);
        }
        public static void DestroyChildren(Transform parentObject, int startFrom)
        {
            for (int i = parentObject.childCount - 1; i >= startFrom; i--)
                UnityEngine.Object.Destroy(parentObject.GetChild(i).gameObject);
        }
        public static void SetChildSideBySide(Transform ParentObject, Transform ChildObject)
        {
            if (ParentObject == null) return;
            Transform[] childObjects = new Transform[ChildObject.childCount];
            for (int i = 0; i < ChildObject.childCount; i++)
            {
                childObjects[i] = ChildObject.GetChild(i);
            }
            for (int i = 0; i < ChildObject.childCount; i++)
            {
                childObjects[i].parent = ParentObject.GetChild(i);
                SetChildSideBySide(ParentObject.GetChild(i), childObjects[i]);
            }
        }
        public static void SetChildSideBySide(Transform ParentObject, Transform ChildObject, bool CheckName)
        {
            if (ParentObject == null) return;
            Transform[] childObjects = new Transform[ChildObject.childCount];
            for (int i = 0; i < childObjects.Length; i++)
            {
                childObjects[i] = ChildObject.GetChild(i);
            }
            for (int i = 0; i < childObjects.Length; i++)
            {
                if (childObjects[i].name == ParentObject.GetChild(i).name)
                {
                    childObjects[i].parent = ParentObject.GetChild(i);
                    SetChildSideBySide(ParentObject.GetChild(i), childObjects[i], true);
                }
            }
        }
        public static void SetLayerRecursively(GameObject go, LayerMask layerMask)
        {
            if (go == null) return;
            foreach (Transform trans in go.GetComponentsInChildren<Transform>(true))
            {
                trans.gameObject.layer = layerMask;
            }
        }
        public static void SetLayerRecursively(GameObject go, LayerMask layerMask, string excludeWithTag) {
            if (go == null) return;
            foreach (Transform trans in go.GetComponentsInChildren<Transform>(true)) {
                if(trans.gameObject.tag != excludeWithTag)
                    trans.gameObject.layer = layerMask;
            }
        }
        public static void SetGlobalScale(Transform transform, Vector3 globalScale)
        {
            transform.localScale = Vector3.one;
            transform.localScale = new Vector3(globalScale.x / transform.lossyScale.x, globalScale.y / transform.lossyScale.y, globalScale.z / transform.lossyScale.z);
        }
        public static bool IsInsidetheBounds(Vector3 p, Vector3[] PointList)
        {
            int sides = PointList.Length;
            int j = sides - 1;
            for (int i = 0; i < sides; i++)
            {
                if (PointList[i].z < p.z && PointList[j].z >= p.z || PointList[j].z < p.z && PointList[i].z >= p.z)
                {
                    if (PointList[i].x + (p.z - PointList[i].z) / (PointList[j].z - PointList[i].z) * (PointList[j].x - PointList[i].x) < p.x)
                    {
                        return true;
                    }
                }
                j = i;
            }
            return false;
        }
        public static bool IsInsideTheBoundsprevious(Vector2 p, Vector2[] PointList)
        {
            int sides = PointList.Length;
            int j = sides - 1;
            for (int i = 0; i < sides; i++)
            {
                if (PointList[i].y < p.y && PointList[j].y >= p.y || PointList[j].y < p.y && PointList[i].y >= p.y)
                {
                    if (PointList[i].x + (p.y - PointList[i].y) / (PointList[j].y - PointList[i].y) * (PointList[j].x - PointList[i].x) < p.x)
                    {
                        return true;
                    }
                }
                j = i;
            }
            return false;
        }
        public static bool IsInsideTheBounds(Vector2 p, Vector2[] PointList)
        {
            int sides = PointList.Length;
            int j = sides - 1;
            bool inside = false;

            for (int i = 0; i < sides; i++)
            {
                if ((PointList[i].y < p.y && PointList[j].y >= p.y) || (PointList[j].y < p.y && PointList[i].y >= p.y))
                {
                    if (PointList[i].x + (p.y - PointList[i].y) / (PointList[j].y - PointList[i].y) * (PointList[j].x - PointList[i].x) < p.x)
                    {
                        inside = !inside;
                    }
                }
                j = i;
            }

            return inside;
        }
        public static Vector3 GetWorldPosFroPers(Vector3 ScreenPos, LayerMask layerMask = default, float MaxDistance = float.PositiveInfinity, bool CollideWithTrigger = true)
        {
            Ray PosRay = Camera.main.ScreenPointToRay(ScreenPos);
            Debug.DrawRay(PosRay.origin, PosRay.direction * 100, Color.red, 10);
            Vector3 HitPos = Vector3.zero;
            if (Physics.Raycast(PosRay, out RaycastHit PosRayCastHit, MaxDistance, layerMask, CollideWithTrigger ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore))
            {
                HitPos = PosRayCastHit.point;
            }
            return HitPos;
        }
        public static Vector3 GetMousePosFroOrtha(Vector3 ScreenPos, float NearClipPlaneDistanceOfCam)
        {
            ScreenPos.z = NearClipPlaneDistanceOfCam;
            return Camera.main.ScreenToWorldPoint(ScreenPos);
        }
        public static int GetIndexInList(int Index, int LengthOfList, int addnum)
        {
            int ReturnIndex = Index + addnum;
            while (!(ReturnIndex < LengthOfList) || !(ReturnIndex >= 0))
            {
                if (ReturnIndex >= LengthOfList) ReturnIndex -= LengthOfList;
                if (ReturnIndex < 0) ReturnIndex += LengthOfList;
            }
            return ReturnIndex;
        }
        public static int NearestPointInList(int IndexOfFromPoint, List<Vector3> PointsArray)
        {
            float Distance = 0;
            Vector3 FromPoint = PointsArray[IndexOfFromPoint];
            int Index = GetIndexInList(IndexOfFromPoint, PointsArray.Count, 1);
            float NearestPointDistance = Vector3.Distance(FromPoint, PointsArray[Index]);
            for (int i = 2; i < PointsArray.Count; i++)
            {
                Index = GetIndexInList(Index, PointsArray.Count, 1);
                Distance = Vector3.Distance(FromPoint, PointsArray[Index]);
                if (NearestPointDistance > Distance)
                {
                    NearestPointDistance = Distance;
                    IndexOfFromPoint = Index;
                }
            }
            return IndexOfFromPoint;
        }
        public static int NearestPointInList(Vector3 FromPoint, List<Transform> TransArray)
        {
            float Distance = 0;
            int IndexOfFromPoint = 0;
            float NearestPointDistance = Vector3.Distance(FromPoint, TransArray[IndexOfFromPoint].position);
            for (int i = 1; i < TransArray.Count; i++)
            {
                Distance = Vector3.Distance(FromPoint, TransArray[i].position);
                if (NearestPointDistance > Distance)
                {
                    NearestPointDistance = Distance;
                    IndexOfFromPoint = i;
                }
            }
            return IndexOfFromPoint;
        }
        public static int NearestPointInList(Vector3 FromPoint, List<Vector3> PointsArray)
        {
            float Distance = 0;
            int IndexOfFromPoint = 0;
            float NearestPointDistance = Vector3.Distance(FromPoint, PointsArray[IndexOfFromPoint]);
            for (int i = 1; i < PointsArray.Count; i++)
            {
                Distance = Vector3.Distance(FromPoint, PointsArray[i]);
                if (NearestPointDistance > Distance)
                {
                    NearestPointDistance = Distance;
                    IndexOfFromPoint = i;
                }
            }
            return IndexOfFromPoint;
        }
        public static int NearestPointInListFrom(int IndexOfFromPoint, List<Vector3> PointsArray)
        {
            float Distance = 0;
            Vector3 FromPos = PointsArray[IndexOfFromPoint];
            IndexOfFromPoint += 1;
            float NearestPointDistance = Vector3.Distance(FromPos, PointsArray[IndexOfFromPoint]);
            for (int i = IndexOfFromPoint + 1; i < PointsArray.Count; i++)
            {
                Distance = Vector3.Distance(FromPos, PointsArray[i]);
                if (NearestPointDistance > Distance)
                {
                    NearestPointDistance = Distance;
                    IndexOfFromPoint = i;
                }
            }
            return IndexOfFromPoint;
        }
        public static float SumOfArray(float[] floatArray)
        {
            float sum = 0;
            for (int i = 0; i < floatArray.Length; i++)
            {
                sum += floatArray[i];
            }
            return sum;
        }
        public static int BinarySearchArray(int[] arr, int key)
        {
            int minNum = 0;
            int maxNum = arr.Length - 1;
            int mid = (minNum + maxNum) / 2;
            while (minNum <= maxNum)
            {
                if (key == arr[mid])
                {
                    return mid;
                }
                else if (key < arr[mid])
                {
                    maxNum = mid - 1;
                }
                else
                {
                    minNum = mid + 1;
                }
                mid = (minNum + maxNum) / 2;
            }
            return mid;
        }
        public static int BinarySearchArrayForVectorX(Vector3[] arr, Vector3 Point)
        {
            int minNum = 0;
            int maxNum = arr.Length - 1;
            int mid = (minNum + maxNum) / 2;
            while (minNum <= maxNum)
            {
                if (Point.x == arr[mid].x)
                {
                    return ++mid;
                }
                else if (Point.x < arr[mid].x)
                {
                    maxNum = mid - 1;
                }
                else
                {
                    minNum = mid + 1;
                }
                mid = (minNum + maxNum) / 2;
            }
            return mid;
        }
        public static bool IsInsidetheBounds(Vector3 UpperLimit, Vector3 LowerLimit, Vector3 value)
        {
            if (UpperLimit.x >= value.x && UpperLimit.y >= value.y && UpperLimit.z >= value.z &&
                LowerLimit.x <= value.x && LowerLimit.y <= value.y && LowerLimit.z <= value.z) return true;
            return false;
        }
        public static Vector3 GetClosestInsideBounds(Vector3 UpperLimit, Vector3 LowerLimit, Vector3 value)
        {
            float x = Mathf.Clamp(value.x, LowerLimit.x, UpperLimit.x);
            float y = Mathf.Clamp(value.y, LowerLimit.y, UpperLimit.y);
            float z = Mathf.Clamp(value.z, LowerLimit.z, UpperLimit.z);

            return new Vector3(x, y, z);
        }
        public static Transform GetTransfromAtPos(Vector3 AtPos, Vector3 Direction, float Distance, LayerMask layerMask, string Tag)
        {
            Transform HitTrans = null;
            RaycastHit[] HittedObjects = Physics.RaycastAll(AtPos, Direction, Distance, layerMask);
            if (HittedObjects.Length != 0)
            {
                foreach (RaycastHit HittedObject in HittedObjects)
                {
                    if (HittedObject.transform.tag == Tag)
                    {
                        HitTrans = HittedObject.transform;
                    }
                }
            }
            return HitTrans;
        }
        public static Transform GetChildOfName(Transform ParenObject, string ChildName)
        {
            for (int i = 0; i < ParenObject.childCount; i++)
            {
                if (ParenObject.GetChild(i).name == ChildName)
                    return ParenObject.GetChild(i);
            }
            return null;
        }
        public static IEnumerator InvokeMethod(Action action, float delayInSeconds)
        {
            yield return new WaitForSeconds(delayInSeconds);
            action();
        }
        public static float SecondsToMultiplier(float seconds){
            return 1/seconds;
        }
        public static float MultiplierToSeconds(float multiplier){
            if (multiplier <= 0){
                Debug.Log("Multiplier must be greater than zero.");
                return 0.1f;
            }
            return 1 / multiplier;
        }
    }
    public static class UIp
    {
        public static IEnumerator TextCompTransitionNumber(TextMeshProUGUI textComp, int fromNumber, int toNumber, float Muiltiplier=1)
        {
            float t = 0;
            int number = 0;
            while (t < 1)
            {
                number = (int)Mathf.RoundToInt(Mathf.Lerp(fromNumber, toNumber, t));
                textComp.text = Mathf.Round(Mathf.Lerp(fromNumber, toNumber, t)).ToString();
                t += Time.deltaTime*Muiltiplier;
                yield return null;
            }
            textComp.text = toNumber.ToString();
        }
        public static IEnumerator TextCompTransitionString(TextMeshProUGUI textComp, string newText, string fromText=null, float letterTransitionDelay=0.1f)
        {
            if(fromText==null)
                textComp.text = fromText;
            fromText = textComp.text;

            // Ensure both texts have the same length
            int maxLength = Mathf.Max(fromText.Length, newText.Length);
            fromText = fromText.PadRight(maxLength);
            newText = newText.PadRight(maxLength);

            for (int i = 0; i < maxLength; i++)
            {
                char currentChar = newText[i];
                if (fromText[i] != currentChar)
                {
                    fromText = fromText.Substring(0, i) + currentChar + fromText.Substring(i + 1);
                    textComp.text = fromText;
                }
                yield return new WaitForSeconds(letterTransitionDelay);
            }
        }
        public static IEnumerator ShiverUpdate(float shiverDuration, float shiverMagnitude, RectTransform imageRectTransform)
        {
            Vector3 originalPosition = imageRectTransform.localPosition;
            float elapsedTime = 0.0f;
            while (elapsedTime < shiverDuration)
            {
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * shiverMagnitude;
                imageRectTransform.localPosition = originalPosition + randomOffset;

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Reset the position back to the original after the shiver effect is done
            imageRectTransform.localPosition = originalPosition;
        }
        public static Vector2 GetBoundsOfUI_Scalar(RectTransform UI)
        {
            Vector3[] corners = new Vector3[4];
            UI.GetWorldCorners(corners);

            Vector3 lowerLeft = corners[0];
            Vector3 upperRight = corners[2];

            Vector3 scaledLowerLeft = RectTransformUtility.WorldToScreenPoint(null, lowerLeft);
            Vector3 scaledUpperRight = RectTransformUtility.WorldToScreenPoint(null, upperRight);

            return new Vector2(scaledUpperRight.x - scaledLowerLeft.x, scaledUpperRight.y - scaledLowerLeft.y);
        }
        public static void GetUpperAndLowerLimit_Scalar(RectTransform UI, out Vector3 UpperLimit, out Vector3 LowerLimit)
        {
            Vector2 UIbounds = GetBoundsOfUI_Scalar(UI)/2;
            Vector2 UIpos = UI.position;
            UpperLimit = UIpos + UIbounds;
            LowerLimit = UIpos - UIbounds;
        }

        public static Vector2 GetBoundsOfUI(RectTransform UI)
        {
            GetUpperAndLowerLimit(UI, out Vector3 UpperLimit, out Vector3 LowerLimit);
            return UpperLimit - LowerLimit;
        }
        public static void GetUpperAndLowerLimit(RectTransform UI, out Vector3 UpperLimit, out Vector3 LowerLimit)
        {
            Vector3 UIpos = UI.position;
            Vector3 UIScale = new Vector3(UI.rect.width, UI.rect.height) / 2;
            UpperLimit = UIpos + UIScale;
            LowerLimit = UIpos - UIScale;
        }
        
        public static IEnumerator UIFadeInOrOutTweening(Image Object, float FromAlpha, float ToAlpha, float Muiltiplier = 1)
        {
            Color FromColor, ToColor;

            FromColor = Object.color;
            if (FromAlpha >= 0 && FromAlpha <= 1)
            {
                FromColor.a = FromAlpha;
                Object.color = FromColor;
            }
            ToColor = Object.color;
            ToColor.a = ToAlpha;
            Object.gameObject.SetActive(true);

            float t = 0;
            while (t <= 1)
            {
                Object.color = Color.Lerp(FromColor, ToColor, t);
                t += Time.deltaTime * Muiltiplier;
                yield return new WaitForEndOfFrame();
            }
            Object.color = ToColor;
        }
        public static IEnumerator UIFadeInOrOutTweening(Text Object, float FromAlpha, float ToAlpha, float Muiltiplier = 1)
        {
            Color FromColor, ToColor;

            FromColor = Object.color;
            if (FromAlpha >= 0 && FromAlpha <= 1)
            {
                FromColor.a = FromAlpha;
                Object.color = FromColor;
            }
            ToColor = Object.color;
            ToColor.a = ToAlpha;
            Object.gameObject.SetActive(true);

            float t = 0;
            while (t <= 1)
            {
                Object.color = Color.Lerp(FromColor, ToColor, t);

                t += Time.deltaTime * Muiltiplier;

                yield return new WaitForEndOfFrame();
            }
            Object.color = ToColor;
        }
        public static IEnumerator UIFadeInOrOutTweeningRectMask(RectMask2D Object, int FromSoftness, int ToSoftness, float Muiltiplier = 1, Action onCompleted=null, bool ToggoleActivation = true, axis Axis = axis.xy)
        {
            if (ToggoleActivation)
                Object.gameObject.SetActive(FromSoftness>ToSoftness);
            float t = 0;
            int currentSoftnessValue = 0;
            Vector2Int currentSoftness = Object.softness;

            Action softness = null;
            switch (Axis)
            {
                case axis.x: softness = ()=> currentSoftness.x = currentSoftnessValue;
                break;
                case axis.y: softness = ()=> currentSoftness.y = currentSoftnessValue;
                break;
                default: softness = ()=> {currentSoftness.x = currentSoftnessValue;currentSoftness.y = currentSoftnessValue;};
                break;
            }
            while (t <= 1)
            {
                currentSoftnessValue = (int)Mathf.Lerp(FromSoftness, ToSoftness, t);
                softness();
                Object.softness = currentSoftness;
                t += Time.deltaTime * Muiltiplier;
                yield return new WaitForEndOfFrame();
            }
            currentSoftness.x = ToSoftness;
            currentSoftness.y = ToSoftness;
            Object.softness = currentSoftness;

            onCompleted?.Invoke();
        }
        public static IEnumerator UIFadeFromSideTweeningRectMaskSetDefaultBefore(RectMask2D rectMask2D, Direction direction, bool IsEnabled, float Muiltiplier = 1, Action onCompleted=null)
        {
            yield return UIFadeFromSideTweeningRectMask(rectMask2D, direction, !IsEnabled, -1);
            yield return UIFadeFromSideTweeningRectMask(rectMask2D, direction, IsEnabled, Muiltiplier, onCompleted);
        }
        public static IEnumerator UIFadeFromSideTweeningRectMask(RectMask2D rectMask2D, Direction direction, bool IsEnabled, float Muiltiplier = 1, Action onCompleted=null)
        {
            // Fill area
            Vector4 fromPadding = rectMask2D.padding;
            Vector4 toPadding = new Vector4();
            Vector2 fillRectBound = GetBoundsOfUI_Scalar(rectMask2D.GetComponent<RectTransform>())*2;
            switch(direction)
            {
                case Direction.FromRightToLeft:
                toPadding.x = IsEnabled? 0: fillRectBound.x; // Left
                break;
                case Direction.FromLeftToRight:
                toPadding.z = IsEnabled? 0: fillRectBound.x; // Right
                break;
                case Direction.FromBottomToTop:
                toPadding.w = IsEnabled? 0: fillRectBound.y; // Top
                break;
                case Direction.FromTopToBottom:
                toPadding.y = IsEnabled? 0: fillRectBound.y; // Bottom
                break;
            }
            Debug.Log($"{fromPadding} {toPadding} {fillRectBound}");
            float t = 0;
            if(Muiltiplier>0)
                while(t<1)
                {
                    t += Time.deltaTime * Muiltiplier;
                    rectMask2D.padding = Vector4.Lerp(fromPadding, toPadding, t);
                    yield return new WaitForEndOfFrame();
                }
            rectMask2D.padding = toPadding;
            onCompleted?.Invoke();
        }
        
        public static IEnumerator UIScaleTweening(RectTransform Object, Vector3 FromScale, Vector3 MiddleScale, Vector3 ToScale, bool DisableOnScaleComplete = false, float Muiltiplier = 1, Action action = null)
        {
            Object.gameObject.SetActive(true);
            float t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.localScale = Vector2.Lerp(FromScale, MiddleScale, t);

                yield return new WaitForEndOfFrame();
            }
            t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.localScale = Vector2.Lerp(MiddleScale, ToScale, t);

                yield return new WaitForEndOfFrame();
            }
            Object.gameObject.SetActive(!DisableOnScaleComplete);
            action?.Invoke();
        }
        public static IEnumerator UIScaleTweening(RectTransform Object, bool ScaleUp, float Muiltiplier=1, Action action = null)
        {
            Vector2 FromScale;
            Vector2 ToScale;

            if (ScaleUp)
            {
                Object.gameObject.SetActive(true);
                FromScale = Vector2.zero;
                ToScale = new Vector2(1, 1);
                Object.localScale = Vector2.zero;
            }
            else
            {
                FromScale = Object.localScale;
                ToScale = Vector2.zero;
            }

            Object.gameObject.SetActive(true);
            float t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.localScale = Vector2.Lerp(FromScale, ToScale, t);

                yield return new WaitForEndOfFrame();
            }

            if (!ScaleUp) Object.gameObject.SetActive(false);
            action?.Invoke();
        }
        public static IEnumerator UIScaleTweeningSizeDelta(RectTransform Object, Vector3 ToScale, bool DisableOnScaleComplete = false, float Muiltiplier = 1, Action action = null)
        {
            Vector3 FromScale = Object.sizeDelta;
            Object.gameObject.SetActive(true);
            float t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.sizeDelta = Vector2.Lerp(FromScale, ToScale, t);

                yield return new WaitForEndOfFrame();
            }
            Object.gameObject.SetActive(!DisableOnScaleComplete);
            action?.Invoke();
        }
        public static IEnumerator UIScaleTweeningSizeDelta(RectTransform Object, Vector3 FromScale, Vector3 ToScale, bool DisableOnScaleComplete = false, float Muiltiplier = 1)
        {
            Object.gameObject.SetActive(true);
            float t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.sizeDelta = Vector2.Lerp(FromScale, ToScale, t);

                yield return new WaitForEndOfFrame();
            }
            Object.gameObject.SetActive(!DisableOnScaleComplete);
        }
        public static IEnumerator UIScaleTweeningSizeDelta(RectTransform Object, Vector3 FromScale, Vector3 MiddleScale, Vector3 ToScale, bool DisableOnScaleComplete = false, float Muiltiplier = 1, Action action = null)
        {
            Object.gameObject.SetActive(true);
            float t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.sizeDelta = Vector2.Lerp(FromScale, MiddleScale, t);

                yield return new WaitForEndOfFrame();
            }
            t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.sizeDelta = Vector2.Lerp(MiddleScale, ToScale, t);

                yield return new WaitForEndOfFrame();
            }
            Object.gameObject.SetActive(!DisableOnScaleComplete);
            action?.Invoke();
        }
        public static IEnumerator UIScaleTweeningSizeDelta(MonoBehaviour mono, RectTransform Object, Vector3 FromScale, Vector3 MiddleScale, Vector3 ToScale, bool DisableOnScaleComplete = false, float Muiltiplier = 1, IEnumerator corotine = null)
        {
            Object.gameObject.SetActive(true);
            float t = 0;
            Object.sizeDelta = FromScale;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                try{
                    Object.sizeDelta = Vector2.Lerp(FromScale, MiddleScale, t);
                }
                catch{
                    break;
                }
                yield return new WaitForEndOfFrame();
            }
            t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                try
                {
                    Object.sizeDelta = Vector2.Lerp(MiddleScale, ToScale, t);
                }
                catch{
                    break;
                }
                yield return new WaitForEndOfFrame();
            }
            try{
                Object.gameObject.SetActive(!DisableOnScaleComplete);
            }
            catch{
                Debug.Log($"UIScaleTweeningSizeDelta gameobject missing");
            }
            if(corotine!=null)
                yield return mono.StartCoroutine(corotine);
        }
        public static IEnumerator UIScaleTweening(MonoBehaviour mono, RectTransform Object, Vector3 FromScale, Vector3 MiddleScale, Vector3 ToScale, bool DisableOnScaleComplete = false, float Muiltiplier = 1, IEnumerator corotine = null)
        {
            Object.gameObject.SetActive(true);
            float t = 0;
            Object.localScale = FromScale;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.localScale = Vector2.Lerp(FromScale, MiddleScale, t);

                yield return new WaitForEndOfFrame();
            }
            t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.localScale = Vector2.Lerp(MiddleScale, ToScale, t);

                yield return new WaitForEndOfFrame();
            }
            Object.gameObject.SetActive(!DisableOnScaleComplete);
            if(corotine!=null)
                yield return mono.StartCoroutine(corotine);
        }
        public static IEnumerator UIScaleTweening(RectTransform Object, Vector3 FromScale, Vector3 ToScale, bool DisableOnScaleComplete = true, float Muiltiplier = 1, Action callback = null)
        {
            Object.gameObject.SetActive(true);
            float t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.transform.localScale = Vector2.Lerp(FromScale, ToScale, t);

                yield return new WaitForEndOfFrame();
            }
            if(DisableOnScaleComplete)
                Object.gameObject.SetActive(false);
            callback?.Invoke();
        }
        public static IEnumerator UIScaleTweeningFlicker(RectTransform Object, Vector3 FromScale, Vector3 ToScale, bool DisableOnScaleComplete = true, float duration = 1)
        {
            Object.gameObject.SetActive(true);

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                float t = elapsedTime / duration;
                Object.transform.localScale = Vector2.Lerp(FromScale, ToScale, t);

                yield return UIScaleTweening(Object, FromScale, ToScale, DisableOnScaleComplete, 40);
                yield return UIScaleTweening(Object, ToScale, FromScale, DisableOnScaleComplete, 40);

                elapsedTime += Time.deltaTime;
                yield return null;
            }
            Object.transform.localScale = ToScale;
            Object.gameObject.SetActive(!DisableOnScaleComplete);
        }
        
        public static Coroutine UITweeningInsideScreenViewFrom(MonoBehaviour monoBehaviour, RectTransform Object, RectTransform ObjectParent, Vector2 FromDirection, float Muiltiplier, Vector2? ToPoint = null, Action action=null, Vector2? FormPos=null, bool ReachToPos=true)
        {
            Vector2 parentSize = GetBoundsOfUI_Scalar(ObjectParent);
            Vector2 objectSize = GetBoundsOfUI_Scalar(Object);

            parentSize.x *= FromDirection.x;
            parentSize.y *= FromDirection.y;

            objectSize.x *= FromDirection.x;
            objectSize.y *= FromDirection.y;

            Vector2 FromPoint;
            if(FormPos == null){
                FromPoint = ObjectParent.position;

                FromPoint.x += parentSize.x/2; 
                FromPoint.y += parentSize.y/2;

                FromPoint.x += objectSize.x/2;
                FromPoint.y += objectSize.y/2;
            }
            else FromPoint = (Vector2)FormPos;
            Object.position = FromPoint;
            Object.gameObject.SetActive(true);

            if(ToPoint == null) ToPoint = ObjectParent.position;
            if(ReachToPos)
                return monoBehaviour.StartCoroutine(UITweening(Object, FromPoint, (Vector2)ToPoint, Muiltiplier, action));
            return null;
        }
        public static Coroutine UITweeningOutsideScreenViewFrom(MonoBehaviour monoBehaviour, RectTransform Object, RectTransform ObjectParent, Vector2 FromDirection, float Muiltiplier, Vector2? FromPoint = null, Action action=null, bool ReachToPos=true, bool SetInactiveOnEnd=true)
        {
            Vector2 parentSize = GetBoundsOfUI_Scalar(ObjectParent);
            Vector2 objectSize = GetBoundsOfUI_Scalar(Object);

            parentSize.x *= FromDirection.x;
            parentSize.y *= FromDirection.y;

            objectSize.x *= FromDirection.x;
            objectSize.y *= FromDirection.y;

            Vector2 ToPoint = ObjectParent.position;
            ToPoint.x += parentSize.x/2;
            ToPoint.y += parentSize.y/2;

            ToPoint.x += objectSize.x/2;
            ToPoint.y += objectSize.y/2;

            // Object.position = ToPoint;

            if(SetInactiveOnEnd)
                action += ()=> Object.gameObject.SetActive(false);

            Object.gameObject.SetActive(true);
            if(FromPoint == null) FromPoint = ObjectParent.position;
            if(ReachToPos)
                return monoBehaviour.StartCoroutine(UITweening(Object, (Vector2)FromPoint, ToPoint, Muiltiplier, action));
            return null;
        }
        public static IEnumerator UITweening(MonoBehaviour mono, RectTransform Object, Vector3 FromPos, Vector3 MiddlePos, Vector3 ToPos, bool DisableOnScaleComplete = false, float Muiltiplier = 1, IEnumerator corotine = null)
        {
            Object.gameObject.SetActive(true);
            float t = 0;
            Object.position = FromPos;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.position = Vector2.Lerp(FromPos, MiddlePos, t);

                yield return new WaitForEndOfFrame();
            }
            t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.position = Vector2.Lerp(MiddlePos, ToPos, t);

                yield return new WaitForEndOfFrame();
            }
            Object.gameObject.SetActive(!DisableOnScaleComplete);
            if(corotine!=null)
                yield return mono.StartCoroutine(corotine);
        }
        public static IEnumerator UITweening(RectTransform Object, Vector2 Point1, Vector2 Point2, float Muiltiplier=1, Action action = null)
        {
            yield return UITweening(Object.transform, Point1, Point2, Muiltiplier, action);
        }
        public static IEnumerator UITweening(Transform Object, Vector2 Point1, Vector2 Point2, float Muiltiplier=1, Action action = null)
        {
            float t = 0;
            while (t <= 1)
            {
                t += Time.deltaTime * Muiltiplier;
                Object.position = Vector2.Lerp(Point1, Point2, t);

                yield return new WaitForEndOfFrame();
            }

            action?.Invoke();
            yield break;
        }
        public static IEnumerator UITextReplacement(TextMeshProUGUI textComponent, string toText, float multiplier=1, Action action = null)
        {
            string originalText = textComponent.text;
            int originalTextLength = originalText.Length;
            int newTextLength = toText.Length;
            int currentCharacterIndex = 0;

            // Remove existing text character by character
            while (currentCharacterIndex < originalTextLength)
            {
                string currentText = originalText.Substring(0, originalTextLength - currentCharacterIndex);
                textComponent.text = currentText;

                yield return new WaitForSecondsRealtime(Time.deltaTime * multiplier);

                currentCharacterIndex++;
            }

            // Set the text to the new text and gradually add characters
            currentCharacterIndex = 0;
            while (currentCharacterIndex < newTextLength)
            {
                string currentText = toText.Substring(0, currentCharacterIndex + 1);
                textComponent.text = currentText;

                yield return new WaitForSecondsRealtime(Time.deltaTime * multiplier);

                currentCharacterIndex++;
            }

            action?.Invoke();
        }
        public static IEnumerator UISliderTransition(Slider sliderComponent, float toValue, float multiplier=1, Action action = null)
        {
            float defaultValue = sliderComponent.value;
            float t = 0;
            while (t <= 1)
            {
                sliderComponent.value = Mathf.Lerp(defaultValue, toValue, t);
                t += Time.deltaTime * multiplier;
                yield return new WaitForEndOfFrame();
            }
            action?.Invoke();
        }

        public static IEnumerator UIColorTweening(Image Object, Color FromColor, Color ToColor, float Muiltiplier = 1)
        {
            float t = 0;
            while (t <= 1)
            {
                Object.color = Color.Lerp(FromColor, ToColor, t);
                t += Time.deltaTime * Muiltiplier;
                yield return new WaitForEndOfFrame();
            }
            Object.color = ToColor;
        }

        // work on this
        public static Coroutine UITweeningObjectFromOutsideToInside(MonoBehaviour monoBehaviour, RectTransform Object, RectTransform ObjectParent, Vector2 FromDirection, float Muiltiplier, Vector2? ToPoint = null, Action action=null, Vector2? FormPos=null, bool ReachToPos=true)
        {
            Vector2 parentSize = GetBoundsOfUI_Scalar(ObjectParent);
            Vector2 objectSize = GetBoundsOfUI_Scalar(Object);

            parentSize.x *= FromDirection.x;
            parentSize.y *= FromDirection.y;

            objectSize.x *= FromDirection.x;
            objectSize.y *= FromDirection.y;

            Vector2 FromPoint;
            if(FormPos == null)
                FromPoint = ObjectParent.position;
            else FromPoint = (Vector2)FormPos;
            FromPoint.x += parentSize.x/2; 
            FromPoint.y += parentSize.y/2;

            FromPoint.x += objectSize.x/2;
            FromPoint.y += objectSize.y/2;

            Object.position = FromPoint;

            if(ToPoint == null) ToPoint = ObjectParent.position;
            if(ReachToPos)
                return monoBehaviour.StartCoroutine(UITweening(Object, FromPoint, (Vector2)ToPoint, Muiltiplier, action));
            return null;
        }
    }
    public static class Mathp
    {
        // Function to find the intersection point of two lines
        public static Vector2 FindIntersectionPoint(Vector2 lineAPointA, Vector2 lineAPointB, Vector2 lineBPointC)
        {
            // Calculate slopes and y-intercepts of the two lines
            float slopeA = (lineAPointB.y - lineAPointA.y) / (lineAPointB.x - lineAPointA.x);
            float yInterceptA = lineAPointA.y - slopeA * lineAPointA.x;
            float slopeB = -1 / slopeA;  // The slope of the perpendicular line
            float yInterceptB = lineBPointC.y - slopeB * lineBPointC.x;

            // Calculate intersection point of the two lines
            float x = (yInterceptB - yInterceptA) / (slopeA - slopeB);
            float y = slopeA * x + yInterceptA;

            return new Vector2(x, y);
        }
        // Function to find the intersection point of two lines
        public static Vector3 FindIntersectionPoint(Vector3 lineAPointA, Vector3 lineAPointB, Vector3 lineBPointC)
        {
            // Convert 3D points to 2D points by ignoring the y-axis
            Vector2 lineAPointA2D = new Vector2(lineAPointA.x, lineAPointA.z);
            Vector2 lineAPointB2D = new Vector2(lineAPointB.x, lineAPointB.z);
            Vector2 lineBPointC2D = new Vector2(lineBPointC.x, lineBPointC.z);

            // Calculate slopes and y-intercepts of the two lines
            float slopeA = (lineAPointB2D.y - lineAPointA2D.y) / (lineAPointB2D.x - lineAPointA2D.x);
            float yInterceptA = lineAPointA2D.y - slopeA * lineAPointA2D.x;
            float slopeB = -1 / slopeA;  // The slope of the perpendicular line
            float yInterceptB = lineBPointC2D.y - slopeB * lineBPointC2D.x;

            // Calculate intersection point of the two lines
            float x = (yInterceptB - yInterceptA) / (slopeA - slopeB);
            float z = slopeA * x + yInterceptA;

            // Return the intersection point as a 3D point
            return new Vector3(x, lineAPointA.y, z);
        }
        public static float AngleBetweenTwoPoints(Vector2 P1, Vector2 P2)
        {
            return Mathf.Atan2(P2.y - P1.y, P2.x - P1.x) * 180 / Mathf.PI;
        }
        public static float Slop(Vector3 Point1, Vector3 Point2)
        {
            return (Point2.y - Point1.y) / (Point2.x - Point1.x);
        }
        public static bool ccw(Vector2 A, Vector2 B, Vector2 C)
        {
            return (C.y - A.y) * (B.x - A.x) > (B.y - A.y) * (C.x - A.x);
        }
        public static bool LineIntersect(Vector2 A, Vector2 B, Vector2 C, Vector2 D)
        {
            return ccw(A, C, D) != ccw(B, C, D) && ccw(A, B, C) != ccw(A, B, D);
        }
        public static double[] SolveQuadratic(double a, double b, double c)
        {
            double d, x1 = 0, x2 = 0;

            d = b * b - 4 * a * c;
            if (d == 0)
            {
                // Both roots are equal
                x1 = -b / (2.0 * a);
                x2 = x1;
                // First  Root Root1= x1
                // Second Root Root2= x2
            }
            else if (d > 0)
            {
                // Both roots are real and diff-2\n

                x1 = (-b + Math.Sqrt(d)) / (2 * a);
                x2 = (-b - Math.Sqrt(d)) / (2 * a);

                // First  Root Root1= x1
                // Second Root root2= x2
            }
            else
                Debug.Log("Root are imeainary that is why there is No Solution");

            return new double[] { x1, x2 };
        }
        // Find out the intersecting points on the curve
        public static List<Vector3> calcQuadraticLineartersects(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 a1, Vector3 a2)
        {
            List<Vector3> intersections = new List<Vector3>();

            // inverse line normal
            Vector3 normal = new Vector3(a1.y - a2.y, a2.x - a1.x);

            // Q-coefficients
            Vector3 c2 = new Vector3(p1.x + p2.x * -2 + p3.x, p1.y + p2.y * -2 + p3.y);

            Vector3 c1 = new Vector3(p1.x * -2 + p2.x * 2, p1.y * -2 + p2.y * 2);

            Vector3 c0 = new Vector3(p1.x, p1.y);

            // Transform to line 
            var coefficient = a1.x * a2.y - a2.x * a1.y;
            var a = normal.x * c2.x + normal.y * c2.y;
            var b = (normal.x * c1.x + normal.y * c1.y) / a;
            var c = (normal.x * c0.x + normal.y * c0.y + coefficient) / a;

            // solve the roots
            double[] roots = new double[1];
            double d = b * b - 4 * c;
            if (d > 0)
            {
                var e = Math.Sqrt(d);
                roots = new double[2];
                roots[0] = ((-b + Math.Sqrt(d)) / 2);
                roots[1] = ((-b - Math.Sqrt(d)) / 2);
            }
            else if (d == 0)
            {
                // roots.Add(-b/2);
                roots[0] = -b / 2;
            }

            // calc the solution points
            for (int i = 0; i < roots.Length; i++)
            {
                float minX = Math.Min(a1.x, a2.x);
                float minY = Math.Min(a1.y, a2.y);
                float maxX = Math.Max(a1.x, a2.x);
                float maxY = Math.Max(a1.y, a2.y);
                float t = (float)roots[i];
                if (t >= 0 && t <= 1)
                {
                    // possible point -- pending bounds check
                    Vector3 point = new Vector3(Mathf.Lerp(Mathf.Lerp(p1.x, p2.x, t), Mathf.Lerp(p2.x, p3.x, t), t), Mathf.Lerp(Mathf.Lerp(p1.y, p2.y, t), Mathf.Lerp(p2.y, p3.y, t), t));
                    float x = point.x;
                    float y = point.y;
                    // bounds checks
                    if (a1.x == a2.x && y >= minY && y <= maxY)
                    {
                        // vertical line
                        intersections.Add(point);
                    }
                    else if (a1.y == a2.y && x >= minX && x <= maxX)
                    {
                        // horizontal line
                        intersections.Add(point);
                    }
                    else if (x >= minX && y >= minY && x <= maxX && y <= maxY)
                    {
                        // line passed bounds check
                        intersections.Add(point);
                    }
                }
            }
            return intersections;
        }
        public static float DistancePointToLineSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            float l2 = (b - a).sqrMagnitude;    // i.e. |b-a|^2 -  avoid a sqrt
            if (l2 == 0.0)
                return (p - a).magnitude;       // a == b case
            float t = Vector2.Dot(p - a, b - a) / l2;
            if (t < 0.0)
                return (p - a).magnitude;       // Beyond the 'a' end of the segment
            if (t > 1.0)
                return (p - b).magnitude;         // Beyond the 'b' end of the segment
            Vector2 projection = a + t * (b - a); // Projection falls on the segment
            return (p - projection).magnitude;
        }
        public static Vector3 EvaluateQuadratic(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            Vector3 p0 = Vector3.Lerp(a, b, t);
            Vector3 p1 = Vector3.Lerp(b, c, t);
            return Vector3.Lerp(p0, p1, t);
        }
        public static Vector3 EvaluateCubic(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            Vector3 p0 = EvaluateQuadratic(a, b, c, t);
            Vector3 p1 = EvaluateQuadratic(b, c, d, t);
            return Vector3.Lerp(p0, p1, t);
        }
        public static Vector3 Multiplication(Vector3 a, Vector3 b)
        {
            a.x *= b.x;
            a.y *= b.y;
            a.z *= b.z;
            return a;
        }
        public static float[,] DotProductOfMatric(float[,] M_A, float[,] M_B)
        {
            int rowM_A = M_A.GetLength(0);
            int columbM_A = M_A.GetLength(1);
            int rowM_B = M_B.GetLength(0);
            int columbM_B = M_B.GetLength(1);

            float[,] product = new float[rowM_A, columbM_B];

            for (int i = 0; i < product.GetLength(0); i++)
            {
                for (int j = 0; j < product.GetLength(1); j++)
                {
                    product[i, j] = SumOfMultiplyRowByColumb(M_A, M_B, i, j);
                }
            }
            return product;
        }
        public static float SumOfMultiplyRowByColumb(float[,] A, float[,] B, int row, int Columb)
        {
            float sum = 0;
            for (int i = 0; i < A.GetLength(1); i++)
            {
                sum += A[row, i] * B[i, Columb];
            }
            return sum;
        }
        public static Vector3 InverseVector(Vector3 ToInverse)
        {
            float[,] A = new float[,]
            {
                { -1, 0, 0},
                { 0, -1, 0},
                { 0, 0, -1}
            };
            float[,] B = new float[,]
            {
                { ToInverse.x},
                { ToInverse.y},
                { ToInverse.z}
            };
            float[,] dotProduct = Mathp.DotProductOfMatric(A, B);
            return new Vector3(dotProduct[0, 0], dotProduct[1, 0], dotProduct[2, 0]).normalized;
        }
    }
    public static class ExtensionMethods
    {
        public static Vector2 ToXY(this Vector3 v3)
        {
            return new Vector2(v3.x, v3.z);
        }
        public static Vector3 ToXZ(this Vector2 v2)
        {
            return new Vector3(v2.x, 0, v2.y);
        }
        public static Vector3 SwitchYZ(this Vector3 v3)
        {
            return new Vector3(v3.x, v3.z, v3.y);
        }
        public static float ToVelocity(this Vector3 v3)
        {
            return Mathf.Sqrt((v3.x * v3.x) + (v3.y * v3.y) + (v3.z * v3.z));
        }
    }

    public enum axis{
        x,
        y,
        z,
        xy,
        yz,
        xz,
        xyz
    }
    public enum Direction{
        FromTopToBottom,
        FromBottomToTop,
        FromRightToLeft,
        FromLeftToRight
    }
    public enum Sides{
        Bottom,
        Top,
        Left,
        Right,
        Middle
    }
}