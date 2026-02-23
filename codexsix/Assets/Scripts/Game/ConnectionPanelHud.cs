using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using CodexSix.TopdownShooter.Net;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class ConnectionPanelHud : MonoBehaviour
    {
        private const string KnownServersPrefKey = "codexsix.debughud.known_servers";
        private const int MaxKnownServers = 8;
        private const int ProbeTimeoutMs = 1500;
        private const float AutoProbeIntervalSeconds = 1f;

        public NetworkGameClient Client;
        public float UiScale = 2f;
        public bool HideWhenConnected = true;

        private readonly List<string> _knownServers = new();
        private string _host = "127.0.0.1";
        private string _port = "7777";
        private string _nickname = "Player";
        private string _pendingEndpoint = string.Empty;
        private string _probeStatus = "-";
        private bool _probeInProgress;
        private bool _knownServersLoaded;
        private bool _connectButtonPressed;
        private bool _windowPlaced;
        private float _autoProbeElapsed;
        private string _autoProbeEndpoint = string.Empty;
        private ConnectionState _previousConnectionState = ConnectionState.Disconnected;
        private Rect _windowRect = new(0f, 0f, 360f, 220f);

        private void Awake()
        {
            LoadKnownServers();
        }

        private void Update()
        {
            if (Client == null)
            {
                return;
            }

            var state = Client.CurrentConnectionState;
            if (_previousConnectionState != state)
            {
                if (state == ConnectionState.Connected)
                {
                    RememberSuccessfulEndpoint();
                    _pendingEndpoint = string.Empty;
                }
                else if (state == ConnectionState.Disconnected)
                {
                    _pendingEndpoint = string.Empty;
                }

                _previousConnectionState = state;
            }

            UpdateAutoProbe(state);
        }

        private void OnGUI()
        {
            if (Client == null)
            {
                return;
            }

            if (HideWhenConnected && Client.CurrentConnectionState == ConnectionState.Connected)
            {
                return;
            }

            if (UiScale <= 0.01f)
            {
                UiScale = 2f;
            }

            var scale = Mathf.Max(1f, UiScale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var viewWidth = Screen.width / scale;
            var viewHeight = Screen.height / scale;
            EnsureWindowPlaced(viewWidth, viewHeight);

            _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Connection");
            GUI.matrix = previousMatrix;
        }

        private void EnsureWindowPlaced(float viewWidth, float viewHeight)
        {
            if (_windowPlaced)
            {
                return;
            }

            _windowRect.x = (viewWidth - _windowRect.width) * 0.5f;
            _windowRect.y = (viewHeight - _windowRect.height) * 0.5f;
            _windowPlaced = true;
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("TCP Server");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Host", GUILayout.Width(50));
            _host = GUILayout.TextField(_host, GUILayout.Width(190));
            GUILayout.Label("Port", GUILayout.Width(40));
            _port = GUILayout.TextField(_port, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Nick", GUILayout.Width(50));
            _nickname = GUILayout.TextField(_nickname, GUILayout.Width(190));
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            using (new GUIEnabledScope(Client.CurrentConnectionState != ConnectionState.Connecting))
            {
                if (GUILayout.Button("Connect", GUILayout.Height(28f)))
                {
                    var port = 7777;
                    if (!TryParsePort(_port, out port))
                    {
                        port = 7777;
                    }

                    _connectButtonPressed = true;
                    _pendingEndpoint = BuildEndpointKey(_host, port);
                    Client.Connect(_host, port, _nickname);
                }
            }

            using (new GUIEnabledScope(Client.CurrentConnectionState == ConnectionState.Connected))
            {
                if (GUILayout.Button("Disconnect", GUILayout.Height(28f)))
                {
                    Client.Disconnect();
                }
            }

            GUILayout.EndHorizontal();
            DrawProbeSection();

            GUI.DragWindow();
        }

        private void DrawProbeSection()
        {
            if (!TryParsePort(_port, out var parsedPort))
            {
                return;
            }

            var endpoint = BuildEndpointKey(_host, parsedPort);
            var knownServer = IsKnownServer(endpoint);

            GUILayout.Space(4f);
            if (!knownServer)
            {
                return;
            }

            GUILayout.Label($"Response: {_probeStatus}");
        }

        private void UpdateAutoProbe(ConnectionState state)
        {
            if (!TryParsePort(_port, out var parsedPort))
            {
                _autoProbeElapsed = 0f;
                _autoProbeEndpoint = string.Empty;
                return;
            }

            var endpoint = BuildEndpointKey(_host, parsedPort);
            if (_autoProbeEndpoint != endpoint)
            {
                _autoProbeEndpoint = endpoint;
                _autoProbeElapsed = 0f;
                if (!_probeInProgress)
                {
                    _probeStatus = "-";
                }
            }

            var shouldAutoProbe =
                !_connectButtonPressed &&
                state == ConnectionState.Disconnected &&
                IsKnownServer(endpoint);

            if (!shouldAutoProbe)
            {
                _autoProbeElapsed = 0f;
                return;
            }

            _autoProbeElapsed += Time.unscaledDeltaTime;
            if (_autoProbeElapsed < AutoProbeIntervalSeconds || _probeInProgress)
            {
                return;
            }

            _autoProbeElapsed = 0f;
            _ = ProbeEndpointAsync(_host, parsedPort);
        }

        private async Task ProbeEndpointAsync(string host, int port)
        {
            if (_probeInProgress)
            {
                return;
            }

            _probeInProgress = true;
            _probeStatus = "Checking...";

            TcpClient probeClient = null;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                probeClient = new TcpClient();
                var connectTask = probeClient.ConnectAsync(host, port);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(ProbeTimeoutMs));
                if (completedTask != connectTask)
                {
                    _probeStatus = $"Timeout > {ProbeTimeoutMs} ms";
                    return;
                }

                await connectTask;
                stopwatch.Stop();
                _probeStatus = $"{stopwatch.ElapsedMilliseconds} ms (tcp)";
            }
            catch (Exception exception)
            {
                _probeStatus = $"Unavailable ({exception.GetType().Name})";
            }
            finally
            {
                try
                {
                    probeClient?.Close();
                }
                catch
                {
                }

                _probeInProgress = false;
            }
        }

        private void RememberSuccessfulEndpoint()
        {
            if (!_knownServersLoaded)
            {
                LoadKnownServers();
            }

            var endpoint = _pendingEndpoint;
            if (string.IsNullOrEmpty(endpoint) && TryParsePort(_port, out var parsedPort))
            {
                endpoint = BuildEndpointKey(_host, parsedPort);
            }

            if (string.IsNullOrEmpty(endpoint))
            {
                return;
            }

            var existingIndex = _knownServers.FindIndex(entry => entry == endpoint);
            if (existingIndex >= 0)
            {
                _knownServers.RemoveAt(existingIndex);
            }

            _knownServers.Insert(0, endpoint);
            if (_knownServers.Count > MaxKnownServers)
            {
                _knownServers.RemoveRange(MaxKnownServers, _knownServers.Count - MaxKnownServers);
            }

            SaveKnownServers();
        }

        private bool IsKnownServer(string endpoint)
        {
            if (!_knownServersLoaded)
            {
                LoadKnownServers();
            }

            return _knownServers.Contains(endpoint);
        }

        private void LoadKnownServers()
        {
            _knownServers.Clear();

            var serialized = PlayerPrefs.GetString(KnownServersPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(serialized))
            {
                var entries = serialized.Split('\n');
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i].Trim();
                    if (string.IsNullOrEmpty(entry))
                    {
                        continue;
                    }

                    if (_knownServers.Contains(entry))
                    {
                        continue;
                    }

                    _knownServers.Add(entry);
                    if (_knownServers.Count >= MaxKnownServers)
                    {
                        break;
                    }
                }
            }

            _knownServersLoaded = true;
        }

        private void SaveKnownServers()
        {
            var serialized = string.Join("\n", _knownServers);
            PlayerPrefs.SetString(KnownServersPrefKey, serialized);
            PlayerPrefs.Save();
        }

        private static bool TryParsePort(string rawPort, out int port)
        {
            if (!int.TryParse(rawPort, out port))
            {
                return false;
            }

            return port is > 0 and <= 65535;
        }

        private static string BuildEndpointKey(string host, int port)
        {
            return $"{host.Trim().ToLowerInvariant()}:{port}";
        }

        private readonly struct GUIEnabledScope : IDisposable
        {
            private readonly bool _previous;

            public GUIEnabledScope(bool enabled)
            {
                _previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = _previous;
            }
        }
    }
}
