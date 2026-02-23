using System.Net.Sockets;
using TopdownShooter.Server.Domain;

namespace TopdownShooter.Server.Networking;

public sealed class ClientConnection : IAsyncDisposable
{
    private const long MaxPendingSendBytes = 1_048_576;

    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly Action<ClientConnection, InboundPacket> _onPacket;
    private readonly Action<ClientConnection> _onDisconnected;
    private readonly CancellationTokenSource _localCts = new();

    private long _pendingSendBytes;
    private int _disconnectNotified;

    public ClientConnection(
        int connectionId,
        TcpClient tcpClient,
        Action<ClientConnection, InboundPacket> onPacket,
        Action<ClientConnection> onDisconnected)
    {
        ConnectionId = connectionId;
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
        _onPacket = onPacket;
        _onDisconnected = onDisconnected;
    }

    public int ConnectionId { get; }
    public int? PlayerId { get; private set; }

    public void BindPlayer(int playerId)
    {
        PlayerId = playerId;
    }

    public async Task RunReceiveLoopAsync(CancellationToken serverToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(serverToken, _localCts.Token);
        var token = linkedCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var header = await ReadExactlyAsync(_stream, ProtocolConstants.HeaderSize, token).ConfigureAwait(false);
                if (header is null)
                {
                    break;
                }

                if (!ProtocolCodec.TryParseHeader(header, out var payloadLength, out var messageType, out var protocolVersion, out var sequence))
                {
                    break;
                }

                if (protocolVersion != ProtocolConstants.ProtocolVersion)
                {
                    await SendAsync(
                        MessageType.Error,
                        sequence,
                        writer => ProtocolCodec.WriteErrorPayload(writer, 100, "Protocol version mismatch"),
                        token).ConfigureAwait(false);
                    break;
                }

                var payload = await ReadExactlyAsync(_stream, payloadLength, token).ConfigureAwait(false);
                if (payload is null)
                {
                    break;
                }

                _onPacket(this, new InboundPacket(messageType, sequence, payload));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            NotifyDisconnectedOnce();
        }
    }

    public async Task SendAsync(MessageType messageType, uint sequence, Action<BinaryWriter> writePayload, CancellationToken token)
    {
        var frame = ProtocolCodec.EncodeFrame(messageType, sequence, writePayload);
        var pendingBytes = Interlocked.Add(ref _pendingSendBytes, frame.Length);
        if (pendingBytes > MaxPendingSendBytes)
        {
            Interlocked.Add(ref _pendingSendBytes, -frame.Length);
            Disconnect();
            throw new IOException("Pending send buffer exceeded threshold");
        }

        await _sendGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(frame.AsMemory(), token).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Add(ref _pendingSendBytes, -frame.Length);
            _sendGate.Release();
        }
    }

    public void Disconnect()
    {
        try
        {
            _localCts.Cancel();
        }
        catch
        {
        }

        try
        {
            _tcpClient.Client.Shutdown(SocketShutdown.Both);
        }
        catch
        {
        }

        try
        {
            _tcpClient.Close();
        }
        catch
        {
        }

        NotifyDisconnectedOnce();
    }

    public async ValueTask DisposeAsync()
    {
        Disconnect();
        _sendGate.Dispose();
        _localCts.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private void NotifyDisconnectedOnce()
    {
        if (Interlocked.Exchange(ref _disconnectNotified, 1) == 1)
        {
            return;
        }

        _onDisconnected(this);
    }

    private static async Task<byte[]?> ReadExactlyAsync(NetworkStream stream, int length, CancellationToken token)
    {
        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), token).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            offset += read;
        }

        return buffer;
    }
}
