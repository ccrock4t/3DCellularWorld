using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Perlin3DGrapher : MonoBehaviour
{
    Vector3 dimensions = new(10, 10, 10);
    public float heightScale = 2;
    [Range(0.0f, 1.0f)]
    public float scale = 0.5f;
    public float heightOffset = 1.0f;
    public int octaves = 1;
    [Range(0.0f, 10.0f)]
    public float DrawCutoff = 1;

    void CreateCubes()
    {
        for (int z = 0; z < dimensions.z; z++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "perlin_cube";
                    cube.transform.parent = this.transform;
                    cube.transform.position = new Vector3(x, y, z);
                }
            }
        }
    }

    void Graph()
    {
        // destroy existing cubes
        MeshRenderer[] cubes = this.GetComponentsInChildren<MeshRenderer>();
        if (cubes.Length == 0) CreateCubes();

        if (cubes.Length == 0) return;

        for (int z = 0; z < dimensions.z; z++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    float p3d = VoxelUtils.FractalBrownianNoise3D(x, y, z, octaves, scale, heightScale, heightOffset);
                    if (p3d < DrawCutoff)
                        cubes[x + (int)dimensions.x * (y + (int)dimensions.z * z)].enabled = false;
                    else
                        cubes[x + (int)dimensions.x * (y + (int)dimensions.z * z)].enabled = true;
                }
            }
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnValidate()
    {
        Graph();
    }
}
