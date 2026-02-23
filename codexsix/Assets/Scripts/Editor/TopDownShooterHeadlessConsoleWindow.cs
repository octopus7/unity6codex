using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace CodexSix.TopdownShooter.EditorTools
{
    public sealed class TopDownShooterHeadlessConsoleWindow : EditorWindow
    {
        private const string ServerBatchRelativePath = "server/run-local-server.bat";
        private const string BotBatchRelativePath = "server/run-local-bot-client.bat";
        private const string AppSettingsRelativePath = "server/TopdownShooter.Server/appsettings.json";
        private const int DefaultPort = 7777;
        private const int MaxBufferedLines = 2000;
        private const double PollIntervalSeconds = 0.2d;
        private const float MonitorColumnWidth = 260f;

        private static readonly Regex ListenPortRegex =
            new("\"listenPort\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private PaneState _serverPane;
        private PaneState _botPane;

        private string _repoRoot = string.Empty;
        private string _logsFolderPath = string.Empty;
        private string _appSettingsPath = string.Empty;
        private int _botCount = 4;
        private double _lastPollTime;

        public static void OpenWindow()
        {
            var window = GetWindow<TopDownShooterHeadlessConsoleWindow>("Headless Console Monitor");
            window.minSize = new Vector2(920f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _serverPane ??= new PaneState("Server", ServerBatchRelativePath, "server.log");
            _botPane ??= new PaneState("Bot Client", BotBatchRelativePath, "bot.log");

            ResolvePaths();
            ResetProcessReferences();

            _lastPollTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DisposeProcessReference(_serverPane);
            DisposeProcessReference(_botPane);
        }

        private void OnEditorUpdate()
        {
#if !UNITY_EDITOR_WIN
            return;
#else
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastPollTime < PollIntervalSeconds)
            {
                return;
            }

            _lastPollTime = now;

            var changed = false;
            changed |= UpdateProcessState(_serverPane);
            changed |= UpdateProcessState(_botPane);
            changed |= PollPaneLog(_serverPane);
            changed |= PollPaneLog(_botPane);

            if (changed)
            {
                Repaint();
            }
#endif
        }

        private void OnGUI()
        {
#if !UNITY_EDITOR_WIN
            EditorGUILayout.HelpBox("Headless Console Monitor is currently supported on Windows only.", MessageType.Info);
            return;
#else
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(MonitorColumnWidth), GUILayout.ExpandHeight(true)))
                {
                    DrawTopToolbar();
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    DrawPane(_serverPane, isServer: true);
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    DrawPane(_botPane, isServer: false);
                }
            }
#endif
        }

        private void DrawTopToolbar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Headless Local Server/Bot Monitor", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Bot Count", GUILayout.Width(60f));
                    _botCount = Mathf.Clamp(EditorGUILayout.IntField(_botCount, GUILayout.Width(52f)), 1, 512);
                }

                using (new EditorGUI.DisabledScope(!HasValidPaths()))
                {
                    if (GUILayout.Button("Start All"))
                    {
                        StartServer();
                        StartBot();
                    }

                    if (GUILayout.Button("Stop All"))
                    {
                        StopBot();
                        StopServer();
                    }

                    if (GUILayout.Button("Restart All"))
                    {
                        StopBot();
                        StopServer();
                        StartServer();
                        StartBot();
                    }
                }

                if (string.IsNullOrEmpty(_repoRoot))
                {
                    EditorGUILayout.HelpBox(
                        "Could not locate repository root containing /server. Start actions are disabled.",
                        MessageType.Error);
                }
            }
        }

        private void DrawPane(PaneState pane, bool isServer)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(pane.DisplayName, EditorStyles.boldLabel, GUILayout.Width(72f));
                    EditorGUILayout.LabelField($"Status: {pane.StatusText}", GUILayout.Width(128f));

                    using (new EditorGUI.DisabledScope(!HasValidPaths()))
                    {
                        if (GUILayout.Button("Start"))
                        {
                            if (isServer)
                            {
                                StartServer();
                            }
                            else
                            {
                                StartBot();
                            }
                        }

                        if (GUILayout.Button("Stop"))
                        {
                            if (isServer)
                            {
                                StopServer();
                            }
                            else
                            {
                                StopBot();
                            }
                        }

                        if (GUILayout.Button("Restart"))
                        {
                            if (isServer)
                            {
                                StopServer();
                                StartServer();
                            }
                            else
                            {
                                StopBot();
                                StartBot();
                            }
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    pane.Filter = EditorGUILayout.TextField("Filter", pane.Filter);
                    pane.AutoScroll = EditorGUILayout.ToggleLeft("Auto-scroll", pane.AutoScroll, GUILayout.Width(100f));

                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                    {
                        pane.Lines.Clear();
                        pane.PendingFragment = string.Empty;
                        pane.Scroll = Vector2.zero;
                        pane.NeedsScrollToEnd = false;
                    }
                }

                var visibleText = BuildVisibleText(pane);
                pane.Scroll = EditorGUILayout.BeginScrollView(pane.Scroll, GUILayout.ExpandHeight(true));
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextArea(visibleText, GUILayout.ExpandHeight(true));
                }

                EditorGUILayout.EndScrollView();

                if (pane.AutoScroll && pane.NeedsScrollToEnd)
                {
                    pane.Scroll = new Vector2(pane.Scroll.x, float.MaxValue);
                    pane.NeedsScrollToEnd = false;
                    Repaint();
                }
            }
        }

        private void StartServer()
        {
            StartPane(_serverPane, isServer: true);
        }

        private void StartBot()
        {
            StartPane(_botPane, isServer: false);
        }

        private void StopServer()
        {
            StopPane(_serverPane, isServer: true);
        }

        private void StopBot()
        {
            StopPane(_botPane, isServer: false);
        }

        private void StartPane(PaneState pane, bool isServer)
        {
#if !UNITY_EDITOR_WIN
            AppendMonitorLine(pane, "Windows only feature.");
            return;
#else
            ResolvePaths();
            if (!HasValidPaths())
            {
                pane.StatusText = "Path Error";
                AppendMonitorLine(pane, "Cannot start because repository or BAT path is invalid.");
                return;
            }

            if (string.IsNullOrEmpty(pane.BatchPath) || !File.Exists(pane.BatchPath))
            {
                pane.StatusText = "Missing BAT";
                AppendMonitorLine(pane, $"Batch file not found: {pane.BatchPath}");
                return;
            }

            if (IsProcessRunning(pane.Process))
            {
                pane.StatusText = "Running";
                AppendMonitorLine(pane, "Process is already running.");
                return;
            }

            try
            {
                Directory.CreateDirectory(_logsFolderPath);

                pane.Lines.Clear();
                pane.PendingFragment = string.Empty;
                pane.ReadOffset = 0;
                pane.LastReadError = string.Empty;
                pane.Scroll = Vector2.zero;
                pane.NeedsScrollToEnd = true;

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pane.BatchPath,
                    WorkingDirectory = Path.GetDirectoryName(pane.BatchPath) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                processStartInfo.EnvironmentVariables["TOPDOWN_HEADLESS"] = "1";
                processStartInfo.EnvironmentVariables["TOPDOWN_LOG_PATH"] = pane.LogPath;
                processStartInfo.EnvironmentVariables["TOPDOWN_TRUNCATE_LOG"] = "1";

                if (!isServer)
                {
                    processStartInfo.EnvironmentVariables["TOPDOWN_BOT_ARGS"] = BuildBotArgs(_botCount);
                }

                var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    pane.StatusText = "Launch Failed";
                    AppendMonitorLine(pane, "Process.Start returned null.");
                    return;
                }

                pane.Process = process;
                pane.StatusText = "Running";
                AppendMonitorLine(
                    pane,
                    $"Started {(isServer ? "server" : "bot client")} headless process (pid={process.Id}).");
            }
            catch (Exception exception)
            {
                pane.StatusText = "Launch Failed";
                AppendMonitorLine(pane, $"Launch failed: {exception.Message}");
            }
