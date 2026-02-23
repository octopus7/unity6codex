using CodexSix.TopdownShooter.Net;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class InventoryPanelHud : MonoBehaviour
    {
        public NetworkGameClient Client;
        public PlayerInventoryManager InventoryManager;
        public ItemDataManager ItemDataManager;
        public float UiScale = 2f;
        public int Columns = 6;
        public float SlotSize = 40f;

        private Rect _windowRect = new(12f, 412f, 340f, 226f);
        private GUIStyle _countStyle;
        private GUIStyle _countShadowStyle;

        private void Awake()
        {
            BindReferences();
        }

        private void OnGUI()
        {
            if (Client == null)
            {
                return;
            }

            if (Client.CurrentConnectionState != ConnectionState.Connected || Client.LocalPlayerId <= 0)
            {
                return;
            }

            BindReferences();
            if (InventoryManager == null || ItemDataManager == null)
            {
                return;
            }

            if (!InventoryManager.TryGetInventory(Client.LocalPlayerId, out var inventory) || inventory == null)
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

            EnsureStyles();
            _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, id => DrawWindow(id, inventory), "Inventory");

            GUI.matrix = previousMatrix;
        }

        private void DrawWindow(int windowId, InventoryContainer inventory)
        {
            var columns = Mathf.Max(1, Columns);
            var slotSize = Mathf.Max(20f, SlotSize);
            var slotCount = inventory.SlotCount;
            var rows = Mathf.CeilToInt(slotCount / (float)columns);

            GUILayout.Label($"Slots {slotCount - inventory.CountFreeSlots()}/{slotCount}");
            for (var row = 0; row < rows; row++)
            {
                GUILayout.BeginHorizontal();
                for (var column = 0; column < columns; column++)
                {
                    var slotIndex = (row * columns) + column;
                    var slotRect = GUILayoutUtility.GetRect(slotSize, slotSize, GUILayout.Width(slotSize), GUILayout.Height(slotSize));
                    if (slotIndex >= slotCount)
                    {
                        continue;
                    }

                    DrawSlot(slotRect, inventory.Slots[slotIndex]);
                }

                GUILayout.EndHorizontal();
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawSlot(Rect rect, InventorySlot slot)
        {
            GUI.Box(rect, GUIContent.none);
            if (slot == null || slot.IsEmpty)
            {
                return;
            }

            var iconTexture = ItemDataManager.GetItemIconOrNull(slot.ItemId);
            if (iconTexture != null)
            {
                var iconRect = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
                GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit, alphaBlend: true);
            }

            if (slot.Quantity > 1)
            {
                var countRect = new Rect(rect.x, rect.y + rect.height - 16f, rect.width - 4f, 14f);
                GUI.Label(new Rect(countRect.x + 1f, countRect.y + 1f, countRect.width, countRect.height), slot.Quantity.ToString(), _countShadowStyle);
                GUI.Label(countRect, slot.Quantity.ToString(), _countStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_countStyle == null)
            {
                _countStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.LowerRight,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
                _countStyle.normal.textColor = new Color(1f, 1f, 1f, 1f);
            }

            if (_countShadowStyle == null)
            {
                _countShadowStyle = new GUIStyle(_countStyle);
                _countShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
            }
        }

        private void BindReferences()
        {
            if (Client == null)
            {
                Client = FindFirstObjectByType<NetworkGameClient>();
            }

            if (InventoryManager == null)
            {
                InventoryManager = FindFirstObjectByType<PlayerInventoryManager>();
            }

            if (ItemDataManager == null)
            {
                ItemDataManager = FindFirstObjectByType<ItemDataManager>();
            }
        }
    }
}
