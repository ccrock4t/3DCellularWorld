using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static WorldAutomatonCPU;
using static WorldAutomaton.Elemental;
using static VoxelUtils;
using WorldCellInfo = CellInfo<WorldAutomaton.Elemental.Element>;
using System;
using System.Runtime.CompilerServices;


/// <summary>
/// Parallel calculate all possible vertices in the world, so they can be accessed later.
/// </summary
/// 


//[BurstCompile]
public struct BuildVoxelWorldMeshMarchingCubes : IJobParallelFor
{
    public int3 automaton_dimensions;

    [NativeDisableParallelForRestriction]
    public NativeArray<WorldCellInfo> cell_grid;

    [NativeDisableParallelForRestriction]
    public ArrayOfNativeListWriter<(int, float3x12, int4x4)> vertices_and_triangles_writers;

    [ReadOnly]
    public NativeArray<int> native_edgeTable; // 256 elements
    [ReadOnly]
    public NativeArray<int> native_triTable; // 256 * 16 elements
    [ReadOnly]
    public NativeArray<int2> native_edges;


    // the index of each voxel
    public void Execute(int i)
    {
        // in regular voxel rendering, we spin up 1 thread for each vertex.
        // the thread is allocate memory to store up to 6 quads, which equals 24 vertices
        // however, a Marching Cubes "voxel" only uses 12 vertices.
        // Therefore, we will only fill the first 2 elements of the allocate memory, leaving the rest empty to be filtered out later
        CreateMesh(i, VoxelUtils.Index_int3FromFlat(i, this.automaton_dimensions));
    }


    public void CreateMesh(int i, int3 index)
    {
        Element state = (Element)GetVoxelType(index.x, index.y, index.z);
        if (state == Element.Empty) return; // if there is no element, no need to place any vertices

        // otherwise, we need to place some vertices
        Element?[] voxelCube = new Element?[8];
        voxelCube[0] = state;
        voxelCube[1] = GetVoxelType(index.x + 1, index.y, index.z);
        voxelCube[2] = GetVoxelType(index.x + 1, index.y, index.z - 1);
        voxelCube[3] = GetVoxelType(index.x, index.y, index.z - 1);

        voxelCube[4] = GetVoxelType(index.x, index.y + 1, index.z);
        voxelCube[5] = GetVoxelType(index.x + 1, index.y + 1, index.z);
        voxelCube[6] = GetVoxelType(index.x + 1, index.y + 1, index.z - 1);
        voxelCube[7] = GetVoxelType(index.x, index.y + 1, index.z - 1);

        var result = Polygonise(index, voxelCube);

        if (result == null) return; // the mesh is entirely hidden by other meshes, so nothing to display
        NativeList<(int, float3x12, int4x4)>.ParallelWriter element_vertices_and_triangles = this.vertices_and_triangles_writers[(int)state];
        element_vertices_and_triangles.AddNoResize(((int, float3x12, int4x4))result);
    }

    public Element? GetVoxelType(int x, int y, int z)
    {
        if (GlobalConfig.world_automaton.IsOutOfBounds(x, y, z)) return null;
        return VoxelAutomaton<Element>.GetCellNextState(this.cell_grid, this.automaton_dimensions, x, y, z);
    }


    const float isolevel = 0.5f;

