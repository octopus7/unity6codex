using System.Text.Json.Serialization;
using TopdownShooter.Server.Domain;

namespace TopdownShooter.Server.WorldMap;

public sealed class WorldMapConfig
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("mapId")]
    public string MapId { get; init; } = "main";

    [JsonPropertyName("battleBounds")]
    public MapBounds BattleBounds { get; init; } = new(-20f, -20f, 20f, 20f);

    [JsonPropertyName("shopZone")]
    public MapBounds ShopZone { get; init; } = new(-6f, 22f, 6f, 34f);

    [JsonPropertyName("obstacles")]
    public List<MapObstacle> Obstacles { get; init; } = [];

    [JsonPropertyName("playerSpawns")]
    public List<MapPoint> PlayerSpawns { get; init; } = [];

    [JsonPropertyName("coinSpawners")]
    public List<MapCoinSpawner> CoinSpawners { get; init; } = [];

    [JsonPropertyName("portals")]
    public List<MapPortal> Portals { get; init; } = [];

    // TODO(gem-branch): Extend schema with gem spawn definitions.
}

public readonly record struct MapBounds(
    [property: JsonPropertyName("minX")] float MinX,
    [property: JsonPropertyName("minY")] float MinY,
    [property: JsonPropertyName("maxX")] float MaxX,
    [property: JsonPropertyName("maxY")] float MaxY)
{
    public bool Contains(MapPoint point)
    {
        return point.X >= MinX && point.X <= MaxX &&
               point.Y >= MinY && point.Y <= MaxY;
    }

    public bool Contains(Vector2f point)
    {
        return point.X >= MinX && point.X <= MaxX &&
               point.Y >= MinY && point.Y <= MaxY;
    }
}

public readonly record struct MapPoint(
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y)
{
    public Vector2f ToVector2f()
    {
        return new Vector2f(X, Y);
    }
}

public sealed class MapObstacle
{
    [JsonPropertyName("id")]
    public string ObstacleId { get; init; } = string.Empty;

    [JsonPropertyName("minX")]
    public float MinX { get; init; }

    [JsonPropertyName("minY")]
    public float MinY { get; init; }

    [JsonPropertyName("maxX")]
    public float MaxX { get; init; }

    [JsonPropertyName("maxY")]
    public float MaxY { get; init; }

    public MapBounds ToBounds()
    {
        return new MapBounds(MinX, MinY, MaxX, MaxY);
    }
}

public sealed class MapCoinSpawner
{
    [JsonPropertyName("id")]
    public int SpawnerId { get; init; }

    [JsonPropertyName("position")]
    public MapPoint Position { get; init; }

    [JsonPropertyName("intervalTicks")]
    public int IntervalTicks { get; init; } = 150;

    [JsonPropertyName("spawnAmount")]
    public int SpawnAmount { get; init; } = 1;
}

public sealed class MapPortal
{
    [JsonPropertyName("id")]
    public int PortalId { get; init; }

    [JsonPropertyName("type")]
    public PortalType PortalType { get; init; } = PortalType.Entry;

    [JsonPropertyName("position")]
    public MapPoint Position { get; init; }

    [JsonPropertyName("radius")]
    public float Radius { get; init; } = 1.2f;

    [JsonPropertyName("target")]
    public MapPoint Target { get; init; }
}
