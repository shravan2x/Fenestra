using System.Buffers.Binary;
using System.Text;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Protocol.X11.Encoding;

public static class X11CoreReplyEncoder
{
    public static byte[] EncodeGetImageReply(
        ByteOrder byteOrder,
        ushort sequenceNumber,
        byte depth,
        uint visualId,
        ReadOnlySpan<byte> imageBytes)
    {
        var additionalLengthWords = imageBytes.Length / 4;
        var buffer = new byte[32 + imageBytes.Length];
        var span = buffer.AsSpan();

        span[0] = 1;
        span[1] = depth;
        WriteUInt16(span[2..4], sequenceNumber, byteOrder);
        WriteUInt32(span[4..8], (uint)additionalLengthWords, byteOrder);
        WriteUInt32(span[8..12], visualId, byteOrder);
        imageBytes.CopyTo(span[32..]);

        return buffer;
    }

    public static byte[] EncodeGetGeometryReply(
        ByteOrder byteOrder,
        ushort sequenceNumber,
        byte depth,
        uint rootWindowId,
        short x,
        short y,
        ushort width,
        ushort height,
        ushort borderWidth)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = 1;
        span[1] = depth;
        WriteUInt16(span[2..4], sequenceNumber, byteOrder);
        WriteUInt32(span[4..8], 0, byteOrder);
        WriteUInt32(span[8..12], rootWindowId, byteOrder);
        WriteInt16(span[12..14], x, byteOrder);
        WriteInt16(span[14..16], y, byteOrder);
        WriteUInt16(span[16..18], width, byteOrder);
        WriteUInt16(span[18..20], height, byteOrder);
        WriteUInt16(span[20..22], borderWidth, byteOrder);

        return buffer;
    }

    public static byte[] EncodeQueryTreeReply(
        ByteOrder byteOrder,
        ushort sequenceNumber,
        uint rootWindowId,
        uint parentWindowId,
        IReadOnlyList<uint> childWindowIds)
    {
        ArgumentNullException.ThrowIfNull(childWindowIds);

        var childListLength = childWindowIds.Count * sizeof(uint);
        var additionalLengthWords = childListLength / 4;
        var buffer = new byte[32 + childListLength];
        var span = buffer.AsSpan();

        span[0] = 1;
        WriteUInt16(span[2..4], sequenceNumber, byteOrder);
        WriteUInt32(span[4..8], (uint)additionalLengthWords, byteOrder);
        WriteUInt32(span[8..12], rootWindowId, byteOrder);
        WriteUInt32(span[12..16], parentWindowId, byteOrder);
        WriteUInt16(span[16..18], (ushort)childWindowIds.Count, byteOrder);

        var offset = 32;
        foreach (var childWindowId in childWindowIds)
        {
            WriteUInt32(span[offset..(offset + 4)], childWindowId, byteOrder);
            offset += 4;
        }

        return buffer;
    }

    public static byte[] EncodeInternAtomReply(
        ByteOrder byteOrder,
        ushort sequenceNumber,
        uint atomId)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = 1;
        WriteUInt16(span[2..4], sequenceNumber, byteOrder);
        WriteUInt32(span[4..8], 0, byteOrder);
        WriteUInt32(span[8..12], atomId, byteOrder);

        return buffer;
    }

    private static void WriteUInt16(Span<byte> destination, ushort value, ByteOrder byteOrder)
    {
        if (byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, value);
        }
    }

    private static void WriteUInt32(Span<byte> destination, uint value, ByteOrder byteOrder)
    {
        if (byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination, value);
        }
    }

    private static void WriteInt16(Span<byte> destination, short value, ByteOrder byteOrder)
    {
        if (byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteInt16LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteInt16BigEndian(destination, value);
        }
    }
}
