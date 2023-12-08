using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static WorldAutomaton.Elemental;

using WorldCellInfo = CellInfo<WorldAutomaton.Elemental.Element>;

/// <summary>
///     Computes and renders the voxel automaton using multithreading on CPU
/// </summary>
public class WorldAutomatonCPU : WorldAutomaton
{

    //CPU (mesh)
    public NativeArray<int3> block_grid; // stores index for corner of each 2x2x2 Margolus block (unshifted blocks)
    public NativeArray<float3x4> world_quad_vertices;

    MarchingCubes marching_cubes_object;

    public Material[] element_materials;
  

    public Dictionary<Element, List<GameObject>> meshGameObjects; // gameobjects for the mesh objects
    public Dictionary<Element, List<MeshFilter>> meshFilters; // filter component for the mesh objects
    public Mesh[] mesh_array;
    public List<Mesh> mesh_list; // a list of mesh data structures / objects that can be re-used each frame
    public Dictionary<Element, List<MeshRenderer>> meshRenderers;
    public Dictionary<Element, List<MeshCollider>> meshColliders;

    public NativeArray<int3> neighbor_offsets_int3; // so we dont have to keep making new objects, store coordinates here


    public override void Setup(WorldCellInfo[] cell_grid, int3[] block_grid)
    {

        this.transform.parent = null;
        // initialize
        this.cell_grid = new NativeArray<WorldCellInfo>(cell_grid,
            Allocator.Persistent);



        this.block_grid = new NativeArray<int3>(block_grid,
            Allocator.Persistent);


        // load resources
        this.element_materials = new Material[Elemental.number_of_elements];
        for (int i = 0; i < this.element_materials.Length; i++)
        {
            string element_name = System.Enum.GetName(typeof(Elemental.Element), i);
            this.element_materials[i] = (Material)Resources.Load("Materials/" + element_name);
        }

        // Set up mesh and job stuff
        this.meshGameObjects = new();
        this.meshFilters = new();
        this.meshRenderers = new();
        this.meshColliders = new();
        for(int i=0; i < Elemental.number_of_elements; i++) {
            Element element = (Element)i;
            this.meshGameObjects.Add(element, new List<GameObject>());
            this.meshRenderers.Add(element, new List<MeshRenderer>());
            this.meshFilters.Add(element, new List<MeshFilter>());
            this.meshColliders.Add(element, new List<MeshCollider>());
        }


        if (GlobalConfig.voxel_mesh_smoothing_method == GlobalConfig.SmoothingMethod.MarchingCubes)
        {
            this.marching_cubes_object = new();
        }
        else
        {
            this.world_quad_vertices = new NativeArray<float3x4>(this.automaton_size * 6, // size voxel, times 6 faces per voxel = 24 vertices per voxel
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            BuildVoxelWorldMesh buildjob = new()
            {
                automaton_dimensions = this.automaton_dimensions,
                vertices = this.world_quad_vertices
            };
            // precompute voxel vertices
            JobHandle buildJobHandle = buildjob.Schedule(this.cell_grid.Length, 256);
            buildJobHandle.Complete();
        }




        // precompute local Moore neighborhood index offsets for later use (from [-1,-1,-1] to [1,1,1])
        neighbor_offsets_int3 = new NativeArray<int3>(27, Allocator.Persistent);
        int key = 0;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    neighbor_offsets_int3[key] = new int3(x, y, z);
                    key++;
                }
            }
        }

        this.mesh_list = new();
        this.mesh_array = new Mesh[0];

        this.setup = true;
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
                    VoxelAutomaton<Element>.SetCellNextState(this.cell_grid, GlobalConfig.world_automaton.automaton_dimensions, this.frame, nx, ny, nz, state);
                    int i = VoxelUtils.Index_FlatFromint3(nx, ny, nz, GlobalConfig.world_automaton.automaton_dimensions);
                    WorldCellInfo value = this.cell_grid[i];
                    this.cell_grid[i] = value;
                }

            }
        }
    }


    private void ProcessUserInput()
    {
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            RunMouseButtonBehaviors(CanvasNav.GetButtonCellState(), this.brush_size);
        }
    }

    public void CalculateNextGridState()
    {
        ///
        /// Automaton
        ///

        // calculate next grid state
        ParallelCalculateNextWorldState job = new()
        {
            cell_grid = this.cell_grid,
            block_grid = this.block_grid,
            frame_mod2 = (byte)(this.frame % 2),
            neighbor_offsets_int3 = this.neighbor_offsets_int3,
            frame = this.frame,
            automaton_dimensions = this.automaton_dimensions
        };
        JobHandle jobHandle = job.Schedule(block_grid.Length, 256);
        jobHandle.Complete();
    }

    /// <summary>
    /// Render the next state of the automata
    /// </summary>
    public void RenderNextGridState()
    {
        // TODO below - filter out the vertices for all elements at once

        int vertex_multiplier = 4; // float3x4, so 4 vertices are stored per array element
        ArrayOfNativeList<float3x4> element_vertices = new();
        ArrayOfNativeListWriter<float3x4> element_vertices_writer = new();
        for (int i = 0; i<Elemental.number_of_elements; i++)
        {
            NativeList<float3x4> vertex_index_list = new(this.world_quad_vertices.Length, Allocator.TempJob);
            element_vertices[i] = vertex_index_list;
            element_vertices_writer[i] = vertex_index_list.AsParallelWriter();
        }

        // filter out the invisible vertex indices for each element, so we don't have to create tons of meshes that only hold invisible vertices
        ParallelFilterVoxelVertices jobVertexIndexFilter = new()
        {
            cell_grid = this.cell_grid,
            filtered_vertex_list_for_each_element = element_vertices_writer,
            unfiltered_quad_vertices = this.world_quad_vertices,
            automaton_dimensions = this.automaton_dimensions
        };
        JobHandle jobHandle = jobVertexIndexFilter.Schedule(this.world_quad_vertices.Length, 256);
        jobHandle.Complete();
         
        


        // for each element, determine if we need more mesh objects to render everything
        int total_number_of_meshes = 0;
        int element_number_of_meshes;
        int[] meshes_per_element = new int[Elemental.number_of_elements];
        for (int i = 0; i < number_of_elements; i++)
        {
            if (element_vertices[i].Length != 0)
            {
                element_number_of_meshes = Mathf.CeilToInt((float)element_vertices[i].Length * vertex_multiplier  / MAX_VERTICES_PER_MESH);
            }
            else
            {
                element_number_of_meshes = 0;
            }
            meshes_per_element[i] = element_number_of_meshes;
            total_number_of_meshes += element_number_of_meshes;
        }


        // Allocate mesh data
        Mesh.MeshDataArray job_mesh_data = Mesh.AllocateWritableMeshData(total_number_of_meshes);
        NativeArray<int2> mesh_index_to_element_idx_and_element = new NativeArray<int2>(total_number_of_meshes, Allocator.TempJob);
        Mesh.MeshData data;
        int z = 0;
        // for each element, create more mesh objects if needed. Also set the number of vertices to be allocated for each MeshDataArray.
        for (int i = 0; i < number_of_elements; i++)
        {
            Element element = (Element)i;
            element_number_of_meshes = meshes_per_element[i];

            // create new gameobject if needed
            while (element_number_of_meshes > this.meshGameObjects[element].Count)
            {
                GameObject meshGO = new GameObject(element.ToString() + " " + this.meshGameObjects[element].Count);
                this.meshFilters[element].Add(meshGO.AddComponent<MeshFilter>());
                this.meshRenderers[element].Add(meshGO.AddComponent<MeshRenderer>());
                
                this.meshGameObjects[element].Add(meshGO);
                this.meshRenderers[element][this.meshGameObjects[element].Count - 1].material = element_materials[(int)element];
            }

            int remaining_vertices = element_vertices[i].Length * vertex_multiplier; // 4 vertex per quad
            int vertices_to_allocate;
            int tris_to_allocate;

            for (int j = 0; j < element_number_of_meshes; j++)
            {
                // set parameters for the allocated parallel writable mesh data
                mesh_index_to_element_idx_and_element[z] = new int2(j, (int)element);
                vertices_to_allocate = (remaining_vertices > WorldAutomatonCPU.MAX_VERTICES_PER_MESH) ? WorldAutomatonCPU.MAX_VERTICES_PER_MESH : remaining_vertices;

                tris_to_allocate = (vertices_to_allocate / 4) * 6; // 6 triangle verts, for 4 vertices
                
  
                data = job_mesh_data[z];
                data.SetIndexBufferParams(tris_to_allocate, IndexFormat.UInt32);
                data.SetVertexBufferParams(vertices_to_allocate,
                    new VertexAttributeDescriptor(VertexAttribute.Position));
                //new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));

                z++;
                remaining_vertices -= vertices_to_allocate;
            }
        }

        // Now create or remove actual mesh objects
        while (this.mesh_list.Count < z)
        {
            Mesh mesh = new Mesh();
            this.mesh_list.Add(mesh);
        }

        // remove meshes if there are more meshes than needed
        while (this.mesh_list.Count > z)
        {
            this.mesh_list.RemoveAt(0);
        }

        // Get mesh data in parallel
        ParallelRenderNextGrid jobRender = new()
        {
            grid = this.cell_grid,
            element_vertex_lists = element_vertices,
            mesh_index_to_element_mesh_info = mesh_index_to_element_idx_and_element,
            job_mesh_data = job_mesh_data,
        };
        JobHandle jobRenderHandle = jobRender.Schedule(total_number_of_meshes, 256);
        jobRenderHandle.Complete();

        // clean up NativeArray
        mesh_index_to_element_idx_and_element.Dispose();
        for (int i = 0; i < Elemental.number_of_elements; i++)
        {
            element_vertices[i].Dispose();
        }

        z = 0;
        for (int i = 0; i < number_of_elements; i++)
        {
            element_number_of_meshes = meshes_per_element[i];
            for (int j = 0; j < element_number_of_meshes; j++)
            {
                data = job_mesh_data[z];
                data.subMeshCount = 1;
                data.SetSubMesh(0, new SubMeshDescriptor(0, data.GetIndexData<UInt32>().Length));
                z++;
            }

        }

        // if the list has changed, copy meshes to a new array so we can write mesh data to them
        if (this.mesh_array.Length != this.mesh_list.Count)
        {
            this.mesh_array = this.mesh_list.ToArray();
        }


        // apply mesh data to meshes
        Mesh.ApplyAndDisposeWritableMeshData(job_mesh_data, this.mesh_array); //MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

        // recalculate bounds of the meshes
        z = 0;
        for (int i = 0; i < number_of_elements; i++)
        {

            Element element = (Element)i;
            element_number_of_meshes = meshes_per_element[i];
            for (int j = 0; j < element_number_of_meshes; j++)
            {
                this.mesh_array[z].RecalculateNormals();
                this.mesh_array[z].RecalculateBounds();
                MeshFilter mf = this.meshFilters[element][j];
                MeshRenderer mr = this.meshRenderers[element][j];
                mr.material = element_materials[(int)element];
                mf.sharedMesh = this.mesh_array[z];
                z++;
            }
        }
    }

    /// <summary>
    /// Render the next state of the automata (marching cubes version)
    /// </summary>
    public void RenderNextGridStateMarchingCubes()
    {

        int vertex_multiplier = 12; // float3x4, so 4 vertices are stored per array element
        ArrayOfNativeList<(int, float3x12, int4x4)> element_vertices = new();
        ArrayOfNativeListWriter<(int, float3x12, int4x4)> element_vertices_writers = new();
        for (int i = 0; i < Elemental.number_of_elements; i++)
        {
            NativeList<(int, float3x12, int4x4)> vertex_index_list = new(initialCapacity: this.automaton_size, Allocator.TempJob);
            element_vertices[i] = vertex_index_list;
            element_vertices_writers[i] = vertex_index_list.AsParallelWriter();
        }

        BuildVoxelWorldMeshMarchingCubes jobVertexIndexFilter = new()
        {
            automaton_dimensions = this.automaton_dimensions,
            cell_grid = this.cell_grid,
            vertices_and_triangles_writers = element_vertices_writers,
            native_edgeTable = this.marching_cubes_object.native_edgeTable,
            native_triTable = this.marching_cubes_object.native_triTable,
            native_edges = this.marching_cubes_object.native_edges
        };
        JobHandle jobHandle = jobVertexIndexFilter.Schedule(this.automaton_size, 256);
        jobHandle.Complete();

   
        // for each element, determine if we need more mesh objects to render everything
        int total_number_of_meshes = 0;
        int element_number_of_meshes;
        int[] meshes_per_element = new int[Elemental.number_of_elements];
        for (int i = 0; i < number_of_elements; i++)
        {
            if (element_vertices[i].Length != 0)
            {
                element_number_of_meshes = Mathf.CeilToInt((float)element_vertices[i].Length * vertex_multiplier / MAX_VERTICES_PER_MESH);
            }
            else
            {
                element_number_of_meshes = 0;
            }
            meshes_per_element[i] = element_number_of_meshes;
            total_number_of_meshes += element_number_of_meshes;
        }


        // Allocate mesh data
        Mesh.MeshDataArray job_mesh_data = Mesh.AllocateWritableMeshData(total_number_of_meshes);
        NativeArray<int2> mesh_index_to_element_idx_and_element = new NativeArray<int2>(total_number_of_meshes, Allocator.TempJob);
        Mesh.MeshData data;
        int z = 0;
        // for each element, create more mesh objects if needed. Also set the number of vertices to be allocated for each MeshDataArray.
        for (int i = 0; i < number_of_elements; i++)
        {
            Element element = (Element)i;
            element_number_of_meshes = meshes_per_element[i];

            // create new gameobject if needed
            while (element_number_of_meshes > this.meshGameObjects[element].Count)
            {
                GameObject meshGO = new GameObject(element.ToString() + " " + this.meshGameObjects[element].Count);
                this.meshFilters[element].Add(meshGO.AddComponent<MeshFilter>());
                this.meshRenderers[element].Add(meshGO.AddComponent<MeshRenderer>());

                this.meshGameObjects[element].Add(meshGO);
                this.meshRenderers[element][this.meshGameObjects[element].Count - 1].material = element_materials[(int)element];
            }

            int remaining_voxels = element_vertices[i].Length;
            int remaining_vertices = remaining_voxels * vertex_multiplier; // 12 vertex per voxel
            int vertices_to_allocate;
            int tris_to_allocate;

            for (int j = 0; j < element_number_of_meshes; j++)
            {
                // set parameters for the allocated parallel writable mesh data
                mesh_index_to_element_idx_and_element[z] = new int2(j, (int)element);
                vertices_to_allocate = (remaining_vertices > WorldAutomatonCPU.MAX_VERTICES_PER_MESH) ? WorldAutomatonCPU.MAX_VERTICES_PER_MESH : remaining_vertices;

                tris_to_allocate = remaining_voxels * 15; // 15 triangle indices
  
                data = job_mesh_data[z];
                data.SetIndexBufferParams(tris_to_allocate, IndexFormat.UInt32);
                data.SetVertexBufferParams(vertices_to_allocate,
                    new VertexAttributeDescriptor(VertexAttribute.Position));
                //new VertexAttributeDescriptor(VertexAttribute.Normal, stream: 1));

                z++;
                remaining_vertices -= vertices_to_allocate;
            }
        }

        // Now create or remove actual mesh objects
        while (this.mesh_list.Count < z)
        {
            Mesh mesh = new Mesh();
            this.mesh_list.Add(mesh);
        }

        // remove meshes if there are more meshes than needed
        while (this.mesh_list.Count > z)
        {
            this.mesh_list.RemoveAt(0);
        }


        // Get mesh data in parallel
        ParallelRenderNextGridMarchingCubes jobRender = new()
        {
            grid = this.cell_grid,
            mesh_index_to_element_mesh_info = mesh_index_to_element_idx_and_element,
            job_mesh_data = job_mesh_data,
            element_vertices_and_triangles = element_vertices
        };

        JobHandle jobRenderHandle = jobRender.Schedule(total_number_of_meshes, 256);
        jobRenderHandle.Complete();

        // clean up NativeArray
        mesh_index_to_element_idx_and_element.Dispose();
        for (int i = 0; i < Elemental.number_of_elements; i++)
        {
            element_vertices[i].Dispose();
        }

        z = 0;
        for (int i = 0; i < number_of_elements; i++)
        {
            element_number_of_meshes = meshes_per_element[i];
            for (int j = 0; j < element_number_of_meshes; j++)
            {
                data = job_mesh_data[z];
                data.subMeshCount = 1;
                data.SetSubMesh(0, new SubMeshDescriptor(0, data.GetIndexData<UInt32>().Length));
                z++;
            }

        }

        // if the list has changed, copy meshes to a new array so we can write mesh data to them
        if (this.mesh_array.Length != this.mesh_list.Count)
        {
            this.mesh_array = this.mesh_list.ToArray();
        }


        // apply mesh data to meshes
        Mesh.ApplyAndDisposeWritableMeshData(job_mesh_data, this.mesh_array); //MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

        // recalculate bounds of the meshes
        z = 0;
        for (int i = 0; i < number_of_elements; i++)
        {

            Element element = (Element)i;
            element_number_of_meshes = meshes_per_element[i];
            for (int j = 0; j < element_number_of_meshes; j++)
            {
                this.mesh_array[z].RecalculateNormals();
                this.mesh_array[z].RecalculateBounds();
                MeshFilter mf = this.meshFilters[element][j];
                MeshRenderer mr = this.meshRenderers[element][j];
                mr.material = element_materials[(int)element];
                mf.sharedMesh = this.mesh_array[z];
                z++;
            }
        }
    }

    /// <summary>
    /// Calculate the next state of the automata
    /// </summary>
    public override void CalculateAndRenderNextGridState()
    {
        CalculateNextGridState();
        if(GlobalConfig.voxel_mesh_smoothing_method == GlobalConfig.SmoothingMethod.None)
        {
            RenderNextGridState();
        }
        else if (GlobalConfig.voxel_mesh_smoothing_method == GlobalConfig.SmoothingMethod.MarchingCubes)
        {
            RenderNextGridStateMarchingCubes();
        }
        else
        {
            Debug.LogError("error");
        }
        
    }



    private void OnApplicationQuit()
    {
 
        try
        {
            this.cell_grid.Dispose();
        }
        catch
        {

        }


        try
        {
            this.block_grid.Dispose();
        }
        catch
        {

        }

        try
        {
            this.neighbor_offsets_int3.Dispose();
        }
        catch
        {

        }

        try
        {
            this.world_quad_vertices.Dispose();
        }
        catch
        {

        }

        try
        {
            this.marching_cubes_object.native_edgeTable.Dispose();
            this.marching_cubes_object.native_triTable.Dispose();
            this.marching_cubes_object.native_edges.Dispose();
        }
        catch
        {

        }
        
    }



    public static readonly int3[] voxel_vertices = new int3[] {
        new(0, 0, 1), //p0
         new(1, 0, 1), //p1
         new(1, 0, 0), //p2
         new(0, 0, 0), //p3
         new(0, 1, 1), //p4
         new(1, 1, 1), //p5
         new(1, 1, 0), //p6
         new(0, 1, 0) // p7
    };
    public const int MAX_VERTICES_PER_MESH = 65532; // divisible by 4 (number of vertices in a quad) so vertices and tris wont get truncated

    public struct float3x12
    {
        float3x4 x;
        float3x4 y;
        float3x4 z;

        public float3x12(float3x4 a, float3x4 b, float3x4 c)
        {
            this.x = a;
            this.y = b;
            this.z = c;
        }

        public float3 this[int index]
        {
            get
            {
                if (index < 4) return this.x[index];
                else if (index < 8) return this.y[index % 4];
                else return this.z[index % 4];
            }

            set
            {
                if (index < 4) this.x[index] = value;
                else if (index < 8) this.y[index % 4] = value;
                else this.z[index % 4] = value;
            }
        }
    }

    public struct ArrayOfNativeList<T> where T : unmanaged
    {

        public NativeList<T> element0_mesh_vertex_data_writer;
        public NativeList<T> element1_mesh_vertex_data_writer;
        public NativeList<T> element2_mesh_vertex_data_writer;
        public NativeList<T> element3_mesh_vertex_data_writer;
        public NativeList<T> element4_mesh_vertex_data_writer;
        public NativeList<T> element5_mesh_vertex_data_writer;
        public int Length;


        public NativeList<T> this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return this.element0_mesh_vertex_data_writer;
                        break;
                    case 1:
                        return this.element1_mesh_vertex_data_writer;
                        break;
                    case 2:
                        return this.element2_mesh_vertex_data_writer;
                        break;
                    case 3:
                        return this.element3_mesh_vertex_data_writer;
                        break;
                    case 4:
                        return this.element4_mesh_vertex_data_writer;
                        break;
                    case 5:
                        return this.element5_mesh_vertex_data_writer;
                        break;
                    default:
                        Debug.LogError("error");
                        return this.element0_mesh_vertex_data_writer;
                        break;
                }
            }

            set
            {
                switch (index)
                {
                    case 0:
                        this.element0_mesh_vertex_data_writer = value;
                        break;
                    case 1:
                        this.element1_mesh_vertex_data_writer = value;
                        break;
                    case 2:
                        this.element2_mesh_vertex_data_writer = value;
                        break;
                    case 3:
                        this.element3_mesh_vertex_data_writer = value;
                        break;
                    case 4:
                        this.element4_mesh_vertex_data_writer = value;
                        break;
                    case 5:
                        this.element5_mesh_vertex_data_writer = value;
                        break;
                    default:
                        Debug.LogError("error");
                        this.element0_mesh_vertex_data_writer = value;

                        break;
                }
            }
        }
    }

    public struct ArrayOfNativeListWriter<T> where T : unmanaged
    {

        public NativeList<T>.ParallelWriter element0_mesh_vertex_data_writer;
        public NativeList<T>.ParallelWriter element1_mesh_vertex_data_writer;
        public NativeList<T>.ParallelWriter element2_mesh_vertex_data_writer;
        public NativeList<T>.ParallelWriter element3_mesh_vertex_data_writer;
        public NativeList<T>.ParallelWriter element4_mesh_vertex_data_writer;
        public NativeList<T>.ParallelWriter element5_mesh_vertex_data_writer;
        public int Length;


        public NativeList<T>.ParallelWriter this[int index]
        {
            get
            {
                NativeList<T>.ParallelWriter writer;
                switch (index)
                {
                    case 0:
                        writer = this.element0_mesh_vertex_data_writer;
                        break;
                    case 1:
                        writer = this.element1_mesh_vertex_data_writer;
                        break;
                    case 2:
                        writer = this.element2_mesh_vertex_data_writer;
                        break;
                    case 3:
                        writer = this.element3_mesh_vertex_data_writer;
                        break;
                    case 4:
                        writer = this.element4_mesh_vertex_data_writer;
                        break;
                    case 5:
                        writer = this.element5_mesh_vertex_data_writer;
                        break;
                    default:
                        Debug.LogError("error");
                        writer = this.element0_mesh_vertex_data_writer;
                        break;
                }
                return writer;
            }

            set
            {
                switch (index)
                {
                    case 0:
                        this.element0_mesh_vertex_data_writer = value;
                        break;
                    case 1:
                        this.element1_mesh_vertex_data_writer = value;
                        break;
                    case 2:
                        this.element2_mesh_vertex_data_writer = value;
                        break;
                    case 3:
                        this.element3_mesh_vertex_data_writer = value;
                        break;
                    case 4:
                        this.element4_mesh_vertex_data_writer = value;
                        break;
                    case 5:
                        this.element5_mesh_vertex_data_writer = value;
                        break;
                    default:
                        Debug.LogError("error");
                        this.element0_mesh_vertex_data_writer = value;

                        break;
                }
            }
        }
    }

    public struct ArrayOfNativeArray<T> where T : struct
    {

        public NativeArray<T> element0_mesh_vertex_data_writer;
        public NativeArray<T> element1_mesh_vertex_data_writer;
        public NativeArray<T> element2_mesh_vertex_data_writer;
        public NativeArray<T> element3_mesh_vertex_data_writer;
        public NativeArray<T> element4_mesh_vertex_data_writer;
        public NativeArray<T> element5_mesh_vertex_data_writer;
        public int Length;


        public NativeArray<T> this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return this.element0_mesh_vertex_data_writer;
                        break;
                    case 1:
                        return this.element1_mesh_vertex_data_writer;
                        break;
                    case 2:
                        return this.element2_mesh_vertex_data_writer;
                        break;
                    case 3:
                        return this.element3_mesh_vertex_data_writer;
                        break;
                    case 4:
                        return this.element4_mesh_vertex_data_writer;
                        break;
                    case 5:
                        return this.element5_mesh_vertex_data_writer;
                        break;
                    default:
                        Debug.LogError("error");
                        return this.element0_mesh_vertex_data_writer;
                        break;
                }
            }

            set
            {
                switch (index)
                {
                    case 0:
                        this.element0_mesh_vertex_data_writer = value;
                        break;
                    case 1:
                        this.element1_mesh_vertex_data_writer = value;
                        break;
                    case 2:
                        this.element2_mesh_vertex_data_writer = value;
                        break;
                    case 3:
                        this.element3_mesh_vertex_data_writer = value;
                        break;
                    case 4:
                        this.element4_mesh_vertex_data_writer = value;
                        break;
                    case 5:
                        this.element5_mesh_vertex_data_writer = value;
                        break;
                    default:
                        Debug.LogError("error");
                        this.element0_mesh_vertex_data_writer = value;

                        break;
                }
            }
        }
    }



}
