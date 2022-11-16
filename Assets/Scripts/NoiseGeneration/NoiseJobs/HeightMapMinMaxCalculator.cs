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


[BurstCompile]
public struct BigHeightMapMinMaxCal : IJobParallelFor
{
    public bool IgnoreUnClamped;
    public MeshAreaSettings mapSettings;
    public NativeArray<RelativeNoiseData> minMax;

    [ReadOnly]
    public NativeArray<CommonSettingsWrapper> commonSettings;
    [ReadOnly]
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        if (!commonSettings[index].clampToFloor && IgnoreUnClamped)
        {
            return;
        }
        int layerSize = mapSettings.mapDimentions.x * mapSettings.mapDimentions.y;
        int startIndex = index * layerSize;
        int endIndex = startIndex+layerSize;
        float2 minMax = new(float.MaxValue, float.MinValue);
        for (; startIndex < endIndex; startIndex++)
        {
            float value = HeightMap[startIndex].Value;
            minMax.x = value < minMax.x ? value : minMax.x;
            minMax.y = value > minMax.y ? value : minMax.y;
        }
        RelativeNoiseData data = this.minMax[index];
        data.minMax = minMax;
        data.mid = (minMax.x + minMax.y) / 2f;
        data.flatFloor = mapSettings.floorPercentage;
        data.minValue = commonSettings[index].minValue;
        this.minMax[index] = data;
    }
}
