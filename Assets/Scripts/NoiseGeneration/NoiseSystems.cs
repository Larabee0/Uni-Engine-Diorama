using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using System;
using System.Runtime.CompilerServices;

public partial class NoiseGenSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem ecbEndSys;

    private EntityQuery GenerateHeightMapQuery;
    protected override void OnCreate()
    {
        var entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(UpdateMeshArea),
                typeof(MeshAreaSettings),
                typeof(MeshAreaRef),
                typeof(GenerateHeightMap),
                typeof(SimpleNoise)
            },
            None = new ComponentType[]
            {
                typeof(UpdatingMeshArea)
            }
        };
        GenerateHeightMapQuery = GetEntityQuery(entityQueries);

        ecbEndSys = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (!GenerateHeightMapQuery.IsEmpty)
        {
            EntityCommandBuffer ecbEnd = ecbEndSys.CreateCommandBuffer();
            NativeArray<Entity> entities = GenerateHeightMapQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<SimpleNoise> noiseComps =GenerateHeightMapQuery.ToComponentDataArray<SimpleNoise>(Allocator.TempJob);
            NativeArray<MeshAreaSettings> areaComps = GenerateHeightMapQuery.ToComponentDataArray<MeshAreaSettings>(Allocator.TempJob);

            for (int i = 0; i < entities.Length; i++)
            {
                NativeArray<HeightMapElement> heightMap = new(areaComps[i].mapDimentions.x * areaComps[i].mapDimentions.y, Allocator.TempJob);
                var heightMapGen = new SimpleNoiseHeightMapGenerator
                {
                    areaSettings = areaComps[i],
                    simpleNoise = noiseComps[i],
                    HeightMap = heightMap
                };

                heightMapGen.Schedule(heightMap.Length, 64).Complete();
                ecbEnd.SetBuffer<HeightMapElement>(entities[i]).CopyFrom(heightMap);
                ecbEnd.RemoveComponent<GenerateHeightMap>(entities[i]);
                heightMap.Dispose();


            }
        }
    }
}

public struct GenerateHeightMap : IComponentData { }

public struct HeightMapElement : IBufferElementData
{
    public float Value;
}

// Simple noise algorithim take from https://github.com/SebLague/Procedural-Planets under the MIT licence
// adapted for  2D height map generation by myself

[Serializable]
public struct SimpleNoise : IComponentData
{
    [Range(10, 500)]
    public int resolution;
    [Range(0.01f, 10f)]
    public float strength;
    [Range(1f, 8f)]
    public int numLayers;
    [Range(0.01f, 4f)]
    public float baseRoughness;
    [Range(0.01f, 4f)]
    public float roughness;
    [Range(0.01f, 4f)]
    public float persistence;
    public float2 centre;
    [Range(-4f, 4f)]
    public float minValue;
}

public struct SimpleNoiseHeightMapGenerator : IJobParallelFor
{
    public SimpleNoise simpleNoise;
    public MeshAreaSettings areaSettings;
    [WriteOnly]
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        float x = (float)index % areaSettings.mapDimentions.x;
        float y = (float)index / areaSettings.mapDimentions.x;

        float2 percent = new float2(x, y) / (simpleNoise.resolution - 1);

        float noiseValue = 0;
        float frequency = simpleNoise.baseRoughness;
        float amplitude = 1;

        for (int i = 0; i < simpleNoise.numLayers; i++)
        {
            float v = noise.cnoise(percent * frequency + simpleNoise.centre);
            noiseValue += (v + 1) * 0.5f * amplitude;
            frequency *= simpleNoise.roughness;
            amplitude *= simpleNoise.persistence;
        }
        HeightMapElement height;
        height.Value = (noiseValue - simpleNoise.minValue) * simpleNoise.strength;
        HeightMap[index] = height;
    }
}