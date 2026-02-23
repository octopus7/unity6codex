using System.Collections.Generic;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class PlayerOverheadHealthHud : MonoBehaviour
    {
        private const float MaxHp = 100f;
        private static readonly Color BackColor = new(0f, 0f, 0f, 0.75f);
        private static readonly Color LocalBarColor = new(0.2f, 0.9f, 0.2f, 0.95f);
        private static readonly Color RemoteBarColor = new(0.2f, 0.8f, 1f, 0.95f);

        public NetworkGameClient Client;
        public Camera WorldCamera;
        public float UiScale = 2f;
        public float VerticalOffset = 2.1f;
        public float BarWidth = 44f;
        public float BarHeight = 6f;

        private readonly List<NetworkGameClient.PlayerHudEntry> _entries = new();

        private void OnGUI()
        {
            if (Client == null)
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

            var scale = Mathf.Max(1f, UiScale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var invScale = 1f / scale;
            Client.FillPlayerHudEntries(_entries);

            var width = Mathf.Max(10f, BarWidth);
            var height = Mathf.Max(3f, BarHeight);
            var innerHeight = Mathf.Max(1f, height - 2f);

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!entry.IsAlive)
                {
                    continue;
                }

                var world = entry.WorldPosition + new Vector3(0f, VerticalOffset, 0f);
                var screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                {
                    continue;
                }

                var x = (screen.x * invScale) - (width * 0.5f);
                var y = ((Screen.height - screen.y) * invScale) - height - 2f;
                var backRect = new Rect(x, y, width, height);

                DrawRect(backRect, BackColor);
                var fill = Mathf.Clamp01(entry.Hp / MaxHp);
                if (fill <= 0f)
                {
                    continue;
                }

                var fillRect = new Rect(x + 1f, y + 1f, (width - 2f) * fill, innerHeight);
                DrawRect(fillRect, entry.IsLocalPlayer ? LocalBarColor : RemoteBarColor);
            }

            GUI.matrix = previousMatrix;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
