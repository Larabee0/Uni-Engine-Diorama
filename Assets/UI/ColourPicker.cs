using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public delegate void ColourPickerApply(Color result);
/// <summary>
/// This is script is part of a colour picker UI system I have developed and used in serval projects.
/// Copyright (c) 2022 William Vickers Hastings
/// </summary>
public class ColourPicker : MonoBehaviour
{
    public static ColourPicker Instance;
    public bool IsOpen = false;
    public ColourPickerApply OnColourPickerApply;
    public ColourPickerApply OnColourPickerClose;
    [SerializeField] private Texture2D colourGraident;
    [SerializeField] private Texture2D SatSliderTexutre;
    [SerializeField] private Texture2D ValueSliderTexutre;
    [SerializeField] private UIDocument document;

    private VisualElement PickerPoint;
    private VisualElement OldColourPreview;
    private VisualElement NewColourPreview;
    private VisualElement ColourGraident;

    private Button SmallCloseButton;
    private Button CloseButton;
    private Button ApplyButton;

    private SliderInt HueSlider;
    private SliderInt SatSlider;
    private SliderInt ValueSlider;
    private SliderInt AlphaSlider;

    private TextField RedValue;
    private TextField GreenValue;
    private TextField BlueValue;
    private TextField AlphaValue;

    private TextField HexField;

    public bool MouseOverGradient = false;
    public bool WentDownoverGradient = false;
    public Color SetColour;
    private Color32[] SatPixelColours;
    private Color32[] ValuePixelColours;
    private void Awake()
    {
        SatSliderTexutre = new Texture2D(147, 18);
        ValueSliderTexutre = new Texture2D(147, 18);

        SatPixelColours = new Color32[147 * 18];
        ValuePixelColours = new Color32[147 * 18];
        QueryDocument();
        SetSatTexture();
        SetValueTexture();
        Close();
        Instance = this;
    }

    private void QueryDocument()
    {
        VisualElement root = document.rootVisualElement;
        PickerPoint = root.Q("PickerPoint");
        OldColourPreview = root.Q("OldColourPreview");
        NewColourPreview = root.Q("NewColourPreview");
        ColourGraident = root.Q("ColourGradient");

        SmallCloseButton = root.Q<Button>("SmallCloseButton");
        CloseButton = root.Q<Button>("CloseButton");
        ApplyButton = root.Q<Button>("ApplyButton");

        HueSlider = root.Q<SliderInt>("HueSlider");
        SatSlider = root.Q<SliderInt>("SaturationSlider");
        ValueSlider = root.Q<SliderInt>("ValueSlider");
        AlphaSlider = root.Q<SliderInt>("AlphaSlider");

        RedValue = root.Q<TextField>("RedValue");
        GreenValue = root.Q<TextField>("GreenValue");
        BlueValue = root.Q<TextField>("BlueValue");
        AlphaValue = root.Q<TextField>("AlphaValue");

        HexField = root.Q<TextField>("HexField");

        SmallCloseButton.RegisterCallback<ClickEvent>(ev => Close());
        CloseButton.RegisterCallback<ClickEvent>(ev => Close());
        ApplyButton.RegisterCallback<ClickEvent>(ev => Apply());

        ColourGraident.RegisterCallback<MouseOverEvent>(ev => MouseOverGradient = true);
        ColourGraident.RegisterCallback<MouseOutEvent>(ev => MouseOverGradient = false);

        HueSlider.RegisterValueChangedCallback(ev => ColourSliderActivity());
        SatSlider.RegisterValueChangedCallback(ev => ColourSliderActivity());
        ValueSlider.RegisterValueChangedCallback(ev => ColourSliderActivity());
        AlphaSlider.RegisterValueChangedCallback(ev => ColourSliderActivity());

        StyleBackground styleBackground = SatSlider.style.backgroundImage;
        Background background = SatSlider.style.backgroundImage.value;
        background.texture = SatSliderTexutre;
        styleBackground.value = background;
        SatSlider.style.backgroundImage = styleBackground;

        styleBackground = ValueSlider.style.backgroundImage;
        background = ValueSlider.style.backgroundImage.value;
        background.texture = ValueSliderTexutre;
        styleBackground.value = background;
        ValueSlider.style.backgroundImage = styleBackground;
    }

    public void Close()
    {
        document.rootVisualElement.style.display = DisplayStyle.None;
        IsOpen = false;
        OnColourPickerClose?.Invoke(SetColour);
    }

    public void Open(Color start)
    {
        document.rootVisualElement.style.display = DisplayStyle.Flex;
        IsOpen = true;
        //QueryDocument();
        StartCoroutine(SetPickerPosDelayed(start));
    }

