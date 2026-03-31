using System.Buffers.Binary;
using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Protocol.X11.Encoding;

public static class X11ErrorEncoder
{
    public static byte[] Encode(
        ByteOrder byteOrder,
        X11ErrorCode errorCode,
        ushort sequenceNumber,
        uint badValue,
        byte majorOpcode,
        ushort minorOpcode = 0)
    {
        var buffer = new byte[32];
        var payload = buffer.AsSpan();

        payload[0] = 0;
        payload[1] = (byte)errorCode;
        WriteUInt16(payload[2..4], sequenceNumber, byteOrder);
        WriteUInt32(payload[4..8], badValue, byteOrder);
        WriteUInt16(payload[8..10], minorOpcode, byteOrder);
        payload[10] = majorOpcode;

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
}
