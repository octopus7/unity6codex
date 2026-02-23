using CodexSix.TopdownShooter.Net;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class ShopPanelHud : MonoBehaviour
    {
        public NetworkGameClient Client;
        public int HealItemId = 1;
        public int SpeedItemId = 2;
        public float UiScale = 2f;

        private Rect _windowRect = new(12f, 320f, 300f, 92f);

        private void OnGUI()
        {
            if (Client == null)
            {
                return;
            }

            if (Client.CurrentConnectionState != ConnectionState.Connected || !Client.LocalInShopZone)
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

            _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Shop");
            GUI.matrix = previousMatrix;
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Buy Heal (5c)", GUILayout.Height(26f)))
            {
                Client.SendShopPurchase((byte)HealItemId);
            }

            if (GUILayout.Button("Buy Speed (8c)", GUILayout.Height(26f)))
            {
                Client.SendShopPurchase((byte)SpeedItemId);
            }

            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }
    }
}
