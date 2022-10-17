using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public struct HeightMapWrapper
{
    public MeshAreaSettings mapSettings;
    public NativeArray<HeightMapElement> baseMap;
    public NativeArray<HeightMapElement> result;
    public bool firstLayer;

    public HeightMapWrapper(MeshAreaSettings mapSettings,NativeArray<HeightMapElement> baseMap, NativeArray<HeightMapElement> result, bool firstLayer = false)
    {
        this.mapSettings = mapSettings;
        this.baseMap = baseMap;
        this.result = result;
        this.firstLayer = firstLayer;
    }
}

public static class NoiseGenerator
{
    public static void GenerateSimpleMaps(SimpleNoise[] simpleLayers, HeightMapWrapper heightMaps)
    {
        for (int i = 0; i < simpleLayers.Length; i++)
        {
            SimpleNoise settings = simpleLayers[i];
            if (i > 0 ^ !heightMaps.firstLayer)
            {
                NativeArray<HeightMapElement> current = new(heightMaps.result.Length, Allocator.TempJob);
                GenerateHeightMap(current, settings,heightMaps.mapSettings);
                if (settings.clampToFlatFloor)
                {
                    ClampToFlatFloor(current, heightMaps.mapSettings, settings);
                }
                LayerTwoHeightMaps(heightMaps.mapSettings,new(settings, heightMaps.baseMap), new(settings, current), new(settings, heightMaps.result));
                current.Dispose();
            }
            else
            {
                GenerateHeightMap(heightMaps.baseMap, settings,heightMaps.mapSettings);
                if (settings.clampToFlatFloor)
                {
                    ClampToFlatFloor(heightMaps.baseMap,heightMaps.mapSettings, settings);
                }
                heightMaps.result.CopyFrom(heightMaps.baseMap);
            }
        }
    }

    /// <summary>
    /// Generates a height map using the give noise settings and instance map settings
    /// </summary>
    /// <param name="heightMap"> output array of height map </param>
    /// <param name="noiseSettings"> noise settings </param>
    public static void GenerateHeightMap(NativeArray<HeightMapElement> heightMap, SimpleNoise noiseSettings, MeshAreaSettings mapSettings)
    {
        var noiseGenerator = new SimpleNoiseHeightMapGenerator
        {
            areaSettings = mapSettings,
            simpleNoise = noiseSettings,
            heightMap = heightMap
        };

        noiseGenerator.Schedule(heightMap.Length, 64).Complete();

        ColourHeightMap(mapSettings,heightMap, noiseSettings);
    }

    public static void ColourHeightMap(MeshAreaSettings mapSettings, NativeArray<HeightMapElement> heightMap, SimpleNoise noiseSettings)
    {
        var colouringJob = new HeightMapPainter
        {
            noiseSettings = noiseSettings,
            relativeNoiseData = CalculateRelativeNoiseData(mapSettings, heightMap),
            HeightMap = heightMap
        };
        colouringJob.Schedule(heightMap.Length, 64).Complete();
    }

    /// <summary>
    /// calculates the lowest, heighest and absolute mid point of a given height map
    /// </summary>
    /// <param name="heightMap"> height map to process</param>
    /// <returns> relative noise data </returns>
    public static RelativeNoiseData CalculateRelativeNoiseData(MeshAreaSettings mapSettings,NativeArray<HeightMapElement> heightMap)
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
    public static void ClampToFlatFloor(NativeArray<HeightMapElement> heightMap, MeshAreaSettings mapSettings, SimpleNoise noiseSettings)
    {
        RelativeNoiseData data = CalculateRelativeNoiseData(mapSettings, heightMap);
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

    private static void LayerTwoHeightMaps(MeshAreaSettings mapSettings,SimpleHeightMapWrapper baseMap, SimpleHeightMapWrapper newMap, SimpleHeightMapWrapper result)
    {

        baseMap.noiseData = CalculateRelativeNoiseData(mapSettings, baseMap.heightMap);
        newMap.noiseData = CalculateRelativeNoiseData(mapSettings, newMap.heightMap);
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

}
