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
using Cinemachine;
using static UnityEditor.Rendering.CameraUI;

/// <summary>
/// IConvertGameObjectToEntity gets called when a gameobject converts into an entity
/// and allows you to add extra componenets during the converstion system - during which time you can
/// still access all game object components and transform hierarchy.
/// this method runs all on instances of the component implementing it in the scene.
/// </summary>
public class MeshArea : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshAreaSettings mapSettings;

    [SerializeField] private SimpleNoise[] simpleLayers;
    [Space(150f)]
    [SerializeField] private RigidNoise rigidNoise;

    [SerializeField] private RelativeNoiseData relativeNoiseData1;
    [SerializeField] private RelativeNoiseData relativeNoiseData2;


    [SerializeField] private FirstLayer singleLayer;
    [SerializeField] private bool UpdateOnChange;

    private Mesh activeMesh;
    private Material meshMat;
    private Texture2D texture;

    private void Awake()
    {
        meshFilter=GetComponent<MeshFilter>();
        meshFilter.mesh = new Mesh() { subMeshCount = 1, name = "MeshArea" };
    }

    private void OnValidate()
    {
        if (UpdateOnChange)
        {
            Generate();
        }
    }

    public void Generate()
    {
        NativeArray<HeightMapElement> result = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob);
        NativeArray<HeightMapElement> baseMap = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob);

        GenerateSimpleMaps(baseMap,result, true);
        baseMap.Dispose();
        GenerateHeightMapMesh(result);
    }

    public void GenerateSimpleMaps(NativeArray<HeightMapElement> baseMap, NativeArray<HeightMapElement> result, bool firstLayer =false)
    {
        for (int i = 0; i < simpleLayers.Length; i++)
        {
            SimpleNoise settings = simpleLayers[i];
            if (i > 0 ^!firstLayer)
            {
                NativeArray<HeightMapElement> current = new(result.Length, Allocator.TempJob);
                GenerateHeightMap(current, settings);
                if (settings.clampToFlatFloor)
                {
                    ClampToFlatFloor(current, settings);
                }
                LayerTwoHeightMaps(new(settings,baseMap),new(settings, current), new(settings, result));
                current.Dispose();
            }
            else
            {
                GenerateHeightMap(baseMap, settings);
                if (settings.clampToFlatFloor)
                {
                    ClampToFlatFloor(baseMap, settings);
                }
                result.CopyFrom(baseMap);
            }
        }
    }

    private void LayerTwoHeightMaps(SimpleHeightMapWrapper baseMap, SimpleHeightMapWrapper newMap, SimpleHeightMapWrapper result)
    {

        baseMap.noiseData =relativeNoiseData1= CalculateRelativeNoiseData(baseMap.heightMap);
        newMap.noiseData = relativeNoiseData2 = CalculateRelativeNoiseData(newMap.heightMap);
        newMap.noiseData.minValue = newMap.simpleNoise.riseUp;
        var layerer = new HeightMapLayerer
        {
            baseRelative = baseMap.noiseData,
            heightMapRelative = newMap.noiseData,
            baseMap = baseMap.heightMap,
            heightMap = newMap.heightMap,
            result = result.heightMap
        };
        layerer.Schedule(result.heightMap.Length, 64).Complete();
    }

    /// <summary>
    /// Generates heightMap from given nativearray, disposes of array when finished.
    /// </summary>
    /// <param name="heightMap">Height map to produce mesh of</param>
    public void GenerateHeightMapMesh(NativeArray<HeightMapElement> heightMap)
    {
        if(activeMesh == null)
        {
            activeMesh = meshFilter.sharedMesh = new Mesh() { subMeshCount = 1, name = "MeshArea" };
        }
        else
        {
            activeMesh.Clear();
        }

        meshRenderer.sharedMaterial.mainTexture = texture = new Texture2D(mapSettings.mapDimentions.x, mapSettings.mapDimentions.y, TextureFormat.RGBA32, false,true);
        texture.filterMode = FilterMode.Trilinear;

        var texturePainter = new FillTexture
        {
            source = heightMap,
            Destination = texture.GetPixelData<Color32>(0)
        };
        texturePainter.Schedule(heightMap.Length, 64).Complete();
        texture.Apply();

        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        var generator = new MeshGenerator
        {
            relativeHeightMapData = CalculateRelativeNoiseData(heightMap),
            meshSettings = mapSettings,
            meshIndex = 0,
            meshDataArray = meshDataArray,
            heightMap = heightMap
        };

        generator.Schedule().Complete();
        heightMap.Dispose();
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, activeMesh);
        activeMesh.RecalculateBounds();
        activeMesh.RecalculateNormals();
    }

    /// <summary>
    /// Generates a height map using the give noise settings and instance map settings
    /// </summary>
    /// <param name="heightMap"> output array of height map </param>
    /// <param name="noiseSettings"> noise settings </param>
    public void GenerateHeightMap(NativeArray<HeightMapElement> heightMap, SimpleNoise noiseSettings)
    {
        var noiseGenerator = new SimpleNoiseHeightMapGenerator
        {
            areaSettings = mapSettings,
            simpleNoise = noiseSettings,
            heightMap = heightMap
        };

        noiseGenerator.Schedule(heightMap.Length, 64).Complete();

        ColourHeightMap(heightMap, noiseSettings);
    }

    public void GenerateRigidHeightMap(NativeArray<HeightMapElement> heightMap,RigidNoise noiseSettings)
    {
        var noiseGenerator = new RigidNoiseHeightMapGenerator
        {
            areaSettings = mapSettings,
            rigidNoise = noiseSettings,
            heightMap = heightMap
        };

        noiseGenerator.Schedule(heightMap.Length, 64).Complete();
    }

    public void ColourHeightMap(NativeArray<HeightMapElement> heightMap, SimpleNoise noiseSettings)
    {
        var colouringJob = new HeightMapPainter
        {
            noiseSettings = noiseSettings,
            relativeNoiseData = CalculateRelativeNoiseData(heightMap),
            HeightMap = heightMap
        };
        colouringJob.Schedule(heightMap.Length, 64).Complete();
    }

    /// <summary>
    /// calculates the lowest, heighest and absolute mid point of a given height map
    /// </summary>
    /// <param name="heightMap"> height map to process</param>
    /// <returns> relative noise data </returns>
    public RelativeNoiseData CalculateRelativeNoiseData(NativeArray<HeightMapElement> heightMap)
    {
        // get the lowest and highest point on the map
        // we can use a 0-1 value to determine the flat floor of the map using this.

        // I wish to keep the mid point at the same relative position for all maps,
        // to avoid having to move the whole map up and down.
        NativeReference<RelativeNoiseData> relativeData = new(Allocator.TempJob, NativeArrayOptions.ClearMemory);
        var heightMapMinMaxer = new HeightMapMinMaxCal
        {
            HeightMap = heightMap,
            minMax = relativeData
        };

        heightMapMinMaxer.Schedule().Complete();
        RelativeNoiseData data = relativeData.Value;
        data.flatFloor = mapSettings.floorPercentage;
        relativeData.Dispose();
        return data;
    }

    /// <summary>
    /// Clamps the height maps min value by the given flatFloor percetange
    /// </summary>
    /// <param name="heightMap"></param>
    public void ClampToFlatFloor(NativeArray<HeightMapElement> heightMap,SimpleNoise noiseSettings)
    {
        RelativeNoiseData data = CalculateRelativeNoiseData(heightMap);
        data.flatFloor = mapSettings.floorPercentage;
        data.minValue = noiseSettings.minValue;
        var heightMapClamper = new HeightMapClamper
        {
            floorColour = mapSettings.floorColour,
            relativeNoiseData = data,
            HeightMap = heightMap
        };

        heightMapClamper.Schedule(heightMap.Length, 64).Complete();
    }

    public void ClampToFlatFloor(NativeArray<HeightMapElement> heightMap, RigidNoise noiseSettings)
    {
        RelativeNoiseData data = CalculateRelativeNoiseData(heightMap);
        data.flatFloor = mapSettings.floorPercentage;
        data.minValue = noiseSettings.minValue;
        var heightMapClamper = new HeightMapClamper
        {
            relativeNoiseData = data,
            HeightMap = heightMap
        };

        heightMapClamper.Schedule(heightMap.Length, 64).Complete();
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        MeshAreaRef mesh = new() { Value = GetComponent<MeshFilter>().mesh };

        dstManager.AddComponentData(entity, mapSettings);
        dstManager.AddComponentData(entity, simpleLayers[0]);
        dstManager.AddComponentData(entity, mesh);
        dstManager.AddComponent<UpdateMeshArea>(entity);
        dstManager.AddComponent<GenerateHeightMap>(entity);
        dstManager.AddBuffer<HeightMapElement>(entity);
    }
}

