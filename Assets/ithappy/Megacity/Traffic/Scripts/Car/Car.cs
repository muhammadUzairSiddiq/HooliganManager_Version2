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
        Renderer[] streamRenderers;
        bool streamVisible=true;

        private int m_Index=-1;
        private float m_Progress;
        private int m_TurnIndex;

        private float m_Speed = 10f;
        public float Speed => m_Speed;

        private void OnEnable() => CacheParts();
        void CacheParts()
        {
            // Interface arrays are not restored by Unity's script hot reload.
            // Rebuild on the main thread without resetting the live road sample.
            m_Parts=GetComponentsInChildren<ICarPart>(true);
            streamRenderers=GetComponentsInChildren<Renderer>(true);
            visibilityDirty=true;
        }
        bool visibilityDirty;

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

            m_HasSample=false;
            CacheParts();
        }

        public void GetTrafficStructs(out TrafficManager.CarData data, out TrafficManager.CarTransport transport, out TrafficManager.CarTransform transform)
        {
            if(!m_Transform||!m_Transform.gameObject.activeInHierarchy||!isActiveAndEnabled||m_Index<0)
            {
                data=TrafficManager.CarData.Empty;transport=TrafficManager.CarTransport.Empty;transform=TrafficManager.CarTransform.Empty;return;
            }
            data = new(m_MaxSpeed, m_AccelerationSpeed);
            transport = new TrafficManager.CarTransport(m_Index, m_Progress, m_Speed);
            transform = new TrafficManager.CarTransform(m_Transform.position, m_Transform.forward, m_Transform.up);
        }

        public void SetTransform(ref Vector3 position, ref Vector3 forward, ref Vector3 up, bool isRender, float deltaTime)
        {
            if (!m_Transform||!m_Transform.gameObject.activeInHierarchy||!isActiveAndEnabled||m_Index<0) return;
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
            if (!isActiveAndEnabled||!m_HasSample || !m_Transform || !m_Transform.gameObject.activeInHierarchy || m_Index<0 || Time.deltaTime <= 0) return;
            if(m_Parts==null||streamRenderers==null)CacheParts();
            bool visible=!CityActivityStreaming.Instance||CityActivityStreaming.Interested(m_TargetPosition,true);
            if(visibilityDirty||visible!=streamVisible){visibilityDirty=false;streamVisible=visible;foreach(var renderer in streamRenderers)if(renderer)renderer.enabled=visible;}
            if(!visible){m_Transform.SetPositionAndRotation(m_TargetPosition,m_TargetRotation);return;}
            float t = Mathf.Clamp01((Time.time - m_SampleTime) / m_InterpolationSeconds);
            m_Transform.SetPositionAndRotation(Vector3.Lerp(m_FromPosition, m_TargetPosition, t), Quaternion.Slerp(m_FromRotation, m_TargetRotation, t));
            foreach (var part in m_Parts)
            {
                if(part==null)continue;
                // Unity's destroyed-object null check is lost when held through an interface.
                if(part is Component component&&(!component||!component.gameObject.activeInHierarchy))continue;
                if(part is Behaviour behaviour&&!behaviour.isActiveAndEnabled)continue;
                part.Move(Time.deltaTime * m_Speed, m_RenderWheels);
            }
        }

        public void SetTransport(int index, int turnIndex, float speed, float progress)
        {
            m_Index = index;
            m_TurnIndex = turnIndex;
            m_Speed = index<0?0f:Mathf.Max(0f,speed);
            m_Progress = progress;
            if(index<0)m_HasSample=false;
        }
    }
}
