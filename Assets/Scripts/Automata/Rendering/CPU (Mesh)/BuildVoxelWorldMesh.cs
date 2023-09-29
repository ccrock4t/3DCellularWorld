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


    /// <summary>
    /// 
    /// </summary>
    /// <param name="neighborX"></param>
    /// <param name="neighborY"></param>
    /// <param name="neighborZ"></param>
    /// <param name="type"></param>
    /// <returns>True if neighbor doesnt need to be drawn, False if neighbor does need to be drawn</returns>
    public bool CanHideQuadToNeighbor(NativeArray<WorldCellInfo> cell_grid, int neighborX, int neighborY, int neighborZ, Element type)
    {
        if (type == Element.Empty) return true; // empty doesnt have quads

        int3 neighborIndex = new int3(neighborX, neighborY, neighborZ);
        if (VoxelUtils.IsOutOfBounds(neighborIndex, this.automaton_dimensions)) return false;
        Element neighborTypeOrNull = VoxelAutomaton<Element>.GetCellNextState(cell_grid, this.automaton_dimensions, neighborX, neighborY, neighborZ);
        Element neighborType = neighborTypeOrNull;


        if (neighborType == Element.Empty) return false; // must show to empty space

        //if neighbor is the same type, can hide the quad (e.g., water neighboring water)
        if (neighborType == type)
        {
            return true;
        }

        // if neighbor is air or water it is translucent, can't hide the quad
        if (IsSolid(type) && !IsSolid(neighborTypeOrNull))
        {
            return false;
        }

        //in any other situation, can hide the quad
        return true;
    }

    public float3x4 FetchQuadVertices(BlockSide side, float3 index)
    {
        float3x4 val = new float3x4();

        switch (side)
        {
            case BlockSide.BOTTOM:
                val[0] = voxel_vertices[0];
                val[1] = voxel_vertices[1];
                val[2] = voxel_vertices[2];
                val[3] = voxel_vertices[3];
                break;
            case BlockSide.TOP:
                val[0] = voxel_vertices[7];
                val[1] = voxel_vertices[6];
                val[2] = voxel_vertices[5];
                val[3] = voxel_vertices[4];
                break;
            case BlockSide.LEFT:
                val[0] = voxel_vertices[7];
                val[1] = voxel_vertices[4];
                val[2] = voxel_vertices[0];
                val[3] = voxel_vertices[3];
                break;
            case BlockSide.RIGHT:
                val[0] = voxel_vertices[5];
                val[1] = voxel_vertices[6];
                val[2] = voxel_vertices[2];
                val[3] = voxel_vertices[1];
                break;
            case BlockSide.FRONT:
                val[0] = voxel_vertices[4];
                val[1] = voxel_vertices[5];
                val[2] = voxel_vertices[1];
                val[3] = voxel_vertices[0];
                break;
            case BlockSide.BACK:
                val[0] = voxel_vertices[6];
                val[1] = voxel_vertices[7];
                val[2] = voxel_vertices[3];
                val[3] = voxel_vertices[2];
                break;
            default:
                Debug.LogError("error");
                val[0] = voxel_vertices[0];
                val[1] = voxel_vertices[2];
                val[2] = voxel_vertices[4];
                val[3] = voxel_vertices[6];
                break;
        }
        val[0] += index;
        val[1] += index;
        val[2] += index;
        val[3] += index;

        return val;
    }


}
