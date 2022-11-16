using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Parallised Array copying? Yes.
/// </summary>
[BurstCompile]
public struct ResultCopy : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<HeightMapElement> allLayers;
    public NativeArray<HeightMapElement> result;

    public void Execute(int index)
    {
        result[index] = allLayers[index];
    }
}

/// <summary>
/// Job takes 2 height maps, and combines them together into one result.
/// </summary>
[BurstCompile]
public struct BigHeightMapLayerer : IJobParallelFor
{
    public int layerIndex;
    public MeshAreaSettings mapSettings;
    [NativeDisableParallelForRestriction,ReadOnly]
    public NativeArray<RelativeNoiseData> heightMapRelative;

    [ReadOnly]
    public NativeArray<HeightMapElement> baseLayer;
    [ReadOnly]
    public NativeArray<HeightMapElement> targetLayer;
    public NativeArray<HeightMapElement> resultMap;


    public void Execute(int index)
    {
        HeightMapElement result = resultMap[index]; // always indexed from 0.

        HeightMapElement target = targetLayer[index]; // never index from 0

        float @base = baseLayer[index].Value; // always indexed from 0.

        float baseWeight = math.unlerp(heightMapRelative[0].minMax.x, heightMapRelative[0].minMax.y, @base);
        float layerMin = heightMapRelative[layerIndex].minMax.x;

        float mask = math.lerp(result.Value, result.Value + target.Value - layerMin, baseWeight);

        if (result.Value < mask)
        {
            float extraWeight = math.clamp(mask - result.Value, 0.0f, 1.0f);
            if (mapSettings.shader == ShaderPicker.BVC)
            {
                result.Colour = math.lerp(result.Colour, target.Colour, extraWeight);
                result.slopeBlend = math.lerp(result.slopeBlend, target.slopeBlend, extraWeight);
                result.upperLowerColours.c0 = math.lerp(result.upperLowerColours.c0,target.upperLowerColours.c0, extraWeight);
                result.upperLowerColours.c1 = math.lerp(result.upperLowerColours.c1, target.upperLowerColours.c1, extraWeight);
            }
            else if (mapSettings.shader == ShaderPicker.ABVC)
            {
                result.slopeBlend = math.lerp(result.slopeBlend, target.slopeBlend, extraWeight);

                result.upperLowerColours.c0 = math.lerp(result.upperLowerColours.c0, target.upperLowerColours.c0, extraWeight);
                result.upperLowerColours.c1 = math.lerp(result.upperLowerColours.c1, target.upperLowerColours.c1, extraWeight);
                result.RimColour = math.lerp(result.RimColour, target.RimColour, extraWeight);
                result.flatMaxHeight = math.lerp(result.flatMaxHeight, target.flatMaxHeight, extraWeight);
                result.heightFade = math.lerp(result.heightFade, target.heightFade, extraWeight);
                result.rimPower = math.lerp(result.rimPower, target.rimPower, extraWeight);
                result.rimFac = math.lerp(result.rimFac, target.rimFac, extraWeight);
                result.absMaxHeight = math.lerp(result.absMaxHeight, target.absMaxHeight, extraWeight);
            }
            else if (mapSettings.shader == ShaderPicker.ABVCTextured)
            {
                result.slopeBlend = math.lerp(result.slopeBlend, target.slopeBlend, extraWeight);

                result.upperLowerColours.c0 = math.lerp(result.upperLowerColours.c0, target.upperLowerColours.c0, extraWeight);
                result.upperLowerColours.c1 = math.lerp(result.upperLowerColours.c1, target.upperLowerColours.c1, extraWeight);
                result.RimColour = math.lerp(result.RimColour, target.RimColour, extraWeight);
                result.flatMaxHeight = math.lerp(result.flatMaxHeight, target.flatMaxHeight, extraWeight);
                result.heightFade = math.lerp(result.heightFade, target.heightFade, extraWeight);
                result.rimPower = math.lerp(result.rimPower, target.rimPower, extraWeight);
                result.rimFac = math.lerp(result.rimFac, target.rimFac, extraWeight);
                result.absMaxHeight = math.lerp(result.absMaxHeight, target.absMaxHeight, extraWeight);

                if (extraWeight > 0.5f)
                {
                    result.secondaryTextureIndex = result.mainTextureIndex;
                    result.mainTextureIndex = target.mainTextureIndex;
                    result.secondaryBlendMul = 1f - extraWeight;
                }
                else
                {
                    result.mainTextureIndex = result.secondaryTextureIndex;
                    result.secondaryTextureIndex = target.mainTextureIndex;
                    result.secondaryBlendMul = extraWeight;
                }
                result.secondaryTextureIndex = result.mainTextureIndex;
                result.mainTextureIndex = target.mainTextureIndex;
                result.secondaryBlendMul = 1f - extraWeight;
            }
        }
        result.Value = math.max(result.Value, mask);

        resultMap[index] = result;
    }
}