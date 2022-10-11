using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Rendering;
using Unity.Rendering;
using Unity.Entities.UniversalDelegates;

/// <summary>
/// In Unity ECS there are two types of component systems,
/// - those that inherit from ComponetSystem
/// - those that inherit from SystemBase
/// 
/// SystemBase systems are partial classes, ComponetSystem systems are not
/// both require the implementation of the OnUpdate method.
/// 
/// At a high level, SystemBase provides methods and properties that allow
/// close usage of the C# Jobs System either via structs that inherit from IJob*something*
/// or from Entities.ForEach Loops, which generate job structs in another code file externally,
/// which is why SystemBase systems are parital.
/// 
/// ComponentSystems don't have full provide proper frameworks for the C# Jobs system, but can still run
/// Entities.ForEach loops, they just won't be Burst accelerated or multi-threaded.
/// The trade off is they allow use of managed C# classes within their Entities.ForEach loops.
/// Generally I use ComponentSystem systems for player input, otherwise I always try to use
/// SystemBase systems.
/// </summary>
public partial class MeshAreaSystem : SystemBase
{
    private readonly List<MeshDataWrapper> meshDataWrappers = new();

    // Entitiy Queries are used to filter entities to a set of componenets they may contain.
    // they are needed to schedule C# jobs that implement IJobEntityBatch or IJobEntityBatchWithIndex
    // interface
    // Entities.ForEach loops build their own queries in declartion via the Entities.WithAll<*types>().Foreach()
    // syntax, their is also .WithAny<>() and .WithNone<>()
    private EntityQuery TriangulatorRunQuery;
    private EntityQuery TriangulatorCompleteQuery;
    private BufferTypeHandle<HeightMapElement> heightMapTypeHandle;
    // this is one of serveral command buffer systems in ECS. the end frame ECB.
    // command buffers are used to record changes to entities in the world then execute them all at once.
    // their is the begin frame, the end frame and the begin presentation command buffer systems.
    // in Game Object Unity, ecbBegin would happen at the start of Update(), ecbEnd and the end of of Update()
    // and ecbPresentation is LateUpdate()
    // their is also the ecbFixed, for the physics engine and equivlenent to FixedUpdate().
    // in ECS, the order goes Fixed Update > Update > Late Update (assuming 50fps)
    private EndSimulationEntityCommandBufferSystem ecbEndSys;

