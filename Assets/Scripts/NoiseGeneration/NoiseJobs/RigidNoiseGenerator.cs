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
        float x = ((float)index % areaSettings.mapDimentions.x) - areaSettings.mapDimentions.x / 2;
        float y = ((float)index / areaSettings.mapDimentions.x) - areaSettings.mapDimentions.y / 2;

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

[BurstCompile]
public struct BigRigidNoiseHeightMapGenerator : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<RigidNoise> rigidNoiseSettings;
    public MeshAreaSettings areaSettings;
    public NativeArray<HeightMapElement> allHeightMaps;
    public void Execute(int index)
    {
        int mapArrayLength = areaSettings.mapDimentions.x * areaSettings.mapDimentions.y;

        int settingIndex = index / mapArrayLength;

        int localOffset = settingIndex * mapArrayLength;

        float x = ((float)(index - localOffset) % areaSettings.mapDimentions.x) - areaSettings.mapDimentions.x / 2;
        float y = ((float)(index - localOffset) / areaSettings.mapDimentions.x) - areaSettings.mapDimentions.y / 2;

        RigidNoise settings = rigidNoiseSettings[settingIndex];
        float2 percent = new float2(x, y) / (settings.resolution - 1);

        HeightMapElement element = allHeightMaps[index];
        float noiseValue = element.Value;
        float frequency = settings.baseRoughness;
        float amplitude = 1;
        float weight = 1;

        for (int i = 0; i < settings.numLayers; i++)
        {
            float v = 1 - math.abs(noise.cnoise(percent * frequency + settings.centre));
            v *= v;
            v *= weight;
            weight = math.clamp(v * settings.weightMultiplier, 0f, 1f);

            noiseValue += v * amplitude;
            frequency *= settings.roughness;
            amplitude *= settings.persistence;
        }

        noiseValue -= settings.offsetValue;
        element.Value = noiseValue * settings.strength;
        allHeightMaps[index] = element;
    }
}
