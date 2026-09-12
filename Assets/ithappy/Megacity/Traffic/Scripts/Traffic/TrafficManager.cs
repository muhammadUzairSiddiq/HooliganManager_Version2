using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ITHappy
{
    [RequireComponent(typeof(TrafficSpawner))]
    [RequireComponent(typeof(TrafficPath))]
    public class TrafficManager : Singleton<TrafficManager>
    {
        private int m_KernelIndex = 0;

        private const int k_GroupSizeX = 32;

        [SerializeField]
        private ComputeShader m_Compute;
        [SerializeField]
        private bool m_IsRunning;
        [SerializeField]
        private float m_CarSpacing = 2f;

        [SerializeField]
        private float m_WheelRenderDistance = 200f;
        [SerializeField]
        private Transform m_Spectator;

        private TrafficSpawner m_Spawner;
        private TrafficPath m_Road;

        private int m_ThreadsSideX;
        private bool m_IsValid;

        private NativeArray<SplineData>[] m_SplineData;

        private NativeArray<CarData> m_CarData;
        private NativeArray<CrossData> m_CrossData;

        private NativeArray<CarTransport> m_CarTransport;
        private NativeArray<CarTransform> m_CarTransform;
        private NativeArray<CrossState> m_CrossState;

        private ComputeBuffer m_RCarsData;

        private ComputeBuffer[] m_RSplinesData;

        private ComputeBuffer m_RCrossData;

        private ComputeBuffer m_RWCarsTransport;
        private ComputeBuffer m_RWCarsTransform;
        private ComputeBuffer m_RWCrossState;

        private GPUCallback m_GPUCallback;
        private float m_MovedTime;

        public bool RenderRange(ref Vector3 position) {
            if (m_Spectator == null) {
                return true;
            }

            return Vector3.SqrMagnitude(position - m_Spectator.position) < m_WheelRenderDistance * m_WheelRenderDistance;
        }

        protected override void Awake()
        {
            m_Spawner = GetComponent<TrafficSpawner>();
            m_Road = GetComponent<TrafficPath>();

            base.Awake();
        }

        private void Start()
        {
            if (m_Compute == null)
            {
                m_IsValid = false;
                return;
            }

            m_Road.GetArrayReferences(out m_SplineData, out m_CrossData, out m_CrossState);

            for(int i = 0; i < 2; i++)
            {
                m_Spawner.Spawn(ref m_SplineData[i]);
            }

            m_Spawner.GetArrayReferences(ref m_CarData, ref m_CarTransport, ref m_CarTransform);

            InitBuffers();

            SetUpStaticBuffers();
            SetUpDynamicBuffers();
            SetUpCompute();

            m_IsValid = true;
            Move();
        }

        protected override void OnDestroy()
        {
            m_IsRunning = false;
            base.OnDestroy();
            DisposeAll();
        }

        private void Move()
        {
            if (!m_IsValid || !m_IsRunning)
            {
                return;
            }

            var time = Time.time;
            m_Compute.SetFloat("Rf_DeltaTime", time - m_MovedTime);

            m_MovedTime = time;
            m_Compute.Dispatch(m_KernelIndex, m_ThreadsSideX / k_GroupSizeX, 1, 1);
            RequestCallbacks();
        }

        private void ApplyBuffersData()
        {
            if(m_GPUCallback.transport == 1 && m_GPUCallback.transform == 1)
            {
                m_Spawner.ApplyBuffers(ref m_CarTransform, ref m_CarTransport, Time.time - m_MovedTime);
                return;
            }
            if(m_GPUCallback.transport == 1 &&  m_GPUCallback.transform == -1)
            {
                m_Spawner.ApplyTransports(ref m_CarTransport);
                return;
            }
            if(m_GPUCallback.transport == -1 && m_GPUCallback.transform == 1)
            {
                m_Spawner.ApplyTransforms(ref m_CarTransform, Time.time - m_MovedTime);
                return;
            }
        }

        #region GPU Callback
        private void RequestCallbacks()
        {
            _ = AsyncGPUReadback.Request(m_RWCarsTransport, OnTransportCallback);
            _ = AsyncGPUReadback.Request(m_RWCarsTransform, OnTransformCallback);

            m_GPUCallback = new GPUCallback(0, 0);
        }

        private void OnTransportCallback(AsyncGPUReadbackRequest request)
        {
            if (!m_IsRunning)
            {
                return;
            }
            
            if (request.hasError)
            {
                m_GPUCallback.transport = -1;
            }
            else if (request.done)
            {
                m_CarTransport.CopyFrom(request.GetData<CarTransport>());
                m_GPUCallback.transport = 1;
            }

            CheckCallback();
        }

        private void OnTransformCallback(AsyncGPUReadbackRequest request)
        {
            if (!m_IsRunning)
            {
                return;
            }

            if (request.hasError)
            {
                m_GPUCallback.transform = -1;
            }
            else if (request.done)
            {
                m_CarTransform.CopyFrom(request.GetData<CarTransform>());
                m_GPUCallback.transform = 1;
            }

            CheckCallback();
        }

        private void CheckCallback()
        {
            if(m_GPUCallback.transport > 0 && m_GPUCallback.transform != 0)
            {
                ApplyBuffersData();
                Move();
            }
        }

        private struct GPUCallback
        {
            public int transport;
            public int transform;

            public GPUCallback(int transport, int transform)
            {
                this.transport = transport;
                this.transform = transform;
            }
        }
        #endregion


        #region Compute 
        private void InitBuffers()
        {
            m_RCarsData = new ComputeBuffer(m_CarData.Length, CarData.Size, ComputeBufferType.Structured);

            m_RSplinesData = new ComputeBuffer[m_SplineData.Length];
            for (int i = 0; i < m_SplineData.Length; i++)
            {
                m_RSplinesData[i] = new ComputeBuffer(m_SplineData[i].Length, SplineData.Size, ComputeBufferType.Structured);
            }

            m_RCrossData = new ComputeBuffer(m_CrossData.Length, CrossData.Size, ComputeBufferType.Structured);

            m_RWCarsTransport = new ComputeBuffer(m_CarTransport.Length, CarTransport.Size, ComputeBufferType.Structured);
            m_RWCarsTransform = new ComputeBuffer(m_CarTransform.Length, CarTransform.Size, ComputeBufferType.Structured);
            m_RWCrossState = new ComputeBuffer(m_CrossState.Length, CrossState.Size, ComputeBufferType.Structured);
        }

        private void SetUpStaticBuffers()
        {
            m_RCarsData.SetData(m_CarData);

            for (int i = 0; i < m_SplineData.Length; i++)
            {
                m_RSplinesData[i].SetData(m_SplineData[i]);
            }

            m_RCrossData.SetData(m_CrossData);
        }

        private void SetUpDynamicBuffers()
        {
            m_RWCarsTransport.SetData(m_CarTransport);
            m_RWCarsTransform.SetData(m_CarTransform);
            m_RWCrossState.SetData(m_CrossState);
        }

        private void SetUpCompute()
        {
            m_Compute.SetBuffer(m_KernelIndex, "Rb_CarData", m_RCarsData);

            m_Compute.SetBuffer(m_KernelIndex, "Rb_SplineData_0", m_RSplinesData[0]);
            m_Compute.SetBuffer(m_KernelIndex, "Rb_SplineData_2048", m_RSplinesData[1]);

            m_Compute.SetBuffer(m_KernelIndex, "Rb_CrossroadData", m_RCrossData);

            m_Compute.SetBuffer(m_KernelIndex, "RWb_CarTransport", m_RWCarsTransport);
            m_Compute.SetBuffer(m_KernelIndex, "RWb_CarTransform", m_RWCarsTransform);
            m_Compute.SetBuffer(m_KernelIndex, "RWb_CrossroadState", m_RWCrossState);

            m_ThreadsSideX = MathExtension.GetClosestWithMult(m_CarData.Length + m_CrossData.Length, 8);
            m_ThreadsSideX = Mathf.Max(m_ThreadsSideX, k_GroupSizeX);

            m_Compute.SetInt("Ri_SideResolution", m_ThreadsSideX);
            m_Compute.SetFloat("Rf_CarSpacing", m_CarSpacing);

            m_Compute.SetInt("Ri_CarCount", m_CarData.Length);
            m_Compute.SetInt("Ri_CrossCount", m_CrossData.Length);

            m_KernelIndex = m_Compute.FindKernel("k_MoveTraffic");
        }

        private void DisposeAll()
        {
            DisposeBuffers();
            DisposeArrays();
        }

        private void DisposeBuffers()
        {
            Dispose(m_RCarsData);

            foreach(var buffer in m_RSplinesData)
            {
                Dispose(buffer);
            }

            Dispose(m_RCrossData);

            Dispose(m_RWCarsTransport);
            Dispose(m_RWCarsTransform);
            Dispose(m_RWCrossState);
        }

        private void DisposeArrays()
        {
            foreach (var buffer in m_SplineData)
            {
                Dispose(buffer);
            }

            Dispose(m_CarData);
            Dispose(m_CrossData);

            Dispose(m_CarTransport);
            Dispose(m_CarTransform);
            Dispose(m_CrossState);
        }

        private void Dispose(ComputeBuffer buffer)
        {
            if(buffer != null && buffer.IsValid())
            {
                buffer.Dispose();
            }
        }

        private void Dispose<T>(NativeArray<T> array) where T : struct
        {
            if(array != null && array.IsCreated)
            {
                array.Dispose();
            }
        }
        #endregion


        #region BufferStructs
        public struct CarData
        {
            public float speed;
            public float acceleration;

            public static int Size => sizeof(float) * 2;
            public static CarData Empty => new(0f, 0f);

            public CarData(float speed, float acceleration)
            {
                this.speed = speed;
                this.acceleration = acceleration;
            }
        }

        public struct CarTransport
        {
            public int index;
            public int turnIndex;

            public float progress;
            public float speed;

            public static int Size => sizeof(int) * 2 + sizeof(float) * 2;
            public static CarTransport Empty => new(-1, 0f, 0f);

            public CarTransport(int index, float progress, float speed)
            {
                this.index = index;
                this.progress = progress;
                this.speed = speed;

                turnIndex = -2;
            }
        }

        public struct CarTransform
        {
            public Vector3 position;
            public Vector3 forward;
            public Vector3 up;

            public static int Size => sizeof(float) * 9;
            public static CarTransform Empty => new(Vector3.zero, Vector3.forward, Vector3.up);

            public CarTransform(Vector3 position, Vector3 forward, Vector3 up)
            {
                this.position = position;
                this.forward = forward;
                this.up = up;
            }
        }

        [Serializable]
        public struct SplineData
        {
            public MathExtension.Int3 outputs;

            public float length;

            public Vector3 p0;
            public Vector3 p1;
            public Vector3 p2;
            public Vector3 p3;

            public Vector3 startNormal;
            public Vector3 endNormal;

            public int crossIndex;

            public static int Size => MathExtension.Int3.Size + sizeof(float) * 19 + sizeof(int);
            public static SplineData Empty => new(new MathExtension.Int3(-1, -1, -1), Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.up, Vector3.up, 0f);

            public SplineData(MathExtension.Int3 outputs, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 startNormal, Vector3 endNormal)
            {
                this.outputs = outputs;

                this.p0 = p0;
                this.p1 = p1;
                this.p2 = p2;
                this.p3 = p3;

                this.startNormal = startNormal;
                this.endNormal = endNormal;

                this.length = MathExtension.GetCubicLength(p0, p1, p2, p3);
                this.crossIndex = -1;
            }

            public SplineData(MathExtension.Int3 outputs, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 startNormal, Vector3 endNormal, int crossIndex)
            {
                this.outputs = outputs;

                this.p0 = p0;
                this.p1 = p1;
                this.p2 = p2;
                this.p3 = p3;

                this.startNormal = startNormal;
                this.endNormal = endNormal;

                this.length = MathExtension.GetCubicLength(p0, p1, p2, p3);
                this.crossIndex = crossIndex;
            }

            public SplineData(MathExtension.Int3 outputs, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 startNormal, Vector3 endNormal, float length)
            {
                this.outputs = outputs;

                this.p0 = p0;
                this.p1 = p1;
                this.p2 = p2;
                this.p3 = p3;

                this.startNormal = startNormal;
                this.endNormal = endNormal;

                this.length = length;
                this.crossIndex = -1;
            }

            public SplineData(MathExtension.Int3 outputs, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 startNormal, Vector3 endNormal, int crossIndex, float length)
            {
                this.outputs = outputs;

                this.p0 = p0;
                this.p1 = p1;
                this.p2 = p2;
                this.p3 = p3;

                this.startNormal = startNormal;
                this.endNormal = endNormal;

                this.length = length;
                this.crossIndex = crossIndex;
            }
        }

        [Serializable]
        public struct CrossData
        {
            public Vector2 timing;

            public static int Size => sizeof(float) * 2;
            public static CrossData Empty => new(1000f, 1000f);

            public CrossData(float greenTime, float yellowTime)
            {
                this.timing = new Vector2(greenTime, yellowTime);
            }
        }

        public struct CrossState
        {
            public float time;
            public int activity;

            public static int Size => sizeof(float) + sizeof(int);
            public static CrossState Empty => new(0f, -1);

            public CrossState(float time)
            {
                this.time = time;
                this.activity = 0;
            }

            public CrossState(float time, int activity)
            {
                this.time = time;
                this.activity = activity;
            }
        }
        #endregion
    }
}