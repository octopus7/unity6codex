#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace McpTest.Bowling
{
    [DisallowMultipleComponent]
    public sealed class BowlingGameController : MonoBehaviour
    {
        const int PinCount = 10;
        const float LaneWidth = 3.2f;
        const float LaneLength = 18.5f;
        const float GutterWidth = 0.45f;
        const float BallRadius = 0.18f;
        const float BallSpawnZ = -7.8f;
        const float PinDeckZ = 6.5f;
        const float BackWallZ = 10.5f;
        const float ThrowTimeout = 10f;
        const float SettleHoldDuration = 0.75f;
        const float PinTiltThreshold = 24f;

        readonly List<BowlingPin> _pins = new List<BowlingPin>(PinCount);
        readonly List<int> _rolls = new List<int>(21);
        readonly List<int> _currentFrameRolls = new List<int>(3);
        readonly Vector3[] _pinPositions = new Vector3[PinCount];

        bool[] _activeRackMask = new bool[PinCount];

        Transform _worldRoot = null!;
        Transform _laneRoot = null!;
        Transform _pinsRoot = null!;
        Transform _decorRoot = null!;

        Camera _mainCamera = null!;
        Light _mainLight = null!;
        Canvas _canvas = null!;
        Text _statusText = null!;
        Text _readoutText = null!;
        Text _scoreText = null!;
        Text _controlsText = null!;
        Text _finalText = null!;

        Material _laneMaterial = null!;
        Material _approachMaterial = null!;
        Material _gutterMaterial = null!;
        Material _trimMaterial = null!;
        Material _accentMaterial = null!;
        Material _pinMaterial = null!;
        Material _ballMaterial = null!;
        PhysicsMaterial _lanePhysics = null!;
        PhysicsMaterial _ballPhysics = null!;
        PhysicsMaterial _pinPhysics = null!;

        GameObject? _ballObject;
        Rigidbody? _ballRigidbody;

        float _aimOffset;
        float _powerNormalized = 0.62f;
        float _spinNormalized;
        float _launchPower;
        float _launchSpin;
        float _launchTime;
        float _settleTimer;
        int _pinsStandingBeforeThrow;
        int _frameNumber = 1;
        bool _ballLaunched;
        bool _awaitingThrow = true;
        bool _gameOver;
        bool _sceneBuilt;

        void Awake()
        {
            EnsureScene();
            ResetMatch();
        }

        void Update()
        {
            if (!_sceneBuilt)
            {
                return;
            }

            if (_gameOver)
            {
                if (IsRestartPressed())
                {
                    ResetMatch();
                }

                UpdateCamera();
                return;
            }

            if (_awaitingThrow)
            {
                HandleAimInput();
            }
            else
            {
                MonitorThrowProgress();
            }

            if (IsRestartPressed())
            {
                ResetMatch();
            }

            UpdateCamera();
            RefreshHud();
        }

        void FixedUpdate()
        {
            if (!_ballLaunched || _ballRigidbody == null)
            {
                return;
            }

            var travel = Mathf.Max(0f, _ballObject!.transform.position.z - BallSpawnZ);
            var hookFactor = Mathf.InverseLerp(2f, LaneLength * 0.8f, travel);
            if (hookFactor > 0f)
            {
                _ballRigidbody.AddForce(Vector3.right * (_launchSpin * 7.5f * hookFactor), ForceMode.Acceleration);
            }
        }

        void EnsureScene()
        {
            if (_sceneBuilt)
            {
                return;
            }

            EnsureCamera();
            EnsureLighting();
            EnsureSharedAssets();
            BuildWorld();
            BuildHud();
            _sceneBuilt = true;
        }

        void ResetMatch()
        {
            StopAllCoroutines();

            _gameOver = false;
            _ballLaunched = false;
            _awaitingThrow = true;
            _frameNumber = 1;
            _aimOffset = 0f;
            _powerNormalized = 0.62f;
            _spinNormalized = 0f;
            _settleTimer = 0f;
            _launchTime = 0f;
            _rolls.Clear();
            _currentFrameRolls.Clear();

            DestroyBall();
            ClearPins();

            PrepareRack(CreateFullRackMask(), "Line up the opener and throw the first ball.");
            RefreshHud();
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
            _mainCamera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            _mainCamera.fieldOfView = 54f;
            _mainCamera.nearClipPlane = 0.03f;
            _mainCamera.farClipPlane = 200f;
        }

        void EnsureLighting()
        {
            _mainLight = FindAnyObjectByType<Light>();
            if (_mainLight == null || _mainLight.type != LightType.Directional)
            {
                var lightObject = new GameObject("Bowling Key Light");
                _mainLight = lightObject.AddComponent<Light>();
                _mainLight.type = LightType.Directional;
            }

            _mainLight.intensity = 1.15f;
            _mainLight.color = new Color(1f, 0.95f, 0.88f);
            _mainLight.transform.rotation = Quaternion.Euler(48f, -24f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.2f, 0.24f);
        }

        void EnsureSharedAssets()
        {
            _laneMaterial = CreateMaterial(new Color(0.84f, 0.61f, 0.28f), 0.55f, 0.1f);
            _approachMaterial = CreateMaterial(new Color(0.34f, 0.18f, 0.1f), 0.38f, 0.05f);
            _gutterMaterial = CreateMaterial(new Color(0.05f, 0.23f, 0.28f), 0.18f);
            _trimMaterial = CreateMaterial(new Color(0.9f, 0.76f, 0.58f), 0.6f, 0.12f);
            _accentMaterial = CreateMaterial(new Color(0.96f, 0.41f, 0.24f), 0.3f);
            _pinMaterial = CreateMaterial(new Color(0.96f, 0.96f, 0.95f), 0.45f);
            _ballMaterial = CreateMaterial(new Color(0.12f, 0.68f, 0.82f), 0.7f, 0.05f);

            _lanePhysics = new PhysicsMaterial("BowlingLane")
            {
                dynamicFriction = 0.18f,
                staticFriction = 0.2f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            _ballPhysics = new PhysicsMaterial("BowlingBall")
            {
                dynamicFriction = 0.24f,
                staticFriction = 0.28f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            _pinPhysics = new PhysicsMaterial("BowlingPin")
            {
                dynamicFriction = 0.32f,
                staticFriction = 0.35f,
                bounciness = 0.02f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };
        }

        void BuildWorld()
        {
            _worldRoot = CreateRoot("BowlingWorld", transform);
            _laneRoot = CreateRoot("Lane", _worldRoot);
            _pinsRoot = CreateRoot("Pins", _worldRoot);
            _decorRoot = CreateRoot("Decor", _worldRoot);

            var floorMaterial = CreateMaterial(new Color(0.09f, 0.11f, 0.15f), 0.2f);

            var platform = CreatePrimitive(
                PrimitiveType.Cube,
                "AlleyFloor",
                new Vector3(0f, -0.65f, 0.7f),
                new Vector3(12f, 1.2f, 26f),
                floorMaterial,
                _decorRoot);
            platform.GetComponent<Collider>().sharedMaterial = _lanePhysics;

            var approach = CreatePrimitive(
                PrimitiveType.Cube,
                "Approach",
                new Vector3(0f, -0.08f, -5.9f),
                new Vector3(LaneWidth, 0.18f, 5f),
                _approachMaterial,
                _laneRoot);
            approach.GetComponent<Collider>().sharedMaterial = _lanePhysics;

            var lane = CreatePrimitive(
                PrimitiveType.Cube,
                "LaneSurface",
                new Vector3(0f, -0.05f, 2.8f),
                new Vector3(LaneWidth, 0.12f, LaneLength),
                _laneMaterial,
                _laneRoot);
            lane.GetComponent<Collider>().sharedMaterial = _lanePhysics;

            var deck = CreatePrimitive(
                PrimitiveType.Cube,
                "PinDeck",
                new Vector3(0f, -0.045f, 7.95f),
                new Vector3(LaneWidth, 0.13f, 3.2f),
                _trimMaterial,
                _laneRoot);
            deck.GetComponent<Collider>().sharedMaterial = _lanePhysics;

            var leftGutter = CreatePrimitive(
                PrimitiveType.Cube,
                "LeftGutter",
                new Vector3(-(LaneWidth * 0.5f + GutterWidth * 0.5f), -0.22f, 2.7f),
                new Vector3(GutterWidth, 0.32f, LaneLength + 2.5f),
                _gutterMaterial,
                _laneRoot);
            leftGutter.GetComponent<Collider>().sharedMaterial = _lanePhysics;

            var rightGutter = CreatePrimitive(
                PrimitiveType.Cube,
                "RightGutter",
                new Vector3(LaneWidth * 0.5f + GutterWidth * 0.5f, -0.22f, 2.7f),
                new Vector3(GutterWidth, 0.32f, LaneLength + 2.5f),
                _gutterMaterial,
                _laneRoot);
            rightGutter.GetComponent<Collider>().sharedMaterial = _lanePhysics;

            CreatePrimitive(
                PrimitiveType.Cube,
                "LeftWall",
                new Vector3(-(LaneWidth * 0.5f + GutterWidth + 0.08f), 1.1f, 2.8f),
                new Vector3(0.16f, 2.2f, LaneLength + 3f),
                _trimMaterial,
                _laneRoot);

            CreatePrimitive(
                PrimitiveType.Cube,
                "RightWall",
                new Vector3(LaneWidth * 0.5f + GutterWidth + 0.08f, 1.1f, 2.8f),
                new Vector3(0.16f, 2.2f, LaneLength + 3f),
                _trimMaterial,
                _laneRoot);

            CreatePrimitive(
                PrimitiveType.Cube,
                "BackWall",
                new Vector3(0f, 1.25f, BackWallZ),
                new Vector3(5f, 2.6f, 0.3f),
                _accentMaterial,
                _laneRoot);

            CreatePrimitive(
                PrimitiveType.Cube,
                "FoulLine",
                new Vector3(0f, 0.015f, -3.55f),
                new Vector3(LaneWidth, 0.02f, 0.08f),
                _accentMaterial,
                _laneRoot);

            for (var index = 0; index < 7; index++)
            {
                var xOffset = -1.2f + index * 0.4f;
                CreatePrimitive(
                    PrimitiveType.Cube,
                    "LaneArrow_" + index,
                    new Vector3(xOffset, 0.03f, -0.8f),
                    new Vector3(0.14f, 0.02f, 0.4f),
                    _accentMaterial,
                    _laneRoot);
            }

            CreatePrimitive(
                PrimitiveType.Cube,
                "Header",
                new Vector3(0f, 3.4f, 2.5f),
                new Vector3(6.2f, 0.18f, 24f),
                CreateMaterial(new Color(0.1f, 0.14f, 0.2f), 0.18f),
                _decorRoot);

            CreatePrimitive(
                PrimitiveType.Cube,
                "PosterLeft",
                new Vector3(-3.8f, 2f, 2.4f),
                new Vector3(0.2f, 2f, 8f),
                CreateMaterial(new Color(0.8f, 0.27f, 0.18f), 0.32f),
                _decorRoot);

            CreatePrimitive(
                PrimitiveType.Cube,
                "PosterRight",
                new Vector3(3.8f, 2f, 2.4f),
                new Vector3(0.2f, 2f, 8f),
                CreateMaterial(new Color(0.12f, 0.62f, 0.72f), 0.32f),
                _decorRoot);

            var rowSpacing = 0.55f;
            var columnSpacing = 0.46f;
            var indexCounter = 0;
            for (var row = 0; row < 4; row++)
            {
                var pinsInRow = row + 1;
                var rowZ = PinDeckZ + row * rowSpacing;
                var startX = -(pinsInRow - 1) * columnSpacing * 0.5f;
                for (var column = 0; column < pinsInRow; column++)
                {
                    _pinPositions[indexCounter++] = new Vector3(
                        startX + column * columnSpacing,
                        0.54f,
                        rowZ);
                }
            }
        }

        void BuildHud()
        {
            _canvas = FindAnyObjectByType<Canvas>();
            if (_canvas == null || _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                var canvasObject = new GameObject("BowlingHud");
                _canvas = canvasObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _statusText = CreatePanelText(
                "StatusPanel",
                new Vector2(16f, -16f),
                new Vector2(420f, 86f),
                font,
                22,
                TextAnchor.UpperLeft,
                new Color(0.08f, 0.11f, 0.16f, 0.78f),
                new Color(1f, 0.97f, 0.93f));

            _readoutText = CreatePanelText(
                "ReadoutPanel",
                new Vector2(-16f, -16f),
                new Vector2(270f, 134f),
                font,
                20,
                TextAnchor.UpperLeft,
                new Color(0.08f, 0.11f, 0.16f, 0.8f),
                new Color(0.91f, 0.96f, 1f),
                true);

            _scoreText = CreatePanelText(
                "ScorePanel",
                new Vector2(0f, -16f),
                new Vector2(420f, 250f),
                font,
                18,
                TextAnchor.UpperLeft,
                new Color(0.08f, 0.11f, 0.16f, 0.82f),
                new Color(1f, 0.97f, 0.93f),
                true);

            var scoreRect = _scoreText.rectTransform;
            scoreRect.anchorMin = new Vector2(0.5f, 1f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.pivot = new Vector2(0.5f, 1f);
            scoreRect.anchoredPosition = new Vector2(0f, -16f);

            _controlsText = CreatePanelText(
                "ControlsPanel",
                new Vector2(16f, 16f),
                new Vector2(520f, 118f),
                font,
                17,
                TextAnchor.LowerLeft,
                new Color(0.08f, 0.11f, 0.16f, 0.78f),
                new Color(0.88f, 0.94f, 1f),
                true);

            var controlsRect = _controlsText.rectTransform;
            controlsRect.anchorMin = new Vector2(0f, 0f);
            controlsRect.anchorMax = new Vector2(0f, 0f);
            controlsRect.pivot = new Vector2(0f, 0f);
            controlsRect.anchoredPosition = new Vector2(16f, 16f);

            _finalText = CreatePanelText(
                "FinalPanel",
                new Vector2(0f, 0f),
                new Vector2(560f, 170f),
                font,
                30,
                TextAnchor.MiddleCenter,
                new Color(0.03f, 0.06f, 0.09f, 0.92f),
                new Color(1f, 0.9f, 0.72f),
                true);

            var finalRect = _finalText.rectTransform;
            finalRect.anchorMin = new Vector2(0.5f, 0.5f);
            finalRect.anchorMax = new Vector2(0.5f, 0.5f);
            finalRect.pivot = new Vector2(0.5f, 0.5f);
            finalRect.anchoredPosition = Vector2.zero;
            _finalText.gameObject.SetActive(false);
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
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = bestFit;
            if (bestFit)
            {
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = fontSize;
            }

            return text;
        }

        void HandleAimInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var lateral = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    lateral -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    lateral += 1f;
                }

                _aimOffset = Mathf.Clamp(_aimOffset + lateral * 1.8f * Time.deltaTime, -1.15f, 1.15f);

                var powerChange = 0f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    powerChange += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    powerChange -= 1f;
                }

                _powerNormalized = Mathf.Clamp01(_powerNormalized + powerChange * 0.45f * Time.deltaTime);

                var spinChange = 0f;
                if (keyboard.qKey.isPressed)
                {
                    spinChange -= 1f;
                }

                if (keyboard.eKey.isPressed)
                {
                    spinChange += 1f;
                }

                _spinNormalized = Mathf.Clamp(_spinNormalized + spinChange * 0.9f * Time.deltaTime, -1f, 1f);

                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                {
                    LaunchBall();
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                LaunchBall();
            }

            if (_ballObject != null)
            {
                _ballObject.transform.position = GetBallPreviewPosition();
            }
        }

        void LaunchBall()
        {
            if (!_awaitingThrow || _ballRigidbody == null)
            {
                return;
            }

            _launchPower = Mathf.Lerp(11f, 18.5f, _powerNormalized);
            _launchSpin = _spinNormalized;
            _ballRigidbody.isKinematic = false;
            _ballRigidbody.linearVelocity = new Vector3(0f, 0f, _launchPower);
            _ballRigidbody.angularVelocity = new Vector3(-_launchPower / BallRadius, 0f, 0f);

            _pinsStandingBeforeThrow = CountStandingPins(_activeRackMask);
            _ballLaunched = true;
            _awaitingThrow = false;
            _launchTime = Time.time;
            _settleTimer = 0f;

            SetStatus("Ball away. Let the rack settle before the reset.");
            RefreshHud();
        }

        void MonitorThrowProgress()
        {
            if (!_ballLaunched)
            {
                return;
            }

            var elapsed = Time.time - _launchTime;
            if (elapsed >= ThrowTimeout)
            {
                CompleteThrow();
                return;
            }

            if (elapsed < 0.9f)
            {
                return;
            }

            if (HasThrowSettled())
            {
                _settleTimer += Time.deltaTime;
                if (_settleTimer >= SettleHoldDuration)
                {
                    CompleteThrow();
                }
            }
            else
            {
                _settleTimer = 0f;
            }
        }

        bool HasThrowSettled()
        {
            var ballSettled = _ballRigidbody == null ||
                _ballRigidbody.IsSleeping() ||
                (_ballRigidbody.linearVelocity.sqrMagnitude <= 0.07f && _ballRigidbody.angularVelocity.sqrMagnitude <= 1.8f);

            if (_ballObject != null && _ballObject.transform.position.y < -0.4f)
            {
                ballSettled = true;
            }

            if (_ballObject != null && _ballObject.transform.position.z > BackWallZ - 0.2f)
            {
                ballSettled = true;
            }

            if (!ballSettled)
            {
                return false;
            }

            for (var index = 0; index < _pins.Count; index++)
            {
                var pin = _pins[index];
                if (pin != null && !pin.IsSettled(0.12f, 1.3f))
                {
                    return false;
                }
            }

            return true;
        }

        void CompleteThrow()
        {
            if (!_ballLaunched)
            {
                return;
            }

            _ballLaunched = false;

            var standingMask = CaptureStandingPins();
            var pinsStandingAfterThrow = CountStandingPins(standingMask);
            var knockedPins = Mathf.Clamp(_pinsStandingBeforeThrow - pinsStandingAfterThrow, 0, _pinsStandingBeforeThrow);

            _rolls.Add(knockedPins);
            _currentFrameRolls.Add(knockedPins);
            RefreshHud();

            StartCoroutine(AdvanceFrameRoutine(standingMask, knockedPins));
        }

        IEnumerator AdvanceFrameRoutine(bool[] standingMask, int knockedPins)
        {
            yield return new WaitForSeconds(1f);

            if (_frameNumber < 10)
            {
                if (_currentFrameRolls.Count == 1)
                {
                    if (knockedPins == 10)
                    {
                        SetStatus("Strike! Fresh rack for the next frame.");
                        AdvanceToNextFrame();
                    }
                    else
                    {
                        PrepareRack(
                            standingMask,
                            knockedPins == 0
                                ? "No damage. Finish the frame."
                                : knockedPins + " down. Clean up the spare.");
                    }

                    yield break;
                }

                var framePins = _currentFrameRolls[0] + _currentFrameRolls[1];
                SetStatus(framePins >= 10 ? "Spare! Moving to the next frame." : framePins + " pins in the frame.");
                AdvanceToNextFrame();
                yield break;
            }

            if (_currentFrameRolls.Count == 1)
            {
                if (_currentFrameRolls[0] == 10)
                {
                    PrepareRack(CreateFullRackMask(), "Strike in the tenth. Two bonus balls remain.");
                }
                else
                {
                    PrepareRack(standingMask, "Tenth frame, second ball.");
                }

                yield break;
            }

            if (_currentFrameRolls.Count == 2)
            {
                var first = _currentFrameRolls[0];
                var second = _currentFrameRolls[1];

                if (first == 10)
                {
                    if (second == 10)
                    {
                        PrepareRack(CreateFullRackMask(), "Double in the tenth. Last bonus ball.");
                    }
                    else
                    {
                        PrepareRack(standingMask, "Last bonus ball in the tenth.");
                    }

                    yield break;
                }

                if (first + second >= 10)
                {
                    PrepareRack(CreateFullRackMask(), "Spare in the tenth. One bonus ball.");
                    yield break;
                }

                FinishGame();
                yield break;
            }

            FinishGame();
        }

        void AdvanceToNextFrame()
        {
            _frameNumber++;
            _currentFrameRolls.Clear();

            if (_frameNumber > 10)
            {
                FinishGame();
                return;
            }

            PrepareRack(CreateFullRackMask(), "Frame " + _frameNumber + ". Set the angle, power, and hook.");
        }

        void FinishGame()
        {
            DestroyBall();
            _gameOver = true;
            _awaitingThrow = false;
            _currentFrameRolls.Clear();

            var scorecard = BowlingScoreCalculator.BuildScorecard(_rolls);
            _finalText.gameObject.SetActive(true);
            _finalText.text =
                "Game Over\n" +
                "Final Score " + scorecard.TotalScore + "\n" +
                "Press R to bowl another game";

            SetStatus("The line is complete.");
            RefreshHud();
        }

        void PrepareRack(bool[] rackMask, string status)
        {
            DestroyBall();
            ClearPins();

            _activeRackMask = (bool[])rackMask.Clone();
            SpawnPins(_activeRackMask);
            SpawnBallPreview();

            _awaitingThrow = true;
            _ballLaunched = false;
            _pinsStandingBeforeThrow = CountStandingPins(_activeRackMask);
            _aimOffset = 0f;
            _settleTimer = 0f;

            _finalText.gameObject.SetActive(false);
            SetStatus(status);
            RefreshHud();
        }

        void SpawnBallPreview()
        {
            _ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ballObject.name = "BowlingBall";
            _ballObject.transform.SetParent(_worldRoot, false);
            _ballObject.transform.localScale = Vector3.one * (BallRadius * 2f);
            _ballObject.transform.position = GetBallPreviewPosition();

            var renderer = _ballObject.GetComponent<Renderer>();
            renderer.material = _ballMaterial;

            var collider = _ballObject.GetComponent<SphereCollider>();
            collider.sharedMaterial = _ballPhysics;

            _ballRigidbody = _ballObject.AddComponent<Rigidbody>();
            _ballRigidbody.mass = 6.4f;
            _ballRigidbody.linearDamping = 0.06f;
            _ballRigidbody.angularDamping = 0.05f;
            _ballRigidbody.useGravity = true;
            _ballRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _ballRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _ballRigidbody.maxAngularVelocity = 60f;
            _ballRigidbody.isKinematic = true;
        }

        void SpawnPins(bool[] rackMask)
        {
            for (var index = 0; index < PinCount; index++)
            {
                if (!rackMask[index])
                {
                    continue;
                }

                var pinObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                pinObject.name = "Pin_" + (index + 1);
                pinObject.transform.SetParent(_pinsRoot, false);
                pinObject.transform.position = _pinPositions[index];
                pinObject.transform.localScale = new Vector3(0.18f, 0.54f, 0.18f);

                var pinRenderer = pinObject.GetComponent<Renderer>();
                pinRenderer.material = _pinMaterial;

                var collider = pinObject.GetComponent<CapsuleCollider>();
                collider.sharedMaterial = _pinPhysics;

                var rigidbody = pinObject.AddComponent<Rigidbody>();
                rigidbody.mass = 1.45f;
                rigidbody.linearDamping = 0.03f;
                rigidbody.angularDamping = 0.04f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.centerOfMass = new Vector3(0f, -0.2f, 0f);

                var pin = pinObject.AddComponent<BowlingPin>();
                pin.Configure(index);
                _pins.Add(pin);
            }
        }

        void ClearPins()
        {
            for (var index = 0; index < _pins.Count; index++)
            {
                if (_pins[index] != null)
                {
                    Destroy(_pins[index].gameObject);
                }
            }

            _pins.Clear();
        }

        void DestroyBall()
        {
            if (_ballObject != null)
            {
                Destroy(_ballObject);
                _ballObject = null;
            }

            _ballRigidbody = null;
        }

        bool[] CaptureStandingPins()
        {
            var standing = new bool[PinCount];
            for (var index = 0; index < _pins.Count; index++)
            {
                var pin = _pins[index];
                if (pin != null && pin.IsStanding(PinTiltThreshold))
                {
                    standing[pin.PinIndex] = true;
                }
            }

            return standing;
        }

        void UpdateCamera()
        {
            if (_mainCamera == null)
            {
                return;
            }

            Vector3 targetPosition;
            Vector3 lookTarget;

            if (_ballObject != null && (_ballLaunched || !_awaitingThrow))
            {
                var ballPosition = _ballObject.transform.position;
                targetPosition = ballPosition + new Vector3(0f, 3.1f, -5.8f);
                lookTarget = ballPosition + new Vector3(0f, 0.45f, 4.6f);
            }
            else
            {
                targetPosition = new Vector3(0f, 3f, -12.3f);
                lookTarget = new Vector3(0f, 0.6f, 3.4f);
            }

            _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, targetPosition, 4f * Time.deltaTime);
            _mainCamera.transform.rotation = Quaternion.Lerp(
                _mainCamera.transform.rotation,
                Quaternion.LookRotation(lookTarget - _mainCamera.transform.position, Vector3.up),
                5f * Time.deltaTime);
        }

        void RefreshHud()
        {
            if (_statusText == null)
            {
                return;
            }

            var scorecard = BowlingScoreCalculator.BuildScorecard(_rolls);

            _readoutText.text =
                "Frame " + _frameNumber + (_gameOver ? " / done" : " / live") + "\n" +
                "Ball " + GetBallNumberLabel() + "\n" +
                "Aim " + FormatSigned(_aimOffset) + "\n" +
                "Power " + Mathf.RoundToInt(_powerNormalized * 100f) + "%\n" +
                "Hook " + FormatSigned(_spinNormalized) + "\n" +
                "Standing " + CountStandingPins(_activeRackMask) + "\n" +
                "Score " + scorecard.TotalScore;

            var builder = new StringBuilder(256);
            builder.AppendLine("Scoreboard");
            for (var frame = 0; frame < 10; frame++)
            {
                var marker = !_gameOver && frame + 1 == _frameNumber ? ">" : " ";
                var marks = string.IsNullOrEmpty(scorecard.FrameMarks[frame]) ? ". . ." : scorecard.FrameMarks[frame];
                var subtotal = scorecard.FrameTotals[frame]?.ToString() ?? "-";
                builder.Append(marker)
                    .Append(" F")
                    .Append(frame + 1)
                    .Append(": ")
                    .Append(marks)
                    .Append("   Total ")
                    .Append(subtotal)
                    .AppendLine();
            }

            _scoreText.text = builder.ToString();
            _controlsText.text =
                "A / D or Left / Right: move the line\n" +
                "W / S or Up / Down: adjust power\n" +
                "Q / E: set hook left or right\n" +
                "Space / Enter / Left Click: bowl\n" +
                "R: restart the full game";
        }

        string GetBallNumberLabel()
        {
            if (_gameOver)
            {
                return "-";
            }

            return (_currentFrameRolls.Count + 1).ToString();
        }

        string FormatSigned(float value)
        {
            return value >= 0f ? "+" + value.ToString("0.00") : value.ToString("0.00");
        }

        void SetStatus(string message)
        {
            _statusText.text = "Midnight Lanes\n" + message;
        }

        Vector3 GetBallPreviewPosition()
        {
            return new Vector3(_aimOffset, BallRadius + 0.02f, BallSpawnZ);
        }

        static int CountStandingPins(bool[] rackMask)
        {
            var count = 0;
            for (var index = 0; index < rackMask.Length; index++)
            {
                if (rackMask[index])
                {
                    count++;
                }
            }

            return count;
        }

        static bool[] CreateFullRackMask()
        {
            var mask = new bool[PinCount];
            for (var index = 0; index < mask.Length; index++)
            {
                mask[index] = true;
            }

            return mask;
        }

        bool IsRestartPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.rKey.wasPressedThisFrame;
        }

        static Material CreateMaterial(Color color, float smoothness, float metallic = 0f)
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
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            return material;
        }

        static Transform CreateRoot(string name, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        static GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Transform parent)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.GetComponent<Renderer>().material = material;
            return primitive;
        }
    }
}
