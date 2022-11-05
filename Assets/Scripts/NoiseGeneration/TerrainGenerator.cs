using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
                if (settings.clampToFloor)
                {
                    ClampToFlatFloor(current, heightMaps.mapSettings, settings, simpleLayers[i-1].abvcSettings.mainColour);
                }
                LayerTwoHeightMaps(heightMaps.mapSettings,new(settings, heightMaps.baseMap), new(settings, current), new(settings, heightMaps.result));
                current.Dispose();
            }
            else
            {
                GenerateHeightMap(heightMaps.baseMap, settings,heightMaps.mapSettings);
                if (settings.clampToFloor)
                {
                    ClampToFlatFloor(heightMaps.baseMap,heightMaps.mapSettings, settings);
                }
                heightMaps.result.CopyFrom(heightMaps.baseMap);
            }
        }
    }

    /// <summary>
    /// **04/11/2022**
    /// This scales with large maps much better 
    /// > 255x255 map (6.7ms to 6.5ms) within margin of error - they perform the same at this size
    /// > 500x500 map (63 to 49ms)
    /// > 1000x1000 map (265 to 204ms)
    /// 
    /// Most performance savings likely comes from running relative data calculator just once.
    /// The big array fits even less well into cache
    /// 255x255x216 = 14 045 400 bytes => 14  megabytes~ (old way, run multiple times for each layer)
    /// 
    /// 255x255x216x3 = 42 136 200 bytes => 42 megabytes~ (new way, all layers run at once in the same array)
    /// 1000x1000x216x3 = 648 000 000 => 648 megabytes
    /// **PROBLEM**
    /// maps generated using this are "lower" though shape is correct
    /// 
    /// **05/11/2022**
    /// Implemented use of GetSubArray() on native containers for only getting the block(s) of memory needed
    /// for the current copying or layering a hieght map
    /// fixed incorrect layering behaviour by running the relative pass after clamping instead of just once before.
    /// fixed memory leak caused by not disposing of the all layers main array.
    /// 
    /// > 255x255 map (6.5ms to 9.5ms) additional run of relative pass seems to add 3ms over yesteday
    /// > 500x500 map (49 to 54ms) still scales better at larger sizes
    /// > 1000x1000 map (204 to 215ms)
    /// 
    /// </summary>
    /// <param name="simpleLayers"></param>
    /// <param name="heightMaps"></param>
    public static void GenerateSimpleMapsBigArray(SimpleNoise[] simpleLayers, HeightMapWrapper heightMaps)
    {
        int2 dim = heightMaps.mapSettings.mapDimentions;
        NativeArray<SimpleNoise> nativeLayers = new(simpleLayers, Allocator.TempJob);
        NativeArray<HeightMapElement> allLayers = new(dim.x * dim.y * simpleLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeArray<CommonSettingsWrapper> settingWrappers = new(simpleLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < nativeLayers.Length; i++)
        {
            settingWrappers[i] = new(nativeLayers[i]);
        }


        MeshAreaSettings mapSettings = heightMaps.mapSettings;

        var noiseGenerator = new BigSimpleNoiseHeightMapGenerator
        {
            areaSettings = mapSettings,
            simpleNoiseSettings = nativeLayers,
            allHeightMaps = allLayers
        };

        JobHandle main = noiseGenerator.Schedule(allLayers.Length, 64);
        // main.Complete();
        NativeArray<RelativeNoiseData> relativeData;
        (main, relativeData) = BigCalculateRelativeNoiseData(main, mapSettings, settingWrappers, allLayers);

        main = BigColourHeightMap(main, mapSettings, allLayers, settingWrappers, relativeData);

        main = BigClampToFlatFloor(main, allLayers, mapSettings, settingWrappers, relativeData);

        (main, relativeData) = BigCalculateRelativeNoiseData(main, mapSettings, settingWrappers, allLayers);
        var resultCopy = new ResultCopy
        {
            allLayers = allLayers.GetSubArray(0, heightMaps.result.Length),
            result = heightMaps.result
        };

        main = resultCopy.Schedule(heightMaps.result.Length, 128, main);

        for (int i = 1; i < simpleLayers.Length; i++)
        {
            var layerer = new BigHeightMapLayerer
            {
                mapSettings = mapSettings,
                heightMapRelative = relativeData,
                baseLayer = allLayers.GetSubArray(0,heightMaps.result.Length),
                targetLayer = allLayers.GetSubArray(i*heightMaps.result.Length, heightMaps.result.Length),
                resultMap = heightMaps.result
            };
            main = layerer.Schedule(heightMaps.result.Length, 64, main);
            main.Complete();
        }
        relativeData.Dispose();
        settingWrappers.Dispose();
        nativeLayers.Dispose();
        allLayers.Dispose();
    }

    public static void GenerateRigidMapsBigArray(RigidNoise[] rigidLayers, HeightMapWrapper heightMaps)
    {
        int2 dim = heightMaps.mapSettings.mapDimentions;
        NativeArray<RigidNoise> nativeLayers = new(rigidLayers, Allocator.TempJob);
        NativeArray<HeightMapElement> allLayers = new(dim.x * dim.y * rigidLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeArray<CommonSettingsWrapper> settingWrappers = new(rigidLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < nativeLayers.Length; i++)
        {
            settingWrappers[i] = new(nativeLayers[i]);
        }

        MeshAreaSettings mapSettings = heightMaps.mapSettings;

        var noiseGenerator = new BigRigidNoiseHeightMapGenerator
        {
            areaSettings = mapSettings,
            rigidNoiseSettings = nativeLayers,
            allHeightMaps = allLayers
        };

        JobHandle main = noiseGenerator.Schedule(allLayers.Length, 64);
        // main.Complete();
        NativeArray<RelativeNoiseData> relativeData;
        (main, relativeData) = BigCalculateRelativeNoiseData(main, mapSettings, settingWrappers, allLayers);

        main = BigColourHeightMap(main, mapSettings, allLayers, settingWrappers, relativeData);

        main = BigClampToFlatFloor(main, allLayers, mapSettings, settingWrappers, relativeData);

        (main, relativeData) = BigCalculateRelativeNoiseData(main, mapSettings, settingWrappers, allLayers);
        var resultCopy = new ResultCopy
        {
            allLayers = allLayers.GetSubArray(0, heightMaps.result.Length),
            result = heightMaps.result
        };

        main = resultCopy.Schedule(heightMaps.result.Length, 128, main);

        for (int i = 1; i < rigidLayers.Length; i++)
        {
            var layerer = new BigHeightMapLayerer
            {
                mapSettings = mapSettings,
                heightMapRelative = relativeData,
                baseLayer = allLayers.GetSubArray(0, heightMaps.result.Length),
                targetLayer = allLayers.GetSubArray(i * heightMaps.result.Length, heightMaps.result.Length),
                resultMap = heightMaps.result
            };
            main = layerer.Schedule(heightMaps.result.Length, 64, main);
            main.Complete();
        }
        relativeData.Dispose();
        nativeLayers.Dispose();
        settingWrappers.Dispose();
        allLayers.Dispose();
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

        ColourHeightMap(mapSettings,heightMap, new(noiseSettings));
    }

    /// <summary>
    /// Sets the colour of a height map layer after it has been generated base of the relative noise data and colour valus in the noiseSettings
    /// </summary>
    /// <param name="mapSettings">Main map settings</param>
    /// <param name="heightMap">Height map layer to colour</param>
    /// <param name="commonSettings">layer settings</param>
    public static void ColourHeightMap(MeshAreaSettings mapSettings, NativeArray<HeightMapElement> heightMap, CommonSettingsWrapper commonSettings)
    {
        if (mapSettings.shader == ShaderPicker.BVC)
        {
            var colouringJob = new HeightMapPainterBVC
            {
                colourWrapper = commonSettings,
                relativeNoiseData = CalculateRelativeNoiseData(commonSettings.floorPercentage, heightMap),
                HeightMap = heightMap
            };
            colouringJob.Schedule(heightMap.Length, 64).Complete();
        }
        else if (mapSettings.shader == ShaderPicker.ABVC || mapSettings.shader == ShaderPicker.ABVCTextured)
        {
            var colouringJob = new HeightMapPainterABVC
            {
                colourWrapper = commonSettings,
                relativeNoiseData = CalculateRelativeNoiseData(commonSettings.floorPercentage, heightMap),
                HeightMap = heightMap
            };
            colouringJob.Schedule(heightMap.Length, 64).Complete();
        }
    }

    public static JobHandle BigColourHeightMap(JobHandle handle, MeshAreaSettings mapSettings, NativeArray<HeightMapElement> allLayers, NativeArray<CommonSettingsWrapper> commonSettings, NativeArray<RelativeNoiseData> relativeData)
    {
        if (mapSettings.shader == ShaderPicker.BVC)
        {
            var colouringJob = new BigHeightMapPainterBVC
            {
                mapDimentions = mapSettings.mapDimentions,
                colourWrappers = commonSettings,
                relativeNoiseData = relativeData,
                allMaps = allLayers
            };
            handle = colouringJob.Schedule(allLayers.Length, 64, handle);
        }
        else if (mapSettings.shader == ShaderPicker.ABVC || mapSettings.shader == ShaderPicker.ABVCTextured)
        {
            var colouringJob = new BigHeightMapPainterABVC
            {
                mapDimentions = mapSettings.mapDimentions,
                colourWrappers = commonSettings,
                relativeNoiseData = relativeData,
                allMaps = allLayers
            };
            handle = colouringJob.Schedule(allLayers.Length, 64, handle);
        }
        return handle;
    }


    /// <summary>
    /// Calculates the lowest, heighest and absolute mid point of a given height map,
    /// </summary>
    /// <param name="mapSettings">Main map settings </param>
    /// <param name="heightMap"> height map to process</param>
    /// <returns> relative noise data </returns>
    public static RelativeNoiseData CalculateRelativeNoiseData(float floor,NativeArray<HeightMapElement> heightMap)
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
        data.flatFloor = floor;
        relativeData.Dispose();
        return data;
    }

    public static (JobHandle, NativeArray<RelativeNoiseData>) BigCalculateRelativeNoiseData(JobHandle jobHandle, MeshAreaSettings mapSettings,NativeArray<CommonSettingsWrapper> commonSettings, NativeArray<HeightMapElement> heightMaps)
    {
        NativeArray<RelativeNoiseData> relativeData = new(commonSettings.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        var heightMapMinMaxer = new BigHeightMapMinMaxCal
        {
            minMax = relativeData,
            mapSettings = mapSettings,
            HeightMap = heightMaps,
            commonSettings = commonSettings,
        };

        return (heightMapMinMaxer.Schedule(commonSettings.Length, 1, jobHandle), relativeData);
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
        RelativeNoiseData data = CalculateRelativeNoiseData(noiseSettings.floorPercentage, heightMap);
        data.flatFloor = noiseSettings.floorPercentage;
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
        RelativeNoiseData data = CalculateRelativeNoiseData(noiseSettings.floorPercentage, heightMap);
        data.flatFloor = noiseSettings.floorPercentage;
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

    public static JobHandle BigClampToFlatFloor(JobHandle handle,NativeArray<HeightMapElement> heightMap, MeshAreaSettings mapSettings, NativeArray<CommonSettingsWrapper> commonSettings,NativeArray<RelativeNoiseData> relativeData)
    {
        var heightMapClamper = new BigHeightMapClamper
        {
            mapSettings = mapSettings,
            relativeNoiseData = relativeData,
            HeightMap = heightMap,
            colourWrappers = commonSettings
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
        baseMap.noiseData = CalculateRelativeNoiseData(baseMap.simpleNoise.floorPercentage, baseMap.heightMap);
        newMap.noiseData = CalculateRelativeNoiseData(newMap.simpleNoise.floorPercentage, newMap.heightMap);
        newMap.noiseData.minValue = newMap.simpleNoise.riseUp;
        var layerer = new HeightMapLayerer
        {
            mapSettings = mapSettings,
            baseRelative = baseMap.noiseData,
            heightMapRelative = newMap.noiseData,
            baseLayer = baseMap.heightMap,
            targetLayer = newMap.heightMap,
            resultMap = result.heightMap
        };
        layerer.Schedule(result.heightMap.Length, 64).Complete();
    }
}
