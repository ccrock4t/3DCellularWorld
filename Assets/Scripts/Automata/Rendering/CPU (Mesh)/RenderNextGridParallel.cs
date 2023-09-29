using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static WorldAutomatonCPU;
using static WorldAutomaton;
using static WorldAutomaton.Elemental;
using static VoxelUtils;
using WorldCellInfo = CellInfo<WorldAutomaton.Elemental.Element>;

/// <summary>
/// Insert the computed mesh data into a mesh data structure so it can be rendered by the GPU
/// </summary>

[BurstCompile]
public struct RenderNextGridParallel : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    public NativeArray<WorldCellInfo> grid;
    [ReadOnly]
    public ArrayOfNativeList<float3x4> element_mesh_data; // mesh data for each element
    [ReadOnly]
    public NativeArray<int2> mesh_index_to_element;
    public Mesh.MeshDataArray job_mesh_data; // mesh data for each element that will be applied


    [ReadOnly]
    public readonly static int3 tri1 = new int3(3, 1, 0);
    [ReadOnly]
    public readonly static int3 tri2 = new int3(3, 2, 1);

    // index = index of mesh
    public void Execute(int index)
    {
        int2 value = mesh_index_to_element[index];
        int element_mesh_index = value.x;
        int element = value.y;
        //Element 
        // in the job
        NativeList<float3x4> inputVerts = this.element_mesh_data[element];
        NativeArray<Vector3> outputVerts = job_mesh_data[index].GetVertexData<Vector3>();
        NativeArray<int> triangles = job_mesh_data[index].GetIndexData<int>();

        int tri_idx = 0;
        int start_idx = 0;

        for (int i = 0; (start_idx + 4) < outputVerts.Length; i++)
        {
            float3x4 vertex_bundle = inputVerts[element_mesh_index * (MAX_VERTICES_PER_MESH/4) + i];
            start_idx = i * 4;
            for (int j = 0; j < 4; j++)
            {
                float3 vertex = vertex_bundle[j];
                outputVerts[start_idx + j] = vertex;
            }

            triangles[tri_idx + 0] = tri1.x + start_idx;
            triangles[tri_idx + 1] = tri1.y + start_idx;
            triangles[tri_idx + 2] = tri1.z + start_idx;

            triangles[tri_idx + 3] = tri2.x + start_idx;
            triangles[tri_idx + 4] = tri2.y + start_idx;
            triangles[tri_idx + 5] = tri2.z + start_idx;

            tri_idx += 6;
        }


    }




}
