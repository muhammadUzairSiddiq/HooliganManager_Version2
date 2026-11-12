using System.Collections.Generic;
using UnityEngine;

/// <summary>Upright freestanding barrier shared by the stadium gates and Matchday road crews.</summary>
internal static class CordonBarrier
{
    static Mesh mesh;
    static Material[] materials;

    public static Transform Create(Transform parent, Vector3 offset)
    {
        if (!mesh) BuildMesh();
        var barrier = new GameObject("Upright 3D road barricade", typeof(MeshFilter), typeof(MeshRenderer));
        barrier.transform.SetParent(parent, false);
        barrier.transform.localPosition = offset;
        barrier.GetComponent<MeshFilter>().sharedMesh = mesh;
        barrier.GetComponent<MeshRenderer>().sharedMaterials = materials;
        return barrier.transform;
    }

    static void BuildMesh()
    {
        var vertices = new List<Vector3>();
        var colors = new[] { new List<int>(), new List<int>(), new List<int>() };
        var corners = new[]
        {
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(1,1,-1), new Vector3(-1,1,-1),
            new Vector3(-1,-1,1), new Vector3(1,-1,1), new Vector3(1,1,1), new Vector3(-1,1,1)
        };
        int[] faces = {0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4};
        void Box(Vector3 center, Vector3 size, int material)
        {
            int start = vertices.Count;
            foreach (var corner in corners) vertices.Add(center + Vector3.Scale(corner, size * .5f));
            foreach (int index in faces) colors[material].Add(start + index);
        }
        foreach (int side in new[] {-1, 1})
        {
            Box(new Vector3(side * 1.35f, .06f, 0), new Vector3(.4f, .12f, 1.05f), 0);
            Box(new Vector3(side * 1.35f, .64f, 0), new Vector3(.14f, 1.16f, .16f), 0);
        }
        for (int row = 0; row < 2; row++)
        for (int band = 0; band < 7; band++)
            Box(new Vector3((band - 3) * .47f, .56f + row * .48f, 0),
                new Vector3(.471f, .24f, .16f), band % 2 == 0 ? 1 : 2);
        mesh = new Mesh { name = "Striped crowd and road barrier", subMeshCount = colors.Length };
        mesh.SetVertices(vertices);
        for (int i = 0; i < colors.Length; i++) mesh.SetTriangles(colors[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var surface = new Material[3];
        var keys = new[] { "RubberBase", "SafetyOrange", "ReflectiveWhite" };
        for (int i = 0; i < keys.Length; i++) surface[i] = Resources.Load<Material>("CityIncidents/" + keys[i]);
        materials = surface;
    }
}
