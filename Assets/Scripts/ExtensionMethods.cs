using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class CustomExtensionMethods
{
    public static float3 ToFloat3(this float4 input)
    {
        return new float3(input.x, input.y, input.z);
    }

    public static float4 ToFloat4(this Color input)
    {
        return new float4(input.r, input.g, input.b, input.a);
    }

    public static Color ToColor(this float4 input)
    {
        return new Color(input.x, input.y, input.z, input.w);
    }

    public static float4 ToFloat4(this Color32 input)
    {
        return new float4((int)input.r / 255f, (int)input.g / 255f, (int)input.b / 255f, (int)input.a / 255f);
    }

    public static Color32 ToColor32(this float4 c)
    {
        return new Color32((byte)math.round(math.clamp(c.x, 0, 1) * 255f), (byte)math.round(math.clamp(c.y, 0, 1) * 255f), (byte)math.round(math.clamp(c.z, 0, 1) * 255f), (byte)math.round(math.clamp(c.w,0,1) * 255f));
    }
}
