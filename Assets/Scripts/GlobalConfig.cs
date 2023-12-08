using System;
using Unity.Mathematics;
using UnityEngine;

public class GlobalConfig : MonoBehaviour
{
    [Serializable]
    public enum ProcessingMethod
    {
        CPU,
        GPU
    }

    public enum SmoothingMethod
    {
        None,
        MarchingCubes
    }


    // === GPU ===
    // To maximize usage of the GPU, request the max number of threads per thread group (brand-dependent), and request the min number of thread groups per dispatch
    public const int MAX_NUM_OF_THREAD_GROUPS = 65535;  // can only have 65535 thread groups per Dispatch
    public const int NUM_OF_GPU_THREADS = 32; // AMD uses 64 threads per GPU thread group. NVIDIA uses 32.

    // ============

    // === World voxel automaton === 
    public static ProcessingMethod voxel_processing_method = ProcessingMethod.CPU;
    public static SmoothingMethod voxel_mesh_smoothing_method = SmoothingMethod.MarchingCubes;
    public GameObject world_automaton_game_object;
    public static WorldAutomaton world_automaton;
    public static readonly int3 WORLD_AUTOMATON_DIMENSIONS = new int3(32, 32, 32); // number cells in each dimension. Must be a multiple of 2
    public static float WORLD_AUTOMATA_SECONDS_PER_STEP = 0.01f; // number of seconds per step of automata
    // ============


    private void Awake()
    {
        WorldAutomaton world_automaton;

        switch (voxel_processing_method)
        {
            case ProcessingMethod.CPU:
                world_automaton = world_automaton_game_object.GetComponent<WorldAutomatonCPU>();
                break;
            case ProcessingMethod.GPU:
                world_automaton = world_automaton_game_object.GetComponent<WorldAutomatonGPU>();
                break;
            default:
                Debug.LogError("No voxel automaton component for the given processing method.");
                return;
        }

        world_automaton.enabled = true;


        GlobalConfig.world_automaton = world_automaton;
        GlobalConfig.world_automaton.Start();
    }
}