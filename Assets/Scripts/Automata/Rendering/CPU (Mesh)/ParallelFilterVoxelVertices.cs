using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static WorldAutomaton;
using static WorldAutomaton.Elemental;
using static WorldAutomatonCPU;
using static VoxelUtils;

using WorldCellInfo = CellInfo<WorldAutomaton.Elemental.Element>;

// Parallel filter visible vertices for an element
[BurstCompile]
public struct ParallelFilterVoxelVertices : IJobParallelFor
{
    public Element filter_element;
    [NativeDisableParallelForRestriction]
    public NativeArray<WorldCellInfo> cell_grid;
    [WriteOnly]
    public ArrayOfNativeListWriter<float3x4> filtered_vertex_list_for_each_element;
    [NativeDisableParallelForRestriction]
    public NativeArray<float3x4> unfiltered_quad_vertices;

    public int3 automaton_dimensions;

    // i = index of quad, the group of its 4 vertices
    // if the quad should be hidden, do nothing. If the quad should be shown, add it to the NativeList for rendering
    public void Execute(int i)
    {
        BlockSide block_side = (BlockSide)(i % 6);
        int voxel_flat_idx = (i - (int)block_side) / 6;
        Element state = VoxelAutomaton<Element>.GetCellNextState(this.cell_grid, voxel_flat_idx);
        int3 index = VoxelUtils.Index_int3FromFlat(voxel_flat_idx, this.automaton_dimensions);
        if (CanHideQuadToNeighbor(index, block_side)) return; // filter out invisible vertices
        NativeList<float3x4>.ParallelWriter filtered_vertex_list = filtered_vertex_list_for_each_element[(int)state];
        filtered_vertex_list.AddNoResize(unfiltered_quad_vertices[i]);
    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name="neighborX"></param>
    /// <param name="neighborY"></param>
    /// <param name="neighborZ"></param>
    /// <param name="type"></param>
    /// <returns>True if neighbor doesnt need to be drawn, False if neighbor does need to be drawn</returns>
    public bool CanHideQuadToNeighbor(int3 index, BlockSide side)
    {
        Element state = VoxelAutomaton<Element>.GetCellNextState(this.cell_grid, this.automaton_dimensions, index.x, index.y, index.z);
        int x = index.x;
        int y = index.y;
        int z = index.z;
        bool can_hide_quad = false;
        switch (side)
        {
            case BlockSide.FRONT:
                can_hide_quad = CanHideQuadToNeighbor(x, y, z + 1, state);
                break;
            case BlockSide.BACK:
                can_hide_quad = CanHideQuadToNeighbor(x, y, z - 1, state);
                break;
            case BlockSide.BOTTOM:
                can_hide_quad = CanHideQuadToNeighbor(x, y - 1, z, state);
                break;
            case BlockSide.TOP:
                can_hide_quad = CanHideQuadToNeighbor(x, y + 1, z, state);
                break;
            case BlockSide.LEFT:
                can_hide_quad = CanHideQuadToNeighbor(x - 1, y, z, state);
                break;
            case BlockSide.RIGHT:
                can_hide_quad = CanHideQuadToNeighbor(x + 1, y, z, state);
                break;
        }
        return can_hide_quad;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="neighborX"></param>
    /// <param name="neighborY"></param>
    /// <param name="neighborZ"></param>
    /// <param name="type"></param>
    /// <returns>True if neighbor doesnt need to be drawn, False if neighbor does need to be drawn</returns>
    public bool CanHideQuadToNeighbor(int neighborX, int neighborY, int neighborZ, Element type)
    {
        if (type == Element.Empty) return true; // empty doesnt have quads

        int3 neighborIndex = new int3(neighborX, neighborY, neighborZ);
        if (VoxelUtils.IsOutOfBounds(neighborIndex, this.automaton_dimensions)) return false;
        Element neighborTypeOrNull = VoxelAutomaton<Element>.GetCellNextState(this.cell_grid, this.automaton_dimensions, neighborX, neighborY, neighborZ);
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

}
