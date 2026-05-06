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
        [SerializeField] private SpriteRenderer facingRenderer;
        [SerializeField] private CharacterMotionDriver motionDriver;

        private bool facingLeft;

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
            EnsureReferences();
        }

        private void Update()
        {
            var horizontal = ReadHorizontal();
            UpdateFacing(horizontal);

            var wantsRun = shiftKeyRuns && ReadRunHeld();
            var hasInput = Mathf.Abs(horizontal) > 0.01f;
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
    }
}
