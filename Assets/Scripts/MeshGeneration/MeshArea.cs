using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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
    [SerializeField] private Material abvcTexturedMat;

    [SerializeField] private int textureHash;
    [SerializeField] private Texture2D floorTexture;
    [SerializeField] private Texture2D[] terrainTextures;
    [SerializeField] private Texture2DArray bundledTextures;
    [SerializeField] private MeshAreaSettings mapSettings;

    [Tooltip("First element in the simple layers list is overwritten by this property.\nThis is a work around for an editor bug when trying to edit the first element of a collection.")]
    [SerializeField] private NoiseSettings firstNoiseLayer;
    [SerializeField] private NoiseSettings[] noiseLayers;

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
        // NativeArray<HeightMapElement> baseMap = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob);
        noiseLayers[0] = firstNoiseLayer;

        // TerrainGenerator.GenerateSimpleMapsBigArray(noiseLayers, new(mapSettings, baseMap, result, true));

        TerrainGenerator.GenerateCommonMaps(noiseLayers, mapSettings, result);

        // baseMap.Dispose();
        Debug.LogFormat("Generation Time: {0}ms", (Time.realtimeSinceStartup - start) * 1000f);
        start = Time.realtimeSinceStartup;
        GenerateHeightMapMesh(result);
        Debug.LogFormat("Total Mesh Time: {0}ms", (Time.realtimeSinceStartup - start) * 1000f);

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

        float start;
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        if (mapSettings.shader == ShaderPicker.BVC)
        {
            start = Time.realtimeSinceStartup;
            meshRenderer.sharedMaterial = bvcMat;
            var generator = new MeshGeneratorBVC
            {
                relativeHeightMapData = TerrainGenerator.CalculateRelativeNoiseData(mapSettings.floorPercentage, heightMap),
                meshSettings = mapSettings,
                meshIndex = 0,
                meshDataArray = meshDataArray,
                heightMap = heightMap
            };
            generator.Schedule().Complete();
            Debug.LogFormat("MeshGeneratorBVC Time: {0}ms", (Time.realtimeSinceStartup - start) * 1000f);
        }
        else if(mapSettings.shader == ShaderPicker.ABVC)
        {
            start = Time.realtimeSinceStartup;
            meshRenderer.sharedMaterial = abvcMat;
            var generator = new MeshGeneratorABVC
            {
                relativeHeightMapData = TerrainGenerator.CalculateRelativeNoiseData(mapSettings.floorPercentage, heightMap),
                meshSettings = mapSettings,
                meshIndex = 0,
                meshDataArray = meshDataArray,
                heightMap = heightMap
            };
            generator.Schedule().Complete();
            Debug.LogFormat("MeshGeneratorABVC Time: {0}ms", (Time.realtimeSinceStartup - start) * 1000f);
        }
        else if(mapSettings.shader == ShaderPicker.ABVCTextured)
        {
            start = Time.realtimeSinceStartup;
            NativeArray<float4> rawDataForTexture = new(heightMap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var generator = new MeshGeneratorABVCT
            {
                relativeHeightMapData = TerrainGenerator.CalculateRelativeNoiseData(mapSettings.floorPercentage, heightMap),
                meshSettings = mapSettings,
                meshIndex = 0,
                meshDataArray = meshDataArray,
                textureData = rawDataForTexture,
                heightMap = heightMap
            };
            JobHandle handle = generator.Schedule();
            Debug.LogFormat("MeshGeneratorABVCT Time: {0}ms", (Time.realtimeSinceStartup - start) * 1000f);

            Texture2D dataTexture = new(mapSettings.mapDimentions.x, mapSettings.mapDimentions.y, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Trilinear,
            };
            var textureFiller = new FillTexture
            {
                source = rawDataForTexture,
                Destination = dataTexture.GetPixelData<Color32>(0)
            };
            textureFiller.Schedule(rawDataForTexture.Length, 64, handle).Complete();
            rawDataForTexture.Dispose();
            dataTexture.Apply();
            abvcTexturedMat.SetTexture("_Genereated_Data", dataTexture);
            abvcTexturedMat.SetVector("_TextureTiling", new Vector4(mapSettings.textureTiling.x, mapSettings.textureTiling.y));

            start = Time.realtimeSinceStartup;
            int hash = TextureArrayGenerator.HashTextures(floorTexture, terrainTextures);
            if (hash != textureHash || bundledTextures == null)
            {
                bundledTextures = TextureArrayGenerator.BasicBundler(floorTexture, terrainTextures);
                textureHash = hash;
                Debug.Log("bundled textures");
            }
            abvcTexturedMat.SetTexture("_Patterns", bundledTextures);
            Debug.LogFormat("Texture Array Time: {0}ms", (Time.realtimeSinceStartup - start) * 1000f);

            meshRenderer.sharedMaterial = abvcTexturedMat;
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
        // dstManager.AddComponentData(entity, noiseLayers[0]);
        dstManager.AddComponentData(entity, mesh);
        dstManager.AddComponent<UpdateMeshArea>(entity);
        dstManager.AddComponent<GenerateHeightMap>(entity);
        dstManager.AddBuffer<HeightMapElement>(entity);
    }
}
