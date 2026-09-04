using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace Pastoral.UI
{
    [AddComponentMenu("UI/Effects/DropShadow", 80)]
    public class DropShadow : BaseMeshEffect
    {
        public enum GradientType { Linear, Radial }

        [SerializeField]
        private Color m_EffectColor = new Color(0f, 0f, 0f, 0.5f);

        [SerializeField]
        private Vector2 m_EffectDistance = new Vector2(1f, -1f);

        [SerializeField]
        private Vector2 m_EffectSize = new Vector2(1f, 1f);

        [SerializeField]
        private Vector2 m_RadialPositionOffset = Vector2.zero;

        [SerializeField]
        private bool m_UseGraphicAlpha = true;

        [SerializeField]
        private GradientType m_GradientType = GradientType.Linear;

        [SerializeField]
        private Gradient m_Gradient = new Gradient();

        private const float kMaxEffectDistance = 600f;
        private const float kMaxEffectSize = 600f;

        protected DropShadow() { }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            effectDistance = m_EffectDistance;
            effectSize = m_EffectSize;
            radialPositionOffset = m_RadialPositionOffset;
            base.OnValidate();
        }
#endif

        public Color effectColor
        {
            get { return m_EffectColor; }
            set
            {
                m_EffectColor = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public Vector2 effectDistance
        {
            get { return m_EffectDistance; }
            set
            {
                m_EffectDistance = ClampValue(value, kMaxEffectDistance);
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public Vector2 effectSize
        {
            get { return m_EffectSize; }
            set
            {
                m_EffectSize = ClampValue(value, kMaxEffectSize);
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public Vector2 radialPositionOffset
        {
            get { return m_RadialPositionOffset; }
            set
            {
                m_RadialPositionOffset = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public bool useGraphicAlpha
        {
            get { return m_UseGraphicAlpha; }
            set
            {
                m_UseGraphicAlpha = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public GradientType gradientType
        {
            get { return m_GradientType; }
            set
            {
                m_GradientType = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public Gradient gradient
        {
            get { return m_Gradient; }
            set
            {
                m_Gradient = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        private Vector2 ClampValue(Vector2 value, float maxValue)
        {
            value.x = Mathf.Clamp(value.x, -maxValue, maxValue);
            value.y = Mathf.Clamp(value.y, -maxValue, maxValue);
            return value;
        }

        protected void ApplyShadow(List<UIVertex> verts, Color32 color, int start, int end, float x, float y, Vector2 size)
        {
            UIVertex vt;
            for (int i = start; i < end; ++i)
            {
                vt = verts[i];
                verts.Add(vt);

                Vector3 v = vt.position;
                v.x += x;
                v.y += y;

                var gradientColor = CalculateGradient(vt.position, size);
                vt.position = v * size;
                vt.color = gradientColor;
                verts[i] = vt;
            }
        }
        private UIVertex InterpolateVertex(UIVertex v1, UIVertex v2)
        {
            UIVertex mid = new UIVertex();
            mid.position = (v1.position + v2.position) / 2f;
            mid.uv0 = (v1.uv0 + v2.uv0) / 2f;
            mid.normal = (v1.normal + v2.normal).normalized;
            mid.color = CalculateGradient(mid.position, effectSize);
            return mid;
        }


        private Color32 CalculateGradient(Vector3 position, Vector2 size)
        {
            switch (m_GradientType)
            {
                case GradientType.Radial:
                    Vector2 time = new Vector2();
                    time.x = Mathf.InverseLerp(-size.x, size.x, position.x);
                    time.y = Mathf.InverseLerp(-size.y, size.y, position.y);
                    float _linearPosition = time.magnitude;
                    return m_Gradient.Evaluate(_linearPosition);
                case GradientType.Linear:
                default:
                    float linearPosition = Mathf.InverseLerp(-size.x, size.x, position.x);
                    return m_Gradient.Evaluate(linearPosition);
            }
        }
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            var output = ListPool<UIVertex>.Get();
            vh.GetUIVertexStream(output);

            ApplyShadow(output, effectColor, 0, output.Count, effectDistance.x, effectDistance.y, effectSize);
            vh.Clear();
            vh.AddUIVertexTriangleStream(output);
            ListPool<UIVertex>.Release(output);
        }
    }
}
