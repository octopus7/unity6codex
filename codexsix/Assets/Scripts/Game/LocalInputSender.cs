using CodexSix.TopdownShooter.Net;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CodexSix.TopdownShooter.Game
{
    public sealed class LocalInputSender : MonoBehaviour
    {
        [Header("Runtime References")]
        public NetworkGameClient Client;
        public Camera WorldCamera;
        public float SendRateHz = 30f;

        [Header("Crosshair")]
        public bool DrawCrosshair = true;
        public float CrosshairSizePixels = 18f;
        public float CrosshairThicknessPixels = 2f;
        public float CrosshairGapPixels = 4f;
        public Color CrosshairColor = new Color(1f, 0.96f, 0.25f, 0.95f);

        [Header("Recoil")]
        public float LocalFireCadenceSeconds = 0.2f;
        public float RecoilKickPixels = 14f;
        public float RecoilKickRandomPixels = 4f;
        public float RecoilKickAngleVarianceDegrees = 16f;
        public float RecoilReturnSpring = 24f;
        public float RecoilDamping = 11f;
        public float MaxRecoilOffsetPixels = 120f;

        private float _sendAccumulator;
        private Vector2 _lastAimDirection = Vector2.up;
        private Vector2 _crosshairScreenPosition;
        private Vector2 _recoilOffset;
        private Vector2 _recoilVelocity;
        private bool _wasFireHeldLastFrame;
        private float _nextRecoilKickAt;
        private static Texture2D _pixel;

        private void Awake()
        {
            _crosshairScreenPosition = ReadPointerPosition();
        }

        private void Update()
        {
            var pointerPosition = ReadPointerPosition();
            var fireHeld = ReadFireInput();

            UpdateRecoil(fireHeld);
            _crosshairScreenPosition = ApplyRecoilToPointer(pointerPosition);

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
            var aim = ReadAimDirection(_crosshairScreenPosition);
            Client.SendInputFrame(move, aim, fireHeld);
        }

        private void OnDisable()
        {
            _sendAccumulator = 0f;
            _recoilOffset = Vector2.zero;
            _recoilVelocity = Vector2.zero;
            _wasFireHeldLastFrame = false;
            _nextRecoilKickAt = 0f;
        }

        private void OnGUI()
        {
            if (!DrawCrosshair || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (Client == null || Client.CurrentConnectionState != ConnectionState.Connected)
            {
                return;
            }

            var x = _crosshairScreenPosition.x;
            var y = Screen.height - _crosshairScreenPosition.y;
            var half = Mathf.Max(2f, CrosshairSizePixels * 0.5f);
            var thickness = Mathf.Max(1f, CrosshairThicknessPixels);
            var gap = Mathf.Max(0f, CrosshairGapPixels);
            var segmentLength = Mathf.Max(1f, half - gap);

            DrawRect(new Rect(x - half, y - (thickness * 0.5f), segmentLength, thickness), CrosshairColor);
            DrawRect(new Rect(x + gap, y - (thickness * 0.5f), segmentLength, thickness), CrosshairColor);
            DrawRect(new Rect(x - (thickness * 0.5f), y - half, thickness, segmentLength), CrosshairColor);
            DrawRect(new Rect(x - (thickness * 0.5f), y + gap, thickness, segmentLength), CrosshairColor);
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

        private void UpdateRecoil(bool fireHeld)
        {
            var now = Time.time;
            var cadence = Mathf.Max(0.03f, LocalFireCadenceSeconds);

            if (!fireHeld)
            {
                _nextRecoilKickAt = now;
            }
            else
            {
                if (!_wasFireHeldLastFrame)
                {
                    _nextRecoilKickAt = now;
                }

                if (now >= _nextRecoilKickAt)
                {
                    ApplyRecoilKick();
                    _nextRecoilKickAt = now + cadence;
                }
            }

            _wasFireHeldLastFrame = fireHeld;

            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            var spring = Mathf.Max(0f, RecoilReturnSpring);
            var damping = Mathf.Max(0f, RecoilDamping);
            _recoilVelocity += (-_recoilOffset * spring) * deltaTime;
            _recoilVelocity *= Mathf.Exp(-damping * deltaTime);
            _recoilOffset += _recoilVelocity * deltaTime;

            var maxOffset = Mathf.Max(0f, MaxRecoilOffsetPixels);
            if (maxOffset > 0f)
            {
                _recoilOffset = Vector2.ClampMagnitude(_recoilOffset, maxOffset);
            }
        }

        private void ApplyRecoilKick()
        {
            var angleVariance = Mathf.Max(0f, RecoilKickAngleVarianceDegrees) * Mathf.Deg2Rad;
            var angle = Random.Range(-angleVariance, angleVariance);
            var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));

            var kickBase = Mathf.Max(0f, RecoilKickPixels);
            var kickRandom = Mathf.Max(0f, RecoilKickRandomPixels);
            var kickDistance = kickBase + Random.Range(-kickRandom, kickRandom);
            if (kickDistance <= 0f)
            {
                return;
            }

            _recoilOffset += direction * kickDistance;
            _recoilVelocity += direction * (kickDistance * 2.5f);
        }

        private Vector2 ApplyRecoilToPointer(Vector2 pointerPosition)
        {
            var withRecoil = pointerPosition + _recoilOffset;
            return ClampToScreen(withRecoil);
        }

        private static Vector2 ClampToScreen(Vector2 screenPosition)
        {
            var maxX = Mathf.Max(0f, Screen.width - 1f);
            var maxY = Mathf.Max(0f, Screen.height - 1f);
            return new Vector2(
                Mathf.Clamp(screenPosition.x, 0f, maxX),
                Mathf.Clamp(screenPosition.y, 0f, maxY));
        }

        private static Vector2 ReadPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
#else
            return Input.mousePosition;
#endif
        }

        private Vector2 ReadAimDirection(Vector2 aimScreenPosition)
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

            var ray = WorldCamera.ScreenPointToRay(aimScreenPosition);
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

        private static void DrawRect(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, PixelTexture);
            GUI.color = previousColor;
        }

        private static Texture2D PixelTexture
        {
            get
            {
                if (_pixel != null)
                {
                    return _pixel;
                }

                _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                _pixel.SetPixel(0, 0, Color.white);
                _pixel.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                return _pixel;
            }
        }
    }
}
