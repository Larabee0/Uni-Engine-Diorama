using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static class Erosion
{
    public static JobHandle Erode(NativeArray<HeightMapElement> heightMap, ErodeSettings settings, JobHandle dependency)
    {
        NativeList<int> brushIndexOffsets = new(settings.erosionBrushRadius * settings.erosionBrushRadius, Allocator.TempJob);
        NativeList<float> brushWeights = new(settings.erosionBrushRadius * settings.erosionBrushRadius, Allocator.TempJob);

        float weightSum = 0;

        for (int brushY = -settings.erosionBrushRadius; brushY <= settings.erosionBrushRadius; brushY++)
        {
            for (int brushX = -settings.erosionBrushRadius; brushX <= settings.erosionBrushRadius; brushX++)
            {
                float sqrDst = brushX * brushX + brushY * brushY;
                if (sqrDst < settings.erosionBrushRadius * settings.erosionBrushRadius)
                {
                    brushIndexOffsets.Add(brushY * settings.mapSizeWithBorder.x + brushX);
                    float brushWeight = 1 - math.sqrt(sqrDst) / settings.erosionBrushRadius;
                    weightSum += brushWeight;
                    brushWeights.Add(brushWeight);
                }
            }
        }

        for (int i = 0; i < brushWeights.Length; i++)
        {
            brushWeights[i] /= weightSum;
        }

        var erodeJob = new ErodeJob
        {
            settings = settings,
            brushIndexOffsets = brushIndexOffsets,
            brushWeights = brushWeights,
            heightMap = heightMap
        };

        return brushIndexOffsets.Dispose(brushWeights.Dispose(erodeJob.Schedule(settings.erosionIterations, 64, dependency)));
    }
}
