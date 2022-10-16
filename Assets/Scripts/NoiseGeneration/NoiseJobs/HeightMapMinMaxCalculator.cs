using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct HeightMapMinMaxCal : IJob
{
    public NativeReference<RelativeNoiseData> minMax;
    [ReadOnly]
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute()
    {
        float2 minMax = new(float.MaxValue, float.MinValue);
        for (int i = 0; i < HeightMap.Length; i++)
        {
            float value = HeightMap[i].Value;
            minMax.x = value < minMax.x ? value : minMax.x;
            minMax.y = value > minMax.y ? value : minMax.y;
        }
        RelativeNoiseData data = this.minMax.Value;
        data.minMax = minMax;
        data.mid = (minMax.x + minMax.y) / 2f;
        this.minMax.Value = data;
    }
}
