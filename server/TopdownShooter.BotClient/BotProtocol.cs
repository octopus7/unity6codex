using System.Buffers.Binary;
using System.Text;

namespace TopdownShooter.BotClient;

internal static class BotProtocol
{
    public const ushort ProtocolVersion = 2;
    public const int HeaderSize = 12;

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

    public enum PlayerKind : byte
    {
        Human = 0,
        Bot = 1
    }

    public readonly record struct InputFrame(
        uint InputSeq,
        uint ClientTick,
        short MoveX,
        short MoveY,
        float AimX,
        float AimY,
        byte Buttons);

    public readonly record struct WelcomeData(
        int PlayerId,
        int TickRateHz,
        int SnapshotRateHz,
        int MaxPlayers);

    public readonly record struct SnapshotSummary(
        uint ServerTick,
        int HumanCount,
        int BotCount);

    public readonly record struct ErrorData(
        ushort ErrorCode,
        string Message);

    public static byte[] EncodeHello(uint sequence, string nickname, PlayerKind kind)
    {
        return EncodeFrame(MessageType.Hello, sequence, writer =>
        {
            WriteString8(writer, nickname, maxBytes: 16);
            writer.Write((byte)kind);
        });
    }

    public static byte[] EncodeInput(uint sequence, in InputFrame input)
    {
        var inputCopy = input;
        return EncodeFrame(MessageType.InputFrame, sequence, writer =>
        {
            writer.Write(inputCopy.InputSeq);
            writer.Write(inputCopy.ClientTick);
            writer.Write(inputCopy.MoveX);
            writer.Write(inputCopy.MoveY);
            writer.Write(inputCopy.AimX);
            writer.Write(inputCopy.AimY);
            writer.Write(inputCopy.Buttons);
        });
    }

    public static bool TryParseHeader(ReadOnlySpan<byte> header, out int payloadLength, out MessageType messageType, out ushort version, out uint sequence)
    {
        payloadLength = 0;
        messageType = default;
        version = 0;
        sequence = 0;

        if (header.Length < HeaderSize)
        {
            return false;
        }

        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header[..4]);
        if (payloadLength < 0 || payloadLength > 1024 * 1024)
        {
            return false;
        }

        messageType = (MessageType)BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(4, 2));
        version = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(6, 2));
        sequence = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8, 4));
        return true;
    }

    public static WelcomeData DecodeWelcome(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return new WelcomeData(
            PlayerId: reader.ReadInt32(),
            TickRateHz: reader.ReadInt32(),
            SnapshotRateHz: reader.ReadInt32(),
            MaxPlayers: reader.ReadInt32());
    }

    public static SnapshotSummary DecodeSnapshotSummary(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var serverTick = reader.ReadUInt32();
        _ = reader.ReadUInt32(); // lastProcessedInputSeq

        var playerCount = reader.ReadUInt16();
        var humanCount = 0;
        var botCount = 0;
        for (var i = 0; i < playerCount; i++)
        {
            _ = reader.ReadInt32(); // playerId
            _ = reader.ReadSingle(); // positionX
            _ = reader.ReadSingle(); // positionY
            _ = reader.ReadSingle(); // aimX
            _ = reader.ReadSingle(); // aimY
            _ = reader.ReadInt16(); // hp
            _ = reader.ReadInt32(); // carriedCoins
            _ = reader.ReadByte(); // speedBuffStacks
            var kind = DecodePlayerKind(reader.ReadByte());
            _ = reader.ReadByte(); // flags

            if (kind == PlayerKind.Bot)
            {
                botCount++;
            }
            else
            {
                humanCount++;
            }
        }

        var projectileCount = reader.ReadUInt16();
        for (var i = 0; i < projectileCount; i++)
        {
            _ = reader.ReadInt32(); // projectileId
            _ = reader.ReadInt32(); // ownerPlayerId
            _ = reader.ReadSingle(); // positionX
            _ = reader.ReadSingle(); // positionY
            _ = reader.ReadSingle(); // directionX
            _ = reader.ReadSingle(); // directionY
        }

        var coinCount = reader.ReadUInt16();
        for (var i = 0; i < coinCount; i++)
        {
            _ = reader.ReadInt32(); // coinStackId
            _ = reader.ReadSingle(); // positionX
            _ = reader.ReadSingle(); // positionY
            _ = reader.ReadInt32(); // amount
            _ = reader.ReadByte(); // coin flags (bit0: isDispenser)
        }

        var itemDropCount = reader.ReadUInt16();
        for (var i = 0; i < itemDropCount; i++)
        {
            _ = reader.ReadInt32(); // itemDropId
            _ = reader.ReadInt32(); // itemId
            _ = reader.ReadSingle(); // positionX
            _ = reader.ReadSingle(); // positionY
            _ = reader.ReadInt32(); // quantity
        }

        var portalCount = reader.ReadByte();
        for (var i = 0; i < portalCount; i++)
        {
            _ = reader.ReadByte(); // portalId
            _ = reader.ReadSingle(); // positionX
            _ = reader.ReadSingle(); // positionY
            _ = reader.ReadByte(); // portalType
        }

        _ = reader.ReadSingle(); // shopMinX
        _ = reader.ReadSingle(); // shopMinY
        _ = reader.ReadSingle(); // shopMaxX
        _ = reader.ReadSingle(); // shopMaxY

        return new SnapshotSummary(serverTick, humanCount, botCount);
    }

    public static ErrorData DecodeError(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var errorCode = reader.ReadUInt16();
        var message = ReadString8(reader, 200);
        return new ErrorData(errorCode, message);
    }

    private static byte[] EncodeFrame(MessageType messageType, uint sequence, Action<BinaryWriter> writePayload)
    {
        using var payloadStream = new MemoryStream();
        using (var payloadWriter = new BinaryWriter(payloadStream, Encoding.UTF8, leaveOpen: true))
        {
            writePayload(payloadWriter);
            payloadWriter.Flush();
        }

        var payload = payloadStream.ToArray();
        var frame = new byte[HeaderSize + payload.Length];

        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4, 2), (ushort)messageType);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6, 2), ProtocolVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8, 4), sequence);
        payload.CopyTo(frame, HeaderSize);

        return frame;
    }

    private static void WriteString8(BinaryWriter writer, string value, int maxBytes)
    {
        value ??= string.Empty;
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > maxBytes)
        {
            bytes = bytes[..maxBytes];
        }

        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString8(BinaryReader reader, int maxBytes)
    {
        var length = reader.ReadByte();
        if (length > maxBytes)
        {
            throw new InvalidDataException($"String length {length} exceeds max {maxBytes}.");
        }

        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading string.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static PlayerKind DecodePlayerKind(byte rawKind)
    {
        return rawKind == (byte)PlayerKind.Bot
            ? PlayerKind.Bot
            : PlayerKind.Human;
    }
}
