using UnityEngine;
using System.Collections.Generic;

public static class IcosphereGenerator
{
    public static Mesh Create(float radius = 0.5f, int subdivisions = 0)
    {
        Mesh mesh = new Mesh();

        // 12 vertices of an icosahedron
        var t = (1f + Mathf.Sqrt(5f)) / 2f;
        var vertices = new List<Vector3>
        {
            new(-1,  t,  0), new( 1,  t,  0), new(-1, -t,  0), new( 1, -t,  0),
            new( 0, -1,  t), new( 0,  1,  t), new( 0, -1, -t), new( 0,  1, -t),
            new( t,  0, -1), new( t,  0,  1), new(-t,  0, -1), new(-t,  0,  1)
        };

        var faces = new List<int[]>
        {
            new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
            new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
            new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1}
        };

        // Normalize vertices
        for (int i = 0; i < vertices.Count; i++)
            vertices[i] = vertices[i].normalized * radius;

        // Build mesh triangles
        List<Vector3> meshVerts = new();
        List<int> meshTris = new();

        foreach (var face in faces)
        {
            meshVerts.Add(vertices[face[0]]);
            meshVerts.Add(vertices[face[1]]);
            meshVerts.Add(vertices[face[2]]);
            int baseIdx = meshVerts.Count - 3;
            meshTris.Add(baseIdx);
            meshTris.Add(baseIdx + 1);
            meshTris.Add(baseIdx + 2);
        }

        mesh.SetVertices(meshVerts);
        mesh.SetTriangles(meshTris, 0);
        mesh.RecalculateNormals();

        return mesh;
    }
}
