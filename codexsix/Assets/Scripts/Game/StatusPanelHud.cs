using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class StatusPanelHud : MonoBehaviour
    {
        public NetworkGameClient Client;
        public GrowthPanelHud GrowthPanel;
        public float UiScale = 2f;

        private bool _overheadHudBound;
        private GUIStyle _backgroundStyle;
        private GUIStyle _leftStyle;
        private GUIStyle _rightStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _leftShadowStyle;
        private GUIStyle _rightShadowStyle;
        private GUIStyle _centerShadowStyle;

        private void OnGUI()
        {
            if (Client == null)
            {
                return;
            }

            if (Client.CurrentConnectionState != CodexSix.TopdownShooter.Net.ConnectionState.Connected)
            {
                return;
            }

            EnsureOverheadHealthHudBound();

            if (UiScale <= 0.01f)
            {
                UiScale = 2f;
            }

            var scale = Mathf.Max(1f, UiScale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var viewWidth = Screen.width / scale;
            var viewHeight = Screen.height / scale;

            const float panelWidth = 340f;
            const float panelHeight = 24f;
            const float margin = 12f;

            var rect = new Rect(
                viewWidth - panelWidth - margin,
                viewHeight - panelHeight - margin,
                panelWidth,
                panelHeight);

            EnsureStyles();
            var backgroundRect = new Rect(rect.x - 8f, rect.y - 2f, rect.width + 16f, rect.height + 4f);
            DrawBackgroundBox(backgroundRect);

            const float pingWidth = 84f;
            const float separatorWidth = 12f;
            const float coinsValueWidth = 72f;
            const float coinsLabelWidth = 52f;
            const float growthButtonWidth = 72f;
            const float spacing = 4f;

            var pingRect = new Rect(rect.xMax - pingWidth, rect.y, pingWidth, rect.height);
            var separatorRect = new Rect(pingRect.x - separatorWidth - spacing, rect.y, separatorWidth, rect.height);
            var coinsValueRect = new Rect(separatorRect.x - coinsValueWidth - spacing, rect.y, coinsValueWidth, rect.height);
            var coinsLabelRect = new Rect(coinsValueRect.x - coinsLabelWidth, rect.y, coinsLabelWidth, rect.height);
            var growthButtonRect = new Rect(coinsLabelRect.x - growthButtonWidth - spacing, rect.y + 1f, growthButtonWidth, rect.height - 2f);

            if (GrowthPanel == null)
            {
                GrowthPanel = Object.FindFirstObjectByType<GrowthPanelHud>();
            }

            if (GrowthPanel != null && GUI.Button(growthButtonRect, "Growth"))
            {
                GrowthPanel.ToggleVisibility();
            }

            DrawShadowedLabel(coinsLabelRect, "Coins", _leftStyle, _leftShadowStyle);
            DrawShadowedLabel(coinsValueRect, Client.LocalCoins.ToString(), _rightStyle, _rightShadowStyle);
            DrawShadowedLabel(separatorRect, "|", _centerStyle, _centerShadowStyle);
            DrawShadowedLabel(pingRect, $"{Client.LastPingMs} ms", _rightStyle, _rightShadowStyle);

            GUI.matrix = previousMatrix;
        }

        private void EnsureOverheadHealthHudBound()
        {
            if (_overheadHudBound)
            {
                return;
            }

            var overheadHud = Object.FindFirstObjectByType<PlayerOverheadHealthHud>();
            if (overheadHud == null)
            {
                overheadHud = gameObject.AddComponent<PlayerOverheadHealthHud>();
            }

            overheadHud.Client = Client;
            overheadHud.UiScale = UiScale;
            _overheadHudBound = true;
        }

        private void EnsureStyles()
        {
            if (_backgroundStyle == null)
            {
                _backgroundStyle = new GUIStyle(GUI.skin.window)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = GUI.skin.window.border
                };
            }

            if (_leftStyle == null)
            {
                _leftStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                _leftStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            }

            if (_rightStyle == null)
            {
                _rightStyle = new GUIStyle(_leftStyle)
                {
                    alignment = TextAnchor.MiddleRight
                };
            }

            if (_centerStyle == null)
            {
                _centerStyle = new GUIStyle(_leftStyle)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (_leftShadowStyle == null)
            {
                _leftShadowStyle = new GUIStyle(_leftStyle);
                _leftShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }

            if (_rightShadowStyle == null)
            {
                _rightShadowStyle = new GUIStyle(_rightStyle);
                _rightShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }

            if (_centerShadowStyle == null)
            {
                _centerShadowStyle = new GUIStyle(_centerStyle);
                _centerShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }
        }

        private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style, GUIStyle shadowStyle)
        {
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
            GUI.Label(rect, text, style);
        }

        private void DrawBackgroundBox(Rect rect)
        {
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.32f);
            GUI.Box(rect, GUIContent.none, _backgroundStyle);
            GUI.color = previousColor;
        }
    }
}