#endif
        }

        private void StopPane(PaneState pane, bool isServer)
        {
#if !UNITY_EDITOR_WIN
            AppendMonitorLine(pane, "Windows only feature.");
            return;
#else
            var stoppedTrackedProcess = TryStopTrackedProcess(pane);

            if (isServer)
            {
                var port = ReadServerPort(_appSettingsPath, DefaultPort);
                TryKillByPort(port);
            }
            else
            {
                TryKillBotDotnetProcesses();
            }

            pane.StatusText = "Stopped";
            AppendMonitorLine(
                pane,
                stoppedTrackedProcess
                    ? "Stop requested."
                    : "Stop requested (used fallback process cleanup).");
#endif
        }

        private bool UpdateProcessState(PaneState pane)
        {
            if (pane == null || pane.Process == null)
            {
                return false;
            }

            try
            {
                if (!pane.Process.HasExited)
                {
                    if (!string.Equals(pane.StatusText, "Running", StringComparison.Ordinal))
                    {
                        pane.StatusText = "Running";
                        return true;
                    }

                    return false;
                }

                var exitCode = pane.Process.ExitCode;
                DisposeProcessReference(pane);
                pane.StatusText = $"Stopped (Exit {exitCode})";
                AppendMonitorLine(pane, $"Process exited with code {exitCode}.");
                return true;
            }
            catch (Exception exception)
            {
                DisposeProcessReference(pane);
                pane.StatusText = "Stopped";
                AppendMonitorLine(pane, $"Process state read failed: {exception.Message}");
                return true;
            }
        }

        private bool PollPaneLog(PaneState pane)
        {
            if (pane == null || string.IsNullOrEmpty(pane.LogPath) || !File.Exists(pane.LogPath))
            {
                return false;
            }

            try
            {
                using var stream = new FileStream(pane.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length < pane.ReadOffset)
                {
                    pane.ReadOffset = 0;
                    pane.PendingFragment = string.Empty;
                }

                if (stream.Length == pane.ReadOffset)
                {
                    return false;
                }

                stream.Seek(pane.ReadOffset, SeekOrigin.Begin);
                var readBuffer = new byte[8192];
                var chunkBuilder = new StringBuilder();

                int readSize;
                while ((readSize = stream.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    pane.ReadOffset += readSize;
                    chunkBuilder.Append(Encoding.UTF8.GetString(readBuffer, 0, readSize));
                }

                pane.LastReadError = string.Empty;
                return AppendChunkLines(pane, chunkBuilder.ToString());
            }
            catch (Exception exception)
            {
                if (string.Equals(pane.LastReadError, exception.Message, StringComparison.Ordinal))
                {
                    return false;
                }

                pane.LastReadError = exception.Message;
                AppendMonitorLine(pane, $"Log read error: {exception.Message}");
                return true;
            }
        }

        private static bool AppendChunkLines(PaneState pane, string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return false;
            }

            var normalized = chunk.Replace("\r\n", "\n").Replace('\r', '\n');
            var merged = pane.PendingFragment + normalized;
            var startIndex = 0;
            var appendedAny = false;

            for (var index = 0; index < merged.Length; index++)
            {
                if (merged[index] != '\n')
                {
                    continue;
                }

                var line = merged.Substring(startIndex, index - startIndex);
                AppendBufferedLine(pane, line);
                startIndex = index + 1;
                appendedAny = true;
            }

            pane.PendingFragment = startIndex < merged.Length ? merged.Substring(startIndex) : string.Empty;
            return appendedAny;
        }

        private static void AppendBufferedLine(PaneState pane, string line)
        {
            pane.Lines.Add(line);
            if (pane.Lines.Count > MaxBufferedLines)
            {
                pane.Lines.RemoveRange(0, pane.Lines.Count - MaxBufferedLines);
            }

            if (pane.AutoScroll)
            {
                pane.NeedsScrollToEnd = true;
            }
        }

        private static void AppendMonitorLine(PaneState pane, string message)
        {
            AppendBufferedLine(pane, $"[monitor {DateTime.Now:HH:mm:ss}] {message}");
        }

        private static bool IsProcessRunning(Process process)
        {
            if (process == null)
            {
                return false;
            }

            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryStopTrackedProcess(PaneState pane)
        {
            if (pane == null || pane.Process == null)
            {
                return false;
            }

            var stopped = false;
            try
            {
                if (!pane.Process.HasExited)
                {
                    TryKillProcessTree(pane.Process);
                    pane.Process.WaitForExit(1000);
                    stopped = true;
                }
            }
            catch
            {
                stopped = false;
            }
            finally
            {
                DisposeProcessReference(pane);
            }

            return stopped;
        }

        private static void DisposeProcessReference(PaneState pane)
        {
            if (pane == null || pane.Process == null)
            {
                return;
            }

            try
            {
                pane.Process.Dispose();
            }
            catch
            {
            }

            pane.Process = null;
        }

        private static void TryKillProcessTree(Process process)
        {
            var killMethod = typeof(Process).GetMethod(
                "Kill",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(bool) },
                null);

            if (killMethod != null)
            {
                killMethod.Invoke(process, new object[] { true });
                return;
            }

            process.Kill();
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
                process?.WaitForExit(1500);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to stop server by port {port}: {exception.Message}");
            }
        }

        private static void TryKillBotDotnetProcesses()
        {
            try
            {
                var script =
                    "$targets = Get-CimInstance Win32_Process -Filter 'Name = ''dotnet.exe''' -ErrorAction SilentlyContinue | " +
                    "Where-Object { $_.CommandLine -like '*TopdownShooter.BotClient.csproj*' }; " +
                    "foreach($target in $targets) { Stop-Process -Id $target.ProcessId -Force -ErrorAction SilentlyContinue }";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(processStartInfo);
                process?.WaitForExit(1500);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to stop bot client process: {exception.Message}");
            }
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
                var match = ListenPortRegex.Match(json);
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

        private void ResolvePaths()
        {
            _repoRoot = FindRepoRoot();
            if (string.IsNullOrEmpty(_repoRoot))
            {
                _logsFolderPath = string.Empty;
                _appSettingsPath = string.Empty;
                _serverPane.BatchPath = string.Empty;
                _serverPane.LogPath = string.Empty;
                _serverPane.StatusText = "Path Error";
                _botPane.BatchPath = string.Empty;
                _botPane.LogPath = string.Empty;
                _botPane.StatusText = "Path Error";
                return;
            }

            _logsFolderPath = Path.Combine(_repoRoot, "server", ".logs");
            _appSettingsPath = Path.Combine(_repoRoot, AppSettingsRelativePath.Replace('/', Path.DirectorySeparatorChar));

            _serverPane.BatchPath = Path.Combine(_repoRoot, _serverPane.BatchRelativePath.Replace('/', Path.DirectorySeparatorChar));
            _botPane.BatchPath = Path.Combine(_repoRoot, _botPane.BatchRelativePath.Replace('/', Path.DirectorySeparatorChar));
            _serverPane.LogPath = Path.Combine(_logsFolderPath, _serverPane.DefaultLogFileName);
            _botPane.LogPath = Path.Combine(_logsFolderPath, _botPane.DefaultLogFileName);

            _serverPane.StatusText = File.Exists(_serverPane.BatchPath) ? "Stopped" : "Missing BAT";
            _botPane.StatusText = File.Exists(_botPane.BatchPath) ? "Stopped" : "Missing BAT";
        }

        private bool HasValidPaths()
        {
            return !string.IsNullOrEmpty(_repoRoot) &&
                   !string.IsNullOrEmpty(_serverPane.BatchPath) &&
                   !string.IsNullOrEmpty(_botPane.BatchPath) &&
                   File.Exists(_serverPane.BatchPath) &&
                   File.Exists(_botPane.BatchPath);
        }

        private static string BuildBotArgs(int botCount)
        {
            var clampedBotCount = Math.Clamp(botCount, 1, 512);
            return $"--bots={clampedBotCount} --threads=8 --connect-stagger-ms=100 --move-interval-ms=120";
        }

        private static string FindRepoRoot()
        {
            var unityProjectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(unityProjectRoot))
            {
                return string.Empty;
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

            return string.Empty;
        }

        private void ResetProcessReferences()
        {
            DisposeProcessReference(_serverPane);
            DisposeProcessReference(_botPane);
        }

        private static string BuildVisibleText(PaneState pane)
        {
            if (pane.Lines.Count == 0)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(pane.Filter))
            {
                return string.Join("\n", pane.Lines);
            }

            var filter = pane.Filter.Trim();
            var builder = new StringBuilder();
            foreach (var line in pane.Lines)
            {
                if (line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(line);
            }

            return builder.ToString();
        }

        private sealed class PaneState
        {
            public PaneState(string displayName, string batchRelativePath, string defaultLogFileName)
            {
                DisplayName = displayName;
                BatchRelativePath = batchRelativePath;
                DefaultLogFileName = defaultLogFileName;
                StatusText = "Stopped";
                Filter = string.Empty;
                PendingFragment = string.Empty;
                LastReadError = string.Empty;
                Lines = new List<string>(MaxBufferedLines);
            }

            public string DisplayName { get; }

            public string BatchRelativePath { get; }

            public string DefaultLogFileName { get; }

            public string BatchPath { get; set; } = string.Empty;

            public string LogPath { get; set; } = string.Empty;

            public string StatusText { get; set; }

            public string Filter { get; set; }

            public bool AutoScroll { get; set; } = true;

            public bool NeedsScrollToEnd { get; set; }

            public string PendingFragment { get; set; }

            public string LastReadError { get; set; }

            public long ReadOffset { get; set; }

            public Vector2 Scroll { get; set; } = Vector2.zero;

            public List<string> Lines { get; }

            public Process Process { get; set; }
        }
    }
}
