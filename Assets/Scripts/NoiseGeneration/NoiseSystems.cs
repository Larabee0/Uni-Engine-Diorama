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
using static UnityEngine.Experimental.Rendering.RayTracingAccelerationStructure;

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
    public static implicit operator float(HeightMapElement v) { return v.Value; }
    public static implicit operator HeightMapElement(float v) { return new HeightMapElement { Value = v }; }
    public float Value;
}

// Simple noise algorithim take from https://github.com/SebLague/Procedural-Planets under the MIT licence
// adapted for 2D height map generation and C# Jobs by myself

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

[BurstCompile]
public struct SimpleNoiseHeightMapGenerator : IJobParallelFor
{
    public SimpleNoise simpleNoise;
    public MeshAreaSettings areaSettings;
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        float x = (float)index % areaSettings.mapDimentions.x;
        float y = (float)index / areaSettings.mapDimentions.x;

        float2 percent = new float2(x, y) / (simpleNoise.resolution - 1);

        float noiseValue = HeightMap[index];
        float frequency = simpleNoise.baseRoughness;
        float amplitude = 1;

        for (int i = 0; i < simpleNoise.numLayers; i++)
        {
            float v = noise.cnoise(percent * frequency + simpleNoise.centre);
            noiseValue += (v + 1) * 0.5f * amplitude;
            frequency *= simpleNoise.roughness;
            amplitude *= simpleNoise.persistence;
        }
        noiseValue -= simpleNoise.minValue;
        HeightMap[index] = noiseValue * simpleNoise.strength;
    }
}

// my own algorithims
[BurstCompile]
public struct HeightMapClamper : IJobParallelFor
{
    public RelativeNoiseData relativeNoiseData;
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        float value = HeightMap[index];
        float minValue = math.lerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, relativeNoiseData.flatFloor);
        HeightMap[index] = math.max(value, minValue) + (0.5f - ((minValue+relativeNoiseData.minMax.y)/2f));
    }
}

[BurstCompile]
public struct HeightMapMinMaxCal : IJob
{
    public NativeReference<RelativeNoiseData> minMax;
    [ReadOnly]
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute()
    {
        float2 minMax = new(float.MaxValue, float.MinValue);
        for (int i = 0; i < HeightMap.Length; i++)
        {
            float value = HeightMap[i];
            minMax.x = value < minMax.x ? value: minMax.x;
            minMax.y = value > minMax.y ? value : minMax.y;
        }
        RelativeNoiseData data = this.minMax.Value;
        data.minMax = minMax;
        data.mid = (minMax.x + minMax.y) / 2f;
        this.minMax.Value = data;
    }
}

/// <summary>
/// Job takes 2 height maps, and combines them together into one result
/// Currently this is weighted by the first height map,
/// closer to the ceiling of HM1 increases the weight to 1f for HM2
/// closer to the floor of HM1 lowers the weight to 0f for HM2
/// </summary>
[BurstCompile]
public struct HeightMapLayerer : IJobParallelFor
{
    public RelativeNoiseData relative1;
    public RelativeNoiseData relative2;

    [ReadOnly]
    public NativeArray<HeightMapElement> HeightMap1;
    [ReadOnly]
    public NativeArray<HeightMapElement> HeightMap2;

    [WriteOnly]
    public NativeArray<HeightMapElement> resultMap;
    public void Execute(int index)
    {
        float hm1 = HeightMap1[index];
        float hm2 = HeightMap2[index];

        float hm2Weight = math.unlerp(relative1.minMax.x, relative2.minMax.y, hm1);

        resultMap[index] = math.lerp(hm1, hm1+hm2, hm2Weight);

    }
}