using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public enum InventoryItemKind
    {
        Unknown = 0,
        Consumable = 1,
        Equipment = 2
    }

    public sealed class ItemDefinition
    {
        public int Id { get; }
        public int CategoryBand { get; }
        public string Name { get; }
        public string Category { get; }
        public InventoryItemKind Kind { get; }
        public bool Stackable { get; }
        public int MaxStack { get; }
        public string Description { get; }

        public bool IsEquipment => Kind == InventoryItemKind.Equipment;

        public ItemDefinition(
            int id,
            int categoryBand,
            string name,
            string category,
            InventoryItemKind kind,
            bool stackable,
            int maxStack,
            string description)
        {
            Id = id;
            CategoryBand = categoryBand;
            Name = name;
            Category = category;
            Kind = kind;
            Stackable = stackable;
            MaxStack = maxStack;
            Description = description;
        }
    }

    public sealed class ItemDataManager : MonoBehaviour
    {
        public TextAsset ItemCatalogJson;
        public string ResourcesCatalogPath = "Items/item_catalog";
        public bool LoadOnAwake = true;

        private readonly Dictionary<int, ItemDefinition> _itemsById = new();
        private bool _loadAttempted;

        public bool IsLoaded { get; private set; }
        public int ItemCount => _itemsById.Count;

        private void Awake()
        {
            if (LoadOnAwake)
            {
                Load();
            }
        }

        public bool Load()
        {
            _loadAttempted = true;
            _itemsById.Clear();
            IsLoaded = false;

            var catalogText = ResolveCatalogText();
            if (string.IsNullOrWhiteSpace(catalogText))
            {
                Debug.LogWarning("ItemDataManager could not find item catalog json.");
                return false;
            }

            ItemCatalogRoot catalog;
            try
            {
                catalog = JsonUtility.FromJson<ItemCatalogRoot>(catalogText);
            }
            catch (Exception exception)
            {
                Debug.LogError($"ItemDataManager failed to parse item catalog json: {exception.Message}");
                return false;
            }

            if (catalog == null || catalog.items == null || catalog.items.Length == 0)
            {
                Debug.LogWarning("ItemDataManager item catalog is empty.");
                return false;
            }

            for (var i = 0; i < catalog.items.Length; i++)
            {
                var record = catalog.items[i];
                if (record == null)
                {
                    continue;
                }

                if (record.id <= 0)
                {
                    Debug.LogWarning($"ItemDataManager ignored invalid item id at index {i}.");
                    continue;
                }

                var definition = BuildDefinition(record);
                if (_itemsById.ContainsKey(definition.Id))
                {
                    Debug.LogWarning($"ItemDataManager ignored duplicate item id {definition.Id}.");
                    continue;
                }

                _itemsById.Add(definition.Id, definition);
            }

            IsLoaded = _itemsById.Count > 0;
            if (IsLoaded)
            {
                Debug.Log($"ItemDataManager loaded {_itemsById.Count} items.");
            }

            return IsLoaded;
        }

        public bool TryGetItem(int itemId, out ItemDefinition definition)
        {
            if (_itemsById.TryGetValue(itemId, out definition))
            {
                return true;
            }

            if (!_loadAttempted)
            {
                Load();
                return _itemsById.TryGetValue(itemId, out definition);
            }

            definition = null;
            return false;
        }

        public ItemDefinition GetItemOrNull(int itemId)
        {
            return TryGetItem(itemId, out var definition) ? definition : null;
        }

        private string ResolveCatalogText()
        {
            if (ItemCatalogJson != null && !string.IsNullOrWhiteSpace(ItemCatalogJson.text))
            {
                return ItemCatalogJson.text;
            }

            if (string.IsNullOrWhiteSpace(ResourcesCatalogPath))
            {
                return string.Empty;
            }

            var resourceAsset = Resources.Load<TextAsset>(ResourcesCatalogPath);
            return resourceAsset != null ? resourceAsset.text : string.Empty;
        }

        private static ItemDefinition BuildDefinition(ItemRecord record)
        {
            var inferredBand = (record.id / 10000) * 10000;
            var categoryBand = record.band > 0 ? record.band : inferredBand;
            if (categoryBand != inferredBand)
            {
                Debug.LogWarning(
                    $"ItemDataManager item {record.id} band {categoryBand} mismatched inferred band {inferredBand}.");
            }

            var kind = ParseKind(record.kind);
            var stackable = kind != InventoryItemKind.Equipment && record.stackable;
            var maxStack = stackable ? ClampInt(record.maxStack, 1, 999) : 1;
            var itemName = string.IsNullOrWhiteSpace(record.name) ? $"Item_{record.id}" : record.name.Trim();
            var category = string.IsNullOrWhiteSpace(record.category) ? "General" : record.category.Trim();
            var description = string.IsNullOrWhiteSpace(record.description) ? string.Empty : record.description.Trim();

            return new ItemDefinition(
                record.id,
                categoryBand,
                itemName,
                category,
                kind,
                stackable,
                maxStack,
                description);
        }

        private static InventoryItemKind ParseKind(string rawKind)
        {
            if (string.IsNullOrWhiteSpace(rawKind))
            {
                return InventoryItemKind.Consumable;
            }

            return Enum.TryParse(rawKind.Trim(), ignoreCase: true, out InventoryItemKind parsedKind)
                ? parsedKind
                : InventoryItemKind.Consumable;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        [Serializable]
        private sealed class ItemCatalogRoot
        {
            public ItemRecord[] items;
        }

        [Serializable]
        private sealed class ItemRecord
        {
            public int id;
            public int band;
            public string name;
            public string category;
            public string kind;
            public bool stackable = true;
            public int maxStack = 20;
            public string description;
        }
    }
}
