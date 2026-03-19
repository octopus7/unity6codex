using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace CodexSix.UguiRuntime.Editor
{
    public static class StackedUguiRuntimeMenu
    {
        private const string MenuPath = "Tools/CodexSix/uGUI Runtime/Create Stacked uGUI Demo Scene";
        private const string ImportedSampleRoot = "Assets/CodexSixSamples/StackedUguiDemo";
        private const string SampleMenuTypeName = "CodexSix.UguiRuntime.Samples.StackedUguiDemo.Editor.StackedUguiDemoSampleSceneMenu";
        private const string SampleCreateMethodName = "CreateSampleScene";

        [MenuItem(MenuPath)]
        public static void CreateStackedUguiDemoScene()
        {
            var sampleMenuType = FindType(SampleMenuTypeName);
            if (sampleMenuType == null)
            {
                if (AssetDatabase.IsValidFolder(ImportedSampleRoot))
                {
                    EditorUtility.DisplayDialog(
                        "Stacked uGUI Demo Not Ready",
                        "Sample source already exists at Assets/CodexSixSamples/StackedUguiDemo. Wait for Unity compilation to finish, then run this menu again. If it still fails, check the Console for compile errors.",
                        "OK");
                    return;
                }

                if (TryImportSampleSource())
                {
                    EditorUtility.DisplayDialog(
                        "Stacked uGUI Demo Imported",
                        "Sample source was copied to Assets/CodexSixSamples/StackedUguiDemo. Unity will recompile; run this menu again after compilation finishes.",
                        "OK");
                    return;
                }

                EditorUtility.DisplayDialog(
                    "Stacked uGUI Demo Sample Missing",
                    "The sample source could not be copied from the package. Reimport com.codexsix.ugui.runtime or check that the package exists at Packages/com.codexsix.ugui.runtime.",
                    "OK");
                return;
            }

            var createMethod = sampleMenuType.GetMethod(SampleCreateMethodName, BindingFlags.Public | BindingFlags.Static);
            if (createMethod == null)
            {
                Debug.LogError($"{SampleMenuTypeName}.{SampleCreateMethodName} was not found.");
                return;
            }

            createMethod.Invoke(null, null);
        }

        private static bool TryImportSampleSource()
        {
            var packageInfo = PackageManagerPackageInfo.FindForAssembly(typeof(StackedUguiRuntimeMenu).Assembly);
            if (packageInfo == null)
            {
                return false;
            }

            var sourceRoot = Path.Combine(packageInfo.resolvedPath, "Samples~", "StackedUguiDemo");
            if (!Directory.Exists(sourceRoot))
            {
                return false;
            }

            CopyDirectoryWithoutMeta(sourceRoot, GetImportedSampleAbsolutePath());
            AssetDatabase.Refresh();
            return true;
        }

        private static string GetImportedSampleAbsolutePath()
        {
            return Path.Combine(Application.dataPath, "CodexSixSamples", "StackedUguiDemo");
        }

        private static void CopyDirectoryWithoutMeta(string sourcePath, string targetPath)
        {
            Directory.CreateDirectory(targetPath);

            foreach (var filePath in Directory.GetFiles(sourcePath))
            {
                if (filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = Path.GetFileName(filePath);
                File.Copy(filePath, Path.Combine(targetPath, fileName), true);
            }

            foreach (var directoryPath in Directory.GetDirectories(sourcePath))
            {
                var childName = Path.GetFileName(directoryPath);
                CopyDirectoryWithoutMeta(directoryPath, Path.Combine(targetPath, childName));
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var resolvedType = assembly.GetType(fullName, false);
                if (resolvedType != null)
                {
                    return resolvedType;
                }
            }

            return null;
        }
    }
}
