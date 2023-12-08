using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static WorldAutomaton;
using static WorldAutomaton.Elemental;
using WorldCellInfo = CellInfo<WorldAutomaton.Elemental.Element>;

// Parallel compute next state of the automata
[BurstCompile]
public struct ParallelCalculateNextWorldState : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    public NativeArray<WorldCellInfo> cell_grid;
    [NativeDisableParallelForRestriction]
    public NativeArray<int3> block_grid;
    [ReadOnly]
    public NativeArray<int3> neighbor_offsets_int3; // quad vertices for each element

    public byte frame_mod2;
    public int frame;


    public int3 automaton_dimensions;

    public void Execute(int i)
    {
        int3 bottom_bottom_left_block_corner_cell_idx = block_grid[i]; // starting at some corner

        int offset_x, offset_y, offset_z;
        (offset_x, offset_y, offset_z) = GetCellBlockOffset(bottom_bottom_left_block_corner_cell_idx.x, bottom_bottom_left_block_corner_cell_idx.y, bottom_bottom_left_block_corner_cell_idx.z);
        bottom_bottom_left_block_corner_cell_idx.x -= offset_x;
        bottom_bottom_left_block_corner_cell_idx.y -= offset_y;
        bottom_bottom_left_block_corner_cell_idx.z -= offset_z;



        // with some offset
        int x, y, z;

        int passes = 2;
        for (int pass = 0; pass < passes; pass++)
        {
            for (byte ox = 0; ox <= 1; ox++)
            {

                //process block
                x = bottom_bottom_left_block_corner_cell_idx.x + ox;
                for (byte oy = 0; oy <= 1; oy++)
                {
                    y = bottom_bottom_left_block_corner_cell_idx.y + oy;
                    for (byte oz = 0; oz <= 1; oz++)
                    {
                        z = bottom_bottom_left_block_corner_cell_idx.z + oz;
                        //cell index
                        if (VoxelUtils.IsOutOfBounds(x, y, z, this.automaton_dimensions)) continue;
                        int j = VoxelUtils.Index_FlatFromint3(x, y, z, this.automaton_dimensions);
                        WorldCellInfo info = VoxelAutomaton<Element>.GetCellInfo(this.cell_grid, j);

                        if (pass == 0)
                        {
                            //the "next state" from last timestep is now the "current state" in this time step.
                            // So, put the next state into the current state. This step is crucial 
                            info.current_state = info.next_state;
                            this.cell_grid[j] = info;
                        }
                        else if (pass == 1)
                        {

                            //skip air
                            if (info.current_state == (int)Element.Empty || info.last_frame_modified == this.frame) continue;

                            //compute the next state
                            this.UpdateCell(x, y, z, ox, oy, oz); // get what the next state for this cell should be
                        }

                    }
                }
            

            }
        }

    }

    /// <summary>
    /// Given the cell and current frame, determine the cell's position in the block
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    (int, int, int) GetCellBlockOffset(int x, int y, int z)
    {

        // classic checkerboard
        switch (this.frame_mod2)
        {
            case 0: //
                return (0, 0, 0);
                break;
            case 1:
                return (1, 1, 1);
                break;
        }
        Debug.LogError("error");
        return (0, 0, 0);
    }


    /// <summary>
    /// Uses local rules to compute the next state / value for the given cell index
    /// </summary>
    /// <param name="cellIndex"></param>
    void UpdateCell(int x, int y, int z, int ox, int oy, int oz)
    {
        Element my_state = VoxelAutomaton<Element>.GetCellCurrentState(this.cell_grid, this.automaton_dimensions, x, y, z);
        //apply rules
        switch (my_state)
        {
            case (Element.Stone):
                StoneRule(x, y, z, ox, oy, oz);
                break;
            case (Element.Sand):
                SandRule(x, y, z, ox, oy, oz);
                break;
            case (Element.Water):
                WaterRule(x, y, z, ox, oy, oz);
                break;
            case (Element.Lava):
                LavaRule(x, y, z, ox, oy, oz);
                break;
            case (Element.Steam):
                SteamRule(x, y, z, ox, oy, oz);
                break;
            default:
                break;
        }
    }



    /// <summary>
    ///     Apply the Stone Rule
    ///     It attempts to move straight downward. Otherwise stays put.
    /// </summary>
    void StoneRule(int x, int y, int z, int ox, int oy, int oz)
    {
        Element state = Element.Stone;
        //try to move down
        if (PropertyGravity(x, y, z, ox, oy, oz, state)) return;
    }


    /// <summary>
    ///     Apply the Sand Rule
    ///     It attempts to move downward in any direction. Otherwise stays put.
    /// </summary>
    void SandRule(int x, int y, int z, int ox, int oy, int oz)
    {
        Element state = Element.Sand;
        //try to move down
        if (PropertyGravity(x, y, z, ox, oy, oz, state)) return;
        //try to move slide down
        if (PropertySlide(x, y, z, ox, oy, oz, state)) return;
    }

    /// <summary>
    ///     Apply the Water Rule
    ///     It attempts to move downward in any direction. If it cannot, tries to move on the level plane. Otherwise stays put.
    ///     
    /// </summary>
    void WaterRule(int x, int y, int z, int ox, int oy, int oz)
    {
        Element state = Element.Water;
        //try to move down
        if (PropertyGravity(x, y, z, ox, oy, oz, state)) return;
        //try to slide down
        if (PropertySlide(x, y, z, ox, oy, oz, state)) return;
        // try to flow horizontally
        if (PropertyFlow(x, y, z, ox, oy, oz, state)) return;
    }

    /// <summary>
    ///     Apply the Lava Rule
    ///     It attempts to move downward in any direction. If it cannot, tries to move on the level plane. Otherwise stays put.
    ///     
    /// </summary>
    void LavaRule(int x, int y, int z, int ox, int oy, int oz)
    {
        Element state = Element.Lava;
        //try to move down
        if (PropertyGravity(x, y, z, ox, oy, oz, state)) return;
        //try to slide down
        if (PropertySlide(x, y, z, ox, oy, oz, state)) return;
        // try to flow horizontally
        if (PropertyFlow(x, y, z, ox, oy, oz, state)) return;
    }

    /// <summary>
    ///     Apply the Steam Rule
    ///     It attempts to move diagonally in any direction. If it cannot, bounces off the object it collides with.
    ///     
    /// </summary>
    void SteamRule(int x, int y, int z, int ox, int oy, int oz)
    {
        Element state = Element.Steam;
        //move around like a billiard ball
        if (PropertyBBM(x, y, z, ox, oy, oz, state)) return;
    }

    /*
     * 
     * 
     * 
     *     PROPERTIES OF ELEMENTS
     *     
     *     PHYSICAL AND CHEMICAL REACTIVE
     *     
     *     Use these functions to build new elements
     * 
     * 
     * 
     */

    /// <summary>
    /// Tries to move directly down
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="ox"></param>
    /// <param name="oy"></param>
    /// <param name="oz"></param>
    /// <param name="state"></param>
    bool PropertyGravity(int x, int y, int z, int ox, int oy, int oz, Element state)
    {
        if (y == 0) return false; // global bottom, can't move down - implicitly impenetrable
        if (oy == 0) return false; // at bottom of neighborhood, cant move down - implicitly impenetrable
        int ny = y - 1; // check directly below
        return CheckNeighborAndTrySwap(x, y, z, x, ny, z, state);
    }

    /// <summary>
    /// Tries to move both down and horizontal (i.e., move downwards diagonally, aka slide)
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="ox"></param>
    /// <param name="oy"></param>
    /// <param name="oz"></param>
    /// <param name="state"></param>
    bool PropertySlide(int x, int y, int z, int ox, int oy, int oz, Element state)
    {
        if (oy == 0) return false; // at bottom of neighborhood, cant move down - implicitly impenetrable

        // check neighbors below
        NativeList<int3> neighbor_offsets = new NativeList<int3>(Allocator.Temp);

        int neighbor_direction_x = (ox == 0) ? 1 : -1;
        int neighbor_direction_z = (oz == 0) ? 1 : -1;

        neighbor_offsets.Add(GetNeighborOffsetInt3(neighbor_direction_x, -1, neighbor_direction_z));
        neighbor_offsets.Add(GetNeighborOffsetInt3(0, -1, neighbor_direction_z));
        neighbor_offsets.Add(GetNeighborOffsetInt3(neighbor_direction_x, -1, 0));
        

        return TrySwap(x, y, z, state, neighbor_offsets);
    }

    /// <summary>
    ///     Tries to move on the level plane
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="ox"></param>
    /// <param name="oy"></param>
    /// <param name="oz"></param>
    /// <param name="state"></param>
    bool PropertyFlow(int x, int y, int z, int ox, int oy, int oz, Element state)
    {
        if (y == 0) return false;
        int neighbor_below_index = VoxelUtils.Index_FlatFromint3(x, y-1, z, this.automaton_dimensions);
        NativeList<int3> neighbor_offsets = new NativeList<int3>(Allocator.Temp);

        // whcih directions are the neighbors in the x and z directions?
        int neighbor_direction_x = (ox == 0) ? 1 : -1;
        int neighbor_direction_z = (oz == 0) ? 1 : -1;

        // try to move on the level plane
        neighbor_offsets.Add(GetNeighborOffsetInt3(neighbor_direction_x, 0, neighbor_direction_z));
        neighbor_offsets.Add(GetNeighborOffsetInt3(0, 0, neighbor_direction_z));
        neighbor_offsets.Add(GetNeighborOffsetInt3(neighbor_direction_x, 0, 0));

        return TrySwap(x, y, z, state, neighbor_offsets);
    }

    /// <summary>
    /// Tries to move both diagonally, flipping both all coordinates
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="ox"></param>
    /// <param name="oy"></param>
    /// <param name="oz"></param>
    /// <param name="state"></param>
    bool PropertyBBM(int x, int y, int z, int ox, int oy, int oz, Element state)
    {
        int nx, ny, nz;
        // check neighbors below
        NativeList<int3> neighbor_offsets = new NativeList<int3>(Allocator.Temp);

        int neighbor_direction_x = (ox == 0) ? 1 : -1;
        int neighbor_direction_y = (oy == 0) ? 1 : -1;
        int neighbor_direction_z = (oz == 0) ? 1 : -1;

        neighbor_offsets.Add(GetNeighborOffsetInt3(neighbor_direction_x, neighbor_direction_y, neighbor_direction_z));

        neighbor_offsets.Add(GetNeighborOffsetInt3(0, neighbor_direction_y, neighbor_direction_z));
        neighbor_offsets.Add(GetNeighborOffsetInt3(neighbor_direction_x, neighbor_direction_y, 0));
        neighbor_offsets.Add(GetNeighborOffsetInt3(neighbor_direction_x, 0, neighbor_direction_z));

        neighbor_offsets.Add(GetNeighborOffsetInt3(0, neighbor_direction_y, 0));
        neighbor_offsets.Add(GetNeighborOffsetInt3(0, 0, neighbor_direction_z));
        neighbor_offsets.Add(GetNeighborOffsetInt3(neighbor_direction_x, 0, 0));

        return TrySwap(x, y, z, state, neighbor_offsets);
    }

    bool TrySwap(int x, int y, int z, Element state, NativeList<int3> neighbor_offsets)
    {
        int3 neighbor_offset;
        int nx, ny, nz;
        while (neighbor_offsets.Length > 0)
        {
            neighbor_offset = neighbor_offsets[0];
            neighbor_offsets.RemoveAt(0);
            nx = x + neighbor_offset.x;
            ny = y + neighbor_offset.y;
            nz = z + neighbor_offset.z;
            if (CheckNeighborAndTrySwap(x, y, z, nx, ny, nz, state)) return true;
        }

        neighbor_offsets.Dispose();

        return false;
    }


    /// <summary>
    ///     Try to move to an empty cell. If the cell is not empty, tries to chemically react.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="nx"></param>
    /// <param name="ny"></param>
    /// <param name="nz"></param>
    /// <returns>Did update cell. Otherwise failed to update cell (could not move nor react)</returns>
    bool CheckNeighborAndTrySwap(int x, int y, int z, int nx, int ny, int nz, Element cell_state)
    {
        if (VoxelUtils.IsOutOfBounds(nx, ny, nz, this.automaton_dimensions)) return false;
        WorldCellInfo neighbor_info = VoxelAutomaton<Element>.GetCellInfo(this.cell_grid, this.automaton_dimensions, nx, ny, nz);
        Element neighbor_state = neighbor_info.current_state;
        bool is_air = (neighbor_state == Element.Empty);
        bool can_displace = IsSolid(cell_state) && !IsSolid(neighbor_state);
         
        // physical reaction
        if ((is_air || can_displace) && neighbor_info.last_frame_modified != this.frame)
        {
            this.SetCellNextState(x, y, z, neighbor_state);
            this.SetCellNextState(nx, ny, nz, cell_state);
            return true;
        }

        // chemical reaction
        Element? reaction_result = ReactionResult(cell_state, neighbor_state);
        if (reaction_result != null && neighbor_info.last_frame_modified != this.frame)
        {
            this.SetCellNextState(x, y, z, (Element)reaction_result);
            this.SetCellNextState(nx, ny, nz, (Element)reaction_result);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Return what the result would be if these 2 elements tried to reaction
    /// </summary>
    /// <param name="cell_state1"></param>
    /// <param name="cell_state2"></param>
    /// <returns></returns>
    Element? ReactionResult(Element cell_state1, Element cell_state2)
    {
        // check in ascending order of Enum value
        if (cell_state1 == Element.Lava || cell_state2 == Element.Lava)
        {
            if (cell_state1 == Element.Water || cell_state2 == Element.Water) return Element.Stone; // Lava and Water make stone

        }
        else if (cell_state1 == Element.Steam || cell_state2 == Element.Steam)
        {
            if (cell_state1 == Element.Water || cell_state2 == Element.Water) return Element.Water; // Water and Steam make Water
        }

        return null;
    }


    void SetCellNextState(int x, int y, int z, Element state)
    {
        VoxelAutomaton<Element>.SetCellNextState(this.cell_grid, this.automaton_dimensions, this.frame, x, y, z, state);
    }


    int3 GetNeighborOffsetInt3(int offset_x, int offset_y, int offset_z)
    {
        byte key = 0;
        key += (byte)((offset_x + 1) * 9);
        key += (byte)((offset_y + 1) * 3);
        key += (byte)(offset_z + 1);
        return this.neighbor_offsets_int3[key];
    }


}
