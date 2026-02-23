namespace TopdownShooter.Server.Domain;

public enum GameEventType : byte
{
    ShotFired = 1,
    Damage = 2,
    Death = 3,
    Respawn = 4,
    CoinDropped = 5,
    CoinPicked = 6,
    ShopPurchased = 7,
    PurchaseRejected = 8,
    ItemPicked = 9
}

public enum PurchaseRejectReason : int
{
    None = 0,
    Dead = 1,
    NotInShop = 2,
    NotEnoughCoins = 3,
    MaxStacksReached = 4,
    UnknownItem = 5
}

public readonly record struct GameEvent(
    GameEventType Type,
    int ActorId,
    int TargetId,
    int Value,
    int ExtraId,
    Vector2f Position);
