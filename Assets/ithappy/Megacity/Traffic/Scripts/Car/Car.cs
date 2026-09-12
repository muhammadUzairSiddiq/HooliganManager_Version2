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
            m_Transform.position = position;
            m_Transform.LookAt(position + forward, up);

            foreach(var part in m_Parts)
            {
                part.Move(deltaTime * m_Speed, isRender);
            }
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