using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using static WorldAutomaton.Elemental;
using WorldCellInfo = CellInfo<WorldAutomaton.Elemental.Element>;

/// <summary>
///     Computes and renders the voxel automaton using GPU
/// </summary>
public class WorldAutomatonGPU : WorldAutomaton
{
    // compute automaton in compute shader
    public ComputeShader compute_shader;


    // Material for fragment shader, to do raymarching rendering
    Material raymarch_shader_material;

    // buffers to share info between C# and GPU
    public ComputeBuffer cell_grid_buffer; // stores data for each cell. x is current state, y is the modified flag z is the computed next state
    public ComputeBuffer debug_buffer; // stores data for each cell. x is current state, y is the modified flag z is the computed next state
    public ComputeBuffer block_grid_buffer; // stores index for corner of each 2x2x2 Margolus block (unshifted blocks)
    
    public int main_kernel;

    WorldCellInfo[] cell_grid_data;
    int last_data_get_frame = -1;


    //const int MAX_NUM_GPU_THREADS_IN_GROUP = 65535;


    public override void Setup(WorldCellInfo[] cell_grid, int3[] block_grid)
    {

        // initialize
        this.cell_grid_buffer = new(this.automaton_size, Marshal.SizeOf(typeof(WorldCellInfo)));
        this.block_grid_buffer = new(block_grid.Length, Marshal.SizeOf(typeof(int3)));
        this.debug_buffer = new(1, Marshal.SizeOf(typeof(int3)));

        // set compute buffer on GPU shader
        raymarch_shader_material = GetComponent<MeshRenderer>().material;

        raymarch_shader_material.SetInteger("_voxel_data_length", this.automaton_size);
        raymarch_shader_material.SetInteger("AUTOMATA_SIZE_X", GlobalConfig.WORLD_AUTOMATON_DIMENSIONS.x);
        raymarch_shader_material.SetInteger("AUTOMATA_SIZE_Y", GlobalConfig.WORLD_AUTOMATON_DIMENSIONS.y);
        raymarch_shader_material.SetInteger("AUTOMATA_SIZE_Z", GlobalConfig.WORLD_AUTOMATON_DIMENSIONS.z);

        raymarch_shader_material.SetFloat("light_position_X", this.light.transform.position.x);
        raymarch_shader_material.SetFloat("light_position_Y", this.light.transform.position.y);
        raymarch_shader_material.SetFloat("light_position_Z", this.light.transform.position.z);

        compute_shader.SetInt("AUTOMATA_SIZE_X", GlobalConfig.WORLD_AUTOMATON_DIMENSIONS.x);
        compute_shader.SetInt("AUTOMATA_SIZE_Y", GlobalConfig.WORLD_AUTOMATON_DIMENSIONS.y);
        compute_shader.SetInt("AUTOMATA_SIZE_Z", GlobalConfig.WORLD_AUTOMATON_DIMENSIONS.z);

        //
        this.main_kernel = compute_shader.FindKernel("CSMain");
        compute_shader.SetBuffer(this.main_kernel, "cell_grid", this.cell_grid_buffer);
        compute_shader.SetBuffer(this.main_kernel, "block_grid", this.block_grid_buffer);
        compute_shader.SetBuffer(this.main_kernel, "debug_buffer", this.debug_buffer);
        raymarch_shader_material.SetBuffer("_voxel_data", this.cell_grid_buffer);
        uint threads_x, threads_y, threads_z;
        compute_shader.GetKernelThreadGroupSizes(this.main_kernel, out threads_x, out threads_y, out threads_z);

        // initial setting of buffers
        this.cell_grid_buffer.SetData(cell_grid);
        this.block_grid_buffer.SetData(block_grid);

        this.cell_grid_data = new WorldCellInfo[this.cell_grid_buffer.count];
    }




    public void UpdateLightPosition()
    {
        raymarch_shader_material.SetFloat("light_position_X", this.light.transform.position.x);
        raymarch_shader_material.SetFloat("light_position_Y", this.light.transform.position.y);
        raymarch_shader_material.SetFloat("light_position_Z", this.light.transform.position.z);
    }



    public override void RunMouseButtonBehaviors(Element state, int brush_size)
    {
        float distance = 2.0f;
        Vector3 point = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
        int3 index = new int3(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), Mathf.RoundToInt(point.z));

        WorldCellInfo[] data = null;

        //check all downward neighbors
        for (int nx = index.x - brush_size; nx <= index.x + brush_size; nx++)
        {
            for (int ny = index.y - brush_size; ny <= index.y + brush_size; ny++)
            {
                for (int nz = index.z - brush_size; nz <= index.z + brush_size; nz++)
                {
                    if (VoxelUtils.IsOutOfBounds(nx, ny, nz, GlobalConfig.world_automaton.automaton_dimensions)) continue;
                    if (data == null)
                    {
                        data = new WorldCellInfo[this.cell_grid_buffer.count];
                        this.cell_grid_buffer.GetData(data);
                    }
                    int i = VoxelUtils.Index_FlatFromint3(nx, ny, nz, this.automaton_dimensions);
                    data[i].current_state = state;
                    data[i].next_state = state;
                }

            }
        }


