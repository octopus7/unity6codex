using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BeltScroll
{
    [RequireComponent(typeof(CharacterMotionDriver))]
    public sealed class PlayerBeltScrollController : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float runSpeed = 5.6f;
        [SerializeField] private Vector2 xBounds = new Vector2(-50f, 50f);
        [SerializeField] private bool shiftKeyRuns = true;
        [SerializeField] private bool doubleTapRuns = true;
        [SerializeField] private float doubleTapRunWindowSeconds = 0.28f;
        [SerializeField] private SpriteRenderer facingRenderer;
        [SerializeField] private CharacterMotionDriver motionDriver;
        [SerializeField] private bool numberKeysSwitchCharacterVariant = true;
        [SerializeField] private Texture2D wizardWalkSheet;
        [SerializeField] private int wizardWalkColumns = 6;
        [SerializeField] private int wizardWalkRows = 4;
        [SerializeField] private int wizardWalkCellWidth = 290;
        [SerializeField] private int wizardWalkCellHeight = 540;
        [SerializeField] private float wizardWalkPixelsPerUnit = 150.76923f;
        [SerializeField] private float wizardWalkFramesPerSecond = 12f;
        [SerializeField] private Vector2 wizardWalkPivot = new Vector2(0.5f, 0.08333334f);

        private bool facingLeft;
        private int previousHorizontalDirection;
        private int lastHorizontalPressDirection;
        private float lastHorizontalPressTime = -999f;
        private int runDirection;
        private CharacterMotionSet defaultMotionSet;
        private CharacterMotionSet wizardWalkMotionSet;

        public Vector2 XBounds
        {
            get => xBounds;
            set => xBounds = value;
        }

        private void Awake()
        {
            EnsureReferences();
            CacheDefaultMotionSet();
        }

        private void OnValidate()
        {
            doubleTapRunWindowSeconds = Mathf.Max(0.05f, doubleTapRunWindowSeconds);
            wizardWalkColumns = Mathf.Max(1, wizardWalkColumns);
            wizardWalkRows = Mathf.Max(1, wizardWalkRows);
            wizardWalkCellWidth = Mathf.Max(1, wizardWalkCellWidth);
            wizardWalkCellHeight = Mathf.Max(1, wizardWalkCellHeight);
            wizardWalkPixelsPerUnit = Mathf.Max(1f, wizardWalkPixelsPerUnit);
            wizardWalkFramesPerSecond = Mathf.Max(0.01f, wizardWalkFramesPerSecond);
            EnsureReferences();
        }

        private void Update()
        {
            HandleCharacterVariantSwitch();

            var horizontal = ReadHorizontal();
            var horizontalDirection = ToDirection(horizontal);
            UpdateRunLatch(horizontalDirection);
            UpdateFacing(horizontal);

            var wantsRun = (shiftKeyRuns && ReadRunHeld()) || (doubleTapRuns && runDirection == horizontalDirection);
            var hasInput = horizontalDirection != 0;
            var desiredMotion = !hasInput
                ? CharacterBaseMotion.Idle
                : wantsRun
                    ? CharacterBaseMotion.Run
                    : CharacterBaseMotion.Walk;

            motionDriver.SetDesiredMotion(desiredMotion);

            if (!hasInput)
            {
                return;
            }

            var speed = desiredMotion == CharacterBaseMotion.Run ? runSpeed : walkSpeed;
            var position = transform.position;
            position.x = Mathf.Clamp(position.x + horizontal * speed * Time.deltaTime, xBounds.x, xBounds.y);
            transform.position = position;

        }

        private void UpdateRunLatch(int horizontalDirection)
        {
            if (horizontalDirection == 0)
            {
                runDirection = 0;
                previousHorizontalDirection = 0;
                return;
            }

            if (horizontalDirection != previousHorizontalDirection)
            {
                var now = Time.unscaledTime;
                var isDoubleTap = doubleTapRuns
                    && horizontalDirection == lastHorizontalPressDirection
                    && now - lastHorizontalPressTime <= doubleTapRunWindowSeconds;

                runDirection = isDoubleTap ? horizontalDirection : 0;
                lastHorizontalPressDirection = horizontalDirection;
                lastHorizontalPressTime = now;
            }

            previousHorizontalDirection = horizontalDirection;
        }

        private void EnsureReferences()
        {
            if (motionDriver == null)
            {
                motionDriver = GetComponent<CharacterMotionDriver>();
            }

            if (facingRenderer == null)
            {
                facingRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void CacheDefaultMotionSet()
        {
            if (defaultMotionSet == null && motionDriver != null)
            {
                defaultMotionSet = motionDriver.MotionSet;
            }
        }

        private void HandleCharacterVariantSwitch()
        {
            if (!numberKeysSwitchCharacterVariant)
            {
                return;
            }

            if (ReadVariantKeyDown(1))
            {
                ApplyDefaultCharacterVariant();
            }
            else if (ReadVariantKeyDown(2))
            {
                ApplyWizardWalkCharacterVariant();
            }
        }

        private void ApplyDefaultCharacterVariant()
        {
            CacheDefaultMotionSet();
            if (motionDriver != null && defaultMotionSet != null)
            {
                motionDriver.SetMotionSet(defaultMotionSet);
            }
        }

        private void ApplyWizardWalkCharacterVariant()
        {
            CacheDefaultMotionSet();
            if (motionDriver == null || defaultMotionSet == null)
            {
                return;
            }

            var motionSet = GetOrCreateWizardWalkMotionSet();
            if (motionSet != null)
            {
                motionDriver.SetMotionSet(motionSet);
            }
        }

        private CharacterMotionSet GetOrCreateWizardWalkMotionSet()
        {
            if (wizardWalkMotionSet != null)
            {
                return wizardWalkMotionSet;
            }

            EnsureWizardWalkSheet();
            if (wizardWalkSheet == null)
            {
                return null;
            }

            var walkFrames = CreateWizardWalkSprites();
            if (walkFrames.Length == 0)
            {
                return null;
            }

            var motionSet = ScriptableObject.CreateInstance<CharacterMotionSet>();
            motionSet.name = "RuntimeWizardWalkMotionSet";
            motionSet.immediateIdleWalkTransitions = true;
            motionSet.fallbackSprite = defaultMotionSet.fallbackSprite;
            motionSet.idle = CloneClip(defaultMotionSet.idle, CharacterMotionState.Idle);
            motionSet.idleToWalk = CloneClip(defaultMotionSet.idleToWalk, CharacterMotionState.IdleToWalk);
            motionSet.walk = CloneClip(defaultMotionSet.walk, CharacterMotionState.Walk);
            motionSet.walkToIdle = CloneClip(defaultMotionSet.walkToIdle, CharacterMotionState.WalkToIdle);
            motionSet.walkToRun = CloneClip(defaultMotionSet.walkToRun, CharacterMotionState.WalkToRun);
            motionSet.run = CloneClip(defaultMotionSet.run, CharacterMotionState.Run);
            motionSet.runToWalk = CloneClip(defaultMotionSet.runToWalk, CharacterMotionState.RunToWalk);
            motionSet.idleToRun = CloneClip(defaultMotionSet.idleToRun, CharacterMotionState.IdleToRun);
            motionSet.runToIdle = CloneClip(defaultMotionSet.runToIdle, CharacterMotionState.RunToIdle);

            var idleFrame = walkFrames[Mathf.Min(2, walkFrames.Length - 1)];
            motionSet.fallbackSprite = idleFrame;
            motionSet.idle.frames = new[] { idleFrame };
            motionSet.idle.framesPerSecond = 1f;
            motionSet.idle.loop = true;
            motionSet.idle.transitionSeconds = 0f;

            motionSet.walk.frames = walkFrames;
            motionSet.walk.framesPerSecond = wizardWalkFramesPerSecond;
            motionSet.walk.loop = true;
            motionSet.walk.transitionSeconds = 0f;

            wizardWalkMotionSet = motionSet;
            return wizardWalkMotionSet;
        }

        private void EnsureWizardWalkSheet()
        {
#if UNITY_EDITOR
            if (wizardWalkSheet == null)
            {
                wizardWalkSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/wizard_walk.png");
            }
#endif
        }

        private Sprite[] CreateWizardWalkSprites()
        {
            if (wizardWalkSheet == null)
            {
                return Array.Empty<Sprite>();
            }

            var columns = Mathf.Max(1, wizardWalkColumns);
            var rows = Mathf.Max(1, wizardWalkRows);
            var expectedWidth = columns * wizardWalkCellWidth;
            var expectedHeight = rows * wizardWalkCellHeight;
            var cellWidth = wizardWalkSheet.width == expectedWidth ? wizardWalkCellWidth : wizardWalkSheet.width / columns;
            var cellHeight = wizardWalkSheet.height == expectedHeight ? wizardWalkCellHeight : wizardWalkSheet.height / rows;
            var frames = new Sprite[columns * rows];

            for (var index = 0; index < frames.Length; index++)
            {
                var column = index % columns;
                var row = index / columns;
                var x = column * cellWidth;
                var y = wizardWalkSheet.height - (row + 1) * cellHeight;
                var rect = new Rect(x, y, cellWidth, cellHeight);
                var sprite = Sprite.Create(
                    wizardWalkSheet,
                    rect,
                    wizardWalkPivot,
                    wizardWalkPixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);

                sprite.name = $"WizardWalk_{index + 1:00}";
                frames[index] = sprite;
            }

            return frames;
        }

        private static CharacterMotionClip CloneClip(CharacterMotionClip source, CharacterMotionState fallbackState)
        {
            return new CharacterMotionClip
            {
                state = source != null ? source.state : fallbackState,
                frames = source != null && source.frames != null ? (Sprite[])source.frames.Clone() : Array.Empty<Sprite>(),
                framesPerSecond = source != null ? source.framesPerSecond : 8f,
                loop = source == null || source.loop,
                transitionSeconds = source != null ? source.transitionSeconds : 0.12f
            };
        }

        private void UpdateFacing(float horizontal)
        {
            if (horizontal < -0.01f)
            {
                facingLeft = true;
            }
            else if (horizontal > 0.01f)
            {
                facingLeft = false;
            }

            if (facingRenderer != null)
            {
                facingRenderer.flipX = facingLeft;
            }
        }

        private static float ReadHorizontal()
        {
            var horizontal = 0f;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    horizontal -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    horizontal += 1f;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                horizontal -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                horizontal += 1f;
            }
#endif

            return Mathf.Clamp(horizontal, -1f, 1f);
        }

        private static bool ReadRunHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                return true;
            }
#endif

            return false;
        }

        private static bool ReadVariantKeyDown(int key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (key == 1 && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame))
                {
                    return true;
                }

                if (key == 2 && (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame))
                {
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (key == 1 && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)))
            {
                return true;
            }

            if (key == 2 && (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)))
            {
                return true;
            }
#endif

            return false;
        }

        private static int ToDirection(float horizontal)
        {
            if (horizontal < -0.01f)
            {
                return -1;
            }

            if (horizontal > 0.01f)
            {
                return 1;
            }

            return 0;
        }
    }
}
