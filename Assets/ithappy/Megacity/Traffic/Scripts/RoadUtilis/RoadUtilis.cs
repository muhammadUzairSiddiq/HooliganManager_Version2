namespace ITHappy
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Splines;

    public static class RoadUtilis
    {
        public static void ParseSplinesToCurves(Spline[] splines, List<TrafficManager.SplineData> curves, Transform transform, int crossIndex = -1)
        {
            int curveIterator = 0;
            foreach (var spline in splines)
            {
                for (int i = 0; i < spline.GetCurveCount(); i++, curveIterator++)
                {
                    var curve = spline.GetCurve(i);
                    var length = spline.GetCurveLength(i);
                    var outputs = new MathExtension.Int3(-1, -1, -1);

                    var startUp = transform.TransformDirection(spline.GetCurveUpVector(i, 0f)).normalized;
                    var endUp = transform.TransformDirection(spline.GetCurveUpVector(i, 1f)).normalized;

                    curves.Add(new(outputs, transform.TransformPoint(curve.P0), transform.TransformPoint(curve.P1),
                        transform.TransformPoint(curve.P2), transform.TransformPoint(curve.P3), startUp, endUp, crossIndex, length));
                }
            }
        }

        public static void OptimizeCurveList(List<TrafficManager.SplineData> curves) {

            Debug.Log("Was" + curves.Count);

            for (int i = 0; i < curves.Count / 2; i++) {

                for(int j = i + 1; j < curves.Count;) {
                    if (isOnLine(curves[i], curves[j])) {
                        var curve = new TrafficManager.SplineData(new MathExtension.Int3(-1, -1, -1), 
                            curves[i].p0, curves[i].p2, curves[j].p1, curves[j].p3,
                            curves[i].startNormal, curves[j].endNormal, curves[j].length + curves[i].length);

                        curves[i] = curve;
                        curves.RemoveAt(j);
                        continue;
                    }

                    j++;
                }
            }

            Debug.Log("Now" + curves.Count);

            bool isOnLine(TrafficManager.SplineData a, TrafficManager.SplineData b) {

                if (a.crossIndex >= 0 || b.crossIndex >= 0) return false;
                if (Vector3.SqrMagnitude(a.p3 - b.p0) > 0.09f) return false;

                if (Vector3.Dot(b.startNormal, a.endNormal) < 0.98f) return false;
                if (Vector3.Dot(a.startNormal, b.endNormal) < 0.98f) return false;

                var vec = Vector3.Normalize(a.p1 - a.p0);

                var vec1 = Vector3.Normalize(a.p2 - a.p0);
                if (Vector3.Dot(vec, vec1) < 0.98f) return false;

                vec1 = Vector3.Normalize(a.p3 - a.p0);
                if (Vector3.Dot(vec, vec1) < 0.98f) return false;

                vec1 = Vector3.Normalize(b.p0 - a.p0);
                if (Vector3.Dot(vec, vec1) < 0.98f) return false;

                vec1 = Vector3.Normalize(b.p1 - a.p0);
                if (Vector3.Dot(vec, vec1) < 0.98f) return false;

                vec1 = Vector3.Normalize(b.p2 - a.p0);
                if (Vector3.Dot(vec, vec1) < 0.98f) return false;

                vec1 = Vector3.Normalize(b.p3 - a.p0);
                if (Vector3.Dot(vec, vec1) < 0.98f) return false;

                return true;
            }
        }

        public static void GetCurvesCount(Spline[] splines, out int count)
        {
            count = 0;
            foreach (var spline in splines)
            {
                count += spline.GetCurveCount();
            }
        }

        public static void ConnectSplines(TrafficManager.SplineData[] curves, int count, float mergeDistance)
        {
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    if (Vector3.SqrMagnitude(curves[i].p3 - curves[j].p0) < mergeDistance)
                    {
                        curves[j].p0 = curves[i].p3;

                        if (curves[i].outputs.x < 0)
                        {
                            curves[i].outputs.x = j;
                        }
                        else
                        {
                            if (curves[i].outputs.y < 0)
                            {
                                curves[i].outputs.y = j;
                            }
                            else if (curves[i].outputs.z < 0)
                            {
                                curves[i].outputs.z = j;
                            }
                        }
                    }
                }
            }
        }
    }
}