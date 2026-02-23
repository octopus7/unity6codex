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

        private GUIStyle _textStyle;
        private GUIStyle _shadowStyle;

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

            EnsureStyles();

            var scale = Mathf.Max(1f, UiScale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var invScale = 1f / scale;
            var remainingSeconds = Mathf.Max(0f, Client.SecondsUntilCoinDispenserSpawn);
            var cycle01 = 1f - Mathf.Clamp01(remainingSeconds / 5f);
            var wobble = Mathf.Sin(Time.unscaledTime * 7.2f) * 0.08f;
            var micro = Mathf.Sin(Time.unscaledTime * 13.7f) * 0.05f;
            var surge = Mathf.Sin(Time.unscaledTime * 24f) * 0.05f * cycle01;
            var scaleMul = 1f + wobble + micro + surge;
            var yBounce = (Mathf.Sin(Time.unscaledTime * 6.2f) * 7f) + (Mathf.Sin(Time.unscaledTime * 11.8f) * 3f);

            var label = $"{Client.CoinDispenserStackAmount}c  +1 in {remainingSeconds:0.0}s";
            var baseWidth = 182f;
            var baseHeight = 24f;
            var width = baseWidth * scaleMul;
            var height = baseHeight * scaleMul;
            var x = (screen.x * invScale) - (width * 0.5f);
            var y = ((Screen.height - screen.y) * invScale) - height - 14f + (yBounce * invScale);
            var rect = new Rect(x, y, width, height);

            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), label, _shadowStyle);
            GUI.Label(rect, label, _textStyle);

            GUI.matrix = previousMatrix;
        }

        private void EnsureStyles()
        {
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                _textStyle.normal.textColor = new Color(1f, 0.92f, 0.32f, 1f);
            }

            if (_shadowStyle == null)
            {
                _shadowStyle = new GUIStyle(_textStyle);
                _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.92f);
            }
        }
    }
}
