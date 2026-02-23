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
        private const string ServerWindowTitlePrefix = "TopdownShooter.Server";
        private const string BotWindowTitlePrefix = "TopdownShooter.BotClient";
        private const int DefaultPort = 7777;

        [MenuItem("Tools/Server/Open Headless Console Monitor", false, 4999)]
        public static void OpenHeadlessConsoleMonitor()
        {
            TopDownShooterHeadlessConsoleWindow.OpenWindow();
        }

        [MenuItem("Tools/Server/Start Local Server", false, 5000)]
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

        [MenuItem("Tools/Server/Start Local Bot Client", false, 5001)]
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

        [MenuItem("Tools/Restart Server BotClient", false, 5002)]
        public static void StartLocalServerAndBotClient()
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

            var serverBatchPath = Path.Combine(repoRoot, ServerBatchRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(serverBatchPath))
            {
                EditorUtility.DisplayDialog("Missing File", $"Batch file not found:\n{serverBatchPath}", "OK");
                return;
            }

            var botBatchPath = Path.Combine(repoRoot, BotBatchRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(botBatchPath))
            {
                EditorUtility.DisplayDialog("Missing File", $"Batch file not found:\n{botBatchPath}", "OK");
                return;
            }

            var appSettingsPath = Path.Combine(repoRoot, AppSettingsRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var port = ReadServerPort(appSettingsPath, DefaultPort);

            StopExistingLocalProcesses(port);
            StartBatch(serverBatchPath, "server");
            StartBatch(botBatchPath, "bot client");
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

        private static void StopExistingLocalProcesses(int serverPort)
        {
            TryKillByWindowTitle(BotWindowTitlePrefix);
            TryKillByWindowTitle(ServerWindowTitlePrefix);

            for (var attempt = 0; attempt < 5 && IsPortListening("127.0.0.1", serverPort); attempt++)
            {
                TryKillByPort(serverPort);
                System.Threading.Thread.Sleep(100);
            }
        }

        private static void TryKillByWindowTitle(string titlePrefix)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c taskkill /FI \"WINDOWTITLE eq {titlePrefix}*\" /T /F",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(processStartInfo);
                process?.WaitForExit(1000);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to stop process with window title '{titlePrefix}': {exception.Message}");
            }
        }

        private static void TryKillByPort(int port)
        {
            try
            {
                var script = "$ownerPids = Get-NetTCPConnection -LocalPort " + port +
                             " -State Listen -ErrorAction SilentlyContinue | " +
                             "Select-Object -ExpandProperty OwningProcess -Unique; " +
                             "foreach($ownerPid in $ownerPids) { Stop-Process -Id $ownerPid -Force -ErrorAction SilentlyContinue }";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(processStartInfo);
                process?.WaitForExit(1000);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to stop process on port {port}: {exception.Message}");
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
