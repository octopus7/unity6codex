namespace TopdownShooter.Server.Networking;

public enum MessageType : ushort
{
    Hello = 1,
    InputFrame = 2,
    Ping = 3,
    ShopPurchaseRequest = 4,

    Welcome = 101,
    Snapshot = 102,
    EventBatch = 103,
    Pong = 104,
    Error = 199
}

public static class ProtocolConstants
{
    public const ushort ProtocolVersion = 2;
    public const int HeaderSize = 12;
}