// ICompenentData is how you declare an entity component in Unity ECS.
// class IComponentDatas can have anyhting from managed C# as a member.
// struct IComponentDatas are restricted to value types, and cannot contain any array type, including native
// containers.
// entities can have array like data structs accosiated with them using the IBufferElement interface
// 
// according to unity it is best to have as few managed components as possible
// it is also best ot have components be as small as possible and for them to contain
// no logic beyond basic accessors and static methods. Logic goes in systems not components.
public class MeshAreaRef : IComponentData { public Mesh Value; }

[Serializable]
public struct MeshAreaSettings : IComponentData 
{
    public int2 mapDimentions;
    [Range(0f,1f)]
    public float floorPercentage;
    public Color32 floorColour;
    public Color32 lower;
    public Color32 higher;
}

[Serializable]
public struct RelativeNoiseData : IComponentData
{
    public float2 minMax;
    public float mid;
    public float flatFloor;
    [HideInInspector] public float minValue;
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

public struct UpdatingMeshArea : IComponentData { public double timeStamp; }

// components that do not declare any members are automatically catagoised as tagging components.
public struct UpdateMeshArea : IComponentData { }
public struct MeshAreaTriangulatorRun : IComponentData { }

public enum FirstLayer
{
    Simple,
    Rigid
}