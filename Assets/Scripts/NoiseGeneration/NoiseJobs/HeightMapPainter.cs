using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct HeightMapPainter : IJobParallelFor
{
    public SimpleNoise noiseSettings;
    public RelativeNoiseData relativeNoiseData;

    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        HeightMapElement element = HeightMap[index];
        float weight = math.unlerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, element.Value);

        element.Colour = (Vector4)Color.Lerp(noiseSettings.lower, noiseSettings.upper, weight);

        // BVC
        element.Colour.x = weight;
        element.slopeBlend = new(noiseSettings.slopeThreshold, noiseSettings.blendAmount);
        element.upperLowerColours = new float4x2((Vector4)noiseSettings.lower, (Vector4)noiseSettings.upper);
        HeightMap[index] = element;
    }
}
