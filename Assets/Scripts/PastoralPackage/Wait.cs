using System.Collections;
using UnityEngine;

namespace Pastoral{
    public static class Wait{
        // public static IEnumerator WaitAccordingToFPS(float delay)
        // {
        //     float start = Time.realtimeSinceStartup;
        //     while (Time.realtimeSinceStartup < start + delay)
        //         yield return null;
        // }
        public static IEnumerator WaitAccordingToFPS(float delay)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < start + delay)
                yield return new WaitForFixedUpdate();
        }
    }
}
