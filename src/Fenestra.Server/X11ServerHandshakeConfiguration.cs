using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Server;

public sealed class X11ServerHandshakeConfiguration
{
    public static X11ServerHandshakeConfiguration CreateDefault()
    {
        return new X11ServerHandshakeConfiguration(
            ReleaseNumber: 1,
            ResourceIdBase: 0x0020_0000,
            ResourceIdMask: 0x001F_FFFF,
            MotionBufferSize: 0,
            Vendor: "Fenestra",
            MaximumRequestLength: ushort.MaxValue,
            ScreenWidthInPixels: 1024,
            ScreenHeightInPixels: 768,
            ScreenWidthInMillimeters: 270,
            ScreenHeightInMillimeters: 203);
    }

    public X11ServerHandshakeConfiguration(
        uint ReleaseNumber,
        uint ResourceIdBase,
        uint ResourceIdMask,
        uint MotionBufferSize,
        string Vendor,
        ushort MaximumRequestLength,
        ushort ScreenWidthInPixels,
        ushort ScreenHeightInPixels,
        ushort ScreenWidthInMillimeters,
        ushort ScreenHeightInMillimeters)
    {
        this.ReleaseNumber = ReleaseNumber;
        this.ResourceIdBase = ResourceIdBase;
        this.ResourceIdMask = ResourceIdMask;
        this.MotionBufferSize = MotionBufferSize;
        this.Vendor = Vendor;
        this.MaximumRequestLength = MaximumRequestLength;
        this.ScreenWidthInPixels = ScreenWidthInPixels;
        this.ScreenHeightInPixels = ScreenHeightInPixels;
        this.ScreenWidthInMillimeters = ScreenWidthInMillimeters;
        this.ScreenHeightInMillimeters = ScreenHeightInMillimeters;
    }

    public uint ReleaseNumber { get; }

    public uint ResourceIdBase { get; }

    public uint ResourceIdMask { get; }

    public uint MotionBufferSize { get; }

    public string Vendor { get; }

    public ushort MaximumRequestLength { get; }

    public ushort ScreenWidthInPixels { get; }

    public ushort ScreenHeightInPixels { get; }

    public ushort ScreenWidthInMillimeters { get; }

    public ushort ScreenHeightInMillimeters { get; }

    public X11SetupSuccessResponse CreateSuccessResponse(ByteOrder byteOrder)
    {
        return new X11SetupSuccessResponse(
            ByteOrder: byteOrder,
            ProtocolMajorVersion: 11,
            ProtocolMinorVersion: 0,
            ReleaseNumber,
            ResourceIdBase,
            ResourceIdMask,
            MotionBufferSize,
            Vendor,
            MaximumRequestLength,
            ImageByteOrder: byteOrder == ByteOrder.LittleEndian ? (byte)'l' : (byte)'B',
            BitmapBitOrder: byteOrder == ByteOrder.LittleEndian ? (byte)'l' : (byte)'B',
            BitmapScanlineUnit: 32,
            BitmapScanlinePad: 32,
            MinKeycode: 8,
            MaxKeycode: 255,
            PixmapFormats:
            [
                new X11PixmapFormat(Depth: 24, BitsPerPixel: 32, ScanlinePad: 32)
            ],
            Screens:
            [
                new X11Screen(
                    RootWindowId: 1,
                    DefaultColormapId: 1,
                    WhitePixel: 0x00FF_FFFF,
                    BlackPixel: 0x0000_0000,
                    CurrentInputMasks: 0,
                    WidthInPixels: ScreenWidthInPixels,
                    HeightInPixels: ScreenHeightInPixels,
                    WidthInMillimeters: ScreenWidthInMillimeters,
                    HeightInMillimeters: ScreenHeightInMillimeters,
                    MinInstalledMaps: 1,
                    MaxInstalledMaps: 1,
                    RootVisualId: 33,
                    BackingStores: 0,
                    SaveUnders: false,
                    RootDepth: 24,
                    AllowedDepths:
                    [
                        new X11Depth(
                            Depth: 24,
                            Visuals:
                            [
                                new X11Visual(
                                    VisualId: 33,
                                    VisualClass: 4,
                                    BitsPerRgbValue: 8,
                                    ColormapEntries: 256,
                                    RedMask: 0x00FF_0000,
                                    GreenMask: 0x0000_FF00,
                                    BlueMask: 0x0000_00FF)
                            ])
                    ])
            ]);
    }
}
