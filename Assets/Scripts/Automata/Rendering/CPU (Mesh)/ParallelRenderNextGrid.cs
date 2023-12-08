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
public struct ParallelRenderNextGrid : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    public NativeArray<WorldCellInfo> grid;
    [ReadOnly]
    public ArrayOfNativeList<float3x4> element_vertex_lists; // mesh data for each element
    [ReadOnly]
    public NativeArray<int2> mesh_index_to_element_mesh_info;
    public Mesh.MeshDataArray job_mesh_data; // mesh data for each element that will be applied


    [ReadOnly]
    public readonly static int3 tri1 = new int3(3, 1, 0);
    [ReadOnly]
    public readonly static int3 tri2 = new int3(3, 2, 1);


    // index = index of mesh
    public void Execute(int index)
    {
        int2 element_mesh_info = mesh_index_to_element_mesh_info[index];
        int element_mesh_index = element_mesh_info.x;
        int element = element_mesh_info.y;
        //Element 
        // in the job
        NativeList<float3x4> inputVerts = this.element_vertex_lists[element];
        NativeArray<Vector3> output_verts = job_mesh_data[index].GetVertexData<Vector3>();
        NativeArray<int> output_triangles = job_mesh_data[index].GetIndexData<int>();

        int output_tri_idx = 0;
        int output_vert_idx = 0;

        int vertex_multiplier = 4;
        int tri_idx_offset;
        for (int i = 0; output_vert_idx < output_verts.Length; i++)
        {
            float3x4 vertex_bundle = inputVerts[element_mesh_index * (MAX_VERTICES_PER_MESH/ vertex_multiplier) + i];

            tri_idx_offset = output_vert_idx;

            for (int j = 0; j < vertex_multiplier; j++)
            {
                float3 vertex = vertex_bundle[j];
                output_verts[output_vert_idx] = vertex;
                output_vert_idx++;
            }

            for (int j = 0; j < 6; j++)
            {
                int3 tri;
                if (j < 3) tri = tri1;
                else tri = tri2;
                output_triangles[output_tri_idx] = tri[j % 3] + tri_idx_offset;
                output_tri_idx++;
            }

        }


    }




}
