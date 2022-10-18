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
    public NativeArray<HeightMapElement> baseMap;

    [ReadOnly]
    public NativeArray<HeightMapElement> heightMap;

    public NativeArray<HeightMapElement> result;

    // BVC
    public void Execute(int index)
    {
        HeightMapElement element = result[index];
        float baseValue = baseMap[index].Value;
        float hm = heightMap[index].Value;

        float baseWeight = math.unlerp(baseRelative.minMax.x, baseRelative.minMax.y, baseValue);

        float mask = math.lerp(element.Value, element.Value + hm, baseWeight);

        if (element.Value < mask)
        {
            float extraWeight = math.clamp(mask - element.Value, 0.0f, 1.0f);
            if (mapSettings.shader == ShaderPicker.BVC)
            {
                element.Colour = math.lerp(element.Colour, heightMap[index].Colour, extraWeight);
                element.slopeBlend = math.lerp(element.slopeBlend, heightMap[index].slopeBlend, extraWeight);
                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, heightMap[index].upperLowerColours.c0, extraWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, heightMap[index].upperLowerColours.c1, extraWeight);
            }
            else if (mapSettings.shader == ShaderPicker.ABVC || mapSettings.shader == ShaderPicker.ABVCTextured)
            {
                element.slopeBlend = math.lerp(element.slopeBlend, heightMap[index].slopeBlend, extraWeight);

                element.upperLowerColours.c0 = math.lerp(element.upperLowerColours.c0, heightMap[index].upperLowerColours.c0, extraWeight);
                element.upperLowerColours.c1 = math.lerp(element.upperLowerColours.c1, heightMap[index].upperLowerColours.c1, extraWeight);
                element.RimColour = math.lerp(element.RimColour, heightMap[index].RimColour, extraWeight);
                element.flatMaxHeight = math.lerp(element.flatMaxHeight, heightMap[index].flatMaxHeight, extraWeight);
                element.heightFade = math.lerp(element.heightFade, heightMap[index].heightFade, extraWeight);
                element.rimPower = math.lerp(element.rimPower, heightMap[index].rimPower, extraWeight);
                element.rimFac = math.lerp(element.rimFac, heightMap[index].rimFac, extraWeight);
                element.absMaxHeight = math.lerp(element.absMaxHeight, heightMap[index].absMaxHeight, extraWeight);
            }
        }
        element.Value = math.max(element.Value, mask);

        result[index] = element;
    }

    public void ExecuteVC(int index)
    {
        HeightMapElement element = result[index];
        float baseValue = baseMap[index].Value;
        float hm = heightMap[index].Value;

        float baseWeight = math.unlerp(baseRelative.minMax.x, baseRelative.minMax.y, baseValue);
        float hmWeight = math.unlerp(heightMapRelative.minMax.x, heightMapRelative.minMax.y, hm);
        float hmBaseWeight = math.unlerp(heightMapRelative.minMax.x, heightMapRelative.minMax.y, baseValue);

        float riseUp = math.lerp(heightMapRelative.minValue, hmWeight, baseWeight);
        float mask = math.lerp(element.Value, element.Value + hm, baseWeight);
        float4 colourMask = math.lerp(element.Colour, heightMap[index].Colour, baseWeight);

        if (element.Value < mask)
        {
            float extraWeight = math.clamp(mask - element.Value, 0.0f, 1.0f);
            element.Colour = math.lerp(element.Colour, heightMap[index].Colour, extraWeight);
        }
        element.Value = math.max(element.Value, mask);

        //element.Colour = element.Value == mask ? colourMask:element.Colour;
        result[index] = element;
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
        float baseValue = baseMap[index].Value;
        float hm = heightMap[index].Value;

        float baseWeight = math.unlerp(baseRelative.minMax.x, baseRelative.minMax.y, baseValue);
        float hmWeight = math.unlerp(heightMapRelative.minMax.x, heightMapRelative.minMax.y, baseValue);

        float riseUp = math.lerp(heightMapRelative.minValue, hmWeight, baseWeight);
        float mask = math.lerp(result[index].Value, hm + riseUp, hmWeight * baseWeight * riseUp);

        // result[index] +=  hm * baseValue;
        HeightMapElement element = result[index];
        element.Value = mask;
        result[index] = element;
        // result[index] = math.lerp(result[index], hm* mask, hmWeight);
    }
}