using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab.Editor
{
    public sealed class TerrainLabMapViewerWindow : EditorWindow
    {
        private TerrainLabWorldConfig _config;
        private int _seed;
        private Vector2Int _centerChunk = Vector2Int.zero;
        private int _gridSize = 3;
        private bool _autoGenerateMissing = true;
        private bool _isDraggingPreview;
        private Vector2 _lastDragMouse;
        private Vector2 _dragRemainderPixels;

        private Texture2D _previewTexture;
        private string _lastStatus = "Ready";

        public static void Open()
        {
            var window = GetWindow<TerrainLabMapViewerWindow>("Terrain Height Viewer");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _config = TerrainLabMenu.LoadOrCreateWorldConfig();
            if (_config != null)
            {
                _seed = _config.DefaultSeed;
                _gridSize = _config.ViewerDefaultGridSize;
            }
        }

        private void OnDisable()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }

            _isDraggingPreview = false;
            _dragRemainderPixels = Vector2.zero;
        }

        private void OnGUI()
        {
            DrawHeader();

            if (_config == null)
            {
                EditorGUILayout.HelpBox("World config is missing.", MessageType.Warning);
                if (GUILayout.Button("Create Config"))
                {
                    _config = TerrainLabMenu.LoadOrCreateWorldConfig();
                }

                return;
            }

            DrawControls();
            DrawPreview();
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Terrain Lab Height Map Viewer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Deterministic world-seed heightmaps stitched as contiguous top-down view. " +
                "Missing chunks can be generated and saved automatically.",
                MessageType.Info);
        }

        private void DrawControls()
        {
            _config = (TerrainLabWorldConfig)EditorGUILayout.ObjectField("World Config", _config, typeof(TerrainLabWorldConfig), false);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _centerChunk = EditorGUILayout.Vector2IntField("Center Chunk", _centerChunk);

            _gridSize = EditorGUILayout.IntField("Grid Size (odd)", _gridSize);
            if (_gridSize < 1)
            {
                _gridSize = 1;
            }

            if ((_gridSize & 1) == 0)
            {
                _gridSize += 1;
            }

            _autoGenerateMissing = EditorGUILayout.ToggleLeft("Auto-generate missing chunk PNG", _autoGenerateMissing);
            EditorGUILayout.HelpBox(
                "Preview navigation: drag anywhere in preview to scroll contiguous neighboring chunks.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Refresh Preview", GUILayout.Height(28f)))
            {
                RefreshPreview();
            }

            if (GUILayout.Button("Export Preview PNG", GUILayout.Height(28f)))
            {
                ExportPreviewPng();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (_previewTexture == null)
            {
                EditorGUILayout.HelpBox("No preview generated yet.", MessageType.None);
                return;
            }

            var viewportHeight = Mathf.Max(220f, position.height - 290f);
            var viewportRect = GUILayoutUtility.GetRect(10f, viewportHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var drawSize = Mathf.Min(viewportRect.width, viewportRect.height);
            var drawRect = new Rect(
                viewportRect.x + ((viewportRect.width - drawSize) * 0.5f),
                viewportRect.y + ((viewportRect.height - drawSize) * 0.5f),
                drawSize,
                drawSize);

            EditorGUI.DrawRect(viewportRect, new Color(0.12f, 0.12f, 0.12f, 1f));
            GUI.DrawTexture(drawRect, _previewTexture, ScaleMode.StretchToFill, alphaBlend: false);
            HandlePreviewDragInput(drawRect);
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.None);
        }

        private void RefreshPreview()
        {
            if (_config == null)
            {
                _lastStatus = "World config is missing.";
                return;
            }

            _config.ValidateInPlace();

            var chunksPerAxis = _gridSize;
            var chunkPixels = _config.ChunkCells;
            var outputWidth = chunksPerAxis * chunkPixels;
            var outputHeight = chunksPerAxis * chunkPixels;

            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }

            _previewTexture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "TerrainLabHeightViewerPreview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var colors = new Color32[outputWidth * outputHeight];

            var radius = chunksPerAxis / 2;
            for (var gz = 0; gz < chunksPerAxis; gz++)
            {
                for (var gx = 0; gx < chunksPerAxis; gx++)
                {
                    var chunkCoord = new Vector2Int(
                        _centerChunk.x + gx - radius,
                        _centerChunk.y + gz - radius);

                    var chunkData = TerrainLabHeightChunkStore.LoadOrCreateChunk(
                        _config,
                        _seed,
                        chunkCoord,
                        forceRegenerate: false,
                        autoGenerateMissing: _autoGenerateMissing);

                    if (chunkData == null)
                    {
                        continue;
                    }

                    WriteChunkIntoPreview(colors, outputWidth, outputHeight, gx, gz, chunkData);
                }
            }

            _previewTexture.SetPixels32(colors);
            _previewTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            _lastStatus = $"Preview refreshed: {_gridSize}x{_gridSize} chunks, seed {_seed}.";
            Repaint();
        }

        private void WriteChunkIntoPreview(
            Color32[] output,
            int outputWidth,
            int outputHeight,
            int gridX,
            int gridZ,
            TerrainLabChunkHeightData chunkData)
        {
            var chunkPixels = _config.ChunkCells;
            var heights = chunkData.Heights;

            for (var z = 0; z < chunkPixels; z++)
            {
                for (var x = 0; x < chunkPixels; x++)
                {
                    var height = heights[x + 1, z + 1];
                    var color = TerrainLabHeightColorRamp.Evaluate(_config, height);

                    var outX = (gridX * chunkPixels) + x;
                    var outY = (gridZ * chunkPixels) + z;
                    var index = (outY * outputWidth) + outX;
                    if (index < 0 || index >= output.Length)
                    {
                        continue;
                    }

                    output[index] = color;
                }
            }
        }

        private void ExportPreviewPng()
        {
            if (_previewTexture == null)
            {
                _lastStatus = "Generate preview first.";
                return;
            }

            var defaultDirectory = "Assets/Labs/TerrainMeshMovementLab/Generated";
            var defaultName = $"terrainlab_view_seed{_seed}_{_gridSize}x{_gridSize}.png";
            var path = EditorUtility.SaveFilePanelInProject(
                "Export Terrain Preview PNG",
                defaultName,
                "png",
                "Choose output path for preview texture.",
                defaultDirectory);

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            var bytes = _previewTexture.EncodeToPNG();
            File.WriteAllBytes(absolute, bytes);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            _lastStatus = $"Preview exported: {path}";
        }

        private void HandlePreviewDragInput(Rect previewRect)
        {
            var currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (currentEvent.button != 0 || !previewRect.Contains(currentEvent.mousePosition))
                    {
                        return;
                    }

                    _isDraggingPreview = true;
                    _lastDragMouse = currentEvent.mousePosition;
                    _dragRemainderPixels = Vector2.zero;
                    currentEvent.Use();
                    return;

                case EventType.MouseDrag:
                    if (!_isDraggingPreview)
                    {
                        return;
                    }

                    var delta = currentEvent.mousePosition - _lastDragMouse;
                    _lastDragMouse = currentEvent.mousePosition;
                    _dragRemainderPixels += delta;
                    if (ConsumePreviewDragRemainder(previewRect))
                    {
                        RefreshPreview();
                    }

                    currentEvent.Use();
                    return;

                case EventType.MouseUp:
                case EventType.MouseLeaveWindow:
                case EventType.Ignore:
                    if (!_isDraggingPreview)
                    {
                        return;
                    }

                    _isDraggingPreview = false;
                    _dragRemainderPixels = Vector2.zero;
                    currentEvent.Use();
                    return;
            }
        }

        private bool ConsumePreviewDragRemainder(Rect previewRect)
        {
            if (_gridSize <= 0)
            {
                return false;
            }

            var chunkWidthPixels = previewRect.width / _gridSize;
            var chunkHeightPixels = previewRect.height / _gridSize;
            if (chunkWidthPixels <= 0.01f || chunkHeightPixels <= 0.01f)
            {
                return false;
            }

            var moved = false;

            while (Mathf.Abs(_dragRemainderPixels.x) >= chunkWidthPixels)
            {
                if (_dragRemainderPixels.x > 0f)
                {
                    _centerChunk.x -= 1;
                    _dragRemainderPixels.x -= chunkWidthPixels;
                }
                else
                {
                    _centerChunk.x += 1;
                    _dragRemainderPixels.x += chunkWidthPixels;
                }

                moved = true;
            }

            while (Mathf.Abs(_dragRemainderPixels.y) >= chunkHeightPixels)
            {
                if (_dragRemainderPixels.y > 0f)
                {
                    _centerChunk.y -= 1;
                    _dragRemainderPixels.y -= chunkHeightPixels;
                }
                else
                {
                    _centerChunk.y += 1;
                    _dragRemainderPixels.y += chunkHeightPixels;
                }

                moved = true;
            }

            return moved;
        }

    }
}
