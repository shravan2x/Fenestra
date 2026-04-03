using Fenestra.NativeHost.Abstractions;
using Fenestra.Protocol.X11.Core;
using Fenestra.Protocol.X11.Setup;

namespace Fenestra.Server;

public sealed class X11DisplayState
{
    private long _nextClientId;
    private readonly object _atomLock = new();
    private uint _nextDynamicAtomId;
    private readonly Dictionary<uint, X11ClientState> _clientsById = new();
    private readonly Dictionary<uint, uint> _ownerClientIdByAllocatedResourceId = new();
    private readonly Dictionary<uint, X11WindowDefinition> _windowsById = new();
    private readonly Dictionary<(uint WindowId, uint AtomId), X11PropertyValue> _propertyStore = new();
    private readonly Dictionary<uint, X11ColormapDefinition> _colormapsById = new();
    private readonly Dictionary<uint, X11CursorDefinition> _cursorsById = new();
    private readonly X11ResourceRegistry _resourceRegistry;
    private readonly X11RenderingState _renderingState;
    private readonly X11EventState _eventState;
    private readonly X11ColormapDefinition _defaultColormap;
    private readonly X11CursorDefinition _defaultCursor;

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
        _resourceRegistry = new X11ResourceRegistry();
        _renderingState = new X11RenderingState(
            rootWindowId,
            screenWidthInPixels,
            screenHeightInPixels,
            rootDepth,
            bitsPerPixel: 32);
        _eventState = new X11EventState(rootWindowId);
        _defaultColormap = new X11ColormapDefinition(defaultColormapId, rootVisualId);
        _defaultCursor = new X11CursorDefinition(0);

        RegisterCoreResources();
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

    public X11ResourceRegistry ResourceRegistry => _resourceRegistry;

    public X11ColormapDefinition DefaultColormap => _defaultColormap;

    public X11CursorDefinition DefaultCursor => _defaultCursor;

    public X11ClientState CreateClientState(ByteOrder byteOrder)
    {
        var clientId = unchecked((uint)Interlocked.Increment(ref _nextClientId));
        var clientState = new X11ClientState(clientId, ResourceIdBase, ResourceIdMask, byteOrder);
        _clientsById[clientId] = clientState;
        return clientState;
    }

    public bool TryGetWindow(uint windowId, out X11WindowDefinition? window)
    {
        return _windowsById.TryGetValue(windowId, out window);
    }

    public bool TryAddWindow(uint clientId, X11WindowDefinition window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.ParentId is not null && !_windowsById.ContainsKey(window.ParentId.Value))
        {
            return false;
        }

        if (!_resourceRegistry.TryRegister(window.Id, X11ResourceType.Window, clientId))
        {
            return false;
        }

