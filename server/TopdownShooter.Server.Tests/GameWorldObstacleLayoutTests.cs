using TopdownShooter.Server.Domain;
using TopdownShooter.Server.Networking;

namespace TopdownShooter.Server.Tests;

public sealed class GameWorldObstacleLayoutTests
{
    [Fact]
    public void CoinDispenserAtCenter_IsReachableThroughCenterLane()
    {
        var world = new GameWorld(maxPlayers: 2, maxWorldCoins: 100, randomSeed: 7);
        var playerId = AddPlayerAt(world, new Vector2f(0f, -2.4f));

        AdvanceTicks(world, GameRules.CoinDispenserIntervalTicks);
        world.ApplyInput(playerId, new InputFrameMessage(1, 0, 0, short.MaxValue, 0f, 1f, 0));
        AdvanceTicks(world, 12);

        var player = GetPlayerSnapshot(world, playerId);
        Assert.True(player.CarriedCoins >= 1);
    }

    [Fact]
    public void CenterCornerObstacle_StillBlocksDiagonalShortcut()
    {
        var world = new GameWorld(maxPlayers: 2, maxWorldCoins: 100, randomSeed: 11);
        var playerId = AddPlayerAt(world, new Vector2f(-2.3f, -2.3f));

        world.ApplyInput(playerId, new InputFrameMessage(1, 0, short.MaxValue, short.MaxValue, 1f, 1f, 0));
        AdvanceTicks(world, 20);

        var player = GetPlayerSnapshot(world, playerId);
        Assert.True(player.Position.X <= -1.98f && player.Position.Y <= -1.98f);
    }

    private static int AddPlayerAt(GameWorld world, Vector2f position)
    {
        var playerId = world.AddPlayer("Tester", PlayerKind.Human);
        Assert.True(playerId > 0);

        Assert.True(world.TryDetachPlayerForReconnect(playerId, out var reconnect));
        var relocated = reconnect with
        {
            Position = position,
            InShopZone = false,
            IsAlive = true,
            Hp = GameRules.MaxHp,
            MoveX = 0,
            MoveY = 0,
            FireHeld = false
        };

        Assert.True(world.TryRestorePlayerFromReconnect(relocated));
        return playerId;
    }

    private static PlayerSnapshotState GetPlayerSnapshot(GameWorld world, int playerId)
    {
        return world.CaptureSnapshot().Players.Single(player => player.PlayerId == playerId);
    }

    private static void AdvanceTicks(GameWorld world, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Step();
        }
    }
}