    // Entity queries should only be created in OnCreate, as they do not need updating ever,
    // making it pointless to do in OnUpdate
    protected override void OnCreate()
    {
        // queries have the All, Any and None property options
        // All = all entities must have the listed component types
        // Any = At least one of the component types must exist
        // None = None of these component types can exist
        var entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(UpdateMeshArea),
                typeof(MeshAreaSettings),
                typeof(MeshAreaRef),
                typeof(HeightMapElement)
            },
            None = new ComponentType[]
            {
                typeof(UpdatingMeshArea),
                typeof(GenerateHeightMap)
            }
        };
        TriangulatorRunQuery = GetEntityQuery(entityQueries);

        entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(UpdateMeshArea),
                typeof(MeshAreaSettings),
                typeof(MeshAreaRef),
                typeof(UpdatingMeshArea),
                typeof(MeshAreaTriangulatorRun)
            }
        };
        TriangulatorCompleteQuery = GetEntityQuery(entityQueries);

        // in order to create command buffers, you need a reference to one of the commandbuffer systems.
        // sometimes you will see me use ecbEnd and ecbBegin at once.
        ecbEndSys = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        heightMapTypeHandle = GetBufferTypeHandle<HeightMapElement>(true);
    }

    protected override void OnUpdate()
    {
        // for use in parallel C# Jobs the command buffer must be a parallel writer
        // I usually always create some command buffer at the start of OnUpdate
        EntityCommandBuffer.ParallelWriter ecbEnd = ecbEndSys.CreateCommandBuffer().AsParallelWriter();

        // Because this system deals with mesh creation, we need some access to managed C# classes as the Mesh
        // class is a class, but provides an system for doing directly mesh data streaming to the GPU.
        // via the advanced Mesh API, see https://docs.unity3d.com/ScriptReference/Mesh.html for more details.
        // All of this means I need to manually check the query for entities, to allocate the meshDataArray.
        if (!TriangulatorRunQuery.IsEmpty)
        {
            // C# Jobs in ECS do not always execute in the same frame they are scheduled and can infact execute,
            // at any time within 4 frames of schduleing.
            // however, you can only create, apply and dispose of MeshDataArray from managed C#
            
            // You also cannot store a MeshDataArray in a struct component, however they could be stored in a class,
            // compoment. However here I decided it was easier for the system to just keep track of all MeshDataArrays
            // in a list.

            // this system adds a witness component to each entity that has a mesh allocated for it this frame via
            // a time stamp, this is really only useful for debugging.

            double timeStamp = Time.ElapsedTime;
            // Native containers can be decalred to use 3 different allocator types, Temp, TempJob and Persistent.
            // Temps have a lifetime of 1 frame and do not need disposing.
            // TemJobs have a lifetime of 4 frames and do need disposing.
            // Persistent live infinitely until disposed.
            // the longer the allocator lifetime the slower the allocation time is.
            NativeArray<Entity> meshAreas = TriangulatorRunQuery.ToEntityArray(Allocator.Temp);
            NativeParallelHashSet<int> includedMeshes = new(meshAreas.Length, Allocator.Persistent);
            UpdatingMeshArea witness = new() { timeStamp = timeStamp };
            for (int i = 0; i < meshAreas.Length; i++)
            {
                // keeps track of the entity index this MeshDataArray will be associated with
                // adds component to that entity with the witness mark.
                // parallelWriter ECBs need a sort index provided - here I just used the entities index,
                // in an IJobEntityBatch I would use the BatchIndex
                includedMeshes.Add(meshAreas[i].Index);
                ecbEnd.AddComponent(meshAreas[i].Index, meshAreas[i], witness);
            }
            // here I create a MeshDataWrapper that contains the HashSet of entities, the time of creation and also the MeshDataArray.
            // Mesh.AllocateWritableMeshData() has some memory overhead, making it more efficient to allocate as many meshes as possible at once.
            MeshDataWrapper meshData = new(timeStamp, includedMeshes, Mesh.AllocateWritableMeshData(meshAreas.Length));
            meshDataWrappers.Add(meshData);


            // here the MeshGenerator Job is initilised and Scheduled.
            // this is an IJobEntityBatchWithIndex which can interact with the entity world directly and safely
            // either in parallel or serial.
            // to do this the job must have component type handles prased into it to access the relevent data
            // from the data chunks.
            // the components gotten here must be included in the entity query used to schedule the job.
            // they can be marked as read only if you never write to them
            // - this improves the efficieny of code generated by Burst.
            // finally if you want to make changes to components or entities not within the query, you need
            // to parse in an entity command buffer.
            heightMapTypeHandle.Update(this);
            var triangulatorJob = new EntityMeshGenerator
            {
                meshEntityTypeHandle = GetEntityTypeHandle(),
                meshSettingsTypeHandle = GetComponentTypeHandle<MeshAreaSettings>(true),
                meshDataArray = meshData.meshDataArray,
                heightMapTypeHandle = heightMapTypeHandle,
                ecbEnd = ecbEnd
            };
            // the Jobs system has a dependency system, which in entities 51.1 is majory simplified over entities 17.
            // here you take a property call dependency from System base, parse it into the Schedule method, and assign
            // the returned jobHandle to Dependency, the code behind will deal with the rest.
            Dependency = triangulatorJob.ScheduleParallel(TriangulatorRunQuery, Dependency);
        }


        // this query checks for when the mesh has been generated by the job, it needs to dispose of the MeshDataArray
        // and apply the meshes back to the entities they came from.
        if (!TriangulatorCompleteQuery.IsEmpty)
        {
            // the meshAreas have a class component I created that references the Mesh they display.
            NativeArray<Entity> meshAreas = TriangulatorCompleteQuery.ToEntityArray(Allocator.Temp);
            MeshAreaRef[] meshRefs = TriangulatorCompleteQuery.ToComponentDataArray<MeshAreaRef>();
            Mesh[] meshes = new Mesh[meshRefs.Length];
            // all entities within the query should in theory have the asme time stamp, so get taht from the first entity.
            double stamp = EntityManager.GetComponentData<UpdatingMeshArea>(meshAreas[0]).timeStamp;

            // iterate over all meshAreas, log if one has a mismatched timeStamp
            // remove the components that flag the two queries in this System to be active, and assgin the
            // contained mesh from the meshAreas to teh meshes array.
            for (int i = 0; i < meshAreas.Length; i++)
            {
                if (stamp != EntityManager.GetComponentData<UpdatingMeshArea>(meshAreas[i]).timeStamp)
                {
                    Debug.LogError("Missaligned chunks! Mesh applicaiton will fail");
                }
                ecbEnd.RemoveComponent<MeshAreaTriangulatorRun>(i, meshAreas[i]);
                ecbEnd.RemoveComponent<UpdatingMeshArea>(i, meshAreas[i]);
                ecbEnd.RemoveComponent<UpdateMeshArea>(i, meshAreas[i]);
                meshes[i] = meshRefs[i].Value;
            }

            // next find the meshDataWrapper from the meshDataWrappers list that matches the time stamp
            int wrapperIndex = meshDataWrappers.FindIndex(0, meshDataWrappers.Count, wrapper => wrapper.TimeStamp == stamp);
            if(wrapperIndex != -1)
            {
                // if we have found it we can now apply and dispose of the meshData and the areasIncluded HashSet.
                // according to the documentation so long as the same entities are within both this query and the previous one,
                // they will be in the same order, so no matching meshDataArray meshes to the meshes on the meshAreas should be needed.
                Mesh.ApplyAndDisposeWritableMeshData(meshDataWrappers[wrapperIndex].meshDataArray, meshes);
                meshDataWrappers[wrapperIndex].areasIncluded.Dispose();

                // with the native data now disposed we can remove the wrapper from the list.
                meshDataWrappers.RemoveAt(wrapperIndex);

                // because the advanced mesh API does not update the meshes normals and bounds when used,
                // we now need to go through and do that otherwise the mesh will not display properly.
                // the render bounds need to be manually set on the entitiy as does a the scale,
                // otherwise the bounds aren't used, this seems to be a bug with the hybrid renderer.
                // previously only the setting of the bounds was necessary.
                // we also use add instead of set for the scale as there isn't a guaratee the scale component
                // will be attached to the entity. It could be added during convertion, if not for that it 
                // prevents the bounds being updated if done during convertion again, seems to be a bug
                // with the hybrid renderer.
                for (int i = 0; i < meshAreas.Length; i++)
                {
                    meshes[i].RecalculateNormals();
                    meshes[i].RecalculateBounds();
                    AABB bounds = meshes[i].bounds.ToAABB();
                    ecbEnd.SetComponent(wrapperIndex, meshAreas[i], new RenderBounds { Value = bounds });
                    ecbEnd.AddComponent(wrapperIndex, meshAreas[i], new Scale { Value = 1f });
                }
            }
        }

        // when a C# Job is scheduled to use a command buffer either a job struct or entities.foreach loop,
        // we need to alert the command buffer system that created that command buffer a job will be writing to it.
        // this is done by adding the job handle to the system like below.
        ecbEndSys.AddJobHandleForProducer(Dependency);
    }

    [BurstCompile]
    private struct EntityMeshGenerator : IJobEntityBatchWithIndex
    {
        [ReadOnly]
        public EntityTypeHandle meshEntityTypeHandle;
        [ReadOnly]
        public ComponentTypeHandle<MeshAreaSettings> meshSettingsTypeHandle;
        [ReadOnly]
        public BufferTypeHandle<HeightMapElement> heightMapTypeHandle;

        [NativeDisableParallelForRestriction]
        public Mesh.MeshDataArray meshDataArray;

        public EntityCommandBuffer.ParallelWriter ecbEnd;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int indexOfFirstEntityInQuery)
        {
            NativeArray<Entity> meshEntities = batchInChunk.GetNativeArray(meshEntityTypeHandle);
            NativeArray<MeshAreaSettings> meshSettings = batchInChunk.GetNativeArray(meshSettingsTypeHandle);
            BufferAccessor<HeightMapElement> heightMapAccessor = batchInChunk.GetBufferAccessor(heightMapTypeHandle);

            for (int i = 0; i < meshEntities.Length; i++)
            {
                int internalMeshIndex = indexOfFirstEntityInQuery + i;
                MeshAreaSettings settings = meshSettings[i];
                int indiciesCount = (settings.mapDimentions.x - 1) * (settings.mapDimentions.y - 1) * 6;
                DynamicBuffer<HeightMapElement> heightMap = heightMapAccessor[i];
                NativeArray<float3> vertices = new(settings.mapDimentions.x * settings.mapDimentions.y, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                NativeArray<uint> indicies = new(indiciesCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                for (uint v = 0; v < vertices.Length; v++)
                {
                    uint x = v % (uint)settings.mapDimentions.x;
                    uint y = v / (uint)settings.mapDimentions.x;
                    int meshMapIndex = (int)y * settings.mapDimentions.x + (int)x;
                    float3 pos = new(x, heightMap[(int)v].Value, y);
                    vertices[meshMapIndex] = pos;
                    if (x != settings.mapDimentions.x - 1 && y != settings.mapDimentions.y - 1)
                    {
                        int t = ((int)y * (settings.mapDimentions.x - 1) + (int)x) * 3 * 2;

                        indicies[t + 0] = v + (uint)settings.mapDimentions.x;
                        indicies[t + 1] = v + (uint)settings.mapDimentions.x + 1;
                        indicies[t + 2] = v;

                        indicies[t + 3] = v + (uint)settings.mapDimentions.x + 1;
                        indicies[t + 4] = v + 1;
                        indicies[t + 5] = v;
                    }
                }

                NativeArray<VertexAttributeDescriptor>  VertexDescriptors = new (1, Allocator.Temp);
                VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
                Mesh.MeshData mesh = meshDataArray[internalMeshIndex];
                mesh.SetVertexBufferParams(vertices.Length, VertexDescriptors);
                mesh.SetIndexBufferParams(indiciesCount, IndexFormat.UInt32);
                mesh.GetVertexData<float3>(0).CopyFrom(vertices);
                mesh.GetIndexData<uint>().CopyFrom(indicies);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new(0, indiciesCount, MeshTopology.Triangles));

                ecbEnd.AddComponent<MeshAreaTriangulatorRun>(batchIndex, meshEntities[i]);
            }
        }
    }
}

