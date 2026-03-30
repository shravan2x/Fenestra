using System.Buffers.Binary;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Protocol.X11.Parsing;

public static class X11HandshakeParser
{
    public const int SetupRequestHeaderLength = 12;

    public static bool TryParseSetupRequest(ReadOnlySpan<byte> buffer, out X11SetupRequest? request)
    {
        request = null;

        if (buffer.Length < SetupRequestHeaderLength)
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

        var paddedNameLength = PadToFourBytes(authProtocolNameLength);
        var paddedDataLength = PadToFourBytes(authProtocolDataLength);
        var expectedLength = SetupRequestHeaderLength + paddedNameLength + paddedDataLength;

        if (buffer.Length != expectedLength)
        {
            return false;
        }

        var nameOffset = SetupRequestHeaderLength;
        var dataOffset = nameOffset + paddedNameLength;
        var authProtocolName = System.Text.Encoding.ASCII.GetString(buffer.Slice(nameOffset, authProtocolNameLength));
        var authProtocolData = buffer.Slice(dataOffset, authProtocolDataLength).ToArray();

        request = new X11SetupRequest(
            byteOrder.Value,
            protocolMajorVersion,
            protocolMinorVersion,
            authProtocolNameLength,
            authProtocolDataLength,
            authProtocolName,
            authProtocolData);

        return true;
    }

    public static int GetSetupRequestLength(ReadOnlySpan<byte> header)
    {
        if (header.Length < SetupRequestHeaderLength)
        {
            throw new ArgumentException(
                $"Setup request header must be at least {SetupRequestHeaderLength} bytes.",
                nameof(header));
        }

        var byteOrder = header[0] switch
        {
            (byte)'l' => ByteOrder.LittleEndian,
            (byte)'B' => ByteOrder.BigEndian,
            _ => throw new InvalidOperationException($"Unsupported X11 byte order value '{header[0]}'.")
        };

        var authProtocolNameLength = byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(header[6..8])
            : BinaryPrimitives.ReadUInt16BigEndian(header[6..8]);

        var authProtocolDataLength = byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(header[8..10])
            : BinaryPrimitives.ReadUInt16BigEndian(header[8..10]);

        return SetupRequestHeaderLength
            + PadToFourBytes(authProtocolNameLength)
            + PadToFourBytes(authProtocolDataLength);
    }

    private static int PadToFourBytes(int length)
    {
        return (length + 3) & ~3;
    }
}
