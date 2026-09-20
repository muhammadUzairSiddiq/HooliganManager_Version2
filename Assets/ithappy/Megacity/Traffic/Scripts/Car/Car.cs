using UnityEngine;

namespace ITHappy
{
    public class Car : MonoBehaviour
    {
        [SerializeField]
        private float m_MaxSpeed = 10f;
        [SerializeField]
        private float m_AccelerationSpeed = 25f;

        private Transform m_Transform;
        private ICarPart[] m_Parts;

        private int m_Index;
        private float m_Progress;
        private int m_TurnIndex;

        private float m_Speed = 10f;
        public float Speed => m_Speed;

        private void OnValidate()
        {
            m_MaxSpeed = Mathf.Max(m_MaxSpeed, 0f);
            m_AccelerationSpeed = Mathf.Max(m_AccelerationSpeed, 0f);
        }

        public void Init(Transform transform, int index, float progress)
        {
            m_Transform = transform;
            m_Index = index;
            m_Progress = progress;

            m_Parts = GetComponentsInChildren<ICarPart>();
        }

        public void GetTrafficStructs(out TrafficManager.CarData data, out TrafficManager.CarTransport transport, out TrafficManager.CarTransform transform)
        {
            data = new(m_MaxSpeed, m_AccelerationSpeed);
            transport = new TrafficManager.CarTransport(m_Index, m_Progress, m_Speed);
            transform = new TrafficManager.CarTransform(m_Transform.position, m_Transform.forward, m_Transform.up);
        }

        public void SetTransform(ref Vector3 position, ref Vector3 forward, ref Vector3 up, bool isRender, float deltaTime)
        {
            if (!m_Transform) return;
            m_FromPosition = m_Transform.position;
            m_FromRotation = m_Transform.rotation;
            m_TargetPosition = position;
            m_TargetRotation = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward, up) : m_FromRotation;
            m_InterpolationSeconds = Mathf.Clamp(deltaTime, .016f, .25f);
            m_SampleTime = Time.time;
            m_RenderWheels = isRender;
            if (!m_HasSample || (position - m_FromPosition).sqrMagnitude > 2500)
            {
                m_FromPosition = position; m_FromRotation = m_TargetRotation;
                m_Transform.SetPositionAndRotation(position, m_TargetRotation);
            }
            m_HasSample = true;
        }
        Vector3 m_FromPosition, m_TargetPosition;
        Quaternion m_FromRotation, m_TargetRotation;
        float m_SampleTime, m_InterpolationSeconds;
        bool m_HasSample, m_RenderWheels;
        void LateUpdate()
        {
            if (!m_HasSample || !m_Transform || Time.deltaTime <= 0) return;
            float t = Mathf.Clamp01((Time.time - m_SampleTime) / m_InterpolationSeconds);
            m_Transform.SetPositionAndRotation(Vector3.Lerp(m_FromPosition, m_TargetPosition, t), Quaternion.Slerp(m_FromRotation, m_TargetRotation, t));
            foreach (var part in m_Parts) part.Move(Time.deltaTime * m_Speed, m_RenderWheels);
        }

        public void SetTransport(int index, int turnIndex, float speed, float progress)
        {
            m_Index = index;
            m_TurnIndex = turnIndex;
            m_Speed = speed;
            m_Progress = progress;
        }
    }
}