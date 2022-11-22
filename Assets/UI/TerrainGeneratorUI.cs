using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UIElements;
using static TerrainGeneratorUI;
using static UnityEngine.EventSystems.EventTrigger;

public class TerrainGeneratorUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    private VisualElement RootVisualElement => uiDocument.rootVisualElement;

    private Foldout globalSettingsFoldout;
    private BasicSettings basicSettings;
    private ErosionSettings erosionSettings;


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
        globalSettingsFoldout = RootVisualElement.Q<Foldout>("GlobalSettingsFoldout");
        basicSettings = new(RootVisualElement.Q("BasicSettings"));
        bool cacheUpdateOnChange = basicSettings.UpdateOnChange;
        basicSettings.updateOnSettingChange.value = basicSettings.UpdateOnChange ? false : true;
        basicSettings.mapWidth.RegisterValueChangedCallback(ev => UpdateDimentions());
        basicSettings.mapHeight.RegisterValueChangedCallback(ev => UpdateDimentions());
        basicSettings.texScaleX.RegisterValueChangedCallback(ev => UpdateTexScale());
        basicSettings.texScaleY.RegisterValueChangedCallback(ev => UpdateTexScale());
        basicSettings.updateOnSettingChange.RegisterValueChangedCallback(ev => UpdateOnChange());
        basicSettings.forceUpdate.RegisterCallback<ClickEvent>(ev => GatherDataAndGenerate());

        erosionSettings = new(this, RootVisualElement.Q<Foldout>("ErosionFoldout"));

        addLayerButton = RootVisualElement.Q<Button>("addLayerButton");
        layersScrollView = RootVisualElement.Q<ScrollView>("LayerScrollView");

        layersScrollView.Clear();

        AddLayer();

        NoiseSettings[] baseSettings = terrainInferface.NoiseLayers;
        for (int i = 1; i < baseSettings.Length; i++)
        {
            AddLayer();
            layerSettings[^1].Setup(terrainInferface.NoiseLayers[i]);
        }

        erosionSettings.Setup(terrainInferface.FirstNoiseLayer.erosionSettings);

        addLayerButton.RegisterCallback<ClickEvent>(ev=> AddLayer());
        basicSettings.updateOnSettingChange.SetValueWithoutNotify(cacheUpdateOnChange);
        globalSettingsFoldout.value = false;
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
            GatherDataAndGenerate();
        }
    }

    private void GatherDataAndGenerate()
    {
        NoiseSettings[] noiseSettings = new NoiseSettings[layerSettings.Count];

        for (int i = 0; i < layerSettings.Count; i++)
        {
            noiseSettings[i] = layerSettings[i].UpdateTemplate();
        }

        noiseSettings[0].erosionSettings = erosionSettings.GetSettings();

        terrainInferface.Generate(noiseSettings);
    }

    private void AddLayer()
    {
        TemplateContainer container = layerFoldoutPrefab.Instantiate();
        layersScrollView.Add(container);
        LayerSettings newSettings = new(this, container);
        layerSettings.Add(newSettings);
        newSettings.LayerFoldoutName = string.Format("Layer {0}", layerSettings.Count);
        newSettings.Setup(terrainInferface.FirstNoiseLayer);
    }

    private void DuplicateLayer(LayerSettings layer)
    {
        AddLayer();
        layerSettings[^1].Setup(layer.UpdateTemplate());
    }

    private void RemoveLayer(LayerSettings layer)
    {
        if(layerSettings.Count > 2)
        {
            layerSettings.Remove(layer);
            layersScrollView.Remove(layer.templateContainer);
        }
    }

    private void MoveLayer(LayerSettings layer, int dir)
    {
        int startIndex = layerSettings.IndexOf(layer);
        if ((startIndex == 0 && dir < 0) || (startIndex == layerSettings.Count - 1 && dir > 0))
        {
            return;
        }

        if (dir > 0)
        {
            layerSettings.RemoveAt(startIndex);
            layerSettings.Insert(startIndex + 1, layer);
        }
        else
        {
            layerSettings.Insert(startIndex - 1, layer);
            layerSettings.RemoveAt(startIndex+1);
        }
        layersScrollView.Clear();

        layerSettings.ForEach(layer => layersScrollView.Add(layer.templateContainer));
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

    public class ErosionSettings
    {
        private readonly TerrainGeneratorUI terrainGeneratorUI;
        public Foldout root;
        private ErodeSettings erodeSettings;
        #region Srosion Setting
        private readonly Toggle erosionToggle;
        public bool EnableErosion { get => erosionToggle.value; set => erosionToggle.value = value; }

        private readonly TextField baseSeed;
        private readonly TextField erosionIterations;

        private readonly SliderInt brushRadius;
        public int BrushRadius { get => brushRadius.value; set => brushRadius.value = value; }

        private readonly Slider interia;
        public float Interia { get => interia.value; set => interia.value = value; }

        private readonly TextField sedimentCapacityFactor;

        private readonly Slider minSedimentCapacity;
        public float MinSedimentCapacity { get => minSedimentCapacity.value; set => minSedimentCapacity.value = value; }

        private readonly Slider erodeSpeed;
        public float ErodeSpeed { get => erodeSpeed.value; set => erodeSpeed.value = value; }

        private readonly Slider depositSpeed;
        public float DepositSpeed { get => depositSpeed.value; set => depositSpeed.value = value; }

        private readonly Slider evaporateSpeed;
        public float EvaporateSpeed { get => evaporateSpeed.value; set => evaporateSpeed.value = value; }

        private readonly Slider gravity;
        public float Gravity { get => gravity.value; set => gravity.value = value; }

        private readonly SliderInt maxLifetime;
        public int MaxLifetime { get => maxLifetime.value; set => maxLifetime.value = value; }

        private readonly Slider startWater;
        public float StartWater { get => startWater.value; set => startWater.value = value; }

        private readonly Slider startSpeed;
        public float StartSpeed { get => startSpeed.value; set => startSpeed.value = value; }
        #endregion

        public ErosionSettings(TerrainGeneratorUI terrainGeneratorUI, Foldout foldout)
        {
            this.terrainGeneratorUI = terrainGeneratorUI;
            root = foldout;

            erosionToggle = root.Q<Toggle>("erodeToggle");
            baseSeed = root.Q<TextField>("baseSeed");
            erosionIterations = root.Q<TextField>("erosionIterations");
            brushRadius = root.Q<SliderInt>("brushRadius");
            interia = root.Q<Slider>("inertia");
            sedimentCapacityFactor = root.Q<TextField>("sedimentCapacityFactor");
            minSedimentCapacity = root.Q<Slider>("minSedimentCapacity");
            erodeSpeed = root.Q<Slider>("erodeSpeed");
            depositSpeed = root.Q<Slider>("depositSpeed");
            evaporateSpeed = root.Q<Slider>("evaporateSpeed");
            gravity = root.Q<Slider>("gravity");
            maxLifetime = root.Q<SliderInt>("maxLifetime");
            startWater = root.Q<Slider>("startWater");
            startSpeed = root.Q<Slider>("startSpeed");

            erosionToggle.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            brushRadius.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            interia.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            sedimentCapacityFactor.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            minSedimentCapacity.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            erodeSpeed.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            depositSpeed.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            evaporateSpeed.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            gravity.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            maxLifetime.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            startWater.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            startSpeed.RegisterValueChangedCallback(ev => OnGeneralValueChanged());

            baseSeed.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));
            erosionIterations.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));
            sedimentCapacityFactor.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));

            baseSeed.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());
            erosionIterations.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());
            sedimentCapacityFactor.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());

            foldout.value = false;
        }

        public void Setup(ErodeSettings settings)
        {
            erodeSettings = settings;
            EnableErosion = settings.erosion;
            baseSeed.value = settings.baseSeed.ToString();
            erosionIterations.value = settings.erosionIterations.ToString();
            BrushRadius = settings.erosionBrushRadius;
            Interia = settings.inertia;
            sedimentCapacityFactor.value = settings.sedimentCapacityFactor.ToString();
            MinSedimentCapacity = settings.minSedimentCapacity;
            ErodeSpeed = settings.erodeSpeed;
            DepositSpeed = settings.depositSpeed;
            EvaporateSpeed = settings.evaporateSpeed;
            Gravity = settings.gravity;
            MaxLifetime = settings.maxLifetime;
            StartWater = settings.startWater;
            StartSpeed = settings.startSpeed;
        }

        public ErodeSettings GetSettings()
        {

            erodeSettings.erosion = EnableErosion;
            erodeSettings.baseSeed = uint.Parse(baseSeed.value);
            erodeSettings.erosionIterations = Mathf.Clamp(int.Parse(erosionIterations.value), 0, 1000000);
            erodeSettings.erosionBrushRadius = BrushRadius;
            erodeSettings.inertia = Interia;
            erodeSettings.sedimentCapacityFactor = float.Parse( sedimentCapacityFactor.value);
            erodeSettings.minSedimentCapacity = MinSedimentCapacity;
            erodeSettings.erodeSpeed = ErodeSpeed;
            erodeSettings.depositSpeed = DepositSpeed;
            erodeSettings.evaporateSpeed = EvaporateSpeed;
            erodeSettings.gravity = Gravity;
            erodeSettings.maxLifetime = MaxLifetime;
            erodeSettings.startWater = StartWater;
            erodeSettings.startSpeed = StartSpeed;
            return erodeSettings;

        }

        private void OnNumberTextFieldChanged(string newValue, string oldValue)
        {
            if (newValue == oldValue)
            {
                return;
            }

            if (IsUInt(baseSeed, out uint value1))
            {
                if(value1 == 0)
                {
                    value1 = 1;
                }
                baseSeed.value = value1.ToString();
                erodeSettings.baseSeed = value1;
            }
            if (IsInt(erosionIterations, out int value2))
            {
                value2 = Mathf.Clamp(value2, 0, 1000000);
                erosionIterations.value = value2.ToString();
                erodeSettings.erosionIterations = value2;
            }
            if (IsFloat(sedimentCapacityFactor, out float value3))
            {
                value3 = Mathf.Clamp(value3, 0, 50);
                sedimentCapacityFactor.value = value3.ToString();
                erodeSettings.sedimentCapacityFactor = value3;
            }
        }

        private void OnNumberTexFieldFocusLoss()
        {
            if (!IsUInt(baseSeed, out uint _))
            {
                baseSeed.value = erodeSettings.baseSeed.ToString();
            }
            if (!IsInt(erosionIterations, out int _))
            {
                erosionIterations.value = erodeSettings.erosionIterations.ToString();
            }
            if(!IsFloat(sedimentCapacityFactor, out float _))
            {
                sedimentCapacityFactor.value = erodeSettings.sedimentCapacityFactor.ToString();
            }

            OnGeneralValueChanged();
        }

        private void OnGeneralValueChanged()
        {
            terrainGeneratorUI.UpdateOnChange();
        }
    }

    public class LayerSettings
    {
        private readonly TerrainGeneratorUI terrainGeneratorUI;

        private MeshArea MeshArea => terrainGeneratorUI.terrainInferface;
        public TemplateContainer templateContainer;
        private readonly Foldout layerFoldout;
        public bool LayerFoldoutExpanded { get => layerFoldout.value; set => layerFoldout.value = value; }
        public string LayerFoldoutName { set=> layerFoldout.text= value; }

        private readonly Button deleteLayerButton;
        private readonly Button duplicateLayerButton;

        private readonly Button moveUpLayerButton;
        private readonly Button moveDownLayerButton;

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

        public LayerSettings(TerrainGeneratorUI terrainGeneratorUI, TemplateContainer container)
        {
            templateContainer = container;
            layerFoldout = container.Q<Foldout>("LayerFoldout");
            this.terrainGeneratorUI = terrainGeneratorUI;

            noiseSettingsFoldout = container.Q<Foldout>("NoiseFoldout");
            layerDropdown = container.Q<DropdownField>("LayerTypeDropDown");

            deleteLayerButton = container.Q<Button>("deleteLayerButton");
            duplicateLayerButton = container.Q<Button>("duplicateLayerButton");

            moveUpLayerButton = container.Q<Button>("moveUpLayerButton");
            moveDownLayerButton = container.Q<Button>("moveDownLayerButton");

            flatternToFloor = container.Q<Toggle>("clampLayerToFloor");
            floorPercentage = container.Q<Slider>("floorPercentageSlider");
            resolution = container.Q<SliderInt>("resolutionSlider");
            noiseStrength = container.Q<Slider>("noiseStrengthSlider");
            layerPasses = container.Q<SliderInt>("layerPassesSlider");
            baseRoughness = container.Q<Slider>("baseRoughnessSlider");
            extraRoughness = container.Q<Slider>("extraRoughnessSlider");
            persistence = container.Q<Slider>("persistenceSlider");
            centerX = container.Q<TextField>("centerOffsetX");
            centerY = container.Q<TextField>("centerOffsetY");
            globalHeightOffset = container.Q<TextField>("globalHeightOffset");
            rigidNoiseWeightMultiplier = container.Q<TextField>("rigidWeightMultiplier");

            shaderFoldout = container.Q<Foldout>("ShaderFoldout");

            // mainColour = layerFoldout.Q("");
            // flatColour = layerFoldout.Q("");
            // rimColour = layerFoldout.Q("");

            patternFoldout = container.Q<Foldout>("PatternFoldout");
            patternIndex = container.Q<SliderInt>("patternIndexSlider");
            patternPreview = container.Q("patternPreview");

            slopeThreshold = container.Q<Slider>("slopeThresholdSlider");
            blendAmmount = container.Q<Slider>("blendAmmountSlider");
            flatMaxHeight = container.Q<TextField>("flatMaxHeight");
            heightFade = container.Q<TextField>("heightFade");
            rimPower = container.Q<TextField>("rimPower");
            rimFraction = container.Q<Slider>("rimFractionSlider");
            absMaxHeight = container.Q<TextField>("absMaxHeight");

            OnLayerTypeChanged();
            OnPatternIndexChanged();

            layerDropdown.RegisterValueChangedCallback(ev => OnLayerTypeChanged());
            patternIndex.RegisterValueChangedCallback(ev => OnPatternIndexChanged());
            deleteLayerButton.RegisterCallback<ClickEvent>(ev => DeleteLayerButton());
            duplicateLayerButton.RegisterCallback<ClickEvent>(ev => DuplicateLayerButton());

            moveUpLayerButton.RegisterCallback<ClickEvent>(ev => MoveLayerOrder(-1));
            moveDownLayerButton.RegisterCallback<ClickEvent>(ev => MoveLayerOrder(1));

            centerX.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));
            centerY.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));
            globalHeightOffset.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));
            rigidNoiseWeightMultiplier.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));

            flatMaxHeight.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));
            heightFade.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));
            rimPower.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));
            absMaxHeight.RegisterValueChangedCallback(ev => OnNumberTextFieldChanged(ev.newValue, ev.previousValue));

            centerX.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());
            centerY.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());
            globalHeightOffset.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());
            rigidNoiseWeightMultiplier.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());

            flatMaxHeight.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());
            heightFade.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());
            rimPower.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());
            absMaxHeight.RegisterCallback<FocusOutEvent>(ev => OnNumberTexFieldFocusLoss());



            flatternToFloor.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            floorPercentage.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            resolution.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            noiseStrength.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            layerPasses.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            baseRoughness.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            extraRoughness.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            persistence.RegisterValueChangedCallback(ev => OnGeneralValueChanged());

            patternIndex.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            slopeThreshold.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            blendAmmount.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
            rimFraction.RegisterValueChangedCallback(ev => OnGeneralValueChanged());
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
            centerY.value = settings.basicSettings.centre.y.ToString();
            globalHeightOffset.value = settings.basicSettings.offsetValue.ToString();
            rigidNoiseWeightMultiplier.value = settings.weightMultiplier.ToString();

            PatternIndex = settings.basicSettings.abvcSettings.mainTextureIndex;
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
            template.layerType = LayerType;
            template.basicSettings.clampToFloor = FlatternToFloor;
            template.basicSettings.floorPercentage = FloorPercentage;
            template.basicSettings.resolution = Resolution;
            template.basicSettings.strength = NoiseStrength;
            template.basicSettings.numLayers = LayerPasses;
            template.basicSettings.baseRoughness= BaseRoughness;
            template.basicSettings.roughness = ExtraRoughness;
            template.basicSettings.persistence= Persistence;

            template.basicSettings.abvcSettings.mainTextureIndex = PatternIndex;
            template.basicSettings.abvcSettings.slopeThreshold= SlopeThreshold;
            template.basicSettings.abvcSettings.blendAmount = BlendAmmount;

            template.basicSettings.abvcSettings.rimFacraction = RimFraction;

            return template;
        }

        private void DuplicateLayerButton()
        {
            terrainGeneratorUI.DuplicateLayer(this);
        }

        private void DeleteLayerButton()
        {
            terrainGeneratorUI.RemoveLayer(this);
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
            PatternPreview = MeshArea.TerrainTextures[PatternIndex-1];
        }

        private void MoveLayerOrder(int dir)
        {
            terrainGeneratorUI.MoveLayer(this, dir);
        }

        private void OnNumberTextFieldChanged(string newValue, string oldValue)
        {
            if (newValue == oldValue)
            {
                return;
            }

            if(IsFloat(centerX, out float value1))
            {
                centerX.value = value1.ToString();
                template.basicSettings.centre.x = value1;
            }
            if(IsFloat(centerY, out float value2))
            {
                centerY.value = value2.ToString();
                template.basicSettings.centre.y = value2;
            }
            if (IsFloat(globalHeightOffset, out float value3))
            {
                globalHeightOffset.value = value3.ToString();
                template.basicSettings.offsetValue = value3;
            }
            if (IsFloat(rigidNoiseWeightMultiplier, out float value4))
            {
                rigidNoiseWeightMultiplier.value = value4.ToString();
                template.weightMultiplier = value4;
            }


            if (IsFloat(flatMaxHeight, out float value5))
            {
                flatMaxHeight.value = value5.ToString();
                template.basicSettings.abvcSettings.flatMaxHeight = value5;
            }
            if (IsFloat(heightFade, out float value6))
            {
                heightFade.value = value6.ToString();
                template.basicSettings.abvcSettings.heightFade = value6;
            }
            if (IsFloat(rimPower, out float value7))
            {
                rimPower.value = value7.ToString();
                template.basicSettings.abvcSettings.rimPower = value7;
            }
            if (IsFloat(absMaxHeight, out float value8))
            {
                absMaxHeight.value = value8.ToString();
                template.basicSettings.abvcSettings.absolutelMaxHeight = value8;
            }
        }

        private void OnNumberTexFieldFocusLoss()
        {
            if (!IsInt(centerX, out int _))
            {
                centerX.value = template.basicSettings.centre.x.ToString();
            }
            if (!IsInt(centerY, out int _))
            {
                centerY.value = template.basicSettings.centre.y.ToString();
            }
            if (!IsFloat(globalHeightOffset, out float _))
            {
                globalHeightOffset.value = template.basicSettings.offsetValue.ToString();
            }
            if (!IsFloat(rigidNoiseWeightMultiplier, out float _))
            {
                rigidNoiseWeightMultiplier.value = template.weightMultiplier.ToString();
            }


            if (!IsFloat(flatMaxHeight, out float _))
            {
                flatMaxHeight.value = template.basicSettings.abvcSettings.flatMaxHeight.ToString();
            }
            if (!IsFloat(heightFade, out float _))
            {
                heightFade.value = template.basicSettings.abvcSettings.heightFade.ToString();
            }
            if (!IsFloat(rimPower, out float _))
            {
                rimPower.value = template.basicSettings.abvcSettings.rimPower.ToString();
            }
            if (!IsFloat(absMaxHeight, out float _))
            {
                absMaxHeight.value = template.basicSettings.abvcSettings.absolutelMaxHeight.ToString();
            }

            OnGeneralValueChanged();
        }

        private void OnGeneralValueChanged()
        {
            terrainGeneratorUI.UpdateOnChange();
        }
    }

    public static bool IsInt(TextField field, out int value)
    {
        if (int.TryParse(field.text, out value))
        {
            ColourTextFieldText(field, Color.white);
            return true;
        }
        else
        {
            ColourTextFieldText(field, new Color32(255, 51, 51, 255));
            return false;
        }
    }

    public static bool IsUInt(TextField field, out uint value)
    {
        if (uint.TryParse(field.text, out value))
        {
            ColourTextFieldText(field, Color.white);
            return true;
        }
        else
        {
            ColourTextFieldText(field, new Color32(255, 51, 51, 255));
            return false;
        }
    }

    public static bool IsFloat(TextField field, out float value)
    {
        if (float.TryParse(field.text, out value))
        {
            ColourTextFieldText(field, Color.white);
            return true;
        }
        else
        {
            ColourTextFieldText(field, new Color32(255, 51, 51, 255));
            return false;
        }
    }

    public static void ColourTextFieldText(TextField field, Color colour)
    {
        VisualElement element = field.Q<VisualElement>("unity-text-input");
        StyleColor textColor = element.style.color;
        textColor.value = colour;
        element.style.color = textColor;
        field.style.borderBottomColor = colour == Color.white ? StyleKeyword.Null : colour;
    }
}
