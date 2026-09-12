using UnityEngine;

namespace ITHappy
{
    [RequireComponent(typeof(MeshRenderer))]
    public class Wheel : MonoBehaviour, ICarPart
    {
        [SerializeField]
        private bool m_Inverse;
        [SerializeField]
        private float m_Radius = 0.45f;

        private MeshRenderer m_Renderer;
        private bool m_RenderToggle;

        private void Awake() {
            m_Renderer = GetComponent<MeshRenderer>();
            m_RenderToggle = m_Renderer.enabled;
        }

        public void Move(float delta, bool isRender)
        {
            if(m_RenderToggle != isRender) {
                m_Renderer.enabled = isRender;
                m_RenderToggle = isRender;
            }
            
            if(!m_RenderToggle) {
                return;
            }

            var angularDelta = delta / m_Radius * Mathf.Rad2Deg;
            var euler = Vector3.right * angularDelta;

            if(m_Inverse)
            {
                transform.Rotate(-euler, Space.Self);
            }
            else
            {
                transform.Rotate(euler, Space.Self);
            }
        }
    }
}