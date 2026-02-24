using System;
using System.Collections.Generic;

namespace CodexSix.TopdownShooter.Game
{
    public static class GrowthUnlockEvaluator
    {
        public static GrowthUnlockEvaluation EvaluateNode(
            GrowthTreeDefinition tree,
            GrowthNodeDefinition node,
            HashSet<string> unlockedNodeIds,
            int availableCoins,
            int gemBalance)
        {
            if (node == null)
            {
                return new GrowthUnlockEvaluation(
                    isUnlocked: false,
                    meetsPrerequisite: false,
                    meetsCost: false,
                    canUnlock: false,
                    openedRequiredCount: 0,
                    requiredNodeCount: 0,
                    openedLowerTierCount: 0,
                    visualState: GrowthNodeVisualState.LockedPrerequisite,
                    blockedReason: "Node is null.");
            }

            unlockedNodeIds ??= new HashSet<string>(StringComparer.Ordinal);

            var isUnlocked = unlockedNodeIds.Contains(node.Id);
            var openedRequiredCount = CountOpenedRequiredNodes(node, unlockedNodeIds);
            var requiredNodeCount = node.RequiredNodeCount;
            var meetsRequiredNodeCount = openedRequiredCount >= node.RequiredOpenedFromRequiredNodes;

            var openedLowerTierCount = tree != null
                ? tree.CountOpenedNodesInLowerTiers(unlockedNodeIds, node.Tier)
                : 0;
            var meetsLowerTierCount = openedLowerTierCount >= node.MinOpenedNodesInLowerTiers;

            var meetsPrerequisite = meetsRequiredNodeCount && meetsLowerTierCount;
            var meetsCost = availableCoins >= node.CoinCost && gemBalance >= node.GemCost;
            var canUnlock = !isUnlocked && meetsPrerequisite && meetsCost;

            var visualState = ResolveVisualState(isUnlocked, meetsPrerequisite, meetsCost);
            var blockedReason = ResolveBlockedReason(node, isUnlocked, meetsRequiredNodeCount, meetsLowerTierCount, meetsCost);

            return new GrowthUnlockEvaluation(
                isUnlocked: isUnlocked,
                meetsPrerequisite: meetsPrerequisite,
                meetsCost: meetsCost,
                canUnlock: canUnlock,
                openedRequiredCount: openedRequiredCount,
                requiredNodeCount: requiredNodeCount,
                openedLowerTierCount: openedLowerTierCount,
                visualState: visualState,
                blockedReason: blockedReason);
        }

        private static int CountOpenedRequiredNodes(GrowthNodeDefinition node, HashSet<string> unlockedNodeIds)
        {
            var count = 0;
            var requiredNodeIds = node.RequiredNodeIds;
            for (var i = 0; i < requiredNodeIds.Count; i++)
            {
                if (unlockedNodeIds.Contains(requiredNodeIds[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static GrowthNodeVisualState ResolveVisualState(bool isUnlocked, bool meetsPrerequisite, bool meetsCost)
        {
            if (isUnlocked)
            {
                return GrowthNodeVisualState.Unlocked;
            }

            if (!meetsPrerequisite)
            {
                return GrowthNodeVisualState.LockedPrerequisite;
            }

            if (!meetsCost)
            {
                return GrowthNodeVisualState.LockedCost;
            }

            return GrowthNodeVisualState.Unlockable;
        }

        private static string ResolveBlockedReason(
            GrowthNodeDefinition node,
            bool isUnlocked,
            bool meetsRequiredNodeCount,
            bool meetsLowerTierCount,
            bool meetsCost)
        {
            if (isUnlocked)
            {
                return "Already unlocked.";
            }

            if (!meetsRequiredNodeCount)
            {
                return "Required predecessor count is not met.";
            }

            if (!meetsLowerTierCount)
            {
                return "Lower-tier opened count is not met.";
            }

            if (!meetsCost)
            {
                if (node.CoinCost > 0 && node.GemCost > 0)
                {
                    return "Not enough coins and gems.";
                }

                if (node.CoinCost > 0)
                {
                    return "Not enough coins.";
                }

                if (node.GemCost > 0)
                {
                    return "Not enough gems.";
                }
            }

            return string.Empty;
        }
    }
}
