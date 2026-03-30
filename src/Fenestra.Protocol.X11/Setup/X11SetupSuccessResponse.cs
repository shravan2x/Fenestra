namespace Fenestra.Protocol.X11.Setup;

public sealed record X11SetupSuccessResponse(
    ByteOrder ByteOrder,
    ushort ProtocolMajorVersion,
    ushort ProtocolMinorVersion,
    uint ReleaseNumber,
    uint ResourceIdBase,
    uint ResourceIdMask,
    uint MotionBufferSize,
    string Vendor,
    ushort MaximumRequestLength,
    byte ImageByteOrder,
    byte BitmapBitOrder,
    byte BitmapScanlineUnit,
    byte BitmapScanlinePad,
    byte MinKeycode,
    byte MaxKeycode,
    IReadOnlyList<X11PixmapFormat> PixmapFormats,
    IReadOnlyList<X11Screen> Screens);

public sealed record X11PixmapFormat(
    byte Depth,
    byte BitsPerPixel,
    byte ScanlinePad);

public sealed record X11Screen(
    uint RootWindowId,
    uint DefaultColormapId,
    uint WhitePixel,
    uint BlackPixel,
    uint CurrentInputMasks,
    ushort WidthInPixels,
    ushort HeightInPixels,
    ushort WidthInMillimeters,
    ushort HeightInMillimeters,
    ushort MinInstalledMaps,
    ushort MaxInstalledMaps,
    uint RootVisualId,
    byte BackingStores,
    bool SaveUnders,
    byte RootDepth,
    IReadOnlyList<X11Depth> AllowedDepths);

public sealed record X11Depth(
    byte Depth,
    IReadOnlyList<X11Visual> Visuals);

public sealed record X11Visual(
    uint VisualId,
    byte VisualClass,
    byte BitsPerRgbValue,
    ushort ColormapEntries,
    uint RedMask,
    uint GreenMask,
    uint BlueMask);
