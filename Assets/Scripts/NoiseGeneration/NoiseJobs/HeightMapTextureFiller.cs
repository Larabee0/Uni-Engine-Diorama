using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct FillTexture : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<HeightMapElement> source;
    [WriteOnly]
    public NativeArray<Color32> Destination;
    public void Execute(int i)
    {
        Destination[i] = (Color)(Vector4)source[i].Colour;
    }
}
