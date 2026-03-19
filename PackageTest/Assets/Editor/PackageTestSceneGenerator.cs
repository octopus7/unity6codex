using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PackageTestSceneGenerator
{
    private const string SceneDirectory = "Assets/Scenes/Generated";
    private const string ScenePath = SceneDirectory + "/RequestPipelineTestScene.unity";
    private const string SmokeTestTypeName = "CodexSix.RequestPipeline.Debug.RequestPipelineSmokeTestBehaviour, CodexSix.RequestPipeline";

    [MenuItem("Tools/PackageTest/Create Request Pipeline Test Scene")]
    public static void CreateRequestPipelineTestScene()
    {
        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"Opened existing package test scene at {ScenePath}");
            return;
        }

        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes", "Generated"));
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var root = new GameObject("RequestPipelineTestRoot");
        var componentType = Type.GetType(SmokeTestTypeName);
        if (componentType == null)
        {
            throw new InvalidOperationException(
                $"Could not resolve package component '{SmokeTestTypeName}'. Check that the local package dependency imported correctly.");
        }

        root.AddComponent(componentType);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();

        Debug.Log($"Created package test scene at {ScenePath}");
    }
}
