using System.Buffers.Binary;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Protocol.X11.Encoding;

public static class X11SetupResponseEncoder
{
    public static byte[] EncodeSuccess(X11SetupSuccessResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var vendorBytes = System.Text.Encoding.ASCII.GetBytes(response.Vendor);
        var paddedVendorLength = PadToFourBytes(vendorBytes.Length);
        var additionalLengthWords = 8
            + (paddedVendorLength / 4)
            + (2 * response.PixmapFormats.Count)
            + response.Screens.Sum(GetScreenLengthInWords);
        var buffer = new byte[8 + (additionalLengthWords * 4)];
        var payload = buffer.AsSpan();

        payload[0] = 1;
        payload[1] = 0;
        WriteUInt16(payload[2..4], response.ProtocolMajorVersion, response.ByteOrder);
        WriteUInt16(payload[4..6], response.ProtocolMinorVersion, response.ByteOrder);
        WriteUInt16(payload[6..8], (ushort)additionalLengthWords, response.ByteOrder);
        WriteUInt32(payload[8..12], response.ReleaseNumber, response.ByteOrder);
        WriteUInt32(payload[12..16], response.ResourceIdBase, response.ByteOrder);
        WriteUInt32(payload[16..20], response.ResourceIdMask, response.ByteOrder);
        WriteUInt32(payload[20..24], response.MotionBufferSize, response.ByteOrder);
        WriteUInt16(payload[24..26], (ushort)vendorBytes.Length, response.ByteOrder);
        WriteUInt16(payload[26..28], response.MaximumRequestLength, response.ByteOrder);
        payload[28] = (byte)response.Screens.Count;
        payload[29] = (byte)response.PixmapFormats.Count;
        payload[30] = response.ImageByteOrder;
        payload[31] = response.BitmapBitOrder;
        payload[32] = response.BitmapScanlineUnit;
        payload[33] = response.BitmapScanlinePad;
        payload[34] = response.MinKeycode;
        payload[35] = response.MaxKeycode;

        var offset = 40;
        vendorBytes.CopyTo(payload[offset..]);
        offset += paddedVendorLength;

        foreach (var format in response.PixmapFormats)
        {
            payload[offset] = format.Depth;
            payload[offset + 1] = format.BitsPerPixel;
            payload[offset + 2] = format.ScanlinePad;
            offset += 8;
        }

        foreach (var screen in response.Screens)
        {
            WriteUInt32(payload[offset..(offset + 4)], screen.RootWindowId, response.ByteOrder);
            WriteUInt32(payload[(offset + 4)..(offset + 8)], screen.DefaultColormapId, response.ByteOrder);
            WriteUInt32(payload[(offset + 8)..(offset + 12)], screen.WhitePixel, response.ByteOrder);
            WriteUInt32(payload[(offset + 12)..(offset + 16)], screen.BlackPixel, response.ByteOrder);
            WriteUInt32(payload[(offset + 16)..(offset + 20)], screen.CurrentInputMasks, response.ByteOrder);
            WriteUInt16(payload[(offset + 20)..(offset + 22)], screen.WidthInPixels, response.ByteOrder);
            WriteUInt16(payload[(offset + 22)..(offset + 24)], screen.HeightInPixels, response.ByteOrder);
            WriteUInt16(payload[(offset + 24)..(offset + 26)], screen.WidthInMillimeters, response.ByteOrder);
            WriteUInt16(payload[(offset + 26)..(offset + 28)], screen.HeightInMillimeters, response.ByteOrder);
            WriteUInt16(payload[(offset + 28)..(offset + 30)], screen.MinInstalledMaps, response.ByteOrder);
            WriteUInt16(payload[(offset + 30)..(offset + 32)], screen.MaxInstalledMaps, response.ByteOrder);
            WriteUInt32(payload[(offset + 32)..(offset + 36)], screen.RootVisualId, response.ByteOrder);
            payload[offset + 36] = screen.BackingStores;
            payload[offset + 37] = screen.SaveUnders ? (byte)1 : (byte)0;
            payload[offset + 38] = screen.RootDepth;
            payload[offset + 39] = (byte)screen.AllowedDepths.Count;
            offset += 40;

            foreach (var depth in screen.AllowedDepths)
            {
                WriteDepth(payload, ref offset, depth, response.ByteOrder);
            }
        }

        return buffer;
    }

    public static byte[] EncodeFailure(X11SetupFailureResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var reasonBytes = System.Text.Encoding.ASCII.GetBytes(response.Reason);
        var paddedReasonLength = PadToFourBytes(reasonBytes.Length);
        var buffer = new byte[8 + paddedReasonLength];
        var payload = buffer.AsSpan();

        payload[0] = 0;
        payload[1] = (byte)reasonBytes.Length;
        WriteUInt16(payload[2..4], response.ProtocolMajorVersion, response.ByteOrder);
        WriteUInt16(payload[4..6], response.ProtocolMinorVersion, response.ByteOrder);
        WriteUInt16(payload[6..8], (ushort)(paddedReasonLength / 4), response.ByteOrder);
        reasonBytes.CopyTo(payload[8..]);

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

    private static void WriteDepth(
        Span<byte> payload,
        ref int offset,
        X11Depth depth,
        ByteOrder byteOrder)
    {
        payload[offset] = depth.Depth;
        payload[offset + 1] = 0;
        WriteUInt16(payload[(offset + 2)..(offset + 4)], (ushort)depth.Visuals.Count, byteOrder);
        offset += 8;

        foreach (var visual in depth.Visuals)
        {
            WriteUInt32(payload[offset..(offset + 4)], visual.VisualId, byteOrder);
            payload[offset + 4] = visual.VisualClass;
            payload[offset + 5] = visual.BitsPerRgbValue;
            WriteUInt16(payload[(offset + 6)..(offset + 8)], visual.ColormapEntries, byteOrder);
            WriteUInt32(payload[(offset + 8)..(offset + 12)], visual.RedMask, byteOrder);
            WriteUInt32(payload[(offset + 12)..(offset + 16)], visual.GreenMask, byteOrder);
            WriteUInt32(payload[(offset + 16)..(offset + 20)], visual.BlueMask, byteOrder);
            offset += 24;
        }
    }

    private static int GetScreenLengthInWords(X11Screen screen)
    {
        return 10 + screen.AllowedDepths.Sum(GetDepthLengthInWords);
    }

    private static int GetDepthLengthInWords(X11Depth depth)
    {
        return 2 + (6 * depth.Visuals.Count);
    }

    private static int PadToFourBytes(int length)
    {
        return (length + 3) & ~3;
    }
}
