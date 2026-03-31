using System.Buffers.Binary;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Protocol.X11.Core;

public readonly record struct X11RequestHeader(
    byte MajorOpcode,
    byte MinorOpcode,
    ushort LengthInWords)
{
    public const int Size = 4;

    public int LengthInBytes => LengthInWords * 4;

    public static X11RequestHeader Parse(ReadOnlySpan<byte> buffer, ByteOrder byteOrder)
    {
        if (buffer.Length < Size)
        {
            throw new ArgumentException($"Request header must be {Size} bytes.", nameof(buffer));
        }

        var lengthInWords = byteOrder == ByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(buffer[2..4])
            : BinaryPrimitives.ReadUInt16BigEndian(buffer[2..4]);

        if (lengthInWords == 0)
        {
            throw new InvalidOperationException("X11 request length must be non-zero.");
        }

        return new X11RequestHeader(
            MajorOpcode: buffer[0],
            MinorOpcode: buffer[1],
            LengthInWords: lengthInWords);
    }
}
