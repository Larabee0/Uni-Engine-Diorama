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

/// <summary>
/// Static class provides access to height map jobs via static methods.
/// </summary>
public static class TerrainGenerator
{
    /// <summary>
    /// Generates and layers height maps for all simple layers given
    /// </summary>
    /// <param name="simpleLayers">Height map layers</param>
    /// <param name="heightMaps">Height map working NativeArrays</param>
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
                    ClampToFlatFloor(current, heightMaps.mapSettings, settings, simpleLayers[i-1].abvcSettings.mainColour);
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
    /// Generates a height map using the give simple noise settings and main map settings
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

    public static JobHandle GenerateHeightMap(JobHandle handle,NativeArray<HeightMapElement> heightMap, SimpleNoise noiseSettings, MeshAreaSettings mapSettings)
    {
        var noiseGenerator = new SimpleNoiseHeightMapGenerator
        {
            areaSettings = mapSettings,
            simpleNoise = noiseSettings,
            heightMap = heightMap
        };

        handle =noiseGenerator.Schedule(heightMap.Length, 64, handle);

        return ColourHeightMap(handle,mapSettings, heightMap, noiseSettings);
    }

    /// <summary>
    /// Sets the colour of a height map layer after it has been generated base of the relative noise data and colour valus in the noiseSettings
    /// </summary>
    /// <param name="mapSettings">Main map settings</param>
    /// <param name="heightMap">Height map layer to colour</param>
    /// <param name="noiseSettings">layer settings</param>
    public static void ColourHeightMap(MeshAreaSettings mapSettings, NativeArray<HeightMapElement> heightMap, SimpleNoise noiseSettings)
    {
        if (mapSettings.shader == ShaderPicker.BVC)
        {
            var colouringJob = new HeightMapPainterBVC
            {
                noiseSettings = noiseSettings,
                relativeNoiseData = CalculateRelativeNoiseData(mapSettings, heightMap),
                HeightMap = heightMap
            };
            colouringJob.Schedule(heightMap.Length, 64).Complete();
        }
        else if (mapSettings.shader == ShaderPicker.ABVC || mapSettings.shader == ShaderPicker.ABVCTextured)
        {
            var colouringJob = new HeightMapPainterABVC
            {
                noiseSettings = noiseSettings,
                relativeNoiseData = CalculateRelativeNoiseData(mapSettings, heightMap),
                HeightMap = heightMap
            };
            colouringJob.Schedule(heightMap.Length, 64).Complete();
        }
    }

    public static JobHandle ColourHeightMap(JobHandle handle, MeshAreaSettings mapSettings, NativeArray<HeightMapElement> heightMap, SimpleNoise noiseSettings)
    {
        if (mapSettings.shader == ShaderPicker.BVC)
        {
            var colouringJob = new HeightMapPainterBVC
            {
                noiseSettings = noiseSettings,
                relativeNoiseData = CalculateRelativeNoiseData(mapSettings, heightMap),
                HeightMap = heightMap
            };
            handle = colouringJob.Schedule(heightMap.Length, 64, handle);
        }
        else if (mapSettings.shader == ShaderPicker.ABVC || mapSettings.shader == ShaderPicker.ABVCTextured)
        {
            var colouringJob = new HeightMapPainterABVC
            {
                noiseSettings = noiseSettings,
                relativeNoiseData = CalculateRelativeNoiseData(mapSettings, heightMap),
                HeightMap = heightMap
            };
            handle = colouringJob.Schedule(heightMap.Length, 64, handle);
        }
        return handle;
    }


    /// <summary>
    /// Calculates the lowest, heighest and absolute mid point of a given height map,
    /// </summary>
    /// <param name="mapSettings">Main map settings </param>
    /// <param name="heightMap"> height map to process</param>
    /// <returns> relative noise data </returns>
    public static RelativeNoiseData CalculateRelativeNoiseData(MeshAreaSettings mapSettings,NativeArray<HeightMapElement> heightMap)
    {
        // get the lowest and highest point on the map
        // we can use a 0-1 value to set the floor of the map using this.

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
    /// Clamps the lower height map to be at a certain % up the range of the height map
    /// these points are coloured using the floor colour value (for water)
    /// </summary>
    /// <param name="heightMap">Height map to clamp </param>
    /// <param name="mapSettings">Main map settings</param>
    /// <param name="noiseSettings">Height map noise settings</param>
    public static void ClampToFlatFloor(NativeArray<HeightMapElement> heightMap, MeshAreaSettings mapSettings, SimpleNoise noiseSettings)
    {
        RelativeNoiseData data = CalculateRelativeNoiseData(mapSettings, heightMap);
        data.flatFloor = mapSettings.floorPercentage;
        data.minValue = noiseSettings.minValue;
        var heightMapClamper = new HeightMapClamper
        {
            mapSettings = mapSettings,
            floorColour = mapSettings.floorColour,
            relativeNoiseData = data,
            HeightMap = heightMap
        };

        heightMapClamper.Schedule(heightMap.Length, 64).Complete();
    }
    public static void ClampToFlatFloor(NativeArray<HeightMapElement> heightMap, MeshAreaSettings mapSettings, SimpleNoise noiseSettings, Color32 floorColour)
    {
        RelativeNoiseData data = CalculateRelativeNoiseData(mapSettings, heightMap);
        data.flatFloor = mapSettings.floorPercentage;
        data.minValue = noiseSettings.minValue;
        var heightMapClamper = new HeightMapClamper
        {
            mapSettings = mapSettings,
            floorColour = floorColour,
            relativeNoiseData = data,
            HeightMap = heightMap
        };

        heightMapClamper.Schedule(heightMap.Length, 64).Complete();
    }

    public static JobHandle ClampToFlatFloor(JobHandle handle,NativeArray<HeightMapElement> heightMap, MeshAreaSettings mapSettings, SimpleNoise noiseSettings)
    {
        handle.Complete();
        RelativeNoiseData data = CalculateRelativeNoiseData(mapSettings, heightMap);
        data.flatFloor = mapSettings.floorPercentage;
        data.minValue = noiseSettings.minValue;
        var heightMapClamper = new HeightMapClamper
        {
            mapSettings = mapSettings,
            floorColour = mapSettings.floorColour,
            relativeNoiseData = data,
            HeightMap = heightMap
        };

        return heightMapClamper.Schedule(heightMap.Length, 64,handle);
    }


    /// <summary>
    /// Layers two simple height maps together, blending colours and transition.
    /// </summary>
    /// <param name="mapSettings">Main map settings</param>
    /// <param name="baseMap">Current main height map</param>
    /// <param name="newMap">New layer to be merged onto main</param>
    /// <param name="result">Resultant height map wrapper</param>
    private static void LayerTwoHeightMaps(MeshAreaSettings mapSettings,SimpleHeightMapWrapper baseMap, SimpleHeightMapWrapper newMap, SimpleHeightMapWrapper result)
    {

        baseMap.noiseData = CalculateRelativeNoiseData(mapSettings, baseMap.heightMap);
        newMap.noiseData = CalculateRelativeNoiseData(mapSettings, newMap.heightMap);
        newMap.noiseData.minValue = newMap.simpleNoise.riseUp;
        var layerer = new HeightMapLayerer
        {
            mapSettings = mapSettings,
            baseRelative = baseMap.noiseData,
            heightMapRelative = newMap.noiseData,
            baseMap = baseMap.heightMap,
            heightMap = newMap.heightMap,
            result = result.heightMap
        };
        layerer.Schedule(result.heightMap.Length, 64).Complete();
    }
}
