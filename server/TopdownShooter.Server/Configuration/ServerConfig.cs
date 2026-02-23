using System.Text.Json;

namespace TopdownShooter.Server.Configuration;

public sealed class ServerConfig
{
    public string ListenIp { get; init; } = "0.0.0.0";
    public int ListenPort { get; init; } = 7777;
    public int MaxPlayers { get; init; } = 8;
    public int TickRateHz { get; init; } = 30;
    public int SnapshotRateHz { get; init; } = 20;
    public int MaxWorldCoins { get; init; } = 5000;
    public int ReconnectGraceSeconds { get; init; } = 60;

    public static ServerConfig Load(string baseDirectory, string[] args)
    {
        var fromFile = LoadFromFile(Path.Combine(baseDirectory, "appsettings.json"));

        var listenIp = fromFile.ListenIp;
        var listenPort = fromFile.ListenPort;
        var maxPlayers = fromFile.MaxPlayers;
        var tickRate = fromFile.TickRateHz;
        var snapshotRate = fromFile.SnapshotRateHz;
        var maxWorldCoins = fromFile.MaxWorldCoins;
        var reconnectGraceSeconds = fromFile.ReconnectGraceSeconds;

        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i].ToLowerInvariant();
            if (i + 1 >= args.Length)
            {
                break;
            }

            var value = args[i + 1];
            switch (key)
            {
                case "--ip":
                    listenIp = value;
                    i++;
                    break;
                case "--port":
                    if (int.TryParse(value, out var parsedPort))
                    {
                        listenPort = parsedPort;
                    }

                    i++;
                    break;
                case "--max-players":
                    if (int.TryParse(value, out var parsedPlayers))
                    {
                        maxPlayers = parsedPlayers;
                    }

                    i++;
                    break;
                case "--tick-rate":
                    if (int.TryParse(value, out var parsedTickRate))
                    {
                        tickRate = parsedTickRate;
                    }

                    i++;
                    break;
                case "--snapshot-rate":
                    if (int.TryParse(value, out var parsedSnapshotRate))
                    {
                        snapshotRate = parsedSnapshotRate;
                    }

                    i++;
                    break;
                case "--max-world-coins":
                    if (int.TryParse(value, out var parsedMaxCoins))
                    {
                        maxWorldCoins = parsedMaxCoins;
                    }

                    i++;
                    break;
                case "--reconnect-grace-seconds":
                    if (int.TryParse(value, out var parsedReconnectGraceSeconds))
                    {
                        reconnectGraceSeconds = parsedReconnectGraceSeconds;
                    }

                    i++;
                    break;
            }
        }

        return new ServerConfig
        {
            ListenIp = string.IsNullOrWhiteSpace(listenIp) ? "0.0.0.0" : listenIp,
            ListenPort = Math.Clamp(listenPort, 1024, 65535),
            MaxPlayers = Math.Clamp(maxPlayers, 2, 64),
            TickRateHz = Math.Clamp(tickRate, 10, 120),
            SnapshotRateHz = Math.Clamp(snapshotRate, 5, 60),
            MaxWorldCoins = Math.Clamp(maxWorldCoins, 500, 100_000),
            ReconnectGraceSeconds = Math.Clamp(reconnectGraceSeconds, 5, 600)
        };
    }

    private static ServerConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return new ServerConfig();
        }

        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var config = new ServerConfig
        {
            ListenIp = ReadString(root, "listenIp", "0.0.0.0"),
            ListenPort = ReadInt(root, "listenPort", 7777),
            MaxPlayers = ReadInt(root, "maxPlayers", 8),
            TickRateHz = ReadInt(root, "tickRateHz", 30),
            SnapshotRateHz = ReadInt(root, "snapshotRateHz", 20),
            MaxWorldCoins = ReadInt(root, "maxWorldCoins", 5000),
            ReconnectGraceSeconds = ReadInt(root, "reconnectGraceSeconds", 60)
        };

        return config;
    }

    private static string ReadString(JsonElement root, string key, string fallback)
    {
        return root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static int ReadInt(JsonElement root, string key, int fallback)
    {
        return root.TryGetProperty(key, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : fallback;
    }
}
