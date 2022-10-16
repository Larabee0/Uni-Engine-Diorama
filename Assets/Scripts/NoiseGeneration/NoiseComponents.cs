
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum FirstLayer
{
    Simple,
    Rigid
}

public struct SimpleHeightMapWrapper
{
    public SimpleNoise simpleNoise;
    public RelativeNoiseData noiseData;
    public NativeArray<HeightMapElement> heightMap;

    public SimpleHeightMapWrapper(SimpleNoise simpleNoise, NativeArray<HeightMapElement> heightMap) : this()
    {
        this.simpleNoise = simpleNoise;
        this.heightMap = heightMap;
    }
}

public struct RigidHeightMapWrapper
{
    public RigidNoise rigidNoise;
    public RelativeNoiseData noiseData;
    public NativeArray<HeightMapElement> heightMap;
}

public struct GenerateHeightMap : IComponentData { }

public struct HeightMapElement : IBufferElementData
{
    public float Value;
    public float4 Colour;
    public float2 slopeBlend;
    public float4x2 upperLowerColours;
    public float4 RimColour;

    public float flatMaxHeight;
    public float heightFade;
    public float rimPower;
    public float rimFac;
    public float absMaxHeight;
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
    [Range(-10f, 10f)]
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
