using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct HeightMapPainterBVC : IJobParallelFor
{
    public CommonSettingsWrapper colourWrapper;
    public RelativeNoiseData relativeNoiseData;

    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        HeightMapElement element = HeightMap[index];
        float weight = math.unlerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, element.Value);

        element.Colour = (Vector4)Color.Lerp(colourWrapper.bvcSettings.lower, colourWrapper.bvcSettings.upper, weight);

        // BVC
        element.Colour.x = weight;
        element.slopeBlend = new(colourWrapper.bvcSettings.slopeThreshold, colourWrapper.bvcSettings.blendAmount);
        element.upperLowerColours = new float4x2((Vector4)colourWrapper.bvcSettings.lower, (Vector4)colourWrapper.bvcSettings.upper);
        HeightMap[index] = element;
    }
}


public struct BigHeightMapPainterBVC : IJobParallelFor
{
    public int2 mapDimentions;
    [ReadOnly]
    public NativeArray<CommonSettingsWrapper> colourWrappers;
    [ReadOnly]
    public NativeArray<RelativeNoiseData> relativeNoiseData;

    public NativeArray<HeightMapElement> allMaps;
    public void Execute(int index)
    {

        int mapArrayLength = mapDimentions.x * mapDimentions.y;

        int settingIndex = index / mapArrayLength;

        CommonSettingsWrapper colourWrapper = colourWrappers[settingIndex];
        HeightMapElement element = allMaps[index];
        float weight = math.unlerp(relativeNoiseData[settingIndex].minMax.x, relativeNoiseData[settingIndex].minMax.y, element.Value);

        element.Colour = (Vector4)Color.Lerp(colourWrapper.bvcSettings.lower, colourWrapper.bvcSettings.upper, weight);

        // BVC
        element.Colour.x = weight;
        element.slopeBlend = new(colourWrapper.bvcSettings.slopeThreshold, colourWrapper.bvcSettings.blendAmount);
        element.upperLowerColours = new float4x2((Vector4)colourWrapper.bvcSettings.lower, (Vector4)colourWrapper.bvcSettings.upper);
        allMaps[index] = element;
    }
}


[BurstCompile]
public struct HeightMapPainterABVC : IJobParallelFor
{
    public CommonSettingsWrapper colourWrapper;
    public RelativeNoiseData relativeNoiseData;

    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        HeightMapElement element = HeightMap[index];
        float weight = math.unlerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, element.Value);

        element.slopeBlend = new float2(colourWrapper.abvcSettings.slopeThreshold, colourWrapper.abvcSettings.blendAmount);
        element.upperLowerColours = new float4x2((Vector4)colourWrapper.abvcSettings.mainColour, (Vector4)colourWrapper.abvcSettings.flatColour);        
        element.RimColour = (Vector4)colourWrapper.abvcSettings.rimColour;

        element.flatMaxHeight = colourWrapper.abvcSettings.flatMaxHeight;
        element.heightFade = colourWrapper.abvcSettings.heightFade;
        element.rimPower = colourWrapper.abvcSettings.rimPower;
        element.rimFac = colourWrapper.abvcSettings.rimFacraction;
        element.absMaxHeight = colourWrapper.abvcSettings.absolutelMaxHeight;
        element.mainTextureIndex = colourWrapper.abvcSettings.MainTextureIndex;

        HeightMap[index] = element;
    }
}

[BurstCompile]
public struct BigHeightMapPainterABVC : IJobParallelFor
{
    public int2 mapDimentions;
    [ReadOnly]
    public NativeArray<CommonSettingsWrapper> colourWrappers;
    [ReadOnly]
    public NativeArray<RelativeNoiseData> relativeNoiseData;

    public NativeArray<HeightMapElement> allMaps;
    public void Execute(int index)
    {
        int mapArrayLength = mapDimentions.x * mapDimentions.y;

        int settingIndex = index / mapArrayLength;

        CommonSettingsWrapper colourWrapper = colourWrappers[settingIndex];
        HeightMapElement element = allMaps[index];
        float weight = math.unlerp(relativeNoiseData[settingIndex].minMax.x, relativeNoiseData[settingIndex].minMax.y, element.Value);

        element.slopeBlend = new float2(colourWrapper.abvcSettings.slopeThreshold, colourWrapper.abvcSettings.blendAmount);
        element.upperLowerColours = new float4x2((Vector4)colourWrapper.abvcSettings.mainColour, (Vector4)colourWrapper.abvcSettings.flatColour);
        element.RimColour = (Vector4)colourWrapper.abvcSettings.rimColour;

        element.flatMaxHeight = colourWrapper.abvcSettings.flatMaxHeight;
        element.heightFade = colourWrapper.abvcSettings.heightFade;
        element.rimPower = colourWrapper.abvcSettings.rimPower;
        element.rimFac = colourWrapper.abvcSettings.rimFacraction;
        element.absMaxHeight = colourWrapper.abvcSettings.absolutelMaxHeight;
        element.mainTextureIndex = colourWrapper.abvcSettings.MainTextureIndex;

        allMaps[index] = element;
    }
}