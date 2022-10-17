using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System;

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
    [Space(200f)]
    [SerializeField] private RigidNoise rigidNoise;

    [SerializeField] private RelativeNoiseData relativeNoiseData1;
    [SerializeField] private RelativeNoiseData relativeNoiseData2;

    [SerializeField] private FilterMode textureFilterMode;
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

        NoiseGenerator.GenerateSimpleMaps(simpleLayers, new(mapSettings, baseMap, result, true));

        baseMap.Dispose();
        GenerateHeightMapMesh(result);
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

        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        var generator = new MeshGeneratorBVC
        {
            relativeHeightMapData = NoiseGenerator.CalculateRelativeNoiseData(mapSettings,heightMap),
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
        dstManager.AddComponentData(entity, simpleLayers[0]);
        dstManager.AddComponentData(entity, mesh);
        dstManager.AddComponent<UpdateMeshArea>(entity);
        dstManager.AddComponent<GenerateHeightMap>(entity);
        dstManager.AddBuffer<HeightMapElement>(entity);
    }
}
