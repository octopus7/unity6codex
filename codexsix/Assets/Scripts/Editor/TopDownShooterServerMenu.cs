using System;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace CodexSix.TopdownShooter.EditorTools
{
    public static class TopDownShooterServerMenu
    {
        private const string ServerBatchRelativePath = "server/run-local-server.bat";
        private const string BotBatchRelativePath = "server/run-local-bot-client.bat";
        private const string AppSettingsRelativePath = "server/TopdownShooter.Server/appsettings.json";
        private const int DefaultPort = 7777;

        [MenuItem("Tools/Start Local Server", false, 5000)]
        public static void StartLocalServer()
        {
#if !UNITY_EDITOR_WIN
            EditorUtility.DisplayDialog("Unsupported Platform", "This menu currently supports Windows only (BAT launch).", "OK");
            return;
#else
            var repoRoot = FindRepoRoot();
            if (string.IsNullOrEmpty(repoRoot))
            {
                EditorUtility.DisplayDialog("Path Error", "Could not locate repository root containing /server.", "OK");
                return;
            }

            var batchPath = Path.Combine(repoRoot, ServerBatchRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(batchPath))
            {
                EditorUtility.DisplayDialog("Missing File", $"Batch file not found:\n{batchPath}", "OK");
                return;
            }

            var appSettingsPath = Path.Combine(repoRoot, AppSettingsRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var port = ReadServerPort(appSettingsPath, DefaultPort);

            if (IsPortListening("127.0.0.1", port))
            {
                EditorUtility.DisplayDialog("Server Already Running", $"A listener is already active on port {port}.", "OK");
                return;
            }

            StartBatch(batchPath, "server");
#endif
        }

        [MenuItem("Tools/Start Local Bot Client", false, 5001)]
        public static void StartLocalBotClient()
        {
#if !UNITY_EDITOR_WIN
            EditorUtility.DisplayDialog("Unsupported Platform", "This menu currently supports Windows only (BAT launch).", "OK");
            return;
#else
            var repoRoot = FindRepoRoot();
            if (string.IsNullOrEmpty(repoRoot))
            {
                EditorUtility.DisplayDialog("Path Error", "Could not locate repository root containing /server.", "OK");
                return;
            }

            var batchPath = Path.Combine(repoRoot, BotBatchRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(batchPath))
            {
                EditorUtility.DisplayDialog("Missing File", $"Batch file not found:\n{batchPath}", "OK");
                return;
            }

            StartBatch(batchPath, "bot client");
#endif
        }

        private static string FindRepoRoot()
        {
            var unityProjectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(unityProjectRoot))
            {
                return null;
            }

            var cursor = new DirectoryInfo(unityProjectRoot);
            while (cursor != null)
            {
                var serverFolder = Path.Combine(cursor.FullName, "server");
                if (Directory.Exists(serverFolder))
                {
                    return cursor.FullName;
                }

                cursor = cursor.Parent;
            }

            return null;
        }

        private static int ReadServerPort(string appSettingsPath, int fallbackPort)
        {
            try
            {
                if (!File.Exists(appSettingsPath))
                {
                    return fallbackPort;
                }

                var json = File.ReadAllText(appSettingsPath);
                var match = Regex.Match(json, "\"listenPort\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedPort))
                {
                    return parsedPort;
                }
            }
            catch
            {
            }

            return fallbackPort;
        }

        private static bool IsPortListening(string host, int port)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var completed = connectTask.Wait(millisecondsTimeout: 150);
                return completed && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static void StartBatch(string batchPath, string label)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = batchPath,
                    WorkingDirectory = Path.GetDirectoryName(batchPath) ?? string.Empty,
                    UseShellExecute = true
                };

                Process.Start(processStartInfo);
                UnityEngine.Debug.Log($"Requested {label} start via BAT: {batchPath}");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Launch Failed", exception.Message, "OK");
            }
        }
    }
}
