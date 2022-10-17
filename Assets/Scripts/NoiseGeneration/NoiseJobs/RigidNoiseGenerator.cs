using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Rigid Noise algorithim take from https://github.com/SebLague/Procedural-Planets under the MIT licence
// adapted for 2D height map generation and C# Jobs by myself
[BurstCompile]
public struct RigidNoiseHeightMapGenerator : IJobParallelFor
{
    public RigidNoise rigidNoise;
    public MeshAreaSettings areaSettings;
    public NativeArray<HeightMapElement> heightMap;
    public void Execute(int index)
    {
        float x = (float)index % areaSettings.mapDimentions.x;
        float y = (float)index / areaSettings.mapDimentions.x;

        float2 percent = new float2(x, y) / (rigidNoise.resolution - 1);

        HeightMapElement element = heightMap[index];
        float noiseValue = element.Value;
        float frequency = rigidNoise.baseRoughness;
        float amplitude = 1;
        float weight = 1;

        for (int i = 0; i < rigidNoise.numLayers; i++)
        {
            float v = 1 - math.abs(noise.cnoise(percent * frequency + rigidNoise.centre));
            v *= v;
            v *= weight;
            weight = math.clamp(v * rigidNoise.weightMultiplier, 0f, 1f);

            noiseValue += v * amplitude;
            frequency *= rigidNoise.roughness;
            amplitude *= rigidNoise.persistence;
        }

        noiseValue -= rigidNoise.offsetValue;
        element.Value = noiseValue * rigidNoise.strength;
        heightMap[index] = element;
    }
}
