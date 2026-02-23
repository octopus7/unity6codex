using System.Globalization;

namespace TopdownShooter.BotClient;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Any(arg => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                            arg.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            PrintUsage();
            return;
        }

        BotClientOptions options;
        try
        {
            options = BotClientOptions.Parse(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Argument error: {exception.Message}");
            PrintUsage();
            return;
        }

        ConfigureThreadPool(options.MaxWorkerThreads);

        Console.WriteLine(
            $"Starting bot host: bots={options.BotCount} host={options.Host}:{options.Port} " +
            $"moveIntervalMs={options.MoveIntervalMs} workerThreads={options.MaxWorkerThreads}");
        Console.WriteLine("Press Ctrl+C to stop.");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        var tasks = new List<Task>(options.BotCount);
        for (var i = 1; i <= options.BotCount; i++)
        {
            var bot = new BotSession(i, options);
            tasks.Add(Task.Run(() => bot.RunAsync(cts.Token), cts.Token));

            if (i < options.BotCount && options.ConnectStaggerMs > 0)
            {
                try
                {
                    await Task.Delay(options.ConnectStaggerMs, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void ConfigureThreadPool(int requestedWorkerThreads)
    {
        ThreadPool.GetMaxThreads(out var currentMaxWorkers, out var currentMaxIo);
        ThreadPool.GetMinThreads(out _, out var currentMinIo);

        var workerLimit = Math.Clamp(requestedWorkerThreads, 1, currentMaxWorkers);

        _ = ThreadPool.SetMaxThreads(workerLimit, currentMaxIo);
        _ = ThreadPool.SetMinThreads(workerLimit, currentMinIo);

        Console.WriteLine($"ThreadPool workers set to min/max={workerLimit}.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("TopdownShooter.BotClient (.NET 10)");
        Console.WriteLine("Options:");
        Console.WriteLine("  --host=127.0.0.1");
        Console.WriteLine("  --port=7777");
        Console.WriteLine("  --bots=4");
        Console.WriteLine("  --threads=8");
        Console.WriteLine("  --move-interval-ms=120");
        Console.WriteLine("  --min-direction-ms=900");
        Console.WriteLine("  --max-direction-ms=2600");
        Console.WriteLine("  --reconnect-delay-ms=1500");
        Console.WriteLine("  --connect-stagger-ms=100");
        Console.WriteLine("  --nickname-prefix=Bot");
    }
}

internal readonly record struct BotClientOptions(
    string Host,
    int Port,
    int BotCount,
    int MaxWorkerThreads,
    int MoveIntervalMs,
    int MinDirectionMs,
    int MaxDirectionMs,
    int ReconnectDelayMs,
    int ConnectStaggerMs,
    string NicknamePrefix)
{
    public static BotClientOptions Parse(string[] args)
    {
        var host = "127.0.0.1";
        var port = 7777;
        var botCount = 4;
        var maxWorkerThreads = Math.Max(2, Environment.ProcessorCount);
        var moveIntervalMs = 120;
        var minDirectionMs = 900;
        var maxDirectionMs = 2600;
        var reconnectDelayMs = 1500;
        var connectStaggerMs = 100;
        var nicknamePrefix = "Bot";

        foreach (var arg in args)
        {
            var separatorIndex = arg.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == arg.Length - 1)
            {
                throw new ArgumentException($"Invalid argument format: {arg}");
            }

            var key = arg[..separatorIndex];
            var value = arg[(separatorIndex + 1)..];

            switch (key.ToLowerInvariant())
            {
                case "--host":
                    host = value.Trim();
                    break;
                case "--port":
                    port = ParseInt(value, 1, 65535, key);
                    break;
                case "--bots":
                    botCount = ParseInt(value, 1, 512, key);
                    break;
                case "--threads":
                    maxWorkerThreads = ParseInt(value, 1, 512, key);
                    break;
                case "--move-interval-ms":
                    moveIntervalMs = ParseInt(value, 20, 5000, key);
                    break;
                case "--min-direction-ms":
                    minDirectionMs = ParseInt(value, 100, 10000, key);
                    break;
                case "--max-direction-ms":
                    maxDirectionMs = ParseInt(value, 100, 10000, key);
                    break;
                case "--reconnect-delay-ms":
                    reconnectDelayMs = ParseInt(value, 100, 30000, key);
                    break;
                case "--connect-stagger-ms":
                    connectStaggerMs = ParseInt(value, 0, 10000, key);
                    break;
                case "--nickname-prefix":
                    nicknamePrefix = value.Trim();
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {key}");
            }
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(nicknamePrefix))
        {
            nicknamePrefix = "Bot";
        }

        if (minDirectionMs > maxDirectionMs)
        {
            (minDirectionMs, maxDirectionMs) = (maxDirectionMs, minDirectionMs);
        }

        return new BotClientOptions(
            Host: host,
            Port: port,
            BotCount: botCount,
            MaxWorkerThreads: maxWorkerThreads,
            MoveIntervalMs: moveIntervalMs,
            MinDirectionMs: minDirectionMs,
            MaxDirectionMs: maxDirectionMs,
            ReconnectDelayMs: reconnectDelayMs,
            ConnectStaggerMs: connectStaggerMs,
            NicknamePrefix: nicknamePrefix);
    }

    private static int ParseInt(string value, int min, int max, string key)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"Invalid integer for {key}: {value}");
        }

        return Math.Clamp(parsed, min, max);
    }
}
