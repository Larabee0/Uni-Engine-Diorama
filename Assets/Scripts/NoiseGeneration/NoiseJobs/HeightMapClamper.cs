using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Cinemachine;

[BurstCompile]
public struct BigHeightMapClamper : IJobParallelFor
{
    public MeshAreaSettings mapSettings;
    [ReadOnly]
    public NativeArray<CommonSettingsWrapper> commonSettings;
    [ReadOnly]
    public NativeArray<RelativeNoiseData> relativeNoiseData;
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        int layerSize = mapSettings.mapDimentions.x * mapSettings.mapDimentions.y;

        int layerIndex = index / layerSize;
        CommonSettingsWrapper simpleNoise = commonSettings[layerIndex];
        if (!simpleNoise.clampToFloor)
        {
            return;
        }

        RelativeNoiseData relativeData = relativeNoiseData[layerIndex];
        Color floorColour;

        HeightMapElement element = HeightMap[index];
        float value = element.Value;
        float zeroOffset = relativeData.minValue - relativeData.minMax.x;
        float minValue = math.lerp(relativeData.minMax.x, relativeData.minMax.y, simpleNoise.floorPercentage);
        
        if (element.Value < minValue)
        {
            float colourWeight = math.clamp(minValue - element.Value, 0.0f, 1.0f);
            if (mapSettings.shader == ShaderPicker.BVC)
            {
                floorColour = layerIndex == 0 ? mapSettings.floorColour : commonSettings[layerIndex - 1].bvcSettings.upper;
                element.Colour.x = colourWeight;
                element.slopeBlend.y = 0f;
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, floorColour.ToFloat4(), colourWeight);
            }
            else if (mapSettings.shader == ShaderPicker.ABVC)
            {
                floorColour = layerIndex == 0 ? mapSettings.floorColour : commonSettings[layerIndex - 1].abvcSettings.mainColour;
                element.slopeBlend.y = 0f;
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, floorColour.ToFloat4(), colourWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, floorColour.ToFloat4(), colourWeight);
                element.RimColour = math.lerp(element.RimColour, floorColour.ToFloat4(), colourWeight);
                element.rimFac = math.lerp(element.rimFac, 0f, colourWeight);
            }
            else if (mapSettings.shader == ShaderPicker.ABVCTextured)
            {
                floorColour = layerIndex == 0 ? mapSettings.floorColour : commonSettings[layerIndex - 1].abvcSettings.mainColour;
                element.slopeBlend.y = 0f;
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, floorColour.ToFloat4(), colourWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, floorColour.ToFloat4(), colourWeight);
                element.RimColour = math.lerp(element.RimColour, floorColour.ToFloat4(), colourWeight);
                element.rimFac = math.lerp(element.rimFac, 0f, colourWeight);
                element.secondaryTextureIndex = element.mainTextureIndex;
                element.mainTextureIndex = layerIndex - 1 >= 0 ? commonSettings[layerIndex - 1].abvcSettings.MainTextureIndex : 0;
                element.secondaryBlendMul = 1f - colourWeight;
            }
        }
        element.Value = math.max(value, minValue) + zeroOffset + commonSettings[layerIndex].offsetValue;

        HeightMap[index] = element;
    }
}
