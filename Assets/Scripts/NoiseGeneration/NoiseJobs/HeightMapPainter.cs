using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct HeightMapPainterBVC : IJobParallelFor
{
    public SimpleNoise noiseSettings;
    public RelativeNoiseData relativeNoiseData;

    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        HeightMapElement element = HeightMap[index];
        float weight = math.unlerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, element.Value);

        element.Colour = (Vector4)Color.Lerp(noiseSettings.bvcSettings.lower, noiseSettings.bvcSettings.upper, weight);

        // BVC
        element.Colour.x = weight;
        element.slopeBlend = new(noiseSettings.bvcSettings.slopeThreshold, noiseSettings.bvcSettings.blendAmount);
        element.upperLowerColours = new float4x2((Vector4)noiseSettings.bvcSettings.lower, (Vector4)noiseSettings.bvcSettings.upper);
        HeightMap[index] = element;
    }
}

[BurstCompile]
public struct HeightMapPainterABVC : IJobParallelFor
{
    public SimpleNoise noiseSettings;
    public RelativeNoiseData relativeNoiseData;

    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        HeightMapElement element = HeightMap[index];
        float weight = math.unlerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, element.Value);

        element.slopeBlend = new float2(noiseSettings.abvcSettings.slopeThreshold, noiseSettings.abvcSettings.blendAmount);
        element.upperLowerColours = new float4x2((Vector4)noiseSettings.abvcSettings.mainColour, (Vector4)noiseSettings.abvcSettings.flatColour);        
        element.RimColour = (Vector4)noiseSettings.abvcSettings.rimColour;

        element.flatMaxHeight = noiseSettings.abvcSettings.flatMaxHeight;
        element.heightFade = noiseSettings.abvcSettings.heightFade;
        element.rimPower = noiseSettings.abvcSettings.rimPower;
        element.rimFac = noiseSettings.abvcSettings.rimFacraction;
        element.absMaxHeight = noiseSettings.abvcSettings.absolutelMaxHeight;

        HeightMap[index] = element;
    }
}