using CodexSix.TopdownShooter.Net;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class SimpleDebugHud : MonoBehaviour
    {
        public NetworkGameClient Client;
        public int HealItemId = 1;
        public int SpeedItemId = 2;
        public float UiScale = 2f;

        private string _host = "127.0.0.1";
        private string _port = "7777";
        private string _nickname = "Player";
        private Rect _windowRect = new(12f, 12f, 360f, 290f);

        private void OnGUI()
        {
            if (UiScale <= 0.01f)
            {
                UiScale = 2f;
            }

            var scale = Mathf.Max(1f, UiScale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "TopDown Shooter Debug");

            GUI.matrix = previousMatrix;
        }

        private void DrawWindow(int id)
        {
            if (Client == null)
            {
                GUILayout.Label("Client reference is not assigned.");
                GUI.DragWindow();
                return;
            }

            GUILayout.Label("TCP Server");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Host", GUILayout.Width(50));
            _host = GUILayout.TextField(_host, GUILayout.Width(190));
            GUILayout.Label("Port", GUILayout.Width(40));
            _port = GUILayout.TextField(_port, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Nick", GUILayout.Width(50));
            _nickname = GUILayout.TextField(_nickname, GUILayout.Width(190));
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            using (new GUIEnabledScope(Client.CurrentConnectionState != ConnectionState.Connecting))
            {
                if (GUILayout.Button("Connect", GUILayout.Height(28f)))
                {
                    var port = 7777;
                    int.TryParse(_port, out port);
                    Client.Connect(_host, port, _nickname);
                }
            }

            using (new GUIEnabledScope(Client.CurrentConnectionState == ConnectionState.Connected))
            {
                if (GUILayout.Button("Disconnect", GUILayout.Height(28f)))
                {
                    Client.Disconnect();
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label($"State: {Client.CurrentConnectionState}");
            GUILayout.Label($"Local Player: {Client.LocalPlayerId}");
            GUILayout.Label($"HP: {Client.LocalHp}");
            GUILayout.Label($"Coins: {Client.LocalCoins}");
            GUILayout.Label($"Ping: {Client.LastPingMs} ms");

            GUILayout.Space(6f);
            GUILayout.Label("Top Coins:");
            GUILayout.TextArea(Client.LeaderboardText, GUILayout.Height(62f));

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            using (new GUIEnabledScope(Client.CurrentConnectionState == ConnectionState.Connected))
            {
                if (GUILayout.Button("Buy Heal (5c)", GUILayout.Height(26f)))
                {
                    Client.SendShopPurchase((byte)HealItemId);
                }

                if (GUILayout.Button("Buy Speed (8c)", GUILayout.Height(26f)))
                {
                    Client.SendShopPurchase((byte)SpeedItemId);
                }
            }

            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private readonly struct GUIEnabledScope : System.IDisposable
        {
            private readonly bool _previous;

            public GUIEnabledScope(bool enabled)
            {
                _previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = _previous;
            }
        }
    }
}
