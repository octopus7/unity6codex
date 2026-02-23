using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodexSix.TopdownShooter.Net
{
    public enum ConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2
    }

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

    public enum PortalType : byte
    {
        Entry = 1,
        Exit = 2
    }

    public enum PlayerKind : byte
    {
        Human = 0,
        Bot = 1
    }

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

    public struct ClientInputFrame
    {
        public uint InputSeq;
        public uint ClientTick;
        public short MoveX;
        public short MoveY;
        public float AimX;
        public float AimY;
        public byte Buttons;
    }

    public struct ShopPurchaseRequest
    {
        public byte ItemId;
        public uint RequestSeq;
    }

    public struct ServerWelcome
    {
        public int PlayerId;
        public int TickRateHz;
        public int SnapshotRateHz;
        public int MaxPlayers;
        public string ReconnectToken;
    }

    public struct PlayerSnapshot
    {
        public int PlayerId;
        public float PositionX;
        public float PositionY;
        public float AimX;
        public float AimY;
        public short Hp;
        public int CarriedCoins;
        public byte SpeedBuffStacks;
        public PlayerKind Kind;
        public bool IsAlive;
        public bool InShopZone;
    }

    public struct ProjectileSnapshot
    {
        public int ProjectileId;
        public int OwnerPlayerId;
        public float PositionX;
        public float PositionY;
        public float DirectionX;
        public float DirectionY;
    }

    public struct CoinStackSnapshot
    {
        public int CoinStackId;
        public float PositionX;
        public float PositionY;
        public int Amount;
        public bool IsDispenser;
    }

    public struct ItemDropSnapshot
    {
        public int ItemDropId;
        public int ItemId;
        public float PositionX;
        public float PositionY;
        public int Quantity;
    }

    public struct PortalSnapshot
    {
        public byte PortalId;
        public float PositionX;
        public float PositionY;
        public PortalType PortalType;
    }

    public struct ShopZoneSnapshot
    {
        public float MinX;
        public float MinY;
        public float MaxX;
        public float MaxY;
    }

    public struct ServerSnapshot
    {
        public uint ServerTick;
        public uint LastProcessedInputSeq;
        public PlayerSnapshot[] Players;
        public ProjectileSnapshot[] Projectiles;
        public CoinStackSnapshot[] CoinStacks;
        public ItemDropSnapshot[] ItemDrops;
        public PortalSnapshot[] Portals;
        public ShopZoneSnapshot ShopZone;
    }

    public struct GameEventData
    {
        public GameEventType EventType;
        public int ActorId;
        public int TargetId;
        public int Value;
        public int ExtraId;
        public float PositionX;
        public float PositionY;
    }

    public struct ServerEventBatch
    {
        public uint ServerTick;
        public GameEventData[] Events;
    }

    public struct ServerError
    {
        public ushort ErrorCode;
        public string Message;
    }

    public struct PongInfo
    {
        public long RttMs;
        public long ServerUnixMs;
        public bool HasServerBuildVersion;
        public int ServerBuildVersion;
    }

    public interface IGameTransport
    {
        ConnectionState CurrentState { get; }

        Task ConnectAsync(string host, int port, string nickname, string reconnectToken, CancellationToken ct);
        void Disconnect();
        void SendInput(in ClientInputFrame input);
        void SendShopPurchase(in ShopPurchaseRequest request);

        event Action<ServerWelcome> WelcomeReceived;
        event Action<ServerSnapshot> SnapshotReceived;
        event Action<ServerEventBatch> EventReceived;
        event Action<long> PongReceived;
        event Action<ServerError> ErrorReceived;
        event Action<ConnectionState> ConnectionStateChanged;
    }
}
