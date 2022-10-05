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

/// <summary>
/// IConvertGameObjectToEntity gets called when a gameobject converts into an entity
/// and allows you to add extra componenets during the converstion system - during which time you can
/// still access all game object components and transform hierarchy.
/// this method runs all on instances of the component implementing it in the scene.
/// </summary>
public class MeshArea : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField] private MeshAreaSettings mapSettings;
    [SerializeField] private SimpleNoise simpleNoise;

    [SerializeField] private SimpleNoise simpleNoiseLayer2;

    [SerializeField] private bool UpdateOnChange;
    [SerializeField] private bool SecondLayer;
    private Mesh activeMesh;

    private void Awake()
    {
        GetComponent<MeshFilter>().mesh = new Mesh() { subMeshCount = 1, name = "MeshArea" };
    }

    private void OnValidate()
    {
        if (UpdateOnChange)
        {
            if (SecondLayer)
            {
                GenerateTwoLayers();
            }
            else
            {
                GenerateOneLayers();
            }
        }
    }

    public void GenerateOneLayers()
    {
        NativeArray<HeightMapElement> heightMap = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob);
        GenerateHeightMap(heightMap, simpleNoise);
        ClampToFlatFloor(heightMap);

        GenerateHeightMapMesh(heightMap);
    }

    public void GenerateTwoLayers()
    {
        NativeArray<HeightMapElement> heightMapLayer1 = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob);
        
        NativeArray<HeightMapElement> finalMap = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        GenerateHeightMap(heightMapLayer1, simpleNoise);
        ClampToFlatFloor(heightMapLayer1);
        NativeArray<HeightMapElement> heightMapLayer2 = new(heightMapLayer1, Allocator.TempJob);
        GenerateHeightMap(heightMapLayer2, simpleNoiseLayer2);

        var layerer = new HeightMapLayerer
        {
            relative1 = CalculateRelativeNoiseData(heightMapLayer1),
            relative2 = CalculateRelativeNoiseData(heightMapLayer2),
            HeightMap1 = heightMapLayer1,
            HeightMap2 = heightMapLayer2,
            resultMap = finalMap
        };
        layerer.Schedule(finalMap.Length, 64).Complete();
        heightMapLayer1.Dispose();
        heightMapLayer2.Dispose();
        GenerateHeightMapMesh(finalMap);
    }

    /// <summary>
    /// Generates heightMap from given nativearray, disposes of array when finished.
    /// </summary>
    /// <param name="heightMap">Height map to produce mesh of</param>
    public void GenerateHeightMapMesh(NativeArray<HeightMapElement> heightMap)
    {
        activeMesh = GetComponent<MeshFilter>().sharedMesh = new Mesh() { subMeshCount = 1, name = "MeshArea" };

        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        var generator = new MeshGenerator
        {
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
            HeightMap = heightMap
        };

        noiseGenerator.Schedule(heightMap.Length, 64).Complete();
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
        data.flatFloor = mapSettings.minValue;
        relativeData.Dispose();
        return data;
    }

    /// <summary>
    /// Clamps the height maps min value by the given flatFloor percetange
    /// </summary>
    /// <param name="heightMap"></param>
    public void ClampToFlatFloor(NativeArray<HeightMapElement> heightMap)
    {
        var heightMapClamper = new HeightMapClamper
        {
            relativeNoiseData = CalculateRelativeNoiseData(heightMap),
            HeightMap = heightMap
        };

        heightMapClamper.Schedule(heightMap.Length, 64).Complete();
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        MeshAreaRef mesh = new() { Value = GetComponent<MeshFilter>().mesh };

        dstManager.AddComponentData(entity, mapSettings);
        dstManager.AddComponentData(entity, simpleNoise);
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
    public float minValue;
}

public struct RelativeNoiseData : IComponentData
{
    public float2 minMax;
    public float mid;
    public float flatFloor;
}

public struct UpdatingMeshArea : IComponentData { public double timeStamp; }

// components that do not declare any members are automatically catagoised as tagging components.
public struct UpdateMeshArea : IComponentData { }
public struct MeshAreaTriangulatorRun : IComponentData { }
