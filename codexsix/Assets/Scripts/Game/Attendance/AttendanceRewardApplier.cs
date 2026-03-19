using System.Collections.Generic;
using System.Text;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class AttendanceRewardApplier
    {
        private readonly NetworkGameClient _client;
        private readonly GrowthProgressionManager _growthProgression;
        private readonly PlayerInventoryManager _inventoryManager;
        private readonly ItemDataManager _itemDataManager;

        public AttendanceRewardApplier(
            NetworkGameClient client,
            GrowthProgressionManager growthProgression,
            PlayerInventoryManager inventoryManager,
            ItemDataManager itemDataManager)
        {
            _client = client;
            _growthProgression = growthProgression;
            _inventoryManager = inventoryManager;
            _itemDataManager = itemDataManager;
        }

        public void ApplyRewards(IReadOnlyList<AttendanceRewardDefinition> rewards, out string summary)
        {
            var builder = new StringBuilder();
            if (rewards == null || rewards.Count == 0)
            {
                summary = "No rewards configured.";
                return;
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null || reward.Amount <= 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                ApplyReward(reward, builder);
            }

            summary = builder.Length > 0 ? builder.ToString() : "No rewards applied.";
        }

        public string FormatRewardSummary(IReadOnlyList<AttendanceRewardDefinition> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return "No rewards";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null || reward.Amount <= 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" + ");
                }

                builder.Append(FormatSingleReward(reward));
            }

            return builder.Length > 0 ? builder.ToString() : "No rewards";
        }

        private void ApplyReward(AttendanceRewardDefinition reward, StringBuilder summaryBuilder)
        {
            switch (reward.Kind)
            {
                case AttendanceRewardKind.Coins:
                    _client?.AddLocalBonusCoins(reward.Amount);
                    summaryBuilder.Append(FormatSingleReward(reward));
                    break;
                case AttendanceRewardKind.Gems:
                    _growthProgression?.AddGems(reward.Amount);
                    summaryBuilder.Append(FormatSingleReward(reward));
                    break;
                case AttendanceRewardKind.Item:
                    ApplyItemReward(reward, summaryBuilder);
                    break;
                default:
                    summaryBuilder.Append(FormatSingleReward(reward));
                    break;
            }
        }

        private void ApplyItemReward(AttendanceRewardDefinition reward, StringBuilder summaryBuilder)
        {
            var displayName = ResolveItemName(reward.ItemId);
            if (_client == null || _client.LocalPlayerId <= 0 || _inventoryManager == null)
            {
                summaryBuilder.Append($"{displayName} x{reward.Amount} (inventory unavailable)");
                return;
            }

            if (!_inventoryManager.TryAddItem(_client.LocalPlayerId, reward.ItemId, reward.Amount, out var remainingQuantity))
            {
                summaryBuilder.Append($"{displayName} x{reward.Amount} (failed)");
                return;
            }

            var acceptedQuantity = reward.Amount - remainingQuantity;
            if (remainingQuantity > 0)
            {
                summaryBuilder.Append($"{displayName} x{acceptedQuantity} (partial)");
                return;
            }

            summaryBuilder.Append($"{displayName} x{reward.Amount}");
        }

        private string FormatSingleReward(AttendanceRewardDefinition reward)
        {
            if (!string.IsNullOrWhiteSpace(reward.OverrideLabel))
            {
                return reward.OverrideLabel.Trim();
            }

            return reward.Kind switch
            {
                AttendanceRewardKind.Coins => $"Coins +{reward.Amount}",
                AttendanceRewardKind.Gems => $"Gems +{reward.Amount}",
                AttendanceRewardKind.Item => $"{ResolveItemName(reward.ItemId)} x{reward.Amount}",
                _ => $"Reward x{reward.Amount}"
            };
        }

        private string ResolveItemName(int itemId)
        {
            if (_itemDataManager != null && _itemDataManager.TryGetItem(itemId, out var definition) && definition != null)
            {
                return definition.Name;
            }

            return itemId > 0 ? $"Item {itemId}" : "Item";
        }
    }
}
