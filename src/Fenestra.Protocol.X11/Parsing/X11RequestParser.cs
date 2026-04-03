using System.Buffers.Binary;
using System.Text;
using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Protocol.X11.Parsing;

public static class X11RequestParser
{
    public static X11RequestHeader ParseHeader(ReadOnlySpan<byte> buffer, ByteOrder byteOrder)
    {
        return X11RequestHeader.Parse(buffer, byteOrder);
    }

    public static uint ReadUInt32(ReadOnlySpan<byte> buffer, ByteOrder byteOrder)
    {
        return byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(buffer)
            : BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    public static ushort ReadUInt16(ReadOnlySpan<byte> buffer, ByteOrder byteOrder)
    {
        return byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(buffer)
            : BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    public static short ReadInt16(ReadOnlySpan<byte> buffer, ByteOrder byteOrder)
    {
        return byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadInt16LittleEndian(buffer)
            : BinaryPrimitives.ReadInt16BigEndian(buffer);
    }

    public static string ReadPaddedAsciiString(ReadOnlySpan<byte> buffer, int offset, int length)
    {
        return System.Text.Encoding.ASCII.GetString(buffer.Slice(offset, length));
    }

    public static int PadToFourBytes(int length)
    {
        return (length + 3) & ~3;
    }
}
