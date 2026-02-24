using System;
using System.Collections.Generic;
using CodexSix.TopdownShooter.Net;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CodexSix.TopdownShooter.Game
{
    public sealed class GrowthPanelHud : MonoBehaviour
    {
        public NetworkGameClient Client;
        public GrowthTreeDataManager TreeDataManager;
        public GrowthProgressionManager ProgressionManager;
        public LocalInputSender InputSender;

        public float UiScale = 2f;
        public KeyCode ToggleKey = KeyCode.K;
        public bool StartVisible;
        public bool DrawGemIcon = true;

        private const float DefaultWindowWidth = 940f;
        private const float DefaultWindowHeight = 560f;

        private readonly Dictionary<string, Rect> _nodeRectsById = new(StringComparer.Ordinal);

        private Rect _windowRect = new(0f, 0f, DefaultWindowWidth, DefaultWindowHeight);
        private Vector2 _treeScroll;
        private bool _windowPlaced;
        private bool _visible;
        private string _selectedNodeId = string.Empty;
        private string _statusMessage = string.Empty;
        private float _statusMessageClearAt;
        private float _lastScale = 1f;
        private bool _pointerInsideWindow;

        private GUIStyle _nodeButtonStyle;
        private GUIStyle _detailTitleStyle;
        private GUIStyle _detailBodyStyle;
        private GUIStyle _statusStyle;

        private GemIconRenderer _gemIconRenderer;
        private GrowthProgressionManager _subscribedProgressionManager;

        public bool IsVisible => _visible;

        private void Awake()
        {
            _visible = StartVisible;
            BindReferences();
        }

        private void OnEnable()
        {
            BindReferences();
            SyncProgressSubscription();
        }

        private void OnDisable()
        {
            if (_subscribedProgressionManager != null)
            {
                _subscribedProgressionManager.ProgressChanged -= HandleProgressChanged;
                _subscribedProgressionManager = null;
            }

            if (InputSender != null)
            {
                InputSender.ExternalUiFireBlock = false;
            }
        }

        private void OnDestroy()
        {
            if (_gemIconRenderer != null)
            {
                _gemIconRenderer.Dispose();
                _gemIconRenderer = null;
            }
        }

        private void Update()
        {
            BindReferences();
            UpdateVisibilityToggle();

            if (_statusMessageClearAt > 0f && Time.unscaledTime >= _statusMessageClearAt)
            {
                _statusMessage = string.Empty;
                _statusMessageClearAt = 0f;
            }

            if (_visible && DrawGemIcon)
            {
                _gemIconRenderer ??= new GemIconRenderer(textureSize: 96);
                _gemIconRenderer.UpdateAndRender(Time.unscaledDeltaTime);
            }

            UpdateFireInputBlockState();
        }

        public void ToggleVisibility()
        {
            SetVisibility(!_visible);
        }

        public void SetVisibility(bool visible)
        {
            _visible = visible;
            if (!_visible && InputSender != null)
            {
                InputSender.ExternalUiFireBlock = false;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            EnsureReferencesReady();
            EnsureStyles();

            if (UiScale <= 0.01f)
            {
                UiScale = 2f;
            }

            var scale = Mathf.Max(1f, UiScale);
            _lastScale = scale;

            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var viewWidth = Screen.width / scale;
            var viewHeight = Screen.height / scale;
            EnsureWindowPlaced(viewWidth, viewHeight);

            _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Growth Tree");

            GUI.matrix = previousMatrix;
        }

        private void DrawWindow(int id)
        {
            var isConnected = Client != null && Client.CurrentConnectionState == ConnectionState.Connected;
            var availableCoins = ProgressionManager != null ? ProgressionManager.AvailableCoins : 0;
            var gems = ProgressionManager != null ? ProgressionManager.GemBalance : 0;
            var spentCoins = ProgressionManager != null ? ProgressionManager.CoinSpent : 0;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Coins: {availableCoins} (spent {spentCoins})", GUILayout.Width(250f));

            if (DrawGemIcon && _gemIconRenderer != null && _gemIconRenderer.IconTexture != null)
            {
                var gemRect = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f), GUILayout.Height(20f));
                GUI.DrawTexture(gemRect, _gemIconRenderer.IconTexture, ScaleMode.StretchToFill, alphaBlend: true);
            }

            GUILayout.Label($"Gems: {gems}", GUILayout.Width(100f));

            using (new GUIEnabledScope(ProgressionManager != null))
            {
                if (GUILayout.Button("+1 Gem", GUILayout.Width(64f)) && ProgressionManager != null)
                {
                    ProgressionManager.AddGems(1);
                    SetStatusMessage("Gem +1");
                }

                if (GUILayout.Button("-1 Gem", GUILayout.Width(64f)) && ProgressionManager != null)
                {
                    ProgressionManager.AddGems(-1);
                    SetStatusMessage("Gem -1");
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(72f)))
            {
                SetVisibility(false);
            }

            GUILayout.EndHorizontal();

            if (!isConnected)
            {
                GUILayout.Space(12f);
                GUILayout.Label("Connect to the server to use growth progression.", _detailBodyStyle);
                GUI.DragWindow();
                return;
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            var treeRect = GUILayoutUtility.GetRect(560f, 460f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawTreeCanvas(treeRect);

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300f), GUILayout.ExpandHeight(true));
            DrawNodeDetailPanel();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                GUILayout.Space(4f);
                GUILayout.Label(_statusMessage, _statusStyle);
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawTreeCanvas(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);

            if (TreeDataManager == null || !TreeDataManager.EnsureLoaded())
            {
                var errorText = TreeDataManager != null && !string.IsNullOrWhiteSpace(TreeDataManager.LastError)
                    ? TreeDataManager.LastError
                    : "Growth tree data is not available.";
                GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f), errorText, _detailBodyStyle);
                return;
            }

            var tree = TreeDataManager.Tree;
            if (tree == null || tree.Nodes.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f), "No growth nodes.", _detailBodyStyle);
                return;
            }

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var i = 0; i < tree.Nodes.Count; i++)
            {
                var node = tree.Nodes[i];
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
            for (var i = 0; i < tree.Nodes.Count; i++)
            {
                var node = tree.Nodes[i];
                var center = new Vector2(node.X + xOffset, node.Y + yOffset);
                _nodeRectsById[node.Id] = new Rect(center.x - 46f, center.y - 24f, 92f, 48f);
            }

            for (var i = 0; i < tree.Nodes.Count; i++)
            {
                var node = tree.Nodes[i];
                if (!_nodeRectsById.TryGetValue(node.Id, out var nodeRect))
                {
                    continue;
                }

                var start = new Vector2(nodeRect.xMin, nodeRect.center.y);
                var requiredNodeIds = node.RequiredNodeIds;
                for (var r = 0; r < requiredNodeIds.Count; r++)
                {
                    var requiredNodeId = requiredNodeIds[r];
                    if (!_nodeRectsById.TryGetValue(requiredNodeId, out var requiredRect))
                    {
                        continue;
                    }

                    var end = new Vector2(requiredRect.xMax, requiredRect.center.y);
                    DrawLine(end, start, new Color(1f, 1f, 1f, 0.35f), 2f);
                }
            }

            for (var i = 0; i < tree.Nodes.Count; i++)
            {
                var node = tree.Nodes[i];
                if (!_nodeRectsById.TryGetValue(node.Id, out var nodeRect))
                {
                    continue;
                }

                var evaluation = ProgressionManager != null
                    ? ProgressionManager.EvaluateNode(node.Id)
                    : default;

                var previousColor = GUI.color;
                GUI.color = ResolveNodeColor(evaluation.VisualState);

                if (GUI.Button(nodeRect, node.Name, _nodeButtonStyle))
                {
                    _selectedNodeId = node.Id;
                }

                GUI.color = previousColor;

                var tierRect = new Rect(nodeRect.x + 3f, nodeRect.y - 16f, nodeRect.width - 6f, 14f);
                GUI.Label(tierRect, $"T{node.Tier}", _detailBodyStyle);
            }

            GUI.EndScrollView();
        }

        private void DrawNodeDetailPanel()
        {
            if (TreeDataManager == null || !TreeDataManager.EnsureLoaded() || TreeDataManager.Tree == null)
            {
                GUILayout.Label("Growth tree is not loaded.", _detailBodyStyle);
                return;
            }

            var tree = TreeDataManager.Tree;
            var selectedNode = ResolveSelectedNode(tree);
            if (selectedNode == null)
            {
                GUILayout.Label("No node selected.", _detailBodyStyle);
                return;
            }

            var evaluation = ProgressionManager != null
                ? ProgressionManager.EvaluateNode(selectedNode.Id)
                : default;

            GUILayout.Label(selectedNode.Name, _detailTitleStyle);
            GUILayout.Label($"ID: {selectedNode.Id}", _detailBodyStyle);
            GUILayout.Label($"Tier: {selectedNode.Tier} / Branch: {selectedNode.BranchId}", _detailBodyStyle);
            GUILayout.Space(6f);

            if (!string.IsNullOrWhiteSpace(selectedNode.Description))
            {
                GUILayout.Label(selectedNode.Description, _detailBodyStyle);
                GUILayout.Space(6f);
            }

            GUILayout.Label($"Cost: {selectedNode.CoinCost} coins, {selectedNode.GemCost} gems", _detailBodyStyle);
            GUILayout.Label(
                $"Required opened from predecessors: {evaluation.OpenedRequiredCount}/{selectedNode.RequiredOpenedFromRequiredNodes}",
                _detailBodyStyle);
            GUILayout.Label(
                $"Opened nodes in lower tiers: {evaluation.OpenedLowerTierCount}/{selectedNode.MinOpenedNodesInLowerTiers}",
                _detailBodyStyle);

            if (selectedNode.RequiredNodeIds.Count > 0)
            {
                GUILayout.Label("Required nodes:", _detailBodyStyle);
                for (var i = 0; i < selectedNode.RequiredNodeIds.Count; i++)
                {
                    GUILayout.Label($"- {selectedNode.RequiredNodeIds[i]}", _detailBodyStyle);
                }
            }
            else
            {
                GUILayout.Label("Required nodes: (none)", _detailBodyStyle);
            }

            GUILayout.Space(8f);

            var unlockLabel = evaluation.IsUnlocked
                ? "Unlocked"
                : evaluation.CanUnlock
                    ? "Unlock"
                    : "Locked";

            using (new GUIEnabledScope(ProgressionManager != null && evaluation.CanUnlock))
            {
                if (GUILayout.Button(unlockLabel, GUILayout.Height(28f)) && ProgressionManager != null)
                {
                    if (ProgressionManager.TryUnlockNode(selectedNode.Id, out var message))
                    {
                        SetStatusMessage(message);
                    }
                    else
                    {
                        SetStatusMessage(message);
                    }
                }
            }

            if (!evaluation.CanUnlock && !evaluation.IsUnlocked && !string.IsNullOrWhiteSpace(evaluation.BlockedReason))
            {
                GUILayout.Space(4f);
                GUILayout.Label(evaluation.BlockedReason, _statusStyle);
            }
        }

        private GrowthNodeDefinition ResolveSelectedNode(GrowthTreeDefinition tree)
        {
            if (tree == null || tree.Nodes.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(_selectedNodeId) && tree.TryGetNode(_selectedNodeId, out var selected))
            {
                return selected;
            }

            var fallback = tree.GetSortedByTierAndPosition()[0];
            _selectedNodeId = fallback.Id;
            return fallback;
        }

        private static void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            var angle = Vector3.Angle(to - from, Vector2.right);
            if (from.y > to.y)
            {
                angle = -angle;
            }

            var length = (to - from).magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.DrawTexture(new Rect(from.x, from.y - (thickness * 0.5f), length, thickness), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private static Color ResolveNodeColor(GrowthNodeVisualState visualState)
        {
            return visualState switch
            {
                GrowthNodeVisualState.Unlocked => new Color(0.22f, 0.82f, 0.32f, 0.95f),
                GrowthNodeVisualState.Unlockable => new Color(0.24f, 0.62f, 0.94f, 0.95f),
                GrowthNodeVisualState.LockedCost => new Color(0.86f, 0.55f, 0.17f, 0.95f),
                _ => new Color(0.42f, 0.42f, 0.42f, 0.95f)
            };
        }

        private void EnsureStyles()
        {
            if (_nodeButtonStyle == null)
            {
                _nodeButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(4, 4, 4, 4)
                };
            }

            if (_detailTitleStyle == null)
            {
                _detailTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                _detailTitleStyle.normal.textColor = Color.white;
            }

            if (_detailBodyStyle == null)
            {
                _detailBodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    wordWrap = true
                };
                _detailBodyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            }

            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(_detailBodyStyle)
                {
                    fontStyle = FontStyle.Bold
                };
                _statusStyle.normal.textColor = new Color(0.98f, 0.84f, 0.25f, 1f);
            }
        }

        private void EnsureWindowPlaced(float viewWidth, float viewHeight)
        {
            if (_windowPlaced)
            {
                return;
            }

            _windowRect.x = (viewWidth - _windowRect.width) * 0.5f;
            _windowRect.y = (viewHeight - _windowRect.height) * 0.5f;
            _windowPlaced = true;
        }

        private void SetStatusMessage(string message, float holdSeconds = 2.6f)
        {
            _statusMessage = message ?? string.Empty;
            _statusMessageClearAt = Time.unscaledTime + Mathf.Max(0.2f, holdSeconds);
        }

        private void HandleProgressChanged()
        {
            if (!_visible)
            {
                return;
            }

            RepaintHint();
        }

        private void RepaintHint()
        {
            // Intentionally empty. OnGUI-driven HUD repaints every frame while visible.
        }

        private void EnsureReferencesReady()
        {
            if (TreeDataManager != null)
            {
                TreeDataManager.EnsureLoaded();
            }

            if (ProgressionManager != null)
            {
                ProgressionManager.LoadOnAwake = true;
            }
        }

        private void UpdateVisibilityToggle()
        {
            if (IsToggleKeyPressed())
            {
                ToggleVisibility();
            }
        }

        private bool IsToggleKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                switch (ToggleKey)
                {
                    case KeyCode.K:
                        return Keyboard.current.kKey.wasPressedThisFrame;
                    default:
                        break;
                }
            }
