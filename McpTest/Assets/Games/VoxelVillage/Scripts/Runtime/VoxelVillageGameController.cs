#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
        const float PlayerCollisionRadius = 0.42f;
        const float VillagerMoveSpeed = 2.55f;
        const float VillagerCollisionRadius = 0.56f;
        const float InteractionDistance = 2.15f;
        const float CameraFollowSpeed = 6f;
        const float BubbleHeight = 2.45f;
        const float TownScaleMultiplier = 8f;
        const float BaseTownFootprint = 18f;
        const float TownFootprint = BaseTownFootprint * TownScaleMultiplier;
        const float TownHalfExtent = TownFootprint * 0.5f;
        const float CameraHeight = 18f;
        const float CameraDistance = 20f;
        const float CameraLookAhead = 13f;
        const float BaseCharacterHeight = 1.8f;
        const float PromptHorizontalPadding = 14f;
        const float PromptVerticalPadding = 9f;
        const float PromptMinWidth = 104f;
        const float PromptMaxWidth = 240f;
        const float PromptMinHeight = 44f;
        const float DoorOpenAngle = 108f;
        const int WorldGridSize = 72;
        const float WorldCellSize = TownFootprint / WorldGridSize;

        static readonly VillagerStyle[] VillagerStyles =
        {
            new VillagerStyle("villager_mina", new Color(0.82f, 0.35f, 0.31f), VoxelCharacterAccessoryType.MerchantApron, 1.04f),
            new VillagerStyle("villager_jisu", new Color(0.83f, 0.68f, 0.24f), VoxelCharacterAccessoryType.GardenerHat, 0.96f),
            new VillagerStyle("villager_haru", new Color(0.28f, 0.63f, 0.36f), VoxelCharacterAccessoryType.CarpenterBelt, 1.02f),
            new VillagerStyle("villager_noah", new Color(0.29f, 0.48f, 0.82f), VoxelCharacterAccessoryType.WatcherScarf, 0.98f),
            new VillagerStyle("villager_yuna", new Color(0.63f, 0.42f, 0.82f), VoxelCharacterAccessoryType.LanternCape, 1.01f),
            new VillagerStyle("villager_kai", new Color(0.9f, 0.52f, 0.2f), VoxelCharacterAccessoryType.CourierPack, 0.99f),
            new VillagerStyle("villager_arin", new Color(0.86f, 0.46f, 0.53f), VoxelCharacterAccessoryType.MerchantApron, 0.95f),
            new VillagerStyle("villager_doyun", new Color(0.22f, 0.66f, 0.64f), VoxelCharacterAccessoryType.CarpenterBelt, 1.07f),
            new VillagerStyle("villager_rika", new Color(0.95f, 0.52f, 0.46f), VoxelCharacterAccessoryType.GardenerHat, 0.97f),
            new VillagerStyle("villager_sora", new Color(0.21f, 0.28f, 0.51f), VoxelCharacterAccessoryType.WatcherScarf, 1.03f),
            new VillagerStyle("villager_nari", new Color(0.78f, 0.29f, 0.62f), VoxelCharacterAccessoryType.LanternCape, 0.94f),
            new VillagerStyle("villager_toma", new Color(0.88f, 0.63f, 0.18f), VoxelCharacterAccessoryType.CourierPack, 1.05f)
        };

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
        ReflectionProbe _globalReflectionProbe = null!;

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
        VillageLayoutData _layout = null!;
        VillageGrid _villageGrid = null!;
        readonly List<VillagerInstance> _villagers = new List<VillagerInstance>();
        readonly List<DoorInstance> _doors = new List<DoorInstance>();
        readonly List<Vector2Int> _patrolCells = new List<Vector2Int>();
        System.Random _worldRandom = new System.Random();

        InteractionTarget _currentTarget;
        VillagerInstance? _currentVillager;
        VillagerInstance? _activeDialogueVillager;
        DoorInstance? _currentDoor;
        bool _dialogueActive;
        int _dialogueLineIndex;
        bool _helpVisible;
        int _worldSeed;

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
            _mainLight.shadows = LightShadows.Soft;
            _mainLight.shadowStrength = 0.92f;
            _mainLight.shadowBias = 0.08f;
            _mainLight.shadowNormalBias = 0.4f;
            _mainLight.shadowNearPlane = 0.2f;
            _mainLight.bounceIntensity = 1.2f;
            RenderSettings.sun = _mainLight;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.53f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.29f, 0.3f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.21f, 0.28f, 0.2f);
            RenderSettings.reflectionIntensity = 1.05f;
            DynamicGI.UpdateEnvironment();

            if (QualitySettings.shadowDistance < 85f)
            {
                QualitySettings.shadowDistance = 85f;
            }

            if (!QualitySettings.realtimeReflectionProbes)
            {
                QualitySettings.realtimeReflectionProbes = true;
            }
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
            if (_worldRoot != null)
            {
                Destroy(_worldRoot.gameObject);
            }

            _villagers.Clear();
            _doors.Clear();
            _patrolCells.Clear();
            _currentTarget = InteractionTarget.None;
            _currentVillager = null;
            _currentDoor = null;
            _activeDialogueVillager = null;
            _dialogueActive = false;
            _dialogueLineIndex = 0;

            _worldSeed = Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7fffffff);
            _worldRandom = new System.Random(_worldSeed);

            _worldRoot = new GameObject("VoxelVillageWorld").transform;
            _worldRoot.SetParent(transform, false);

            _layout = ProceduralVillageGenerator.Generate(_worldSeed, WorldGridSize);
            _villageGrid = VillageGrid.FromLayout(_layout);
            CollectPatrolCells();

            var grassMaterial = CreateMaterial(new Color(0.49f, 0.74f, 0.46f));
            var roadMaterial = CreateMaterial(new Color(0.77f, 0.67f, 0.5f));
            var plazaMaterial = CreateMaterial(new Color(0.87f, 0.8f, 0.63f));
            var waterMaterial = CreateMaterial(new Color(0.31f, 0.61f, 0.82f));

            var ground = CreatePrimitive(
                PrimitiveType.Cube,
                "Ground",
                new Vector3(0f, -0.5f, 0f),
                new Vector3(TownFootprint, 1f, TownFootprint),
                grassMaterial);
            ground.transform.SetParent(_worldRoot, false);

            BuildGridSurface(roadMaterial, plazaMaterial);
            BuildProceduralVillage(roadMaterial);
            BuildFountain();
            BuildPond(waterMaterial);
            CreatePlayer(CellToWorld(_layout.plazaCenter + new Vector2Int(0, -6)) + new Vector3(0f, 0.9f, 0f), new Color(0.16f, 0.41f, 0.95f));
            SpawnVillagersFromLayout();
            EnsureGlobalIlluminationProbe();
        }

        void EnsureGlobalIlluminationProbe()
        {
            if (_globalReflectionProbe == null)
            {
                var probeObject = new GameObject("VoxelVillage Reflection Probe");
                probeObject.transform.SetParent(transform, false);
                _globalReflectionProbe = probeObject.AddComponent<ReflectionProbe>();
            }

            _globalReflectionProbe.transform.position = new Vector3(0f, 9f, 0f);
            _globalReflectionProbe.mode = ReflectionProbeMode.Realtime;
            _globalReflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _globalReflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            _globalReflectionProbe.clearFlags = ReflectionProbeClearFlags.Skybox;
            _globalReflectionProbe.boxProjection = true;
            _globalReflectionProbe.size = new Vector3(TownFootprint, 18f, TownFootprint);
            _globalReflectionProbe.center = new Vector3(0f, 2f, 0f);
            _globalReflectionProbe.nearClipPlane = 0.3f;
            _globalReflectionProbe.farClipPlane = TownFootprint * 1.5f;
            _globalReflectionProbe.intensity = 1.1f;
            _globalReflectionProbe.resolution = 256;
            _globalReflectionProbe.cullingMask = ~0;
            _globalReflectionProbe.RenderProbe();
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

        void BuildGridSurface(Material roadMaterial, Material plazaMaterial)
        {
            var tilesRoot = new GameObject("VillageTiles").transform;
            tilesRoot.SetParent(_worldRoot, false);

            for (var y = 0; y < _villageGrid.Height; y++)
            {
                for (var x = 0; x < _villageGrid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var kind = _villageGrid.GetCellKind(cell);
                    if (kind != VillageCellKind.Road && kind != VillageCellKind.Plaza)
                    {
                        continue;
                    }

                    var tile = CreatePrimitive(
                        PrimitiveType.Cube,
                        $"{kind}_{x}_{y}",
                        CellToWorld(cell) + new Vector3(0f, -0.42f, 0f),
                        new Vector3(WorldCellSize * 0.98f, 0.16f, WorldCellSize * 0.98f),
                        kind == VillageCellKind.Plaza ? plazaMaterial : roadMaterial);
                    tile.transform.SetParent(tilesRoot, false);
                }
            }
        }

        void BuildProceduralVillage(Material _)
        {
            for (var buildingIndex = 0; buildingIndex < _layout.buildings.Length; buildingIndex++)
            {
                var building = _layout.buildings[buildingIndex];
                var buildingDoor = FindDoorForBuilding(building.id);
                var facing = buildingDoor?.facing ?? Vector2Int.down;
                var center = CellRectCenterToWorld(building.origin, building.size);
                var palette = GetBuildingPalette(buildingIndex);
                var height = 3.6f + building.height * 0.45f;
                var size = new Vector3(
                    building.size.x * WorldCellSize * 0.96f,
                    height,
                    building.size.y * WorldCellSize * 0.96f);

                var house = VoxelEnvironmentFactory.CreateHouse(
                    "House_" + building.id,
                    center + new Vector3(0f, height * 0.5f, 0f),
                    size,
                    YawFromDirection(facing),
                    palette.Wall,
                    palette.Roof,
                    palette.Trim);
                house.Root.transform.SetParent(_worldRoot, true);
            }

            for (var doorIndex = 0; doorIndex < _layout.doors.Length; doorIndex++)
            {
                CreateDoor(_layout.doors[doorIndex]);
            }

            for (var foliageIndex = 0; foliageIndex < _layout.foliage.Length; foliageIndex++)
            {
                CreateFoliage(_layout.foliage[foliageIndex], foliageIndex);
            }
        }

        void BuildFountain()
        {
            var fountainHeight = 3.6f;
            var fountain = VoxelEnvironmentFactory.CreateFountain(
                "PlazaFountain",
                CellToWorld(_layout.plazaCenter) + new Vector3(0f, fountainHeight * 0.5f, 0f),
                new Vector3(WorldCellSize * 2.4f, fountainHeight, WorldCellSize * 2.4f),
                0f,
                new Color(0.76f, 0.76f, 0.79f),
                new Color(0.33f, 0.72f, 0.9f));
            fountain.Root.transform.SetParent(_worldRoot, true);
        }

        void BuildPond(Material waterMaterial)
        {
            if (!TryFindPondRect(out var pondRect))
            {
                return;
            }

            var pondRoot = new GameObject("Pond").transform;
            pondRoot.SetParent(_worldRoot, false);

            for (var y = pondRect.yMin; y < pondRect.yMax; y++)
            {
                for (var x = pondRect.xMin; x < pondRect.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    _villageGrid.SetCellKind(cell, VillageCellKind.Foliage);

                    var waterTile = CreatePrimitive(
                        PrimitiveType.Cube,
                        $"Pond_{x}_{y}",
                        CellToWorld(cell) + new Vector3(0f, -0.36f, 0f),
                        new Vector3(WorldCellSize * 0.98f, 0.22f, WorldCellSize * 0.98f),
                        waterMaterial);
                    waterTile.transform.SetParent(pondRoot, false);
                }
            }
        }

        void SpawnVillagersFromLayout()
        {
            var spawnCount = Mathf.Min(VillagerStyles.Length, _layout.npcSpawnPoints.Length);
            var usedCells = new HashSet<Vector2Int>();
            for (var index = 0; index < spawnCount; index++)
            {
                var style = VillagerStyles[index];
                var spawn = _layout.npcSpawnPoints[index];
                var spawnCell = FindUniqueSpawnCell(spawn.cell, usedCells);
                usedCells.Add(spawnCell);
                var groundPosition = CellToWorld(spawnCell) + new Vector3(0f, 0.9f, 0f);

                CreateVillager(
                    style.NpcId,
                    "NPC_" + index,
                    groundPosition,
                    style.Color,
                    Vector3.one * (style.HeightScale * BaseCharacterHeight),
                    YawFromDirection(spawn.facing),
                    Range(0.02f, 0.045f),
                    Range(1.4f, 2.6f),
                    Range(2.5f, 6f),
                    Range(0.7f, 1.45f),
                    Range(0f, Mathf.PI * 2f),
                    style.AccessoryType,
                    spawnCell);
            }
        }

        Vector2Int FindUniqueSpawnCell(Vector2Int preferred, HashSet<Vector2Int> usedCells)
        {
            if (!usedCells.Contains(preferred))
            {
                return preferred;
            }

            for (var radius = 1; radius <= 8; radius++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        var candidate = preferred + new Vector2Int(x, y);
                        if (usedCells.Contains(candidate))
                        {
                            continue;
                        }

                        if (_villageGrid.IsWalkable(candidate, false))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return preferred;
        }

        void HandleMovement()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _player == null)
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

            move.Normalize();
            var currentPosition = _player.transform.position;
            var nextPosition = MoveWithWorldCollision(currentPosition, move * (PlayerMoveSpeed * Time.deltaTime), PlayerCollisionRadius, true);
            nextPosition = ResolveDynamicBlocking(null, currentPosition, nextPosition, PlayerCollisionRadius);
            nextPosition.y = currentPosition.y;
            _player.transform.position = nextPosition;

            var look = nextPosition - currentPosition;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
            {
                _player.transform.forward = Vector3.Lerp(_player.transform.forward, look.normalized, 16f * Time.deltaTime);
            }
        }

        Vector3 MoveWithWorldCollision(Vector3 currentPosition, Vector3 delta, float collisionRadius, bool includeEmpty)
        {
            var resolved = currentPosition;
            var xCandidate = ClampWorldPosition(resolved + new Vector3(delta.x, 0f, 0f), collisionRadius);
            if (IsPositionWalkable(xCandidate, collisionRadius, includeEmpty))
            {
                resolved.x = xCandidate.x;
            }

            var zCandidate = ClampWorldPosition(resolved + new Vector3(0f, 0f, delta.z), collisionRadius);
            if (IsPositionWalkable(zCandidate, collisionRadius, includeEmpty))
            {
                resolved.z = zCandidate.z;
            }

            return resolved;
        }

        bool IsPositionWalkable(Vector3 position, float collisionRadius, bool includeEmpty)
        {
            if (Mathf.Abs(position.x) > TownHalfExtent - collisionRadius || Mathf.Abs(position.z) > TownHalfExtent - collisionRadius)
            {
                return false;
            }

            var samples = new[]
            {
                new Vector2(position.x, position.z),
                new Vector2(position.x + collisionRadius, position.z),
                new Vector2(position.x - collisionRadius, position.z),
                new Vector2(position.x, position.z + collisionRadius),
                new Vector2(position.x, position.z - collisionRadius),
                new Vector2(position.x + collisionRadius * 0.7f, position.z + collisionRadius * 0.7f),
                new Vector2(position.x - collisionRadius * 0.7f, position.z - collisionRadius * 0.7f),
                new Vector2(position.x + collisionRadius * 0.7f, position.z - collisionRadius * 0.7f),
                new Vector2(position.x - collisionRadius * 0.7f, position.z + collisionRadius * 0.7f)
            };

            for (var index = 0; index < samples.Length; index++)
            {
                if (!TryWorldToCell(samples[index], out var cell) || !_villageGrid.IsWalkable(cell, includeEmpty))
                {
                    return false;
                }
            }

            return true;
        }

        void UpdateCamera()
        {
            if (_player == null)
            {
                return;
            }

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
            if (_player == null)
            {
                return;
            }

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
                    _currentDoor = null;
                    return;
                }
            }

            _currentTarget = InteractionTarget.None;
            _currentVillager = null;
            _currentDoor = null;
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

            for (var doorIndex = 0; doorIndex < _doors.Count; doorIndex++)
            {
                var door = _doors[doorIndex];
                var doorDistance = Vector3.Distance(playerPosition, door.InteractionPoint);
                if (doorDistance <= bestDistance)
                {
                    _currentTarget = InteractionTarget.Door;
                    _currentVillager = null;
                    _currentDoor = door;
                    bestDistance = doorDistance;
                }
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
                    if (_currentDoor != null)
                    {
                        ToggleDoor(_currentDoor);
                    }

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

            var lineCount = _database.GetDialogueLineCount(villager.NpcId, villager.DialogueSetIndex);
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
            villager.DialogueSetIndex = (villager.DialogueSetIndex + 1) % Mathf.Max(1, _database.GetDialogueSetCount(villager.NpcId));
            _activeDialogueVillager = null;
            RefreshLocalizedTexts();
        }

        void ToggleDoor(DoorInstance door)
        {
            if (door.IsOpen)
            {
                if (!CanCloseDoor(door))
                {
                    return;
                }
            }

            door.IsOpen = !door.IsOpen;
            _villageGrid.TrySetDoorState(door.Cell, door.IsOpen);

            for (var index = 0; index < _villagers.Count; index++)
            {
                _villagers[index].NextPathRefreshTime = 0f;
            }
        }

        bool CanCloseDoor(DoorInstance door)
        {
            var threshold = WorldCellSize * 0.9f;
            var doorCenter = CellToWorld(door.Cell);
            if (Vector2.Distance(
                    new Vector2(_player.transform.position.x, _player.transform.position.z),
                    new Vector2(doorCenter.x, doorCenter.z)) <= threshold)
            {
                return false;
            }

            for (var index = 0; index < _villagers.Count; index++)
            {
                var villager = _villagers[index];
                if (Vector2.Distance(
                        new Vector2(villager.Transform.position.x, villager.Transform.position.z),
                        new Vector2(doorCenter.x, doorCenter.z)) <= threshold)
                {
                    return false;
                }
            }

            return true;
        }

        void UpdateDoorVisual()
        {
            for (var index = 0; index < _doors.Count; index++)
            {
                var door = _doors[index];
                var targetYaw = door.ClosedYaw + (door.IsOpen ? door.OpenDeltaYaw : 0f);
                door.CurrentYaw = targetYaw;
                door.Pivot.localRotation = Quaternion.Euler(0f, door.CurrentYaw, 0f);
            }
        }

        void UpdateSpeechBubble()
        {
            if (_bubbleRect == null)
            {
                return;
            }

            if (!_dialogueActive || _activeDialogueVillager == null)
            {
                _bubbleRect.gameObject.SetActive(false);
                return;
            }

            var line = _database.GetDialogueLine(_activeDialogueVillager.NpcId, _activeDialogueVillager.DialogueSetIndex, _dialogueLineIndex);
            if (line == null)
            {
                _bubbleRect.gameObject.SetActive(false);
                return;
            }

            _bubbleSpeakerText.text =
                line.speaker.Equals("npc", StringComparison.OrdinalIgnoreCase)
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
                    var lineCount = _database.GetDialogueLineCount(promptVillager.NpcId, promptVillager.DialogueSetIndex);
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
                    if (_currentDoor == null)
                    {
                        _promptText.transform.parent.gameObject.SetActive(false);
                        return;
                    }

                    key = _currentDoor.IsOpen ? "interaction.closeDoor" : "interaction.openDoor";
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

        void UpdateVillagers()
        {
            var time = Time.time;
            for (var index = 0; index < _villagers.Count; index++)
            {
                UpdateVillager(_villagers[index], time);
            }
        }

        void UpdateVillager(VillagerInstance villager, float time)
        {
            var currentGroundPosition = villager.Transform.position;
            currentGroundPosition.y = villager.GroundY;
            villager.CurrentCell = WorldToCell(currentGroundPosition);

            var moving = false;
            if (!_dialogueActive || _activeDialogueVillager != villager)
            {
                if (villager.WaitUntilTime <= time && (villager.Path.Count == 0 || villager.PathIndex >= villager.Path.Count || villager.NextPathRefreshTime <= time))
                {
                    TryAssignPatrolPath(villager, time);
                }

                if (villager.PathIndex < villager.Path.Count)
                {
                    moving = MoveVillagerAlongPath(villager, currentGroundPosition, time);
                }
            }

            var bob = Mathf.Sin(time * villager.BobSpeed + villager.PhaseOffset) * villager.BobAmplitude;
            var sway = moving
                ? Mathf.Sin(time * villager.SwaySpeed + villager.PhaseOffset) * (villager.SwayAngle * 0.32f)
                : Mathf.Sin(time * villager.SwaySpeed + villager.PhaseOffset) * villager.SwayAngle;

            var finalPosition = villager.Transform.position;
            finalPosition.y = villager.GroundY + bob;
            villager.Transform.position = finalPosition;
            villager.Transform.rotation = Quaternion.Euler(0f, villager.FacingYaw + sway, 0f);
        }

        bool MoveVillagerAlongPath(VillagerInstance villager, Vector3 currentGroundPosition, float time)
        {
            if (villager.PathIndex >= villager.Path.Count)
            {
                return false;
            }

            var nextCell = villager.Path[villager.PathIndex];
            if (!_villageGrid.IsWalkable(nextCell))
            {
                villager.Path.Clear();
                villager.PathIndex = 0;
                villager.NextPathRefreshTime = time + Range(0.35f, 0.75f);
                return false;
            }

            var nextWorld = CellToWorld(nextCell) + new Vector3(0f, villager.GroundY, 0f);
            var desired = Vector3.MoveTowards(currentGroundPosition, nextWorld, VillagerMoveSpeed * Time.deltaTime);
            desired = MoveWithWorldCollision(currentGroundPosition, desired - currentGroundPosition, VillagerCollisionRadius, false);
            desired = ResolveDynamicBlocking(villager, currentGroundPosition, desired, VillagerCollisionRadius);

            if (!IsPositionWalkable(desired, VillagerCollisionRadius, false))
            {
                villager.Path.Clear();
                villager.PathIndex = 0;
                villager.NextPathRefreshTime = time + Range(0.35f, 0.75f);
                return false;
            }

            var movement = desired - currentGroundPosition;
            movement.y = 0f;
            if (movement.sqrMagnitude > 0.0001f)
            {
                villager.FacingYaw = Mathf.LerpAngle(villager.FacingYaw, Quaternion.LookRotation(movement.normalized, Vector3.up).eulerAngles.y, 14f * Time.deltaTime);
            }

            villager.Transform.position = new Vector3(desired.x, villager.Transform.position.y, desired.z);

            var remaining = new Vector2(nextWorld.x - desired.x, nextWorld.z - desired.z);
            if (remaining.sqrMagnitude <= 0.03f)
            {
                villager.Transform.position = new Vector3(nextWorld.x, villager.Transform.position.y, nextWorld.z);
                villager.CurrentCell = nextCell;
                villager.PathIndex++;

                if (villager.PathIndex >= villager.Path.Count)
                {
                    villager.Path.Clear();
                    villager.PathIndex = 0;
                    villager.WaitUntilTime = time + Range(0.8f, 2.1f);
                    villager.NextPathRefreshTime = villager.WaitUntilTime;
                }
            }

            return true;
        }

        void TryAssignPatrolPath(VillagerInstance villager, float time)
        {
            villager.Path.Clear();
            villager.PathIndex = 0;

            var start = WorldToCell(villager.Transform.position);
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var destination = ChoosePatrolDestination(start, villager.HomeCell);
                if (destination == start)
                {
                    continue;
                }

                if (_villageGrid.TryFindPath(start, destination, villager.Path, false) && villager.Path.Count > 1)
                {
                    villager.PathIndex = villager.Path[0] == start ? 1 : 0;
                    villager.WaitUntilTime = 0f;
                    villager.NextPathRefreshTime = time + Range(5f, 9f);
                    return;
                }
            }

            villager.Path.Clear();
            villager.PathIndex = 0;
            villager.WaitUntilTime = time + Range(0.7f, 1.8f);
            villager.NextPathRefreshTime = villager.WaitUntilTime;
        }

        Vector2Int ChoosePatrolDestination(Vector2Int start, Vector2Int homeCell)
        {
            if (_patrolCells.Count == 0)
            {
                return homeCell;
            }

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = _patrolCells[_worldRandom.Next(0, _patrolCells.Count)];
                var distance = Mathf.Abs(candidate.x - homeCell.x) + Mathf.Abs(candidate.y - homeCell.y);
                if (candidate != start && distance >= 2 && distance <= 18)
                {
                    return candidate;
                }
            }

            return homeCell;
        }

        Vector3 ResolveDynamicBlocking(VillagerInstance? self, Vector3 currentPosition, Vector3 desiredPosition, float collisionRadius)
        {
            var resolved = new Vector2(desiredPosition.x, desiredPosition.z);
            var current = new Vector2(currentPosition.x, currentPosition.z);

            if (self != null && _player != null)
            {
                resolved = PlanarPersonCollision.Resolve(
                    current,
                    resolved,
                    new Vector2(_player.transform.position.x, _player.transform.position.z),
                    collisionRadius + PlayerCollisionRadius);
            }

            for (var index = 0; index < _villagers.Count; index++)
            {
                var other = _villagers[index];
                if (ReferenceEquals(other, self))
                {
                    continue;
                }

                resolved = PlanarPersonCollision.Resolve(
                    current,
                    resolved,
                    new Vector2(other.Transform.position.x, other.Transform.position.z),
                    collisionRadius + VillagerCollisionRadius);
            }

            desiredPosition.x = resolved.x;
            desiredPosition.z = resolved.y;
            return desiredPosition;
        }

        void CollectPatrolCells()
        {
            var lookup = new HashSet<Vector2Int>();

            for (var roadIndex = 0; roadIndex < _layout.roads.Length; roadIndex++)
            {
                var road = _layout.roads[roadIndex];
                for (var cellIndex = 0; cellIndex < road.cells.Length; cellIndex++)
                {
                    RegisterPatrolCell(lookup, road.cells[cellIndex]);
                }
            }

            for (var y = -3; y <= 3; y++)
            {
                for (var x = -3; x <= 3; x++)
                {
                    RegisterPatrolCell(lookup, _layout.plazaCenter + new Vector2Int(x, y));
                }
            }

            for (var spawnIndex = 0; spawnIndex < _layout.npcSpawnPoints.Length; spawnIndex++)
            {
                RegisterPatrolCell(lookup, _layout.npcSpawnPoints[spawnIndex].cell);
            }
        }

        void RegisterPatrolCell(HashSet<Vector2Int> lookup, Vector2Int cell)
        {
            if (!_villageGrid.IsWalkable(cell, false) || !lookup.Add(cell))
            {
                return;
            }

            _patrolCells.Add(cell);
        }

        void CreateFoliage(VillageFoliagePlacement foliage, int index)
        {
            var world = CellToWorld(foliage.cell);
            var yaw = Range(0f, 360f);

            switch (foliage.kind)
            {
                case VillageFoliageKind.Tree:
                {
                    var scale = 2.2f + foliage.scale * 0.6f;
                    var tree = VoxelEnvironmentFactory.CreateTree(
                        "Tree_" + index,
                        world + new Vector3(0f, scale * 0.65f, 0f),
                        new Vector3(scale, scale * 1.35f, scale),
                        yaw,
                        new Color(0.47f, 0.31f, 0.16f),
                        new Color(0.27f + 0.03f * foliage.scale, 0.58f, 0.3f));
                    tree.Root.transform.SetParent(_worldRoot, true);
                    break;
                }

                case VillageFoliageKind.Shrub:
                {
                    var shrub = VoxelEnvironmentFactory.CreateShrub(
                        "Shrub_" + index,
                        world + new Vector3(0f, 0.55f, 0f),
                        new Vector3(1.2f + foliage.scale * 0.45f, 1f + foliage.scale * 0.25f, 1.2f + foliage.scale * 0.45f),
                        yaw,
                        new Color(0.31f, 0.58f, 0.28f));
                    shrub.Root.transform.SetParent(_worldRoot, true);
                    break;
                }

                case VillageFoliageKind.Flower:
                {
                    var flower = VoxelEnvironmentFactory.CreateFlower(
                        "Flower_" + index,
                        world + new Vector3(0f, 0.5f, 0f),
                        new Vector3(0.72f + foliage.scale * 0.16f, 1.2f + foliage.scale * 0.14f, 0.72f + foliage.scale * 0.16f),
                        yaw,
                        GetFlowerColor(index));
                    flower.Root.transform.SetParent(_worldRoot, true);
                    break;
                }

                case VillageFoliageKind.Rock:
                {
                    var rock = VoxelEnvironmentFactory.CreateShrub(
                        "Rock_" + index,
                        world + new Vector3(0f, 0.42f, 0f),
                        new Vector3(1f + foliage.scale * 0.22f, 0.8f + foliage.scale * 0.12f, 0.96f + foliage.scale * 0.22f),
                        yaw,
                        new Color(0.57f, 0.59f, 0.63f));
                    rock.Root.transform.SetParent(_worldRoot, true);
                    break;
                }
            }
        }

        void CreateDoor(VillageDoorLayout layoutDoor)
        {
            var facing = DirectionToWorld(layoutDoor.facing);
            var closedYaw = YawFromDirection(layoutDoor.facing);
            var openDelta = layoutDoor.facing.x <= 0 && layoutDoor.facing.y >= 0 ? -DoorOpenAngle : DoorOpenAngle;
            var doorSize = new Vector3(WorldCellSize * 0.64f, 2.45f, WorldCellSize * 0.12f);
            var right = Quaternion.Euler(0f, closedYaw, 0f) * Vector3.right;
            var hingeBase = CellToWorld(layoutDoor.cell) - facing * (WorldCellSize * 0.16f) - (right * (doorSize.x * 0.5f));

            var pivot = new GameObject("DoorPivot_" + layoutDoor.id).transform;
            pivot.SetParent(_worldRoot, false);
            pivot.position = hingeBase;

            var door = VoxelEnvironmentFactory.CreateDoor(
                "Door_" + layoutDoor.id,
                Vector3.zero,
                doorSize,
                0f,
                new Color(0.53f, 0.31f, 0.17f));
            door.Root.transform.SetParent(pivot, false);
            door.Root.transform.localPosition = new Vector3(doorSize.x * 0.5f, doorSize.y * 0.5f, 0f);

            var interactionPoint = CellToWorld(layoutDoor.cell) + new Vector3(0f, 1.1f, 0f) + facing * (WorldCellSize * 0.62f);
            var instance = new DoorInstance(layoutDoor.id, layoutDoor.cell, pivot, interactionPoint, closedYaw, openDelta, layoutDoor.startsOpen);
            if (layoutDoor.startsOpen)
            {
                instance.CurrentYaw = closedYaw + openDelta;
                pivot.localRotation = Quaternion.Euler(0f, instance.CurrentYaw, 0f);
            }

            _doors.Add(instance);
        }

        bool TryFindPondRect(out RectInt pondRect)
        {
            var size = new Vector2Int(4, 3);
            var candidates = new[]
            {
                new RectInt(6, 6, size.x, size.y),
                new RectInt(WorldGridSize - 10, 7, size.x, size.y),
                new RectInt(7, WorldGridSize - 10, size.x, size.y),
                new RectInt(WorldGridSize - 10, WorldGridSize - 10, size.x, size.y)
            };

            for (var index = 0; index < candidates.Length; index++)
            {
                if (_villageGrid.IsRectClear(candidates[index]))
                {
                    pondRect = candidates[index];
                    return true;
                }
            }

            pondRect = default;
            return false;
        }

        VillageDoorLayout? FindDoorForBuilding(string buildingId)
        {
            for (var index = 0; index < _layout.doors.Length; index++)
            {
                var door = _layout.doors[index];
                if (string.Equals(door.buildingId, buildingId, StringComparison.Ordinal))
                {
                    return door;
                }
            }

            return null;
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
            VoxelCharacterAccessoryType accessoryType,
            Vector2Int homeCell)
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
            _villagers.Add(new VillagerInstance(
                npcId,
                villager.transform,
                homeCell,
                position.y,
                yaw,
                bobAmplitude,
                bobSpeed,
                swayAngle,
                swaySpeed,
                phaseOffset,
                character.HeadOffset));
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

        static float YawFromDirection(Vector2Int direction)
        {
            if (direction == Vector2Int.up)
            {
                return 0f;
            }

            if (direction == Vector2Int.right)
            {
                return 90f;
            }

            if (direction == Vector2Int.left)
            {
                return -90f;
            }

            return 180f;
        }

        static Vector3 DirectionToWorld(Vector2Int direction)
        {
            return new Vector3(direction.x, 0f, direction.y);
        }

        Vector3 CellRectCenterToWorld(Vector2Int origin, Vector2Int size)
        {
            var x = (origin.x + size.x * 0.5f) * WorldCellSize - TownFootprint * 0.5f;
            var z = (origin.y + size.y * 0.5f) * WorldCellSize - TownFootprint * 0.5f;
            return new Vector3(x, 0f, z);
        }

        Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(
                (cell.x + 0.5f) * WorldCellSize - TownFootprint * 0.5f,
                0f,
                (cell.y + 0.5f) * WorldCellSize - TownFootprint * 0.5f);
        }

        Vector2Int WorldToCell(Vector3 position)
        {
            var x = Mathf.Clamp(Mathf.FloorToInt((position.x + TownFootprint * 0.5f) / WorldCellSize), 0, WorldGridSize - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt((position.z + TownFootprint * 0.5f) / WorldCellSize), 0, WorldGridSize - 1);
            return new Vector2Int(x, y);
        }

        bool TryWorldToCell(Vector2 position, out Vector2Int cell)
        {
            var x = Mathf.FloorToInt((position.x + TownFootprint * 0.5f) / WorldCellSize);
            var y = Mathf.FloorToInt((position.y + TownFootprint * 0.5f) / WorldCellSize);
            if (x < 0 || x >= WorldGridSize || y < 0 || y >= WorldGridSize)
            {
                cell = default;
                return false;
            }

            cell = new Vector2Int(x, y);
            return true;
        }

        Vector3 ClampWorldPosition(Vector3 position, float radius)
        {
            position.x = Mathf.Clamp(position.x, -TownHalfExtent + radius, TownHalfExtent - radius);
            position.z = Mathf.Clamp(position.z, -TownHalfExtent + radius, TownHalfExtent - radius);
            return position;
        }

        float Range(float minInclusive, float maxInclusive)
        {
            return Mathf.Lerp(minInclusive, maxInclusive, (float)_worldRandom.NextDouble());
        }

        static Color GetFlowerColor(int index)
        {
            switch (index % 4)
            {
                case 0:
                    return new Color(0.95f, 0.45f, 0.59f);
                case 1:
                    return new Color(0.98f, 0.82f, 0.28f);
                case 2:
                    return new Color(0.84f, 0.54f, 0.96f);
                default:
                    return new Color(0.98f, 0.71f, 0.44f);
            }
        }

        static BuildingPalette GetBuildingPalette(int index)
        {
            switch (index % 6)
            {
                case 0:
                    return new BuildingPalette(new Color(0.85f, 0.73f, 0.55f), new Color(0.59f, 0.25f, 0.21f), new Color(0.44f, 0.31f, 0.22f));
                case 1:
                    return new BuildingPalette(new Color(0.77f, 0.81f, 0.63f), new Color(0.46f, 0.31f, 0.2f), new Color(0.35f, 0.27f, 0.19f));
                case 2:
                    return new BuildingPalette(new Color(0.79f, 0.67f, 0.76f), new Color(0.41f, 0.22f, 0.28f), new Color(0.53f, 0.41f, 0.29f));
                case 3:
                    return new BuildingPalette(new Color(0.7f, 0.8f, 0.84f), new Color(0.36f, 0.44f, 0.63f), new Color(0.35f, 0.3f, 0.24f));
                case 4:
                    return new BuildingPalette(new Color(0.87f, 0.74f, 0.66f), new Color(0.56f, 0.37f, 0.21f), new Color(0.45f, 0.33f, 0.28f));
                default:
                    return new BuildingPalette(new Color(0.75f, 0.82f, 0.74f), new Color(0.31f, 0.43f, 0.24f), new Color(0.34f, 0.29f, 0.21f));
            }
        }

        readonly struct BuildingPalette
        {
            public BuildingPalette(Color wall, Color roof, Color trim)
            {
                Wall = wall;
                Roof = roof;
                Trim = trim;
            }

            public Color Wall { get; }

            public Color Roof { get; }

            public Color Trim { get; }
        }

        readonly struct VillagerStyle
        {
            public VillagerStyle(string npcId, Color color, VoxelCharacterAccessoryType accessoryType, float heightScale)
            {
                NpcId = npcId;
                Color = color;
                AccessoryType = accessoryType;
                HeightScale = heightScale;
            }

            public string NpcId { get; }

            public Color Color { get; }

            public VoxelCharacterAccessoryType AccessoryType { get; }

            public float HeightScale { get; }
        }

        sealed class DoorInstance
        {
            public DoorInstance(string doorId, Vector2Int cell, Transform pivot, Vector3 interactionPoint, float closedYaw, float openDeltaYaw, bool startsOpen)
            {
                DoorId = doorId;
                Cell = cell;
                Pivot = pivot;
                InteractionPoint = interactionPoint;
                ClosedYaw = closedYaw;
                OpenDeltaYaw = openDeltaYaw;
                CurrentYaw = startsOpen ? closedYaw + openDeltaYaw : closedYaw;
                IsOpen = startsOpen;
            }

            public string DoorId { get; }

            public Vector2Int Cell { get; }

            public Transform Pivot { get; }

            public Vector3 InteractionPoint { get; }

            public float ClosedYaw { get; }

            public float OpenDeltaYaw { get; }

            public float CurrentYaw { get; set; }

            public bool IsOpen { get; set; }
        }

        sealed class VillagerInstance
        {
            public VillagerInstance(
                string npcId,
                Transform transform,
                Vector2Int homeCell,
                float groundY,
                float facingYaw,
                float bobAmplitude,
                float bobSpeed,
                float swayAngle,
                float swaySpeed,
                float phaseOffset,
                float headOffset)
            {
                NpcId = npcId;
                Transform = transform;
                HomeCell = homeCell;
                CurrentCell = homeCell;
                GroundY = groundY;
                FacingYaw = facingYaw;
                BobAmplitude = bobAmplitude;
                BobSpeed = bobSpeed;
                SwayAngle = swayAngle;
                SwaySpeed = swaySpeed;
                PhaseOffset = phaseOffset;
                HeadOffset = headOffset;
            }

            public string NpcId { get; }

            public Transform Transform { get; }

            public Vector2Int HomeCell { get; }

            public Vector2Int CurrentCell { get; set; }

            public float GroundY { get; }

            public float FacingYaw { get; set; }

            public float BobAmplitude { get; }

            public float BobSpeed { get; }

            public float SwayAngle { get; }

            public float SwaySpeed { get; }

            public float PhaseOffset { get; }

            public float HeadOffset { get; }

            public List<Vector2Int> Path { get; } = new List<Vector2Int>();

            public int PathIndex { get; set; }

            public float WaitUntilTime { get; set; }

            public float NextPathRefreshTime { get; set; }

            public int DialogueSetIndex { get; set; }
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
