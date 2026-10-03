namespace ITHappy
{
    using UnityEngine;
    using UnityEngine.Splines;

    public class SplineSpeed : MonoBehaviour
    {
        [SerializeField]
        private SplineContainer m_Container;

        [SerializeField]
        private bool m_IsApplyRotation = true;
        [SerializeField]
        private bool m_IsLoop = true;
        [SerializeField]
        private float m_Offset;
        [SerializeField]
        private float m_Speed = 1f;
        [SerializeField]
        private int m_Spline;

        private float m_Time;
        private float m_Length;

        private int m_LastIndex;
        public float RuntimeSpeedMultiplier { get; set; } = 1f;
        public bool StationStop { get; set; }
        public float RuntimeHeightOffset { get; set; }
        public Vector3 NearestRoutePoint(Vector3 point)
        {
            Vector3 best=transform.position;float distance=float.MaxValue;
            if(!m_Container)return best;
            for(int i=0;i<=256;i++)
            {
                m_Container.Evaluate(m_Spline,i/256f,out var position,out var tangent,out var up);
                Vector3 p=(Vector3)position+Vector3.up*RuntimeHeightOffset;float d=(p-point).sqrMagnitude;
                if(d<distance){best=p;distance=d;}
            }
            return best;
        }

        private void Awake()
        {
            if (!m_Container)
                return;

            m_Length = CalculateLength();
            m_Time = Mathf.Clamp01(m_Offset / m_Length);
        }

        private void Update()
        {
            if (!m_Container)
                return;

            if (m_LastIndex != m_Spline)
                m_Length = CalculateLength();

            if (m_Length <= .001f || StationStop) return;
            m_Time += m_Speed * Mathf.Clamp(RuntimeSpeedMultiplier,0f,1f) * Time.deltaTime / m_Length;
            m_Time = m_IsLoop ? m_Time - Mathf.Floor(m_Time) : Mathf.Clamp01(m_Time);
            EvaluateTransform();
        }

        private float CalculateLength()
        {
            return m_Container.Splines[m_Spline].GetLength();
        }

        private void EvaluateTransform()
        {
            m_Container.Evaluate(m_Spline, m_Time, out var pos, out var tan, out var up);

            transform.position = (Vector3)pos+Vector3.up*RuntimeHeightOffset;
            if (m_IsApplyRotation)
            {
                transform.LookAt((Vector3)(pos + tan)+Vector3.up*RuntimeHeightOffset, up);
            }
        }
    }

}
