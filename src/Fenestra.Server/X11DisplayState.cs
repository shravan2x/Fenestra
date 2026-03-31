namespace Fenestra.Server;

public sealed class X11DisplayState
{
    private long _nextClientId;

    public X11DisplayState(
        uint resourceIdBase,
        uint resourceIdMask,
        ushort screenWidthInPixels,
        ushort screenHeightInPixels,
        ushort screenWidthInMillimeters,
        ushort screenHeightInMillimeters,
        uint rootWindowId,
        uint defaultColormapId,
        uint rootVisualId,
        uint whitePixel,
        uint blackPixel,
        byte rootDepth,
        IReadOnlyList<X11PixmapFormatDefinition> pixmapFormats,
        IReadOnlyList<X11DepthDefinition> allowedDepths,
        X11AtomTable atomTable)
    {
        if (resourceIdMask == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resourceIdMask), "Resource ID mask must be non-zero.");
        }

        ResourceIdBase = resourceIdBase;
        ResourceIdMask = resourceIdMask;
        ScreenWidthInPixels = screenWidthInPixels;
        ScreenHeightInPixels = screenHeightInPixels;
        ScreenWidthInMillimeters = screenWidthInMillimeters;
        ScreenHeightInMillimeters = screenHeightInMillimeters;
        RootWindowId = rootWindowId;
        DefaultColormapId = defaultColormapId;
        RootVisualId = rootVisualId;
        WhitePixel = whitePixel;
        BlackPixel = blackPixel;
        RootDepth = rootDepth;
        PixmapFormats = pixmapFormats ?? throw new ArgumentNullException(nameof(pixmapFormats));
        AllowedDepths = allowedDepths ?? throw new ArgumentNullException(nameof(allowedDepths));
        AtomTable = atomTable ?? throw new ArgumentNullException(nameof(atomTable));
    }

    public uint ResourceIdBase { get; }

    public uint ResourceIdMask { get; }

    public ushort ScreenWidthInPixels { get; }

    public ushort ScreenHeightInPixels { get; }

    public ushort ScreenWidthInMillimeters { get; }

    public ushort ScreenHeightInMillimeters { get; }

    public uint RootWindowId { get; }

    public uint DefaultColormapId { get; }

    public uint RootVisualId { get; }

    public uint WhitePixel { get; }

    public uint BlackPixel { get; }

    public byte RootDepth { get; }

    public IReadOnlyList<X11PixmapFormatDefinition> PixmapFormats { get; }

    public IReadOnlyList<X11DepthDefinition> AllowedDepths { get; }

    public X11AtomTable AtomTable { get; }

    public X11ClientState CreateClientState()
    {
        var clientId = unchecked((uint)Interlocked.Increment(ref _nextClientId));
        return new X11ClientState(clientId, ResourceIdBase, ResourceIdMask);
    }

    public static X11DisplayState CreateDefault()
    {
        var visual = new X11VisualDefinition(
            VisualId: 33,
            VisualClass: 4,
            BitsPerRgbValue: 8,
            ColormapEntries: 256,
            RedMask: 0x00FF_0000,
            GreenMask: 0x0000_FF00,
            BlueMask: 0x0000_00FF);

        return new X11DisplayState(
            resourceIdBase: 0x0020_0000,
            resourceIdMask: 0x001F_FFFF,
            screenWidthInPixels: 1024,
            screenHeightInPixels: 768,
            screenWidthInMillimeters: 270,
            screenHeightInMillimeters: 203,
            rootWindowId: 1,
            defaultColormapId: 1,
            rootVisualId: visual.VisualId,
            whitePixel: 0x00FF_FFFF,
            blackPixel: 0x0000_0000,
            rootDepth: 24,
            pixmapFormats:
            [
                new X11PixmapFormatDefinition(Depth: 24, BitsPerPixel: 32, ScanlinePad: 32)
            ],
            allowedDepths:
            [
                new X11DepthDefinition(
                    Depth: 24,
                    Visuals: [visual])
            ],
            atomTable: X11AtomTable.CreateDefault());
    }
}

public sealed record X11PixmapFormatDefinition(
    byte Depth,
    byte BitsPerPixel,
    byte ScanlinePad);

public sealed record X11DepthDefinition(
    byte Depth,
    IReadOnlyList<X11VisualDefinition> Visuals);

public sealed record X11VisualDefinition(
    uint VisualId,
    byte VisualClass,
    byte BitsPerRgbValue,
    ushort ColormapEntries,
    uint RedMask,
    uint GreenMask,
    uint BlueMask);

public sealed class X11AtomTable
{
    private readonly Dictionary<string, uint> _atomsByName;

    private X11AtomTable(Dictionary<string, uint> atomsByName)
    {
        _atomsByName = atomsByName;
    }

    public IReadOnlyDictionary<string, uint> AtomsByName => _atomsByName;

    public uint? TryGet(string name)
    {
        return _atomsByName.TryGetValue(name, out var atom) ? atom : null;
    }

    public static X11AtomTable CreateDefault()
    {
        return new X11AtomTable(new Dictionary<string, uint>(StringComparer.Ordinal)
        {
            ["PRIMARY"] = 1,
            ["SECONDARY"] = 2,
            ["ARC"] = 3,
            ["ATOM"] = 4,
            ["BITMAP"] = 5,
            ["CARDINAL"] = 6,
            ["COLORMAP"] = 7,
            ["CURSOR"] = 8,
            ["DRAWABLE"] = 17,
            ["WINDOW"] = 33
        });
    }
}
