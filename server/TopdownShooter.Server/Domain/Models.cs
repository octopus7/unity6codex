namespace TopdownShooter.Server.Domain;

public enum PlayerKind : byte
{
    Human = 0,
    Bot = 1
}

public enum PortalType : byte
{
    Entry = 1,
    Exit = 2
}

public sealed class PlayerState
{
    public int PlayerId { get; init; }
    public string Nickname { get; init; } = string.Empty;
    public PlayerKind Kind { get; init; } = PlayerKind.Human;

    public Vector2f Position { get; set; }
    public Vector2f AimDirection { get; set; } = new Vector2f(1f, 0f);

    public int Hp { get; set; } = GameRules.MaxHp;
    public bool IsAlive { get; set; } = true;
    public bool InShopZone { get; set; }

    public int CarriedCoins { get; set; }
    public int SpeedBuffStacks { get; set; }

    public short MoveX { get; set; }
    public short MoveY { get; set; }
    public bool FireHeld { get; set; }
    public uint LastInputSeq { get; set; }

    public uint RespawnAtTick { get; set; }
    public uint NextFireAllowedTick { get; set; }
}

public readonly record struct ReconnectPlayerState(
    int PlayerId,
    string Nickname,
    PlayerKind Kind,
    Vector2f Position,
    Vector2f AimDirection,
    int Hp,
    bool IsAlive,
    bool InShopZone,
    int CarriedCoins,
    int SpeedBuffStacks,
    short MoveX,
    short MoveY,
    bool FireHeld,
    uint LastInputSeq,
    uint RespawnAtTick,
    uint NextFireAllowedTick);

public sealed class ProjectileState
{
    public int ProjectileId { get; init; }
    public int OwnerPlayerId { get; init; }
    public Vector2f Position { get; set; }
    public Vector2f Direction { get; init; }
    public uint SpawnTick { get; init; }
}

public sealed class CoinStackState
{
    public int CoinStackId { get; init; }
    public Vector2f Position { get; init; }
    public int Amount { get; set; }
    public uint CreatedTick { get; init; }
}

public sealed class ItemDropState
{
    public int ItemDropId { get; init; }
    public int ItemId { get; init; }
    public Vector2f Position { get; init; }
    public int Quantity { get; init; }
    public uint CreatedTick { get; init; }
}

public sealed class PortalState
{
    public byte PortalId { get; init; }
    public PortalType PortalType { get; init; }
    public Vector2f Position { get; init; }
    public float Radius { get; init; }
}

public sealed class ShopZoneState
{
    public float MinX { get; init; }
    public float MinY { get; init; }
    public float MaxX { get; init; }
    public float MaxY { get; init; }
}

public readonly record struct PlayerSnapshotState(
    int PlayerId,
    Vector2f Position,
    Vector2f AimDirection,
    short Hp,
    int CarriedCoins,
    byte SpeedBuffStacks,
    PlayerKind Kind,
    bool IsAlive,
    bool InShopZone,
    uint LastInputSeq);

public readonly record struct ProjectileSnapshotState(
    int ProjectileId,
    int OwnerPlayerId,
    Vector2f Position,
    Vector2f Direction);

public readonly record struct CoinSnapshotState(
    int CoinStackId,
    Vector2f Position,
    int Amount,
    uint CreatedTick,
    bool IsDispenser);

public readonly record struct ItemDropSnapshotState(
    int ItemDropId,
    int ItemId,
    Vector2f Position,
    int Quantity,
    uint CreatedTick);

public readonly record struct PortalSnapshotState(
    byte PortalId,
    PortalType PortalType,
    Vector2f Position);

public sealed class WorldSnapshot
{
    public uint ServerTick { get; init; }
    public IReadOnlyList<PlayerSnapshotState> Players { get; init; } = Array.Empty<PlayerSnapshotState>();
    public IReadOnlyList<ProjectileSnapshotState> Projectiles { get; init; } = Array.Empty<ProjectileSnapshotState>();
    public IReadOnlyList<CoinSnapshotState> CoinStacks { get; init; } = Array.Empty<CoinSnapshotState>();
    public IReadOnlyList<ItemDropSnapshotState> ItemDrops { get; init; } = Array.Empty<ItemDropSnapshotState>();
    public IReadOnlyList<PortalSnapshotState> Portals { get; init; } = Array.Empty<PortalSnapshotState>();
    public required ShopZoneState ShopZone { get; init; }
}
