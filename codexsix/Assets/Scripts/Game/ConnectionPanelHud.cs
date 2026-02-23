using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
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
        private const float MinimumLoadingDurationSeconds = 0.2f;

        public NetworkGameClient Client;
        public float UiScale = 2f;
        public bool HideWhenConnected = true;

        [Header("Startup Loading")]
        public float StartupLoadingSeconds = 0.2f;
        public float StartupCompletedHoldSeconds = 0f;

        private readonly List<string> _knownServers = new();
        private string _host = "127.0.0.1";
        private string _port = "7777";
        private string _nickname = "Player";
        private string _pendingEndpoint = string.Empty;
        private string _probeStatus = "-";
        private bool _probeInProgress;
        private bool _probeCompleted;
        private bool _probeVersionCompatible;
        private bool _probeHasServerBuildVersion;
        private int _probedServerBuildVersion;
        private bool _knownServersLoaded;
        private bool _windowPlaced;
        private float _autoProbeElapsed;
        private float _startupSequenceStartRealtime;
        private string _autoProbeEndpoint = string.Empty;
        private string _connectDisabledTooltip = string.Empty;
        private ConnectionState _previousConnectionState = ConnectionState.Disconnected;
        private Rect _windowRect = new(0f, 0f, 360f, 220f);
        private Rect _connectButtonRect;
        private GUIStyle _connectTooltipStyle;

        private void Awake()
        {
            LoadKnownServers();
            BeginStartupSequence();
        }

        private void OnEnable()
        {
            if (_startupSequenceStartRealtime <= 0f)
            {
                BeginStartupSequence();
            }
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
                    _autoProbeElapsed = AutoProbeIntervalSeconds;
                }

                _previousConnectionState = state;
            }

            if (!IsConnectionPanelReady())
            {
                _autoProbeElapsed = 0f;
                return;
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

            if (IsShowingStartupProgress())
            {
                DrawStartupProgress(viewWidth, viewHeight);
                GUI.matrix = previousMatrix;
                return;
            }

            if (IsShowingStartupCompleted())
            {
                DrawStartupCompleted(viewWidth, viewHeight);
                GUI.matrix = previousMatrix;
                return;
            }

            EnsureWindowPlaced(viewWidth, viewHeight);

            _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Connection");
            GUI.matrix = previousMatrix;
        }

        private bool IsConnectionPanelReady()
        {
            var totalDuration = GetStartupLoadingDuration() + GetStartupCompletedHoldDuration();
            return GetStartupSequenceElapsed() >= totalDuration;
        }

        private bool IsShowingStartupProgress()
        {
            return GetStartupSequenceElapsed() < GetStartupLoadingDuration();
        }

        private bool IsShowingStartupCompleted()
        {
            var loadingDuration = GetStartupLoadingDuration();
            var completionHoldDuration = GetStartupCompletedHoldDuration();
            var elapsed = GetStartupSequenceElapsed();
            return completionHoldDuration > 0f &&
                   elapsed >= loadingDuration &&
                   elapsed < loadingDuration + completionHoldDuration;
        }

        private float GetStartupLoadingDuration()
        {
            return Mathf.Max(MinimumLoadingDurationSeconds, StartupLoadingSeconds);
        }

        private float GetStartupCompletedHoldDuration()
        {
            return Mathf.Max(0f, StartupCompletedHoldSeconds);
        }

        private void DrawStartupProgress(float viewWidth, float viewHeight)
        {
            var panelWidth = 360f;
            var panelHeight = 110f;
            var panelRect = new Rect(
                (viewWidth - panelWidth) * 0.5f,
                (viewHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUILayout.BeginArea(panelRect, "Loading", GUI.skin.window);
            var progress = Mathf.Clamp01(GetStartupSequenceElapsed() / GetStartupLoadingDuration());
            GUILayout.Label($"Loading... {Mathf.RoundToInt(progress * 100f)}%");
            var progressRect = GUILayoutUtility.GetRect(326f, 18f);
            DrawProgressBar(progressRect, progress);
            GUILayout.EndArea();
        }

        private void DrawStartupCompleted(float viewWidth, float viewHeight)
        {
            var panelWidth = 320f;
            var panelHeight = 80f;
            var panelRect = new Rect(
                (viewWidth - panelWidth) * 0.5f,
                (viewHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUILayout.BeginArea(panelRect, "Loading", GUI.skin.window);
            GUILayout.Space(20f);
            GUILayout.Label("Loading complete");
            GUILayout.EndArea();
        }

        private static void DrawProgressBar(Rect rect, float progress01)
        {
            var clampedProgress = Mathf.Clamp01(progress01);
            GUI.Box(rect, GUIContent.none);

            const float border = 2f;
            var innerWidth = Mathf.Max(0f, rect.width - (border * 2f));
            var fillWidth = innerWidth * clampedProgress;
            var fillRect = new Rect(rect.x + border, rect.y + border, fillWidth, Mathf.Max(0f, rect.height - (border * 2f)));

            var previousColor = GUI.color;
            GUI.color = new Color(0.2f, 0.75f, 1f, 0.95f);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void BeginStartupSequence()
        {
            _startupSequenceStartRealtime = Time.realtimeSinceStartup;
        }

        private float GetStartupSequenceElapsed()
        {
            if (_startupSequenceStartRealtime <= 0f)
            {
                BeginStartupSequence();
            }

            var elapsed = Time.realtimeSinceStartup - _startupSequenceStartRealtime;
            if (elapsed < 0f)
            {
                return 0f;
            }

            return elapsed;
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

            var hasValidPort = TryParsePort(_port, out var parsedPort);
            var endpoint = hasValidPort
                ? BuildEndpointKey(_host, parsedPort)
                : string.Empty;
            var canConnect = CanConnectToEndpoint(endpoint, hasValidPort, out var blockedReason);

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            var connectButtonContent = new GUIContent("Connect", canConnect ? string.Empty : blockedReason);
            using (new GUIEnabledScope(canConnect))
            {
                if (GUILayout.Button(connectButtonContent, GUILayout.Height(28f)))
                {
                    _pendingEndpoint = endpoint;
                    Client.Connect(_host, parsedPort, _nickname);
                }
            }
            _connectButtonRect = GUILayoutUtility.GetLastRect();
            _connectDisabledTooltip = canConnect ? string.Empty : blockedReason;

            using (new GUIEnabledScope(Client.CurrentConnectionState == ConnectionState.Connected))
            {
                if (GUILayout.Button("Disconnect", GUILayout.Height(28f)))
                {
                    Client.Disconnect();
                }
            }

            GUILayout.EndHorizontal();
            DrawProbeSection(hasValidPort);
            DrawDisabledConnectTooltip();

            GUI.DragWindow();
        }

        private void DrawProbeSection(bool hasValidPort)
        {
            GUILayout.Space(4f);
            if (!hasValidPort)
            {
                GUILayout.Label("Response: 포트가 올바르지 않습니다.");
                return;
            }

            if (_probeInProgress)
            {
                GUILayout.Label("Response: 확인 중...");
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
                _autoProbeElapsed = AutoProbeIntervalSeconds;
                _probeStatus = "-";
                _probeCompleted = false;
                _probeVersionCompatible = false;
                _probeHasServerBuildVersion = false;
                _probedServerBuildVersion = 0;
            }

            var shouldAutoProbe = state == ConnectionState.Disconnected;

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
            _probeStatus = "확인 중...";
            _probeCompleted = false;
            _probeVersionCompatible = false;
            _probeHasServerBuildVersion = false;
            _probedServerBuildVersion = 0;

            TcpClient probeClient = null;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                probeClient = new TcpClient();
                var connectTask = probeClient.ConnectAsync(host, port);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(ProbeTimeoutMs));
                if (completedTask != connectTask)
                {
                    _probeStatus = $"시간 초과 > {ProbeTimeoutMs} ms";
                    _probeCompleted = true;
                    return;
                }

                await connectTask;
                probeClient.NoDelay = true;

                using var stream = probeClient.GetStream();
                using var timeoutCts = new CancellationTokenSource(ProbeTimeoutMs);
                var token = timeoutCts.Token;

                var pingFrame = NetProtocolCodec.EncodePing(sequence: 1u, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await stream.WriteAsync(pingFrame, 0, pingFrame.Length, token);

                var header = await ReadExactlyAsync(stream, NetProtocolCodec.HeaderSize, token);
                if (header == null ||
                    !NetProtocolCodec.TryParseHeader(header, out var payloadLength, out var messageType, out var version, out _))
                {
                    _probeStatus = "프로토콜 응답이 올바르지 않습니다.";
                    _probeCompleted = true;
                    return;
                }

                if (version != NetProtocolCodec.ProtocolVersion)
                {
                    _probeStatus = $"프로토콜 불일치 (server={version}, client={NetProtocolCodec.ProtocolVersion})";
                    _probeCompleted = true;
                    return;
                }

                var payload = await ReadExactlyAsync(stream, payloadLength, token);
                if (payload == null)
                {
                    _probeStatus = "응답 페이로드가 없습니다.";
                    _probeCompleted = true;
                    return;
                }

                if (messageType == MessageType.Error)
                {
                    var error = NetProtocolCodec.DecodeError(payload);
                    _probeStatus = $"Error {error.ErrorCode}: {error.Message}";
                    _probeCompleted = true;
                    return;
                }

                if (messageType != MessageType.Pong)
                {
                    _probeStatus = $"예상치 못한 응답 ({messageType})";
                    _probeCompleted = true;
                    return;
                }

                stopwatch.Stop();
                var pongInfo = NetProtocolCodec.DecodePongInfo(payload);
                _probeCompleted = true;
                _probeHasServerBuildVersion = pongInfo.HasServerBuildVersion;
                _probedServerBuildVersion = pongInfo.ServerBuildVersion;

                if (!pongInfo.HasServerBuildVersion)
                {
                    _probeStatus = $"{stopwatch.ElapsedMilliseconds} ms / 서버 버전 정보 없음";
                    return;
                }

                _probeVersionCompatible = pongInfo.ServerBuildVersion == NetProtocolCodec.ExpectedServerBuildVersion;
                if (_probeVersionCompatible)
                {
                    _probeStatus = $"{stopwatch.ElapsedMilliseconds} ms / 서버 v{pongInfo.ServerBuildVersion}";
                }
                else
                {
                    _probeStatus =
                        $"{stopwatch.ElapsedMilliseconds} ms / 서버 v{pongInfo.ServerBuildVersion} (필요 v{NetProtocolCodec.ExpectedServerBuildVersion})";
                }
            }
            catch (OperationCanceledException)
            {
                _probeStatus = $"시간 초과 > {ProbeTimeoutMs} ms";
                _probeCompleted = true;
            }
            catch (Exception exception)
            {
                _probeStatus = $"서버 응답 불가 ({exception.GetType().Name})";
                _probeCompleted = true;
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

        private bool CanConnectToEndpoint(string endpoint, bool hasValidPort, out string blockedReason)
        {
            blockedReason = string.Empty;
            if (Client.CurrentConnectionState == ConnectionState.Connected)
            {
                blockedReason = "이미 연결되어 있습니다.";
                return false;
            }

            if (Client.CurrentConnectionState == ConnectionState.Connecting)
            {
                blockedReason = "연결 중입니다.";
                return false;
            }

            if (!hasValidPort)
            {
                blockedReason = "포트가 올바르지 않습니다.";
                return false;
            }

            if (_probeInProgress)
            {
                blockedReason = "서버 버전을 확인 중입니다.";
                return false;
            }

            if (_autoProbeEndpoint != endpoint || !_probeCompleted)
            {
                blockedReason = "접속 전 서버 응답을 확인하는 중입니다.";
                return false;
            }

            if (!_probeHasServerBuildVersion)
            {
                blockedReason = "핑 응답에 서버 버전이 없어 접속할 수 없습니다.";
                return false;
            }

            if (!_probeVersionCompatible)
            {
                blockedReason =
                    $"서버 버전이 다릅니다. 서버 v{_probedServerBuildVersion}, 필요 v{NetProtocolCodec.ExpectedServerBuildVersion}.";
                return false;
            }

            return true;
        }

        private void DrawDisabledConnectTooltip()
        {
            if (string.IsNullOrWhiteSpace(_connectDisabledTooltip))
            {
                return;
            }

            if (!_connectButtonRect.Contains(Event.current.mousePosition))
            {
                return;
            }

            EnsureConnectTooltipStyle();
            var tooltipWidth = Mathf.Max(220f, _connectButtonRect.width + 50f);
            var tooltipContent = new GUIContent(_connectDisabledTooltip);
            var textHeight = _connectTooltipStyle.CalcHeight(tooltipContent, tooltipWidth - 14f);
            var tooltipHeight = Mathf.Max(30f, textHeight + 10f);
            var tooltipRect = new Rect(
                _connectButtonRect.xMin,
                _connectButtonRect.yMax + 6f,
                tooltipWidth,
                tooltipHeight);

            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(tooltipRect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            var textRect = new Rect(tooltipRect.x + 7f, tooltipRect.y + 5f, tooltipRect.width - 14f, tooltipRect.height - 10f);
            GUI.Label(textRect, tooltipContent, _connectTooltipStyle);
        }

        private void EnsureConnectTooltipStyle()
        {
            if (_connectTooltipStyle != null)
            {
                return;
            }

            _connectTooltipStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            _connectTooltipStyle.normal.textColor = Color.white;
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

        private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int length, CancellationToken token)
        {
            if (length == 0)
            {
                return Array.Empty<byte>();
            }

            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = await stream.ReadAsync(buffer, offset, length - offset, token);
                if (read == 0)
                {
                    return null;
                }

                offset += read;
            }

            return buffer;
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
