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

            m_Time += m_Speed * Time.deltaTime / m_Length;
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

            transform.position = pos;
            if (m_IsApplyRotation)
            {
                transform.LookAt(pos + tan, up);
            }
        }
    }

}