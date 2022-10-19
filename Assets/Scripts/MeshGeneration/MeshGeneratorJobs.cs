using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine;


[BurstCompile]
public struct EntityMeshGenerator : IJobEntityBatchWithIndex
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

            NativeArray<VertexAttributeDescriptor> VertexDescriptors = new(1, Allocator.Temp);
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
        NativeArray<float2> uv0 = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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
        NativeArray<float4> uv0 = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float3x2> uv1and2 = new(vertices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<uint> indicies = new(indiciesCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        for (uint v = 0; v < vertices.Length; v++)
        {
            uint x = v % (uint)settings.mapDimentions.x;
            uint y = v / (uint)settings.mapDimentions.x;
            int meshMapIndex = (int)y * settings.mapDimentions.x + (int)x;
            float3 pos = new(x, heightMap[(int)v].Value, y);

            uv0[meshMapIndex] = new float4(math.lerp(float2.zero, new(1), math.unlerp(float2.zero, settings.mapDimentions, new(x, y))), heightMap[(int)v].slopeBlend);

            uv1and2[meshMapIndex] = new()
            {
                c0 = heightMap[(int)v].upperLowerColours.c0.ToFloat3(),
                c1 = heightMap[(int)v].upperLowerColours.c1.ToFloat3()
            };
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
        // The advanced mesh api only allows 4 seperate data streams into the mesh
        // for using more than 4 vertex attributes its neccescary to wrap certain properties into a single struct and combine the
        // streams.
        // Here I am combining uv1 and uv2 into a float3x2 (two float3s) and writing that into stream 2.
        // a third colour component could be parsed in on stream 2 using a float3x3 which will be useful on the next shader.
        NativeArray<VertexAttributeDescriptor> VertexDescriptors = new(4, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4, 1);
        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3, 2);
        VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);
        Mesh.MeshData mesh = meshDataArray[internalMeshIndex];
        mesh.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        mesh.SetIndexBufferParams(indiciesCount, IndexFormat.UInt32);
        mesh.GetVertexData<float3>(0).CopyFrom(vertices);
        mesh.GetVertexData<float4>(1).CopyFrom(uv0);
        mesh.GetVertexData<float3x2>(2).CopyFrom(uv1and2);
        // mesh.GetVertexData<float3>(3).CopyFrom(uv1);
        mesh.GetIndexData<uint>().CopyFrom(indicies);
        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new(0, indiciesCount, MeshTopology.Triangles));
    }
}

/// <summary>
/// This job reaches the limit of what can reasonably be stored in the mesh data.
/// I wish to actually use terrain textures for grass/rock/mud instead of flat colours which involves having UV coordinates
/// for sampling the texture, currently all 4 uv channels I have access to in shader graph contain colours or
/// other data for the shader.
/// To solve this we need to move something out of the uv so we can fit float2 uv coordinates in.
/// Plan create and RGBA32 texture to store the uv0 setting values kept in that dimention.
/// 
/// We could probably move these into uv1,2 and 3 alpha channels and the unused channels in vertex colours,
/// but I wish to have the option of using colour alpha in the future.
/// 
/// Then we can use the uv coordinates there to now sample the texture for the values and additionally
/// sample terrain textures. we will have to add a blend value for blending between textures in the shader
/// This will be calculated in the heightMap layerer and stored in the vertex colour alpha channel
/// </summary>
[BurstCompile]
public struct MeshGeneratorABVC : IJob
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

        NativeArray<float3> vertices = new(heightMap.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float3> vertexColours = new(heightMap.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float4x4> uvs = new(heightMap.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<uint> indicies = new(indiciesCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        for (uint v = 0; v < vertices.Length; v++)
        {
            uint x = v % (uint)settings.mapDimentions.x;
            uint y = v / (uint)settings.mapDimentions.x;
            int meshMapIndex = (int)y * settings.mapDimentions.x + (int)x;

            HeightMapElement mapElement = heightMap[(int)v];

            vertexColours[meshMapIndex] = new(mapElement.rimPower, mapElement.rimFac, mapElement.absMaxHeight);

            uvs[meshMapIndex] = new()
            {
                c0 = new float4(mapElement.flatMaxHeight, mapElement.heightFade, mapElement.slopeBlend),
                c1 = mapElement.upperLowerColours.c0,
                c2 = mapElement.upperLowerColours.c1,
                c3 = mapElement.RimColour
            };

            vertices[meshMapIndex] = new float3(x, mapElement.Value, y);

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

        NativeArray<VertexAttributeDescriptor> VertexDescriptors = new(6, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 3, 1);
        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4, 2);
        VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4, 2);
        VertexDescriptors[4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4, 2);
        VertexDescriptors[5] = new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4, 2);
        Mesh.MeshData mesh = meshDataArray[internalMeshIndex];
        mesh.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        mesh.SetIndexBufferParams(indiciesCount, IndexFormat.UInt32);
        mesh.GetVertexData<float3>(0).CopyFrom(vertices);
        mesh.GetVertexData<float3>(1).CopyFrom(vertexColours);
        mesh.GetVertexData<float4x4>(2).CopyFrom(uvs);
        mesh.GetIndexData<uint>().CopyFrom(indicies);
        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new(0, indiciesCount, MeshTopology.Triangles));
    }
}

