using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using TopdownShooter.Server.Configuration;
using TopdownShooter.Server.Domain;
using TopdownShooter.Server.Networking;

namespace TopdownShooter.Server.Runtime;

public sealed class GameServer
{
    private readonly ServerConfig _config;
    private readonly GameWorld _world;
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<int, ClientConnection> _connections = new();
    private readonly ConcurrentQueue<InboundEnvelope> _inbound = new();
    private readonly Dictionary<string, ReconnectSession> _sessionsByToken = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _tokenByPlayerId = new();
    private readonly TimeSpan _reconnectGracePeriod;

    private int _nextConnectionId = 1;
    private int _serverSequence;
    private long _nextReconnectSessionPruneUnixMs;

    public GameServer(ServerConfig config)
    {
        _config = config;
        _world = new GameWorld(config.MaxPlayers, config.MaxWorldCoins);
        _listener = new TcpListener(IPAddress.Parse(config.ListenIp), config.ListenPort);
        _reconnectGracePeriod = TimeSpan.FromSeconds(config.ReconnectGraceSeconds);
    }

    public async Task RunAsync(CancellationToken token)
    {
        _listener.Start();
        using var stopRegistration = token.Register(() => _listener.Stop());

        var acceptTask = AcceptLoopAsync(token);
        var tickTask = TickLoopAsync(token);

        try
        {
            await Task.WhenAll(acceptTask, tickTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (var connection in _connections.Values)
            {
                connection.Disconnect();
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            _connections.Clear();
        }
    }

    public string BuildStatusLine()
    {
        return $"players={_world.PlayerCount} projectiles={_world.ProjectileCount} " +
               $"coinStacks={_world.CoinStackCount} worldCoins={_world.WorldCoinTotal} tick={_world.ServerTick}";
    }

    public bool KickPlayer(int playerId)
    {
        foreach (var connection in _connections.Values)
        {
            if (connection.PlayerId != playerId)
            {
                continue;
            }

            connection.Disconnect();
            return true;
        }

        return false;
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient tcpClient;
            try
            {
                tcpClient = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }
            catch (ObjectDisposedException)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            tcpClient.NoDelay = true;
            var connectionId = Interlocked.Increment(ref _nextConnectionId);
            var connection = new ClientConnection(connectionId, tcpClient, OnInboundPacket, OnConnectionClosed);
            _connections[connectionId] = connection;

            Console.WriteLine($"[connect] connection={connectionId}");
            _ = connection.RunReceiveLoopAsync(token);
        }
    }

    private async Task TickLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / _config.TickRateHz));
        var snapshotIntervalSeconds = 1.0 / _config.SnapshotRateHz;
        var snapshotAccumulator = 0.0;

        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            DrainInboundQueue(token);
            _world.Step();
            PruneExpiredReconnectSessions();

            snapshotAccumulator += 1.0 / _config.TickRateHz;
            if (snapshotAccumulator < snapshotIntervalSeconds)
            {
                continue;
            }

            snapshotAccumulator -= snapshotIntervalSeconds;
            await BroadcastSnapshotAndEventsAsync(token).ConfigureAwait(false);
        }
    }

    private void DrainInboundQueue(CancellationToken token)
    {
        while (_inbound.TryDequeue(out var inbound))
        {
            if (inbound.Kind == InboundKind.Disconnected)
            {
                HandleDisconnection(inbound.Connection);
                continue;
            }

            if (inbound.Packet is null)
            {
                continue;
            }

            HandleInboundPacket(inbound.Connection, inbound.Packet.Value, token);
        }
    }

    private void HandleInboundPacket(ClientConnection connection, InboundPacket inboundPacket, CancellationToken token)
    {
        try
        {
            switch (inboundPacket.MessageType)
            {
                case MessageType.Hello:
                    HandleHello(connection, inboundPacket.Sequence, inboundPacket.Payload, token);
                    break;
                case MessageType.InputFrame:
                    HandleInputFrame(connection, inboundPacket.Payload);
                    break;
                case MessageType.ShopPurchaseRequest:
                    HandleShopPurchase(connection, inboundPacket.Payload);
                    break;
                case MessageType.Ping:
                    HandlePing(connection, inboundPacket.Sequence, inboundPacket.Payload, token);
                    break;
                default:
                    _ = TrySendErrorAsync(connection, 400, "Unsupported message type", token);
                    break;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[packet-error] connection={connection.ConnectionId} type={inboundPacket.MessageType} error={exception.Message}");
            _ = TrySendErrorAsync(connection, 500, "Malformed payload", token);
        }
    }

    private void HandleHello(ClientConnection connection, uint sequence, byte[] payload, CancellationToken token)
    {
        if (connection.PlayerId.HasValue)
        {
            return;
        }

        var hello = ProtocolCodec.DecodeHello(payload);
        var nickname = NormalizeNickname(hello.Nickname);
        var kind = SanitizePlayerKind(hello.Kind);
        var reconnectToken = NormalizeReconnectToken(hello.ReconnectToken);

        if (TryReconnectExistingSession(connection, sequence, reconnectToken, token))
        {
            return;
        }

        if (_world.PlayerCount >= _config.MaxPlayers)
        {
            _ = TrySendErrorAsync(connection, 1010, "Server full", token);
            connection.Disconnect();
            return;
        }

        var playerId = _world.AddPlayer(nickname, kind);
        if (playerId <= 0)
        {
            _ = TrySendErrorAsync(connection, 1010, "Server full", token);
            connection.Disconnect();
            return;
        }

        reconnectToken = AllocateReconnectToken();
        _sessionsByToken[reconnectToken] = new ReconnectSession(
            playerId: playerId,
            activeConnectionId: connection.ConnectionId,
            parkedState: null,
            expiresAtUtc: DateTimeOffset.MaxValue);
        _tokenByPlayerId[playerId] = reconnectToken;

        connection.BindPlayer(playerId);
        _ = connection.SendAsync(
            MessageType.Welcome,
            sequence,
            writer => ProtocolCodec.WriteWelcomePayload(writer, playerId, _config, reconnectToken),
            token);

        Console.WriteLine($"[join] player={playerId} kind={kind} nickname={nickname} connection={connection.ConnectionId}");
    }

    private void HandleInputFrame(ClientConnection connection, byte[] payload)
    {
        if (!connection.PlayerId.HasValue)
        {
            return;
        }

        var input = ProtocolCodec.DecodeInputFrame(payload);
        _world.ApplyInput(connection.PlayerId.Value, input);
    }

    private void HandleShopPurchase(ClientConnection connection, byte[] payload)
    {
        if (!connection.PlayerId.HasValue)
        {
            return;
        }

        var request = ProtocolCodec.DecodeShopPurchaseRequest(payload);
        _world.QueueShopPurchase(connection.PlayerId.Value, request.ItemId);
    }

    private void HandlePing(ClientConnection connection, uint sequence, byte[] payload, CancellationToken token)
    {
        var ping = ProtocolCodec.DecodePing(payload);
        var serverMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _ = connection.SendAsync(
            MessageType.Pong,
            sequence,
            writer => ProtocolCodec.WritePongPayload(
                writer,
                ping.ClientUnixMs,
                serverMs,
                ProtocolConstants.ServerBuildVersion),
            token);
    }

    private async Task BroadcastSnapshotAndEventsAsync(CancellationToken token)
    {
        var snapshot = _world.CaptureSnapshot();
        var events = _world.DrainPendingEvents();

        var sendTasks = new List<Task>(_connections.Count * (events.Count > 0 ? 2 : 1));

        foreach (var connection in _connections.Values)
        {
            if (!connection.PlayerId.HasValue)
            {
                continue;
            }

            var playerId = connection.PlayerId.Value;
            var lastInputSeq = snapshot.Players
                .Where(player => player.PlayerId == playerId)
                .Select(player => player.LastInputSeq)
                .FirstOrDefault();

            var snapshotSequence = NextServerSequence();
            sendTasks.Add(connection.SendAsync(
                MessageType.Snapshot,
                snapshotSequence,
                writer => ProtocolCodec.WriteSnapshotPayload(writer, snapshot, lastInputSeq),
                token));

            if (events.Count == 0)
            {
                continue;
            }

            var eventSequence = NextServerSequence();
            sendTasks.Add(connection.SendAsync(
                MessageType.EventBatch,
                eventSequence,
                writer => ProtocolCodec.WriteEventBatchPayload(writer, snapshot.ServerTick, events),
                token));
        }

        if (sendTasks.Count == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(sendTasks).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[send-error] {exception.Message}");
        }
    }

    private void HandleDisconnection(ClientConnection connection)
    {
        if (!_connections.TryRemove(connection.ConnectionId, out _))
        {
            return;
        }

        var playerId = connection.PlayerId;
        if (playerId.HasValue)
        {
            var wasHandledBySession = false;
            if (_tokenByPlayerId.TryGetValue(playerId.Value, out var reconnectToken) &&
                _sessionsByToken.TryGetValue(reconnectToken, out var reconnectSession))
            {
                wasHandledBySession = true;

                if (reconnectSession.ActiveConnectionId.HasValue &&
                    reconnectSession.ActiveConnectionId.Value != connection.ConnectionId)
                {
                    Console.WriteLine(
                        $"[disconnect-stale] player={playerId.Value} connection={connection.ConnectionId} activeConnection={reconnectSession.ActiveConnectionId.Value}");
                }
                else if (_world.TryDetachPlayerForReconnect(playerId.Value, out var parkedState))
                {
                    reconnectSession.ActiveConnectionId = null;
                    reconnectSession.ParkedState = parkedState;
                    reconnectSession.ExpiresAtUtc = DateTimeOffset.UtcNow.Add(_reconnectGracePeriod);
                    Console.WriteLine(
                        $"[leave] player={playerId.Value} connection={connection.ConnectionId} reconnectUntil={reconnectSession.ExpiresAtUtc:O}");
                }
                else
                {
                    reconnectSession.ActiveConnectionId = null;
                    reconnectSession.ParkedState = null;
                    reconnectSession.ExpiresAtUtc = DateTimeOffset.UtcNow;
                    RemoveReconnectSession(reconnectToken, playerId.Value);
                    Console.WriteLine($"[leave] player={playerId.Value} connection={connection.ConnectionId}");
                }
            }

            if (!wasHandledBySession)
            {
                _world.RemovePlayer(playerId.Value);
                Console.WriteLine($"[leave] player={playerId.Value} connection={connection.ConnectionId}");
            }
        }
        else
        {
            Console.WriteLine($"[disconnect] connection={connection.ConnectionId}");
        }

        _ = connection.DisposeAsync();
    }

    private bool TryReconnectExistingSession(ClientConnection connection, uint sequence, string reconnectToken, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(reconnectToken))
        {
            return false;
        }

        if (!_sessionsByToken.TryGetValue(reconnectToken, out var reconnectSession))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (!reconnectSession.ActiveConnectionId.HasValue &&
            (!reconnectSession.ParkedState.HasValue || reconnectSession.ExpiresAtUtc <= now))
        {
            RemoveReconnectSession(reconnectToken, reconnectSession.PlayerId);
            return false;
        }

        var tookOverConnectionId = -1;
        if (reconnectSession.ActiveConnectionId.HasValue &&
            reconnectSession.ActiveConnectionId.Value != connection.ConnectionId)
        {
            tookOverConnectionId = reconnectSession.ActiveConnectionId.Value;
            if (_connections.TryGetValue(tookOverConnectionId, out var previousConnection))
            {
                previousConnection.Disconnect();
            }
        }
        else if (!reconnectSession.ActiveConnectionId.HasValue)
        {
            if (_world.PlayerCount >= _config.MaxPlayers)
            {
                _ = TrySendErrorAsync(connection, 1010, "Server full", token);
                connection.Disconnect();
                return true;
            }

            if (!reconnectSession.ParkedState.HasValue ||
                !_world.TryRestorePlayerFromReconnect(reconnectSession.ParkedState.Value))
            {
                _ = TrySendErrorAsync(connection, 1010, "Reconnect failed", token);
                connection.Disconnect();
                return true;
            }

            reconnectSession.ParkedState = null;
        }

        reconnectSession.ActiveConnectionId = connection.ConnectionId;
        reconnectSession.ExpiresAtUtc = DateTimeOffset.MaxValue;
        connection.BindPlayer(reconnectSession.PlayerId);
        _tokenByPlayerId[reconnectSession.PlayerId] = reconnectToken;

        _ = connection.SendAsync(
            MessageType.Welcome,
            sequence,
            writer => ProtocolCodec.WriteWelcomePayload(writer, reconnectSession.PlayerId, _config, reconnectToken),
            token);

        if (tookOverConnectionId > 0)
        {
            Console.WriteLine(
                $"[rejoin] player={reconnectSession.PlayerId} connection={connection.ConnectionId} takeoverConnection={tookOverConnectionId}");
        }
        else
        {
            Console.WriteLine($"[rejoin] player={reconnectSession.PlayerId} connection={connection.ConnectionId}");
        }

        return true;
    }

    private void PruneExpiredReconnectSessions()
    {
        var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowUnixMs < _nextReconnectSessionPruneUnixMs)
        {
            return;
        }

        _nextReconnectSessionPruneUnixMs = nowUnixMs + 1000;
        if (_sessionsByToken.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var expiredTokens = new List<string>();
        foreach (var pair in _sessionsByToken)
        {
            var reconnectSession = pair.Value;
            if (reconnectSession.ActiveConnectionId.HasValue)
            {
                continue;
            }

            if (reconnectSession.ExpiresAtUtc > now)
            {
                continue;
            }

            expiredTokens.Add(pair.Key);
        }

        for (var i = 0; i < expiredTokens.Count; i++)
        {
            var reconnectToken = expiredTokens[i];
            if (!_sessionsByToken.TryGetValue(reconnectToken, out var reconnectSession))
            {
                continue;
            }

            RemoveReconnectSession(reconnectToken, reconnectSession.PlayerId);
            Console.WriteLine($"[reconnect-expired] player={reconnectSession.PlayerId}");
        }
    }

    private void RemoveReconnectSession(string reconnectToken, int playerId)
    {
        _sessionsByToken.Remove(reconnectToken);
        if (_tokenByPlayerId.TryGetValue(playerId, out var mappedToken) &&
            string.Equals(mappedToken, reconnectToken, StringComparison.Ordinal))
        {
            _tokenByPlayerId.Remove(playerId);
        }
    }

    private string AllocateReconnectToken()
    {
        string reconnectToken;
        do
        {
            reconnectToken = CreateReconnectToken();
        } while (_sessionsByToken.ContainsKey(reconnectToken));

        return reconnectToken;
    }

    private static string NormalizeReconnectToken(string reconnectToken)
    {
        if (string.IsNullOrWhiteSpace(reconnectToken))
        {
            return string.Empty;
        }

        reconnectToken = reconnectToken.Trim();
        return reconnectToken.Length <= 96
            ? reconnectToken
            : reconnectToken[..96];
    }

    private static string CreateReconnectToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string NormalizeNickname(string value)
    {
        value = value.Trim();
        if (value.Length == 0)
        {
            return "Guest";
        }

        if (value.Length > 16)
        {
            value = value[..16];
        }

        return value;
    }

    private static PlayerKind SanitizePlayerKind(PlayerKind kind)
    {
        return kind == PlayerKind.Bot
            ? PlayerKind.Bot
            : PlayerKind.Human;
    }

    private uint NextServerSequence()
    {
        return unchecked((uint)Interlocked.Increment(ref _serverSequence));
    }

    private async Task TrySendErrorAsync(ClientConnection connection, ushort errorCode, string message, CancellationToken token)
    {
        try
        {
            await connection.SendAsync(
                MessageType.Error,
                NextServerSequence(),
                writer => ProtocolCodec.WriteErrorPayload(writer, errorCode, message),
                token).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void OnInboundPacket(ClientConnection connection, InboundPacket packet)
    {
        _inbound.Enqueue(new InboundEnvelope(InboundKind.Packet, connection, packet));
    }

    private void OnConnectionClosed(ClientConnection connection)
    {
        _inbound.Enqueue(new InboundEnvelope(InboundKind.Disconnected, connection, null));
    }

    private sealed class ReconnectSession
    {
        public ReconnectSession(
            int playerId,
            int? activeConnectionId,
            ReconnectPlayerState? parkedState,
            DateTimeOffset expiresAtUtc)
        {
            PlayerId = playerId;
            ActiveConnectionId = activeConnectionId;
            ParkedState = parkedState;
            ExpiresAtUtc = expiresAtUtc;
        }

        public int PlayerId { get; }
        public int? ActiveConnectionId { get; set; }
        public ReconnectPlayerState? ParkedState { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }

    private enum InboundKind : byte
    {
        Packet = 1,
        Disconnected = 2
    }

    private readonly record struct InboundEnvelope(
        InboundKind Kind,
        ClientConnection Connection,
        InboundPacket? Packet);
}
