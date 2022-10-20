using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct FillTexture : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<float4> source;
    [WriteOnly]
    public NativeArray<Color32> Destination;
    public void Execute(int i)
    {
        Destination[i] = source[i].ToColor();
    }
}

[BurstCompile]
public struct FillTextureColor32 : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<Color32> source;
    [WriteOnly]
    public NativeArray<Color32> destination;
    public void Execute(int i)
    {
        destination[i] = source[i];
    }
}