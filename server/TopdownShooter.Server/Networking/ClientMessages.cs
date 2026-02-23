using TopdownShooter.Server.Domain;

namespace TopdownShooter.Server.Networking;

public readonly record struct HelloMessage(string Nickname, PlayerKind Kind);

public readonly record struct InputFrameMessage(
    uint InputSeq,
    uint ClientTick,
    short MoveX,
    short MoveY,
    float AimX,
    float AimY,
    byte Buttons);

public readonly record struct ShopPurchaseRequestMessage(
    byte ItemId,
    uint RequestSeq);

public readonly record struct PingMessage(long ClientUnixMs);

public readonly record struct InboundPacket(
    MessageType MessageType,
    uint Sequence,
    byte[] Payload);