[BurstCompile]
public struct MeshGeneratorABVCT : IJob
{
    public RelativeNoiseData relativeHeightMapData;
    [ReadOnly]
    public MeshAreaSettings meshSettings;
    [ReadOnly]
    public NativeArray<HeightMapElement> heightMap;

    [WriteOnly]
    public NativeArray<float4> textureData;

    public int meshIndex;

    public Mesh.MeshDataArray meshDataArray;

    public void Execute()
    {
        int internalMeshIndex = meshIndex;
        MeshAreaSettings settings = meshSettings;
        int indiciesCount = (settings.mapDimentions.x - 1) * (settings.mapDimentions.y - 1) * 6;

        NativeArray<float3> vertices = new(heightMap.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float4> vertexColours = new(heightMap.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
//         NativeArray<float2> textureUVs = new(heightMap.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<float4x4> UVs = new(heightMap.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        NativeArray<uint> indicies = new(indiciesCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        for (uint v = 0; v < vertices.Length; v++)
        {
            uint x = v % (uint)settings.mapDimentions.x;
            uint y = v / (uint)settings.mapDimentions.x;
            int meshMapIndex = (int)y * settings.mapDimentions.x + (int)x;

            HeightMapElement mapElement = heightMap[(int)v];

            vertexColours[meshMapIndex] = new(mapElement.rimPower, mapElement.rimFac, mapElement.absMaxHeight,mapElement.secondaryBlendMul);
            textureData[meshMapIndex] = new float4(mapElement.flatMaxHeight, mapElement.heightFade, mapElement.slopeBlend);
            float2 textureUV = new()
            {
                x = math.unlerp(0, settings.mapDimentions.x -1, x ),
                y = math.unlerp(0, settings.mapDimentions.y -1, y )
            };
            UVs[meshMapIndex] = new()
            {
                c0 = new float4(textureUV, mapElement.mainTextureIndex, mapElement.secondaryTextureIndex),
                c1 = mapElement.upperLowerColours.c0,
                c2 = mapElement.upperLowerColours.c1,
                c3 = mapElement.RimColour
            };

            vertices[meshMapIndex] = new float3(x, mapElement.Value, y);

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

        NativeArray<VertexAttributeDescriptor> VertexDescriptors = new(6, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4, 2);
        VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4, 2);
        VertexDescriptors[4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4, 2);
        VertexDescriptors[5] = new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4, 2);
        Mesh.MeshData mesh = meshDataArray[internalMeshIndex];
        mesh.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        mesh.SetIndexBufferParams(indiciesCount, IndexFormat.UInt32);
        mesh.GetVertexData<float3>(0).CopyFrom(vertices);
        mesh.GetVertexData<float4>(1).CopyFrom(vertexColours);
        // mesh.GetVertexData<float2>(2).CopyFrom(textureUVs);
        mesh.GetVertexData<float4x4>(2).CopyFrom(UVs);
        mesh.GetIndexData<uint>().CopyFrom(indicies);
        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new(0, indiciesCount, MeshTopology.Triangles));
    }
}