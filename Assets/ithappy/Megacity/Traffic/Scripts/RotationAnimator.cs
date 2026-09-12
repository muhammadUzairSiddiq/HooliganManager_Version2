namespace ITHappy
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class RotationAnimator : MonoBehaviour
    {
        [SerializeField]
        private float m_AngularSpeed = 0.5f;
        [SerializeField]
        private Vector3 m_Axis = Vector3.up;

        private void Update()
        {
            transform.Rotate(m_Axis, m_AngularSpeed, Space.Self);
        }
    }
}