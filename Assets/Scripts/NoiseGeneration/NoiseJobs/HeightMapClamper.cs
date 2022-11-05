using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Cinemachine;

[BurstCompile]
public struct HeightMapClamper : IJobParallelFor
{
    public MeshAreaSettings mapSettings;
    public Color floorColour;
    public RelativeNoiseData relativeNoiseData;
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        HeightMapElement element = HeightMap[index];
        float value = element.Value;
        float zeroOffset = (relativeNoiseData.minValue - relativeNoiseData.minMax.x);
        float minValue = math.lerp(relativeNoiseData.minMax.x, relativeNoiseData.minMax.y, relativeNoiseData.flatFloor);
        /// // VC
        /// if(element.Value < minValue)
        /// {
        ///     float colourWeight = math.clamp(minValue - element.Value, 0.0f, 1.0f);
        ///     element.Colour = math.lerp( element.Colour, (Vector4)floorColour, colourWeight);
        /// }

        if (element.Value < minValue)
        {
            float colourWeight = math.clamp(minValue - element.Value, 0.0f, 1.0f);
            if (mapSettings.shader == ShaderPicker.BVC)
            {
                element.Colour.x = colourWeight;
                element.slopeBlend.y = 0f;
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, floorColour.ToFloat4(), colourWeight);
            }
            else if(mapSettings.shader == ShaderPicker.ABVC)
            {
                element.slopeBlend.y = 0f;
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, floorColour.ToFloat4(), colourWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, floorColour.ToFloat4(), colourWeight);
                element.RimColour = math.lerp(element.RimColour, floorColour.ToFloat4(), colourWeight);
                // element.RimColour =  colourWeight;

                element.rimFac = math.lerp(element.rimFac,0f,colourWeight) ;

                // element.flatMaxHeight = math.lerp(element.flatMaxHeight, (Vector4)floorColour, colourWeight);
                // element.heightFade = math.lerp(element.heightFade, (Vector4)floorColour, colourWeight);
            }
            else if(mapSettings.shader == ShaderPicker.ABVCTextured)
            {
                element.slopeBlend.y = 0f;
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, floorColour.ToFloat4(), colourWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, floorColour.ToFloat4(), colourWeight);
                element.RimColour = math.lerp(element.RimColour, floorColour.ToFloat4(), colourWeight);
                element.rimFac = math.lerp(element.rimFac, 0f, colourWeight);
                element.secondaryTextureIndex = element.mainTextureIndex;
                element.mainTextureIndex = 0;
                element.secondaryBlendMul = 1f - colourWeight;
            }
        }
        element.Value = math.max(value, minValue) + zeroOffset;

        HeightMap[index] = element;
    }
}

[BurstCompile]
public struct BigHeightMapClamper : IJobParallelFor
{
    public MeshAreaSettings mapSettings;
    [ReadOnly]
    public NativeArray<CommonSettingsWrapper> colourWrappers;
    [ReadOnly]
    public NativeArray<RelativeNoiseData> relativeNoiseData;
    public NativeArray<HeightMapElement> HeightMap;
    public void Execute(int index)
    {
        int layerSize = mapSettings.mapDimentions.x * mapSettings.mapDimentions.y;

        int layerIndex = index / layerSize;
        CommonSettingsWrapper simpleNoise = colourWrappers[layerIndex];
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
                floorColour = layerIndex == 0 ? mapSettings.floorColour : colourWrappers[layerIndex - 1].bvcSettings.upper;
                element.Colour.x = colourWeight;
                element.slopeBlend.y = 0f;
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, floorColour.ToFloat4(), colourWeight);
            }
            else if (mapSettings.shader == ShaderPicker.ABVC)
            {
                floorColour = layerIndex == 0 ? mapSettings.floorColour : colourWrappers[layerIndex - 1].abvcSettings.mainColour;
                element.slopeBlend.y = 0f;
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, floorColour.ToFloat4(), colourWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, floorColour.ToFloat4(), colourWeight);
                element.RimColour = math.lerp(element.RimColour, floorColour.ToFloat4(), colourWeight);
                element.rimFac = math.lerp(element.rimFac, 0f, colourWeight);
            }
            else if (mapSettings.shader == ShaderPicker.ABVCTextured)
            {
                floorColour = layerIndex == 0 ? mapSettings.floorColour : colourWrappers[layerIndex - 1].abvcSettings.mainColour;
                element.slopeBlend.y = 0f;
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, floorColour.ToFloat4(), colourWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, floorColour.ToFloat4(), colourWeight);
                element.RimColour = math.lerp(element.RimColour, floorColour.ToFloat4(), colourWeight);
                element.rimFac = math.lerp(element.rimFac, 0f, colourWeight);
                element.secondaryTextureIndex = element.mainTextureIndex;
                element.mainTextureIndex = 0;
                element.secondaryBlendMul = 1f - colourWeight;
            }
        }
        element.Value = math.max(value, minValue) + zeroOffset;

        HeightMap[index] = element;
    }
}
