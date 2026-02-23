using CodexSix.TopdownShooter.Net;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class CoinDispenserHud : MonoBehaviour
    {
        public NetworkGameClient Client;
        public Camera WorldCamera;
        public float UiScale = 2f;
        public float WorldHeightOffset = 1.05f;

        private GUIStyle _amountStyle;
        private GUIStyle _amountShadowStyle;
        private Texture2D _coinTexture;
        private Texture2D _radialTexture;
        private Color32[] _radialPixels;
        private float _cachedFill01 = -1f;

        private const float CoinDispenserIntervalSeconds = 5f;
        private const int CoinTextureSize = 48;
        private const int RadialTextureSize = 64;

        private static readonly Color CoinCenterColor = new(1f, 0.93f, 0.34f, 1f);
        private static readonly Color CoinEdgeColor = new(0.95f, 0.77f, 0.16f, 1f);
        private static readonly Color GaugeFillColor = new(0.63f, 0.83f, 1f, 1f);
        private static readonly Color GaugeOutlineColor = new(0.74f, 0.91f, 1f, 1f);
        private static readonly Color GaugeEmptyColor = new(0f, 0f, 0f, 1f);

        private void OnDisable()
        {
            ReleaseTextures();
        }

        private void OnDestroy()
        {
            ReleaseTextures();
        }

        private void OnGUI()
        {
            if (Client == null || Client.CurrentConnectionState != ConnectionState.Connected)
            {
                return;
            }

            var stackId = Client.CoinDispenserStackId;
            if (stackId <= 0)
            {
                return;
            }

            if (!Client.TryGetCoinStackWorldPosition(stackId, out var worldPosition))
            {
                return;
            }

            var camera = WorldCamera != null ? WorldCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            if (UiScale <= 0.01f)
            {
                UiScale = 2f;
            }

            var screen = camera.WorldToScreenPoint(worldPosition + new Vector3(0f, WorldHeightOffset, 0f));
            if (screen.z <= 0f)
            {
                return;
            }

            EnsureAssets();

            var scale = Mathf.Max(1f, UiScale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var invScale = 1f / scale;
            var remainingSeconds = Mathf.Max(0f, Client.SecondsUntilCoinDispenserSpawn);
            var cycle01 = 1f - Mathf.Clamp01(remainingSeconds / CoinDispenserIntervalSeconds);
            var wobble = Mathf.Sin(Time.unscaledTime * 7.2f) * 0.08f;
            var micro = Mathf.Sin(Time.unscaledTime * 13.7f) * 0.05f;
            var surge = Mathf.Sin(Time.unscaledTime * 24f) * 0.05f * cycle01;
            var scaleMul = 1f + wobble + micro + surge;
            var yBounce = (Mathf.Sin(Time.unscaledTime * 6.2f) * 7f) + (Mathf.Sin(Time.unscaledTime * 11.8f) * 3f);

            var fill01 = 1f - Mathf.Clamp01(remainingSeconds / CoinDispenserIntervalSeconds);
            UpdateRadialTexture(fill01);

            var iconSize = 20f * scaleMul;
            var gap = 8f;
            var totalWidth = (iconSize * 2f) + gap;
            var x = (screen.x * invScale) - (totalWidth * 0.5f);
            var y = ((Screen.height - screen.y) * invScale) - iconSize - 14f + (yBounce * invScale);

            var amountRect = new Rect(x, y, iconSize, iconSize);
            var radialRect = new Rect(x + iconSize + gap, y, iconSize, iconSize);
            GUI.DrawTexture(amountRect, _coinTexture, ScaleMode.StretchToFill, alphaBlend: true);
            GUI.DrawTexture(radialRect, _radialTexture, ScaleMode.StretchToFill, alphaBlend: true);

            var amount = Client.CoinDispenserStackAmount;
            var amountText = amount <= 99 ? amount.ToString() : "99+";
            var shadowRect = new Rect(amountRect.x + 1f, amountRect.y + 1f, amountRect.width, amountRect.height);
            GUI.Label(shadowRect, amountText, _amountShadowStyle);
            GUI.Label(amountRect, amountText, _amountStyle);

            GUI.matrix = previousMatrix;
        }

        private void EnsureAssets()
        {
            EnsureStyles();
            EnsureTextures();
        }

        private void EnsureStyles()
        {
            if (_amountStyle == null)
            {
                _amountStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
                _amountStyle.normal.textColor = new Color(0.08f, 0.07f, 0.04f, 1f);
            }

            if (_amountShadowStyle == null)
            {
                _amountShadowStyle = new GUIStyle(_amountStyle);
                _amountShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            }
        }

        private void EnsureTextures()
        {
            if (_coinTexture == null)
            {
                _coinTexture = CreateRuntimeTexture(CoinTextureSize, "CoinDispenser_Coin");
                RebuildCoinTexture();
            }

            if (_radialTexture == null)
            {
                _radialTexture = CreateRuntimeTexture(RadialTextureSize, "CoinDispenser_Radial");
                _radialPixels = new Color32[RadialTextureSize * RadialTextureSize];
                _cachedFill01 = -1f;
                UpdateRadialTexture(0f);
            }
            else if (_radialPixels == null || _radialPixels.Length != (RadialTextureSize * RadialTextureSize))
            {
                _radialPixels = new Color32[RadialTextureSize * RadialTextureSize];
                _cachedFill01 = -1f;
            }
        }

        private static Texture2D CreateRuntimeTexture(int size, string textureName)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, mipChain: false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture;
        }

        private void RebuildCoinTexture()
        {
            var size = CoinTextureSize;
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = center;
            var outlineRadius = radius - Mathf.Max(1f, size * 0.08f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var index = (y * size) + x;
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt((dx * dx) + (dy * dy));

                    if (distance > radius)
                    {
                        pixels[index] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    if (distance >= outlineRadius)
                    {
                        pixels[index] = (Color32)CoinEdgeColor;
                        continue;
                    }

                    var t = Mathf.Clamp01(distance / Mathf.Max(0.0001f, outlineRadius));
                    pixels[index] = (Color32)Color.Lerp(CoinCenterColor, CoinEdgeColor, t);
                }
            }

            _coinTexture.SetPixels32(pixels);
            _coinTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        private void UpdateRadialTexture(float fill01)
        {
            if (_radialTexture == null || _radialPixels == null)
            {
                return;
            }

            fill01 = Mathf.Clamp01(fill01);
            if (Mathf.Abs(fill01 - _cachedFill01) < 0.001f)
            {
                return;
            }

            _cachedFill01 = fill01;

            var size = RadialTextureSize;
            var center = (size - 1) * 0.5f;
            var radius = center;
            var outlineRadius = radius - Mathf.Max(1f, size * 0.1f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var index = (y * size) + x;
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt((dx * dx) + (dy * dy));

                    if (distance > radius)
                    {
                        _radialPixels[index] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    if (distance >= outlineRadius)
                    {
                        _radialPixels[index] = (Color32)GaugeOutlineColor;
                        continue;
                    }

                    var angle01 = ToClockwiseAngle01(dx, dy);
                    var filled = fill01 >= 0.999f || angle01 < fill01;
                    _radialPixels[index] = (Color32)(filled ? GaugeFillColor : GaugeEmptyColor);
                }
            }

            _radialTexture.SetPixels32(_radialPixels);
            _radialTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        private static float ToClockwiseAngle01(float dx, float dy)
        {
            var angle = Mathf.Atan2(dy, dx);
            var value = 0.25f - (angle / (Mathf.PI * 2f));
            if (value < 0f)
            {
                value += 1f;
            }

            return value;
        }

        private void ReleaseTextures()
        {
            ReleaseTexture(ref _coinTexture);
            ReleaseTexture(ref _radialTexture);
            _radialPixels = null;
            _cachedFill01 = -1f;
        }

        private static void ReleaseTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }

            texture = null;
        }
    }
}
