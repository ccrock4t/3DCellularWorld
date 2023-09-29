using System;
using Unity.Mathematics;
using UnityEngine;
using static WorldAutomaton.Elemental;


using WorldCellInfo = CellInfo<WorldAutomaton.Elemental.Element>;


public abstract class WorldAutomaton : VoxelAutomaton<Element>
{
    public struct Elemental 
    {
        public static int number_of_elements = Enum.GetValues(typeof(Element)).Length;
        public enum Element : int
        {
            Empty,
            Stone,
            Sand,
            Water,
            Lava,
            Steam
        };

        public static bool IsSolid(Element element)
        {
            return element == Element.Stone
                || element == Element.Sand;
        }

        public static bool IsLiquid(Element element)
        {
            return element == Element.Water
                || element == Element.Lava;
        }

        public static bool IsGas(Element element)
        {
            return element == Element.Steam;
        }
    }




    /*
     *  Automata data and config
     */    
    public int number_of_blocks;
    public int automaton_size;
    public int neighborhood_size;
    public Unity.Mathematics.Random random_number_generator;
    public GameObject light;

    [SerializeField] public int brush_size = 3; // 0 is 1 block, 1 is all neighbors in 1 block, 2 is all  neighbor in 2 blocks, etc.
    //graphers
    [SerializeField] public PerlinGrapher surface_grapher;

    public bool setup = false;

    // Start is called before the first frame update
    // initializes the automaton
    public void Start()
    {
        if (!setup)
        {
            this.automaton_dimensions = GlobalConfig.WORLD_AUTOMATON_DIMENSIONS;
            this.automaton_size = this.automaton_dimensions.x * this.automaton_dimensions.y * this.automaton_dimensions.z;
            this.neighborhood_size = 2 * 2 * 2;
            this.random_number_generator = new Unity.Mathematics.Random(1);
            (WorldCellInfo[] cell_grid, int3[] block_grid) = GenerateAndInitializeWorld();
            Setup(cell_grid, block_grid);
        }
    }

    private void Awake()
    {
        // limit framerate to prevent GPU from going overdrive for no reason
        QualitySettings.vSyncCount = 0;  // VSync must be disabled
        Application.targetFrameRate = 45;
    }


    /// <summary>
    /// Generate initial voxel information, and store indices of blocks
    /// </summary>
    /// <returns></returns>
    (WorldCellInfo[], int3[]) GenerateAndInitializeWorld()
    {
        int water_level = 4;

        WorldCellInfo[] cell_grid = new WorldCellInfo[this.automaton_size];
        int3[] block_grid = new int3[this.automaton_size / this.neighborhood_size];
        int block_grid_idx = 0;
        int3 index;
        WorldCellInfo value;
        Element state;
        // initial setting of cells
        for (int i = 0; i < this.automaton_size; i++)
        {
            index = VoxelUtils.Index_int3FromFlat(i, this.automaton_dimensions);

            int surface_height = (int)VoxelUtils.FractalBrownianNoise(index.x,
                index.z,
                surface_grapher.octaves,
                surface_grapher.scale,
                surface_grapher.heightScale,
                surface_grapher.heightOffset);



            if (index.y <= surface_height)
            {
                if (this.random_number_generator.NextFloat() <= surface_grapher.probability)
                {
                    state = Element.Stone;
                }
                else
                {
                    state = Element.Sand;
                }

            }
            else if (index.y < water_level)
            {
                state = Element.Water;
            }
            else
            {
                state = Element.Empty;
            }

            value = cell_grid[i];
            value.current_state = state;
            value.next_state = value.current_state;
            value.last_frame_modified = this.frame;



            cell_grid[i] = value;

            if (index.x % 2 == 0 && index.y % 2 == 0 && index.z % 2 == 0)
            {
                block_grid[block_grid_idx] = index;
                block_grid_idx++;
            }
        }

        return (cell_grid, block_grid);
    }


    public abstract void Setup(WorldCellInfo[] cell_grid, int3[] block_grid);



    public abstract void RunMouseButtonBehaviors(Element state, int brush_size);

    public abstract void CalculateAndRenderNextGridState();

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer > GlobalConfig.WORLD_AUTOMATA_SECONDS_PER_STEP)
        {
            CalculateAndRenderNextGridState();
            ProcessUserInput();
            this.frame++;
            timer = 0;
        }

    }

    private void ProcessUserInput()
    {
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            //RunMouseButtonBehaviors(CanvasNav.GetButtonCellState(), this.brush_size);
        }
    }






}
