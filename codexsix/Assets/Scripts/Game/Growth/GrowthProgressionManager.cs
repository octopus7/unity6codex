using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class GrowthProgressionManager : MonoBehaviour
    {
        public const string PrefPrefix = "codexsix.growth";
        public const string UnlockedIdsPrefKey = PrefPrefix + ".unlocked_ids";
        public const string GemsPrefKey = PrefPrefix + ".gems";
        public const string CoinSpentPrefKey = PrefPrefix + ".coin_spent";

        public NetworkGameClient Client;
        public GrowthTreeDataManager TreeDataManager;
        public int DefaultGemBalance = 12;
        public bool LoadOnAwake = true;

        private readonly HashSet<string> _unlockedNodeIds = new(StringComparer.Ordinal);
        private bool _progressLoaded;
        private int _gemBalance;
        private int _coinSpent;

        public event Action ProgressChanged;

        public int GemBalance => _gemBalance;
        public int CoinSpent => _coinSpent;
        public int AvailableCoins
        {
            get
            {
                var baseCoins = Client != null ? Mathf.Max(0, Client.LocalCoins) : 0;
                return Mathf.Max(0, baseCoins - _coinSpent);
            }
        }

        public int UnlockedCount => _unlockedNodeIds.Count;
        public IReadOnlyCollection<string> UnlockedNodeIds => _unlockedNodeIds;

        private void Awake()
        {
            BindReferences();
            if (LoadOnAwake)
            {
                LoadProgress();
            }
        }

        private void OnEnable()
        {
            if (!_progressLoaded && LoadOnAwake)
            {
                LoadProgress();
            }
        }

        public void LoadProgress()
        {
            BindReferences();
            _unlockedNodeIds.Clear();

            _gemBalance = Mathf.Max(0, PlayerPrefs.GetInt(GemsPrefKey, Mathf.Max(0, DefaultGemBalance)));
            _coinSpent = Mathf.Max(0, PlayerPrefs.GetInt(CoinSpentPrefKey, 0));

            var serializedUnlockedIds = PlayerPrefs.GetString(UnlockedIdsPrefKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(serializedUnlockedIds))
            {
                var tokens = serializedUnlockedIds.Split('|');
                for (var i = 0; i < tokens.Length; i++)
                {
                    var nodeId = tokens[i].Trim();
                    if (string.IsNullOrEmpty(nodeId))
                    {
                        continue;
                    }

                    _unlockedNodeIds.Add(nodeId);
                }
            }

            SanitizeUnlockedNodesAgainstTree();
            _progressLoaded = true;
            ProgressChanged?.Invoke();
        }

        public void SaveProgress()
        {
            if (!_progressLoaded)
            {
                _progressLoaded = true;
            }

            var serializedUnlockedIds = _unlockedNodeIds.Count == 0
                ? string.Empty
                : string.Join("|", _unlockedNodeIds.OrderBy(id => id));

            PlayerPrefs.SetString(UnlockedIdsPrefKey, serializedUnlockedIds);
            PlayerPrefs.SetInt(GemsPrefKey, Mathf.Max(0, _gemBalance));
            PlayerPrefs.SetInt(CoinSpentPrefKey, Mathf.Max(0, _coinSpent));
            PlayerPrefs.Save();
        }

        public bool IsUnlocked(string nodeId)
        {
            return !string.IsNullOrWhiteSpace(nodeId) && _unlockedNodeIds.Contains(nodeId);
        }

        public bool TryUnlockNode(string nodeId, out string message)
        {
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(nodeId))
            {
                message = "Node id is empty.";
                return false;
            }

            if (!EnsureTreeLoaded(out var tree))
            {
                message = "Growth tree data is not loaded.";
                return false;
            }

            if (!tree.TryGetNode(nodeId, out var node))
            {
                message = $"Node '{nodeId}' does not exist.";
                return false;
            }

            var evaluation = GrowthUnlockEvaluator.EvaluateNode(tree, node, _unlockedNodeIds, AvailableCoins, GemBalance);
            if (!evaluation.CanUnlock)
            {
                message = string.IsNullOrWhiteSpace(evaluation.BlockedReason)
                    ? "Node cannot be unlocked yet."
                    : evaluation.BlockedReason;
                return false;
            }

            if (!_unlockedNodeIds.Add(nodeId))
            {
                message = "Node already unlocked.";
                return false;
            }

            _coinSpent += Mathf.Max(0, node.CoinCost);
            _gemBalance = Mathf.Max(0, _gemBalance - Mathf.Max(0, node.GemCost));

            SaveProgress();
            ProgressChanged?.Invoke();

            message = $"Unlocked {node.Name}.";
            return true;
        }

        public GrowthUnlockEvaluation EvaluateNode(string nodeId)
        {
            if (!EnsureTreeLoaded(out var tree) || !tree.TryGetNode(nodeId, out var node))
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
                    blockedReason: "Node is missing.");
            }

            return GrowthUnlockEvaluator.EvaluateNode(tree, node, _unlockedNodeIds, AvailableCoins, GemBalance);
        }

        public void AddGems(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            _gemBalance = Mathf.Max(0, _gemBalance + amount);
            SaveProgress();
            ProgressChanged?.Invoke();
        }

        public void ResetProgress(bool keepGems = true)
        {
            _unlockedNodeIds.Clear();
            _coinSpent = 0;
            if (!keepGems)
            {
                _gemBalance = Mathf.Max(0, DefaultGemBalance);
            }

            SaveProgress();
            ProgressChanged?.Invoke();
        }

        public HashSet<string> CreateUnlockedSnapshot()
        {
            return new HashSet<string>(_unlockedNodeIds, StringComparer.Ordinal);
        }

        private bool EnsureTreeLoaded(out GrowthTreeDefinition tree)
        {
            tree = null;
            BindReferences();

            if (TreeDataManager == null)
            {
                return false;
            }

            if (!TreeDataManager.EnsureLoaded())
            {
                return false;
            }

            tree = TreeDataManager.Tree;
            return tree != null;
        }

        private void SanitizeUnlockedNodesAgainstTree()
        {
            if (!EnsureTreeLoaded(out var tree))
            {
                return;
            }

            if (_unlockedNodeIds.Count == 0)
            {
                return;
            }

            var removedAny = false;
            var toRemove = new List<string>();
            foreach (var nodeId in _unlockedNodeIds)
            {
                if (tree.HasNode(nodeId))
                {
                    continue;
                }

                toRemove.Add(nodeId);
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                removedAny |= _unlockedNodeIds.Remove(toRemove[i]);
            }

            if (removedAny)
            {
                SaveProgress();
            }
        }

        private void BindReferences()
        {
            if (Client == null)
            {
                Client = FindFirstObjectByType<NetworkGameClient>();
            }

            if (TreeDataManager == null)
            {
                TreeDataManager = GetComponent<GrowthTreeDataManager>();
                if (TreeDataManager == null)
                {
                    TreeDataManager = FindFirstObjectByType<GrowthTreeDataManager>();
                }
            }
        }
    }
}
