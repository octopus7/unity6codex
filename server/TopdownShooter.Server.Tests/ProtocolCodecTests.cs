using TopdownShooter.Server.Networking;
using TopdownShooter.Server.Domain;

namespace TopdownShooter.Server.Tests;

public sealed class ProtocolCodecTests
{
    [Fact]
    public void EncodeFrame_ProducesParsableHeader()
    {
        var frame = ProtocolCodec.EncodeFrame(
            MessageType.Ping,
            sequence: 77,
            writer => writer.Write(123456789L));

        Assert.True(frame.Length >= ProtocolConstants.HeaderSize);

        var header = frame.AsSpan(0, ProtocolConstants.HeaderSize);
        var parsed = ProtocolCodec.TryParseHeader(header, out var payloadLength, out var messageType, out var version, out var sequence);

        Assert.True(parsed);
        Assert.Equal(MessageType.Ping, messageType);
        Assert.Equal(ProtocolConstants.ProtocolVersion, version);
        Assert.Equal<uint>(77, sequence);
        Assert.Equal(sizeof(long), payloadLength);
    }

    [Fact]
    public void DecodeInputFrame_ReadsExpectedValues()
    {
        byte[] payload;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((uint)999);
            writer.Write((uint)123);
            writer.Write((short)1000);
            writer.Write((short)-2000);
            writer.Write(0.5f);
            writer.Write(-0.25f);
            writer.Write((byte)0x1);
            writer.Flush();
            payload = stream.ToArray();
        }

        var decoded = ProtocolCodec.DecodeInputFrame(payload);
        Assert.Equal<uint>(999, decoded.InputSeq);
        Assert.Equal<uint>(123, decoded.ClientTick);
        Assert.Equal((short)1000, decoded.MoveX);
        Assert.Equal((short)-2000, decoded.MoveY);
        Assert.Equal(0.5f, decoded.AimX, 3);
        Assert.Equal(-0.25f, decoded.AimY, 3);
        Assert.Equal((byte)0x1, decoded.Buttons);
    }

    [Fact]
    public void DecodeHello_DefaultsToHumanWhenKindIsMissing()
    {
        byte[] payload;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((byte)4);
            writer.Write(new byte[] { (byte)'T', (byte)'e', (byte)'s', (byte)'t' });
            writer.Flush();
            payload = stream.ToArray();
        }

        var decoded = ProtocolCodec.DecodeHello(payload);

        Assert.Equal("Test", decoded.Nickname);
        Assert.Equal(PlayerKind.Human, decoded.Kind);
    }

    [Fact]
    public void DecodeHello_ReadsBotKind()
    {
        byte[] payload;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((byte)3);
            writer.Write(new byte[] { (byte)'A', (byte)'I', (byte)'1' });
            writer.Write((byte)PlayerKind.Bot);
            writer.Flush();
            payload = stream.ToArray();
        }

        var decoded = ProtocolCodec.DecodeHello(payload);

        Assert.Equal("AI1", decoded.Nickname);
        Assert.Equal(PlayerKind.Bot, decoded.Kind);
    }
}
