#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using com.IvanMurzak.Unity.MCP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace McpTest.RuntimeMcpDemo
{
    public static class RuntimeMcpGridBootstrap
    {
        const string ConfigFileName = "AI-Game-Developer-Config.json";
        const string ConfigFolderName = "UserSettings";
        const string RuntimeHostEnvVar = "UNITY_MCP_RUNTIME_HOST";
        const string RuntimeTokenEnvVar = "UNITY_MCP_RUNTIME_TOKEN";
        const string HostEnvVar = "UNITY_MCP_HOST";
        const string TokenEnvVar = "UNITY_MCP_TOKEN";
        const string AllowEditorConfigEnvVar = "UNITY_MCP_RUNTIME_ALLOW_EDITOR_CONFIG";
        const string BowlingScenePath = "Assets/Games/Bowling/Scenes/BowlingGame.unity";
        const string VoxelVillageScenePath = "Assets/Games/VoxelVillage/Scenes/VoxelVillage.unity";

        static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            var activeScenePath = SceneManager.GetActiveScene().path;
            if (activeScenePath == BowlingScenePath || activeScenePath == VoxelVillageScenePath)
            {
                return;
            }

            var demo = RuntimeMcpGridDemo.EnsureExists();
            var settings = RuntimeMcpConnectionSettings.Load();
            if (!settings.IsUsable)
            {
                var missingConfigMessage = settings.StatusMessage;

                demo.SetConnectionStatus(missingConfigMessage);
                Debug.LogWarning(missingConfigMessage);
                return;
            }

            var plugin = UnityMcpPluginRuntime.Initialize(builder =>
            {
                var runtimeConfig = new UnityMcpPlugin.UnityConnectionConfig
                {
                    ConnectionMode = ConnectionMode.Custom,
                    KeepConnected = true
                };

                runtimeConfig.Host = settings.Host;
                runtimeConfig.Token = settings.Token;

                builder.SetConfig(runtimeConfig);

                builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly());
                builder.WithPromptsFromAssembly(Assembly.GetExecutingAssembly());
            }).Build();

            demo.SetConnectionStatus("Runtime MCP configured for " + settings.Host);
            _ = ConnectAsync(plugin, settings.Host);
        }

        static async Task ConnectAsync(UnityMcpPluginRuntime plugin, string host)
        {
            try
            {
                RuntimeMcpGridDemo.Instance.SetConnectionStatus("Runtime MCP connecting to " + host + "...");

                var connected = await plugin.ConnectIfNeeded();
                RuntimeMcpGridDemo.Instance.SetConnectionStatus(
                    connected
                        ? "Runtime MCP connected to " + host
                        : "Runtime MCP failed to connect to " + host);
            }
            catch (Exception exception)
            {
                RuntimeMcpGridDemo.Instance.SetConnectionStatus("Runtime MCP connection failed. Check the Unity console.");
                Debug.LogException(exception);
            }
        }

        [Serializable]
        sealed class EditorMcpConfigFile
        {
            public string host = string.Empty;
            public string token = string.Empty;
        }

        readonly struct RuntimeMcpConnectionSettings
        {
            public RuntimeMcpConnectionSettings(string host, string? token, string statusMessage)
            {
                Host = host;
                Token = token;
                StatusMessage = statusMessage;
            }

            public string Host { get; }

            public string? Token { get; }

            public string StatusMessage { get; }

            public bool IsUsable => !string.IsNullOrWhiteSpace(Host);

            public static RuntimeMcpConnectionSettings Load()
            {
                string? host = null;
                string? token = null;
                var allowEditorConfigFallback = IsEnabled(Environment.GetEnvironmentVariable(AllowEditorConfigEnvVar));

                var runtimeEnvHost = Environment.GetEnvironmentVariable(RuntimeHostEnvVar);
                var runtimeEnvToken = Environment.GetEnvironmentVariable(RuntimeTokenEnvVar);
                var envHost = Environment.GetEnvironmentVariable(HostEnvVar);
                var envToken = Environment.GetEnvironmentVariable(TokenEnvVar);

                if (!string.IsNullOrWhiteSpace(runtimeEnvHost))
                {
                    host = runtimeEnvHost;
                }
                else if (!string.IsNullOrWhiteSpace(envHost))
                {
                    host = envHost;
                }

                if (!string.IsNullOrWhiteSpace(runtimeEnvToken))
                {
                    token = runtimeEnvToken;
                }
                else if (!string.IsNullOrWhiteSpace(envToken))
                {
                    token = envToken;
                }

                if (string.IsNullOrWhiteSpace(host) && allowEditorConfigFallback)
                {
                    var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                    if (!string.IsNullOrWhiteSpace(projectRoot))
                    {
                        var configPath = Path.Combine(projectRoot, ConfigFolderName, ConfigFileName);
                        if (File.Exists(configPath))
                        {
                            try
                            {
                                var json = File.ReadAllText(configPath);
                                var fileConfig = JsonUtility.FromJson<EditorMcpConfigFile>(json);
                                host = fileConfig != null ? fileConfig.host : null;
                                token = fileConfig != null ? fileConfig.token : null;
                            }
                            catch (Exception exception)
                            {
                                Debug.LogWarning("Failed to read runtime MCP config file: " + exception.Message);
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(host))
                {
                    var source =
                        !string.IsNullOrWhiteSpace(runtimeEnvHost) ? "runtime environment" :
                        allowEditorConfigFallback && string.IsNullOrWhiteSpace(envHost) ? "editor config fallback" :
                        "environment";

                    return new RuntimeMcpConnectionSettings(
                        host.Trim(),
                        !string.IsNullOrWhiteSpace(token) ? token.Trim() : null,
                        "Runtime MCP ready via " + source + ".");
                }

                return new RuntimeMcpConnectionSettings(
                    string.Empty,
                    null,
                    "Runtime MCP is idle. Set UNITY_MCP_RUNTIME_HOST to a dedicated runtime server, or set " +
                    AllowEditorConfigEnvVar +
                    "=1 to explicitly reuse the editor config. Reusing the editor's no-auth localhost server will disconnect the editor MCP session.");
            }

            static bool IsEnabled(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                switch (value.Trim().ToLowerInvariant())
                {
                    case "1":
                    case "true":
                    case "yes":
                    case "on":
                        return true;

                    default:
                        return false;
                }
            }
        }
    }
}
