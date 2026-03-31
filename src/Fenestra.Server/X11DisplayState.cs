using Fenestra.NativeHost.Abstractions;
using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Server;

public sealed class X11DisplayState
{
    private long _nextClientId;
    private readonly object _atomLock = new();
    private uint _nextDynamicAtomId;
    private readonly X11RenderingState _renderingState;
    private readonly X11EventState _eventState;

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
        _renderingState = new X11RenderingState(
            rootWindowId,
            screenWidthInPixels,
            screenHeightInPixels,
            rootDepth,
            bitsPerPixel: 32);
        _eventState = new X11EventState(rootWindowId);
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

    public SoftwareFramebuffer RootFramebuffer => _renderingState.RootFramebuffer;

    public uint FocusWindowId => _eventState.FocusWindowId;

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

    public bool CreatePixmap(uint pixmapId, ushort width, ushort height, byte depth)
    {
        return _renderingState.CreatePixmap(pixmapId, width, height, depth);
    }

    public bool FreePixmap(uint pixmapId)
    {
        return _renderingState.FreePixmap(pixmapId);
    }

    public bool CreateGraphicsContext(uint graphicsContextId, uint drawableId)
    {
        if (!TryGetWindow(drawableId, out _) && !_renderingState.TryGetPixmap(drawableId, out _))
        {
            return false;
        }

        return _renderingState.CreateGraphicsContext(graphicsContextId, drawableId);
    }

    public bool FreeGraphicsContext(uint graphicsContextId)
    {
        return _renderingState.FreeGraphicsContext(graphicsContextId);
    }

    public bool TryGetGraphicsContext(uint graphicsContextId, out GraphicsContextDefinition? graphicsContext)
    {
        return _renderingState.TryGetGraphicsContext(graphicsContextId, out graphicsContext);
    }

    public bool TryGetDrawable(uint drawableId, out DrawableTarget target)
    {
        if (drawableId == RootWindowId)
        {
            target = new DrawableTarget(
                drawableId,
                RootFramebuffer.Width,
                RootFramebuffer.Height,
                RootFramebuffer.Depth,
                RootFramebuffer.BitsPerPixel,
                RootFramebuffer.StrideInBytes,
                RootFramebuffer.Pixels);
            return true;
        }

        if (_renderingState.TryGetPixmap(drawableId, out var pixmap) && pixmap is not null)
        {
            target = new DrawableTarget(
                drawableId,
                pixmap.Width,
                pixmap.Height,
                pixmap.Depth,
                pixmap.BitsPerPixel,
                pixmap.StrideInBytes,
                pixmap.Pixels);
            return true;
        }

        target = default;
        return false;
    }

    public bool TryGetImage(
        uint drawableId,
        short x,
        short y,
        ushort width,
        ushort height,
        out GetImageResult result)
    {
        if (!TryGetDrawable(drawableId, out var target))
        {
            result = default;
            return false;
        }

        if (x < 0 || y < 0)
        {
            result = default;
            return false;
        }

        if (x + width > target.Width || y + height > target.Height)
        {
            result = default;
            return false;
        }

        var bytesPerPixel = target.BitsPerPixel / 8;
        var resultStride = width * bytesPerPixel;
        var buffer = new byte[resultStride * height];

        for (var row = 0; row < height; row++)
        {
            var sourceOffset = ((y + row) * target.StrideInBytes) + (x * bytesPerPixel);
            var destinationOffset = row * resultStride;
            target.Pixels.AsSpan(sourceOffset, resultStride).CopyTo(buffer.AsSpan(destinationOffset, resultStride));
        }

        result = new GetImageResult(target.Depth, RootVisualId, buffer);
        return true;
    }

    public bool PutImage(
        uint drawableId,
        uint graphicsContextId,
        ushort width,
        ushort height,
        short dstX,
        short dstY,
        byte leftPad,
        byte depth,
        ReadOnlySpan<byte> imageBytes)
    {
        if (leftPad != 0)
        {
            return false;
        }

        if (!TryGetGraphicsContext(graphicsContextId, out var graphicsContext) || graphicsContext is null)
        {
            return false;
        }

        if (graphicsContext.DrawableId != drawableId)
        {
            return false;
        }

        if (!TryGetDrawable(drawableId, out var target))
        {
            return false;
        }

        if (depth != target.Depth || target.BitsPerPixel != 32)
        {
            return false;
        }

        if (dstX < 0 || dstY < 0)
        {
            return false;
        }

        if (dstX + width > target.Width || dstY + height > target.Height)
        {
            return false;
        }

        var bytesPerPixel = target.BitsPerPixel / 8;
        var expectedStride = width * bytesPerPixel;
        if (imageBytes.Length != expectedStride * height)
        {
            return false;
        }

        for (var row = 0; row < height; row++)
        {
            var sourceOffset = row * expectedStride;
            var destinationOffset = ((dstY + row) * target.StrideInBytes) + (dstX * bytesPerPixel);
            imageBytes.Slice(sourceOffset, expectedStride).CopyTo(target.Pixels.AsSpan(destinationOffset, expectedStride));
        }

        return true;
    }

    public bool SelectInput(uint clientId, uint windowId, uint eventMask)
    {
        if (!TryGetWindow(windowId, out _))
        {
            return false;
        }

        _eventState.SetSelection(clientId, windowId, eventMask);
        return true;
    }

    public void SetInputFocus(uint windowId)
    {
        _eventState.SetInputFocus(windowId);
    }

    public void EnqueueInputEvent(NativeInputEvent inputEvent)
    {
        _eventState.EnqueueInputEvent(inputEvent);
    }

    public IReadOnlyList<QueuedX11Event> DrainEventsForClient(X11ClientState clientState)
    {
        ArgumentNullException.ThrowIfNull(clientState);
        return _eventState.DrainEventsForClient(clientState.ClientId);
    }

    public void TranslatePendingInputEvents(ushort sequenceNumber)
    {
        _eventState.TranslatePendingInputEvents(sequenceNumber);
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
