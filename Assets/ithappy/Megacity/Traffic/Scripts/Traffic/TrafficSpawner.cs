using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace ITHappy
{
    public class TrafficSpawner : MonoBehaviour
    {
        private const int k_MaxCarCount = 2048;

        [SerializeField]
        private List<GameObject> m_CarPrefabs = new();
        [SerializeField]
        private float m_SpawnDistance = 5f;
        [SerializeField]
        private Transform m_CarRoot;
        [SerializeField, Range(0f, 1f)]
        private float m_SpawnProbability = 0.5f;

        private Transform m_Transform;

        private List<Car> m_Cars = new();

        private void Awake()
        {
            m_Transform = transform;
        }

        public void Spawn(ref NativeArray<TrafficManager.SplineData> splines)
        {
            if(m_CarPrefabs.Count == 0 || m_Cars.Count >= k_MaxCarCount)
            {
                return;
            }

            for(int i = 0; i < splines.Length; i++)
            {
                var length = splines[i].length;
                if (length <= m_SpawnDistance || splines[i].crossIndex >= 0)
                {
                    continue;
                }

                var offset = 0f;

                while (offset <= length - m_SpawnDistance)
                {
                    float random = Random.Range(0f, 1f);
                    if (random < m_SpawnProbability)
                    {
                        var prefIndex = Random.Range(0, m_CarPrefabs.Count);
                        var carTransform = Instantiate(m_CarPrefabs[prefIndex]).transform;

                        carTransform.name = $"Car_{m_Cars.Count}";
                        carTransform.SetParent((m_CarRoot == null) ? m_Transform : m_CarRoot);

                        var car = carTransform.GetComponent<Car>();

                        if (car != null)
                        {
                            car.Init(carTransform, i, offset / length);
                            m_Cars.Add(car);
                            if(m_Cars.Count >= k_MaxCarCount)
                            {
                                return;
                            }
                        }
                    }

                    offset += m_SpawnDistance;
                }
            }
        }

        public void GetArrayReferences(ref NativeArray<TrafficManager.CarData> data, 
            ref NativeArray<TrafficManager.CarTransport> transport, ref NativeArray<TrafficManager.CarTransform> transform)
        {
            var size = Mathf.Max(MathExtension.GetClosestWithMult(m_Cars.Count, 4), 4);

            data = new(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            transport = new(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            transform = new(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for(int i = 0; i < m_Cars.Count; i++)
            {
                m_Cars[i].GetTrafficStructs(out var carData, out var carTransport, out var carTransform);

                data[i] = carData;
                transport[i] = carTransport;
                transform[i] = carTransform;
            }
            for(int i = m_Cars.Count; i < size; i++)
            {
                data[i] = TrafficManager.CarData.Empty;
                transport[i] = TrafficManager.CarTransport.Empty;
                transform[i] = TrafficManager.CarTransform.Empty;
            }
        }

        public void ApplyBuffers(ref NativeArray<TrafficManager.CarTransform> transforms, ref NativeArray<TrafficManager.CarTransport> transports, float deltaTime)
        {
            for(int i = 0; i < m_Cars.Count; i++)
            {
                var transf = transforms[i];
                var car = m_Cars[i];

                car.SetTransform(ref transf.position, ref transf.forward, ref transf.up, TrafficManager.Instance.RenderRange(ref transf.position), deltaTime);
                car.SetTransport(transports[i].index, transports[i].turnIndex, transports[i].speed, transports[i].progress);
            }
        }

        public void ApplyTransforms(ref NativeArray<TrafficManager.CarTransform> transforms, float deltaTime)
        {
            for (int i = 0; i < m_Cars.Count; i++)
            {
                var transf = transforms[i];
                m_Cars[i].SetTransform(ref transf.position, ref transf.forward, ref transf.up, TrafficManager.Instance.RenderRange(ref transf.position), deltaTime);
            }
        }

        public void ApplyTransports(ref NativeArray<TrafficManager.CarTransport> transports)
        {
            for (int i = 0; i < m_Cars.Count; i++)
            {
                m_Cars[i].SetTransport(transports[i].index, transports[i].turnIndex, transports[i].speed, transports[i].progress);
            }
        }
    }
}