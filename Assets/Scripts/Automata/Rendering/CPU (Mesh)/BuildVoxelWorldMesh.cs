using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static WorldAutomatonCPU;
using static WorldAutomaton.Elemental;
using static VoxelUtils;
using WorldCellInfo = CellInfo<WorldAutomaton.Elemental.Element>;


/// <summary>
/// Parallel calculate all possible vertices in the world, so they can be accessed later.
/// </summary>

[BurstCompile]
public struct BuildVoxelWorldMesh : IJobParallelFor
{
    public int3 automaton_dimensions;

    [NativeDisableParallelForRestriction]
    public NativeArray<float3x4> vertices; 


    // the index of each voxel
    public void Execute(int i)
    {
        CreateVoxelMesh(i, VoxelUtils.Index_int3FromFlat(i, this.automaton_dimensions));
    }


    public void CreateVoxelMesh(int i, int3 index)
    {
        for(int j = 0; j < 6; j++)
        {
            CreateQuad(i, index, (BlockSide)j);
        }


    }

    public void CreateQuad(int i, int3 index, BlockSide side)
    {
        float3x4 verts = FetchQuadVertices(side, index);
        this.vertices[i * 6 + (int)side] = verts;
    }


  
    public float3x4 FetchQuadVertices(BlockSide side, float3 position_offset)
    {
        float3x4 val = new float3x4();
        //NativeArray<float3> normals;

        switch (side)
        {
            case BlockSide.BOTTOM:
                val[0] = voxel_vertices[0];
                val[1] = voxel_vertices[1];
                val[2] = voxel_vertices[2];
                val[3] = voxel_vertices[3];
                /*                normals = new NativeArray<float3>(4, Allocator.Temp)
                                {
                                    [0] = downVector,
                                    [1] = downVector,
                                    [2] = downVector,
                                    [3] = downVector
                                };
                */
                break;
            case BlockSide.TOP:
                val[0] = voxel_vertices[7];
                val[1] = voxel_vertices[6];
                val[2] = voxel_vertices[5];
                val[3] = voxel_vertices[4];
                /*                normals = new NativeArray<float3>(4, Allocator.Temp)
                                {
                                    [0] = upVector,
                                    [1] = upVector,
                                    [2] = upVector,
                                    [3] = upVector
                                };*/
                break;
            case BlockSide.LEFT:
                val[0] = voxel_vertices[7];
                val[1] = voxel_vertices[4];
                val[2] = voxel_vertices[0];
                val[3] = voxel_vertices[3];
                /*                normals = new NativeArray<float3>(4, Allocator.Temp)
                                {
                                    [0] = leftVector,
                                    [1] = leftVector,
                                    [2] = leftVector,
                                    [3] = leftVector
                                };*/
                break;
            case BlockSide.RIGHT:
                val[0] = voxel_vertices[5];
                val[1] = voxel_vertices[6];
                val[2] = voxel_vertices[2];
                val[3] = voxel_vertices[1];
                /*                normals = new NativeArray<float3>(4, Allocator.Temp)
                                {
                                    [0] = rightVector,
                                    [1] = rightVector,
                                    [2] = rightVector,
                                    [3] = rightVector
                                };*/
                break;
            case BlockSide.FRONT:
                val[0] = voxel_vertices[4];
                val[1] = voxel_vertices[5];
                val[2] = voxel_vertices[1];
                val[3] = voxel_vertices[0];
                /*                normals = new NativeArray<float3>(4, Allocator.Temp)
                                {
                                    [0] = forwardVector,
                                    [1] = forwardVector,
                                    [2] = forwardVector,
                                    [3] = forwardVector
                                };*/
                break;
            case BlockSide.BACK:
                val[0] = voxel_vertices[6];
                val[1] = voxel_vertices[7];
                val[2] = voxel_vertices[3];
                val[3] = voxel_vertices[2];
                /*                normals = new NativeArray<float3>(4, Allocator.Temp)
                                {
                                    [0] = backVector,
                                    [1] = backVector,
                                    [2] = backVector,
                                    [3] = backVector
                                };*/
                break;
            default:
                Debug.LogError("error");
                val[0] = voxel_vertices[0];
                val[1] = voxel_vertices[2];
                val[2] = voxel_vertices[4];
                val[3] = voxel_vertices[6];
                /*                normals = new NativeArray<float3>(4, Allocator.Temp)
                                {
                                    [0] = backVector,
                                    [1] = backVector,
                                    [2] = backVector,
                                    [3] = backVector
                                };*/
                break;
        }
        val[0] += position_offset;
        val[1] += position_offset;
        val[2] += position_offset;
        val[3] += position_offset;

        return val;
    }


    [ReadOnly]
    static float3 forwardVector = new float3(0, 0, 1);
    [ReadOnly]
    static float3 backVector = new float3(0, 0, -1);
    [ReadOnly]
    static float3 upVector = new float3(0, 1, 0);
    [ReadOnly]
    static float3 downVector = new float3(0, -1, 0);
    [ReadOnly]
    static float3 leftVector = new float3(-1, 0, 0);
    [ReadOnly]
    static float3 rightVector = new float3(1, 0, 0);

    [ReadOnly]
    static int2 uv00 = new int2(0, 0);
    [ReadOnly]
    static int2 uv10 = new int2(1, 0);
    [ReadOnly]
    static int2 uv01 = new int2(0, 1);
    [ReadOnly]
    static int2 uv11 = new int2(1, 1);

}
