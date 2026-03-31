using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Server;

public sealed class X11ServerHandshakeConfiguration
{
    public static X11ServerHandshakeConfiguration CreateDefault()
    {
        return new X11ServerHandshakeConfiguration(
            releaseNumber: 1,
            motionBufferSize: 0,
            vendor: "Fenestra",
            maximumRequestLength: ushort.MaxValue);
    }

    public X11ServerHandshakeConfiguration(
        uint releaseNumber,
        uint motionBufferSize,
        string vendor,
        ushort maximumRequestLength)
    {
        ReleaseNumber = releaseNumber;
        MotionBufferSize = motionBufferSize;
        Vendor = string.IsNullOrWhiteSpace(vendor)
            ? throw new ArgumentException("Vendor name is required.", nameof(vendor))
            : vendor;
        MaximumRequestLength = maximumRequestLength;
    }

    public uint ReleaseNumber { get; }

    public uint MotionBufferSize { get; }

    public string Vendor { get; }

    public ushort MaximumRequestLength { get; }

    public X11SetupSuccessResponse CreateSuccessResponse(
        ByteOrder byteOrder,
        X11DisplayState displayState,
        X11ClientState clientState)
    {
        ArgumentNullException.ThrowIfNull(displayState);
        ArgumentNullException.ThrowIfNull(clientState);

        return new X11SetupSuccessResponse(
            ByteOrder: byteOrder,
            ProtocolMajorVersion: 11,
            ProtocolMinorVersion: 0,
            ReleaseNumber: ReleaseNumber,
            ResourceIdBase: clientState.ResourceIdBase,
            ResourceIdMask: clientState.ResourceIdMask,
            MotionBufferSize: MotionBufferSize,
            Vendor: Vendor,
            MaximumRequestLength: MaximumRequestLength,
            ImageByteOrder: byteOrder == ByteOrder.LittleEndian ? (byte)'l' : (byte)'B',
            BitmapBitOrder: byteOrder == ByteOrder.LittleEndian ? (byte)'l' : (byte)'B',
            BitmapScanlineUnit: 32,
            BitmapScanlinePad: 32,
            MinKeycode: 8,
            MaxKeycode: 255,
            PixmapFormats:
            [
                new X11PixmapFormat(Depth: displayState.RootDepth, BitsPerPixel: 32, ScanlinePad: 32)
            ],
            Screens:
            [
                new X11Screen(
                    RootWindowId: displayState.RootWindowId,
                    DefaultColormapId: displayState.DefaultColormapId,
                    WhitePixel: displayState.WhitePixel,
                    BlackPixel: displayState.BlackPixel,
                    CurrentInputMasks: 0,
                    WidthInPixels: displayState.ScreenWidthInPixels,
                    HeightInPixels: displayState.ScreenHeightInPixels,
                    WidthInMillimeters: displayState.ScreenWidthInMillimeters,
                    HeightInMillimeters: displayState.ScreenHeightInMillimeters,
                    MinInstalledMaps: 1,
                    MaxInstalledMaps: 1,
                    RootVisualId: displayState.RootVisualId,
                    BackingStores: 0,
                    SaveUnders: false,
                    RootDepth: displayState.RootDepth,
                    AllowedDepths: displayState.AllowedDepths
                        .Select(static depth => new X11Depth(
                            Depth: depth.Depth,
                            Visuals: depth.Visuals
                                .Select(static visual => new X11Visual(
                                    VisualId: visual.VisualId,
                                    VisualClass: visual.VisualClass,
                                    BitsPerRgbValue: visual.BitsPerRgbValue,
                                    ColormapEntries: visual.ColormapEntries,
                                    RedMask: visual.RedMask,
                                    GreenMask: visual.GreenMask,
                                    BlueMask: visual.BlueMask))
                                .ToArray()))
                        .ToArray())
            ]);
    }
}
