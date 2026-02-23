using System;
using System.Collections.Generic;

namespace CodexSix.TopdownShooter.Game
{
    [Serializable]
    public sealed class InventorySlot
    {
        public int ItemId;
        public int Quantity;

        public bool IsEmpty => ItemId <= 0 || Quantity <= 0;

        public void Set(int itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public void Clear()
        {
            ItemId = 0;
            Quantity = 0;
        }
    }

    public sealed class InventoryContainer
    {
        private readonly InventorySlot[] _slots;
        private readonly ItemDataManager _itemDataManager;

        public int SlotCount => _slots.Length;
        public IReadOnlyList<InventorySlot> Slots => _slots;

        public InventoryContainer(int slotCount, ItemDataManager itemDataManager)
        {
            if (slotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), "Slot count must be greater than zero.");
            }

            _itemDataManager = itemDataManager ?? throw new ArgumentNullException(nameof(itemDataManager));
            _slots = new InventorySlot[slotCount];
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new InventorySlot();
            }
        }

        public bool TryAddItem(int itemId, int quantity, out int remainingQuantity)
        {
            remainingQuantity = quantity;
            if (quantity <= 0)
            {
                remainingQuantity = 0;
                return true;
            }

            if (!_itemDataManager.TryGetItem(itemId, out var itemDefinition))
            {
                return false;
            }

            if (itemDefinition.Stackable)
            {
                for (var i = 0; i < _slots.Length && remainingQuantity > 0; i++)
                {
                    var slot = _slots[i];
                    if (slot.IsEmpty || slot.ItemId != itemId)
                    {
                        continue;
                    }

                    var availableSpace = itemDefinition.MaxStack - slot.Quantity;
                    if (availableSpace <= 0)
                    {
                        continue;
                    }

                    var addAmount = Math.Min(availableSpace, remainingQuantity);
                    slot.Quantity += addAmount;
                    remainingQuantity -= addAmount;
                }
            }

            while (remainingQuantity > 0)
            {
                var emptySlotIndex = FindFirstEmptySlot();
                if (emptySlotIndex < 0)
                {
                    break;
                }

                var addAmount = itemDefinition.Stackable
                    ? Math.Min(itemDefinition.MaxStack, remainingQuantity)
                    : 1;

                _slots[emptySlotIndex].Set(itemId, addAmount);
                remainingQuantity -= addAmount;
            }

            return remainingQuantity <= 0;
        }

        public bool TryRemoveItem(int itemId, int quantity)
        {
            if (quantity <= 0)
            {
                return true;
            }

            if (GetItemCount(itemId) < quantity)
            {
                return false;
            }

            var remainingToRemove = quantity;
            for (var i = 0; i < _slots.Length && remainingToRemove > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.ItemId != itemId)
                {
                    continue;
                }

                var removeAmount = Math.Min(slot.Quantity, remainingToRemove);
                slot.Quantity -= removeAmount;
                remainingToRemove -= removeAmount;
                if (slot.Quantity <= 0)
                {
                    slot.Clear();
                }
            }

            return remainingToRemove == 0;
        }

        public int GetItemCount(int itemId)
        {
            var total = 0;
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.ItemId != itemId)
                {
                    continue;
                }

                total += slot.Quantity;
            }

            return total;
        }

        public int CountFreeSlots()
        {
            var freeCount = 0;
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    freeCount++;
                }
            }

            return freeCount;
        }

        public bool TryGetSlot(int slotIndex, out InventorySlot slot)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
            {
                slot = null;
                return false;
            }

            slot = _slots[slotIndex];
            return true;
        }

        public void Clear()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i].Clear();
            }
        }

        private int FindFirstEmptySlot()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
