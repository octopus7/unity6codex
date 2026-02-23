using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class LeaderboardPanelHud : MonoBehaviour
    {
        public NetworkGameClient Client;
        public float UiScale = 2f;

        private void OnGUI()
        {
            if (Client == null || Client.CurrentConnectionState != CodexSix.TopdownShooter.Net.ConnectionState.Connected)
            {
                return;
            }

            if (!IsLeaderboardKeyHeld())
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

            var viewWidth = Screen.width / scale;
            var viewHeight = Screen.height / scale;

            const float panelWidth = 320f;
            const float panelHeight = 188f;
            var rect = new Rect(
                (viewWidth - panelWidth) * 0.5f,
                (viewHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUILayout.BeginArea(rect, "Top Coins", GUI.skin.window);
            var totalPlayers = Client != null ? Client.CurrentPlayerCount : 0;
            GUILayout.Label($"Players: {totalPlayers}");
            var leaderboard = Client != null ? Client.LeaderboardText : "-";
            GUILayout.TextArea(leaderboard, GUILayout.Height(118f));
            GUILayout.EndArea();

            GUI.matrix = previousMatrix;
        }

        private static bool IsLeaderboardKeyHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.tabKey.isPressed)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Tab);
#else
            return false;
#endif
        }
    }
}
