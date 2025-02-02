using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
/// <summary>
/// Wrapper struct no longer used as of 16/11/2022
/// </summary>
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
    /// **16/11/2022**
    /// No longer used
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
    /// for the current copying or layering of a height map
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
        NativeArray<SimpleNoise> nativeLayers = new(simpleLayers, Allocator.TempJob);
        NativeArray<HeightMapElement> allLayers = new(heightMaps.mapSettings.mapDimentions.x * heightMaps.mapSettings.mapDimentions.y * simpleLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeArray<CommonSettingsWrapper> settingWrappers = new(simpleLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < nativeLayers.Length; i++)
        {
            settingWrappers[i] = nativeLayers[i].commonSettings;
        }


        MeshAreaSettings mapSettings = heightMaps.mapSettings;

        var noiseGenerator = new BigSimpleNoiseHeightMapGenerator
        {
            areaSettings = mapSettings,
            simpleNoiseSettings = nativeLayers,
            allHeightMaps = allLayers
        };

        JobHandle main = noiseGenerator.Schedule(allLayers.Length, 64);

        NativeArray<RelativeNoiseData> relativeData = new(nativeLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        main = BigCalculateRelativeNoiseData(main, mapSettings,relativeData, settingWrappers, allLayers);

        main = BigColourHeightMap(main, mapSettings, allLayers, settingWrappers, relativeData);

        main = BigClampToFlatFloor(main, allLayers, mapSettings, settingWrappers, relativeData);

        main= BigCalculateRelativeNoiseData(main, mapSettings, relativeData, settingWrappers, allLayers);
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
            main = layerer.Schedule(heightMaps.result.Length,64, main);
            main.Complete();
        }
        relativeData.Dispose();
        settingWrappers.Dispose();
        nativeLayers.Dispose();
        allLayers.Dispose();
    }

    /// <summary>
    /// 16/11/2022 No longer used
    /// </summary>
    /// <param name="rigidLayers"></param>
    /// <param name="heightMaps"></param>
    public static void GenerateRigidMapsBigArray(RigidNoise[] rigidLayers, HeightMapWrapper heightMaps)
    {
        int2 dim = heightMaps.mapSettings.mapDimentions;
        NativeArray<RigidNoise> nativeLayers = new(rigidLayers, Allocator.TempJob);
        NativeArray<HeightMapElement> allLayers = new(dim.x * dim.y * rigidLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeArray<CommonSettingsWrapper> settingWrappers = new(rigidLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < nativeLayers.Length; i++)
        {
            settingWrappers[i] = nativeLayers[i].commonSettings;
        }

        MeshAreaSettings mapSettings = heightMaps.mapSettings;

        var noiseGenerator = new BigRigidNoiseHeightMapGenerator
        {
            areaSettings = mapSettings,
            rigidNoiseSettings = nativeLayers,
            allHeightMaps = allLayers
        };

        JobHandle main = noiseGenerator.Schedule(allLayers.Length, 64);


        NativeArray<RelativeNoiseData> relativeData = new(nativeLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        main = BigCalculateRelativeNoiseData(main, mapSettings, relativeData, settingWrappers, allLayers);

        main = BigColourHeightMap(main, mapSettings, allLayers, settingWrappers, relativeData);

        main = BigClampToFlatFloor(main, allLayers, mapSettings, settingWrappers, relativeData);

        main = BigCalculateRelativeNoiseData(main, mapSettings, relativeData, settingWrappers, allLayers);
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
            main = layerer.Schedule(heightMaps.result.Length,64, main);
            main.Complete();
        }
        relativeData.Dispose();
        nativeLayers.Dispose();
        settingWrappers.Dispose();
        allLayers.Dispose();
    }

    /// <summary>
    /// Generates all height maps into 1 working array in 1 job.
    /// height maps are then layered into the result Height Map array.
    /// This variant supports erosion of the result height map - this means that all height maps are generated, then layered
    /// then the final result is eroded. It takes the erosion settings from the first element in noiseSettings.
    /// </summary>
    /// <param name="noiseSettings"></param>
    /// <param name="mapSettings"></param>
    /// <param name="resultHeightMap"></param>
    public static void GenerateCommonErosion(NoiseSettings[] noiseSettings, MeshAreaSettings mapSettings, NativeArray<HeightMapElement> resultHeightMap)
    {
        NativeArray<NoiseSettings> nativeLayers = new(noiseSettings, Allocator.TempJob);
        NativeArray<CommonSettingsWrapper> commonSettingWrappers = new(noiseSettings.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        int2 finalMapSize = mapSettings.mapDimentions;

        if (noiseSettings[0].erosionSettings.erosion)
        {
            int brushRadius = noiseSettings[0].erosionSettings.erosionBrushRadius * 2;
            mapSettings.mapDimentions = new(mapSettings.mapDimentions.x + brushRadius, mapSettings.mapDimentions.y + brushRadius);
        }
        
        NativeArray<HeightMapElement> allLayers = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y * noiseSettings.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        NativeParallelHashSet<int> excludedLayers = new(noiseSettings.Length, Allocator.TempJob);
        for (int i = 0; i < nativeLayers.Length; i++)
        {
            commonSettingWrappers[i] = noiseSettings[i].basicSettings;
        }

        var noiseGenerator = new CommonNoiseGenerator
        {
            areaSettings = mapSettings,
            noiseSettings = nativeLayers,
            allHeightMaps = allLayers,
            excludeLayers = excludedLayers,
        };

        JobHandle main = noiseGenerator.ScheduleParallel(allLayers.Length, 448, default);

        NativeArray<RelativeNoiseData> relativeData = new(nativeLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        main = BigCalculateRelativeNoiseData(main, mapSettings, relativeData, commonSettingWrappers, allLayers);

        main = BigColourHeightMap(main, mapSettings, allLayers, commonSettingWrappers, relativeData);

        main = BigClampToFlatFloor(main, allLayers, mapSettings, commonSettingWrappers, relativeData);

        main = BigCalculateRelativeNoiseData(main, mapSettings, relativeData, commonSettingWrappers, allLayers,true);

        NativeArray<HeightMapElement> preErosinoResult = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        var resultCopy = new ResultCopy
        {
            allLayers = allLayers.GetSubArray(0, preErosinoResult.Length),
            result = preErosinoResult
        };

        main = resultCopy.Schedule(preErosinoResult.Length, 128, main);

        for (int i = 1; i < noiseSettings.Length; i++)
        {
            var layerer = new BigHeightMapLayerer
            {
                layerIndex = i,
                mapSettings = mapSettings,
                heightMapRelative = relativeData,
                baseLayer = allLayers.GetSubArray(0, preErosinoResult.Length),
                targetLayer = allLayers.GetSubArray(i * preErosinoResult.Length, preErosinoResult.Length),
                resultMap = preErosinoResult
            };
            main = layerer.Schedule(preErosinoResult.Length, 512, main);
            main.Complete();
        }

        if (noiseSettings[0].erosionSettings.erosion)
        {
            ErodeSettings erosionSettings = noiseSettings[0].erosionSettings;
            erosionSettings.mapSizeWithBorder =mapSettings.mapDimentions;
            erosionSettings.mapSize = finalMapSize;
            main = Erosion.Erode(preErosinoResult, erosionSettings, default);

            mapSettings.mapDimentions = finalMapSize;

            var cutter = new ErosionCutter
            {
                mapSettings = mapSettings,
                eroisonSettings = erosionSettings,
                source = preErosinoResult,
                destination= resultHeightMap

            };
            cutter.ScheduleParallel(resultHeightMap.Length, 64, main).Complete();
        }
        else
        {
            var resultCopy2 = new ResultCopy
            {
                allLayers = preErosinoResult,
                result = resultHeightMap
            };
            preErosinoResult.Dispose(resultCopy2.Schedule(preErosinoResult.Length, 64, main)).Complete();
        }

        excludedLayers.Dispose();
        relativeData.Dispose();
        commonSettingWrappers.Dispose();
        nativeLayers.Dispose();
        allLayers.Dispose();
    }

    /// <summary>
    /// This variant allows for per hieght map erosion controls - this produces pretty bad results
    /// It sorts out which height maps need eroding and will kick those off to GenerateErodedMaps,
    /// which will schedule its own jobs for generation and eroding.
    /// meanwhile this will generate noise for maps not being a eroded concurrently with GenerateErodedMaps.
    /// both these job handles are then combined into the main handle used by this method and it procedes much like the above method.
    /// /// </summary>
    /// <param name="noiseSettings"></param>
    /// <param name="mapSettings"></param>
    /// <param name="resultHeightMap"></param>
    public static void GenerateCommonPerMapErosion(NoiseSettings[] noiseSettings, MeshAreaSettings mapSettings,NativeArray<HeightMapElement> resultHeightMap)
    {
        NativeArray<NoiseSettings> nativeLayers = new(noiseSettings, Allocator.TempJob);
        NativeArray<CommonSettingsWrapper> commonSettingWrappers = new(noiseSettings.Length, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
        NativeArray<HeightMapElement> allLayers = new(mapSettings.mapDimentions.x * mapSettings.mapDimentions.y * noiseSettings.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        int markedForErosion = 0;
        NativeParallelHashSet<int> excludedLayers = new(noiseSettings.Length, Allocator.TempJob);
        for (int i = 0; i < nativeLayers.Length; i++)
        {
            commonSettingWrappers[i] = noiseSettings[i].basicSettings;
            if (nativeLayers[i].erosionSettings.erosion)
            {
                excludedLayers.Add(i);
                markedForErosion++;
            }
        }

        var noiseGenerator = new CommonNoiseGenerator
        {
            areaSettings = mapSettings,
            noiseSettings = nativeLayers,
            allHeightMaps = allLayers,
            excludeLayers= excludedLayers,
        };

        JobHandle main = noiseGenerator.ScheduleParallel(allLayers.Length, 448,default);

        if (markedForErosion > 0)
        {
            main = JobHandle.CombineDependencies(GenerateErodedMaps(noiseSettings, mapSettings, out NativeArray<HeightMapElement> erosionLayers), main);
            NativeArray<int> erosionSettingsIndexRemap = new(noiseSettings.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (int ns = 0,i = 0; ns < noiseSettings.Length; ns++)
            {
                if (nativeLayers[i].erosionSettings.erosion)
                {
                    erosionSettingsIndexRemap[ns] = i;
                    i++;
                }
            }

            var erosionMerge = new ErosionCombiner
            {
                mapSettings = mapSettings,
                noiseSettings = nativeLayers,
                erosionSettingsIndexRemap = erosionSettingsIndexRemap,
                source = erosionLayers,
                destination = allLayers
            };
            main = erosionMerge.Schedule(allLayers.Length, main);

        }

        NativeArray<RelativeNoiseData> relativeData = new(nativeLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        main = BigCalculateRelativeNoiseData(main, mapSettings, relativeData, commonSettingWrappers, allLayers);

        main = BigColourHeightMap(main, mapSettings, allLayers, commonSettingWrappers, relativeData);

        main = BigClampToFlatFloor(main, allLayers, mapSettings, commonSettingWrappers, relativeData);

        main = BigCalculateRelativeNoiseData(main, mapSettings, relativeData, commonSettingWrappers, allLayers);
        var resultCopy = new ResultCopy
        {
            allLayers = allLayers.GetSubArray(0, resultHeightMap.Length),
            result = resultHeightMap
        };

        main = resultCopy.Schedule(resultHeightMap.Length, 128, main);

        for (int i = 1; i < noiseSettings.Length; i++)
        {
            var layerer = new BigHeightMapLayerer
            {
                layerIndex= i,
                mapSettings = mapSettings,
                heightMapRelative = relativeData,
                baseLayer = allLayers.GetSubArray(0, resultHeightMap.Length),
                targetLayer = allLayers.GetSubArray(i * resultHeightMap.Length, resultHeightMap.Length),
                resultMap = resultHeightMap
            };
            main = layerer.Schedule(resultHeightMap.Length,512, main);
            main.Complete();
        }
        excludedLayers.Dispose();
        relativeData.Dispose();
        commonSettingWrappers.Dispose();
        nativeLayers.Dispose();
        allLayers.Dispose();
    }

    /// <summary>
    /// height maps with per height map erosion settings are sent here to be generated then eroded and
    /// parsed back to the caller for further processing
    /// </summary>
    /// <param name="noiseSettings"></param>
    /// <param name="mapSettings"></param>
    /// <param name="erosionResults"></param>
    /// <returns></returns>
    private static JobHandle GenerateErodedMaps(NoiseSettings[] noiseSettings, MeshAreaSettings mapSettings, out NativeArray<HeightMapElement> erosionResults)
    {
        List<NoiseSettings> erosionLayers = new();
        int largestBrush = int.MinValue;
        for (int i = 0; i < noiseSettings.Length; i++)
        {
            if (noiseSettings[i].RigidNoise.erosionSettings.erosion)
            {
                erosionLayers.Add(noiseSettings[i]);
                if(noiseSettings[i].RigidNoise.erosionSettings.erosionBrushRadius > largestBrush)
                {
                    largestBrush = noiseSettings[i].RigidNoise.erosionSettings.erosionBrushRadius;
                }
            }
        }
        
        int totalIterations = 0;
        int erosionMaxDimentions = (mapSettings.mapDimentions.x + largestBrush * 2) * (mapSettings.mapDimentions.y + largestBrush * 2);

        NativeArray<NoiseSettings> nativeLayers = new(erosionLayers.ToArray(), Allocator.TempJob);
        erosionResults = new(erosionMaxDimentions * nativeLayers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        NativeArray<int2> brushRanges = new(nativeLayers.Length,Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        List<int> brushIndexOffsets = new(largestBrush * largestBrush * nativeLayers.Length);
        List<float> brushWeights = new(largestBrush * largestBrush * nativeLayers.Length);
        for (int i = 0; i < nativeLayers.Length; i++)
        {
            NoiseSettings value = nativeLayers[i];
            value.erosionSettings.largestBrush = largestBrush;
            value.erosionSettings.mapSize = mapSettings.mapDimentions;
            value.erosionSettings.mapSizeWithBorder =new( mapSettings.mapDimentions.x + largestBrush * 2, mapSettings.mapDimentions.x + largestBrush * 2);
            totalIterations += value.erosionSettings.erosionIterations;
            nativeLayers[i] = value;
            float weightSum = 0;
            int brushRadius = value.erosionSettings.erosionBrushRadius;
            int2 brushIndex = brushIndexOffsets.Count;
            for (int brushY = -brushRadius; brushY <= brushRadius; brushY++)
            {
                for (int brushX = -brushRadius; brushX <= brushRadius; brushX++)
                {
                    float sqrDst = brushX * brushX + brushY * brushY;
                    if(sqrDst < brushRadius * brushRadius)
                    {
                        brushIndexOffsets.Add(brushY * mapSettings.mapDimentions.x + brushX);
                        float brushWeight = 1 - math.sqrt(sqrDst) / brushRadius;
                        weightSum+= brushWeight;
                        brushWeights.Add(brushWeight);
                    }
                }
            }
            for (int j = 0; j < brushWeights.Count; j++)
            {
                brushWeights[j] /= weightSum;
            }
            brushIndex.y = brushIndexOffsets.Count;
            brushRanges[i] = brushIndex;
        }

        NativeParallelHashSet<int> excludedLayers = new(1,Allocator.TempJob);

        NativeArray<RandomErosionElement> randomIndices = new(totalIterations, Allocator.TempJob);
        NativeArray<int> layerStartIndices = new(nativeLayers.Length,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
        for (int i = nativeLayers.Length-1; i >= 0; i--)
        {
            totalIterations -= nativeLayers[i].erosionSettings.erosionIterations;
            layerStartIndices[i] = totalIterations;
        }

        var noiseGenerator = new CommonNoiseGenerator
        {
            areaSettings = mapSettings,
            noiseSettings = nativeLayers,
            allHeightMaps = erosionResults,
            excludeLayers = excludedLayers,
        };

        noiseGenerator.areaSettings.mapDimentions = nativeLayers[0].erosionSettings.mapSizeWithBorder;

        JobHandle eroder = excludedLayers.Dispose(noiseGenerator.ScheduleParallel(erosionResults.Length,448,default));
        var indexGenerator = new RandomIndexGenerator
        {
            mapSettings = mapSettings,
            eroisonSettings = nativeLayers,
            layerStartIndices = layerStartIndices,
            randomIndices = randomIndices
        };
        //eroder = JobHandle.CombineDependencies(indexGenerator.Schedule(randomIndices.Length, 64),eroder);
        
        indexGenerator.Schedule(randomIndices.Length,64,default).Complete();
        var erosionJob = new BigErodeJob
        {
            mapSettings = mapSettings,
            erosionSettings= nativeLayers,
            brushRanges = brushRanges,
            brushIndexOffsets = new(brushIndexOffsets.ToArray(),Allocator.TempJob),
            brushWeights = new(brushWeights.ToArray(), Allocator.TempJob),
            randomIndices= randomIndices,
            heightMap = erosionResults
        };

        return nativeLayers.Dispose(erosionJob.Schedule(randomIndices.Length,64,eroder));
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
            handle = colouringJob.Schedule(allLayers.Length, 128, handle);
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
            handle = colouringJob.Schedule(allLayers.Length, 128, handle);
        }
        return handle;
    }

    /// <summary>
    /// Calculates the lowest, heighest and absolute mid point of a given height map
    /// Required by the mesh generator.
    /// </summary>
    /// <param name="mapSettings">Main map settings </param>
    /// <param name="heightMap"> height map to process</param>
    /// <returns> relative noise data </returns>
    public static RelativeNoiseData CalculateRelativeNoiseData(NativeArray<HeightMapElement> heightMap)
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
        relativeData.Dispose();
        return data;
    }

    /// <summary>
    /// This varient is used by the terrain generation system to calculate the same information
    /// as the above method for each height map in the main working array "heightMaps".
    /// Unlike the above, this does not complete its job handle and returns it for better job scheduling behaviour.
    /// It can be set to ignore height maps that aren't being clamped improving performance in some cases.
    /// </summary>
    /// <param name="jobHandle"></param>
    /// <param name="mapSettings"></param>
    /// <param name="relativeData"></param>
    /// <param name="commonSettings"></param>
    /// <param name="heightMaps"></param>
    /// <returns></returns>
    public static JobHandle BigCalculateRelativeNoiseData(JobHandle jobHandle, MeshAreaSettings mapSettings, NativeArray<RelativeNoiseData> relativeData, NativeArray<CommonSettingsWrapper> commonSettings, NativeArray<HeightMapElement> heightMaps, bool ignoreUnClamped = false)
    {

        var heightMapMinMaxer = new BigHeightMapMinMaxCal
        {
            IgnoreUnClamped = ignoreUnClamped,
            minMax = relativeData,
            mapSettings = mapSettings,
            HeightMap = heightMaps,
            commonSettings = commonSettings,
        };

        return heightMapMinMaxer.Schedule(commonSettings.Length, 1, jobHandle);
    }

    /// <summary>
    /// values below a certain minimum in relativeData are clamped to the minimum to produce a flat floor or water level
    /// This clamps all heights maps that are set to be clamped in their settings.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="heightMap"></param>
    /// <param name="mapSettings"></param>
    /// <param name="commonSettings"></param>
    /// <param name="relativeData"></param>
    /// <returns></returns>
    public static JobHandle BigClampToFlatFloor(JobHandle handle,NativeArray<HeightMapElement> heightMap, MeshAreaSettings mapSettings, NativeArray<CommonSettingsWrapper> commonSettings,NativeArray<RelativeNoiseData> relativeData)
    {
        var heightMapClamper = new BigHeightMapClamper
        {
            mapSettings = mapSettings,
            relativeNoiseData = relativeData,
            HeightMap = heightMap,
            commonSettings = commonSettings
        };

        return heightMapClamper.Schedule(heightMap.Length, 32,handle);
    }

}
