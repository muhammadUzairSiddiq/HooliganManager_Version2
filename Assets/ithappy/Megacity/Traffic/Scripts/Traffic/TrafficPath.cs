using System;
using Unity.Collections;
using UnityEngine;

namespace ITHappy
{
    public class TrafficPath : MonoBehaviour
    {
        [SerializeField]
        private SplineConnector m_Road;

        public void GetArrayReferences(out NativeArray<TrafficManager.SplineData>[] splineData, 
            out NativeArray<TrafficManager.CrossData> crossData, out NativeArray<TrafficManager.CrossState> crossState)
        {
            splineData = new NativeArray<TrafficManager.SplineData>[2];

            for(int i = 0; i < 2; i++)
            {
                var arr = new TrafficManager.SplineData[2048];
                Array.Copy(m_Road.Splines, 2048 * i, arr, 0, 2048);

                splineData[i] = new(arr, Allocator.Persistent);
                if (splineData == null || splineData.Length == 0)
                {
                    Debug.LogError("null road is being used");
                    throw new Exception();
                }
            }

            crossData = new(m_Road.Crosses, Allocator.Persistent);
            crossState = new(crossData.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for(int i = 0; i < crossData.Length; i++)
            {
                crossState[i] = new(crossData[i].timing.y, 0);
            }
        }
    }
}