    /// <summary>
    /// http://paulbourke.net/geometry/polygonise/
    /// </summary>
    /// 
    public (int, float3x12, int4x4)? Polygonise(int3 vertex_offset, Element?[] voxelCube)
    {
        int bitvector = 0;

        float3[] p = new float3[8];
        p[0] = new float3(0, 0, 0);
        p[1] = new float3(1f, 0, 0);
        p[2] = new float3(1f, 0, -1f);
        p[3] = new float3(0, 0, -1f);
        p[4] = new float3(0, 1f, 0);
        p[5] = new float3(1f, 1f, 0);
        p[6] = new float3(1f, 1f, -1f);
        p[7] = new float3(0, 1f, -1f);


        for (int i = 0; i < 8; i++) // 8 voxels representing vertexes of a virtual cube
        {
            float voxel_level = GetVoxelIsolevel(voxelCube[i]);

            if (voxel_level < isolevel)
            {
                int or_element = pow2(i);
                // = (int)math.pow(2, i);
                bitvector |= or_element; //bitwise OR to form a byte, where each bit represents whether its corresponding vertex is in or out
            }
        }
        

        /* Cube is entirely inside or outside of the surface */
        if (this.native_edgeTable[bitvector] == 0)
        {
            // do not draw
            return null;
        }


        // actual vertices (located on the edges of the virtual cube where the surface heightmap would intersect).
        float3x12 vertices = new();
        for (int i = 0; i < 12; i++) //12 edges of the virtual cube that could have a real mesh vertex
        {
            float3 interpolated_vertex;
         /*   if ((this.native_edgeTable[bitvector] & pow2(i)) != 0)
            {*/
                int2 edgePointIndexes = this.native_edges[i];
                int idx0 = edgePointIndexes[0];
                int idx1 = edgePointIndexes[1];
                float p0_level = GetVoxelIsolevel(voxelCube[idx0]);
                float p1_level = GetVoxelIsolevel(voxelCube[idx1]);
                interpolated_vertex = VertexInterpolate(isolevel, p[idx0], p[idx1], p0_level, p1_level); // LinearInterp(p[idx0], p[idx1], p0_level, p1_level);
                interpolated_vertex += vertex_offset;
       
            vertices[i] = interpolated_vertex;

        }


        int4x4 tris = new();
        for (int i = 0; i<15; i++)
        {
            int index = this.native_triTable[bitvector * 16 + i];
            tris[i / 4][i % 4] = index;
        }


        return (bitvector, vertices, tris);


    }

    float GetVoxelIsolevel(Element? voxel)
    {
        return (voxel == Element.Empty || voxel == null) ? 0.99999f : 0.49999f;
    }

    int pow2(int i)
    {
        int result = 1;
        for (int j = 0; j < i; j++)
        {
            result *= 2;
        }
        return result;
    }

    /*
       Linearly interpolate the position where an isosurface cuts
       an edge between two vertices, each with their own scalar value
        http://paulbourke.net/geometry/polygonise/
    */
    public static float3 VertexInterpolate(float isolevel, float3 p0, float3 p1, float valp0, float valp1)
    {


        float3 p = new float3();

        if (math.abs(isolevel - valp0) < 0.00001)
            return (p0);
        if (math.abs(isolevel - valp1) < 0.00001)
            return (p1);
        if (math.abs(valp0 - valp1) < 0.00001)
            return (p0);
        float mu = (isolevel - valp0) / (valp1 - valp0);
        p.x = p0.x + mu * (p1.x - p0.x);
        p.y = p0.y + mu * (p1.y - p0.y);
        p.z = p0.z + mu * (p1.z - p0.z);

        return p;
    }

    public static float3 VertexInterpolate2(float isolevel, float3 p0, float3 p1, float v0, float v1)
    {
        return p0 + (isolevel - v0) * (p1 - p0) / (v1 - v0);
    }
    /// </summary>
    public static Vector3 midlePointVertex(Vector3 p0, Vector3 p1)
    {
        return (p0 + p1) / 2;
    }

    public float3 LinearInterp(float3 p1, float3 p2, float valp1, float valp2)
    {

        float3 p = new float3();
        p.x = p1.x;
        p.y = p1.y;
        p.z = p1.z;
        float val = 0;
        if (p1.x != p2.x)
        {
            val = p2.x + (p1.x - p2.x) * (isolevel - valp2) / (valp1 - valp2);
            p.x = val;
        }
        else if (p1.y != p2.y)
        {
            val = p2.y + (p1.y - p2.y) * (isolevel - valp2) / (valp1 - valp2);
            p.y = val;
        }
        else
        {
            val = p2.z + (p1.z - p2.z) * (isolevel - valp2) / (valp1 - valp2);
            p.z = val;
        }
        return p;
    }

}
