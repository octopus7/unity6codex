using TopdownShooter.Server.Configuration;
using TopdownShooter.Server.Runtime;

var config = ServerConfig.Load(AppContext.BaseDirectory, args);
using var cts = new CancellationTokenSource();
var server = new GameServer(config);

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

Console.WriteLine(
    $"Starting server on {config.ListenIp}:{config.ListenPort} " +
    $"(tick={config.TickRateHz}Hz snapshot={config.SnapshotRateHz}Hz maxPlayers={config.MaxPlayers} reconnectGrace={config.ReconnectGraceSeconds}s)");
Console.WriteLine("Commands: status | kick <playerId> | quit");

var runTask = server.RunAsync(cts.Token);

while (!cts.IsCancellationRequested)
{
    var line = Console.ReadLine();
    if (line is null)
    {
        cts.Cancel();
        break;
    }

    line = line.Trim();
    if (line.Length == 0)
    {
        continue;
    }

    if (line.Equals("status", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine(server.BuildStatusLine());
        continue;
    }

    if (line.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        cts.Cancel();
        break;
    }

    if (line.StartsWith("kick ", StringComparison.OrdinalIgnoreCase))
    {
        var token = line[5..].Trim();
        if (int.TryParse(token, out var playerId))
        {
            Console.WriteLine(server.KickPlayer(playerId)
                ? $"Kicked player {playerId}"
                : $"Player {playerId} not found");
        }
        else
        {
            Console.WriteLine("Usage: kick <playerId>");
        }

        continue;
    }

    Console.WriteLine("Unknown command. Use: status | kick <playerId> | quit");
}

try
{
    await runTask.ConfigureAwait(false);
}
catch (OperationCanceledException)
{
}
