using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CodexSix.TopdownShooter.Net
{
    public sealed class TcpGameTransport : MonoBehaviour, IGameTransport
    {
        private readonly ConcurrentQueue<Action> _mainThreadActions = new();
        private readonly SemaphoreSlim _sendGate = new(1, 1);

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _sessionCts;
        private Task _receiveLoopTask;
        private int _sequence;
        private float _pingTimer;
        private bool _applicationQuitting;
        private ConnectionState _currentState = ConnectionState.Disconnected;

        public ConnectionState CurrentState => _currentState;

        public event Action<ServerWelcome> WelcomeReceived;
        public event Action<ServerSnapshot> SnapshotReceived;
        public event Action<ServerEventBatch> EventReceived;
        public event Action<long> PongReceived;
        public event Action<ServerError> ErrorReceived;
        public event Action<ConnectionState> ConnectionStateChanged;

        public async Task ConnectAsync(string host, int port, string nickname, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host is required.", nameof(host));
            }

            Disconnect();
            UpdateState(ConnectionState.Connecting);

            _tcpClient = new TcpClient();
            try
            {
                await _tcpClient.ConnectAsync(host, port);
                ct.ThrowIfCancellationRequested();

                _tcpClient.NoDelay = true;
                _stream = _tcpClient.GetStream();
                _sessionCts = new CancellationTokenSource();
                _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_sessionCts.Token));

                UpdateState(ConnectionState.Connected);

                var frame = NetProtocolCodec.EncodeHello(NextSequence(), nickname);
                await SendFrameAsync(frame, ct);
            }
            catch
            {
                DisconnectInternal(notify: true);
                throw;
            }
        }

        public void Disconnect()
        {
            DisconnectInternal(notify: true);
        }

        public void SendInput(in ClientInputFrame input)
        {
            if (_currentState != ConnectionState.Connected || _stream == null)
            {
                return;
            }

            var frame = NetProtocolCodec.EncodeInput(NextSequence(), input);
            _ = SendFrameSafeAsync(frame);
        }

        public void SendShopPurchase(in ShopPurchaseRequest request)
        {
            if (_currentState != ConnectionState.Connected || _stream == null)
            {
                return;
            }

            var frame = NetProtocolCodec.EncodeShopPurchase(NextSequence(), request);
            _ = SendFrameSafeAsync(frame);
        }

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (_currentState != ConnectionState.Connected || _stream == null)
            {
                return;
            }

            _pingTimer += Time.unscaledDeltaTime;
            if (_pingTimer < 1.0f)
            {
                return;
            }

            _pingTimer = 0f;
            var frame = NetProtocolCodec.EncodePing(NextSequence(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _ = SendFrameSafeAsync(frame);
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var header = await ReadExactlyAsync(_stream, NetProtocolCodec.HeaderSize, token);
                    if (header == null)
                    {
                        break;
                    }

                    if (!NetProtocolCodec.TryParseHeader(header, out var payloadLength, out var messageType, out var version, out var sequence))
                    {
                        EnqueueError(new ServerError { ErrorCode = 300, Message = "Invalid packet header." });
                        break;
                    }

                    if (version != NetProtocolCodec.ProtocolVersion)
                    {
                        EnqueueError(new ServerError { ErrorCode = 301, Message = "Protocol version mismatch." });
                        break;
                    }

                    var payload = await ReadExactlyAsync(_stream, payloadLength, token);
                    if (payload == null)
                    {
                        break;
                    }

                    DispatchPayload(messageType, sequence, payload);
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
            catch (Exception exception)
            {
                EnqueueError(new ServerError
                {
                    ErrorCode = 302,
                    Message = "Receive loop failed: " + exception.Message
                });
            }
            finally
            {
                EnqueueMainThreadAction(() => DisconnectInternal(notify: true));
            }
        }

        private void DispatchPayload(MessageType messageType, uint sequence, byte[] payload)
        {
            _ = sequence;
            switch (messageType)
            {
                case MessageType.Welcome:
                {
                    var welcome = NetProtocolCodec.DecodeWelcome(payload);
                    EnqueueMainThreadAction(() => WelcomeReceived?.Invoke(welcome));
                    break;
                }
                case MessageType.Snapshot:
                {
                    var snapshot = NetProtocolCodec.DecodeSnapshot(payload);
                    EnqueueMainThreadAction(() => SnapshotReceived?.Invoke(snapshot));
                    break;
                }
                case MessageType.EventBatch:
                {
                    var batch = NetProtocolCodec.DecodeEventBatch(payload);
                    EnqueueMainThreadAction(() => EventReceived?.Invoke(batch));
                    break;
                }
                case MessageType.Pong:
                {
                    var rttMs = NetProtocolCodec.DecodePong(payload);
                    EnqueueMainThreadAction(() => PongReceived?.Invoke(rttMs));
                    break;
                }
                case MessageType.Error:
                {
                    var error = NetProtocolCodec.DecodeError(payload);
                    EnqueueError(error);
                    break;
                }
            }
        }

        private async Task SendFrameSafeAsync(byte[] frame)
        {
            if (_sessionCts == null)
            {
                return;
            }

            try
            {
                await SendFrameAsync(frame, _sessionCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                EnqueueError(new ServerError
                {
                    ErrorCode = 303,
                    Message = "Send failed: " + exception.Message
                });
                EnqueueMainThreadAction(() => DisconnectInternal(notify: true));
            }
        }

        private async Task SendFrameAsync(byte[] frame, CancellationToken token)
        {
            if (_stream == null)
            {
                throw new InvalidOperationException("Network stream is not available.");
            }

            await _sendGate.WaitAsync(token);
            try
            {
                await _stream.WriteAsync(frame, 0, frame.Length, token);
            }
            finally
            {
                _sendGate.Release();
            }
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

        private void EnqueueError(ServerError error)
        {
            EnqueueMainThreadAction(() => ErrorReceived?.Invoke(error));
        }

        private void EnqueueMainThreadAction(Action action)
        {
            _mainThreadActions.Enqueue(action);
        }

        private uint NextSequence()
        {
            return unchecked((uint)Interlocked.Increment(ref _sequence));
        }

        private void DisconnectInternal(bool notify)
        {
            if (_currentState == ConnectionState.Disconnected && _tcpClient == null && _stream == null)
            {
                return;
            }

            try
            {
                _sessionCts?.Cancel();
            }
            catch
            {
            }

            try
            {
                _stream?.Dispose();
            }
            catch
            {
            }

            try
            {
                _tcpClient?.Close();
            }
            catch
            {
            }

            _stream = null;
            _tcpClient = null;
            _sessionCts?.Dispose();
            _sessionCts = null;
            _receiveLoopTask = null;
            _pingTimer = 0f;

            if (notify)
            {
                UpdateState(ConnectionState.Disconnected);
            }
        }

        private void UpdateState(ConnectionState state)
        {
            if (_currentState == state)
            {
                return;
            }

            _currentState = state;
            ConnectionStateChanged?.Invoke(state);
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
            DisconnectInternal(notify: false);
        }

        private void OnDestroy()
        {
            if (!_applicationQuitting)
            {
                DisconnectInternal(notify: false);
            }

            _sendGate.Dispose();
        }
    }
}
