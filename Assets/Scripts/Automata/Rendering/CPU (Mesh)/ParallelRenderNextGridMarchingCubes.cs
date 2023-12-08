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

//[BurstCompile]
public struct ParallelRenderNextGridMarchingCubes : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    public NativeArray<WorldCellInfo> grid;
    [ReadOnly]
    public NativeArray<int2> mesh_index_to_element_mesh_info;
    public Mesh.MeshDataArray job_mesh_data; // mesh data for each element that will be applied

    // for Marching cubes rendering only
    [ReadOnly]
    public ArrayOfNativeList<(int, float3x12, int4x4)> element_vertices_and_triangles;

    // index = index of mesh
    public void Execute(int index)
    {
        int2 element_mesh_info = mesh_index_to_element_mesh_info[index];
        int element_mesh_index = element_mesh_info.x;
        int element = element_mesh_info.y;
        //Element 
        // in the job
        NativeList<(int, float3x12, int4x4)> input_verts_and_tris = this.element_vertices_and_triangles[element];
        NativeArray<Vector3> outputVerts = job_mesh_data[index].GetVertexData<Vector3>();
        NativeArray<int> output_triangles = job_mesh_data[index].GetIndexData<int>();

        int output_tri_idx = 0;
        int output_vert_idx = 0;

        int vertex_multiplier = 12;

        for (int i = 0; output_vert_idx < outputVerts.Length; i++)
        {
            int voxel_of_element_index = element_mesh_index * (MAX_VERTICES_PER_MESH / vertex_multiplier) + i;
            (_, float3x12 verts, int4x4 tris) = input_verts_and_tris[voxel_of_element_index];
           
            for (int j = 0; j < vertex_multiplier; j++)
            {
                float3 vertex = verts[j];
                outputVerts[output_vert_idx] = vertex;
                output_vert_idx++;
            }

            
            for (int j=0;j<15; j++)
            {
                int tri_value = tris[j / 4][j % 4];
                if (tri_value == -1)
                {
                    output_triangles[output_tri_idx] = 0;
                }
                else
                {
                    output_triangles[output_tri_idx] = tri_value + (output_vert_idx - vertex_multiplier);
                }
                
                output_tri_idx++;
            }
        }



    }




}
