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

        private bool facingLeft;
        private int previousHorizontalDirection;
        private int lastHorizontalPressDirection;
        private float lastHorizontalPressTime = -999f;
        private int runDirection;

        public Vector2 XBounds
        {
            get => xBounds;
            set => xBounds = value;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            doubleTapRunWindowSeconds = Mathf.Max(0.05f, doubleTapRunWindowSeconds);
            EnsureReferences();
        }

        private void Update()
        {
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
