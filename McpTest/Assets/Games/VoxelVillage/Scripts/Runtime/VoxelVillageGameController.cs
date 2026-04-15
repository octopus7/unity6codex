#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        const float TopButtonInset = 16f;
        const float TopButtonGap = 12f;
        const float TopButtonHeight = 42f;
        const float TopButtonTextHorizontalPadding = 12f;
        const float LanguageButtonWidth = 196f;
        const float ControlsButtonWidth = 124f;
        const float CoinHoverHeight = 1.2f;
        const float CoinCounterWidth = 128f;
        const float CoinCounterHeight = 48f;
        const float GrassScaleMultiplier = 0.5f;
        const float DoorOpenAngle = 108f;
        const float DoorOpeningWidthRatio = 2.5f / 28f;
        const float DoorOpeningHeightRatio = 10f / 24f;
        const float DoorLeafClearance = 0.92f;
        const float DoorWallDepthRatio = 2f / 26f;
        const float DoorInteractionForwardOffset = 0.62f;
        const float RoofRevealFeather = 1.05f;
        const float TrafficSignalGreenDuration = 9f;
        const float TrafficSignalYellowDuration = 1.6f;
        const float TrafficSignalWidth = WorldCellSize * 0.58f;
        const float TrafficSignalHeight = 3.5f;
        const float TrafficSignalDepth = WorldCellSize * 0.58f;
        const float TrafficSignalLampSize = 0.34f;
        const float TrafficSignalLampDepth = 0.12f;
        const float TrafficSignalLampSpacing = 0.4f;
        const float TrafficSignalLampCenterYOffset = 0.92f;
        const float TrafficSignalLampForwardOffset = 0.56f;
        const float TrafficSignalLampActiveEmission = 5.8f;
        const float TrafficSignalLampInactiveEmission = 0.05f;
        const int CoinSpawnCount = 12;
        const int WorldGridSize = 72;
        const float WorldCellSize = TownFootprint / WorldGridSize;
        const string CoinPrefabResourcePath = "VoxelVillage/Pickups/VV_Coin";

        static readonly int RevealEnabledShaderId = Shader.PropertyToID("_RevealEnabled");
        static readonly int RevealHeightShaderId = Shader.PropertyToID("_RevealHeight");
        static readonly int RevealFeatherShaderId = Shader.PropertyToID("_RevealFeather");

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

        enum TrafficSignalCyclePhase
        {
            NorthSouthGreen,
            NorthSouthYellow,
            EastWestGreen,
            EastWestYellow
        }

        enum TrafficSignalLamp
        {
            Red,
            Yellow,
            Green
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
        Text _coinCountText = null!;
        Text _languageButtonText = null!;
        Text _controlsButtonText = null!;
        RectTransform _bubbleRect = null!;
        Text _bubbleSpeakerText = null!;
        Text _bubbleContentText = null!;

        Transform _worldRoot = null!;
        GameObject _player = null!;
        VillageLayoutData _layout = null!;
        VillageGrid _villageGrid = null!;
        readonly List<HouseInstance> _houses = new List<HouseInstance>();
        readonly List<VillagerInstance> _villagers = new List<VillagerInstance>();
        readonly List<DoorInstance> _doors = new List<DoorInstance>();
        readonly List<TrafficSignalInstance> _trafficSignals = new List<TrafficSignalInstance>();
        readonly List<AmbientSpiderWalkerController> _ambientSpiders = new List<AmbientSpiderWalkerController>();
        readonly List<CoinInstance> _coins = new List<CoinInstance>();
        System.Random _worldRandom = new System.Random();
        Material? _trafficSignalLampMaterial;
        GameObject? _coinPrefab;

        InteractionTarget _currentTarget;
        VillagerInstance? _currentVillager;
        VillagerInstance? _activeDialogueVillager;
        DoorInstance? _currentDoor;
        bool _dialogueActive;
        int _dialogueLineIndex;
        bool _helpVisible;
        int _coinsCollected;
        int _worldSeed;
        TrafficSignalCyclePhase _trafficSignalPhase;
        float _trafficSignalPhaseElapsed;

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
            UpdateCoins();
            UpdateBuildingRoofReveal();
            UpdateVillagers();
            UpdateCamera();
            UpdateInteractionTarget();
            HandleInteractionInput();
            UpdateDoorVisual();
            UpdateTrafficSignals();
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
            _mainCamera.allowHDR = true;
            _mainCamera.nearClipPlane = 0.03f;
            _mainCamera.farClipPlane = 400f;

            var additionalCameraData = _mainCamera.GetUniversalAdditionalCameraData();
            additionalCameraData.renderPostProcessing = true;
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

            _houses.Clear();
            _villagers.Clear();
            _doors.Clear();
            _trafficSignals.Clear();
            _ambientSpiders.Clear();
            _coins.Clear();
            _currentTarget = InteractionTarget.None;
            _currentVillager = null;
            _currentDoor = null;
            _activeDialogueVillager = null;
            _dialogueActive = false;
            _dialogueLineIndex = 0;
            _trafficSignalPhase = TrafficSignalCyclePhase.NorthSouthGreen;
            _trafficSignalPhaseElapsed = 0f;
            _coinsCollected = 0;

            _worldSeed = Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7fffffff);
            _worldRandom = new System.Random(_worldSeed);

            _worldRoot = new GameObject("VoxelVillageWorld").transform;
            _worldRoot.SetParent(transform, false);

            _layout = ProceduralVillageGenerator.Generate(_worldSeed, WorldGridSize);
            _villageGrid = VillageGrid.FromLayout(_layout);

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
            BuildGrassPatches();
            BuildFountain();
            BuildPond(waterMaterial);
            CreatePlayer(CellToWorld(_layout.plazaCenter + new Vector2Int(0, -6)) + new Vector3(0f, 0.9f, 0f), new Color(0.16f, 0.41f, 0.95f));
            SpawnCoins();
            SpawnVillagersFromLayout();
            SpawnAmbientSpiderWalker();
            EnsureGlobalIlluminationProbe();
            RefreshCoinCountText();
        }

        void SpawnCoins()
        {
            _coins.Clear();

            var coinPrefab = LoadCoinPrefab();
            if (coinPrefab == null || _player == null)
            {
                return;
            }

            var playerCell = WorldToCell(_player.transform.position);
            var spawnCells = BuildCoinSpawnCells(_layout, _villageGrid, playerCell, CoinSpawnCount);
            for (var index = 0; index < spawnCells.Count; index++)
            {
                var cell = spawnCells[index];
                var position = CellToWorld(cell) + new Vector3(0f, CoinHoverHeight, 0f);
                var coinObject = Instantiate(coinPrefab, position, Quaternion.identity, _worldRoot);
                coinObject.name = $"Coin_{index + 1:00}";

                var pickup = coinObject.GetComponent<VoxelVillageCoinPickup>();
                if (pickup != null)
                {
                    pickup.SetBaseHeight(position.y);
                }

                _coins.Add(new CoinInstance(
                    coinObject.transform,
                    pickup != null ? pickup.PickupRadius : 0.95f));
            }
        }

        GameObject? LoadCoinPrefab()
        {
            if (_coinPrefab != null)
            {
                return _coinPrefab;
            }

            _coinPrefab = Resources.Load<GameObject>(CoinPrefabResourcePath);
            if (_coinPrefab == null)
            {
                Debug.LogWarning("Voxel Village coin prefab is missing at Resources/" + CoinPrefabResourcePath + ".prefab");
            }

            return _coinPrefab;
        }

        static List<Vector2Int> BuildCoinSpawnCells(
            VillageLayoutData layout,
            VillageGrid grid,
            Vector2Int playerCell,
            int maxCount)
        {
            var selected = new List<Vector2Int>(Mathf.Max(0, maxCount));
            if (maxCount <= 0)
            {
                return selected;
            }

            var candidates = new List<Vector2Int>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var kind = grid.GetCellKind(cell);
                    if (kind == VillageCellKind.Road || kind == VillageCellKind.Plaza)
                    {
                        candidates.Add(cell);
                    }
                }
            }

            candidates.Sort((left, right) => GetCoinPlacementScore(left, layout.seed).CompareTo(GetCoinPlacementScore(right, layout.seed)));

            var spacingPasses = new[] { 6, 4, 2 };
            for (var passIndex = 0; passIndex < spacingPasses.Length && selected.Count < maxCount; passIndex++)
            {
                var minimumCoinSpacing = spacingPasses[passIndex];
                for (var index = 0; index < candidates.Count && selected.Count < maxCount; index++)
                {
                    var candidate = candidates[index];
                    if (selected.Contains(candidate))
                    {
                        continue;
                    }

                    if (!IsCoinSpawnCandidate(candidate, selected, layout, playerCell, minimumCoinSpacing))
                    {
                        continue;
                    }

                    selected.Add(candidate);
                }
            }

            return selected;
        }

        static bool IsCoinSpawnCandidate(
            Vector2Int candidate,
            List<Vector2Int> selected,
            VillageLayoutData layout,
            Vector2Int playerCell,
            int minimumCoinSpacing)
        {
            if (ManhattanDistance(candidate, playerCell) <= 3)
            {
                return false;
            }

            for (var index = 0; index < layout.npcSpawnPoints.Length; index++)
            {
                if (ManhattanDistance(candidate, layout.npcSpawnPoints[index].cell) <= 2)
                {
                    return false;
                }
            }

            for (var index = 0; index < layout.doors.Length; index++)
            {
                if (ManhattanDistance(candidate, layout.doors[index].cell) <= 1)
                {
                    return false;
                }
            }

            for (var index = 0; index < layout.trafficSignals.Length; index++)
            {
                if (ManhattanDistance(candidate, layout.trafficSignals[index].cell) <= 1)
                {
                    return false;
                }
            }

            for (var index = 0; index < selected.Count; index++)
            {
                if (ManhattanDistance(candidate, selected[index]) < minimumCoinSpacing)
                {
                    return false;
                }
            }

            return true;
        }

        static int GetCoinPlacementScore(Vector2Int cell, int seed)
        {
            unchecked
            {
                var hash = (uint)seed;
                hash ^= (uint)(cell.x + 1) * 73856093u;
                hash ^= (uint)(cell.y + 1) * 19349663u;
                hash ^= hash >> 13;
                hash *= 83492791u;
                hash ^= hash >> 16;
                return (int)(hash & 0x7fffffff);
            }
        }

        static int ManhattanDistance(Vector2Int left, Vector2Int right)
        {
            return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
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

            CreateCoinCounter(font);
            CreateLanguageButton(font);
            CreateControlsButton(font);
            CreateSpeechBubble(font);

            _helpVisible = false;
            _helpPanel.SetActive(false);
        }

        void CreateCoinCounter(Font font)
        {
            _coinCountText = CreatePanelText(
                "CoinCounterPanel",
                new Vector2(16f, -16f),
                new Vector2(CoinCounterWidth, CoinCounterHeight),
                font,
                24,
                TextAnchor.MiddleCenter,
                new Color(0.98f, 0.84f, 0.24f, 0.92f),
                new Color(0.2f, 0.14f, 0.04f));
            _coinCountText.fontStyle = FontStyle.Bold;
            RefreshCoinCountText();
        }

        void CreateLanguageButton(Font font)
        {
            var buttonObject = new GameObject("LanguageButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_canvas.transform, false);

            var rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-TopButtonInset, -TopButtonInset);
            rectTransform.sizeDelta = new Vector2(LanguageButtonWidth, TopButtonHeight);

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
            textRect.offsetMin = new Vector2(TopButtonTextHorizontalPadding, 0f);
            textRect.offsetMax = new Vector2(-TopButtonTextHorizontalPadding, 0f);

            _languageButtonText = textObject.GetComponent<Text>();
            _languageButtonText.font = font;
            _languageButtonText.fontSize = 17;
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
            rectTransform.anchoredPosition = new Vector2(
                -(TopButtonInset + LanguageButtonWidth + TopButtonGap),
                -TopButtonInset);
            rectTransform.sizeDelta = new Vector2(ControlsButtonWidth, TopButtonHeight);

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
            textRect.offsetMin = new Vector2(TopButtonTextHorizontalPadding, 0f);
            textRect.offsetMax = new Vector2(-TopButtonTextHorizontalPadding, 0f);

            _controlsButtonText = textObject.GetComponent<Text>();
            _controlsButtonText.font = font;
            _controlsButtonText.fontSize = 16;
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
                var size = GetHouseSize(building, facing);

                var house = VoxelEnvironmentFactory.CreateHouse(
                    "House_" + building.id,
                    center + new Vector3(0f, size.y * 0.5f, 0f),
                    size,
                    YawFromDirection(facing),
                    palette.Wall,
                    palette.Roof,
                    palette.Trim);
                house.Root.transform.SetParent(_worldRoot, true);

                var houseRenderers = house.Root.GetComponentsInChildren<MeshRenderer>();
                if (houseRenderers.Length == 0)
                {
                    throw new InvalidOperationException("House visual is missing a MeshRenderer.");
                }

                var roofRevealHeight =
                    house.Root.transform.position.y -
                    (house.LocalSize.y * 0.5f) +
                    (house.LocalSize.y * VoxelEnvironmentFactory.HouseRoofRevealNormalizedHeight);
                var houseInstance = new HouseInstance(
                    building.id,
                    GetBuildingInteriorRect(building),
                    houseRenderers,
                    roofRevealHeight,
                    RoofRevealFeather);
                SetHouseRoofRevealState(houseInstance, false, true);
                _houses.Add(houseInstance);
            }

            for (var fenceIndex = 0; fenceIndex < _layout.fences.Length; fenceIndex++)
            {
                CreateFencePath(_layout.fences[fenceIndex], fenceIndex);
            }

            for (var doorIndex = 0; doorIndex < _layout.doors.Length; doorIndex++)
            {
                CreateDoor(_layout.doors[doorIndex]);
            }

            for (var foliageIndex = 0; foliageIndex < _layout.foliage.Length; foliageIndex++)
            {
                CreateFoliage(_layout.foliage[foliageIndex], foliageIndex);
            }

            for (var signalIndex = 0; signalIndex < _layout.trafficSignals.Length; signalIndex++)
            {
                CreateTrafficSignal(_layout.trafficSignals[signalIndex]);
            }
        }

        void BuildGrassPatches()
        {
            var placements = VillageGrassScatterGenerator.Generate(_layout, _villageGrid);
            if (placements.Length == 0)
            {
                return;
            }

            var hasPond = TryFindPondRect(out var pondRect);
            var grassRoot = new GameObject("GrassPatches").transform;
            grassRoot.SetParent(_worldRoot, false);

            for (var index = 0; index < placements.Length; index++)
            {
                if (hasPond && pondRect.Contains(placements[index].Cell))
                {
                    continue;
                }

                CreateGrassPatch(grassRoot, placements[index], index);
            }

            if (grassRoot.childCount == 0)
            {
                Destroy(grassRoot.gameObject);
                return;
            }

            StaticBatchingUtility.Combine(grassRoot.gameObject);
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
                    spawnCell,
                    spawn.patrolCenter,
                    spawn.patrolRadius);
            }
        }

        void SpawnAmbientSpiderWalker()
        {
            if (!TryChooseAmbientSpiderSpawnCell(out var spawnCell))
            {
                return;
            }

            var spawnPosition = CellToWorld(spawnCell);
            var resourcePrefab = Resources.Load<GameObject>("VoxelVillage/Threats/VV_Ambient_SpiderWalker");
            GameObject spiderRoot;

            if (resourcePrefab != null)
            {
                spiderRoot = Instantiate(resourcePrefab, spawnPosition, Quaternion.identity, _worldRoot);
            }
            else
            {
                spiderRoot = VoxelSpiderWalkerFactory.CreateInstance("VV_Ambient_SpiderWalker", spawnPosition).Root;
                spiderRoot.transform.SetParent(_worldRoot, true);
            }

            var spider = VoxelSpiderWalkerFactory.EnsureInstance(spiderRoot).Controller;
            spider.BindNavigation(
                _villageGrid,
                _layout.threatAnchors,
                spawnCell,
                WorldCellSize,
                TownFootprint,
                BuildSpiderAvoidanceTargets());
            _ambientSpiders.Add(spider);
        }

        bool TryChooseAmbientSpiderSpawnCell(out Vector2Int spawnCell)
        {
            for (var index = 0; index < _layout.threatAnchors.Length; index++)
            {
                var anchor = _layout.threatAnchors[(_worldRandom.Next(0, _layout.threatAnchors.Length) + index) % _layout.threatAnchors.Length];
                if (IsValidSpiderSpawnCell(anchor.cell))
                {
                    spawnCell = anchor.cell;
                    return true;
                }
            }

            var fallbackCells = new[]
            {
                _layout.plazaCenter + new Vector2Int(-4, 4),
                _layout.plazaCenter + new Vector2Int(4, 4),
                _layout.plazaCenter + new Vector2Int(-4, -4),
                _layout.plazaCenter + new Vector2Int(4, -4),
                _layout.plazaCenter + new Vector2Int(0, 6),
                _layout.plazaCenter + new Vector2Int(0, -6),
                _layout.plazaCenter + new Vector2Int(-6, 0),
                _layout.plazaCenter + new Vector2Int(6, 0)
            };

            for (var index = 0; index < fallbackCells.Length; index++)
            {
                if (IsValidSpiderSpawnCell(fallbackCells[index]))
                {
                    spawnCell = fallbackCells[index];
                    return true;
                }
            }

            spawnCell = default;
            return false;
        }

        bool IsValidSpiderSpawnCell(Vector2Int cell)
        {
            if (!_villageGrid.IsWalkable(cell, false, MovementFootprint.SqueezedSpider1x1))
            {
                return false;
            }

            if (_player != null && Mathf.Abs(WorldToCell(_player.transform.position).x - cell.x) + Mathf.Abs(WorldToCell(_player.transform.position).y - cell.y) < 8)
            {
                return false;
            }

            for (var index = 0; index < _layout.npcSpawnPoints.Length; index++)
            {
                if (Mathf.Abs(_layout.npcSpawnPoints[index].cell.x - cell.x) + Mathf.Abs(_layout.npcSpawnPoints[index].cell.y - cell.y) < 3)
                {
                    return false;
                }
            }

            for (var index = 0; index < _layout.doors.Length; index++)
            {
                if (Mathf.Abs(_layout.doors[index].cell.x - cell.x) + Mathf.Abs(_layout.doors[index].cell.y - cell.y) < 3)
                {
                    return false;
                }
            }

            for (var index = 0; index < _layout.trafficSignals.Length; index++)
            {
                if (_layout.trafficSignals[index].cell == cell)
                {
                    return false;
                }
            }

            return true;
        }

        Transform[] BuildSpiderAvoidanceTargets()
        {
            var targets = new List<Transform>(_villagers.Count + 1);
            if (_player != null)
            {
                targets.Add(_player.transform);
            }

            for (var index = 0; index < _villagers.Count; index++)
            {
                targets.Add(_villagers[index].Transform);
            }

            return targets.ToArray();
        }

        void CreateInvaderPrototype()
        {
            var tracker = MukhaengTrackerFactory.CreateTracker(
                "ThreatPrototype_MukhaengTracker",
                GetInvaderPrototypeSpawnPosition());
            tracker.Root.transform.SetParent(_worldRoot, true);
            tracker.Controller.SetHomePosition(tracker.Root.transform.position);
            if (_player != null)
            {
                tracker.Controller.SetTarget(_player.transform);
            }
        }

        Vector3 GetInvaderPrototypeSpawnPosition()
        {
            if (TryFindPondRect(out var pondRect))
            {
                var centerY = pondRect.yMin + (pondRect.height / 2);
                var spawnCell = pondRect.xMin < WorldGridSize * 0.5f
                    ? new Vector2Int(Mathf.Min(WorldGridSize - 1, pondRect.xMax + 2), centerY)
                    : new Vector2Int(Mathf.Max(0, pondRect.xMin - 3), centerY);
                return CellToWorld(spawnCell);
            }

            return new Vector3(-TownHalfExtent + 6f, 0f, -TownHalfExtent + 6f);
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

        void UpdateCoins()
        {
            if (_player == null || _coins.Count == 0)
            {
                return;
            }

            var playerPosition = _player.transform.position;
            for (var index = _coins.Count - 1; index >= 0; index--)
            {
                var coin = _coins[index];
                if (coin.Transform == null)
                {
                    _coins.RemoveAt(index);
                    continue;
                }

                var delta = coin.Transform.position - playerPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude <= coin.PickupRadius * coin.PickupRadius)
                {
                    CollectCoin(index);
                }
            }
        }

        void CollectCoin(int coinIndex)
        {
            var coin = _coins[coinIndex];
            if (coin.Transform != null)
            {
                Destroy(coin.Transform.gameObject);
            }

            _coins.RemoveAt(coinIndex);
            _coinsCollected++;
            RefreshCoinCountText();
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
            if (Vector2.Distance(
                    new Vector2(_player.transform.position.x, _player.transform.position.z),
                    new Vector2(door.ClosedCenter.x, door.ClosedCenter.z)) <= threshold)
            {
                return false;
            }

            for (var index = 0; index < _villagers.Count; index++)
            {
                var villager = _villagers[index];
                if (Vector2.Distance(
                        new Vector2(villager.Transform.position.x, villager.Transform.position.z),
                        new Vector2(door.ClosedCenter.x, door.ClosedCenter.z)) <= threshold)
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
                var targetMesh = door.IsOpen ? door.OpenMesh : door.ClosedMesh;
                if (door.VisualMeshFilter.sharedMesh != targetMesh)
                {
                    door.VisualMeshFilter.sharedMesh = targetMesh;
                }

                if (door.Collision.enabled == door.IsOpen)
                {
                    door.Collision.enabled = !door.IsOpen;
                }
            }
        }

        void UpdateBuildingRoofReveal()
        {
            if (_player == null || _houses.Count == 0)
            {
                return;
            }

            HouseInstance? activeHouse = null;
            var playerCell = WorldToCell(_player.transform.position);
            for (var index = 0; index < _houses.Count; index++)
            {
                var house = _houses[index];
                if (house.Interior.Contains(playerCell))
                {
                    activeHouse = house;
                    break;
                }
            }

            for (var index = 0; index < _houses.Count; index++)
            {
                var house = _houses[index];
                SetHouseRoofRevealState(house, ReferenceEquals(house, activeHouse));
            }
        }

        static void SetHouseRoofRevealState(HouseInstance house, bool reveal, bool force = false)
        {
            if (!force && house.RevealActive == reveal)
            {
                return;
            }

            house.RevealActive = reveal;
            house.PropertyBlock.Clear();
            house.PropertyBlock.SetFloat(RevealEnabledShaderId, reveal ? 1f : 0f);
            house.PropertyBlock.SetFloat(RevealHeightShaderId, house.RoofRevealHeight);
            house.PropertyBlock.SetFloat(RevealFeatherShaderId, house.RoofRevealFeather);
            for (var index = 0; index < house.Renderers.Length; index++)
            {
                house.Renderers[index].SetPropertyBlock(house.PropertyBlock);
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

            RefreshCoinCountText();
            RefreshPrompt();
            UpdateSpeechBubble();
        }

        void RefreshCoinCountText()
        {
            if (_coinCountText == null)
            {
                return;
            }

            _coinCountText.text = FormatCoinCount(_coinsCollected);
        }

        static string FormatCoinCount(int coinCount)
        {
            return "x " + Mathf.Max(0, coinCount);
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
                var destination = ChoosePatrolDestination(villager, start);
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

        Vector2Int ChoosePatrolDestination(VillagerInstance villager, Vector2Int start)
        {
            if (villager.PatrolCells.Count == 0)
            {
                return villager.HomeCell;
            }

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = villager.PatrolCells[_worldRandom.Next(0, villager.PatrolCells.Count)];
                var distance = Mathf.Abs(candidate.x - start.x) + Mathf.Abs(candidate.y - start.y);
                if (candidate != start && distance >= 2)
                {
                    return candidate;
                }
            }

            return villager.HomeCell != start ? villager.HomeCell : villager.PatrolCenter;
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

        void PopulatePatrolCells(VillagerInstance villager)
        {
            villager.PatrolCells.Clear();
            _villageGrid.CollectReachableCells(villager.PatrolCenter, villager.PatrolRadius, villager.PatrolCells, false);
            if (villager.PatrolCells.Count == 0)
            {
                _villageGrid.CollectReachableCells(villager.HomeCell, villager.PatrolRadius, villager.PatrolCells, false);
            }
            if (!villager.PatrolCells.Contains(villager.HomeCell))
            {
                villager.PatrolCells.Add(villager.HomeCell);
            }
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

        void CreateGrassPatch(Transform grassRoot, VillageGrassPlacement placement, int index)
        {
            var size = GetGrassSize(placement.Variant, index);
            var palette = GetGrassPalette(placement.Variant);
            var world =
                CellToWorld(placement.Cell) +
                new Vector3(placement.CellOffset.x * WorldCellSize, 0f, placement.CellOffset.y * WorldCellSize);

            var grass = VoxelEnvironmentFactory.CreateGrass(
                "Grass_" + index,
                world + new Vector3(0f, size.y * 0.5f, 0f),
                size,
                placement.Yaw,
                placement.Variant,
                palette.Base,
                palette.Tip);
            grass.Root.transform.SetParent(grassRoot, true);
        }

        void CreateFencePath(VillageFencePath fencePath, int index)
        {
            if (fencePath.cells.Length == 0)
            {
                return;
            }

            var fenceRoot = new GameObject("Fence_" + fencePath.id).transform;
            fenceRoot.SetParent(_worldRoot, false);

            var lookup = new HashSet<Vector2Int>(fencePath.cells);
            var height = 1.7f;
            var size = new Vector3(WorldCellSize, height, WorldCellSize);
            var color = GetFenceColor(index);

            for (var cellIndex = 0; cellIndex < fencePath.cells.Length; cellIndex++)
            {
                var cell = fencePath.cells[cellIndex];
                var fence = VoxelEnvironmentFactory.CreateFence(
                    "FenceCell_" + cellIndex,
                    CellToWorld(cell) + new Vector3(0f, height * 0.5f, 0f),
                    size,
                    lookup.Contains(cell + Vector2Int.up),
                    lookup.Contains(cell + Vector2Int.right),
                    lookup.Contains(cell + Vector2Int.down),
                    lookup.Contains(cell + Vector2Int.left),
                    color);
                fence.Root.transform.SetParent(fenceRoot, true);
            }
        }

        void CreateDoor(VillageDoorLayout layoutDoor)
        {
            var facing = DirectionToWorld(layoutDoor.facing);
            var closedYaw = YawFromDirection(layoutDoor.facing);
            var openDelta = layoutDoor.facing.x <= 0 && layoutDoor.facing.y >= 0 ? -DoorOpenAngle : DoorOpenAngle;
            var building = FindBuildingLayout(layoutDoor.buildingId);
            var houseSize = building != null
                ? GetHouseSize(building, layoutDoor.facing)
                : new Vector3(WorldCellSize * 6f, 5.4f, WorldCellSize * 6f);
            var openingWidth = houseSize.x * DoorOpeningWidthRatio;
            var openingHeight = houseSize.y * DoorOpeningHeightRatio;
            var doorSize = new Vector3(
                Mathf.Clamp(openingWidth * DoorLeafClearance, WorldCellSize * 0.58f, WorldCellSize * 1.05f),
                Mathf.Clamp(openingHeight * DoorLeafClearance, WorldCellSize * 0.92f, WorldCellSize * 1.28f),
                Mathf.Clamp(houseSize.z * DoorWallDepthRatio * 0.72f, WorldCellSize * 0.18f, WorldCellSize * 0.32f));
            var right = Quaternion.Euler(0f, closedYaw, 0f) * Vector3.right;
            var closedCenter = GetClosedDoorCenter(layoutDoor, building, houseSize, doorSize.z, facing);
            var hingeBase = closedCenter - (right * (doorSize.x * 0.5f));

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
            door.Root.transform.localPosition = new Vector3(
                doorSize.x * 0.5f,
                (houseSize.y * VoxelEnvironmentFactory.HouseDoorSillNormalizedHeight) + (doorSize.y * 0.5f),
                0f);

            var meshFilter = door.Root.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null)
            {
                throw new InvalidOperationException("Door visual is missing a MeshFilter.");
            }

            var closedMesh = VoxelEnvironmentFactory.GetDoorMesh(false);
            var openMesh = VoxelEnvironmentFactory.GetDoorMesh(true);
            var doorCollider = door.Root.AddComponent<BoxCollider>();
            doorCollider.center = Vector3.zero;
            doorCollider.size = closedMesh.bounds.size;
            doorCollider.enabled = !layoutDoor.startsOpen;
            meshFilter.sharedMesh = layoutDoor.startsOpen ? openMesh : closedMesh;

            var interactionPoint = closedCenter + new Vector3(0f, 1.1f, 0f) + (facing * (WorldCellSize * DoorInteractionForwardOffset));
            var instance = new DoorInstance(
                layoutDoor.id,
                layoutDoor.cell,
                pivot,
                closedCenter,
                interactionPoint,
                meshFilter,
                doorCollider,
                closedMesh,
                openMesh,
                closedYaw,
                openDelta,
                layoutDoor.startsOpen);
            if (layoutDoor.startsOpen)
            {
                instance.CurrentYaw = closedYaw + openDelta;
                pivot.localRotation = Quaternion.Euler(0f, instance.CurrentYaw, 0f);
            }

            _doors.Add(instance);
        }

        void CreateTrafficSignal(VillageTrafficSignalLayout layoutSignal)
        {
            var size = new Vector3(TrafficSignalWidth, TrafficSignalHeight, TrafficSignalDepth);
            var worldCenter = CellToWorld(layoutSignal.cell) + new Vector3(0f, size.y * 0.5f, 0f);
            var signal = VoxelEnvironmentFactory.CreateTrafficSignal(
                "TrafficSignal_" + layoutSignal.id,
                worldCenter,
                size,
                YawFromDirection(layoutSignal.facing),
                new Color(0.44f, 0.47f, 0.5f),
                new Color(0.14f, 0.15f, 0.16f));
            signal.Root.transform.SetParent(_worldRoot, true);

            var lampAnchor = new GameObject("LampAnchor").transform;
            lampAnchor.SetParent(_worldRoot, false);
            lampAnchor.position = signal.Root.transform.position;
            lampAnchor.rotation = signal.Root.transform.rotation;

            var lampScale = new Vector3(TrafficSignalLampSize, TrafficSignalLampSize, TrafficSignalLampDepth);
            var redLamp = CreateTrafficSignalLamp(
                lampAnchor,
                "Lamp_Red",
                new Vector3(0f, TrafficSignalLampCenterYOffset + TrafficSignalLampSpacing, TrafficSignalLampForwardOffset),
                lampScale);
            var yellowLamp = CreateTrafficSignalLamp(
                lampAnchor,
                "Lamp_Yellow",
                new Vector3(0f, TrafficSignalLampCenterYOffset, TrafficSignalLampForwardOffset),
                lampScale);
            var greenLamp = CreateTrafficSignalLamp(
                lampAnchor,
                "Lamp_Green",
                new Vector3(0f, TrafficSignalLampCenterYOffset - TrafficSignalLampSpacing, TrafficSignalLampForwardOffset),
                lampScale);

            var instance = new TrafficSignalInstance(
                layoutSignal.id,
                layoutSignal.phaseGroup,
                signal.Root.transform,
                redLamp,
                yellowLamp,
                greenLamp);
            _trafficSignals.Add(instance);
            ApplyTrafficSignalState(instance, ResolveTrafficSignalLamp(layoutSignal.phaseGroup), true);
        }

        Renderer CreateTrafficSignalLamp(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamp.name = name;
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = localPosition;
            lamp.transform.localScale = localScale;

            var collider = lamp.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            var renderer = lamp.GetComponent<Renderer>();
            renderer.sharedMaterial = GetTrafficSignalLampMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        Material GetTrafficSignalLampMaterial()
        {
            if (_trafficSignalLampMaterial != null)
            {
                return _trafficSignalLampMaterial;
            }

            _trafficSignalLampMaterial = CreateEmissiveMaterial();
            return _trafficSignalLampMaterial;
        }

        void UpdateTrafficSignals()
        {
            if (_trafficSignals.Count == 0)
            {
                return;
            }

            _trafficSignalPhaseElapsed += Time.deltaTime;
            while (_trafficSignalPhaseElapsed >= GetTrafficSignalPhaseDuration(_trafficSignalPhase))
            {
                _trafficSignalPhaseElapsed -= GetTrafficSignalPhaseDuration(_trafficSignalPhase);
                _trafficSignalPhase = GetNextTrafficSignalPhase(_trafficSignalPhase);
            }

            for (var index = 0; index < _trafficSignals.Count; index++)
            {
                var signal = _trafficSignals[index];
                ApplyTrafficSignalState(signal, ResolveTrafficSignalLamp(signal.PhaseGroup), false);
            }
        }

        void ApplyTrafficSignalState(TrafficSignalInstance signal, TrafficSignalLamp activeLamp, bool forceRefresh)
        {
            if (!forceRefresh && signal.ActiveLamp == activeLamp)
            {
                return;
            }

            signal.ActiveLamp = activeLamp;
            for (var index = 0; index < signal.LampRenderers.Length; index++)
            {
                var isActive = index == (int)activeLamp;
                var baseColor = GetTrafficSignalLampColor(index);
                var lampRenderer = signal.LampRenderers[index];
                var propertyBlock = signal.PropertyBlocks[index];
                lampRenderer.GetPropertyBlock(propertyBlock);

                var visibleColor = isActive
                    ? Color.Lerp(baseColor, Color.white, 0.2f)
                    : Color.Lerp(baseColor, Color.black, 0.82f);
                var emissionIntensity = isActive ? TrafficSignalLampActiveEmission : TrafficSignalLampInactiveEmission;
                var emissionColor = baseColor * emissionIntensity;

                if (lampRenderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    propertyBlock.SetColor("_BaseColor", visibleColor);
                }

                if (lampRenderer.sharedMaterial.HasProperty("_Color"))
                {
                    propertyBlock.SetColor("_Color", visibleColor);
                }

                if (lampRenderer.sharedMaterial.HasProperty("_EmissionColor"))
                {
                    propertyBlock.SetColor("_EmissionColor", emissionColor);
                }

                lampRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        TrafficSignalLamp ResolveTrafficSignalLamp(VillageTrafficSignalPhaseGroup phaseGroup)
        {
            switch (_trafficSignalPhase)
            {
                case TrafficSignalCyclePhase.NorthSouthGreen:
                    return phaseGroup == VillageTrafficSignalPhaseGroup.NorthSouth ? TrafficSignalLamp.Green : TrafficSignalLamp.Red;
                case TrafficSignalCyclePhase.NorthSouthYellow:
                    return phaseGroup == VillageTrafficSignalPhaseGroup.NorthSouth ? TrafficSignalLamp.Yellow : TrafficSignalLamp.Red;
                case TrafficSignalCyclePhase.EastWestGreen:
                    return phaseGroup == VillageTrafficSignalPhaseGroup.EastWest ? TrafficSignalLamp.Green : TrafficSignalLamp.Red;
                default:
                    return phaseGroup == VillageTrafficSignalPhaseGroup.EastWest ? TrafficSignalLamp.Yellow : TrafficSignalLamp.Red;
            }
        }

        static TrafficSignalCyclePhase GetNextTrafficSignalPhase(TrafficSignalCyclePhase phase)
        {
            switch (phase)
            {
                case TrafficSignalCyclePhase.NorthSouthGreen:
                    return TrafficSignalCyclePhase.NorthSouthYellow;
                case TrafficSignalCyclePhase.NorthSouthYellow:
                    return TrafficSignalCyclePhase.EastWestGreen;
                case TrafficSignalCyclePhase.EastWestGreen:
                    return TrafficSignalCyclePhase.EastWestYellow;
                default:
                    return TrafficSignalCyclePhase.NorthSouthGreen;
            }
        }

        static float GetTrafficSignalPhaseDuration(TrafficSignalCyclePhase phase)
        {
            return phase == TrafficSignalCyclePhase.NorthSouthYellow || phase == TrafficSignalCyclePhase.EastWestYellow
                ? TrafficSignalYellowDuration
                : TrafficSignalGreenDuration;
        }

        static Color GetTrafficSignalLampColor(int index)
        {
            switch (index)
            {
                case 0:
                    return new Color(1f, 0.18f, 0.14f);
                case 1:
                    return new Color(1f, 0.78f, 0.16f);
                default:
                    return new Color(0.2f, 0.95f, 0.28f);
            }
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
            Vector2Int homeCell,
            Vector2Int patrolCenter,
            int patrolRadius)
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
            var villagerInstance = new VillagerInstance(
                npcId,
                villager.transform,
                homeCell,
                patrolCenter,
                patrolRadius,
                position.y,
                yaw,
                bobAmplitude,
                bobSpeed,
                swayAngle,
                swaySpeed,
                phaseOffset,
                character.HeadOffset);
            PopulatePatrolCells(villagerInstance);
            _villagers.Add(villagerInstance);
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

        static Material CreateEmissiveMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                enableInstancing = true
            };

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.18f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.18f);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.black);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.black);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
            }

            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
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

        Vector3 GetClosedDoorCenter(
            VillageDoorLayout layoutDoor,
            VillageBuildingLayout? building,
            Vector3 houseSize,
            float doorDepth,
            Vector3 facing)
        {
            if (building == null)
            {
                return CellToWorld(layoutDoor.cell) - (facing * (WorldCellSize * 0.16f));
            }

            var buildingCenter = CellRectCenterToWorld(building.origin, building.size);
            var frontFaceOffset = (houseSize.z * VoxelEnvironmentFactory.HouseDoorFrontFaceNormalizedDepth) - (doorDepth * 0.5f);
            return buildingCenter + (facing * frontFaceOffset);
        }

        Vector3 CellRectCenterToWorld(Vector2Int origin, Vector2Int size)
        {
            var x = (origin.x + size.x * 0.5f) * WorldCellSize - TownFootprint * 0.5f;
            var z = (origin.y + size.y * 0.5f) * WorldCellSize - TownFootprint * 0.5f;
            return new Vector3(x, 0f, z);
        }

        static RectInt GetBuildingInteriorRect(VillageBuildingLayout building)
        {
            var paddingX = building.size.x > 2 ? 1 : 0;
            var paddingY = building.size.y > 2 ? 1 : 0;
            return new RectInt(
                building.origin.x + paddingX,
                building.origin.y + paddingY,
                Mathf.Max(1, building.size.x - (paddingX * 2)),
                Mathf.Max(1, building.size.y - (paddingY * 2)));
        }

        static Vector3 GetHouseSize(VillageBuildingLayout building, Vector2Int facing)
        {
            var facadeWidthCells = facing.x != 0 ? building.size.y : building.size.x;
            var facadeDepthCells = facing.x != 0 ? building.size.x : building.size.y;
            return new Vector3(
                facadeWidthCells * WorldCellSize * 0.96f,
                3.6f + building.height * 0.45f,
                facadeDepthCells * WorldCellSize * 0.96f);
        }

        VillageBuildingLayout? FindBuildingLayout(string buildingId)
        {
            for (var index = 0; index < _layout.buildings.Length; index++)
            {
                var building = _layout.buildings[index];
                if (string.Equals(building.id, buildingId, StringComparison.Ordinal))
                {
                    return building;
                }
            }

            return null;
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

        static Color GetFenceColor(int index)
        {
            switch (index % 4)
            {
                case 0:
                    return new Color(0.6f, 0.39f, 0.22f);
                case 1:
                    return new Color(0.56f, 0.33f, 0.18f);
                case 2:
                    return new Color(0.67f, 0.45f, 0.25f);
                default:
                    return new Color(0.5f, 0.3f, 0.16f);
            }
        }

        static Vector3 GetGrassSize(VillageGrassVariant variant, int index)
        {
            var widthJitter = Mathf.Lerp(0.92f, 1.12f, Hash01(index, 17 + ((int)variant * 11)));
            var heightJitter = Mathf.Lerp(0.9f, 1.16f, Hash01(index, 53 + ((int)variant * 17)));

            switch (variant)
            {
                case VillageGrassVariant.PatchA:
                    return new Vector3(WorldCellSize * 0.38f * widthJitter, WorldCellSize * 0.62f * heightJitter, WorldCellSize * 0.38f * widthJitter) * GrassScaleMultiplier;
                case VillageGrassVariant.PatchB:
                    return new Vector3(WorldCellSize * 0.5f * widthJitter, WorldCellSize * 0.54f * heightJitter, WorldCellSize * 0.5f * widthJitter) * GrassScaleMultiplier;
                case VillageGrassVariant.PatchC:
                    return new Vector3(WorldCellSize * 0.32f * widthJitter, WorldCellSize * 0.88f * heightJitter, WorldCellSize * 0.32f * widthJitter) * GrassScaleMultiplier;
                default:
                    return new Vector3(WorldCellSize * 0.56f * widthJitter, WorldCellSize * 0.68f * heightJitter, WorldCellSize * 0.56f * widthJitter) * GrassScaleMultiplier;
            }
        }

        static GrassPalette GetGrassPalette(VillageGrassVariant variant)
        {
            // Share grass materials per variant so runtime static combine can collapse them.
            switch (variant)
            {
                case VillageGrassVariant.PatchA:
                    return new GrassPalette(
                        new Color(0.28f, 0.55f, 0.23f),
                        new Color(0.54f, 0.77f, 0.37f));
                case VillageGrassVariant.PatchB:
                    return new GrassPalette(
                        new Color(0.34f, 0.58f, 0.2f),
                        new Color(0.68f, 0.83f, 0.34f));
                case VillageGrassVariant.PatchC:
                    return new GrassPalette(
                        new Color(0.23f, 0.47f, 0.21f),
                        new Color(0.46f, 0.72f, 0.4f));
                default:
                    return new GrassPalette(
                        new Color(0.27f, 0.53f, 0.28f),
                        new Color(0.52f, 0.78f, 0.52f));
            }
        }

        static float Hash01(int index, int salt)
        {
            var value = (uint)(index + 1);
            value ^= (uint)(salt * 747796405);
            value *= 2891336453u;
            value ^= value >> 15;
            value *= 277803737u;
            value ^= value >> 13;
            return (value & 0x00ffffffu) / 16777215f;
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

        readonly struct GrassPalette
        {
            public GrassPalette(Color @base, Color tip)
            {
                Base = @base;
                Tip = tip;
            }

            public Color Base { get; }

            public Color Tip { get; }
        }

        sealed class HouseInstance
        {
            public HouseInstance(
                string buildingId,
                RectInt interior,
                MeshRenderer[] renderers,
                float roofRevealHeight,
                float roofRevealFeather)
            {
                BuildingId = buildingId;
                Interior = interior;
                Renderers = renderers;
                RoofRevealHeight = roofRevealHeight;
                RoofRevealFeather = roofRevealFeather;
                PropertyBlock = new MaterialPropertyBlock();
            }

            public string BuildingId { get; }

            public RectInt Interior { get; }

            public MeshRenderer[] Renderers { get; }

            public float RoofRevealHeight { get; }

            public float RoofRevealFeather { get; }

            public MaterialPropertyBlock PropertyBlock { get; }

            public bool RevealActive { get; set; }
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
            public DoorInstance(
                string doorId,
                Vector2Int cell,
                Transform pivot,
                Vector3 closedCenter,
                Vector3 interactionPoint,
                MeshFilter visualMeshFilter,
                Collider collision,
                Mesh closedMesh,
                Mesh openMesh,
                float closedYaw,
                float openDeltaYaw,
                bool startsOpen)
            {
                DoorId = doorId;
                Cell = cell;
                Pivot = pivot;
                ClosedCenter = closedCenter;
                InteractionPoint = interactionPoint;
                VisualMeshFilter = visualMeshFilter;
                Collision = collision;
                ClosedMesh = closedMesh;
                OpenMesh = openMesh;
                ClosedYaw = closedYaw;
                OpenDeltaYaw = openDeltaYaw;
                CurrentYaw = startsOpen ? closedYaw + openDeltaYaw : closedYaw;
                IsOpen = startsOpen;
            }

            public string DoorId { get; }

            public Vector2Int Cell { get; }

            public Transform Pivot { get; }

            public Vector3 ClosedCenter { get; }

            public Vector3 InteractionPoint { get; }

            public MeshFilter VisualMeshFilter { get; }

            public Collider Collision { get; }

            public Mesh ClosedMesh { get; }

            public Mesh OpenMesh { get; }

            public float ClosedYaw { get; }

            public float OpenDeltaYaw { get; }

            public float CurrentYaw { get; set; }

            public bool IsOpen { get; set; }
        }

        sealed class TrafficSignalInstance
        {
            public TrafficSignalInstance(
                string signalId,
                VillageTrafficSignalPhaseGroup phaseGroup,
                Transform root,
                Renderer redLamp,
                Renderer yellowLamp,
                Renderer greenLamp)
            {
                SignalId = signalId;
                PhaseGroup = phaseGroup;
                Root = root;
                LampRenderers = new[] { redLamp, yellowLamp, greenLamp };
                PropertyBlocks = new[]
                {
                    new MaterialPropertyBlock(),
                    new MaterialPropertyBlock(),
                    new MaterialPropertyBlock()
                };
                ActiveLamp = TrafficSignalLamp.Red;
            }

            public string SignalId { get; }

            public VillageTrafficSignalPhaseGroup PhaseGroup { get; }

            public Transform Root { get; }

            public Renderer[] LampRenderers { get; }

            public MaterialPropertyBlock[] PropertyBlocks { get; }

            public TrafficSignalLamp ActiveLamp { get; set; }
        }

        sealed class CoinInstance
        {
            public CoinInstance(Transform transform, float pickupRadius)
            {
                Transform = transform;
                PickupRadius = Mathf.Max(0.1f, pickupRadius);
            }

            public Transform Transform { get; }

            public float PickupRadius { get; }
        }

        sealed class VillagerInstance
        {
            public VillagerInstance(
                string npcId,
                Transform transform,
                Vector2Int homeCell,
                Vector2Int patrolCenter,
                int patrolRadius,
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
                PatrolCenter = patrolCenter;
                PatrolRadius = Mathf.Max(2, patrolRadius);
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

            public Vector2Int PatrolCenter { get; }

            public int PatrolRadius { get; }

            public Vector2Int CurrentCell { get; set; }

            public float GroundY { get; }

            public float FacingYaw { get; set; }

            public float BobAmplitude { get; }

            public float BobSpeed { get; }

            public float SwayAngle { get; }

            public float SwaySpeed { get; }

            public float PhaseOffset { get; }

            public float HeadOffset { get; }

            public List<Vector2Int> PatrolCells { get; } = new List<Vector2Int>();

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
