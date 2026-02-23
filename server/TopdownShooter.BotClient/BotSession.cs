using System.Diagnostics;
using System.Net.Sockets;

namespace TopdownShooter.BotClient;

internal sealed class BotSession
{
    private readonly int _botIndex;
    private readonly BotClientOptions _options;
    private readonly string _nickname;
    private readonly Random _random;

    private int _lastKnownHumanCount = -1;
    private int _lastKnownBotCount = -1;

    public BotSession(int botIndex, BotClientOptions options)
    {
        _botIndex = botIndex;
        _options = options;
        _nickname = BuildNickname(options.NicknamePrefix, botIndex);
        _random = new Random(unchecked(Environment.TickCount * 397 ^ botIndex));
    }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await RunSingleConnectionAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[{_nickname}] reconnect in {_options.ReconnectDelayMs}ms ({exception.Message})");
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(_options.ReconnectDelayMs, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunSingleConnectionAsync(CancellationToken token)
    {
        using var tcpClient = new TcpClient
        {
            NoDelay = true
        };

        await tcpClient.ConnectAsync(_options.Host, _options.Port, token).ConfigureAwait(false);
        using var stream = tcpClient.GetStream();

        uint sequence = 1;
        var helloFrame = BotProtocol.EncodeHello(sequence, _nickname, BotProtocol.PlayerKind.Bot);
        await stream.WriteAsync(helloFrame.AsMemory(), token).ConfigureAwait(false);

        Console.WriteLine($"[{_nickname}] connected as bot.");

        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var receiveTask = ReceiveLoopAsync(stream, sessionCts.Token);
        var movementTask = MovementLoopAsync(stream, initialSequence: sequence, sessionCts.Token);

        await Task.WhenAny(receiveTask, movementTask).ConfigureAwait(false);
        sessionCts.Cancel();

        try
        {
            await Task.WhenAll(receiveTask, movementTask).ConfigureAwait(false);
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
            Console.WriteLine($"[{_nickname}] disconnected.");
        }
    }

    private async Task MovementLoopAsync(NetworkStream stream, uint initialSequence, CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        var sequence = initialSequence;
        uint inputSequence = 0;

        var moveVector = NextMoveVector();
        var aimX = moveVector.IsIdle ? 1f : moveVector.X;
        var aimY = moveVector.IsIdle ? 0f : moveVector.Y;
        var nextDirectionChangeAtMs = NextDirectionChangeAt(stopwatch.ElapsedMilliseconds);

        while (!token.IsCancellationRequested)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            if (elapsedMs >= nextDirectionChangeAtMs)
            {
                moveVector = NextMoveVector();
                nextDirectionChangeAtMs = NextDirectionChangeAt(elapsedMs);

                if (!moveVector.IsIdle)
                {
                    aimX = moveVector.X;
                    aimY = moveVector.Y;
                }
            }

            var input = new BotProtocol.InputFrame(
                InputSeq: ++inputSequence,
                ClientTick: unchecked((uint)elapsedMs),
                MoveX: ToAxisShort(moveVector.X),
                MoveY: ToAxisShort(moveVector.Y),
                AimX: aimX,
                AimY: aimY,
                Buttons: 0);

            sequence++;
            var frame = BotProtocol.EncodeInput(sequence, in input);
            await stream.WriteAsync(frame.AsMemory(), token).ConfigureAwait(false);
            await Task.Delay(_options.MoveIntervalMs, token).ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var header = await ReadExactlyAsync(stream, BotProtocol.HeaderSize, token).ConfigureAwait(false);
            if (header == null)
            {
                return;
            }

            if (!BotProtocol.TryParseHeader(header, out var payloadLength, out var messageType, out var version, out _))
            {
                throw new InvalidDataException("Invalid server header.");
            }

            if (version != BotProtocol.ProtocolVersion)
            {
                throw new InvalidDataException(
                    $"Protocol version mismatch. expected={BotProtocol.ProtocolVersion} actual={version}");
            }

            var payload = await ReadExactlyAsync(stream, payloadLength, token).ConfigureAwait(false);
            if (payload == null)
            {
                return;
            }

            switch (messageType)
            {
                case BotProtocol.MessageType.Welcome:
                {
                    var welcome = BotProtocol.DecodeWelcome(payload);
                    Console.WriteLine($"[{_nickname}] welcome player={welcome.PlayerId} maxPlayers={welcome.MaxPlayers}");
                    break;
                }
                case BotProtocol.MessageType.Snapshot:
                {
                    var summary = BotProtocol.DecodeSnapshotSummary(payload);
                    if (summary.HumanCount != _lastKnownHumanCount || summary.BotCount != _lastKnownBotCount)
                    {
                        _lastKnownHumanCount = summary.HumanCount;
                        _lastKnownBotCount = summary.BotCount;
                        Console.WriteLine($"[{_nickname}] tick={summary.ServerTick} humans={summary.HumanCount} bots={summary.BotCount}");
                    }

                    break;
                }
                case BotProtocol.MessageType.Error:
                {
                    var error = BotProtocol.DecodeError(payload);
                    Console.WriteLine($"[{_nickname}] server error {error.ErrorCode}: {error.Message}");
                    break;
                }
            }
        }
    }

    private long NextDirectionChangeAt(long fromMs)
    {
        var durationMs = _random.Next(_options.MinDirectionMs, _options.MaxDirectionMs + 1);
        return fromMs + durationMs;
    }

    private MoveVector NextMoveVector()
    {
        if (_random.NextDouble() < 0.2d)
        {
            return new MoveVector(0f, 0f, IsIdle: true);
        }

        var angle = _random.NextDouble() * (Math.PI * 2d);
        return new MoveVector((float)Math.Cos(angle), (float)Math.Sin(angle), IsIdle: false);
    }

    private static short ToAxisShort(float value)
    {
        var clamped = Math.Clamp(value, -1f, 1f);
        return (short)Math.Round(clamped * 32767f);
    }

    private static string BuildNickname(string prefix, int botIndex)
    {
        var value = $"{prefix}{botIndex:D3}";
        return value.Length <= 16 ? value : value[..16];
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

    private readonly record struct MoveVector(float X, float Y, bool IsIdle);
}
