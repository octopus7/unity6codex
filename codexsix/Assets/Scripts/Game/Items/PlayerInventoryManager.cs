using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class PlayerInventoryManager : MonoBehaviour
    {
        public ItemDataManager ItemDataManager;
        public int DefaultSlotCount = 24;

        private readonly Dictionary<int, InventoryContainer> _inventoriesByOwnerId = new();

        public int InventoryOwnerCount => _inventoriesByOwnerId.Count;

        private void Awake()
        {
            if (ItemDataManager == null)
            {
                ItemDataManager = GetComponent<ItemDataManager>();
            }

            if (ItemDataManager != null && !ItemDataManager.IsLoaded)
            {
                ItemDataManager.Load();
            }
        }

        public bool TryGetInventory(int ownerId, out InventoryContainer inventory)
        {
            if (ownerId <= 0)
            {
                inventory = null;
                return false;
            }

            return _inventoriesByOwnerId.TryGetValue(ownerId, out inventory);
        }

        public InventoryContainer GetOrCreateInventory(int ownerId)
        {
            if (ownerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerId), "Owner id must be greater than zero.");
            }

            if (_inventoriesByOwnerId.TryGetValue(ownerId, out var existingInventory))
            {
                return existingInventory;
            }

            if (ItemDataManager == null)
            {
                throw new InvalidOperationException("PlayerInventoryManager requires ItemDataManager.");
            }

            var slotCount = Math.Max(1, DefaultSlotCount);
            var inventory = new InventoryContainer(slotCount, ItemDataManager);
            _inventoriesByOwnerId.Add(ownerId, inventory);
            return inventory;
        }

        public bool TryAddItem(int ownerId, int itemId, int quantity, out int remainingQuantity)
        {
            remainingQuantity = quantity;
            if (ownerId <= 0)
            {
                return false;
            }

            if (quantity <= 0)
            {
                remainingQuantity = 0;
                return true;
            }

            var inventory = GetOrCreateInventory(ownerId);
            return inventory.TryAddItem(itemId, quantity, out remainingQuantity);
        }

        public bool TryRemoveItem(int ownerId, int itemId, int quantity)
        {
            if (!TryGetInventory(ownerId, out var inventory))
            {
                return false;
            }

            return inventory.TryRemoveItem(itemId, quantity);
        }

        public int GetItemCount(int ownerId, int itemId)
        {
            return TryGetInventory(ownerId, out var inventory) ? inventory.GetItemCount(itemId) : 0;
        }

        public bool HasInventory(int ownerId)
        {
            return ownerId > 0 && _inventoriesByOwnerId.ContainsKey(ownerId);
        }

        public bool ClearInventory(int ownerId)
        {
            if (!TryGetInventory(ownerId, out var inventory))
            {
                return false;
            }

            inventory.Clear();
            return true;
        }

        public void RemoveInventory(int ownerId)
        {
            if (ownerId <= 0)
            {
                return;
            }

            _inventoriesByOwnerId.Remove(ownerId);
        }

        public void ClearAllInventories()
        {
            _inventoriesByOwnerId.Clear();
        }
    }
}
