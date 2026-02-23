using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace CodexSix.TopdownShooter.Net
{
    public static class NetProtocolCodec
    {
        public const ushort ProtocolVersion = 2;
        public const int HeaderSize = 12;
        // Must match server's ProtocolConstants.ServerBuildVersion.
        public const int ExpectedServerBuildVersion = 1;
        private const int MaxReconnectTokenBytes = 96;

        public static byte[] EncodeHello(
            uint sequence,
            string nickname,
            PlayerKind kind = PlayerKind.Human,
            string reconnectToken = "")
        {
            return EncodeFrame(MessageType.Hello, sequence, writer =>
            {
                WriteString8(writer, nickname ?? "Guest", 16);
                writer.Write((byte)kind);
                WriteString8(writer, reconnectToken ?? string.Empty, MaxReconnectTokenBytes);
            });
        }

        public static byte[] EncodeInput(uint sequence, in ClientInputFrame input)
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

        public static byte[] EncodeShopPurchase(uint sequence, in ShopPurchaseRequest request)
        {
            var requestCopy = request;
            return EncodeFrame(MessageType.ShopPurchaseRequest, sequence, writer =>
            {
                writer.Write(requestCopy.ItemId);
                writer.Write(requestCopy.RequestSeq);
            });
        }

        public static byte[] EncodePing(uint sequence, long clientUnixMs)
        {
            return EncodeFrame(MessageType.Ping, sequence, writer =>
            {
                writer.Write(clientUnixMs);
            });
        }

        public static bool TryParseHeader(byte[] header, out int payloadLength, out MessageType messageType, out ushort version, out uint sequence)
        {
            payloadLength = 0;
            messageType = default;
            version = 0;
            sequence = 0;

            if (header == null || header.Length < HeaderSize)
            {
                return false;
            }

            var headerSpan = header.AsSpan();
            payloadLength = BinaryPrimitives.ReadInt32LittleEndian(headerSpan.Slice(0, 4));
            if (payloadLength < 0 || payloadLength > (1024 * 1024))
            {
                return false;
            }

            messageType = (MessageType)BinaryPrimitives.ReadUInt16LittleEndian(headerSpan.Slice(4, 2));
            version = BinaryPrimitives.ReadUInt16LittleEndian(headerSpan.Slice(6, 2));
            sequence = BinaryPrimitives.ReadUInt32LittleEndian(headerSpan.Slice(8, 4));
            return true;
        }

        public static ServerWelcome DecodeWelcome(byte[] payload)
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var welcome = new ServerWelcome
            {
                PlayerId = reader.ReadInt32(),
                TickRateHz = reader.ReadInt32(),
                SnapshotRateHz = reader.ReadInt32(),
                MaxPlayers = reader.ReadInt32()
            };

            welcome.ReconnectToken = stream.Position < stream.Length
                ? ReadString8(reader, MaxReconnectTokenBytes)
                : string.Empty;
            return welcome;
        }

        public static ServerSnapshot DecodeSnapshot(byte[] payload)
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var snapshot = new ServerSnapshot
            {
                ServerTick = reader.ReadUInt32(),
                LastProcessedInputSeq = reader.ReadUInt32()
            };

            var playerCount = reader.ReadUInt16();
            snapshot.Players = new PlayerSnapshot[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                var flags = (byte)0;
                var player = new PlayerSnapshot
                {
                    PlayerId = reader.ReadInt32(),
                    PositionX = reader.ReadSingle(),
                    PositionY = reader.ReadSingle(),
                    AimX = reader.ReadSingle(),
                    AimY = reader.ReadSingle(),
                    Hp = reader.ReadInt16(),
                    CarriedCoins = reader.ReadInt32(),
                    SpeedBuffStacks = reader.ReadByte(),
                    Kind = DecodePlayerKind(reader.ReadByte())
                };

                flags = reader.ReadByte();
                player.IsAlive = (flags & 0x1) != 0;
                player.InShopZone = (flags & 0x2) != 0;
                snapshot.Players[i] = player;
            }

            var projectileCount = reader.ReadUInt16();
            snapshot.Projectiles = new ProjectileSnapshot[projectileCount];
            for (var i = 0; i < projectileCount; i++)
            {
                snapshot.Projectiles[i] = new ProjectileSnapshot
                {
                    ProjectileId = reader.ReadInt32(),
                    OwnerPlayerId = reader.ReadInt32(),
                    PositionX = reader.ReadSingle(),
                    PositionY = reader.ReadSingle(),
                    DirectionX = reader.ReadSingle(),
                    DirectionY = reader.ReadSingle()
                };
            }

            var coinCount = reader.ReadUInt16();
            snapshot.CoinStacks = new CoinStackSnapshot[coinCount];
            for (var i = 0; i < coinCount; i++)
            {
                snapshot.CoinStacks[i] = new CoinStackSnapshot
                {
                    CoinStackId = reader.ReadInt32(),
                    PositionX = reader.ReadSingle(),
                    PositionY = reader.ReadSingle(),
                    Amount = reader.ReadInt32(),
                    IsDispenser = (reader.ReadByte() & 0x1) != 0
                };
            }

            var itemDropCount = reader.ReadUInt16();
            snapshot.ItemDrops = new ItemDropSnapshot[itemDropCount];
            for (var i = 0; i < itemDropCount; i++)
            {
                snapshot.ItemDrops[i] = new ItemDropSnapshot
                {
                    ItemDropId = reader.ReadInt32(),
                    ItemId = reader.ReadInt32(),
                    PositionX = reader.ReadSingle(),
                    PositionY = reader.ReadSingle(),
                    Quantity = reader.ReadInt32()
                };
            }

            var portalCount = reader.ReadByte();
            snapshot.Portals = new PortalSnapshot[portalCount];
            for (var i = 0; i < portalCount; i++)
            {
                snapshot.Portals[i] = new PortalSnapshot
                {
                    PortalId = reader.ReadByte(),
                    PositionX = reader.ReadSingle(),
                    PositionY = reader.ReadSingle(),
                    PortalType = (PortalType)reader.ReadByte()
                };
            }

            snapshot.ShopZone = new ShopZoneSnapshot
            {
                MinX = reader.ReadSingle(),
                MinY = reader.ReadSingle(),
                MaxX = reader.ReadSingle(),
                MaxY = reader.ReadSingle()
            };

            return snapshot;
        }

        public static ServerEventBatch DecodeEventBatch(byte[] payload)
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var serverTick = reader.ReadUInt32();
            var count = reader.ReadUInt16();
            var events = new GameEventData[count];
            for (var i = 0; i < count; i++)
            {
                events[i] = new GameEventData
                {
                    EventType = (GameEventType)reader.ReadByte(),
                    ActorId = reader.ReadInt32(),
                    TargetId = reader.ReadInt32(),
                    Value = reader.ReadInt32(),
                    ExtraId = reader.ReadInt32(),
                    PositionX = reader.ReadSingle(),
                    PositionY = reader.ReadSingle()
                };
            }

            return new ServerEventBatch
            {
                ServerTick = serverTick,
                Events = events
            };
        }

        public static long DecodePong(byte[] payload)
        {
            return DecodePongInfo(payload).RttMs;
        }

        public static PongInfo DecodePongInfo(byte[] payload)
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var clientUnixMs = reader.ReadInt64();
            var serverUnixMs = reader.ReadInt64();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var estimatedRtt = Math.Max(0L, now - clientUnixMs);
            var hasServerBuildVersion = stream.Position < stream.Length;
            var serverBuildVersion = hasServerBuildVersion ? reader.ReadInt32() : 0;

            return new PongInfo
            {
                RttMs = estimatedRtt,
                ServerUnixMs = serverUnixMs,
                HasServerBuildVersion = hasServerBuildVersion,
                ServerBuildVersion = serverBuildVersion
            };
        }

        public static ServerError DecodeError(byte[] payload)
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            return new ServerError
            {
                ErrorCode = reader.ReadUInt16(),
                Message = ReadString8(reader, 200)
            };
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
            var frameSpan = frame.AsSpan();

            BinaryPrimitives.WriteInt32LittleEndian(frameSpan.Slice(0, 4), payload.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(frameSpan.Slice(4, 2), (ushort)messageType);
            BinaryPrimitives.WriteUInt16LittleEndian(frameSpan.Slice(6, 2), ProtocolVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(frameSpan.Slice(8, 4), sequence);
            payload.CopyTo(frame, HeaderSize);

            return frame;
        }

        private static void WriteString8(BinaryWriter writer, string value, int maxBytes)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > maxBytes)
            {
                Array.Resize(ref bytes, maxBytes);
            }

            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString8(BinaryReader reader, int maxBytes)
        {
            var length = reader.ReadByte();
            if (length > maxBytes)
            {
                throw new InvalidDataException("String length is out of allowed range.");
            }

            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Could not read full string payload.");
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
}
