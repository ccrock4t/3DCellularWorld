using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    // ===== World Settings ======
    // Voxel rendering
    Toggle CPU_toggle;
    Toggle MarchingCubes_toggle;
    Toggle GPU_toggle;

    // Start is called before the first frame update
    void Awake()
    {
        CPU_toggle = transform.Find("VoxelRenderingOption").Find("CPUToggle").GetComponent<Toggle>();
        MarchingCubes_toggle = CPU_toggle.transform.Find("MarchingCubesToggle").GetComponent<Toggle>();
        GPU_toggle = transform.Find("VoxelRenderingOption").Find("GPUToggle").GetComponent<Toggle>();
        UpdateButtonsToMatchGlobalConfig();
    }

    void UpdateButtonsToMatchGlobalConfig()
    {
        CPU_toggle.SetIsOnWithoutNotify(GlobalConfig.voxel_processing_method == GlobalConfig.ProcessingMethod.CPU);
        GPU_toggle.SetIsOnWithoutNotify(GlobalConfig.voxel_processing_method == GlobalConfig.ProcessingMethod.GPU);
        MarchingCubes_toggle.SetIsOnWithoutNotify(GlobalConfig.voxel_mesh_smoothing_method == GlobalConfig.SmoothingMethod.MarchingCubes);
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    public void ChangeVoxelRenderMethod(string method)
    {
        if (method == "CPU")
        {
            // change method
            GlobalConfig.voxel_processing_method = GlobalConfig.ProcessingMethod.CPU;

            // allow marching cubes
            MarchingCubes_toggle.interactable = true;

            Debug.Log("Changing processing method to CPU");
        }
        else if (method == "GPU")
        {
            // change method
            GlobalConfig.voxel_processing_method = GlobalConfig.ProcessingMethod.GPU;

            // disable marching cubes 
            MarchingCubes_toggle.interactable = false;
            GlobalConfig.voxel_mesh_smoothing_method = GlobalConfig.SmoothingMethod.None;

            Debug.Log("Changing processing method to GPU");
        }else if(method == "MarchingCubes")
        {
            if (GlobalConfig.voxel_mesh_smoothing_method == GlobalConfig.SmoothingMethod.MarchingCubes)
            {
                GlobalConfig.voxel_mesh_smoothing_method = GlobalConfig.SmoothingMethod.None;
            }
            else if (GlobalConfig.voxel_mesh_smoothing_method == GlobalConfig.SmoothingMethod.None)
            {
                GlobalConfig.voxel_mesh_smoothing_method = GlobalConfig.SmoothingMethod.MarchingCubes;
            }
               
        }
        else
        {
            Debug.LogError("error");
        }
        UpdateButtonsToMatchGlobalConfig();
    }
}
