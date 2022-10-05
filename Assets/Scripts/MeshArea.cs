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

    [SerializeField] private bool UpdateOnChange;
    private Mesh activeMesh;

    private void Awake()
    {
        GetComponent<MeshFilter>().mesh = new Mesh() { subMeshCount = 1, name = "MeshArea" };
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
        activeMesh = GetComponent<MeshFilter>().sharedMesh = new Mesh() { subMeshCount = 1, name = "MeshArea" };

        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        NativeArray<HeightMapElement> heightMap = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var noiseGenerator = new SimpleNoiseHeightMapGenerator
        {
            areaSettings = mapSettings,
            simpleNoise = simpleNoise,
            HeightMap = heightMap
        };

        noiseGenerator.Schedule(heightMap.Length, 64).Complete();

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
public struct MeshAreaSettings : IComponentData { public int2 mapDimentions; }

public struct UpdatingMeshArea : IComponentData { public double timeStamp; }

// components that do not declare any members are automatically catagoised as tagging components.
public struct UpdateMeshArea : IComponentData { }
public struct MeshAreaTriangulatorRun : IComponentData { }
