using System;
using System.Collections.Generic;
using System.Text;
using CodexSix.TopdownShooter.Game;
using UnityEditor;
using UnityEngine;

namespace CodexSix.TopdownShooter.EditorTools
{
    public sealed class GrowthTreePreviewWindow : EditorWindow
    {
        private TextAsset _jsonOverride;
        private string _resourcePath = "Progression/growth_tree";
        private int _previewCoins = 120;
        private int _previewGems = 12;
        private int _savedCoinSpent;

        private GrowthTreeDefinition _tree;
        private string _loadError = string.Empty;
        private string _selectedNodeId = string.Empty;

        private readonly HashSet<string> _savedUnlockedNodeIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Rect> _nodeRectsById = new(StringComparer.Ordinal);

        private Vector2 _treeScroll;

        [MenuItem("Tools/TopDownShooter/Growth Tree Preview")]
        public static void OpenWindow()
        {
            var window = GetWindow<GrowthTreePreviewWindow>("Growth Tree Preview");
            window.minSize = new Vector2(920f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSavedProgress();
            ReloadTree();
        }

        private void OnGUI()
        {
            DrawToolbar();

            GUILayout.Space(6f);
            DrawCurrencyPanel();
            GUILayout.Space(8f);

            if (!string.IsNullOrWhiteSpace(_loadError))
            {
                EditorGUILayout.HelpBox(_loadError, MessageType.Warning);
            }

            if (_tree == null || _tree.Nodes.Count == 0)
            {
                EditorGUILayout.HelpBox("No growth tree loaded.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var treeRect = GUILayoutUtility.GetRect(620f, 430f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawTreeCanvas(treeRect);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(280f), GUILayout.ExpandHeight(true)))
                {
                    DrawSelectedNodeDetail();
                }
            }

            GUILayout.Space(4f);
            EditorGUILayout.HelpBox("Read-only preview. Edit node data in JSON.", MessageType.None);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Growth Tree Source", EditorStyles.boldLabel);
                _jsonOverride = (TextAsset)EditorGUILayout.ObjectField("JSON Override", _jsonOverride, typeof(TextAsset), false);
                _resourcePath = EditorGUILayout.TextField("Resources Path", _resourcePath);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reload JSON", GUILayout.Width(120f)))
                    {
                        ReloadTree();
                    }

                    if (GUILayout.Button("Refresh Saved Progress", GUILayout.Width(160f)))
                    {
                        LoadSavedProgress();
                    }
                }
            }
        }

        private void DrawCurrencyPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Preview Currency", EditorStyles.boldLabel);
                _previewCoins = Mathf.Max(0, EditorGUILayout.IntField("Server Coins", _previewCoins));
                _previewGems = Mathf.Max(0, EditorGUILayout.IntField("Preview Gems", _previewGems));
                _savedCoinSpent = Mathf.Max(0, EditorGUILayout.IntField("Saved Coin Spent", _savedCoinSpent));

