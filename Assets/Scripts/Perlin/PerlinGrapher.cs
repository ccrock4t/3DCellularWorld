using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PerlinGrapher : MonoBehaviour
{
    public LineRenderer lr;
    public float heightScale = 2;
    [Range(0.0f, 1.0f)]
    public float scale = 0.5f;
    public float heightOffset = 1.0f;
    public int octaves = 1;
    [Range(0.0f, 1.0f)]
    public float probability = 1;

    // Start is called before the first frame update
    void Start()
    {
        Graph();
    }

    void Graph()
    {
        lr = this.GetComponent<LineRenderer>();
        lr.positionCount = 100;
        int z = 11;
        Vector3[] positions = new Vector3[lr.positionCount];
        for(int x = 0; x < lr.positionCount; x++)
        {
            float y = VoxelUtils.FractalBrownianNoise(x, z, octaves, scale, heightScale, heightOffset);
            positions[x] = new(x, y, z);
        }
        lr.SetPositions(positions);
    }

    private void OnValidate()
    {
        Graph();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
