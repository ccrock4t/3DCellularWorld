using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(PerlinGrapher))]
public class PerlinGrapherHandles : Editor
{
    void OnSceneGUI()
    {
        PerlinGrapher handle = (PerlinGrapher)target;
        if (handle == null)
        {
            return;
        }

        Handles.color = Color.blue;
        Handles.Label(handle.lr.GetPosition(0) + Vector3.up * 2,
            "Layer: " +
            handle.gameObject.name);
    }

}
