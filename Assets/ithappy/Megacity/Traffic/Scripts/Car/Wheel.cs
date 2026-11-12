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
            CacheRenderer();
        }
        private void OnEnable() => CacheRenderer();
        bool CacheRenderer() {
            m_Renderer = GetComponent<MeshRenderer>();
            if(!m_Renderer)return false;
            m_RenderToggle = m_Renderer.enabled;return true;
        }

        public void Move(float delta, bool isRender)
        {
            if(!isActiveAndEnabled||!gameObject.activeInHierarchy||(!m_Renderer&&!CacheRenderer()))return;
            if(m_RenderToggle != isRender) {
                m_Renderer.enabled = isRender;
                m_RenderToggle = isRender;
            }
            
            if(!m_RenderToggle) {
                return;
            }

            var angularDelta = delta / Mathf.Max(.001f,m_Radius) * Mathf.Rad2Deg;
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