        _windowsById[window.Id] = window;
        if (window.ParentId is { } parentId)
        {
            _windowsById[parentId].ChildWindowIds.Add(window.Id);
        }
        return true;
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
        if (_windowsById.TryGetValue(windowId, out var window))
        {
            result = new X11QueryTreeResult(
                RootWindowId,
                window.ParentId ?? 0,
                window.ChildWindowIds.ToArray());
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

    public bool IsClientResource(uint resourceId)
    {
        return _resourceRegistry.TryGet(resourceId, out _);
    }

    public bool DoesClientOwnResource(uint clientId, uint resourceId)
    {
        return _resourceRegistry.TryGet(resourceId, out var resource)
            && resource is not null
            && resource.OwnerClientId == clientId;
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

    public uint GetOrCreateAtom(string atomName)
    {
        return InternAtom(atomName, onlyIfExists: false);
    }

    public bool SetProperty(uint windowId, uint atomId, byte format, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryGetWindow(windowId, out _))
        {
            return false;
        }

        _propertyStore[(windowId, atomId)] = new X11PropertyValue(atomId, format, value.ToArray());
        return true;
    }

    public bool TryGetProperty(uint windowId, uint atomId, out X11PropertyValue propertyValue)
    {
        if (_propertyStore.TryGetValue((windowId, atomId), out var value))
        {
            propertyValue = value;
            return true;
        }

        propertyValue = null!;
        return false;
    }

    public bool DeleteProperty(uint windowId, uint atomId)
    {
        return _propertyStore.Remove((windowId, atomId));
    }

    public bool TryCreateWindow(
        uint clientId,
        uint windowId,
        uint parentId,
        short x,
        short y,
        ushort width,
        ushort height,
        ushort borderWidth,
        byte depth)
    {
        return TryAddWindow(
            clientId,
            new X11WindowDefinition(
                windowId,
                parentId,
                x,
                y,
                width,
                height,
                borderWidth,
                depth));
    }

    public bool TryDestroyWindow(uint clientId, uint windowId, out IReadOnlyList<uint> destroyedWindowIds)
    {
        destroyedWindowIds = [];

        if (windowId == RootWindowId || !_windowsById.TryGetValue(windowId, out var window))
        {
            return false;
        }

        if (!DoesClientOwnResource(clientId, windowId))
        {
            return false;
        }

        var toDestroy = new List<uint>();
        CollectDescendants(windowId, toDestroy);

        foreach (var destroyedId in toDestroy)
        {
            if (_windowsById.TryGetValue(destroyedId, out var destroyedWindow))
            {
                if (destroyedWindow.ParentId is { } parentId && _windowsById.TryGetValue(parentId, out var parentWindow))
                {
                    parentWindow.ChildWindowIds.Remove(destroyedId);
                }

                foreach (var propertyKey in _propertyStore.Keys.Where(key => key.WindowId == destroyedId).ToArray())
                {
                    _propertyStore.Remove(propertyKey);
                }

                _windowsById.Remove(destroyedId);
                _resourceRegistry.TryRemove(destroyedId, clientId);
            }
        }

        destroyedWindowIds = toDestroy;
        return true;
    }

    private void CollectDescendants(uint windowId, List<uint> destination)
    {
        destination.Add(windowId);

        if (!_windowsById.TryGetValue(windowId, out var window))
        {
            return;
        }

        foreach (var childWindowId in window.ChildWindowIds.ToArray())
        {
            CollectDescendants(childWindowId, destination);
        }
    }

    public bool TryMapWindow(uint clientId, uint windowId, out X11WindowDefinition? window)
    {
        if (!_windowsById.TryGetValue(windowId, out window) || !DoesClientOwnResource(clientId, windowId))
        {
            window = null;
            return false;
        }

        window.MapState = X11MapState.Viewable;
        return true;
    }

    public bool TryUnmapWindow(uint clientId, uint windowId, out X11WindowDefinition? window)
    {
        if (!_windowsById.TryGetValue(windowId, out window) || !DoesClientOwnResource(clientId, windowId))
        {
            window = null;
            return false;
        }

        window.MapState = X11MapState.Unmapped;
        return true;
    }

    public bool TryConfigureWindow(
        uint clientId,
        uint windowId,
        int? x,
        int? y,
        uint? width,
        uint? height,
        uint? borderWidth,
        out X11WindowDefinition? window)
    {
        if (!_windowsById.TryGetValue(windowId, out window) || !DoesClientOwnResource(clientId, windowId))
        {
            window = null;
            return false;
        }

        if (x is not null)
        {
            window.X = checked((short)x.Value);
        }

        if (y is not null)
        {
            window.Y = checked((short)y.Value);
        }

        if (width is not null)
        {
            window.Width = checked((ushort)width.Value);
        }

        if (height is not null)
        {
            window.Height = checked((ushort)height.Value);
        }

        if (borderWidth is not null)
        {
            window.BorderWidth = checked((ushort)borderWidth.Value);
        }

        return true;
    }

    public bool TryReparentWindow(
        uint clientId,
        uint windowId,
        uint newParentId,
        short x,
        short y,
        out X11WindowDefinition? window)
    {
        if (!_windowsById.TryGetValue(windowId, out window) || !DoesClientOwnResource(clientId, windowId))
        {
            window = null;
            return false;
        }

        if (!_windowsById.ContainsKey(newParentId) || newParentId == windowId || IsDescendantOf(newParentId, windowId))
        {
            return false;
        }

        if (window.ParentId is { } oldParentId && _windowsById.TryGetValue(oldParentId, out var oldParent))
        {
            oldParent.ChildWindowIds.Remove(windowId);
        }

        _windowsById[newParentId].ChildWindowIds.Add(windowId);
        window.ParentId = newParentId;
        window.X = x;
        window.Y = y;
        return true;
    }

    private bool IsDescendantOf(uint candidateWindowId, uint ancestorWindowId)
    {
        if (!_windowsById.TryGetValue(ancestorWindowId, out var ancestorWindow))
        {
            return false;
        }

        foreach (var childWindowId in ancestorWindow.ChildWindowIds)
        {
            if (childWindowId == candidateWindowId || IsDescendantOf(candidateWindowId, childWindowId))
            {
                return true;
            }
        }

        return false;
    }

    public uint CreateColormap(uint clientId)
    {
        var colormapId = AllocateClientResourceId(clientId);
        var colormap = new X11ColormapDefinition(colormapId, RootVisualId);
        _colormapsById[colormapId] = colormap;
        _resourceRegistry.TryRegister(colormapId, X11ResourceType.Colormap, clientId);
        return colormapId;
    }

    public bool TryGetColormap(uint colormapId, out X11ColormapDefinition? colormap)
    {
        return _colormapsById.TryGetValue(colormapId, out colormap);
    }

    public uint CreateCursor(uint clientId)
    {
        var cursorId = AllocateClientResourceId(clientId);
        var cursor = new X11CursorDefinition(cursorId);
        _cursorsById[cursorId] = cursor;
        _resourceRegistry.TryRegister(cursorId, X11ResourceType.Cursor, clientId);
        return cursorId;
    }

    public bool TryGetCursor(uint cursorId, out X11CursorDefinition? cursor)
    {
        return _cursorsById.TryGetValue(cursorId, out cursor);
    }

    public bool CreatePixmap(uint pixmapId, ushort width, ushort height, byte depth)
    {
        if (!_renderingState.CreatePixmap(pixmapId, width, height, depth))
        {
            return false;
        }

        var ownerClientId = InferOwnerClientId(pixmapId);
        if (ownerClientId is null)
        {
            _renderingState.FreePixmap(pixmapId);
            return false;
        }

        if (!_resourceRegistry.TryRegister(pixmapId, X11ResourceType.Pixmap, ownerClientId.Value))
        {
            _renderingState.FreePixmap(pixmapId);
            return false;
        }

        return true;
    }

    public bool FreePixmap(uint pixmapId)
    {
        if (!_renderingState.FreePixmap(pixmapId))
        {
            return false;
        }

        if (_resourceRegistry.TryGet(pixmapId, out var resource) && resource is not null)
        {
            _resourceRegistry.TryRemove(pixmapId, resource.OwnerClientId);
        }
        return true;
    }

    public bool CreateGraphicsContext(uint graphicsContextId, uint drawableId)
    {
        if (!TryGetWindow(drawableId, out _) && !_renderingState.TryGetPixmap(drawableId, out _))
        {
            return false;
        }

        if (!_renderingState.CreateGraphicsContext(graphicsContextId, drawableId))
        {
            return false;
        }

        var ownerClientId = InferOwnerClientId(graphicsContextId);
        if (ownerClientId is null)
        {
            _renderingState.FreeGraphicsContext(graphicsContextId);
            return false;
        }

        if (!_resourceRegistry.TryRegister(graphicsContextId, X11ResourceType.GraphicsContext, ownerClientId.Value))
        {
            _renderingState.FreeGraphicsContext(graphicsContextId);
            return false;
        }

        return true;
    }

    public bool FreeGraphicsContext(uint graphicsContextId)
    {
        if (!_renderingState.FreeGraphicsContext(graphicsContextId))
        {
            return false;
        }

        if (_resourceRegistry.TryGet(graphicsContextId, out var resource) && resource is not null)
        {
            _resourceRegistry.TryRemove(graphicsContextId, resource.OwnerClientId);
        }
        return true;
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

    public void BroadcastDestroyNotify(ushort sequenceNumber, uint windowId)
    {
        _eventState.BroadcastStructureEvent(
            X11EventKind.DestroyNotify,
            sequenceNumber,
            windowId,
            parentWindowId: windowId);
    }

    public void BroadcastMapNotify(ushort sequenceNumber, uint windowId)
    {
        _eventState.BroadcastStructureEvent(
            X11EventKind.MapNotify,
            sequenceNumber,
            windowId,
            parentWindowId: windowId);
    }

    public void BroadcastUnmapNotify(ushort sequenceNumber, uint windowId)
    {
        _eventState.BroadcastStructureEvent(
            X11EventKind.UnmapNotify,
            sequenceNumber,
            windowId,
            parentWindowId: windowId);
    }

    public void BroadcastReparentNotify(
        ushort sequenceNumber,
        uint windowId,
        uint newParentId,
        short x,
        short y)
    {
        _eventState.BroadcastStructureEvent(
            X11EventKind.ReparentNotify,
            sequenceNumber,
            windowId,
            newParentId,
            x: x,
            y: y);
    }

    public void BroadcastConfigureNotify(
        ushort sequenceNumber,
        uint windowId,
        short x,
        short y,
        ushort width,
        ushort height,
        ushort borderWidth)
    {
        _eventState.BroadcastStructureEvent(
            X11EventKind.ConfigureNotify,
            sequenceNumber,
            windowId,
            parentWindowId: windowId,
            x: x,
            y: y,
            width: width,
            height: height,
            borderWidth: borderWidth);
    }

    public void BroadcastPropertyNotify(ushort sequenceNumber, uint windowId, uint atomId)
    {
        _eventState.BroadcastPropertyNotify(sequenceNumber, windowId, atomId);
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

    private void RegisterCoreResources()
    {
        var rootWindow = new X11WindowDefinition(
            RootWindowId,
            null,
            0,
            0,
            ScreenWidthInPixels,
            ScreenHeightInPixels,
            0,
            RootDepth,
            X11MapState.Viewable);

        _windowsById[RootWindowId] = rootWindow;
        _resourceRegistry.TryRegister(RootWindowId, X11ResourceType.Window, ownerClientId: 0);
        _resourceRegistry.TryRegister(DefaultColormapId, X11ResourceType.Colormap, ownerClientId: 0);
        _colormapsById[DefaultColormapId] = _defaultColormap;
    }

    private uint AllocateClientResourceId(uint clientId)
    {
        if (!_clientsById.TryGetValue(clientId, out var clientState))
        {
            throw new InvalidOperationException($"Client {clientId} is not registered.");
        }

        var resourceId = clientState.AllocateXid();
        _ownerClientIdByAllocatedResourceId[resourceId] = clientId;
        return resourceId;
    }

    private uint? InferOwnerClientId(uint resourceId)
    {
        if (_ownerClientIdByAllocatedResourceId.TryGetValue(resourceId, out var trackedClientId))
        {
            return trackedClientId;
        }

        foreach (var clientState in _clientsById.Values)
        {
            if ((resourceId & ~clientState.ResourceIdMask) == clientState.ResourceIdBase)
            {
                return clientState.ClientId;
            }
        }

        return null;
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

