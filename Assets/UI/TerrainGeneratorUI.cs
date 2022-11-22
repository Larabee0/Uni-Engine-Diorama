using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public class TerrainGeneratorUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    private VisualElement RootVisualElement => uiDocument.rootVisualElement;

    private BasicSettings basicSettings;

    [SerializeField] private MeshArea terrainInferface;

    private Button addLayerButton;
    private ScrollView layersScrollView;

    [SerializeField] private VisualTreeAsset layerFoldoutPrefab;

    private readonly List<LayerSettings> layerSettings = new();

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();

        QueryUIDoc();
    }

    private void QueryUIDoc()
    {
        basicSettings = new(RootVisualElement.Q("BasicSettings"));

        basicSettings.mapWidth.RegisterValueChangedCallback(ev=>UpdateDimentions());
        basicSettings.mapHeight.RegisterValueChangedCallback(ev => UpdateDimentions());
        basicSettings.texScaleX.RegisterValueChangedCallback(ev => UpdateTexScale());
        basicSettings.texScaleY.RegisterValueChangedCallback(ev => UpdateTexScale());
        basicSettings.updateOnSettingChange.RegisterValueChangedCallback(ev => UpdateOnChange());
        basicSettings.forceUpdate.RegisterCallback<ClickEvent>(ev => terrainInferface.Generate());
        layersScrollView = RootVisualElement.Q<ScrollView>("LayerScrollView");

        layersScrollView.Clear();

        TemplateContainer container = layerFoldoutPrefab.Instantiate();
        layersScrollView.Add(container);
        layerSettings.Add(new(this, container.Q<Foldout>("LayerFoldout")));
        layerSettings[0].Setup(terrainInferface.FirstNoiseLayer);

        container = layerFoldoutPrefab.Instantiate();
        layersScrollView.Add(container);
        layerSettings.Add(new(this, container.Q<Foldout>("LayerFoldout")));
        layerSettings[1].Setup(terrainInferface.FirstNoiseLayer);
    }

    private void UpdateDimentions()
    {
        terrainInferface.UpdateDimentions(new(basicSettings.mapWidth.value, basicSettings.mapHeight.value));
        UpdateOnChange();
    }

    private void UpdateTexScale()
    {
        terrainInferface.UpdateTextureTiling(new(basicSettings.texScaleX.value, basicSettings.texScaleY.value));
        UpdateOnChange();
    }

    private void UpdateOnChange()
    {
        if (basicSettings.UpdateOnChange)
        {
            terrainInferface.Generate();
        }
    }

    public class BasicSettings
    {
        public VisualElement root;
        public SliderInt mapWidth;
        public SliderInt mapHeight;

        public SliderInt texScaleX;
        public SliderInt texScaleY;

        public Slider floorFraction;

        public Toggle updateOnSettingChange;
        public bool UpdateOnChange => updateOnSettingChange.value;
        public Button forceUpdate;

        public BasicSettings(VisualElement root)
        {
            this.root = root;
            mapWidth = root.Q<SliderInt>("mapWidthSlider");
            mapHeight = root.Q<SliderInt>("mapHeightSlider");
            texScaleX = root.Q<SliderInt>("texScaleX");
            texScaleY = root.Q<SliderInt>("texScaleY");
            floorFraction = root.Q<Slider>("floorFracSlider");

            updateOnSettingChange = root.Q<Toggle>("updateOnChange");
            forceUpdate = root.Q<Button>("forceUpdate");
        }
    }

    public class LayerSettings
    {

        private readonly TerrainGeneratorUI terrainGeneratorUI;

        private MeshArea MeshArea => terrainGeneratorUI.terrainInferface;

        private readonly Foldout layerFoldout;
        public bool LayerFoldoutExpanded { get => layerFoldout.value; set => layerFoldout.value = value; }

        private readonly Button deleteLayerButton;
        private readonly Button duplicateLayerButton;

        private NoiseSettings template;

        #region NoiseSettings
        private readonly Foldout noiseSettingsFoldout;
        public bool NoiseSettingsFoldoutExpanded { get => noiseSettingsFoldout.value; set => noiseSettingsFoldout.value = value; }

        private readonly DropdownField layerDropdown;
        public LayerType LayerType { get => (LayerType)layerDropdown.index; set => layerDropdown.index = (int)value; }

        private readonly Toggle flatternToFloor;
        public bool FlatternToFloor { get => flatternToFloor.value; set => flatternToFloor.value = value; }

        private readonly Slider floorPercentage;
        public float FloorPercentage { get => floorPercentage.value; set => floorPercentage.value = value; }

        private readonly SliderInt resolution;
        public int Resolution { get => resolution.value; set => resolution.value = value; }

        private readonly Slider noiseStrength;
        public float NoiseStrength { get => noiseStrength.value; set => noiseStrength.value = value; }

        private readonly SliderInt layerPasses;
        public int LayerPasses { get => layerPasses.value; set => layerPasses.value = value; }

        private readonly Slider baseRoughness;
        public float BaseRoughness { get => baseRoughness.value; set => baseRoughness.value = value; }

        private readonly Slider extraRoughness;
        public float ExtraRoughness { get => extraRoughness.value; set => extraRoughness.value = value; }

        private readonly Slider persistence;
        public float Persistence { get => persistence.value; set => persistence.value = value; }

        private readonly TextField centerX;
        private readonly TextField centerY;
        private readonly TextField globalHeightOffset;
        private readonly TextField rigidNoiseWeightMultiplier;
        #endregion

        #region ShaderSettings
        private readonly Foldout shaderFoldout;
        public bool ShaderFoldoutExpanded { get => shaderFoldout.value; set => shaderFoldout.value = value; }

        private readonly VisualElement mainColour;
        private readonly VisualElement flatColour;
        private readonly VisualElement rimColour;

        private readonly Foldout patternFoldout;
        public bool PatternFoldoutExpanded { get => patternFoldout.value; set => patternFoldout.value = value; }

        private readonly SliderInt patternIndex;
        public int PatternIndex { get => patternIndex.value; set => patternIndex.value = value; }
        private readonly VisualElement patternPreview;

        public Texture2D PatternPreview
        {
            set
            {
                patternPreview.style.backgroundImage = value;
            }
        }

        private readonly Slider slopeThreshold;
        public float SlopeThreshold { get => slopeThreshold.value; set => slopeThreshold.value = value; }

        private readonly Slider blendAmmount;
        public float BlendAmmount { get => blendAmmount.value; set => blendAmmount.value = value; }

        private readonly TextField flatMaxHeight;
        private readonly TextField heightFade;
        private readonly TextField rimPower;

        private readonly Slider rimFraction;
        public float RimFraction { get => rimFraction.value; set => rimFraction.value = value; }

        private readonly TextField absMaxHeight;
        #endregion

        public LayerSettings (TerrainGeneratorUI terrainGeneratorUI, Foldout layerFoldout)
        {
            this.layerFoldout = layerFoldout;
            this.terrainGeneratorUI= terrainGeneratorUI;

            noiseSettingsFoldout = layerFoldout.Q<Foldout>("NoiseFoldout");
            layerDropdown = layerFoldout.Q<DropdownField>("LayerTypeDropDown");

            deleteLayerButton = layerFoldout.Q<Button>("deleteLayerButton");
            duplicateLayerButton = layerFoldout.Q<Button>("duplicateLayerButton");

            flatternToFloor = layerFoldout.Q<Toggle>("clampLayerToFloor");
            floorPercentage = layerFoldout.Q<Slider>("floorPercentageSlider");
            resolution = layerFoldout.Q<SliderInt>("resolutionSlider");
            noiseStrength = layerFoldout.Q<Slider>("noiseStrengthSlider");
            layerPasses = layerFoldout.Q<SliderInt>("layerPassesSlider");
            baseRoughness = layerFoldout.Q<Slider>("baseRoughnessSlider");
            extraRoughness = layerFoldout.Q<Slider>("extraRoughnessSlider");
            persistence = layerFoldout.Q<Slider>("persistenceSlider");
            centerX = layerFoldout.Q<TextField>("centerOffsetX");
            centerY = layerFoldout.Q<TextField>("centerOffsetY");
            globalHeightOffset = layerFoldout.Q<TextField>("globalHeightOffset");
            rigidNoiseWeightMultiplier = layerFoldout.Q<TextField>("rigidWeightMultiplier");

            shaderFoldout = layerFoldout.Q<Foldout>("ShaderFoldout");

            // mainColour = layerFoldout.Q("");
            // flatColour = layerFoldout.Q("");
            // rimColour = layerFoldout.Q("");

            patternFoldout = layerFoldout.Q<Foldout>("PatternFoldout");
            patternIndex = layerFoldout.Q<SliderInt>("patternIndexSlider");
            patternPreview = layerFoldout.Q("patternPreview");

            slopeThreshold = layerFoldout.Q<Slider>("slopeThresholdSlider");
            blendAmmount = layerFoldout.Q<Slider>("blendAmmountSlider");
            flatMaxHeight = layerFoldout.Q<TextField>("flatMaxHeight");
            heightFade = layerFoldout.Q<TextField>("heightFade");
            rimPower = layerFoldout.Q<TextField>("rimPower");
            rimFraction = layerFoldout.Q<Slider>("rimFractionSlider");
            absMaxHeight = layerFoldout.Q<TextField>("absMaxHeight");

            OnLayerTypeChanged();
            OnPatternIndexChanged();

            layerDropdown.RegisterValueChangedCallback(ev => OnLayerTypeChanged());
            patternIndex.RegisterValueChangedCallback(ev=>OnPatternIndexChanged());

        }

        public void Setup(NoiseSettings settings)
        {
            template = settings;
            layerDropdown.index = (int)settings.layerType;
            FlatternToFloor = settings.basicSettings.clampToFloor;
            FloorPercentage = settings.basicSettings.floorPercentage;
            Resolution= settings.basicSettings.resolution;
            NoiseStrength = settings.basicSettings.strength;
            LayerPasses = settings.basicSettings.numLayers;
            BaseRoughness = settings.basicSettings.baseRoughness;
            ExtraRoughness = settings.basicSettings.roughness;
            Persistence = settings.basicSettings.persistence;

            centerX.value = settings.basicSettings.centre.x.ToString();
            centerY.value = settings.basicSettings.centre.x.ToString();
            globalHeightOffset.value = settings.basicSettings.offsetValue.ToString();
            rigidNoiseWeightMultiplier.value = settings.weightMultiplier.ToString();

            PatternIndex = settings.basicSettings.abvcSettings.MainTextureIndex;
            SlopeThreshold = settings.basicSettings.abvcSettings.slopeThreshold;
            BlendAmmount = settings.basicSettings.abvcSettings.blendAmount;

            flatMaxHeight.value = settings.basicSettings.abvcSettings.flatMaxHeight.ToString();
            heightFade.value = settings.basicSettings.abvcSettings.heightFade.ToString();
            rimPower.value = settings.basicSettings.abvcSettings.rimPower.ToString();
            RimFraction = settings.basicSettings.abvcSettings.rimFacraction;
            absMaxHeight.value = settings.basicSettings.abvcSettings.absolutelMaxHeight.ToString();

            LayerFoldoutExpanded = false;
            NoiseSettingsFoldoutExpanded = false;
            ShaderFoldoutExpanded = false;
            PatternFoldoutExpanded= false;
            OnLayerTypeChanged();
            OnPatternIndexChanged();

        }

        public NoiseSettings UpdateTemplate()
        {


            return template;
        }


        private void OnLayerTypeChanged()
        {
            if(LayerType == LayerType.Rigid)
            {
                rigidNoiseWeightMultiplier.parent.style.display = DisplayStyle.Flex;
            }
            else
            {
                rigidNoiseWeightMultiplier.parent.style.display = DisplayStyle.None;
            }
        }

        private void OnPatternIndexChanged()
        {
            PatternPreview = MeshArea.TerrainTextures[PatternIndex];
        }
    }
}
