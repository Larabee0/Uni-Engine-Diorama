using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// Rigid Noise algorithim take from https://github.com/SebLague/Procedural-Planets/blob/master/Procedural%20Planet%20E07/RidgidNoiseFilter.cs
// under the MIT licence adapted for 2D height map generation and C# Jobs by myself

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
        float2 percent = new float2(x, y) / (settings.Resolution - 1);

        HeightMapElement element = allHeightMaps[index];
        float noiseValue = element.Value;
        float frequency = settings.BaseRoughness;
        float amplitude = 1;
        float weight = 1;

        for (int i = 0; i < settings.NumLayers; i++)
        {
            float v = 1 - math.abs(noise.cnoise(percent * frequency + settings.Centre));
            v *= v;
            v *= weight;
            weight = math.clamp(v * settings.weightMultiplier, 0f, 1f);

            noiseValue += v * amplitude;
            frequency *= settings.Roughness;
            amplitude *= settings.Persistence;
        }

        noiseValue -= settings.OffsetValue;
        element.Value = noiseValue * settings.Strength;
        allHeightMaps[index] = element;
    }
}
