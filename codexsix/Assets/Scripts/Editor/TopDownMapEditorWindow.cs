using System.Collections.Generic;
using CodexSix.TopdownShooter.Game;
using UnityEditor;
using UnityEngine;

namespace CodexSix.TopdownShooter.EditorTools
{
    public sealed class TopDownMapEditorWindow : EditorWindow
    {
        private TopDownMapAuthoringAsset _mapAsset;
        private SerializedObject _serializedMapAsset;
        private Vector2 _scroll;
        private List<string> _lastValidationErrors = new();
        private bool _hasValidationResult;

        [MenuItem("Tools/TopDownShooter/Map Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<TopDownMapEditorWindow>("TopDown Map Editor");
            window.minSize = new Vector2(560f, 460f);
            window.Show();
        }

        private void OnEnable()
        {
            _mapAsset = TopDownMapExportUtility.LoadOrCreateDefaultAsset();
            BindSerializedAsset();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_mapAsset == null)
            {
                EditorGUILayout.HelpBox("Assign a map asset to edit.", MessageType.Info);
                return;
            }

            DrawActions();
            DrawValidationResult();
            DrawEditorBody();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var selectedAsset = (TopDownMapAuthoringAsset)EditorGUILayout.ObjectField(
                _mapAsset,
                typeof(TopDownMapAuthoringAsset),
                allowSceneObjects: false,
                GUILayout.MinWidth(240f));

            if (selectedAsset != _mapAsset)
            {
                _mapAsset = selectedAsset;
                BindSerializedAsset();
            }

            if (GUILayout.Button("Load Default", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                _mapAsset = TopDownMapExportUtility.LoadOrCreateDefaultAsset();
                BindSerializedAsset();
            }

            if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(60f)) && _mapAsset != null)
            {
                EditorGUIUtility.PingObject(_mapAsset);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Validate", GUILayout.Height(28f)))
            {
                RunValidation();
            }

            if (GUILayout.Button("Export Server JSON", GUILayout.Height(28f)))
            {
                if (TopDownMapExportUtility.TryExportToServerJson(_mapAsset, out var outputPath, out var errors))
                {
                    _lastValidationErrors = new List<string>();
                    _hasValidationResult = true;
                    ShowNotification(new GUIContent("Map export complete"));
                    Debug.Log($"Map exported: {outputPath}");
                }
                else
                {
                    _lastValidationErrors = errors;
                    _hasValidationResult = true;
                    Debug.LogError("Map export failed:\n" + string.Join("\n", errors));
                }
            }

            if (GUILayout.Button("Reset To Defaults", GUILayout.Height(28f)))
            {
                var confirmed = EditorUtility.DisplayDialog(
                    "Reset Map Asset",
                    "Reset all map authoring values to defaults?",
                    "Reset",
                    "Cancel");

                if (confirmed)
                {
                    Undo.RecordObject(_mapAsset, "Reset Map Asset");
                    _mapAsset.ResetToDefaults();
                    EditorUtility.SetDirty(_mapAsset);
                    AssetDatabase.SaveAssets();
                    BindSerializedAsset();
                    _hasValidationResult = false;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawValidationResult()
        {
            if (!_hasValidationResult)
            {
                return;
            }

            if (_lastValidationErrors.Count == 0)
            {
                EditorGUILayout.HelpBox("Validation passed.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(string.Join("\n", _lastValidationErrors), MessageType.Error);
        }

        private void DrawEditorBody()
        {
            if (_serializedMapAsset == null)
            {
                BindSerializedAsset();
                if (_serializedMapAsset == null)
                {
                    return;
                }
            }

            _serializedMapAsset.Update();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawProperty(nameof(TopDownMapAuthoringAsset.SchemaVersion));
            DrawProperty(nameof(TopDownMapAuthoringAsset.MapId));
            DrawProperty(nameof(TopDownMapAuthoringAsset.BattleBounds));
            DrawProperty(nameof(TopDownMapAuthoringAsset.ShopZone));
            DrawProperty(nameof(TopDownMapAuthoringAsset.Obstacles));
            DrawProperty(nameof(TopDownMapAuthoringAsset.PlayerSpawns));
            DrawProperty(nameof(TopDownMapAuthoringAsset.CoinSpawners));
            DrawProperty(nameof(TopDownMapAuthoringAsset.Portals));
            EditorGUILayout.EndScrollView();

            if (_serializedMapAsset.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_mapAsset);
            }
        }

        private void DrawProperty(string propertyName)
        {
            var property = _serializedMapAsset.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, includeChildren: true);
            }
        }

        private void RunValidation()
        {
            _hasValidationResult = true;
            if (TopDownMapAuthoringValidator.TryValidate(_mapAsset, out var errors))
            {
                _lastValidationErrors = new List<string>();
                return;
            }

            _lastValidationErrors = errors;
        }

        private void BindSerializedAsset()
        {
            _serializedMapAsset = _mapAsset != null
                ? new SerializedObject(_mapAsset)
                : null;
        }
    }
}
