using CodexSix.TopdownShooter.Net;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CodexSix.TopdownShooter.Game
{
    public sealed class LocalInputSender : MonoBehaviour
    {
        public NetworkGameClient Client;
        public Camera WorldCamera;
        public float SendRateHz = 30f;

        private float _sendAccumulator;
        private Vector2 _lastAimDirection = Vector2.up;

        private void Update()
        {
            if (Client == null || Client.CurrentConnectionState != ConnectionState.Connected)
            {
                return;
            }

            _sendAccumulator += Time.deltaTime;
            var sendInterval = 1f / Mathf.Max(1f, SendRateHz);
            if (_sendAccumulator < sendInterval)
            {
                return;
            }

            _sendAccumulator -= sendInterval;

            var move = ReadMoveInput();
            var fireHeld = ReadFireInput();
            var aim = ReadAimDirection();
            Client.SendInputFrame(move, aim, fireHeld);
        }

        private Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                var x = 0f;
                var y = 0f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                {
                    x -= 1f;
                }

                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                {
                    x += 1f;
                }

                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                {
                    y -= 1f;
                }

                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                {
                    y += 1f;
                }

                var input = new Vector2(x, y);
                return input.sqrMagnitude > 1f ? input.normalized : input;
            }
#endif
            var fallback = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            return fallback.sqrMagnitude > 1f ? fallback.normalized : fallback;
        }

        private bool ReadFireInput()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.leftButton.isPressed : Input.GetMouseButton(0);
#else
            return Input.GetMouseButton(0);
#endif
        }

        private Vector2 ReadAimDirection()
        {
            if (WorldCamera == null && Camera.main != null)
            {
                WorldCamera = Camera.main;
            }

            if (WorldCamera == null)
            {
                return _lastAimDirection;
            }

            if (!Client.TryGetLocalPlayerPosition(out var localPlayerPosition))
            {
                return _lastAimDirection;
            }

            Vector2 pointerPosition;
#if ENABLE_INPUT_SYSTEM
            pointerPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
#else
            pointerPosition = Input.mousePosition;
#endif

            var ray = WorldCamera.ScreenPointToRay(pointerPosition);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out var enter))
            {
                return _lastAimDirection;
            }

            var hitPoint = ray.GetPoint(enter);
            var rawDirection = new Vector2(hitPoint.x - localPlayerPosition.x, hitPoint.z - localPlayerPosition.z);
            if (rawDirection.sqrMagnitude < 0.0001f)
            {
                return _lastAimDirection;
            }

            _lastAimDirection = rawDirection.normalized;
            return _lastAimDirection;
        }
    }
}