    public void Apply()
    {
        OnColourPickerApply?.Invoke(SetColour);
        Close();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            WentDownoverGradient = false;
            if (MouseOverGradient)
            {
                WentDownoverGradient = true;
            }
        }
        if ((MouseOverGradient || WentDownoverGradient) && Input.GetMouseButton(0))
        {
            Vector2 mousePos = Input.mousePosition;
            float pickerHalfSize = PickerPoint.worldBound.height / 2;
            mousePos.y -= (ColourGraident.worldBound.height / 2) - pickerHalfSize * 3;
            Vector2 localPos = ColourGraident.WorldToLocal(math.clamp(mousePos, ColourGraident.worldBound.min, ColourGraident.worldBound.max));
            float u = Mathf.InverseLerp(0, ColourGraident.worldBound.width, localPos.x);
            float v = Mathf.InverseLerp(0, ColourGraident.worldBound.height, localPos.y);
            SetPickerPos(u, v);
        }
    }

    private IEnumerator SetPickerPosDelayed(Color start)
    {
        yield return null;

        OldColourPreview.style.backgroundColor = start;

        Color.RGBToHSV(start, out float H, out float S, out float V);

        HueSlider.value = (int)Mathf.Lerp(0, 360, H);
        SatSlider.value = (int)Mathf.Lerp(0, 100, S);
        ValueSlider.value = (int)Mathf.Lerp(0, 100, V);
        AlphaSlider.value = ((Color32)start).a;
    }

    public void ColourSliderActivity()
    {
        RedValue.value = HueSlider.value.ToString();
        GreenValue.value = SatSlider.value.ToString();
        BlueValue.value = ValueSlider.value.ToString();
        AlphaValue.value = AlphaSlider.value.ToString();
        SetPickerPos(Mathf.InverseLerp(0, 360, HueSlider.value), Mathf.InverseLerp(0, 100, SatSlider.value));
        GenerateColour();
    }

    public void SetPickerPos(float u, float v)
    {
        float pickerHalfSize = PickerPoint.worldBound.height / 2;
        PickerPoint.style.top = Mathf.Lerp(ColourGraident.worldBound.height - pickerHalfSize, -pickerHalfSize, v);
        PickerPoint.style.left = Mathf.Lerp(-pickerHalfSize, ColourGraident.worldBound.width - pickerHalfSize, u);
        HueSlider.value = (int)Mathf.Lerp(0, 360, u);
        SatSlider.value = (int)Mathf.Lerp(0, 100, v);
    }

    public void GenerateColour()
    {
        SetColour = Color.HSVToRGB(Mathf.InverseLerp(0, 360, HueSlider.value), Mathf.InverseLerp(0, 100, SatSlider.value), Mathf.InverseLerp(0, 100, ValueSlider.value));
        SetColour.a = Mathf.InverseLerp(0, 255, AlphaSlider.value);
        NewColourPreview.style.backgroundColor = SetColour;
        HexField.value = ColorUtility.ToHtmlStringRGBA(SetColour);
        SetSatTexture();
        SetValueTexture();
    }

    public void SetSatTexture()
    {
        Color SatSim = Color.HSVToRGB(Mathf.InverseLerp(0, 360, HueSlider.value), 1, 1);
        float darkness = Mathf.InverseLerp(0, 100, ValueSlider.value);
        for (int x = 0; x < 147; x++)
        {
            float t = Mathf.InverseLerp(0, 147, x);
            Color SatColor = Color.Lerp(Color.white, SatSim, t);
            SatColor.r *= darkness;
            SatColor.g *= darkness;
            SatColor.b *= darkness;
            for (int y = 0; y < 18; y++)
            {
                SatPixelColours[x + (y * 147)] = SatColor;
            }
        }

        SatSliderTexutre.SetPixels32(SatPixelColours);
        SatSliderTexutre.Apply();
    }

    public void SetValueTexture()
    {
        Color colour = Color.HSVToRGB(Mathf.InverseLerp(0, 360, HueSlider.value), Mathf.InverseLerp(0, 100, SatSlider.value), 1);

        for (int x = 0; x < 147; x++)
        {
            float t = Mathf.InverseLerp(0, 147, x);
            Color SatColor = Color.Lerp(Color.black, colour, t);
            for (int y = 0; y < 18; y++)
            {
                ValuePixelColours[x + (y * 147)] = SatColor;
            }
        }

        ValueSliderTexutre.SetPixels32(ValuePixelColours);
        ValueSliderTexutre.Apply();
    }
}

public class ColourDisplay
{
    private readonly VisualElement MainColour;
    private readonly VisualElement WhiteAlpha;
    private readonly VisualElement BlackAlpha;

    private Color colour;
    public Color Colour
    {
        get => colour;
        set
        {
            colour = value;
            Color mainColour = value;
            mainColour.a = 1f;
            float alphaPercent = colour.a * 100;
            MainColour.style.backgroundColor = mainColour;

            WhiteAlpha.style.width = new Length(alphaPercent, LengthUnit.Percent);
            BlackAlpha.style.width = new Length(100 - alphaPercent, LengthUnit.Percent);
        }
    }

    public ColourDisplay(VisualElement root)
    {
        MainColour = root[0];
        VisualElement alphaContainer = root[1];
        WhiteAlpha = alphaContainer[0];
        BlackAlpha = alphaContainer[1];
        root.RegisterCallback<ClickEvent>(ev => OnClickCallback());
    }

    public void OnClickCallback()
    {
        ColourPicker.Instance.OnColourPickerClose += OnClose;
        ColourPicker.Instance.OnColourPickerApply += OnApply;
        ColourPicker.Instance.Open(colour);
        Debug.Log("Colour Picker open request");
    }

    private void OnClose(Color colour)
    {
        ColourPicker.Instance.OnColourPickerClose -= OnClose;
        ColourPicker.Instance.OnColourPickerApply -= OnApply;
    }

    private void OnApply(Color colour)
    {
        Colour = colour;
    }
}