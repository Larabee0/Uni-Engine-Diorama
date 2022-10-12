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
                    heightMap = heightMap
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
    public float4 Colour;
    public float2 slopeBlend;
    public float4x2 upperLowerColours;
}

// Simple noise algorithim take from https://github.com/SebLague/Procedural-Planets under the MIT licence
// adapted for 2D height map generation and C# Jobs by myself

[Serializable]
public struct SimpleNoise : IComponentData
{
    public bool clampToFlatFloor;
    [Range(2, 500)]
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
    public float offsetValue;
    [Range(-10f,10f)]
    public float minValue;
    [Range(-1f, 1f)]
    public float riseUp;

    [Range(0f, 1f)]
    public float slopeThreshold;
    [Range(0f, 1f)]
    public float blendAmount;

    public Color lower;
    public Color upper;
}

[Serializable]
public struct RigidNoise : IComponentData
{
    public bool clampToFlatFloor;
    [Range(2, 500)]
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
    public float offsetValue;
    [Range(-10f, 10f)]
    public float minValue;
    public float weightMultiplier;
}

[BurstCompile]
public struct SimpleNoiseHeightMapGenerator : IJobParallelFor
{
    public SimpleNoise simpleNoise;
    public MeshAreaSettings areaSettings;
    public NativeArray<HeightMapElement> heightMap;
    public void Execute(int index)
    {
        float x = (float)index % areaSettings.mapDimentions.x;
        float y = (float)index / areaSettings.mapDimentions.x;

        float2 percent = new float2(x, y) / (simpleNoise.resolution - 1);
        HeightMapElement element = heightMap[index];
        float noiseValue = element.Value;
        float frequency = simpleNoise.baseRoughness;
        float amplitude = 1;

        for (int i = 0; i < simpleNoise.numLayers; i++)
        {
            float v = noise.cnoise(percent * frequency + simpleNoise.centre);
            noiseValue += (v + 1) * 0.5f * amplitude;
            frequency *= simpleNoise.roughness;
            amplitude *= simpleNoise.persistence;
        }
        //noiseValue -= simpleNoise.offsetValue;
        element.Value = noiseValue * simpleNoise.strength;
        heightMap[index] = element;
    }
}

[BurstCompile]
public struct RigidNoiseHeightMapGenerator : IJobParallelFor
{
    public RigidNoise rigidNoise;
    public MeshAreaSettings areaSettings;
    public NativeArray<HeightMapElement> heightMap;
    public void Execute(int index)
    {
        float x = (float)index % areaSettings.mapDimentions.x;
        float y = (float)index / areaSettings.mapDimentions.x;

        float2 percent = new float2(x, y) / (rigidNoise.resolution - 1);

        HeightMapElement element = heightMap[index];
        float noiseValue = element.Value;
        float frequency = rigidNoise.baseRoughness;
        float amplitude = 1;
        float weight = 1;

        for (int i = 0; i < rigidNoise.numLayers; i++)
        {
            float v = 1 - math.abs(noise.cnoise(percent * frequency + rigidNoise.centre));
            v *= v;
            v *= weight;
            weight = math.clamp(v*rigidNoise.weightMultiplier,0f, 1f);

            noiseValue += v * amplitude;
            frequency *= rigidNoise.roughness;
            amplitude *= rigidNoise.persistence;
        }

        noiseValue -= rigidNoise.offsetValue;
        element.Value = noiseValue * rigidNoise.strength;
        heightMap[index] = element;
    }
}


// my own algorithims
[BurstCompile]
public struct HeightMapClamper : IJobParallelFor
{
    public Color floorColour;
    public RelativeNoiseData relativeNoiseData;
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        HeightMapElement element = HeightMap[index];
        float value = element.Value;
        float zeroOffset = (relativeNoiseData.minValue - relativeNoiseData.minMax.x);
        float minValue = math.lerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, relativeNoiseData.flatFloor);
        /// // VC
        /// if(element.Value < minValue)
        /// {
        ///     float colourWeight = math.clamp(minValue - element.Value, 0.0f, 1.0f);
        ///     element.Colour = math.lerp( element.Colour, (Vector4)floorColour, colourWeight);
        /// }

        if(element.Value < minValue)
        {
            float colourWeight = math.clamp(minValue - element.Value, 0.0f, 1.0f);
            element.Colour.x = colourWeight;
            element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, (Vector4)floorColour, colourWeight);
            // element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, heightMap[index].upperLowerColours.c1, colourWeight);
        }

        element.Value = math.max(value, minValue) + zeroOffset;
        

        HeightMap[index] = element;
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
            float value = HeightMap[i].Value;
            minMax.x = value < minMax.x ? value: minMax.x;
            minMax.y = value > minMax.y ? value : minMax.y;
        }
        RelativeNoiseData data = this.minMax.Value;
        data.minMax = minMax;
        data.mid = (minMax.x + minMax.y) / 2f;
        this.minMax.Value = data;
    }
}

