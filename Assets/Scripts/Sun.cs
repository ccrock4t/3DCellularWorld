using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sun : MonoBehaviour
{

    Vector3 old_position;
    // Start is called before the first frame update
    void Start()
    {
        old_position = this.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if(Vector3.Distance(this.transform.position, old_position) > 0.001f)
        {
            if(GlobalConfig.voxel_processing_method == GlobalConfig.ProcessingMethod.GPU)
            {
                ((WorldAutomatonGPU)GlobalConfig.world_automaton).UpdateLightPosition();
            }
            old_position= this.transform.position;
        }
    }
}
