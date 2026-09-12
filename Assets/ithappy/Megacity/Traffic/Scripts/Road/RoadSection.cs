using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace ITHappy
{
    [RequireComponent(typeof(SplineContainer))]
    public class RoadSection : MonoBehaviour
    {
        [SerializeField]
        private RoadCross m_Cross;
        [SerializeField]
        private int m_CrossOrder;

        public void GetSection(out RoadCross cross, out int crossOrder, out Spline[] splines)
        {
            var container = GetComponent<SplineContainer>();

            cross = m_Cross;
            crossOrder = m_CrossOrder;
            splines = container.Splines.ToArray();
        }
    }
}