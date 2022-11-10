using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct CommonNoiseGenerator : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<NoiseSettings> noiseSettings;
    public MeshAreaSettings areaSettings;
    public NativeArray<HeightMapElement> allHeightMaps;
    public void Execute(int index)
    {

        int mapArrayLength = areaSettings.mapDimentions.x * areaSettings.mapDimentions.y;

        int settingsIndex = index / mapArrayLength;

        int localOffset = settingsIndex * mapArrayLength;

        float2 xy = new()
        {
            x = ((float)(index - localOffset) % areaSettings.mapDimentions.x) - areaSettings.mapDimentions.x / 2,
            y = ((float)(index - localOffset) / areaSettings.mapDimentions.x) - areaSettings.mapDimentions.y / 2
        };

        NoiseSettings settings = noiseSettings[settingsIndex];

        allHeightMaps[index] = settings.layerType switch
        {
            LayerType.Simple => SimpleNoiseGenerator(index, xy, settings.SimpleNoise),
            LayerType.Rigid => RigidNoiseGenerator(index, xy, settings.RigidNoise),
            _ => allHeightMaps[index]
        };
    }

    private HeightMapElement SimpleNoiseGenerator(int index, float2 xy , SimpleNoise settings)
    {
        HeightMapElement element = allHeightMaps[index];
        float2 percent = xy / (settings.Resolution - 1);
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
        noiseValue += settings.OffsetValue;
        element.Value = noiseValue * settings.Strength;

        return element;
    }

    private HeightMapElement RigidNoiseGenerator(int index, float2 xy, RigidNoise settings)
    {

        HeightMapElement element = allHeightMaps[index];
        float2 percent = xy / (settings.Resolution - 1);
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

        noiseValue += settings.OffsetValue;
        element.Value = noiseValue * settings.Strength;
        return element;
    }
}