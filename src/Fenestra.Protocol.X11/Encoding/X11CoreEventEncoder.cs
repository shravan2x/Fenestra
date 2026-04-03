using System.Buffers.Binary;
using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Protocol.X11.Encoding;

public static class X11CoreEventEncoder
{
    public static byte[] Encode(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        return queuedEvent.Kind switch
        {
            X11EventKind.KeyPress => EncodeInputLikeEvent(byteOrder, queuedEvent),
            X11EventKind.KeyRelease => EncodeInputLikeEvent(byteOrder, queuedEvent),
            X11EventKind.ButtonPress => EncodeInputLikeEvent(byteOrder, queuedEvent),
            X11EventKind.ButtonRelease => EncodeInputLikeEvent(byteOrder, queuedEvent),
            X11EventKind.MotionNotify => EncodeInputLikeEvent(byteOrder, queuedEvent),
            X11EventKind.FocusIn => EncodeFocusEvent(byteOrder, queuedEvent),
            X11EventKind.FocusOut => EncodeFocusEvent(byteOrder, queuedEvent),
            X11EventKind.Expose => EncodeExposeEvent(byteOrder, queuedEvent),
            X11EventKind.DestroyNotify => EncodeDestroyNotify(byteOrder, queuedEvent),
            X11EventKind.UnmapNotify => EncodeUnmapNotify(byteOrder, queuedEvent),
            X11EventKind.MapNotify => EncodeMapNotify(byteOrder, queuedEvent),
            X11EventKind.ReparentNotify => EncodeReparentNotify(byteOrder, queuedEvent),
            X11EventKind.ConfigureNotify => EncodeConfigureNotify(byteOrder, queuedEvent),
            X11EventKind.PropertyNotify => EncodePropertyNotify(byteOrder, queuedEvent),
            _ => throw new InvalidOperationException($"Unsupported event kind '{queuedEvent.Kind}'.")
        };
    }

    private static byte[] EncodeInputLikeEvent(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = (byte)queuedEvent.Kind;
        span[1] = queuedEvent.Detail;
        WriteUInt16(span[2..4], queuedEvent.SequenceNumber, byteOrder);
        WriteUInt32(span[4..8], queuedEvent.Time, byteOrder);
        WriteUInt32(span[8..12], queuedEvent.RootWindowId, byteOrder);
        WriteUInt32(span[12..16], queuedEvent.EventWindowId, byteOrder);
        WriteUInt32(span[16..20], 0, byteOrder);
        WriteInt16(span[20..22], queuedEvent.RootX, byteOrder);
        WriteInt16(span[22..24], queuedEvent.RootY, byteOrder);
        WriteInt16(span[24..26], queuedEvent.EventX, byteOrder);
        WriteInt16(span[26..28], queuedEvent.EventY, byteOrder);
        WriteUInt16(span[28..30], queuedEvent.State, byteOrder);

        return buffer;
    }

    private static byte[] EncodeFocusEvent(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = (byte)queuedEvent.Kind;
        span[1] = queuedEvent.Detail;
        WriteUInt16(span[2..4], queuedEvent.SequenceNumber, byteOrder);
        WriteUInt32(span[8..12], queuedEvent.EventWindowId, byteOrder);
        span[12] = 0;

        return buffer;
    }

    private static byte[] EncodeExposeEvent(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = (byte)queuedEvent.Kind;
        WriteUInt16(span[2..4], queuedEvent.SequenceNumber, byteOrder);
        WriteUInt32(span[4..8], queuedEvent.EventWindowId, byteOrder);
        WriteUInt16(span[8..10], unchecked((ushort)queuedEvent.EventX), byteOrder);
        WriteUInt16(span[10..12], unchecked((ushort)queuedEvent.EventY), byteOrder);
        WriteUInt16(span[12..14], queuedEvent.Width, byteOrder);
        WriteUInt16(span[14..16], queuedEvent.Height, byteOrder);

        return buffer;
    }

    private static byte[] EncodeDestroyNotify(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = (byte)queuedEvent.Kind;
        WriteUInt16(span[2..4], queuedEvent.SequenceNumber, byteOrder);
        WriteUInt32(span[4..8], queuedEvent.EventWindowId, byteOrder);
        WriteUInt32(span[8..12], queuedEvent.ChildWindowId, byteOrder);

        return buffer;
    }

    private static byte[] EncodeUnmapNotify(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = (byte)queuedEvent.Kind;
        WriteUInt16(span[2..4], queuedEvent.SequenceNumber, byteOrder);
        WriteUInt32(span[4..8], queuedEvent.EventWindowId, byteOrder);
        WriteUInt32(span[8..12], queuedEvent.ChildWindowId, byteOrder);
        span[12] = queuedEvent.Detail;

        return buffer;
    }

    private static byte[] EncodeMapNotify(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = (byte)queuedEvent.Kind;
        WriteUInt16(span[2..4], queuedEvent.SequenceNumber, byteOrder);
        WriteUInt32(span[4..8], queuedEvent.EventWindowId, byteOrder);
        WriteUInt32(span[8..12], queuedEvent.ChildWindowId, byteOrder);
        span[12] = queuedEvent.Detail;

        return buffer;
    }

    private static byte[] EncodeReparentNotify(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = (byte)queuedEvent.Kind;
        WriteUInt16(span[2..4], queuedEvent.SequenceNumber, byteOrder);
        WriteUInt32(span[4..8], queuedEvent.EventWindowId, byteOrder);
        WriteUInt32(span[8..12], queuedEvent.ChildWindowId, byteOrder);
        WriteUInt32(span[12..16], queuedEvent.RelatedWindowId, byteOrder);
        WriteInt16(span[16..18], queuedEvent.EventX, byteOrder);
        WriteInt16(span[18..20], queuedEvent.EventY, byteOrder);
        span[20] = queuedEvent.Detail;

        return buffer;
    }

    private static byte[] EncodeConfigureNotify(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = (byte)queuedEvent.Kind;
        WriteUInt16(span[2..4], queuedEvent.SequenceNumber, byteOrder);
        WriteUInt32(span[4..8], queuedEvent.EventWindowId, byteOrder);
        WriteUInt32(span[8..12], queuedEvent.ChildWindowId, byteOrder);
        WriteUInt32(span[12..16], queuedEvent.RelatedWindowId, byteOrder);
        WriteInt16(span[16..18], queuedEvent.EventX, byteOrder);
        WriteInt16(span[18..20], queuedEvent.EventY, byteOrder);
        WriteUInt16(span[20..22], queuedEvent.Width, byteOrder);
        WriteUInt16(span[22..24], queuedEvent.Height, byteOrder);
        WriteUInt16(span[24..26], queuedEvent.BorderWidth, byteOrder);
        span[26] = queuedEvent.Detail;

        return buffer;
    }

    private static byte[] EncodePropertyNotify(
        ByteOrder byteOrder,
        QueuedX11Event queuedEvent)
    {
        var buffer = new byte[32];
        var span = buffer.AsSpan();

        span[0] = (byte)queuedEvent.Kind;
        WriteUInt16(span[2..4], queuedEvent.SequenceNumber, byteOrder);
        WriteUInt32(span[4..8], queuedEvent.EventWindowId, byteOrder);
        WriteUInt32(span[8..12], queuedEvent.ChildWindowId, byteOrder);
        WriteUInt32(span[12..16], queuedEvent.Time, byteOrder);
        span[16] = queuedEvent.Detail;

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
