using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Simple noise algorithim take from https://github.com/SebLague/Procedural-Planets/blob/master/Procedural%20Planet%20E07/SimpleNoiseFilter.cs
// under the MIT licence adapted for 2D height map generation and C# Jobs by myself
[BurstCompile]
public struct SimpleNoiseHeightMapGenerator : IJobParallelFor
{
    public SimpleNoise simpleNoise;
    public MeshAreaSettings areaSettings;
    public NativeArray<HeightMapElement> heightMap;
    public void Execute(int index)
    {
        float x = ((float)index % areaSettings.mapDimentions.x)-areaSettings.mapDimentions.x/2;
        float y = ((float)index / areaSettings.mapDimentions.x)-areaSettings.mapDimentions.y/2;

        float2 percent = new float2(x, y) / (simpleNoise.Resolution - 1);
        HeightMapElement element = heightMap[index];
        float noiseValue = element.Value;
        float frequency = simpleNoise.BaseRoughness;
        float amplitude = 1;

        for (int i = 0; i < simpleNoise.NumLayers; i++)
        {
            float v = noise.cnoise(percent * frequency + simpleNoise.Centre);
            noiseValue += (v + 1) * 0.5f * amplitude;
            frequency *= simpleNoise.Roughness;
            amplitude *= simpleNoise.Persistence;
        }
        //noiseValue -= simpleNoise.offsetValue;
        element.Value = noiseValue * simpleNoise.Strength;
        heightMap[index] = element;
    }
}


/// <summary>
/// Modified version of the above Job
/// capable of generating all simple layers at once
/// </summary>
[BurstCompile]
public struct BigSimpleNoiseHeightMapGenerator : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<SimpleNoise> simpleNoiseSettings;
    public MeshAreaSettings areaSettings;
    public NativeArray<HeightMapElement> allHeightMaps;
    public void Execute(int index)
    {
        int mapArrayLength = areaSettings.mapDimentions.x * areaSettings.mapDimentions.y;

        int settingIndex = index / mapArrayLength;

        int localOffset = settingIndex * mapArrayLength;

        float x = ((float)(index-localOffset) % areaSettings.mapDimentions.x) - areaSettings.mapDimentions.x / 2;
        float y = ((float)(index-localOffset) / areaSettings.mapDimentions.x) - areaSettings.mapDimentions.y / 2;
        SimpleNoise settings = simpleNoiseSettings[settingIndex];
        float2 percent = new float2(x, y) / (settings.Resolution - 1);
        HeightMapElement element = allHeightMaps[index];
        float noiseValue = element.Value;
        float frequency = settings.BaseRoughness;
        float amplitude = 1;

        for (int i = 0; i < settings.NumLayers; i++)
        {
            float v = noise.cnoise(percent * frequency + settings.Centre);
            noiseValue += (v + 1) * 0.5f * amplitude;
            frequency *= settings.Roughness;
            amplitude *= settings.Persistence;
        }
        noiseValue -= settings.OffsetValue;
        element.Value = noiseValue * settings.Strength;
        allHeightMaps[index] = element;
    }
}
