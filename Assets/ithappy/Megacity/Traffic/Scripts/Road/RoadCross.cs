using UnityEngine;

namespace ITHappy
{
    public class RoadCross : MonoBehaviour
    {
        [SerializeField]
        private float m_GreenTime = 20f;
        [SerializeField]
        private float m_YellowTime = 20f;

        public void GetInfo(out float green ,out float yellow)
        {
            green = m_GreenTime;
            yellow = m_YellowTime;
        }
    }
}