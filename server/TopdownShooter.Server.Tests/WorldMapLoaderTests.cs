using TopdownShooter.Server.Domain;
using TopdownShooter.Server.WorldMap;

namespace TopdownShooter.Server.Tests;

public sealed class WorldMapLoaderTests
{
    [Fact]
    public void TryLoad_ReturnsFalse_WhenFileIsMissing()
    {
        var loaded = WorldMapLoader.TryLoad(
            "Content/Maps/does-not-exist.json",
            AppContext.BaseDirectory,
            out _,
            out var errors);

        Assert.False(loaded);
        Assert.Contains(errors, error => error.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryLoad_ReturnsFalse_WhenPortalTargetIsInvalid()
    {
        var mapFilePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(mapFilePath, """
                {
                  "schemaVersion": 1,
                  "mapId": "invalid",
                  "battleBounds": { "minX": -10, "minY": -10, "maxX": 10, "maxY": 10 },
                  "shopZone": { "minX": -2, "minY": 6, "maxX": 2, "maxY": 10 },
                  "obstacles": [],
                  "playerSpawns": [
                    { "x": 0, "y": 0 }
                  ],
                  "coinSpawners": [
                    {
                      "id": 1,
                      "position": { "x": 0, "y": -5 },
                      "intervalTicks": 150,
                      "spawnAmount": 1
                    }
                  ],
                  "portals": [
                    {
                      "id": 1,
                      "type": "Entry",
                      "position": { "x": 0, "y": 0 },
                      "radius": 1.2,
                      "target": { "x": 0, "y": 3 }
                    },
                    {
                      "id": 2,
                      "type": "Exit",
                      "position": { "x": 0, "y": 8 },
                      "radius": 1.2,
                      "target": { "x": 0, "y": 0 }
                    }
                  ]
                }
                """);

            var loaded = WorldMapLoader.TryLoad(mapFilePath, AppContext.BaseDirectory, out _, out var errors);

            Assert.False(loaded);
            Assert.Contains(errors, error => error.Contains("target for Entry", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(mapFilePath);
        }
    }

    [Fact]
    public void GameWorld_UsesPortalTargetsFromMapConfig()
    {
        var mapConfig = CreateValidMap(
            coinIntervalTicks: 150,
            coinSpawnAmount: 1,
            entryTarget: new MapPoint(0f, 8f));
        var world = new GameWorld(maxPlayers: 2, maxWorldCoins: 100, mapConfig, randomSeed: 3);
        var playerId = world.AddPlayer("Tester", PlayerKind.Human);

        world.Step();

        var player = world.CaptureSnapshot().Players.Single(p => p.PlayerId == playerId);
        Assert.True(player.InShopZone);
        Assert.Equal(0f, player.Position.X, 3);
        Assert.Equal(8f, player.Position.Y, 3);
    }

    [Fact]
    public void GameWorld_UsesCoinSpawnerSettingsFromMapConfig()
    {
        var mapConfig = CreateValidMap(
            coinIntervalTicks: 1,
            coinSpawnAmount: 3,
            entryTarget: new MapPoint(0f, 8f));
        var world = new GameWorld(maxPlayers: 2, maxWorldCoins: 100, mapConfig, randomSeed: 9);

        world.Step();

        var coins = world.CaptureSnapshot().CoinStacks;
        Assert.Single(coins);
        Assert.Equal(3, coins[0].Amount);
        Assert.True(coins[0].IsDispenser);
    }

    private static WorldMapConfig CreateValidMap(int coinIntervalTicks, int coinSpawnAmount, MapPoint entryTarget)
    {
        return new WorldMapConfig
        {
            SchemaVersion = 1,
            MapId = "test",
            BattleBounds = new MapBounds(-10f, -10f, 10f, 10f),
            ShopZone = new MapBounds(-2f, 6f, 2f, 10f),
            Obstacles = [],
            PlayerSpawns =
            [
                new MapPoint(0f, 0f)
            ],
            CoinSpawners =
            [
                new MapCoinSpawner
                {
                    SpawnerId = 1,
                    Position = new MapPoint(0f, -5f),
                    IntervalTicks = coinIntervalTicks,
                    SpawnAmount = coinSpawnAmount
                }
            ],
            Portals =
            [
                new MapPortal
                {
                    PortalId = 1,
                    PortalType = PortalType.Entry,
                    Position = new MapPoint(0f, 0f),
                    Radius = 1.2f,
                    Target = entryTarget
                },
                new MapPortal
                {
                    PortalId = 2,
                    PortalType = PortalType.Exit,
                    Position = new MapPoint(0f, 8f),
                    Radius = 1.2f,
                    Target = new MapPoint(0f, 0f)
                }
            ]
        };
    }
}
