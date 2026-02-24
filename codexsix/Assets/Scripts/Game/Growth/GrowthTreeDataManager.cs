using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class GrowthTreeDataManager : MonoBehaviour
    {
        public TextAsset GrowthTreeJson;
        public string ResourcesTreePath = "Progression/growth_tree";
        public bool LoadOnAwake = true;

        public bool IsLoaded { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public GrowthTreeDefinition Tree { get; private set; }
        public int NodeCount => Tree != null ? Tree.Nodes.Count : 0;

        private bool _loadAttempted;

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
            IsLoaded = false;
            Tree = null;
            LastError = string.Empty;

            var treeText = ResolveTreeText();
            if (string.IsNullOrWhiteSpace(treeText))
            {
                LastError = "Growth tree json is missing.";
                Debug.LogWarning("GrowthTreeDataManager could not find growth tree json.");
                return false;
            }

            if (!TryParseTree(treeText, out var treeDefinition, out var errors))
            {
                var messageBuilder = new StringBuilder();
                for (var i = 0; i < errors.Count; i++)
                {
                    if (i > 0)
                    {
                        messageBuilder.Append(" | ");
                    }

                    messageBuilder.Append(errors[i]);
                    Debug.LogWarning($"GrowthTreeDataManager validation error: {errors[i]}");
                }

                LastError = messageBuilder.ToString();
                Debug.LogWarning("GrowthTreeDataManager disabled due to schema validation errors.");
                return false;
            }

            Tree = treeDefinition;
            IsLoaded = true;
            LastError = string.Empty;
            Debug.Log($"GrowthTreeDataManager loaded {Tree.Nodes.Count} growth nodes.");
            return true;
        }

        public bool EnsureLoaded()
        {
            if (IsLoaded)
            {
                return true;
            }

            if (_loadAttempted)
            {
                return false;
            }

            return Load();
        }

        public bool TryGetNode(string nodeId, out GrowthNodeDefinition node)
        {
            if (Tree != null && Tree.TryGetNode(nodeId, out node))
            {
                return true;
            }

            node = null;
            return false;
        }

        public IReadOnlyList<GrowthNodeDefinition> GetNodesOrEmpty()
        {
            return Tree != null ? Tree.Nodes : Array.Empty<GrowthNodeDefinition>();
        }

        private string ResolveTreeText()
        {
            if (GrowthTreeJson != null && !string.IsNullOrWhiteSpace(GrowthTreeJson.text))
            {
                return GrowthTreeJson.text;
            }

            if (string.IsNullOrWhiteSpace(ResourcesTreePath))
            {
                return string.Empty;
            }

            var resourceAsset = Resources.Load<TextAsset>(ResourcesTreePath);
            return resourceAsset != null ? resourceAsset.text : string.Empty;
        }

        public static bool TryParseTree(
            string jsonText,
            out GrowthTreeDefinition treeDefinition,
            out List<string> errors)
        {
            errors = new List<string>();
            treeDefinition = null;

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errors.Add("Growth tree json text is empty.");
                return false;
            }

            GrowthTreeRecord treeRecord;
            try
            {
                treeRecord = JsonUtility.FromJson<GrowthTreeRecord>(jsonText);
            }
            catch (Exception exception)
            {
                errors.Add($"Growth tree json parse failed: {exception.Message}");
                return false;
            }

            if (treeRecord == null)
            {
                errors.Add("Growth tree json produced null root object.");
                return false;
            }

            if (treeRecord.nodes == null || treeRecord.nodes.Length == 0)
            {
                errors.Add("Growth tree json has no nodes.");
                return false;
            }

            var definitions = new List<GrowthNodeDefinition>(treeRecord.nodes.Length);
            var knownIds = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < treeRecord.nodes.Length; i++)
            {
                var record = treeRecord.nodes[i];
                if (record == null)
                {
                    errors.Add($"nodes[{i}] is null.");
                    continue;
                }

                var nodeId = NormalizeId(record.id);
                if (string.IsNullOrEmpty(nodeId))
                {
                    errors.Add($"nodes[{i}] has empty id.");
                    continue;
                }

                if (!knownIds.Add(nodeId))
                {
                    errors.Add($"nodes[{i}] duplicated id '{nodeId}'.");
                    continue;
                }

                if (record.tier < 0)
                {
                    errors.Add($"node '{nodeId}' has negative tier {record.tier}.");
                }

                if (record.coinCost < 0)
                {
                    errors.Add($"node '{nodeId}' has negative coinCost {record.coinCost}.");
                }

                if (record.gemCost < 0)
                {
                    errors.Add($"node '{nodeId}' has negative gemCost {record.gemCost}.");
                }

                if (record.requiredOpenedFromRequiredNodes < 0)
                {
                    errors.Add(
                        $"node '{nodeId}' has negative requiredOpenedFromRequiredNodes {record.requiredOpenedFromRequiredNodes}.");
                }

                if (record.minOpenedNodesInLowerTiers < 0)
                {
                    errors.Add(
                        $"node '{nodeId}' has negative minOpenedNodesInLowerTiers {record.minOpenedNodesInLowerTiers}.");
                }

                var requiredNodeIds = NormalizeRequiredNodeIds(record.requiredNodeIds, nodeId, errors);
                if (record.requiredOpenedFromRequiredNodes > requiredNodeIds.Length)
                {
                    errors.Add(
                        $"node '{nodeId}' requiredOpenedFromRequiredNodes {record.requiredOpenedFromRequiredNodes} exceeds requiredNodeIds count {requiredNodeIds.Length}.");
                }

                var nodeName = string.IsNullOrWhiteSpace(record.name)
                    ? nodeId
                    : record.name.Trim();

                var nodeDescription = string.IsNullOrWhiteSpace(record.description)
                    ? string.Empty
                    : record.description.Trim();

                var branchId = string.IsNullOrWhiteSpace(record.branchId)
                    ? "default"
                    : record.branchId.Trim();

                definitions.Add(new GrowthNodeDefinition(
                    id: nodeId,
                    name: nodeName,
                    description: nodeDescription,
                    tier: record.tier,
                    branchId: branchId,
                    x: record.x,
                    y: record.y,
                    coinCost: Math.Max(0, record.coinCost),
                    gemCost: Math.Max(0, record.gemCost),
                    requiredNodeIds: requiredNodeIds,
                    requiredOpenedFromRequiredNodes: Math.Max(0, record.requiredOpenedFromRequiredNodes),
                    minOpenedNodesInLowerTiers: Math.Max(0, record.minOpenedNodesInLowerTiers)));
            }

            var knownDefinitionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < definitions.Count; i++)
            {
                knownDefinitionIds.Add(definitions[i].Id);
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var node = definitions[i];
                var requiredNodeIds = node.RequiredNodeIds;
                for (var r = 0; r < requiredNodeIds.Count; r++)
                {
                    var requiredNodeId = requiredNodeIds[r];
                    if (!knownDefinitionIds.Contains(requiredNodeId))
                    {
                        errors.Add($"node '{node.Id}' references missing required node '{requiredNodeId}'.");
                    }
                }
            }

            if (errors.Count > 0)
            {
                treeDefinition = null;
                return false;
            }

            treeDefinition = new GrowthTreeDefinition(treeRecord.treeId, treeRecord.version, definitions);
            return true;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private static string[] NormalizeRequiredNodeIds(string[] rawRequiredNodeIds, string ownerNodeId, List<string> errors)
        {
            if (rawRequiredNodeIds == null || rawRequiredNodeIds.Length == 0)
            {
                return Array.Empty<string>();
            }

            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(rawRequiredNodeIds.Length);

            for (var i = 0; i < rawRequiredNodeIds.Length; i++)
            {
                var nodeId = NormalizeId(rawRequiredNodeIds[i]);
                if (string.IsNullOrEmpty(nodeId))
                {
                    errors.Add($"node '{ownerNodeId}' has empty requiredNodeIds[{i}].");
                    continue;
                }

                if (!uniqueIds.Add(nodeId))
                {
                    errors.Add($"node '{ownerNodeId}' has duplicate required node id '{nodeId}'.");
                    continue;
                }

                result.Add(nodeId);
            }

            return result.ToArray();
        }
    }
}
