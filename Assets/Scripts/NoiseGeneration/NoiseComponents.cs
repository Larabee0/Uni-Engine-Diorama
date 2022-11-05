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

public enum ShaderPicker
{
    BVC,
    ABVC,
    ABVCTextured
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

    public int mainTextureIndex;
    public int secondaryTextureIndex;
    public float secondaryBlendMul;

    public float flatMaxHeight;
    public float heightFade;
    public float rimPower;
    public float rimFac;
    public float absMaxHeight;
}

[Serializable]
public struct SimpleNoise : IComponentData
{
    [Range(0f, 1f)]
    public float floorPercentage;
    public bool clampToFloor;
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

    public BVC bvcSettings;
    public ABVC abvcSettings;
}

public struct CommonSettingsWrapper
{
    public bool clampToFloor;
    public float floorPercentage;
    public float minValue;

    public BVC bvcSettings;
    public ABVC abvcSettings;

    public CommonSettingsWrapper(SimpleNoise settings)
    {
        clampToFloor = settings.clampToFloor;
        floorPercentage = settings.floorPercentage;
        minValue = settings.minValue;
        bvcSettings = settings.bvcSettings;
        abvcSettings = settings.abvcSettings;
    }

    public CommonSettingsWrapper(RigidNoise settings)
    {
        clampToFloor = settings.clampToFloor;
        floorPercentage = settings.floorPercentage;
        minValue = settings.minValue;
        bvcSettings = settings.bvcSettings;
        abvcSettings = settings.abvcSettings;
    }
}

[Serializable]
public struct BVC
{
    public Color lower;
    public Color upper;

    [Range(0f, 1f)]
    public float slopeThreshold;
    [Range(0f, 1f)]
    public float blendAmount;
}

[Serializable]
public struct ABVC
{
    public Color flatColour;
    public Color mainColour;
    public Color rimColour;

    [SerializeField] private int mainTextureIndex;
    public int MainTextureIndex => mainTextureIndex+1;


    [Range(0f, 1f)]
    public float slopeThreshold;
    [Range(0f, 1f)]
    public float blendAmount;

    public float flatMaxHeight;
    public float heightFade;
    public float rimPower;
    [Range(0f, 1f)]
    public float rimFacraction;
    public float absolutelMaxHeight;
}


[Serializable]
public struct RigidNoise : IComponentData
{
    [Range(0f, 1f)]
    public float floorPercentage;
    public bool clampToFloor;
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

    public BVC bvcSettings;
    public ABVC abvcSettings;
}
