using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

public class GradientExample : MonoBehaviour
{
    [SerializeField] Gradient exampleGradient;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] int textureWidth = 300;
    [SerializeField] int textureHeight = 50;

    [SerializeField] private float time;
    [SerializeField] private float scrollSpeed;
    private Texture2D texture; // cached texture

    private void UpdateGradientSlider()
    {
        // if the texture is null create a new one
        if(texture == null)
        {
            texture = new Texture2D(textureWidth, textureHeight);
        }
        // if the width or height don't match reinitialize the texture with new dimentions
        else if(texture.width != textureWidth || texture.height != textureHeight)
        {
            texture.Reinitialize(textureWidth, textureHeight);
        }

        // create a single dimentional array of length textureWidth
        Color32[] colours = new Color32[textureWidth];

        for (int i = 0; i < colours.Length; i++)
        {
            // converting to Color32 is expensive, so do it once for 1 row.
            colours[i] = exampleGradient.Evaluate((Mathf.InverseLerp(0, textureWidth, i) + time) % 1);
        }

        // create an single dimentional array for all the pixels
        Color32[] pixels = new Color32[textureWidth * textureHeight];

        for (int i = 0; i < pixels.Length; i++)
        {
            // calculate x coordinate from xy pixel coordinates,
            // if we needed y as wel this would be int y = i / textureWidth
            int x = i % textureWidth;
            // set the pixel colour to the corrisponding column in the colours array
            // (pretend colours is  2d array of textureWidth * 1)
            pixels[i] = colours[x];
        }

        // writing to the texture as color32s is faster than colors as they must be
        // converted from 0-1 floats to 0-255 byte values with some maths - hence
        // why I do this this once for one row then copy it to the whole texture
        texture.SetPixels32(pixels, 0);

        // upload texture to GPU frame buffer
        texture.Apply();

        // create and set sprite
        Sprite sprite = Sprite.Create(texture,new Rect(0, 0, textureWidth, textureHeight), new Vector2(0.5f, 0.5f));
        spriteRenderer.sprite = sprite;
    }

    private void Update()
    {
        time += scrollSpeed*Time.deltaTime;
        UpdateGradientSlider();
    }
}