public struct MeshDataWrapper
{
    public double TimeStamp;
    public NativeParallelHashSet<int> areasIncluded;
    public Mesh.MeshDataArray meshDataArray;

    public MeshDataWrapper(double timeStamp, NativeParallelHashSet<int> chunksIncluded, Mesh.MeshDataArray meshDataArray)
    {
        TimeStamp = timeStamp;
        this.areasIncluded = chunksIncluded;
        this.meshDataArray = meshDataArray;
    }
}

[BurstCompile]
public struct MeshGeneratorVC : IJob
{
    public RelativeNoiseData relativeHeightMapData;
    [ReadOnly]
    public MeshAreaSettings meshSettings;
    [ReadOnly]
    public NativeArray<HeightMapElement> heightMap;

    public int meshIndex;

    public Mesh.MeshDataArray meshDataArray;

    public void Execute()
    {
        int internalMeshIndex = meshIndex;
        MeshAreaSettings settings = meshSettings;
        int indiciesCount = (settings.mapDimentions.x - 1) * (settings.mapDimentions.y - 1) * 6;
        
        NativeArray<float3> vertices = new(settings.mapDimentions.x * settings.mapDimentions.y, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float4> vertexColours = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float2> uv0 = new (vertices.Length,Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<uint> indicies = new(indiciesCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        for (uint v = 0; v < vertices.Length; v++)
        {
            uint x = v % (uint)settings.mapDimentions.x;
            uint y = v / (uint)settings.mapDimentions.x;
            int meshMapIndex = (int)y * settings.mapDimentions.x + (int)x;
            float3 pos = new(x, heightMap[(int)v].Value, y);
            

            vertexColours[meshMapIndex] = heightMap[(int)v].Colour;
            uv0[meshMapIndex] = relativeHeightMapData.minMax;
            vertices[meshMapIndex] = pos;
            if (x != settings.mapDimentions.x - 1 && y != settings.mapDimentions.y - 1)
            {
                int t = ((int)y * (settings.mapDimentions.x - 1) + (int)x) * 3 * 2;

                indicies[t + 0] = v + (uint)settings.mapDimentions.x;
                indicies[t + 1] = v + (uint)settings.mapDimentions.x + 1;
                indicies[t + 2] = v;

                indicies[t + 3] = v + (uint)settings.mapDimentions.x + 1;
                indicies[t + 4] = v + 1;
                indicies[t + 5] = v;
            }
        }

        NativeArray<VertexAttributeDescriptor> VertexDescriptors = new(3, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2);
        Mesh.MeshData mesh = meshDataArray[internalMeshIndex];
        mesh.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        mesh.SetIndexBufferParams(indiciesCount, IndexFormat.UInt32);
        mesh.GetVertexData<float3>(0).CopyFrom(vertices);
        mesh.GetVertexData<float4>(1).CopyFrom(vertexColours);
        mesh.GetVertexData<float2>(2).CopyFrom(uv0);
        mesh.GetIndexData<uint>().CopyFrom(indicies);
        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new(0, indiciesCount, MeshTopology.Triangles));
    }
}


[BurstCompile]
public struct MeshGeneratorBVC : IJob
{
    public RelativeNoiseData relativeHeightMapData;
    [ReadOnly]
    public MeshAreaSettings meshSettings;
    [ReadOnly]
    public NativeArray<HeightMapElement> heightMap;

    public int meshIndex;

    public Mesh.MeshDataArray meshDataArray;

    public void Execute()
    {
        int internalMeshIndex = meshIndex;
        MeshAreaSettings settings = meshSettings;
        int indiciesCount = (settings.mapDimentions.x - 1) * (settings.mapDimentions.y - 1) * 6;

        NativeArray<float3> vertices = new(settings.mapDimentions.x * settings.mapDimentions.y, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float4> vertexColours = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float4> uv0 = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float4> uv1 = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<uint> indicies = new(indiciesCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        for (uint v = 0; v < vertices.Length; v++)
        {
            uint x = v % (uint)settings.mapDimentions.x;
            uint y = v / (uint)settings.mapDimentions.x;
            int meshMapIndex = (int)y * settings.mapDimentions.x + (int)x;
            float3 pos = new(x, heightMap[(int)v].Value, y);


            vertexColours[meshMapIndex] = heightMap[(int)v].Colour;
            uv0[meshMapIndex] = heightMap[(int)v].upperLowerColours.c0;
            uv1[meshMapIndex] = heightMap[(int)v].upperLowerColours.c1;
            vertices[meshMapIndex] = pos;
            if (x != settings.mapDimentions.x - 1 && y != settings.mapDimentions.y - 1)
            {
                int t = ((int)y * (settings.mapDimentions.x - 1) + (int)x) * 3 * 2;

                indicies[t + 0] = v + (uint)settings.mapDimentions.x;
                indicies[t + 1] = v + (uint)settings.mapDimentions.x + 1;
                indicies[t + 2] = v;

                indicies[t + 3] = v + (uint)settings.mapDimentions.x + 1;
                indicies[t + 4] = v + 1;
                indicies[t + 5] = v;
            }
        }

        NativeArray<VertexAttributeDescriptor> VertexDescriptors = new(4, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4, 2);
        VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4, 3);
        Mesh.MeshData mesh = meshDataArray[internalMeshIndex];
        mesh.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        mesh.SetIndexBufferParams(indiciesCount, IndexFormat.UInt32);
        mesh.GetVertexData<float3>(0).CopyFrom(vertices);
        mesh.GetVertexData<float4>(1).CopyFrom(vertexColours);
        mesh.GetVertexData<float4>(2).CopyFrom(uv0);
        mesh.GetVertexData<float4>(3).CopyFrom(uv1);
        mesh.GetIndexData<uint>().CopyFrom(indicies);
        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new(0, indiciesCount, MeshTopology.Triangles));
    }
}