using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Simple noise algorithim take from https://github.com/SebLague/Procedural-Planets under the MIT licence
// adapted for 2D height map generation and C# Jobs by myself
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

        float2 percent = new float2(x, y) / (simpleNoise.resolution - 1);
        HeightMapElement element = heightMap[index];
        float noiseValue = element.Value;
        float frequency = simpleNoise.baseRoughness;
        float amplitude = 1;

        for (int i = 0; i < simpleNoise.numLayers; i++)
        {
            float v = noise.cnoise(percent * frequency + simpleNoise.centre);
            noiseValue += (v + 1) * 0.5f * amplitude;
            frequency *= simpleNoise.roughness;
            amplitude *= simpleNoise.persistence;
        }
        //noiseValue -= simpleNoise.offsetValue;
        element.Value = noiseValue * simpleNoise.strength;
        heightMap[index] = element;
    }
}