#endif

            return Input.GetKeyDown(ToggleKey);
        }

        private void UpdateFireInputBlockState()
        {
            if (InputSender == null)
            {
                return;
            }

            if (!_visible)
            {
                _pointerInsideWindow = false;
                InputSender.ExternalUiFireBlock = false;
                return;
            }

            var scale = Mathf.Max(1f, _lastScale);
            var pointer = ReadPointerInGuiSpace(scale);
            _pointerInsideWindow = _windowRect.Contains(pointer);
            InputSender.ExternalUiFireBlock = _pointerInsideWindow;
        }

        private static Vector2 ReadPointerInGuiSpace(float scale)
        {
            var mousePosition = Input.mousePosition;
            return new Vector2(
                mousePosition.x / scale,
                (Screen.height - mousePosition.y) / scale);
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

            if (ProgressionManager == null)
            {
                ProgressionManager = GetComponent<GrowthProgressionManager>();
                if (ProgressionManager == null)
                {
                    ProgressionManager = FindFirstObjectByType<GrowthProgressionManager>();
                }
            }

            if (InputSender == null)
            {
                InputSender = FindFirstObjectByType<LocalInputSender>();
            }

            if (ProgressionManager != null && ProgressionManager.Client == null)
            {
                ProgressionManager.Client = Client;
            }

            if (ProgressionManager != null && ProgressionManager.TreeDataManager == null)
            {
                ProgressionManager.TreeDataManager = TreeDataManager;
            }

            SyncProgressSubscription();
        }

        private void SyncProgressSubscription()
        {
            if (_subscribedProgressionManager == ProgressionManager)
            {
                return;
            }

            if (_subscribedProgressionManager != null)
            {
                _subscribedProgressionManager.ProgressChanged -= HandleProgressChanged;
            }

            _subscribedProgressionManager = ProgressionManager;
            if (_subscribedProgressionManager != null)
            {
                _subscribedProgressionManager.ProgressChanged += HandleProgressChanged;
            }
        }

        private readonly struct GUIEnabledScope : IDisposable
        {
            private readonly bool _previous;

            public GUIEnabledScope(bool enabled)
            {
                _previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = _previous;
            }
        }
    }
}
