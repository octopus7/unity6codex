using System;
using System.Collections.Generic;
using System.Linq;

namespace CodexSix.TopdownShooter.Game
{
    [Serializable]
    public sealed class GrowthTreeRecord
    {
        public string treeId = "default";
        public int version = 1;
        public GrowthNodeRecord[] nodes;
    }

    [Serializable]
    public sealed class GrowthNodeRecord
    {
        public string id;
        public string name;
        public string description;
        public int tier;
        public string branchId;
        public float x;
        public float y;
        public int coinCost;
        public int gemCost;
        public string[] requiredNodeIds;
        public int requiredOpenedFromRequiredNodes;
        public int minOpenedNodesInLowerTiers;
    }

    public sealed class GrowthNodeDefinition
    {
        private readonly string[] _requiredNodeIds;

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Tier { get; }
        public string BranchId { get; }
        public float X { get; }
        public float Y { get; }
        public int CoinCost { get; }
        public int GemCost { get; }
        public IReadOnlyList<string> RequiredNodeIds => _requiredNodeIds;
        public int RequiredOpenedFromRequiredNodes { get; }
        public int MinOpenedNodesInLowerTiers { get; }

        public int RequiredNodeCount => _requiredNodeIds.Length;

        public GrowthNodeDefinition(
            string id,
            string name,
            string description,
            int tier,
            string branchId,
            float x,
            float y,
            int coinCost,
            int gemCost,
            string[] requiredNodeIds,
            int requiredOpenedFromRequiredNodes,
            int minOpenedNodesInLowerTiers)
        {
            Id = id;
            Name = name;
            Description = description;
            Tier = tier;
            BranchId = branchId;
            X = x;
            Y = y;
            CoinCost = coinCost;
            GemCost = gemCost;
            _requiredNodeIds = requiredNodeIds ?? Array.Empty<string>();
            RequiredOpenedFromRequiredNodes = requiredOpenedFromRequiredNodes;
            MinOpenedNodesInLowerTiers = minOpenedNodesInLowerTiers;
        }
    }

    public sealed class GrowthTreeDefinition
    {
        private readonly Dictionary<string, GrowthNodeDefinition> _nodesById;
        private readonly List<GrowthNodeDefinition> _nodes;

        public string TreeId { get; }
        public int Version { get; }
        public IReadOnlyList<GrowthNodeDefinition> Nodes => _nodes;
        public int MinimumTier { get; }
        public int MaximumTier { get; }

        public GrowthTreeDefinition(string treeId, int version, List<GrowthNodeDefinition> nodes)
        {
            TreeId = string.IsNullOrWhiteSpace(treeId) ? "default" : treeId;
            Version = version;
            _nodes = nodes ?? new List<GrowthNodeDefinition>(capacity: 0);
            _nodesById = new Dictionary<string, GrowthNodeDefinition>(_nodes.Count, StringComparer.Ordinal);

            var minimumTier = int.MaxValue;
            var maximumTier = int.MinValue;

            foreach (var node in _nodes)
            {
                _nodesById[node.Id] = node;
                if (node.Tier < minimumTier)
                {
                    minimumTier = node.Tier;
                }

                if (node.Tier > maximumTier)
                {
                    maximumTier = node.Tier;
                }
            }

            if (_nodes.Count == 0)
            {
                minimumTier = 0;
                maximumTier = 0;
            }

            MinimumTier = minimumTier;
            MaximumTier = maximumTier;
        }

        public bool TryGetNode(string nodeId, out GrowthNodeDefinition node)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                node = null;
                return false;
            }

            return _nodesById.TryGetValue(nodeId, out node);
        }

        public bool HasNode(string nodeId)
        {
            return !string.IsNullOrWhiteSpace(nodeId) && _nodesById.ContainsKey(nodeId);
        }

        public int CountOpenedNodesInLowerTiers(HashSet<string> unlockedNodeIds, int tierExclusive)
        {
            if (unlockedNodeIds == null || unlockedNodeIds.Count == 0)
            {
                return 0;
            }

            var count = 0;
            foreach (var nodeId in unlockedNodeIds)
            {
                if (!_nodesById.TryGetValue(nodeId, out var node))
                {
                    continue;
                }

                if (node.Tier < tierExclusive)
                {
                    count++;
                }
            }

            return count;
        }

        public IReadOnlyList<GrowthNodeDefinition> GetSortedByTierAndPosition()
        {
            return _nodes
                .OrderBy(node => node.Tier)
                .ThenBy(node => node.Y)
                .ThenBy(node => node.X)
                .ToList();
        }
    }

    public enum GrowthNodeVisualState
    {
        Unlocked = 0,
        Unlockable = 1,
        LockedPrerequisite = 2,
        LockedCost = 3
    }

    public readonly struct GrowthUnlockEvaluation
    {
        public GrowthUnlockEvaluation(
            bool isUnlocked,
            bool meetsPrerequisite,
            bool meetsCost,
            bool canUnlock,
            int openedRequiredCount,
            int requiredNodeCount,
            int openedLowerTierCount,
            GrowthNodeVisualState visualState,
            string blockedReason)
        {
            IsUnlocked = isUnlocked;
            MeetsPrerequisite = meetsPrerequisite;
            MeetsCost = meetsCost;
            CanUnlock = canUnlock;
            OpenedRequiredCount = openedRequiredCount;
            RequiredNodeCount = requiredNodeCount;
            OpenedLowerTierCount = openedLowerTierCount;
            VisualState = visualState;
            BlockedReason = blockedReason ?? string.Empty;
        }

        public bool IsUnlocked { get; }
        public bool MeetsPrerequisite { get; }
        public bool MeetsCost { get; }
        public bool CanUnlock { get; }
        public int OpenedRequiredCount { get; }
        public int RequiredNodeCount { get; }
        public int OpenedLowerTierCount { get; }
        public GrowthNodeVisualState VisualState { get; }
        public string BlockedReason { get; }
    }
}