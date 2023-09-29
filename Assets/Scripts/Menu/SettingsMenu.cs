using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{

    Toggle CPU_toggle;
    Toggle GPU_toggle;

    // Start is called before the first frame update
    void Awake()
    {
        CPU_toggle = transform.Find("VoxelRenderingOption").Find("CPUToggle").GetComponent<Toggle>();
        GPU_toggle = transform.Find("VoxelRenderingOption").Find("GPUToggle").GetComponent<Toggle>();
        UpdateButtonsToMatchGlobalConfig();
    }

    void UpdateButtonsToMatchGlobalConfig()
    {
        if (GlobalConfig.voxel_processing_method == GlobalConfig.ProcessingMethod.CPU)
        {
            CPU_toggle.SetIsOnWithoutNotify(true);
            GPU_toggle.SetIsOnWithoutNotify(false);
        }
        else if (GlobalConfig.voxel_processing_method == GlobalConfig.ProcessingMethod.GPU)
        {
            CPU_toggle.SetIsOnWithoutNotify(false);
            GPU_toggle.SetIsOnWithoutNotify(true);
        }
        else
        {
            Debug.LogError("error");
        }
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    public void ChangeVoxelRenderMethod(string method)
    {
        if (method == "CPU")
        {
            GlobalConfig.voxel_processing_method = GlobalConfig.ProcessingMethod.CPU;
            Debug.Log("Changing processing method to CPU");
        }
        else if (method == "GPU")
        {
            GlobalConfig.voxel_processing_method = GlobalConfig.ProcessingMethod.GPU;
            Debug.Log("Changing processing method to GPU");
        }
        else
        {
            Debug.LogError("error");
        }
        UpdateButtonsToMatchGlobalConfig();
    }
}
