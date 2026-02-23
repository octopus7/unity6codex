using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace CodexSix.TopdownShooter.EditorTools
{
    public static class TopDownShooterAutoBootstrapAfterCompile
    {
        private const string LastProcessedSourceTicksKey = "TopDownShooter.AutoSafeBootstrapAfterCompile.LastProcessedSourceTicks";
        private const string ScriptsRootRelativePath = "Assets/Scripts";

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            var latestSourceTimestampTicks = GetLatestSourceTimestampTicksUtc();
            if (latestSourceTimestampTicks <= 0L)
            {
                return;
            }

            var lastProcessedTimestampTicks = ReadLastProcessedSourceTimestampTicks();
            if (latestSourceTimestampTicks <= lastProcessedTimestampTicks)
            {
                return;
            }

            EditorApplication.delayCall += () => RunSafeBootstrapAndRecord(latestSourceTimestampTicks);
        }

        private static void RunSafeBootstrapAndRecord(long processedSourceTimestampTicks)
        {
            try
            {
                Debug.Log("Safe Bootstrap executed (trigger: script reload)");
                TopDownShooterBootstrap.BootstrapSceneSafe();
            }
            finally
            {
                WriteLastProcessedSourceTimestampTicks(processedSourceTimestampTicks);
            }
        }

        private static long GetLatestSourceTimestampTicksUtc()
        {
            var absoluteScriptsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScriptsRootRelativePath));
            if (!Directory.Exists(absoluteScriptsPath))
            {
                return 0L;
            }

            long latestTimestampTicks = 0L;
            foreach (var sourceFilePath in Directory.EnumerateFiles(absoluteScriptsPath, "*.cs", SearchOption.AllDirectories))
            {
                var sourceTimestampTicks = File.GetLastWriteTimeUtc(sourceFilePath).Ticks;
                if (sourceTimestampTicks > latestTimestampTicks)
                {
                    latestTimestampTicks = sourceTimestampTicks;
                }
            }

            return latestTimestampTicks;
        }

        private static long ReadLastProcessedSourceTimestampTicks()
        {
            var serializedTicks = EditorPrefs.GetString(LastProcessedSourceTicksKey, "0");
            return long.TryParse(serializedTicks, out var parsedTicks) ? parsedTicks : 0L;
        }

        private static void WriteLastProcessedSourceTimestampTicks(long timestampTicks)
        {
            EditorPrefs.SetString(LastProcessedSourceTicksKey, timestampTicks.ToString());
        }
    }
}
