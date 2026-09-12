namespace ITHappy
{
    using System;
    using UnityEngine;

    public static class MathExtension
    {
        public static int GetClosestWithMult(int number, int mult)
        {
            int output = number / mult * mult;
            if (number % mult > 0)
            {
                output += mult;
            }

            return output;
        }

        public static Vector3 EvaluateCuibic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;

            return
                p0 * (-1f * t3 + 3f * t2 - 3f * t + 1f) +
                p1 * (3f * t3 - 6f * t2 + 3f * t) +
                p2 * (-3f * t3 + 3f * t2) +
                p3 * (t3);
        }

        public static float GetCubicLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples = 10)
        {
            float length = 0f;
            Vector3 lastSample = p0;
            Vector3 sample;
            float t;

            for (int i = 0; i < samples; i++)
            {
                t = i / (float)(samples - 1);

                sample = EvaluateCuibic(p0, p1, p2, p3, t);
                length += Vector3.Distance(sample, lastSample);
                lastSample = sample;
            }

            return length;
        }

        [Serializable]
        public struct Int3
        {
            public int x;
            public int y;
            public int z;

            public static int Size => sizeof(int) * 3;

            public Int3(int x, int y, int z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }
    }
}