#nullable enable

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
        const string DemoNpcId = "villager_mina";
        const float PlayerMoveSpeed = 4.8f;
        const float InteractionDistance = 2.15f;
        const float CameraFollowSpeed = 6f;
        const float BubbleHeight = 2.45f;

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

        Text _titleText = null!;
        Text _helpText = null!;
        Text _promptText = null!;
        Text _languageButtonText = null!;
        RectTransform _bubbleRect = null!;
        Text _bubbleSpeakerText = null!;
        Text _bubbleContentText = null!;

        Transform _worldRoot = null!;
        GameObject _player = null!;
        GameObject _npc = null!;
        Transform _doorPivot = null!;

        InteractionTarget _currentTarget;
        bool _dialogueActive;
        int _dialogueLineIndex;
        bool _doorOpen;
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
            _mainCamera.fieldOfView = 52f;
            _mainCamera.nearClipPlane = 0.03f;
            _mainCamera.farClipPlane = 200f;
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

            var ground = CreatePrimitive(
                PrimitiveType.Cube,
                "Ground",
                new Vector3(0f, -0.5f, 0f),
                new Vector3(18f, 1f, 18f),
                CreateMaterial(new Color(0.49f, 0.74f, 0.46f)));
            ground.transform.SetParent(_worldRoot, false);

            var path = CreatePrimitive(
                PrimitiveType.Cube,
                "VillagePath",
                new Vector3(0f, -0.44f, 1.2f),
                new Vector3(3.6f, 0.12f, 9f),
                CreateMaterial(new Color(0.77f, 0.67f, 0.5f)));
            path.transform.SetParent(_worldRoot, false);

            var house = CreatePrimitive(
                PrimitiveType.Cube,
                "House",
                new Vector3(5f, 1.45f, 1.5f),
                new Vector3(4.4f, 3f, 4f),
                CreateMaterial(new Color(0.89f, 0.84f, 0.72f)));
            house.transform.SetParent(_worldRoot, false);

            var roof = CreatePrimitive(
                PrimitiveType.Cube,
                "Roof",
                new Vector3(5f, 3.32f, 1.5f),
                new Vector3(4.9f, 0.8f, 4.5f),
                CreateMaterial(new Color(0.73f, 0.31f, 0.23f)));
            roof.transform.SetParent(_worldRoot, false);

            var doorFrame = CreatePrimitive(
                PrimitiveType.Cube,
                "DoorFrame",
                new Vector3(3.42f, 1.1f, 3.52f),
                new Vector3(0.2f, 2.2f, 1f),
                CreateMaterial(new Color(0.42f, 0.28f, 0.18f)));
            doorFrame.transform.SetParent(_worldRoot, false);

            _doorPivot = new GameObject("DoorPivot").transform;
            _doorPivot.SetParent(_worldRoot, false);
            _doorPivot.position = new Vector3(3.52f, 0f, 3.52f);

            var doorPanel = CreatePrimitive(
                PrimitiveType.Cube,
                "DoorPanel",
                new Vector3(0.45f, 1.1f, 0f),
                new Vector3(0.9f, 2.2f, 0.12f),
                CreateMaterial(new Color(0.56f, 0.35f, 0.2f)));
            doorPanel.transform.SetParent(_doorPivot, false);

            _player = CreatePrimitive(
                PrimitiveType.Capsule,
                "Player",
                new Vector3(0f, 0.9f, -2.75f),
                new Vector3(0.85f, 1.8f, 0.85f),
                CreateMaterial(new Color(0.16f, 0.41f, 0.95f)));
            _player.transform.SetParent(_worldRoot, false);

            _npc = CreatePrimitive(
                PrimitiveType.Capsule,
                "Npc_Mina",
                new Vector3(0f, 0.9f, 2.8f),
                new Vector3(0.85f, 1.8f, 0.85f),
                CreateMaterial(new Color(0.92f, 0.43f, 0.35f)));
            _npc.transform.SetParent(_worldRoot, false);

            var shrub = CreatePrimitive(
                PrimitiveType.Sphere,
                "Shrub",
                new Vector3(-2.5f, 0.5f, 2.2f),
                new Vector3(1.1f, 1f, 1.1f),
                CreateMaterial(new Color(0.23f, 0.56f, 0.23f)));
            shrub.transform.SetParent(_worldRoot, false);

            var flower = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Flower",
                new Vector3(-3.4f, 0.18f, 1.4f),
                new Vector3(0.12f, 0.18f, 0.12f),
                CreateMaterial(new Color(0.94f, 0.73f, 0.2f)));
            flower.transform.SetParent(_worldRoot, false);
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

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _titleText = CreatePanelText(
                "TitlePanel",
                new Vector2(16f, -16f),
                new Vector2(440f, 76f),
                font,
                24,
                TextAnchor.UpperLeft,
                new Color(0.1f, 0.16f, 0.24f, 0.8f),
                new Color(1f, 0.97f, 0.92f));

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

            var helpRect = _helpText.rectTransform;
            helpRect.anchorMin = new Vector2(0f, 0f);
            helpRect.anchorMax = new Vector2(0f, 0f);
            helpRect.pivot = new Vector2(0f, 0f);
            helpRect.anchoredPosition = new Vector2(16f, 16f);

            _promptText = CreatePanelText(
                "PromptPanel",
                new Vector2(0f, 110f),
                new Vector2(320f, 64f),
                font,
                22,
                TextAnchor.MiddleCenter,
                new Color(0.09f, 0.13f, 0.2f, 0.82f),
                new Color(1f, 0.97f, 0.92f));

            var promptRect = _promptText.rectTransform;
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 110f);

            CreateLanguageButton(font);
            CreateSpeechBubble(font);
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

        void CreateSpeechBubble(Font font)
        {
            var bubble = new GameObject("SpeechBubble", typeof(RectTransform), typeof(Image));
            bubble.transform.SetParent(_canvas.transform, false);

            _bubbleRect = bubble.GetComponent<RectTransform>();
            _bubbleRect.sizeDelta = new Vector2(300f, 122f);
            _bubbleRect.pivot = new Vector2(0.5f, 0f);

            bubble.GetComponent<Image>().color = new Color(0.99f, 0.97f, 0.92f, 0.96f);

            var speakerObject = new GameObject("Speaker", typeof(RectTransform), typeof(Text));
            speakerObject.transform.SetParent(bubble.transform, false);
            var speakerRect = speakerObject.GetComponent<RectTransform>();
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0.5f, 1f);
            speakerRect.offsetMin = new Vector2(14f, -36f);
            speakerRect.offsetMax = new Vector2(-14f, -10f);

            _bubbleSpeakerText = speakerObject.GetComponent<Text>();
            _bubbleSpeakerText.font = font;
            _bubbleSpeakerText.fontStyle = FontStyle.Bold;
            _bubbleSpeakerText.fontSize = 19;
            _bubbleSpeakerText.alignment = TextAnchor.UpperLeft;
            _bubbleSpeakerText.color = new Color(0.23f, 0.18f, 0.12f);

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(Text));
            contentObject.transform.SetParent(bubble.transform, false);
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(14f, 14f);
            contentRect.offsetMax = new Vector2(-14f, -38f);

            _bubbleContentText = contentObject.GetComponent<Text>();
            _bubbleContentText.font = font;
            _bubbleContentText.fontSize = 20;
            _bubbleContentText.alignment = TextAnchor.UpperLeft;
            _bubbleContentText.color = new Color(0.16f, 0.14f, 0.12f);
            _bubbleContentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bubbleContentText.verticalOverflow = VerticalWrapMode.Overflow;

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
            var nextPosition = _player.transform.position + move;
            nextPosition.x = Mathf.Clamp(nextPosition.x, -7f, 7f);
            nextPosition.z = Mathf.Clamp(nextPosition.z, -7f, 7f);
            nextPosition.y = _player.transform.position.y;
            _player.transform.position = nextPosition;
            _player.transform.forward = Vector3.Lerp(_player.transform.forward, move.normalized, 16f * Time.deltaTime);
        }

        void UpdateCamera()
        {
            var targetPosition = _player.transform.position + new Vector3(0f, 6.2f, -6f);
            _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, targetPosition, CameraFollowSpeed * Time.deltaTime);
            var lookTarget = _player.transform.position + new Vector3(0f, 1.1f, 3.4f);
            _mainCamera.transform.rotation = Quaternion.Lerp(
                _mainCamera.transform.rotation,
                Quaternion.LookRotation(lookTarget - _mainCamera.transform.position, Vector3.up),
                CameraFollowSpeed * Time.deltaTime);
        }

        void UpdateInteractionTarget()
        {
            if (_dialogueActive && Vector3.Distance(_player.transform.position, _npc.transform.position) > InteractionDistance + 1f)
            {
                _dialogueActive = false;
                _dialogueLineIndex = 0;
            }

            var playerPosition = _player.transform.position;
            var npcDistance = Vector3.Distance(playerPosition, _npc.transform.position);
            var doorDistance = Vector3.Distance(playerPosition, GetDoorInteractionPoint());

            _currentTarget = InteractionTarget.None;
            var bestDistance = InteractionDistance;

            if (npcDistance <= bestDistance)
            {
                _currentTarget = InteractionTarget.Npc;
                bestDistance = npcDistance;
            }

            if (doorDistance <= bestDistance)
            {
                _currentTarget = InteractionTarget.Door;
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
            var lineCount = _database.GetDialogueLineCount(DemoNpcId);
            if (lineCount <= 0)
            {
                return;
            }

            if (!_dialogueActive)
            {
                _dialogueActive = true;
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

            var line = _database.GetDialogueLine(DemoNpcId, _dialogueLineIndex);
            if (line == null)
            {
                _bubbleRect.gameObject.SetActive(false);
                return;
            }

            _bubbleSpeakerText.text = _database.GetSpeakerDisplayName(line.speaker, DemoNpcId, _languageState.Current);
            _bubbleContentText.text = line.translations.Get(_languageState.Current);

            var screenPoint = _mainCamera.WorldToScreenPoint(_npc.transform.position + new Vector3(0f, BubbleHeight, 0f));
            var visible = screenPoint.z > 0f;
            _bubbleRect.gameObject.SetActive(visible);
            if (visible)
            {
                _bubbleRect.position = new Vector3(screenPoint.x, screenPoint.y, 0f);
            }
        }

        void RefreshLocalizedTexts()
        {
            _titleText.text = _database.GetUiText("hud.title", _languageState.Current);
            _helpText.text = _database.GetUiText("hud.instructions", _languageState.Current);
            _languageButtonText.text = string.Format(
                _database.GetUiText("hud.language.label", _languageState.Current),
                _database.GetUiText("language.name." + _languageState.Current.ToCode(), _languageState.Current));

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
                    if (!_dialogueActive)
                    {
                        key = "interaction.talk";
                    }
                    else if (_dialogueLineIndex < _database.GetDialogueLineCount(DemoNpcId) - 1)
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
        }

        void OnLanguageButtonClicked()
        {
            _languageState.CycleNext();
        }

        void OnLanguageChanged(LanguageCode _)
        {
            RefreshLocalizedTexts();
        }

        Vector3 GetDoorInteractionPoint()
        {
            return _doorPivot.position + new Vector3(-0.45f, 1.1f, 0f);
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
    }
}
