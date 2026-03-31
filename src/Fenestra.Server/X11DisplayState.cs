using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Server;

public sealed class X11DisplayState
{
    private long _nextClientId;
    private readonly object _atomLock = new();
    private uint _nextDynamicAtomId;

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
        _nextDynamicAtomId = AtomTable.AtomsByName.Values.DefaultIfEmpty(0u).Max() + 1;
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

    public X11ClientState CreateClientState(ByteOrder byteOrder)
    {
        var clientId = unchecked((uint)Interlocked.Increment(ref _nextClientId));
        return new X11ClientState(clientId, ResourceIdBase, ResourceIdMask, byteOrder);
    }

    public bool TryGetWindow(uint windowId, out X11WindowDefinition? window)
    {
        if (windowId == RootWindowId)
        {
            window = new X11WindowDefinition(
                Id: RootWindowId,
                ParentId: null,
                X: 0,
                Y: 0,
                Width: ScreenWidthInPixels,
                Height: ScreenHeightInPixels,
                BorderWidth: 0,
                Depth: RootDepth);
            return true;
        }

        window = null;
        return false;
    }

    public bool TryGetGeometry(uint drawableId, out X11DrawableGeometry geometry, out X11ErrorCode? errorCode)
    {
        if (drawableId == RootWindowId)
        {
            geometry = new X11DrawableGeometry(
                RootWindowId,
                X: 0,
                Y: 0,
                Width: ScreenWidthInPixels,
                Height: ScreenHeightInPixels,
                BorderWidth: 0,
                Depth: RootDepth);
            errorCode = null;
            return true;
        }

        geometry = default;
        errorCode = X11ErrorCode.Drawable;
        return false;
    }

    public bool TryQueryTree(uint windowId, out X11QueryTreeResult result, out X11ErrorCode? errorCode)
    {
        if (windowId == RootWindowId)
        {
            result = new X11QueryTreeResult(RootWindowId, ParentWindowId: 0, ChildWindowIds: []);
            errorCode = null;
            return true;
        }

        result = default;
        errorCode = X11ErrorCode.Window;
        return false;
    }

    public uint? LookupAtom(string atomName)
    {
        ArgumentNullException.ThrowIfNull(atomName);
        return AtomTable.TryGet(atomName);
    }

    public uint InternAtom(string atomName, bool onlyIfExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atomName);

        lock (_atomLock)
        {
            if (AtomTable.TryGet(atomName) is { } existingAtom)
            {
                return existingAtom;
            }

            if (onlyIfExists)
            {
                return 0;
            }

            var atomId = _nextDynamicAtomId++;
            AtomTable.Register(atomName, atomId);
            return atomId;
        }
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

    public void Register(string name, uint atomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _atomsByName[name] = atomId;
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

public readonly record struct X11DrawableGeometry(
    uint RootWindowId,
    short X,
    short Y,
    ushort Width,
    ushort Height,
    ushort BorderWidth,
    byte Depth);

public readonly record struct X11QueryTreeResult(
    uint RootWindowId,
    uint ParentWindowId,
    IReadOnlyList<uint> ChildWindowIds);

public sealed record X11WindowDefinition(
    uint Id,
    uint? ParentId,
    short X,
    short Y,
    ushort Width,
    ushort Height,
    ushort BorderWidth,
    byte Depth);