                var availableCoins = Mathf.Max(0, _previewCoins - _savedCoinSpent);
                EditorGUILayout.LabelField($"Available Coins (preview): {availableCoins}");
                EditorGUILayout.LabelField($"Saved Unlocked Nodes: {_savedUnlockedNodeIds.Count}");
            }
        }

        private void DrawTreeCanvas(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

            if (_tree == null || _tree.Nodes.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 24f), "No nodes.");
                return;
            }

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;

            for (var i = 0; i < _tree.Nodes.Count; i++)
            {
                var node = _tree.Nodes[i];
                minX = Mathf.Min(minX, node.X);
                maxX = Mathf.Max(maxX, node.X);
                minY = Mathf.Min(minY, node.Y);
                maxY = Mathf.Max(maxY, node.Y);
            }

            const float padding = 76f;
            var contentWidth = Mathf.Max(rect.width - 18f, (maxX - minX) + (padding * 2f));
            var contentHeight = Mathf.Max(rect.height - 18f, (maxY - minY) + (padding * 2f));
            var contentRect = new Rect(0f, 0f, contentWidth, contentHeight);

            var xOffset = padding - minX;
            var yOffset = padding - minY;

            _treeScroll = GUI.BeginScrollView(rect, _treeScroll, contentRect);
            _nodeRectsById.Clear();

            for (var i = 0; i < _tree.Nodes.Count; i++)
            {
                var node = _tree.Nodes[i];
                var center = new Vector2(node.X + xOffset, node.Y + yOffset);
                _nodeRectsById[node.Id] = new Rect(center.x - 46f, center.y - 24f, 92f, 48f);
            }

            Handles.BeginGUI();
            var previousHandleColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.28f);

            for (var i = 0; i < _tree.Nodes.Count; i++)
            {
                var node = _tree.Nodes[i];
                if (!_nodeRectsById.TryGetValue(node.Id, out var nodeRect))
                {
                    continue;
                }

                var start = new Vector3(nodeRect.xMin, nodeRect.center.y, 0f);
                var requiredNodeIds = node.RequiredNodeIds;
                for (var r = 0; r < requiredNodeIds.Count; r++)
                {
                    if (!_nodeRectsById.TryGetValue(requiredNodeIds[r], out var requiredRect))
                    {
                        continue;
                    }

                    var end = new Vector3(requiredRect.xMax, requiredRect.center.y, 0f);
                    Handles.DrawAAPolyLine(2f, end, start);
                }
            }

            Handles.color = previousHandleColor;
            Handles.EndGUI();

            var availableCoins = Mathf.Max(0, _previewCoins - _savedCoinSpent);
            for (var i = 0; i < _tree.Nodes.Count; i++)
            {
                var node = _tree.Nodes[i];
                if (!_nodeRectsById.TryGetValue(node.Id, out var nodeRect))
                {
                    continue;
                }

                var evaluation = GrowthUnlockEvaluator.EvaluateNode(
                    _tree,
                    node,
                    _savedUnlockedNodeIds,
                    availableCoins,
                    _previewGems);

                var previousColor = GUI.color;
                GUI.color = ResolveNodeColor(evaluation.VisualState);
                if (GUI.Button(nodeRect, node.Name))
                {
                    _selectedNodeId = node.Id;
                }

                GUI.color = previousColor;
                GUI.Label(new Rect(nodeRect.x + 2f, nodeRect.y - 16f, nodeRect.width - 4f, 14f), $"T{node.Tier}");
            }

            GUI.EndScrollView();
        }

        private void DrawSelectedNodeDetail()
        {
            var selectedNode = ResolveSelectedNode();
            if (selectedNode == null)
            {
                EditorGUILayout.LabelField("Select a node.");
                return;
            }

            var availableCoins = Mathf.Max(0, _previewCoins - _savedCoinSpent);
            var evaluation = GrowthUnlockEvaluator.EvaluateNode(
                _tree,
                selectedNode,
                _savedUnlockedNodeIds,
                availableCoins,
                _previewGems);

            EditorGUILayout.LabelField(selectedNode.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {selectedNode.Id}");
            EditorGUILayout.LabelField($"Tier: {selectedNode.Tier}");
            EditorGUILayout.LabelField($"Branch: {selectedNode.BranchId}");

            if (!string.IsNullOrWhiteSpace(selectedNode.Description))
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField(selectedNode.Description, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"Cost: {selectedNode.CoinCost}c, {selectedNode.GemCost}g");
            EditorGUILayout.LabelField(
                $"Predecessor opened: {evaluation.OpenedRequiredCount}/{selectedNode.RequiredOpenedFromRequiredNodes}");
            EditorGUILayout.LabelField(
                $"Lower-tier opened: {evaluation.OpenedLowerTierCount}/{selectedNode.MinOpenedNodesInLowerTiers}");
            EditorGUILayout.LabelField($"State: {evaluation.VisualState}");

            if (!evaluation.CanUnlock && !evaluation.IsUnlocked && !string.IsNullOrWhiteSpace(evaluation.BlockedReason))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(evaluation.BlockedReason, MessageType.None);
            }

            EditorGUILayout.Space(6f);
            if (selectedNode.RequiredNodeIds.Count > 0)
            {
                EditorGUILayout.LabelField("Required Node IDs:", EditorStyles.boldLabel);
                for (var i = 0; i < selectedNode.RequiredNodeIds.Count; i++)
                {
                    EditorGUILayout.LabelField($"- {selectedNode.RequiredNodeIds[i]}");
                }
            }
            else
            {
                EditorGUILayout.LabelField("Required Node IDs: (none)");
            }
        }

        private GrowthNodeDefinition ResolveSelectedNode()
        {
            if (_tree == null || _tree.Nodes.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(_selectedNodeId) && _tree.TryGetNode(_selectedNodeId, out var node))
            {
                return node;
            }

            var sorted = _tree.GetSortedByTierAndPosition();
            if (sorted.Count == 0)
            {
                return null;
            }

            _selectedNodeId = sorted[0].Id;
            return sorted[0];
        }

        private void ReloadTree()
        {
            _tree = null;
            _loadError = string.Empty;

            var jsonText = ResolveJsonText();
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                _loadError = "Growth tree json text is empty or missing.";
                Repaint();
                return;
            }

            if (!GrowthTreeDataManager.TryParseTree(jsonText, out var tree, out var errors))
            {
                var builder = new StringBuilder();
                for (var i = 0; i < errors.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append("\n");
                    }

                    builder.Append(errors[i]);
                }

                _loadError = builder.ToString();
                Repaint();
                return;
            }

            _tree = tree;
            _loadError = string.Empty;

            if (!string.IsNullOrWhiteSpace(_selectedNodeId) && !_tree.HasNode(_selectedNodeId))
            {
                _selectedNodeId = string.Empty;
            }

            Repaint();
        }

        private string ResolveJsonText()
        {
            if (_jsonOverride != null)
            {
                return _jsonOverride.text;
            }

            if (string.IsNullOrWhiteSpace(_resourcePath))
            {
                return string.Empty;
            }

            var resource = Resources.Load<TextAsset>(_resourcePath);
            return resource != null ? resource.text : string.Empty;
        }

        private void LoadSavedProgress()
        {
            _savedUnlockedNodeIds.Clear();

            var serializedUnlocked = PlayerPrefs.GetString(GrowthProgressionManager.UnlockedIdsPrefKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(serializedUnlocked))
            {
                var tokens = serializedUnlocked.Split('|');
                for (var i = 0; i < tokens.Length; i++)
                {
                    var nodeId = tokens[i].Trim();
                    if (!string.IsNullOrEmpty(nodeId))
                    {
                        _savedUnlockedNodeIds.Add(nodeId);
                    }
                }
            }

            _previewGems = Mathf.Max(0, PlayerPrefs.GetInt(GrowthProgressionManager.GemsPrefKey, _previewGems));
            _savedCoinSpent = Mathf.Max(0, PlayerPrefs.GetInt(GrowthProgressionManager.CoinSpentPrefKey, _savedCoinSpent));
        }

        private static Color ResolveNodeColor(GrowthNodeVisualState visualState)
        {
            return visualState switch
            {
                GrowthNodeVisualState.Unlocked => new Color(0.24f, 0.82f, 0.34f, 0.96f),
                GrowthNodeVisualState.Unlockable => new Color(0.24f, 0.64f, 0.96f, 0.96f),
                GrowthNodeVisualState.LockedCost => new Color(0.9f, 0.58f, 0.18f, 0.96f),
                _ => new Color(0.45f, 0.45f, 0.45f, 0.96f)
            };
        }
    }
}