[BurstCompile]
public struct HeightMapPainter : IJobParallelFor
{
    public SimpleNoise noiseSettings;
    public RelativeNoiseData relativeNoiseData;

    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        HeightMapElement element = HeightMap[index];
        float weight = math.unlerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, element.Value);

        element.Colour= (Vector4)Color.Lerp(noiseSettings.lower, noiseSettings.upper, weight);

        // BVC
        element.Colour.x = weight;
        element.slopeBlend =new( noiseSettings.slopeThreshold,noiseSettings.blendAmount);
        element.upperLowerColours = new float4x2((Vector4)noiseSettings.lower, (Vector4)noiseSettings.upper);
        HeightMap[index] = element;
    }
}



[BurstCompile]
public struct FillTexture : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<HeightMapElement> source;
    [WriteOnly]
    public NativeArray<Color32> Destination;
    public void Execute(int i)
    {
        Destination[i] = (Color)(Vector4)source[i].Colour;
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
    public RelativeNoiseData baseRelative;
     public RelativeNoiseData heightMapRelative;
    // public RelativeNoiseData relative2;

    [ReadOnly]
    public NativeArray<HeightMapElement> baseMap;

    [ReadOnly]
    public NativeArray<HeightMapElement> heightMap;

    public NativeArray<HeightMapElement> result;

    // BVC
    public void Execute(int index)
    {
        HeightMapElement element = result[index];
        float baseValue = baseMap[index].Value;
        float hm = heightMap[index].Value;

        float baseWeight = math.unlerp(baseRelative.minMax.x, baseRelative.minMax.y, baseValue);

        float mask = math.lerp(element.Value, element.Value + hm, baseWeight);

        if (element.Value < mask)
        {
            float extraWeight = math.clamp(mask - element.Value, 0.0f, 1.0f);
            element.Colour = math.lerp(element.Colour, heightMap[index].Colour, extraWeight);
            element.slopeBlend = math.lerp(element.slopeBlend, heightMap[index].slopeBlend, extraWeight);
            element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, heightMap[index].upperLowerColours.c0, extraWeight);
            element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, heightMap[index].upperLowerColours.c1, extraWeight);
        }
        element.Value = math.max(element.Value, mask);

        result[index] = element;
    }

    public void ExecuteVC(int index)
    {
        HeightMapElement element = result[index];
        float baseValue = baseMap[index].Value;
        float hm = heightMap[index].Value;

        float baseWeight = math.unlerp(baseRelative.minMax.x, baseRelative.minMax.y, baseValue);
        float hmWeight = math.unlerp(heightMapRelative.minMax.x, heightMapRelative.minMax.y, hm);
        float hmBaseWeight = math.unlerp(heightMapRelative.minMax.x, heightMapRelative.minMax.y, baseValue);

        float riseUp =  math.lerp(heightMapRelative.minValue, hmWeight, baseWeight) ;
        float mask = math.lerp(element.Value, element.Value+hm ,  baseWeight);
        float4 colourMask = math.lerp(element.Colour, heightMap[index].Colour, baseWeight);

        if (element.Value < mask)
        {
            float extraWeight = math.clamp(mask - element.Value, 0.0f, 1.0f);
            element.Colour = math.lerp(element.Colour, heightMap[index].Colour, extraWeight);
        }
        element.Value = math.max(element.Value, mask);

        //element.Colour = element.Value == mask ? colourMask:element.Colour;
        result[index] = element;
        //result[index] = math.lerp(result[index], hm* mask, hmWeight);
    }

    // not going to implement
    private Color BlendWithNeighbours()
    {
        // go throug the 4 neighbouring nodes and blend the colours between them and the current
        // this needst to be impemeneted in a seperate job run at the end  - run it in the texture generator.
        return  Color.white;
    }

    // thursday 06 october
    private void StartOfDayExecute(int index)
    {
        float baseValue = baseMap[index].Value;
        float hm = heightMap[index].Value;

        float baseWeight = math.unlerp(baseRelative.minMax.x, baseRelative.minMax.y, baseValue);
        float hmWeight = math.unlerp(heightMapRelative.minMax.x, heightMapRelative.minMax.y, baseValue);

        float riseUp = math.lerp(heightMapRelative.minValue, hmWeight, baseWeight);
        float mask = math.lerp(result[index].Value, hm + riseUp, hmWeight * baseWeight * riseUp);

        // result[index] +=  hm * baseValue;
        HeightMapElement element = result[index];
        element.Value = mask;
        result[index] = element;
        //result[index] = math.lerp(result[index], hm* mask, hmWeight);
    }
}