        this.cell_grid_buffer.SetData(data);
    }



    /// <summary>
    /// Calculate the next state of the automata
    /// </summary>
    public override void CalculateAndRenderNextGridState()
    {
        compute_shader.SetInt("frame", this.frame);
        compute_shader.SetInt("frame_mod2", this.frame % 2);


        int remaining_blocks = this.block_grid_buffer.count;

        int i = 0;
        int max_blocks_processed_per_dispatch = GlobalConfig.MAX_NUM_OF_THREAD_GROUPS * GlobalConfig.NUM_OF_GPU_THREADS;
        while (remaining_blocks > 0)
        {
            compute_shader.SetInt("index_offset", i * max_blocks_processed_per_dispatch);
            if (remaining_blocks < max_blocks_processed_per_dispatch)
            {
                compute_shader.Dispatch(this.main_kernel, Mathf.CeilToInt(remaining_blocks / GlobalConfig.NUM_OF_GPU_THREADS), 1, 1);
                remaining_blocks = 0;
                break;
            }
            else
            {
                compute_shader.Dispatch(this.main_kernel, GlobalConfig.MAX_NUM_OF_THREAD_GROUPS, 1, 1);
                remaining_blocks -= max_blocks_processed_per_dispatch;
            }
            i++;
        }
    }





    private void OnApplicationQuit()
    {

        try
        {
            this.cell_grid_buffer.Dispose();
        }
        catch
        {

        }


        try
        {
            this.block_grid_buffer.Dispose();
        }
        catch
        {

        }
        try
        {
            this.debug_buffer.Dispose();
        }
        catch
        {

        }

    }

    /**
     *  Helpers
     * 
     */

    public void RefreshCellInfoData()
    {
        if (last_data_get_frame != this.frame)
        {
            cell_grid_buffer.GetData(this.cell_grid_data);
            last_data_get_frame = this.frame;
        }
    }



    //=====================

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public override Element GetCellNextState(int i)
    {
        RefreshCellInfoData();
        return (Element)this.cell_grid_data[i].next_state;
    }

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public override Element GetCellCurrentState(int i)
    {
        RefreshCellInfoData();
        return (Element)this.cell_grid_data[i].current_state;
    }


    /// <summary>
    ///     Returns a vector 3 where:
    ///         x is the current state
    ///         y is whether this cell was modified this frame
    ///         z is the previous state
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public override WorldCellInfo GetCellInfo(int i)
    {
        RefreshCellInfoData();
        return this.cell_grid_data[i];
    }

    /// <summary>
    ///     Set the current state of a cell. Also flags the cell as modified during this frame.
    /// </summary>
    /// <param name="i"></param>
    /// <param name="state"></param>
    public override void SetCellNextState(int i, Element state)
    {
        WorldCellInfo[] data = new WorldCellInfo[this.cell_grid_buffer.count];
        this.cell_grid_buffer.GetData(data);
        WorldCellInfo info = data[i];
        info.current_state = state;
        info.next_state = state;
        info.last_frame_modified = this.frame;
        data[i] = info;
        this.cell_grid_buffer.SetData(data);
    }


    //=====================


    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public override Element GetCellNextState(int x, int y, int z)
    {
        int i = VoxelUtils.Index_FlatFromint3(x, y, z, this.automaton_dimensions);
        return GetCellNextState(i);
    }

    /// <summary>
    ///     Get the current state of a cell
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public override Element GetCellCurrentState(int x, int y, int z)
    {
        int i = VoxelUtils.Index_FlatFromint3(x, y, z, this.automaton_dimensions);
        return GetCellCurrentState(i);
    }


    /// <summary>
    ///     Returns a vector 3 where:
    ///         x is the current state
    ///         y is whether this cell was modified this frame
    ///         z is the previous state
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public override WorldCellInfo GetCellInfo(int x, int y, int z)
    {
        int i = VoxelUtils.Index_FlatFromint3(x, y, z, this.automaton_dimensions);
        return GetCellInfo(i);
    }

    /// <summary>
    ///     Set the current state of a cell. Also flags the cell as modified during this frame.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="state"></param>
    public override void SetCellNextState(int x, int y, int z, Element state)
    {
        int i = VoxelUtils.Index_FlatFromint3(x, y, z, this.automaton_dimensions);
        SetCellNextState(i, state);
    }

    //=====================

    /// <summary>
    ///     Returns a vector 3 where:
    ///         x is the current state
    ///         y is whether this cell was modified this frame
    ///         z is the previous state
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public override WorldCellInfo GetCellInfo(int3 index)
    {
        return GetCellInfo(index.x, index.y, index.z);
    }


}
