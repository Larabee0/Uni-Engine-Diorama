using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum LayerType
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
public struct NoiseSettings
{
    public LayerType layerType;
    public CommonSettingsWrapper basicSettings;
    public SimpleNoise SimpleNoise => new() { commonSettings = basicSettings };
    public RigidNoise RigidNoise => new() { commonSettings = basicSettings, weightMultiplier = weightMultiplier,erosionSettings = erosionSettings };
    [Header("For Rigid Noise Only")]
    public float weightMultiplier;
    public ErodeSettings erosionSettings;
}

[Serializable]
public struct SimpleNoise : IComponentData
{
    [HideInInspector]
    public CommonSettingsWrapper commonSettings;


    public bool ClampToFloor => commonSettings.clampToFloor;
    public float FloorPercentage => commonSettings.floorPercentage;
    public float MinValue => commonSettings.minValue;
    public int Resolution => commonSettings.resolution;
    public float Strength => commonSettings.strength;
    public int NumLayers => commonSettings.numLayers;
    public float BaseRoughness => commonSettings.baseRoughness;
    public float Roughness => commonSettings.roughness;
    public float Persistence => commonSettings.persistence;
    public float2 Centre => commonSettings.centre;
    public float OffsetValue => commonSettings.offsetValue;

    public BVC BvcSettings => commonSettings.bvcSettings;
    public ABVC AbvcSettings => commonSettings.abvcSettings;
}

[Serializable]
public struct RigidNoise : IComponentData
{
    [HideInInspector]
    public CommonSettingsWrapper commonSettings;
    public float weightMultiplier;
    public ErodeSettings erosionSettings;


    public bool ClampToFloor => commonSettings.clampToFloor;
    public float FloorPercentage => commonSettings.floorPercentage;
    public float MinValue => commonSettings.minValue;
    public int Resolution => commonSettings.resolution;
    public float Strength => commonSettings.strength;
    public int NumLayers => commonSettings.numLayers;
    public float BaseRoughness => commonSettings.baseRoughness;
    public float Roughness => commonSettings.roughness;
    public float Persistence => commonSettings.persistence;
    public float2 Centre => commonSettings.centre;
    public float OffsetValue => commonSettings.offsetValue;
    public float WeightMultiplier => weightMultiplier;

    public BVC BvcSettings => commonSettings.bvcSettings;
    public ABVC AbvcSettings => commonSettings.abvcSettings;

}

[Serializable]
public struct CommonSettingsWrapper
{
    public bool clampToFloor;
    [Range(0f, 1f)]
    public float floorPercentage;
    [Range(-10f, 10f)]
    public float minValue;
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
    
    public float offsetValue;

    public BVC bvcSettings;
    public ABVC abvcSettings;
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


[System.Serializable]
public struct ErodeSettings
{
    public int mapSize;
    public int mapSizeWithBorder;
    public float startWater;
    public float startSpeed;
    public int maxLifetime;
    public float inertia;
    public int erosionBrushRadius;
    public float sedimentCapacityFactor;
    public float minSedimentCapacity;
    public float depositSpeed;
    public float erodeSpeed;
    public float gravity;
    public float evaporateSpeed;
    public uint baseSeed;
}
