namespace ITHappy
{
    using UnityEngine;
    using UnityEngine.Splines;

    public class SplineAnimator : MonoBehaviour
    {
        [SerializeField]
        private SplineContainer m_Container;

        [SerializeField]
        private bool m_IsApplyRotation = true;
        [SerializeField, Range(0, 1)]
        private float m_Time;
        [SerializeField]
        private int m_Spline;

        private void OnValidate()
        {
            if (!m_Container)
                return;

            m_Spline = Mathf.Clamp(m_Spline, 0, m_Container.Splines.Count);
            EvaluateTransform();
        }

        private void Update()
        {
            if (!m_Container)
                return;

            EvaluateTransform();
        }

        private void EvaluateTransform()
        {
            m_Container.Evaluate(m_Spline, m_Time, out var pos, out var tan, out var up);

            transform.position = pos;
            if (m_IsApplyRotation)
            {
                transform.up = up;
                transform.forward = tan;
            }
        }
    }
}