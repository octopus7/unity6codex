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
        private Vector2 _scroll;

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

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(220f));
            var maxWidth = position.width - 32f;
            var drawWidth = Mathf.Min(maxWidth, _previewTexture.width);
            var ratio = _previewTexture.height > 0 ? _previewTexture.width / (float)_previewTexture.height : 1f;
            var drawHeight = drawWidth / Mathf.Max(0.001f, ratio);

            var rect = GUILayoutUtility.GetRect(drawWidth, drawHeight, GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(rect, _previewTexture, null, ScaleMode.ScaleToFit);
            EditorGUILayout.EndScrollView();
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
                    var color = EvaluateHeightColor(_config, height);

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

        private static Color32 EvaluateHeightColor(TerrainLabWorldConfig config, float height)
        {
            if (Mathf.Approximately(config.HeightMin, config.HeightMax))
            {
                return new Color32(124, 170, 124, 255);
            }

            var t = Mathf.InverseLerp(config.HeightMin, config.HeightMax, height);
            if (t < 0.30f)
            {
                return Color32.Lerp(new Color32(35, 90, 42, 255), new Color32(84, 130, 62, 255), t / 0.30f);
            }

            if (t < 0.7f)
            {
                return Color32.Lerp(new Color32(84, 130, 62, 255), new Color32(160, 150, 95, 255), (t - 0.30f) / 0.40f);
            }

            return Color32.Lerp(new Color32(160, 150, 95, 255), new Color32(238, 236, 220, 255), (t - 0.7f) / 0.3f);
        }
    }
}
