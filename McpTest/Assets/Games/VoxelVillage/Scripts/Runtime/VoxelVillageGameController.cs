#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace McpTest.VoxelVillage
{
    [DisallowMultipleComponent]
    public sealed class VoxelVillageGameController : MonoBehaviour
    {
        const float PlayerMoveSpeed = 8f;
        const float InteractionDistance = 2.15f;
        const float CameraFollowSpeed = 6f;
        const float BubbleHeight = 2.45f;
        const float TownScaleMultiplier = 8f;
        const float BaseTownFootprint = 18f;
        const float TownFootprint = BaseTownFootprint * TownScaleMultiplier;
        const float TownHalfExtent = TownFootprint * 0.5f - 8f;
        const float CameraHeight = 18f;
        const float CameraDistance = 20f;
        const float CameraLookAhead = 13f;
        const float PersonCollisionRadius = 0.7f;
        const float BaseCharacterHeight = 1.8f;
        const float PromptHorizontalPadding = 14f;
        const float PromptVerticalPadding = 9f;
        const float PromptMinWidth = 104f;
        const float PromptMaxWidth = 240f;
        const float PromptMinHeight = 44f;

        enum InteractionTarget
        {
            None,
            Npc,
            Door
        }

        LocalizationDatabase _database = null!;
        LanguageState _languageState = null!;

        Camera _mainCamera = null!;
        Light _mainLight = null!;
        Canvas _canvas = null!;

        GameObject _helpPanel = null!;
        Text _helpText = null!;
        Text _promptText = null!;
        Text _languageButtonText = null!;
        Text _controlsButtonText = null!;
        RectTransform _bubbleRect = null!;
        Text _bubbleSpeakerText = null!;
        Text _bubbleContentText = null!;

        Transform _worldRoot = null!;
        GameObject _player = null!;
        Transform _doorPivot = null!;
        readonly List<VillagerInstance> _villagers = new List<VillagerInstance>();

        InteractionTarget _currentTarget;
        VillagerInstance? _currentVillager;
        VillagerInstance? _activeDialogueVillager;
        bool _dialogueActive;
        int _dialogueLineIndex;
        bool _doorOpen;
        bool _helpVisible;
        float _doorYaw;

        void Awake()
        {
            _database = LocalizationDatabase.LoadFromResources();
            _languageState = new LanguageState(LanguageCode.Ko);
            _languageState.Changed += OnLanguageChanged;

            EnsureScene();
            RefreshLocalizedTexts();
        }

        void OnDestroy()
        {
            _languageState.Changed -= OnLanguageChanged;
        }

        void Update()
        {
            HandleMovement();
            UpdateVillagers();
            UpdateCamera();
            UpdateInteractionTarget();
            HandleInteractionInput();
            UpdateDoorVisual();
            UpdateSpeechBubble();
            RefreshPrompt();
        }

        void EnsureScene()
        {
            EnsureCamera();
            EnsureLighting();
            EnsureEventSystem();
            BuildWorld();
            BuildHud();
        }

        void EnsureCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                _mainCamera = FindAnyObjectByType<Camera>();
            }

            if (_mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                _mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = new Color(0.77f, 0.9f, 1f);
            _mainCamera.fieldOfView = 58f;
            _mainCamera.nearClipPlane = 0.03f;
            _mainCamera.farClipPlane = 400f;
        }

        void EnsureLighting()
        {
            _mainLight = FindAnyObjectByType<Light>();
            if (_mainLight == null || _mainLight.type != LightType.Directional)
            {
                var lightObject = new GameObject("VoxelVillage Light");
                _mainLight = lightObject.AddComponent<Light>();
                _mainLight.type = LightType.Directional;
            }

            _mainLight.intensity = 1.1f;
            _mainLight.color = new Color(1f, 0.96f, 0.9f);
            _mainLight.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.ambientLight = new Color(0.64f, 0.72f, 0.82f);
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        void BuildWorld()
        {
            _worldRoot = new GameObject("VoxelVillageWorld").transform;
            _worldRoot.SetParent(transform, false);

            var grassMaterial = CreateMaterial(new Color(0.49f, 0.74f, 0.46f));
            var roadMaterial = CreateMaterial(new Color(0.77f, 0.67f, 0.5f));
            var plazaMaterial = CreateMaterial(new Color(0.87f, 0.8f, 0.63f));
            var wallWarmMaterial = CreateMaterial(new Color(0.89f, 0.84f, 0.72f));
            var wallCoolMaterial = CreateMaterial(new Color(0.8f, 0.87f, 0.92f));
            var wallPeachMaterial = CreateMaterial(new Color(0.92f, 0.8f, 0.72f));
            var roofRedMaterial = CreateMaterial(new Color(0.73f, 0.31f, 0.23f));
            var roofBlueMaterial = CreateMaterial(new Color(0.23f, 0.36f, 0.61f));
            var roofGreenMaterial = CreateMaterial(new Color(0.24f, 0.48f, 0.27f));
            var woodMaterial = CreateMaterial(new Color(0.56f, 0.35f, 0.2f));
            var trunkMaterial = CreateMaterial(new Color(0.4f, 0.26f, 0.16f));
            var foliageMaterial = CreateMaterial(new Color(0.23f, 0.56f, 0.23f));
            var shrubMaterial = CreateMaterial(new Color(0.29f, 0.6f, 0.3f));
            var flowerYellowMaterial = CreateMaterial(new Color(0.94f, 0.73f, 0.2f));
            var flowerPinkMaterial = CreateMaterial(new Color(0.91f, 0.48f, 0.7f));
            var waterMaterial = CreateMaterial(new Color(0.31f, 0.61f, 0.82f));

            var ground = CreatePrimitive(
                PrimitiveType.Cube,
                "Ground",
                new Vector3(0f, -0.5f, 0f),
                new Vector3(TownFootprint, 1f, TownFootprint),
                grassMaterial);
            ground.transform.SetParent(_worldRoot, false);

            var mainRoad = CreatePrimitive(
                PrimitiveType.Cube,
                "MainRoad",
                new Vector3(0f, -0.44f, 0f),
                new Vector3(12f, 0.12f, TownFootprint * 0.82f),
                roadMaterial);
            mainRoad.transform.SetParent(_worldRoot, false);

            var crossRoad = CreatePrimitive(
                PrimitiveType.Cube,
                "CrossRoad",
                new Vector3(0f, -0.44f, 8f),
                new Vector3(TownFootprint * 0.74f, 0.12f, 10f),
                roadMaterial);
            crossRoad.transform.SetParent(_worldRoot, false);

            var sideRoadWest = CreatePrimitive(
                PrimitiveType.Cube,
                "SideRoadWest",
                new Vector3(-28f, -0.44f, -4f),
                new Vector3(9f, 0.12f, TownFootprint * 0.48f),
                roadMaterial);
            sideRoadWest.transform.SetParent(_worldRoot, false);

            var sideRoadEast = CreatePrimitive(
                PrimitiveType.Cube,
                "SideRoadEast",
                new Vector3(30f, -0.44f, -2f),
                new Vector3(9f, 0.12f, TownFootprint * 0.52f),
                roadMaterial);
            sideRoadEast.transform.SetParent(_worldRoot, false);

            var plaza = CreatePrimitive(
                PrimitiveType.Cube,
                "CentralPlaza",
                new Vector3(0f, -0.41f, 2f),
                new Vector3(22f, 0.16f, 18f),
                plazaMaterial);
            plaza.transform.SetParent(_worldRoot, false);

            var fountainBase = CreatePrimitive(
                PrimitiveType.Cylinder,
                "FountainBase",
                new Vector3(0f, 0.1f, 2f),
                new Vector3(4f, 0.2f, 4f),
                CreateMaterial(new Color(0.68f, 0.72f, 0.77f)));
            fountainBase.transform.SetParent(_worldRoot, false);

            var fountainWater = CreatePrimitive(
                PrimitiveType.Cylinder,
                "FountainWater",
                new Vector3(0f, 0.24f, 2f),
                new Vector3(3.2f, 0.08f, 3.2f),
                waterMaterial);
            fountainWater.transform.SetParent(_worldRoot, false);

            CreateInteractiveHouse(new Vector3(16f, 1.6f, 10f), new Vector3(6.4f, 3.2f, 5.8f), wallWarmMaterial, roofRedMaterial, woodMaterial, roadMaterial);

            CreateDecorativeHouse("NorthHouseA", new Vector3(-18f, 1.55f, 17f), new Vector3(7.2f, 3.1f, 5.6f), wallCoolMaterial, roofBlueMaterial);
            CreateDecorativeHouse("NorthHouseB", new Vector3(34f, 1.65f, 18f), new Vector3(8f, 3.3f, 6.2f), wallPeachMaterial, roofGreenMaterial);
            CreateDecorativeHouse("WestHouseA", new Vector3(-28f, 1.6f, -14f), new Vector3(7.6f, 3.2f, 5.4f), wallWarmMaterial, roofRedMaterial);
            CreateDecorativeHouse("EastHouseA", new Vector3(31f, 1.55f, -18f), new Vector3(7.4f, 3.1f, 5.8f), wallCoolMaterial, roofBlueMaterial);
            CreateDecorativeHouse("FarNorth", new Vector3(6f, 1.7f, 42f), new Vector3(9.2f, 3.4f, 6.6f), wallPeachMaterial, roofGreenMaterial);
            CreateDecorativeHouse("FarSouth", new Vector3(-4f, 1.7f, -42f), new Vector3(9f, 3.4f, 6.6f), wallWarmMaterial, roofRedMaterial);
            CreateDecorativeHouse("FarWest", new Vector3(-46f, 1.6f, 8f), new Vector3(8.4f, 3.2f, 6.1f), wallCoolMaterial, roofBlueMaterial);
            CreateDecorativeHouse("FarEast", new Vector3(48f, 1.65f, 6f), new Vector3(8.6f, 3.3f, 6.1f), wallPeachMaterial, roofGreenMaterial);

            CreatePlayer(new Vector3(-5f, 0.9f, -10f), new Color(0.16f, 0.41f, 0.95f));

            CreateVillager("villager_mina", "Npc_Mina", new Vector3(2.8f, 0.9f, 10.5f), new Color(0.92f, 0.43f, 0.35f), new Vector3(0.9f, 1.88f, 0.9f), -35f, 0.06f, 1.9f, 6f, 1.4f, 0f, VoxelCharacterAccessoryType.MerchantApron);
            CreateVillager("villager_jisu", "Npc_Jisu", new Vector3(-7.5f, 0.82f, 8.5f), new Color(0.94f, 0.68f, 0.26f), new Vector3(0.82f, 1.68f, 0.82f), 22f, 0.04f, 1.4f, 4f, 1f, 0.7f, VoxelCharacterAccessoryType.GardenerHat);
            CreateVillager("villager_haru", "Npc_Haru", new Vector3(8.5f, 0.96f, 2.5f), new Color(0.34f, 0.75f, 0.46f), new Vector3(0.98f, 1.98f, 0.98f), -12f, 0.03f, 1.2f, 5f, 0.8f, 1.2f, VoxelCharacterAccessoryType.CarpenterBelt);
            CreateVillager("villager_noah", "Npc_Noah", new Vector3(-16f, 0.9f, 15f), new Color(0.32f, 0.64f, 0.92f), new Vector3(0.86f, 1.8f, 0.86f), 40f, 0.02f, 0.9f, 7f, 1.8f, 1.9f, VoxelCharacterAccessoryType.WatcherScarf);
            CreateVillager("villager_yuna", "Npc_Yuna", new Vector3(18f, 0.9f, 13f), new Color(0.76f, 0.47f, 0.9f), new Vector3(0.88f, 1.84f, 0.88f), 148f, 0.05f, 1.6f, 4.5f, 1.2f, 2.4f, VoxelCharacterAccessoryType.LanternCape);
            CreateVillager("villager_kai", "Npc_Kai", new Vector3(24f, 0.9f, -8f), new Color(0.9f, 0.54f, 0.28f), new Vector3(0.92f, 1.86f, 0.92f), -120f, 0.04f, 2.2f, 5.5f, 2.2f, 3.1f, VoxelCharacterAccessoryType.CourierPack);
            CreateVillager("villager_arin", "Npc_Arin", new Vector3(-24f, 0.84f, 2f), new Color(0.89f, 0.52f, 0.62f), new Vector3(0.84f, 1.72f, 0.84f), 84f, 0.03f, 1.1f, 3.5f, 0.9f, 3.8f, VoxelCharacterAccessoryType.MerchantApron);
            CreateVillager("villager_doyun", "Npc_Doyun", new Vector3(30f, 0.95f, 16f), new Color(0.28f, 0.7f, 0.68f), new Vector3(0.96f, 1.94f, 0.96f), 166f, 0.04f, 1.5f, 6.5f, 1.1f, 4.6f, VoxelCharacterAccessoryType.CarpenterBelt);
            CreateVillager("villager_rika", "Npc_Rika", new Vector3(12f, 0.86f, 24f), new Color(0.98f, 0.63f, 0.44f), new Vector3(0.86f, 1.76f, 0.86f), -168f, 0.05f, 1.3f, 4.8f, 1.5f, 5.1f, VoxelCharacterAccessoryType.GardenerHat);
            CreateVillager("villager_sora", "Npc_Sora", new Vector3(-32f, 0.9f, -6f), new Color(0.45f, 0.58f, 0.93f), new Vector3(0.9f, 1.82f, 0.9f), 58f, 0.03f, 1f, 5.8f, 1.7f, 5.9f, VoxelCharacterAccessoryType.WatcherScarf);
            CreateVillager("villager_nari", "Npc_Nari", new Vector3(6f, 0.88f, -20f), new Color(0.82f, 0.43f, 0.86f), new Vector3(0.88f, 1.8f, 0.88f), -42f, 0.04f, 1.7f, 4.2f, 1.4f, 6.6f, VoxelCharacterAccessoryType.LanternCape);
            CreateVillager("villager_toma", "Npc_Toma", new Vector3(40f, 0.92f, -4f), new Color(0.84f, 0.72f, 0.33f), new Vector3(0.94f, 1.9f, 0.94f), -132f, 0.05f, 1.8f, 5.2f, 1.9f, 7.3f, VoxelCharacterAccessoryType.CourierPack);

            CreateTree("TreeNorthWest", new Vector3(-20f, 0f, 28f), trunkMaterial, foliageMaterial);
            CreateTree("TreeSouthWest", new Vector3(-34f, 0f, -28f), trunkMaterial, foliageMaterial);
            CreateTree("TreeNorthEast", new Vector3(26f, 0f, 34f), trunkMaterial, foliageMaterial);
            CreateTree("TreeSouthEast", new Vector3(41f, 0f, -26f), trunkMaterial, foliageMaterial);
            CreateTree("TreeFarWest", new Vector3(-58f, 0f, 10f), trunkMaterial, foliageMaterial);
            CreateTree("TreeFarEast", new Vector3(57f, 0f, -6f), trunkMaterial, foliageMaterial);

            CreateShrub("ShrubA", new Vector3(-6f, 0.55f, 8f), new Vector3(1.5f, 1.2f, 1.4f), shrubMaterial);
            CreateShrub("ShrubB", new Vector3(9f, 0.55f, 15f), new Vector3(1.2f, 1f, 1.2f), shrubMaterial);
            CreateShrub("ShrubC", new Vector3(-22f, 0.55f, -10f), new Vector3(1.6f, 1.2f, 1.4f), shrubMaterial);
            CreateShrub("ShrubD", new Vector3(22f, 0.55f, -14f), new Vector3(1.7f, 1.2f, 1.5f), shrubMaterial);

            CreateFlower("FlowerA", new Vector3(-4f, 0.18f, 6f), flowerYellowMaterial);
            CreateFlower("FlowerB", new Vector3(-3f, 0.18f, 6.8f), flowerPinkMaterial);
            CreateFlower("FlowerC", new Vector3(11f, 0.18f, 14f), flowerYellowMaterial);
            CreateFlower("FlowerD", new Vector3(12f, 0.18f, 14.7f), flowerPinkMaterial);

            var pond = CreatePrimitive(
                PrimitiveType.Cube,
                "Pond",
                new Vector3(-18f, -0.42f, -24f),
                new Vector3(16f, 0.08f, 12f),
                waterMaterial);
            pond.transform.SetParent(_worldRoot, false);
        }

        void BuildHud()
        {
            _canvas = FindAnyObjectByType<Canvas>();
            if (_canvas == null || _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                var canvasObject = new GameObject("VoxelVillageHud");
                _canvas = canvasObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            var font = LoadUiFont();

            _helpText = CreatePanelText(
                "HelpPanel",
                new Vector2(16f, 16f),
                new Vector2(470f, 138f),
                font,
                18,
                TextAnchor.UpperLeft,
                new Color(0.08f, 0.12f, 0.18f, 0.78f),
                new Color(0.94f, 0.97f, 1f),
                true);

            _helpPanel = _helpText.transform.parent.gameObject;
            var helpRect = (RectTransform)_helpPanel.transform;
            helpRect.anchorMin = new Vector2(0f, 0f);
            helpRect.anchorMax = new Vector2(0f, 0f);
            helpRect.pivot = new Vector2(0f, 0f);
            helpRect.anchoredPosition = new Vector2(16f, 16f);

            _promptText = CreatePanelText(
                "PromptPanel",
                new Vector2(0f, 110f),
                new Vector2(160f, PromptMinHeight),
                font,
                22,
                TextAnchor.MiddleCenter,
                new Color(0.09f, 0.13f, 0.2f, 0.82f),
                new Color(1f, 0.97f, 0.92f));
            _promptText.fontStyle = FontStyle.Bold;
            _promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _promptText.verticalOverflow = VerticalWrapMode.Overflow;

            var promptTextRect = _promptText.rectTransform;
            promptTextRect.offsetMin = new Vector2(PromptHorizontalPadding, PromptVerticalPadding);
            promptTextRect.offsetMax = new Vector2(-PromptHorizontalPadding, -PromptVerticalPadding);

            var promptRect = (RectTransform)_promptText.transform.parent;
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 110f);

            CreateLanguageButton(font);
            CreateControlsButton(font);
            CreateSpeechBubble(font);

            _helpVisible = false;
            _helpPanel.SetActive(false);
        }

        void CreateLanguageButton(Font font)
        {
            var buttonObject = new GameObject("LanguageButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_canvas.transform, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-16f, -16f);
            rectTransform.sizeDelta = new Vector2(220f, 56f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.96f, 0.75f, 0.26f, 0.95f);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(1f, 0.83f, 0.36f, 1f);
            colors.pressedColor = new Color(0.85f, 0.65f, 0.21f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(OnLanguageButtonClicked);

            var textObject = new GameObject("LanguageButton_Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _languageButtonText = textObject.GetComponent<Text>();
            _languageButtonText.font = font;
            _languageButtonText.fontSize = 19;
            _languageButtonText.alignment = TextAnchor.MiddleCenter;
            _languageButtonText.color = new Color(0.14f, 0.11f, 0.08f);
        }

        void CreateControlsButton(Font font)
        {
            var buttonObject = new GameObject("ControlsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_canvas.transform, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-252f, -16f);
            rectTransform.sizeDelta = new Vector2(170f, 56f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.86f, 0.91f, 0.96f, 0.96f);

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.93f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.74f, 0.82f, 0.9f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(OnControlsButtonClicked);

            var textObject = new GameObject("ControlsButton_Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _controlsButtonText = textObject.GetComponent<Text>();
            _controlsButtonText.font = font;
            _controlsButtonText.fontSize = 18;
            _controlsButtonText.alignment = TextAnchor.MiddleCenter;
            _controlsButtonText.color = new Color(0.14f, 0.2f, 0.26f);
        }

        void CreateSpeechBubble(Font font)
        {
            var bubble = new GameObject("SpeechBubble", typeof(RectTransform), typeof(SpeechBubbleGraphic), typeof(Shadow));
            bubble.transform.SetParent(_canvas.transform, false);

            _bubbleRect = bubble.GetComponent<RectTransform>();
            _bubbleRect.sizeDelta = new Vector2(312f, 138f);
            _bubbleRect.pivot = new Vector2(0.5f, 0f);

            var bubbleGraphic = bubble.GetComponent<SpeechBubbleGraphic>();
            bubbleGraphic.color = new Color(0.99f, 0.97f, 0.92f, 0.96f);
            bubbleGraphic.raycastTarget = false;
            bubbleGraphic.CornerRadius = 24f;
            bubbleGraphic.TailWidth = 34f;
            bubbleGraphic.TailHeight = 18f;
            bubbleGraphic.CornerSegments = 6;

            var shadow = bubble.GetComponent<Shadow>();
            shadow.effectColor = new Color(0.16f, 0.11f, 0.07f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;

            var speakerObject = new GameObject("Speaker", typeof(RectTransform), typeof(Text));
            speakerObject.transform.SetParent(bubble.transform, false);
            var speakerRect = speakerObject.GetComponent<RectTransform>();
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0.5f, 1f);
            speakerRect.offsetMin = new Vector2(18f, -40f);
            speakerRect.offsetMax = new Vector2(-18f, -14f);

            _bubbleSpeakerText = speakerObject.GetComponent<Text>();
            _bubbleSpeakerText.font = font;
            _bubbleSpeakerText.fontStyle = FontStyle.Bold;
            _bubbleSpeakerText.fontSize = 19;
            _bubbleSpeakerText.alignment = TextAnchor.UpperLeft;
            _bubbleSpeakerText.color = new Color(0.23f, 0.18f, 0.12f);
            _bubbleSpeakerText.raycastTarget = false;

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(Text));
            contentObject.transform.SetParent(bubble.transform, false);
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(18f, 24f);
            contentRect.offsetMax = new Vector2(-18f, -42f);

            _bubbleContentText = contentObject.GetComponent<Text>();
            _bubbleContentText.font = font;
            _bubbleContentText.fontSize = 20;
            _bubbleContentText.alignment = TextAnchor.UpperLeft;
            _bubbleContentText.color = new Color(0.16f, 0.14f, 0.12f);
            _bubbleContentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bubbleContentText.verticalOverflow = VerticalWrapMode.Overflow;
            _bubbleContentText.raycastTarget = false;

            bubble.SetActive(false);
        }

        Text CreatePanelText(
            string panelName,
            Vector2 anchoredPosition,
            Vector2 size,
            Font font,
            int fontSize,
            TextAnchor anchor,
            Color panelColor,
            Color textColor,
            bool bestFit = false)
        {
            var panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_canvas.transform, false);

            var rectTransform = panel.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            panel.GetComponent<Image>().color = panelColor;

            var textObject = new GameObject(panelName + "_Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 12f);
            textRect.offsetMax = new Vector2(-14f, -12f);

            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = textColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = bestFit;
            if (bestFit)
            {
                text.resizeTextMinSize = 12;
                text.resizeTextMaxSize = fontSize;
            }

            return text;
        }

        void HandleMovement()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var move = Vector3.zero;
            if (keyboard.wKey.isPressed)
            {
                move.z += 1f;
            }

            if (keyboard.sKey.isPressed)
            {
                move.z -= 1f;
            }

            if (keyboard.aKey.isPressed)
            {
                move.x -= 1f;
            }

            if (keyboard.dKey.isPressed)
            {
                move.x += 1f;
            }

            if (move.sqrMagnitude <= 0f)
            {
                return;
            }

            move = move.normalized * (PlayerMoveSpeed * Time.deltaTime);
            var currentPosition = _player.transform.position;
            var nextPosition = currentPosition + move;
            nextPosition.x = Mathf.Clamp(nextPosition.x, -TownHalfExtent, TownHalfExtent);
            nextPosition.z = Mathf.Clamp(nextPosition.z, -TownHalfExtent, TownHalfExtent);
            nextPosition = ResolveVillagerBlocking(currentPosition, nextPosition);
            nextPosition.y = currentPosition.y;
            _player.transform.position = nextPosition;
            _player.transform.forward = Vector3.Lerp(_player.transform.forward, move.normalized, 16f * Time.deltaTime);
        }

        void UpdateCamera()
        {
            var targetPosition = _player.transform.position + new Vector3(0f, CameraHeight, -CameraDistance);
            _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, targetPosition, CameraFollowSpeed * Time.deltaTime);
            var lookTarget = _player.transform.position + new Vector3(0f, 1.4f, CameraLookAhead);
            _mainCamera.transform.rotation = Quaternion.Lerp(
                _mainCamera.transform.rotation,
                Quaternion.LookRotation(lookTarget - _mainCamera.transform.position, Vector3.up),
                CameraFollowSpeed * Time.deltaTime);
        }

        void UpdateInteractionTarget()
        {
            var playerPosition = _player.transform.position;

            if (_dialogueActive && _activeDialogueVillager != null)
            {
                if (Vector3.Distance(playerPosition, _activeDialogueVillager.Transform.position) > InteractionDistance + 1f)
                {
                    _dialogueActive = false;
                    _dialogueLineIndex = 0;
                    _activeDialogueVillager = null;
                }
                else
                {
                    _currentTarget = InteractionTarget.Npc;
                    _currentVillager = _activeDialogueVillager;
                    return;
                }
            }

            var doorDistance = Vector3.Distance(playerPosition, GetDoorInteractionPoint());

            _currentTarget = InteractionTarget.None;
            _currentVillager = null;
            var bestDistance = InteractionDistance;

            for (var index = 0; index < _villagers.Count; index++)
            {
                var villager = _villagers[index];
                var npcDistance = Vector3.Distance(playerPosition, villager.Transform.position);
                if (npcDistance <= bestDistance)
                {
                    _currentTarget = InteractionTarget.Npc;
                    _currentVillager = villager;
                    bestDistance = npcDistance;
                }
            }

            if (doorDistance <= bestDistance)
            {
                _currentTarget = InteractionTarget.Door;
                _currentVillager = null;
            }
        }

        void HandleInteractionInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
            {
                return;
            }

            switch (_currentTarget)
            {
                case InteractionTarget.Npc:
                    AdvanceDialogue();
                    break;

                case InteractionTarget.Door:
                    _doorOpen = !_doorOpen;
                    break;
            }
        }

        void AdvanceDialogue()
        {
            var villager = _dialogueActive ? _activeDialogueVillager : _currentVillager;
            if (villager == null)
            {
                return;
            }

            var lineCount = _database.GetDialogueLineCount(villager.NpcId);
            if (lineCount <= 0)
            {
                return;
            }

            if (!_dialogueActive)
            {
                _dialogueActive = true;
                _activeDialogueVillager = villager;
                _dialogueLineIndex = 0;
                RefreshLocalizedTexts();
                return;
            }

            if (_dialogueLineIndex < lineCount - 1)
            {
                _dialogueLineIndex++;
                RefreshLocalizedTexts();
                return;
            }

            _dialogueActive = false;
            _dialogueLineIndex = 0;
            _activeDialogueVillager = null;
            RefreshLocalizedTexts();
        }

        void UpdateDoorVisual()
        {
            var targetYaw = _doorOpen ? -105f : 0f;
            _doorYaw = Mathf.LerpAngle(_doorYaw, targetYaw, 10f * Time.deltaTime);
            _doorPivot.localRotation = Quaternion.Euler(0f, _doorYaw, 0f);
        }

        void UpdateSpeechBubble()
        {
            if (_bubbleRect == null)
            {
                return;
            }

            if (!_dialogueActive)
            {
                _bubbleRect.gameObject.SetActive(false);
                return;
            }

            if (_activeDialogueVillager == null)
            {
                _bubbleRect.gameObject.SetActive(false);
                return;
            }

            var line = _database.GetDialogueLine(_activeDialogueVillager.NpcId, _dialogueLineIndex);
            if (line == null)
            {
                _bubbleRect.gameObject.SetActive(false);
                return;
            }

            _bubbleSpeakerText.text =
                line.speaker.Equals("npc", System.StringComparison.OrdinalIgnoreCase)
                    ? _database.GetNpcHeader(_activeDialogueVillager.NpcId, _languageState.Current)
                    : _database.GetSpeakerDisplayName(line.speaker, _activeDialogueVillager.NpcId, _languageState.Current);
            _bubbleContentText.text = line.translations.Get(_languageState.Current);

            var screenPoint = _mainCamera.WorldToScreenPoint(_activeDialogueVillager.Transform.position + new Vector3(0f, BubbleHeight + _activeDialogueVillager.HeadOffset, 0f));
            var visible = screenPoint.z > 0f;
            _bubbleRect.gameObject.SetActive(visible);
            if (visible)
            {
                _bubbleRect.position = new Vector3(screenPoint.x, screenPoint.y, 0f);
            }
        }

        void RefreshLocalizedTexts()
        {
            _helpText.text = _database.GetUiText("hud.instructions", _languageState.Current);
            _languageButtonText.text = string.Format(
                _database.GetUiText("hud.language.label", _languageState.Current),
                _database.GetUiText("language.name." + _languageState.Current.ToCode(), _languageState.Current));
            _controlsButtonText.text = _database.GetUiText(
                _helpVisible ? "hud.controls.hide" : "hud.controls.show",
                _languageState.Current);

            RefreshPrompt();
            UpdateSpeechBubble();
        }

        void RefreshPrompt()
        {
            if (_promptText == null)
            {
                return;
            }

            string key;
            switch (_currentTarget)
            {
                case InteractionTarget.Npc:
                    if (_currentVillager == null && _activeDialogueVillager == null)
                    {
                        _promptText.transform.parent.gameObject.SetActive(false);
                        return;
                    }

                    var promptVillager = _activeDialogueVillager ?? _currentVillager!;
                    var lineCount = _database.GetDialogueLineCount(promptVillager.NpcId);
                    if (!_dialogueActive)
                    {
                        key = "interaction.talk";
                    }
                    else if (_dialogueLineIndex < lineCount - 1)
                    {
                        key = "interaction.nextLine";
                    }
                    else
                    {
                        key = "interaction.closeDialogue";
                    }

                    break;

                case InteractionTarget.Door:
                    key = _doorOpen ? "interaction.closeDoor" : "interaction.openDoor";
                    break;

                default:
                    _promptText.transform.parent.gameObject.SetActive(false);
                    return;
            }

            _promptText.transform.parent.gameObject.SetActive(true);
            _promptText.text = _database.GetUiText(key, _languageState.Current);
            ResizePromptPanel();
        }

        void ResizePromptPanel()
        {
            var promptRect = (RectTransform)_promptText.transform.parent;
            var preferredWidth = _promptText.preferredWidth + (PromptHorizontalPadding * 2f);
            var preferredHeight = _promptText.preferredHeight + (PromptVerticalPadding * 2f);

            promptRect.sizeDelta = new Vector2(
                Mathf.Clamp(preferredWidth, PromptMinWidth, PromptMaxWidth),
                Mathf.Max(PromptMinHeight, preferredHeight));
        }

        void OnLanguageButtonClicked()
        {
            _languageState.CycleNext();
        }

        void OnControlsButtonClicked()
        {
            _helpVisible = !_helpVisible;
            _helpPanel.SetActive(_helpVisible);
            _controlsButtonText.text = _database.GetUiText(
                _helpVisible ? "hud.controls.hide" : "hud.controls.show",
                _languageState.Current);
        }

        void OnLanguageChanged(LanguageCode _)
        {
            RefreshLocalizedTexts();
        }

        static Font LoadUiFont()
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(
                    new[]
                    {
                        "Malgun Gothic",
                        "Yu Gothic UI",
                        "Meiryo",
                        "Segoe UI",
                        "Arial Unicode MS",
                        "Arial"
                    },
                    20);
            }
            catch
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        Vector3 GetDoorInteractionPoint()
        {
            return _doorPivot.position + new Vector3(-0.45f, 1.1f, 0f);
        }

        Vector3 ResolveVillagerBlocking(Vector3 currentPosition, Vector3 desiredPosition)
        {
            var resolved = new Vector2(desiredPosition.x, desiredPosition.z);
            var current = new Vector2(currentPosition.x, currentPosition.z);
            var minDistance = PersonCollisionRadius * 2f;

            for (var index = 0; index < _villagers.Count; index++)
            {
                var villager = _villagers[index];
                if (villager == null)
                {
                    continue;
                }

                resolved = PlanarPersonCollision.Resolve(
                    current,
                    resolved,
                    new Vector2(villager.Transform.position.x, villager.Transform.position.z),
                    minDistance);
            }

            desiredPosition.x = resolved.x;
            desiredPosition.z = resolved.y;
            return desiredPosition;
        }

        void UpdateVillagers()
        {
            var time = Time.time;
            for (var index = 0; index < _villagers.Count; index++)
            {
                var villager = _villagers[index];
                var bob = Mathf.Sin(time * villager.BobSpeed + villager.PhaseOffset) * villager.BobAmplitude;
                var sway = Mathf.Sin(time * villager.SwaySpeed + villager.PhaseOffset) * villager.SwayAngle;

                villager.Transform.position = villager.BasePosition + new Vector3(0f, bob, 0f);
                villager.Transform.rotation = Quaternion.Euler(0f, villager.BaseYaw + sway, 0f);
            }
        }

        void CreateInteractiveHouse(
            Vector3 center,
            Vector3 houseScale,
            Material wallMaterial,
            Material roofMaterial,
            Material woodMaterial,
            Material pathMaterial)
        {
            CreateDecorativeHouse("InteractiveHouse", center, houseScale, wallMaterial, roofMaterial);

            var doorAnchorPosition = center + new Vector3(-(houseScale.x * 0.5f) - 0.02f, 0f, houseScale.z * 0.24f);
            var doorFrame = CreatePrimitive(
                PrimitiveType.Cube,
                "DoorFrame",
                doorAnchorPosition + new Vector3(-0.1f, 1.12f, 0f),
                new Vector3(0.2f, 2.24f, 1.1f),
                woodMaterial);
            doorFrame.transform.SetParent(_worldRoot, false);

            _doorPivot = new GameObject("DoorPivot").transform;
            _doorPivot.SetParent(_worldRoot, false);
            _doorPivot.position = doorAnchorPosition;

            var doorPanel = CreatePrimitive(
                PrimitiveType.Cube,
                "DoorPanel",
                new Vector3(0.45f, 1.1f, 0f),
                new Vector3(0.9f, 2.2f, 0.12f),
                woodMaterial);
            doorPanel.transform.SetParent(_doorPivot, false);

            var walkway = CreatePrimitive(
                PrimitiveType.Cube,
                "DoorWalkway",
                doorAnchorPosition + new Vector3(-4.2f, -0.43f, 0f),
                new Vector3(8.4f, 0.1f, 1.8f),
                pathMaterial);
            walkway.transform.SetParent(_worldRoot, false);
        }

        void CreateDecorativeHouse(
            string name,
            Vector3 center,
            Vector3 houseScale,
            Material wallMaterial,
            Material roofMaterial)
        {
            var house = CreatePrimitive(
                PrimitiveType.Cube,
                name,
                center,
                houseScale,
                wallMaterial);
            house.transform.SetParent(_worldRoot, false);

            var roof = CreatePrimitive(
                PrimitiveType.Cube,
                name + "_Roof",
                center + new Vector3(0f, houseScale.y * 0.62f + 0.46f, 0f),
                houseScale + new Vector3(0.7f, 0.8f, 0.7f),
                roofMaterial);
            roof.transform.SetParent(_worldRoot, false);
        }

        void CreateTree(string name, Vector3 basePosition, Material trunkMaterial, Material foliageMaterial)
        {
            var trunk = CreatePrimitive(
                PrimitiveType.Cylinder,
                name + "_Trunk",
                basePosition + new Vector3(0f, 1.4f, 0f),
                new Vector3(0.45f, 1.4f, 0.45f),
                trunkMaterial);
            trunk.transform.SetParent(_worldRoot, false);

            var crown = CreatePrimitive(
                PrimitiveType.Sphere,
                name + "_Crown",
                basePosition + new Vector3(0f, 4f, 0f),
                new Vector3(3.8f, 3.2f, 3.8f),
                foliageMaterial);
            crown.transform.SetParent(_worldRoot, false);
        }

        void CreateShrub(string name, Vector3 position, Vector3 scale, Material material)
        {
            var shrub = CreatePrimitive(
                PrimitiveType.Sphere,
                name,
                position,
                scale,
                material);
            shrub.transform.SetParent(_worldRoot, false);
        }

        void CreateFlower(string name, Vector3 position, Material petalMaterial)
        {
            var flower = CreatePrimitive(
                PrimitiveType.Cylinder,
                name,
                position,
                new Vector3(0.12f, 0.18f, 0.12f),
                petalMaterial);
            flower.transform.SetParent(_worldRoot, false);
        }

        void CreatePlayer(Vector3 position, Color color)
        {
            var player = VoxelCharacterFactory.CreateCharacter(
                "Player",
                position,
                color,
                VoxelCharacterAccessoryType.None,
                true);

            _player = player.Root;
            _player.transform.SetParent(_worldRoot, true);
        }

        void CreateVillager(
            string npcId,
            string objectName,
            Vector3 position,
            Color color,
            Vector3 scale,
            float yaw,
            float bobAmplitude,
            float bobSpeed,
            float swayAngle,
            float swaySpeed,
            float phaseOffset,
            VoxelCharacterAccessoryType accessoryType)
        {
            var character = VoxelCharacterFactory.CreateCharacter(
                objectName,
                position,
                color,
                accessoryType,
                false,
                scale.y / BaseCharacterHeight);
            var villager = character.Root;
            villager.transform.SetParent(_worldRoot, true);
            villager.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _villagers.Add(new VillagerInstance(npcId, villager.transform, position, yaw, bobAmplitude, bobSpeed, swayAngle, swaySpeed, phaseOffset, character.HeadOffset));
        }

        static GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.position = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().material = material;
            return primitive;
        }

        static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.08f);
            }

            return material;
        }

        sealed class VillagerInstance
        {
            public VillagerInstance(
                string npcId,
                Transform transform,
                Vector3 basePosition,
                float baseYaw,
                float bobAmplitude,
                float bobSpeed,
                float swayAngle,
                float swaySpeed,
                float phaseOffset,
                float headOffset)
            {
                NpcId = npcId;
                Transform = transform;
                BasePosition = basePosition;
                BaseYaw = baseYaw;
                BobAmplitude = bobAmplitude;
                BobSpeed = bobSpeed;
                SwayAngle = swayAngle;
                SwaySpeed = swaySpeed;
                PhaseOffset = phaseOffset;
                HeadOffset = headOffset;
            }

            public string NpcId { get; }

            public Transform Transform { get; }

            public Vector3 BasePosition { get; }

            public float BaseYaw { get; }

            public float BobAmplitude { get; }

            public float BobSpeed { get; }

            public float SwayAngle { get; }

            public float SwaySpeed { get; }

            public float PhaseOffset { get; }

            public float HeadOffset { get; }
        }
    }

    public static class PlanarPersonCollision
    {
        public static Vector2 Resolve(Vector2 currentPosition, Vector2 desiredPosition, Vector2 obstaclePosition, float minimumDistance)
        {
            var offset = desiredPosition - obstaclePosition;
            var distance = offset.magnitude;
            if (distance >= minimumDistance)
            {
                return desiredPosition;
            }

            if (distance > 0.0001f)
            {
                return obstaclePosition + offset / distance * minimumDistance;
            }

            var fallbackDirection = currentPosition - obstaclePosition;
            if (fallbackDirection.sqrMagnitude <= 0.0001f)
            {
                fallbackDirection = Vector2.up;
            }

            return obstaclePosition + fallbackDirection.normalized * minimumDistance;
        }
    }
}
