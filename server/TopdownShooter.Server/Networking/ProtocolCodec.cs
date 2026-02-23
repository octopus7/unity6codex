using System.Buffers.Binary;
using System.Text;
using TopdownShooter.Server.Configuration;
using TopdownShooter.Server.Domain;

namespace TopdownShooter.Server.Networking;

public static class ProtocolCodec
{
    private const int MaxReconnectTokenBytes = 96;

    public static byte[] EncodeFrame(MessageType messageType, uint sequence, Action<BinaryWriter> writePayload)
    {
        using var payloadStream = new MemoryStream();
        using (var payloadWriter = new BinaryWriter(payloadStream, Encoding.UTF8, leaveOpen: true))
        {
            writePayload(payloadWriter);
            payloadWriter.Flush();
        }

        var payload = payloadStream.ToArray();
        var frame = new byte[ProtocolConstants.HeaderSize + payload.Length];

        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4, 2), (ushort)messageType);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6, 2), ProtocolConstants.ProtocolVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8, 4), sequence);

        payload.CopyTo(frame, ProtocolConstants.HeaderSize);
        return frame;
    }

    public static bool TryParseHeader(ReadOnlySpan<byte> header, out int payloadLength, out MessageType messageType, out ushort protocolVersion, out uint sequence)
    {
        payloadLength = 0;
        messageType = default;
        protocolVersion = 0;
        sequence = 0;

        if (header.Length < ProtocolConstants.HeaderSize)
        {
            return false;
        }

        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header[..4]);
        if (payloadLength < 0 || payloadLength > 1024 * 1024)
        {
            return false;
        }

        messageType = (MessageType)BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(4, 2));
        protocolVersion = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(6, 2));
        sequence = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8, 4));
        return true;
    }

    public static HelloMessage DecodeHello(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var nickname = ReadString8(reader, GameRules.MaxNicknameBytes);
        var kind = PlayerKind.Human;
        var reconnectToken = string.Empty;
        if (stream.Position < stream.Length)
        {
            var rawKind = reader.ReadByte();
            kind = rawKind == (byte)PlayerKind.Bot
                ? PlayerKind.Bot
                : PlayerKind.Human;
        }

        if (stream.Position < stream.Length)
        {
            reconnectToken = ReadString8(reader, MaxReconnectTokenBytes);
        }

        return new HelloMessage(nickname, kind, reconnectToken);
    }

    public static InputFrameMessage DecodeInputFrame(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        return new InputFrameMessage(
            InputSeq: reader.ReadUInt32(),
            ClientTick: reader.ReadUInt32(),
            MoveX: reader.ReadInt16(),
            MoveY: reader.ReadInt16(),
            AimX: reader.ReadSingle(),
            AimY: reader.ReadSingle(),
            Buttons: reader.ReadByte());
    }

    public static ShopPurchaseRequestMessage DecodeShopPurchaseRequest(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return new ShopPurchaseRequestMessage(
            ItemId: reader.ReadByte(),
            RequestSeq: reader.ReadUInt32());
    }

    public static PingMessage DecodePing(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return new PingMessage(reader.ReadInt64());
    }

    public static void WriteWelcomePayload(BinaryWriter writer, int playerId, ServerConfig config, string reconnectToken)
    {
        writer.Write(playerId);
        writer.Write(config.TickRateHz);
        writer.Write(config.SnapshotRateHz);
        writer.Write(config.MaxPlayers);
        WriteString8(writer, reconnectToken ?? string.Empty, MaxReconnectTokenBytes);
    }

    public static void WritePongPayload(BinaryWriter writer, long clientUnixMs, long serverUnixMs, int serverBuildVersion)
    {
        writer.Write(clientUnixMs);
        writer.Write(serverUnixMs);
        writer.Write(serverBuildVersion);
    }

    public static void WriteErrorPayload(BinaryWriter writer, ushort errorCode, string message)
    {
        writer.Write(errorCode);
        WriteString8(writer, message, maxBytes: 200);
    }

    public static void WriteSnapshotPayload(BinaryWriter writer, WorldSnapshot snapshot, uint lastProcessedInputSeq)
    {
        writer.Write(snapshot.ServerTick);
        writer.Write(lastProcessedInputSeq);

        writer.Write((ushort)snapshot.Players.Count);
        foreach (var player in snapshot.Players)
        {
            writer.Write(player.PlayerId);
            writer.Write(player.Position.X);
            writer.Write(player.Position.Y);
            writer.Write(player.AimDirection.X);
            writer.Write(player.AimDirection.Y);
            writer.Write(player.Hp);
            writer.Write(player.CarriedCoins);
            writer.Write(player.SpeedBuffStacks);
            writer.Write((byte)player.Kind);

            byte flags = 0;
            if (player.IsAlive)
            {
                flags |= 0x1;
            }

            if (player.InShopZone)
            {
                flags |= 0x2;
            }

            writer.Write(flags);
        }

        writer.Write((ushort)snapshot.Projectiles.Count);
        foreach (var projectile in snapshot.Projectiles)
        {
            writer.Write(projectile.ProjectileId);
            writer.Write(projectile.OwnerPlayerId);
            writer.Write(projectile.Position.X);
            writer.Write(projectile.Position.Y);
            writer.Write(projectile.Direction.X);
            writer.Write(projectile.Direction.Y);
        }

        writer.Write((ushort)snapshot.CoinStacks.Count);
        foreach (var coin in snapshot.CoinStacks)
        {
            writer.Write(coin.CoinStackId);
            writer.Write(coin.Position.X);
            writer.Write(coin.Position.Y);
            writer.Write(coin.Amount);
            writer.Write((byte)(coin.IsDispenser ? 0x1 : 0x0));
        }

        writer.Write((ushort)snapshot.ItemDrops.Count);
        foreach (var itemDrop in snapshot.ItemDrops)
        {
            writer.Write(itemDrop.ItemDropId);
            writer.Write(itemDrop.ItemId);
            writer.Write(itemDrop.Position.X);
            writer.Write(itemDrop.Position.Y);
            writer.Write(itemDrop.Quantity);
        }

        writer.Write((byte)snapshot.Portals.Count);
        foreach (var portal in snapshot.Portals)
        {
            writer.Write(portal.PortalId);
            writer.Write(portal.Position.X);
            writer.Write(portal.Position.Y);
            writer.Write((byte)portal.PortalType);
        }

        writer.Write(snapshot.ShopZone.MinX);
        writer.Write(snapshot.ShopZone.MinY);
        writer.Write(snapshot.ShopZone.MaxX);
        writer.Write(snapshot.ShopZone.MaxY);
    }

    public static void WriteEventBatchPayload(BinaryWriter writer, uint serverTick, IReadOnlyList<GameEvent> events)
    {
        writer.Write(serverTick);
        writer.Write((ushort)events.Count);
        foreach (var gameEvent in events)
        {
            writer.Write((byte)gameEvent.Type);
            writer.Write(gameEvent.ActorId);
            writer.Write(gameEvent.TargetId);
            writer.Write(gameEvent.Value);
            writer.Write(gameEvent.ExtraId);
            writer.Write(gameEvent.Position.X);
            writer.Write(gameEvent.Position.Y);
        }
    }

    public static void WriteString8(BinaryWriter writer, string value, int maxBytes)
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

    public static string ReadString8(BinaryReader reader, int maxBytes)
    {
        var length = reader.ReadByte();
        if (length > maxBytes)
        {
            throw new InvalidDataException($"String length {length} exceeds max {maxBytes}");
        }

        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException("Unexpected end of stream when reading string");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
