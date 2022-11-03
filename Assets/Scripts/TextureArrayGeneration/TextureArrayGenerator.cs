using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public static class TextureArrayGenerator
{
    public static Texture2DArray BasicBundlerNoMipMaps(Texture2D floor, Texture2D[] terrainTextures)
    {
        Texture2DArray textureArray = new(floor.width, floor.height, terrainTextures.Length + 1, floor.format, false)
        {
            anisoLevel = floor.anisoLevel,
            filterMode = floor.filterMode,
            wrapMode = floor.wrapMode
        };

        Graphics.CopyTexture(floor, 0, 0, textureArray, 0, 0);

        for (int i = 0; i < terrainTextures.Length; i++)
        {
            Graphics.CopyTexture(terrainTextures[i], 0, 0, textureArray, i + 1, 0);
        }

        return textureArray;
    }

    public static int HashTextures(Texture2D floor, Texture2D[] terrainTextures)
    {
        int hash = floor.GetHashCode();
        for (int i = 0; i < terrainTextures.Length; i++)
        {
            hash += terrainTextures[i].GetHashCode();
        }
        return hash;
    }

    public static Texture2DArray BasicBundler(Texture2D floor, Texture2D[] terrainTextures)
    {
        Texture2DArray textureArray = new(floor.width, floor.height, terrainTextures.Length + 1, floor.format, floor.mipmapCount > 1)
        {
            anisoLevel = floor.anisoLevel,
            filterMode = floor.filterMode,
            wrapMode = floor.wrapMode
        };

        for (int m = 0; m < floor.mipmapCount; m++)
        {
            Graphics.CopyTexture(floor, 0, m, textureArray, 0, m);
        }

        for (int i = 0; i < terrainTextures.Length; i++)
        {
            for (int m = 0; m < floor.mipmapCount; m++)
            {
                Graphics.CopyTexture(terrainTextures[i], 0, m, textureArray, i + 1, m);
            }
        }

        return textureArray;
    }

    public static Texture2DArray AdvancedBundler(Texture2D floor, Texture2D[] terrainTextures)
    {
        Texture2DArray textureArray = new(floor.width, floor.height, terrainTextures.Length + 1, floor.format, false)
        {
            anisoLevel = floor.anisoLevel,
            filterMode = floor.filterMode,
            wrapMode = floor.wrapMode
        };


        CopyTextureJobs(floor.GetPixelData<Color32>(0), textureArray.GetPixelData<Color32>(0, 0));
        for (int i = 0; i < terrainTextures.Length; i++)
        {
            CopyTextureJobs(terrainTextures[i].GetPixelData<Color32>(0), textureArray.GetPixelData<Color32>(0, i + 1));
        }
        textureArray.Apply();
        return textureArray;
    }

    public static void CopyTextureJobs(NativeArray<Color32> source, NativeArray<Color32> destination)
    {
        var textureFiller = new FillTextureColor32
        {
            source =source,
            destination = destination
        };

        textureFiller.Schedule(source.Length, 64).Complete();
    }
}
