using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;

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
    [SerializeField] private Material bvcMat;
    [SerializeField] private Material abvcMat;
    [SerializeField] private MeshAreaSettings mapSettings;

    [SerializeField] private SimpleNoise[] simpleLayers;
    [Space(400f)] // work around for editor bug in 2021.3.4 were first item in an array does not get any space to display

    [SerializeField] private bool UpdateOnChange;

    private Mesh activeMesh;
    

    private void Awake()
    {
        meshFilter=GetComponent<MeshFilter>();
        meshFilter.mesh = activeMesh = new Mesh() { subMeshCount = 1, name = "MeshArea" };
    }
    private void Start()
    {

        Generate();
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
        float start = Time.realtimeSinceStartup;
        NativeArray<HeightMapElement> result = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob);
        NativeArray<HeightMapElement> baseMap = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob);

        TerrainGenerator.GenerateSimpleMaps(simpleLayers, new(mapSettings, baseMap, result, true));

        baseMap.Dispose();
        GenerateHeightMapMesh(result);
        Debug.LogFormat("Generation Time: {0}ms", (Time.realtimeSinceStartup - start) * 1000f);
    }

    /// <summary>
    /// Generates a height map mesh from given NativeArray, disposes of array when finished.
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
        if (mapSettings.shader == ShaderPicker.BVC)
        {
            meshRenderer.sharedMaterial = bvcMat;
            var generator = new MeshGeneratorBVC
            {
                relativeHeightMapData = TerrainGenerator.CalculateRelativeNoiseData(mapSettings, heightMap),
                meshSettings = mapSettings,
                meshIndex = 0,
                meshDataArray = meshDataArray,
                heightMap = heightMap
            };
            generator.Schedule().Complete();
        }
        else if(mapSettings.shader == ShaderPicker.ABVC)
        {
            meshRenderer.sharedMaterial = abvcMat;
            var generator = new MeshGeneratorABVC
            {
                relativeHeightMapData = TerrainGenerator.CalculateRelativeNoiseData(mapSettings, heightMap),
                meshSettings = mapSettings,
                meshIndex = 0,
                meshDataArray = meshDataArray,
                heightMap = heightMap
            };
            generator.Schedule().Complete();
        }

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
