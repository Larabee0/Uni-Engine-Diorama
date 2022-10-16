using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct HeightMapClamper : IJobParallelFor
{
    public Color floorColour;
    public RelativeNoiseData relativeNoiseData;
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        HeightMapElement element = HeightMap[index];
        float value = element.Value;
        float zeroOffset = (relativeNoiseData.minValue - relativeNoiseData.minMax.x);
        float minValue = math.lerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, relativeNoiseData.flatFloor);
        /// // VC
        /// if(element.Value < minValue)
        /// {
        ///     float colourWeight = math.clamp(minValue - element.Value, 0.0f, 1.0f);
        ///     element.Colour = math.lerp( element.Colour, (Vector4)floorColour, colourWeight);
        /// }

        if (element.Value < minValue)
        {
            float colourWeight = math.clamp(minValue - element.Value, 0.0f, 1.0f);
            element.Colour.x = colourWeight;
            element.slopeBlend.y = 0f;
            element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, (Vector4)floorColour, colourWeight);
            // element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, heightMap[index].upperLowerColours.c1, colourWeight);
        }

        element.Value = math.max(value, minValue) + zeroOffset;


        HeightMap[index] = element;
    }
}
