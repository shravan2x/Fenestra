using System.Buffers.Binary;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Protocol.X11.Parsing;

public static class X11HandshakeParser
{
    public static bool TryParseSetupRequest(ReadOnlySpan<byte> buffer, out X11SetupRequest? request)
    {
        request = null;

        if (buffer.Length < 12)
        {
            return false;
        }

        var byteOrder = buffer[0] switch
        {
            (byte)'l' => ByteOrder.LittleEndian,
            (byte)'B' => ByteOrder.BigEndian,
            _ => (ByteOrder?)null
        };

        if (byteOrder is null)
        {
            return false;
        }

        var protocolMajorVersion = byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(buffer[2..4])
            : BinaryPrimitives.ReadUInt16BigEndian(buffer[2..4]);

        var protocolMinorVersion = byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(buffer[4..6])
            : BinaryPrimitives.ReadUInt16BigEndian(buffer[4..6]);

        var authProtocolNameLength = byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(buffer[6..8])
            : BinaryPrimitives.ReadUInt16BigEndian(buffer[6..8]);

        var authProtocolDataLength = byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(buffer[8..10])
            : BinaryPrimitives.ReadUInt16BigEndian(buffer[8..10]);

        request = new X11SetupRequest(
            byteOrder.Value,
            protocolMajorVersion,
            protocolMinorVersion,
            authProtocolNameLength,
            authProtocolDataLength);

        return true;
    }
}
