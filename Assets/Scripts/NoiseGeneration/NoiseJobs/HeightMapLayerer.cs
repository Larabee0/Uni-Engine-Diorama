using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Job takes 2 height maps, and combines them together into one result
/// Currently this is weighted by the first height map,
/// closer to the ceiling of HM1 increases the weight to 1f for HM2
/// closer to the floor of HM1 lowers the weight to 0f for HM2
/// </summary>
[BurstCompile]
public struct HeightMapLayerer : IJobParallelFor
{
    public MeshAreaSettings mapSettings;
    public RelativeNoiseData baseRelative;
    public RelativeNoiseData heightMapRelative;

    [ReadOnly]
    public NativeArray<HeightMapElement> baseLayer;

    [ReadOnly]
    public NativeArray<HeightMapElement> targetLayer;

    public NativeArray<HeightMapElement> resultMap;

    // BVC
    public void Execute(int index)
    {
        HeightMapElement element = resultMap[index];
        float @base = baseLayer[index].Value;
        HeightMapElement target = targetLayer[index];

        float baseWeight = math.unlerp(baseRelative.minMax.x, baseRelative.minMax.y, @base);

        float mask = math.lerp(element.Value, element.Value + target.Value, baseWeight);

        if (element.Value < mask)
        {
            float extraWeight = math.clamp(mask - element.Value, 0.0f, 1.0f);
            if (mapSettings.shader == ShaderPicker.BVC)
            {
                element.Colour = math.lerp(element.Colour, target.Colour, extraWeight);
                element.slopeBlend = math.lerp(element.slopeBlend, target.slopeBlend, extraWeight);
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, target.upperLowerColours.c0, extraWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, target.upperLowerColours.c1, extraWeight);
            }
            else if (mapSettings.shader == ShaderPicker.ABVC )
            {
                element.slopeBlend = math.lerp(element.slopeBlend, target.slopeBlend, extraWeight);

                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, target.upperLowerColours.c0, extraWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, target.upperLowerColours.c1, extraWeight);
                element.RimColour = math.lerp(element.RimColour, target.RimColour, extraWeight);
                element.flatMaxHeight = math.lerp(element.flatMaxHeight, target.flatMaxHeight, extraWeight);
                element.heightFade = math.lerp(element.heightFade, target.heightFade, extraWeight);
                element.rimPower = math.lerp(element.rimPower, target.rimPower, extraWeight);
                element.rimFac = math.lerp(element.rimFac, target.rimFac, extraWeight);
                element.absMaxHeight = math.lerp(element.absMaxHeight, target.absMaxHeight, extraWeight);
            }
            else if ( mapSettings.shader == ShaderPicker.ABVCTextured)
            {
                element.slopeBlend = math.lerp(element.slopeBlend, target.slopeBlend, extraWeight);

                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, target.upperLowerColours.c0, extraWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, target.upperLowerColours.c1, extraWeight);
                element.RimColour = math.lerp(element.RimColour, target.RimColour, extraWeight);
                element.flatMaxHeight = math.lerp(element.flatMaxHeight, target.flatMaxHeight, extraWeight);
                element.heightFade = math.lerp(element.heightFade, target.heightFade, extraWeight);
                element.rimPower = math.lerp(element.rimPower, target.rimPower, extraWeight);
                element.rimFac = math.lerp(element.rimFac, target.rimFac, extraWeight);
                element.absMaxHeight = math.lerp(element.absMaxHeight, target.absMaxHeight, extraWeight);

                if(extraWeight > 0.5f)
                {
                    element.secondaryTextureIndex = element.mainTextureIndex;
                    element.mainTextureIndex = target.mainTextureIndex;
                    element.secondaryBlendMul = 1f-extraWeight;
                }
                else
                {
                    element.mainTextureIndex = element.secondaryTextureIndex;
                    element.secondaryTextureIndex = target.mainTextureIndex;
                    element.secondaryBlendMul = extraWeight;
                }
                element.secondaryTextureIndex = element.mainTextureIndex;
                element.mainTextureIndex = target.mainTextureIndex;
                element.secondaryBlendMul = 1f - extraWeight;
            }
        }
        element.Value = math.max(element.Value, mask);

        resultMap[index] = element;
    }

    public void ExecuteVC(int index)
    {
        HeightMapElement element = resultMap[index];
        float baseValue = baseLayer[index].Value;
        float hm = targetLayer[index].Value;

        float baseWeight = math.unlerp(baseRelative.minMax.x, baseRelative.minMax.y, baseValue);
        float hmWeight = math.unlerp(heightMapRelative.minMax.x, heightMapRelative.minMax.y, hm);
        float hmBaseWeight = math.unlerp(heightMapRelative.minMax.x, heightMapRelative.minMax.y, baseValue);

        float riseUp = math.lerp(heightMapRelative.minValue, hmWeight, baseWeight);
        float mask = math.lerp(element.Value, element.Value + hm, baseWeight);
        float4 colourMask = math.lerp(element.Colour, targetLayer[index].Colour, baseWeight);

        if (element.Value < mask)
        {
            float extraWeight = math.clamp(mask - element.Value, 0.0f, 1.0f);
            element.Colour = math.lerp(element.Colour, targetLayer[index].Colour, extraWeight);
        }
        element.Value = math.max(element.Value, mask);

        //element.Colour = element.Value == mask ? colourMask:element.Colour;
        resultMap[index] = element;
        //result[index] = math.lerp(result[index], hm* mask, hmWeight);
    }

    // not going to implement
    private Color BlendWithNeighbours()
    {
        // go throug the 4 neighbouring nodes and blend the colours between them and the current
        // this needst to be impemeneted in a seperate job run at the end  - run it in the texture generator.
        return Color.white;
    }

    // thursday 06 october
    private void StartOfDayExecute(int index)
    {
        float baseValue = baseLayer[index].Value;
        float hm = targetLayer[index].Value;

        float baseWeight = math.unlerp(baseRelative.minMax.x, baseRelative.minMax.y, baseValue);
        float hmWeight = math.unlerp(heightMapRelative.minMax.x, heightMapRelative.minMax.y, baseValue);

        float riseUp = math.lerp(heightMapRelative.minValue, hmWeight, baseWeight);
        float mask = math.lerp(resultMap[index].Value, hm + riseUp, hmWeight * baseWeight * riseUp);

        // result[index] +=  hm * baseValue;
        HeightMapElement element = resultMap[index];
        element.Value = mask;
        resultMap[index] = element;
        // result[index] = math.lerp(result[index], hm* mask, hmWeight);
    }
}


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

[BurstCompile]
public struct BigHeightMapLayerer : IJobParallelFor
{
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

        float mask = math.lerp(result.Value, result.Value + target.Value, baseWeight);

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