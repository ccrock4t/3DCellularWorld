using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VertexData = System.Tuple<UnityEngine.Vector3, UnityEngine.Vector3, UnityEngine.Vector2>;

public class VoxelUtils : MonoBehaviour
{

    public enum BlockSide { BOTTOM=0, TOP=1, LEFT=2, RIGHT=3, FRONT=4, BACK=5 };

    /// <summary>
    ///     Merge an array of meshes into a single mesh
    /// </summary>
    /// <param name="meshes"></param>
    /// <returns></returns>
    public static Mesh MergeMeshes(Mesh[] meshes)
    {
        Mesh mesh = new Mesh();

        Dictionary<VertexData, int> pointsOrder = new Dictionary<VertexData, int>();
        HashSet<VertexData> pointsHash = new HashSet<VertexData>();
        List<int> tris = new List<int>();

        // merge vertices
        int pIndex = 0;
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i] == null) continue;
            for (int j = 0; j < meshes[i].vertices.Length; j++)
            {
                Vector3 v = meshes[i].vertices[j];
                Vector3 n = meshes[i].normals[j];
                Vector2 u = meshes[i].uv[j];
                VertexData p = new VertexData(v, n, u);

                if (!pointsHash.Contains(p))
                {
                    pointsOrder.Add(p, pIndex);
                    pointsHash.Add(p);
                    pIndex++;
                }
            }

            // merge triangles
            for (int t = 0; t < meshes[i].triangles.Length; t++)
            {
                int triPoint = meshes[i].triangles[t];
                Vector3 v = meshes[i].vertices[triPoint];
                Vector3 n = meshes[i].normals[triPoint];
                Vector2 u = meshes[i].uv[triPoint];
                VertexData p = new VertexData(v, n, u);

                int index;
                pointsOrder.TryGetValue(p, out index);
                tris.Add(index);
            }
            meshes[i] = null;
        }

        ExtractArrays(pointsOrder, mesh);
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    ///     Transfer vertex, normal, and uv information
    ///     from a dictionary into a mesh.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="mesh"></param>
    public static void ExtractArrays(Dictionary<VertexData, int> data, Mesh mesh)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        foreach (VertexData v in data.Keys)
        {
            verts.Add(v.Item1);
            norms.Add(v.Item2);
            uvs.Add(v.Item3);
        }
        mesh.vertices = verts.ToArray();
        mesh.normals = norms.ToArray();
        mesh.uv = uvs.ToArray();
    }

    /// <summary>
    ///     Fractal Brownian Noise (sum of Perlin Noise)
    /// </summary>
    /// <param name="x"></param>
    /// <param name="z"></param>
    /// <param name="octaves"></param>
    /// <param name="scale"></param>
    /// <param name="heightScale"></param>
    /// <returns></returns>
    public static float FractalBrownianNoise(float x, float z, float octaves, float scale, float heightScale, float heightOffset)
    {
        float total = 0;
        float frequency = 1;
        for (int i = 0; i < octaves; i++)
        {
            float noise = Mathf.PerlinNoise(x * scale * frequency, z * scale * frequency) * heightScale;
            total += noise;

            frequency *= 2;
        }
        return total + heightOffset;
    }

    public static float FractalBrownianNoise3D(float x, float y, float z, float octaves, float scale, float heightScale, float heightOffset)
    {
        float XY = FractalBrownianNoise(x, y, octaves, scale, heightScale, heightOffset);
        float YZ = FractalBrownianNoise(y, z, octaves, scale, heightScale, heightOffset);
        float XZ = FractalBrownianNoise(x, z, octaves, scale, heightScale, heightOffset);
        float YX = FractalBrownianNoise(y, x, octaves, scale, heightScale, heightOffset);
        float ZY = FractalBrownianNoise(z, y, octaves, scale, heightScale, heightOffset);
        float ZX = FractalBrownianNoise(z, x, octaves, scale, heightScale, heightOffset);

        return (XY + YZ + XZ + YX + ZY + ZX) / 6.0f;
    }



    ///
    /// Index converters
    ///

    public static int3 Index_int3FromFlat(int i, int3 automaton_dimensions)
    {
        int x = i % automaton_dimensions.x;
        int y = Mathf.FloorToInt(i / automaton_dimensions.x) % automaton_dimensions.y;
        int z = Mathf.FloorToInt(i / (automaton_dimensions.x * automaton_dimensions.y));
        return new int3(x, y, z);
    }


    public static int Index_FlatFromint3(int3 coords, int3 automaton_dimensions)
    {
        return Index_FlatFromint3(coords, automaton_dimensions);
    }

    public static int Index_FlatFromint3(int x, int y, int z, int3 automaton_dimensions)
    {
        return x + automaton_dimensions.x * y + automaton_dimensions.x * automaton_dimensions.y * z;
    }

    public static bool IsOutOfBounds(int3 index, int3 automaton_dimensions)
    {
        return IsOutOfBounds(index.x, index.y, index.z, automaton_dimensions);
    }

    public static bool IsOutOfBounds(int x, int y, int z, int3 automaton_dimensions)
    {
        return (x < 0 || y < 0 || z < 0 || x >= automaton_dimensions.x || y >= automaton_dimensions.y || z >= automaton_dimensions.z);
    }
}
